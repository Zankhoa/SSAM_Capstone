using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SSAM_Server.Common;
using SSAM_Server.Data;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Documents;
using SSAM_Server.DTOs.Kanban;
using SSAM_Server.DTOs.Testing;
using SSAM_Server.Interfaces.Services;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.Services;

public class TestingService : ITestingService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IProjectAccessGuard _accessGuard;
    private readonly IKanbanService _kanbanService;
    private readonly TimeProvider _clock;
    private readonly IAiService _aiService;
    private readonly IFigmaService _figmaService;
    private readonly IFigmaWatchService _figmaWatchService;

    private const string RepositoryPlanName = "__REPOSITORY__";
    private const int MaxDesignsPerGeneration = 20;
    private const int AiTestCaseBatchSize = 3;
    private static readonly JsonSerializerOptions JsonOptions = ServiceMappingExtensions.DefaultJsonOptions;
    private static readonly Regex HexColorRegex = new(@"^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);

    public TestingService(
        AppDbContext db,
        ICurrentUser currentUser,
        IProjectAccessGuard accessGuard,
        IKanbanService kanbanService,
        TimeProvider clock,
        IAiService aiService,
        IFigmaService figmaService,
        IFigmaWatchService figmaWatchService)
    {
        _db = db;
        _currentUser = currentUser;
        _accessGuard = accessGuard;
        _kanbanService = kanbanService;
        _clock = clock;
        _aiService = aiService;
        _figmaService = figmaService;
        _figmaWatchService = figmaWatchService;
    }

    // =========================================================================
    // 1. Test Plans
    // =========================================================================

    public async Task<ServiceResult<ItemsDto<TestPlanDto>>> GetProjectTestPlansAsync(
        long projectId,
        CancellationToken ct = default)
    {
        var auth = await _accessGuard.EnsureProjectPermissionAsync(projectId, PermissionKeys.TestCaseView, ct);
        if (!auth.IsSuccess) return auth.Cast<ItemsDto<TestPlanDto>>();

        var planIds = await _db.TestPlans
            .AsNoTracking()
            .Where(p => p.ProjectId == projectId && p.Name != RepositoryPlanName)
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToListAsync(ct);

        var items = new List<TestPlanDto>(planIds.Count);
        foreach (var id in planIds)
        {
            var dto = await BuildPlanDtoAsync(id, ct);
            if (dto != null) items.Add(dto);
        }

        return ServiceResult<ItemsDto<TestPlanDto>>.Success(new ItemsDto<TestPlanDto>(items));
    }

    public async Task<ServiceResult<TestPlanDto>> GetTestRepositoryAsync(
        long projectId,
        CancellationToken ct = default)
    {
        var auth = await _accessGuard.EnsureProjectPermissionAsync(projectId, PermissionKeys.TestCaseView, ct);
        if (!auth.IsSuccess) return auth.Cast<TestPlanDto>();

        var repo = await _db.TestPlans
            .FirstOrDefaultAsync(p => p.ProjectId == projectId && p.Name == RepositoryPlanName, ct);

        if (repo == null)
        {
            var actorId = _currentUser.UserIdOrNull ?? 1;
            var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
            var now = _clock.GetUtcNow().UtcDateTime;
            repo = new TestPlan
            {
                ProjectId = projectId,
                Name = RepositoryPlanName,
                StartDate = today,
                EndDate = today.AddYears(1),
                Status = TestPlanStatuses.Open,
                CreatedById = actorId,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.TestPlans.Add(repo);
            await _db.SaveChangesAsync(ct);
        }

        var dto = await BuildPlanDtoAsync(repo.Id, ct);
        return dto != null
            ? ServiceResult<TestPlanDto>.Success(dto)
            : ServiceResult<TestPlanDto>.NotFound("TEST_PLAN_NOT_FOUND", "Test repository not found.");
    }

    public async Task<ServiceResult<ItemsDto<TestCaseDto>>> AddTestCasesFromRepositoryAsync(
        long planId,
        AddFromRepositoryRequest request,
        CancellationToken ct = default)
    {
        var plan = await _db.TestPlans.FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan == null)
            return ServiceResult<ItemsDto<TestCaseDto>>.NotFound("TEST_PLAN_NOT_FOUND", "Test plan not found.");

        if (plan.Name == RepositoryPlanName)
            return ServiceResult<ItemsDto<TestCaseDto>>.BadRequest("INVALID_PLAN", "Cannot import into the repository itself.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(plan.ProjectId, PermissionKeys.TestCaseManage, ct);
        if (!auth.IsSuccess) return auth.Cast<ItemsDto<TestCaseDto>>();

        if (plan.Status == TestPlanStatuses.HandedOver)
            return ServiceResult<ItemsDto<TestCaseDto>>.Conflict("PLAN_HANDED_OVER", "Test plan is already handed over.");

        var ids = (request.TestCaseIds ?? new List<long>()).Distinct().ToList();
        if (ids.Count == 0)
        {
            return ServiceResult<ItemsDto<TestCaseDto>>.ValidationFailure(new Dictionary<string, string[]>
            {
                ["testCaseIds"] = new[] { "Pick at least 1 test case." }
            });
        }

        var sources = await _db.TestCases
            .Include(c => c.Module)
            .Include(c => c.Section)
            .Include(c => c.RequirementLinks)
            .Where(c => ids.Contains(c.Id) && c.ProjectId == plan.ProjectId && c.Status == TestCaseStatuses.Active)
            .OrderBy(c => c.Position)
            .ThenBy(c => c.Id)
            .ToListAsync(ct);

        if (sources.Count == 0)
            return ServiceResult<ItemsDto<TestCaseDto>>.NotFound("TEST_CASE_NOT_FOUND", "No matching test cases found.");

        var actorId = _currentUser.UserIdOrNull ?? 1;
        var now = _clock.GetUtcNow().UtcDateTime;

        var targetModules = await _db.TestModules
            .Include(m => m.Sections)
            .Where(m => m.PlanId == planId)
            .ToListAsync(ct);

        var nextModPos = targetModules.Count > 0 ? targetModules.Max(m => m.Position) : 0;
        var createdIds = new List<long>(sources.Count);

        foreach (var src in sources)
        {
            var mod = targetModules.FirstOrDefault(m =>
                string.Equals(m.ShortCode, src.Module.ShortCode, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.Name, src.Module.Name, StringComparison.OrdinalIgnoreCase));
            if (mod == null)
            {
                mod = new TestModule
                {
                    PlanId = planId,
                    DocumentId = src.Module.DocumentId,
                    Name = src.Module.Name,
                    ShortCode = src.Module.ShortCode,
                    Position = ++nextModPos,
                    CaseSeq = 0
                };
                _db.TestModules.Add(mod);
                await _db.SaveChangesAsync(ct);
                targetModules.Add(mod);
            }

            var sec = mod.Sections.FirstOrDefault(s =>
                string.Equals(s.Title, src.Section.Title, StringComparison.OrdinalIgnoreCase));
            if (sec == null)
            {
                var nextSecPos = mod.Sections.Count > 0 ? mod.Sections.Max(s => s.Position) + 1 : 1;
                sec = new TestSection
                {
                    ModuleId = mod.Id,
                    Code = src.Section.Code,
                    Title = src.Section.Title,
                    AnchorId = src.Section.AnchorId,
                    Position = nextSecPos
                };
                _db.TestSections.Add(sec);
                await _db.SaveChangesAsync(ct);
                mod.Sections.Add(sec);
            }

            mod.CaseSeq += 1;
            var code = $"{mod.ShortCode}_{mod.CaseSeq}";
            var clone = new TestCase
            {
                ProjectId = plan.ProjectId,
                PlanId = planId,
                ModuleId = mod.Id,
                SectionId = sec.Id,
                Code = code,
                Title = src.Title,
                Preconditions = src.Preconditions,
                Steps = src.Steps,
                Actual = string.Empty,
                AiSuggestedReqs = src.AiSuggestedReqs,
                Priority = src.Priority,
                Category = src.Category,
                Browser = src.Browser,
                CustomFields = src.CustomFields,
                CellStyles = src.CellStyles,
                Status = TestCaseStatuses.Active,
                Position = mod.CaseSeq,
                CreatedById = actorId,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.TestCases.Add(clone);
            await _db.SaveChangesAsync(ct);

            foreach (var link in src.RequirementLinks)
            {
                _db.TestCaseRequirementLinks.Add(new TestCaseRequirementLink
                {
                    TestCaseId = clone.Id,
                    DocumentId = link.DocumentId,
                    ReqUid = link.ReqUid,
                    ReqHash = link.ReqHash,
                    ChangedVersionLabel = link.ChangedVersionLabel,
                    LinkedAt = now
                });
            }

            createdIds.Add(clone.Id);
        }

        _db.TestActivities.Add(new TestActivity
        {
            PlanId = planId,
            Type = TestActivityTypes.TestCase,
            ActorId = actorId,
            At = now,
            Text = $"Đã lấy {createdIds.Count} testcase từ Kho"
        });
        await _db.SaveChangesAsync(ct);

        var dtos = await LoadAndMapTestCasesAsync(plan.ProjectId, createdIds, ct);
        return ServiceResult<ItemsDto<TestCaseDto>>.Created(new ItemsDto<TestCaseDto>(dtos));
    }

    public async Task<ServiceResult<TestPlanDto>> CreateTestPlanAsync(
        long projectId,
        CreateTestPlanRequest request,
        CancellationToken ct = default)
    {
        var auth = await _accessGuard.EnsureProjectPermissionAsync(projectId, PermissionKeys.TestCaseCreate, ct);
        if (!auth.IsSuccess) return auth.Cast<TestPlanDto>();

        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
        {
            return ServiceResult<TestPlanDto>.ValidationFailure(new Dictionary<string, string[]>
            {
                ["name"] = new[] { "Plan name is required (max 200 chars)." }
            });
        }

        var startDate = ParseOptionalDate(request.StartDate);
        var endDate = ParseOptionalDate(request.EndDate);
        var userId = _currentUser.UserId;
        var now = _clock.GetUtcNow().UtcDateTime;

        var planId = await _db.ExecuteInTransactionAsync(async innerCt =>
        {
            var plan = new TestPlan
            {
                ProjectId = projectId,
                Name = name,
                StartDate = startDate,
                EndDate = endDate,
                Status = TestPlanStatuses.Open,
                SheetLayout = JsonSerializer.Serialize(new SheetLayoutDto(), JsonOptions),
                CreatedById = userId,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.TestPlans.Add(plan);
            await _db.SaveChangesAsync(innerCt);

            var envNames = (request.Environments ?? new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (envNames.Count == 0)
            {
                envNames = new List<string> { "SIT", "UAT" };
            }

            for (var i = 0; i < envNames.Count; i++)
            {
                _db.TestEnvironments.Add(new TestEnvironment
                {
                    PlanId = plan.Id,
                    Name = envNames[i],
                    Url = string.Empty,
                    Note = string.Empty,
                    Position = i + 1,
                    IsDeleted = false,
                    CreatedAt = now
                });
            }

            _db.TestActivities.Add(new TestActivity
            {
                PlanId = plan.Id,
                Type = TestActivityTypes.Setup,
                At = now,
                ActorId = userId,
                Text = $"Created test plan {name}"
            });

            await _db.SaveChangesAsync(innerCt);
            return plan.Id;
        }, ct);

        var dto = await BuildPlanDtoAsync(planId, ct);
        return ServiceResult<TestPlanDto>.Created(dto!);
    }

    public async Task<ServiceResult<TestPlanDto>> GetTestPlanAsync(
        long planId,
        CancellationToken ct = default)
    {
        var plan = await _db.TestPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan == null)
            return ServiceResult<TestPlanDto>.NotFound("TEST_PLAN_NOT_FOUND", "Test plan not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(plan.ProjectId, PermissionKeys.TestCaseView, ct);
        if (!auth.IsSuccess) return auth.Cast<TestPlanDto>();

        var dto = await BuildPlanDtoAsync(planId, ct);
        return ServiceResult<TestPlanDto>.Success(dto!);
    }

    public async Task<ServiceResult<TestPlanDto>> UpdateTestPlanAsync(
        long planId,
        UpdateTestPlanRequest request,
        CancellationToken ct = default)
    {
        var plan = await _db.TestPlans.FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan == null)
            return ServiceResult<TestPlanDto>.NotFound("TEST_PLAN_NOT_FOUND", "Test plan not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(plan.ProjectId, PermissionKeys.TestCaseUpdate, ct);
        if (!auth.IsSuccess) return auth.Cast<TestPlanDto>();

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            plan.Name = request.Name.Trim();
        }

        if (request.StartDateSpecified)
        {
            plan.StartDate = ParseOptionalDate(request.StartDate);
        }

        if (request.EndDateSpecified)
        {
            plan.EndDate = ParseOptionalDate(request.EndDate);
        }

        plan.UpdatedAt = _clock.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct);

        var dto = await BuildPlanDtoAsync(planId, ct);
        return ServiceResult<TestPlanDto>.Success(dto!);
    }

    public async Task<ServiceResult> DeleteTestPlanAsync(long planId, CancellationToken ct = default)
    {
        var plan = await _db.TestPlans.FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan == null)
            return ServiceResult.NotFound("TEST_PLAN_NOT_FOUND", "Test plan not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(plan.ProjectId, PermissionKeys.TestCaseDelete, ct);
        if (!auth.IsSuccess) return auth;

        return await _db.ExecuteInTransactionAsync(async innerCt =>
        {
            var envIds = await _db.TestEnvironments.Where(e => e.PlanId == planId).Select(e => e.Id).ToListAsync(innerCt);
            var moduleIds = await _db.TestModules.Where(m => m.PlanId == planId).Select(m => m.Id).ToListAsync(innerCt);
            var caseIds = await _db.TestCases.Where(tc => tc.PlanId == planId).Select(tc => tc.Id).ToListAsync(innerCt);

            if (caseIds.Count > 0)
            {
                await _db.TestResults.Where(r => caseIds.Contains(r.TestCaseId)).ExecuteDeleteAsync(innerCt);
                await _db.TestRetestFlags.Where(f => caseIds.Contains(f.TestCaseId)).ExecuteDeleteAsync(innerCt);
                await _db.TestCaseRequirementLinks.Where(l => caseIds.Contains(l.TestCaseId)).ExecuteDeleteAsync(innerCt);
                await _db.TestCases.Where(tc => tc.PlanId == planId).ExecuteDeleteAsync(innerCt);
            }

            await _db.TestActivities.Where(a => a.PlanId == planId).ExecuteDeleteAsync(innerCt);

            if (moduleIds.Count > 0)
            {
                await _db.TestAssignments.Where(a => moduleIds.Contains(a.ModuleId)).ExecuteDeleteAsync(innerCt);
                await _db.TestSections.Where(s => moduleIds.Contains(s.ModuleId) && s.ParentId != null).ExecuteDeleteAsync(innerCt);
                await _db.TestSections.Where(s => moduleIds.Contains(s.ModuleId)).ExecuteDeleteAsync(innerCt);
                await _db.TestModules.Where(m => m.PlanId == planId).ExecuteDeleteAsync(innerCt);
            }

            if (envIds.Count > 0)
            {
                await _db.TestBuilds.Where(b => envIds.Contains(b.EnvId)).ExecuteDeleteAsync(innerCt);
                await _db.TestEnvironments.Where(e => e.PlanId == planId).ExecuteDeleteAsync(innerCt);
            }

            _db.TestPlans.Remove(plan);
            await _db.SaveChangesAsync(innerCt);
            return ServiceResult.NoContent();
        }, ct);
    }

    // =========================================================================
    // 2. Environments & Builds
    // =========================================================================

    public async Task<ServiceResult<TestEnvironmentDto>> CreateEnvironmentAsync(
        long planId,
        CreateTestEnvironmentRequest request,
        CancellationToken ct = default)
    {
        var plan = await _db.TestPlans.FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan == null)
            return ServiceResult<TestEnvironmentDto>.NotFound("TEST_PLAN_NOT_FOUND", "Test plan not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(plan.ProjectId, PermissionKeys.TestCaseUpdate, ct);
        if (!auth.IsSuccess) return auth.Cast<TestEnvironmentDto>();

        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            return ServiceResult<TestEnvironmentDto>.ValidationFailure(new Dictionary<string, string[]>
            {
                ["name"] = new[] { "Environment name is required." }
            });
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var maxPos = await _db.TestEnvironments
            .Where(e => e.PlanId == planId && !e.IsDeleted)
            .Select(e => (int?)e.Position)
            .MaxAsync(ct) ?? 0;

        var env = new TestEnvironment
        {
            PlanId = planId,
            Name = name,
            Url = request.Url?.Trim() ?? string.Empty,
            Note = request.Note?.Trim() ?? string.Empty,
            Position = maxPos + 1,
            IsDeleted = false,
            CreatedAt = now
        };
        _db.TestEnvironments.Add(env);

        _db.TestActivities.Add(new TestActivity
        {
            PlanId = planId,
            Type = TestActivityTypes.Setup,
            At = now,
            ActorId = _currentUser.UserId,
            Text = $"Added environment {env.Name}"
        });

        plan.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);

        return ServiceResult<TestEnvironmentDto>.Created(new TestEnvironmentDto
        {
            EnvId = env.Id,
            Name = env.Name,
            Url = NullIfEmpty(env.Url),
            Note = NullIfEmpty(env.Note),
            Position = env.Position,
            CurrentBuild = null,
            Builds = new List<TestBuildDto>()
        });
    }

    public async Task<ServiceResult<TestEnvironmentDto>> UpdateEnvironmentAsync(
        long envId,
        UpdateTestEnvironmentRequest request,
        CancellationToken ct = default)
    {
        var env = await _db.TestEnvironments
            .Include(e => e.Plan)
            .Include(e => e.Builds)
                .ThenInclude(b => b.DeployedBy)
            .FirstOrDefaultAsync(e => e.Id == envId && !e.IsDeleted, ct);
        if (env == null)
            return ServiceResult<TestEnvironmentDto>.NotFound("ENVIRONMENT_NOT_FOUND", "Test environment not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(env.Plan.ProjectId, PermissionKeys.TestCaseUpdate, ct);
        if (!auth.IsSuccess) return auth.Cast<TestEnvironmentDto>();

        if (!string.IsNullOrWhiteSpace(request.Name))
            env.Name = request.Name.Trim();
        if (request.Url != null)
            env.Url = request.Url.Trim();
        if (request.Note != null)
            env.Note = request.Note.Trim();
        if (request.Position.HasValue)
            env.Position = request.Position.Value;

        env.Plan.UpdatedAt = _clock.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct);

        var dto = await MapEnvironmentDtoAsync(env, ct);
        return ServiceResult<TestEnvironmentDto>.Success(dto);
    }

    public async Task<ServiceResult> DeleteEnvironmentAsync(long envId, CancellationToken ct = default)
    {
        var env = await _db.TestEnvironments
            .Include(e => e.Plan)
            .FirstOrDefaultAsync(e => e.Id == envId && !e.IsDeleted, ct);
        if (env == null)
            return ServiceResult.NotFound("ENVIRONMENT_NOT_FOUND", "Test environment not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(env.Plan.ProjectId, PermissionKeys.TestCaseDelete, ct);
        if (!auth.IsSuccess) return auth;

        var activeEnvCount = await _db.TestEnvironments
            .CountAsync(e => e.PlanId == env.PlanId && !e.IsDeleted, ct);
        if (activeEnvCount <= 1)
        {
            return ServiceResult.Conflict("LAST_ENVIRONMENT", "At least one test environment must be maintained.");
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        env.IsDeleted = true;
        env.Plan.UpdatedAt = now;

        var assignments = await _db.TestAssignments.Where(a => a.EnvId == envId).ToListAsync(ct);
        if (assignments.Count > 0) _db.TestAssignments.RemoveRange(assignments);

        var flags = await _db.TestRetestFlags.Where(f => f.EnvId == envId).ToListAsync(ct);
        if (flags.Count > 0) _db.TestRetestFlags.RemoveRange(flags);

        _db.TestActivities.Add(new TestActivity
        {
            PlanId = env.PlanId,
            Type = TestActivityTypes.Setup,
            At = now,
            ActorId = _currentUser.UserId,
            Text = $"Deleted environment {env.Name}"
        });

        await _db.SaveChangesAsync(ct);
        return ServiceResult.NoContent();
    }

    public async Task<ServiceResult<DeployTestBuildResponse>> DeployBuildAsync(
        long envId,
        DeployTestBuildRequest request,
        CancellationToken ct = default)
    {
        var env = await _db.TestEnvironments
            .Include(e => e.Plan)
            .FirstOrDefaultAsync(e => e.Id == envId && !e.IsDeleted, ct);
        if (env == null)
            return ServiceResult<DeployTestBuildResponse>.NotFound("ENVIRONMENT_NOT_FOUND", "Test environment not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(env.Plan.ProjectId, PermissionKeys.TestRunExecute, ct);
        if (!auth.IsSuccess) return auth.Cast<DeployTestBuildResponse>();

        var version = request.Version?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(version))
        {
            return ServiceResult<DeployTestBuildResponse>.ValidationFailure(new Dictionary<string, string[]>
            {
                ["version"] = new[] { "Build version is required." }
            });
        }

        var fixedIssueIds = (request.FixedIssueIds ?? new List<long>()).Distinct().ToList();
        var userId = _currentUser.UserId;
        var now = _clock.GetUtcNow().UtcDateTime;

        var issueKeyMap = fixedIssueIds.Count == 0
            ? new Dictionary<long, string>()
            : await _db.Issues
                .AsNoTracking()
                .Where(i => fixedIssueIds.Contains(i.Id))
                .ToDictionaryAsync(i => i.Id, i => i.Key, ct);

        var build = new TestBuild
        {
            EnvId = envId,
            Version = version,
            DeployedAt = now,
            DeployedById = userId,
            FixedIssueIds = JsonSerializer.Serialize(fixedIssueIds)
        };
        _db.TestBuilds.Add(build);

        int retestCount = 0;
        if (fixedIssueIds.Count > 0)
        {
            var matchingCases = await _db.TestCases
                .Where(tc => tc.PlanId == env.PlanId &&
                             tc.Status == TestCaseStatuses.Active &&
                             tc.BugIssueId.HasValue &&
                             fixedIssueIds.Contains(tc.BugIssueId.Value))
                .ToListAsync(ct);

            var caseIds = matchingCases.Select(c => c.Id).ToList();
            var testedCaseIds = caseIds.Count == 0
                ? new HashSet<long>()
                : (await _db.TestResults
                    .AsNoTracking()
                    .Where(r => r.EnvId == envId && caseIds.Contains(r.TestCaseId))
                    .Select(r => r.TestCaseId)
                    .Distinct()
                    .ToListAsync(ct)).ToHashSet();

            var existingFlags = await _db.TestRetestFlags
                .Where(f => f.EnvId == envId && caseIds.Contains(f.TestCaseId))
                .ToListAsync(ct);
            var flagByCase = existingFlags.ToDictionary(f => f.TestCaseId);

            foreach (var tc in matchingCases)
            {
                if (!testedCaseIds.Contains(tc.Id))
                    continue;

                if (flagByCase.TryGetValue(tc.Id, out var existing))
                {
                    existing.Reason = TestRetestReasons.BugFixed;
                    existing.BugIssueId = tc.BugIssueId;
                    existing.Build = version;
                    existing.ReqKey = null;
                    existing.At = now;
                }
                else
                {
                    _db.TestRetestFlags.Add(new TestRetestFlag
                    {
                        TestCaseId = tc.Id,
                        EnvId = envId,
                        Reason = TestRetestReasons.BugFixed,
                        BugIssueId = tc.BugIssueId,
                        Build = version,
                        At = now
                    });
                }

                tc.UpdatedAt = now;
                retestCount++;
            }
        }

        var fixedKeysText = fixedIssueIds
            .Select(id => issueKeyMap.GetValueOrDefault(id, $"#{id}"))
            .ToList();

        var suffix = fixedKeysText.Count > 0
            ? $" (fixed {string.Join(", ", fixedKeysText)} → {retestCount} case(s) need retest)"
            : string.Empty;

        _db.TestActivities.Add(new TestActivity
        {
            PlanId = env.PlanId,
            Type = TestActivityTypes.Build,
            At = now,
            ActorId = userId,
            EnvId = envId,
            Build = version,
            Text = $"Deployed build {version} to {env.Name}{suffix}"
        });

        env.Plan.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);

        var actorRef = await GetUserRefAsync(userId, ct);
        var buildDto = new TestBuildDto
        {
            BuildId = build.Id,
            Version = build.Version,
            DeployedAt = FormatUtc(build.DeployedAt),
            DeployedBy = actorRef,
            FixedBugs = fixedIssueIds.Select(id => new IssueKeyRefDto
            {
                IssueId = id,
                Key = issueKeyMap.GetValueOrDefault(id, $"#{id}")
            }).ToList(),
            ResultCount = 0
        };

        return ServiceResult<DeployTestBuildResponse>.Created(new DeployTestBuildResponse
        {
            Build = buildDto,
            RetestCount = retestCount
        });
    }

    public async Task<ServiceResult<TriggerRegressionResponse>> TriggerRegressionAsync(
        long envId,
        TriggerRegressionRequest request,
        CancellationToken ct = default)
    {
        var env = await _db.TestEnvironments
            .Include(e => e.Plan)
            .Include(e => e.Builds)
            .FirstOrDefaultAsync(e => e.Id == envId && !e.IsDeleted, ct);
        if (env == null)
            return ServiceResult<TriggerRegressionResponse>.NotFound("ENVIRONMENT_NOT_FOUND", "Test environment not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(env.Plan.ProjectId, PermissionKeys.TestRunExecute, ct);
        if (!auth.IsSuccess) return auth.Cast<TriggerRegressionResponse>();

        var latestBuild = env.Builds
            .OrderByDescending(b => b.DeployedAt)
            .ThenByDescending(b => b.Id)
            .FirstOrDefault()?.Version;

        var casesQuery = _db.TestCases
            .Include(tc => tc.Results.Where(r => r.EnvId == envId))
            .Where(tc => tc.PlanId == env.PlanId && tc.Status == TestCaseStatuses.Active);

        if (request.ModuleIds != null && request.ModuleIds.Count > 0)
        {
            var moduleSet = request.ModuleIds.Distinct().ToList();
            casesQuery = casesQuery.Where(tc => moduleSet.Contains(tc.ModuleId));
        }

        var cases = await casesQuery.ToListAsync(ct);
        var caseIds = cases.Select(c => c.Id).ToList();

        var existingFlags = await _db.TestRetestFlags
            .Where(f => f.EnvId == envId && caseIds.Contains(f.TestCaseId))
            .ToListAsync(ct);
        var flagByCase = existingFlags.ToDictionary(f => f.TestCaseId);

        var now = _clock.GetUtcNow().UtcDateTime;
        var retestCount = 0;

        foreach (var tc in cases)
        {
            var latestRes = tc.Results
                .OrderByDescending(r => r.At)
                .ThenByDescending(r => r.Id)
                .FirstOrDefault();

            if (latestRes == null || latestRes.Result != TestResultValues.Pass)
                continue;

            if (flagByCase.TryGetValue(tc.Id, out var existing))
            {
                existing.Reason = TestRetestReasons.Regression;
                existing.BugIssueId = null;
                existing.Build = latestBuild;
                existing.ReqKey = null;
                existing.At = now;
            }
            else
            {
                _db.TestRetestFlags.Add(new TestRetestFlag
                {
                    TestCaseId = tc.Id,
                    EnvId = envId,
                    Reason = TestRetestReasons.Regression,
                    Build = latestBuild,
                    At = now
                });
            }

            tc.UpdatedAt = now;
            retestCount++;
        }

        _db.TestActivities.Add(new TestActivity
        {
            PlanId = env.PlanId,
            Type = TestActivityTypes.Build,
            At = now,
            ActorId = _currentUser.UserId,
            EnvId = envId,
            Build = latestBuild,
            Text = $"Marked regression on {env.Name} ({retestCount} PASS case(s) need retest)"
        });

        env.Plan.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);

        return ServiceResult<TriggerRegressionResponse>.Success(new TriggerRegressionResponse
        {
            RetestCount = retestCount
        });
    }

    // =========================================================================
    // 3. Modules & Sections
    // =========================================================================

    public async Task<ServiceResult<TestModuleDto>> CreateModuleAsync(
        long planId,
        CreateTestModuleRequest request,
        CancellationToken ct = default)
    {
        var plan = await _db.TestPlans
            .Include(p => p.Modules)
            .FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan == null)
            return ServiceResult<TestModuleDto>.NotFound("TEST_PLAN_NOT_FOUND", "Test plan not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(plan.ProjectId, PermissionKeys.TestCaseCreate, ct);
        if (!auth.IsSuccess) return auth.Cast<TestModuleDto>();

        DocumentNode? doc = null;
        if (request.DocumentId.HasValue && request.DocumentId.Value > 0)
        {
            doc = await _db.DocumentNodes
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == request.DocumentId.Value && d.ProjectId == plan.ProjectId && !d.IsDeleted, ct);
            if (doc == null)
                return ServiceResult<TestModuleDto>.NotFound("DOCUMENT_NOT_FOUND", "Document not found in project.");
        }

        var name = !string.IsNullOrWhiteSpace(request.Name)
            ? request.Name.Trim()
            : (doc?.Title ?? "Module");

        var existingCodes = plan.Modules.Select(m => m.ShortCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var shortCode = !string.IsNullOrWhiteSpace(request.ShortCode)
            ? request.ShortCode.Trim().ToUpperInvariant()
            : MakeShortCode(name, existingCodes);

        var maxPos = plan.Modules.Count == 0 ? 0 : plan.Modules.Max(m => m.Position);
        var now = _clock.GetUtcNow().UtcDateTime;

        var module = new TestModule
        {
            PlanId = planId,
            DocumentId = doc?.Id,
            Name = name,
            ShortCode = shortCode,
            Position = maxPos + 1,
            CaseSeq = 0
        };
        _db.TestModules.Add(module);
        await _db.SaveChangesAsync(ct);

        // Auto-create top-level sections from document headings if available
        var docSections = new List<DocSectionDto>();
        if (doc != null)
        {
            var targetVerId = doc.BaselineVersionId ?? doc.CurrentVersionId;
            if (targetVerId.HasValue)
            {
                var ver = await _db.DocumentVersions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(v => v.Id == targetVerId.Value, ct);
                if (ver != null)
                {
                    var allHeadings = DocumentContentHelper.ExtractDocSections(ver.Content);
                    if (request.AnchorIds != null && request.AnchorIds.Count > 0)
                    {
                        var anchorSet = request.AnchorIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
                        docSections = allHeadings.Where(s => anchorSet.Contains(s.AnchorId)).ToList();
                    }
                    else
                    {
                        docSections = DefaultModuleSections(allHeadings);
                    }
                }
            }
        }

        if (docSections.Count > 0)
        {
            for (var i = 0; i < docSections.Count; i++)
            {
                var ds = docSections[i];
                _db.TestSections.Add(new TestSection
                {
                    ModuleId = module.Id,
                    ParentId = null,
                    Code = $"{i + 1}.",
                    Title = StripHeadingNumber(ds.Title),
                    AnchorId = ds.AnchorId,
                    Position = i + 1
                });
            }
        }
        else
        {
            _db.TestSections.Add(new TestSection
            {
                ModuleId = module.Id,
                ParentId = null,
                Code = "1.",
                Title = name,
                AnchorId = null,
                Position = 1
            });
        }

        _db.TestActivities.Add(new TestActivity
        {
            PlanId = planId,
            Type = TestActivityTypes.Setup,
            At = now,
            ActorId = _currentUser.UserId,
            ModuleId = module.Id,
            Text = $"Added module {module.Name} ({doc?.Code ?? $"DOC-{doc?.Id ?? 0}"})"
        });

        plan.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);

        var dto = await BuildSingleModuleDtoAsync(module.Id, ct);
        return ServiceResult<TestModuleDto>.Created(dto!);
    }

    public async Task<ServiceResult<TestModuleDto>> UpdateModuleAsync(
        long moduleId,
        UpdateTestModuleRequest request,
        CancellationToken ct = default)
    {
        var module = await _db.TestModules
            .Include(m => m.Plan)
            .FirstOrDefaultAsync(m => m.Id == moduleId, ct);
        if (module == null)
            return ServiceResult<TestModuleDto>.NotFound("MODULE_NOT_FOUND", "Test module not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(module.Plan.ProjectId, PermissionKeys.TestCaseUpdate, ct);
        if (!auth.IsSuccess) return auth.Cast<TestModuleDto>();

        if (!string.IsNullOrWhiteSpace(request.Name))
            module.Name = request.Name.Trim();
        if (!string.IsNullOrWhiteSpace(request.ShortCode))
            module.ShortCode = request.ShortCode.Trim().ToUpperInvariant();
        if (request.Position.HasValue)
            module.Position = request.Position.Value;

        module.Plan.UpdatedAt = _clock.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct);

        var dto = await BuildSingleModuleDtoAsync(module.Id, ct);
        return ServiceResult<TestModuleDto>.Success(dto!);
    }

    public async Task<ServiceResult> DeleteModuleAsync(long moduleId, CancellationToken ct = default)
    {
        var module = await _db.TestModules
            .Include(m => m.Plan)
            .FirstOrDefaultAsync(m => m.Id == moduleId, ct);
        if (module == null)
            return ServiceResult.NotFound("MODULE_NOT_FOUND", "Test module not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(module.Plan.ProjectId, PermissionKeys.TestCaseDelete, ct);
        if (!auth.IsSuccess) return auth;

        var now = _clock.GetUtcNow().UtcDateTime;

        return await _db.ExecuteInTransactionAsync(async innerCt =>
        {
            var caseIds = await _db.TestCases
                .Where(tc => tc.ModuleId == moduleId)
                .Select(tc => tc.Id)
                .ToListAsync(innerCt);

            if (caseIds.Count > 0)
            {
                await _db.TestActivities.Where(a => a.TestCaseId.HasValue && caseIds.Contains(a.TestCaseId.Value))
                    .ExecuteUpdateAsync(s => s.SetProperty(a => a.TestCaseId, (long?)null), innerCt);
                await _db.TestResults.Where(r => caseIds.Contains(r.TestCaseId)).ExecuteDeleteAsync(innerCt);
                await _db.TestRetestFlags.Where(f => caseIds.Contains(f.TestCaseId)).ExecuteDeleteAsync(innerCt);
                await _db.TestCaseRequirementLinks.Where(l => caseIds.Contains(l.TestCaseId)).ExecuteDeleteAsync(innerCt);
                await _db.TestCases.Where(tc => tc.ModuleId == moduleId).ExecuteDeleteAsync(innerCt);
            }

            await _db.TestActivities.Where(a => a.ModuleId == moduleId)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.ModuleId, (long?)null), innerCt);

            await _db.TestAssignments.Where(a => a.ModuleId == moduleId).ExecuteDeleteAsync(innerCt);
            await _db.TestSections.Where(s => s.ModuleId == moduleId && s.ParentId != null).ExecuteDeleteAsync(innerCt);
            await _db.TestSections.Where(s => s.ModuleId == moduleId).ExecuteDeleteAsync(innerCt);

            _db.TestModules.Remove(module);
            module.Plan.UpdatedAt = now;
            await _db.SaveChangesAsync(innerCt);

            return ServiceResult.NoContent();
        }, ct);
    }

    public async Task<ServiceResult<TestSectionDto>> CreateSectionAsync(
        long moduleId,
        CreateTestSectionRequest request,
        CancellationToken ct = default)
    {
        var module = await _db.TestModules
            .Include(m => m.Plan)
            .Include(m => m.Sections)
            .FirstOrDefaultAsync(m => m.Id == moduleId, ct);
        if (module == null)
            return ServiceResult<TestSectionDto>.NotFound("MODULE_NOT_FOUND", "Test module not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(module.Plan.ProjectId, PermissionKeys.TestCaseCreate, ct);
        if (!auth.IsSuccess) return auth.Cast<TestSectionDto>();

        var title = request.Title?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(title))
        {
            return ServiceResult<TestSectionDto>.ValidationFailure(new Dictionary<string, string[]>
            {
                ["title"] = new[] { "Section title is required." }
            });
        }

        TestSection? parent = null;
        if (request.ParentId.HasValue)
        {
            parent = module.Sections.FirstOrDefault(s => s.Id == request.ParentId.Value);
            if (parent == null || parent.ParentId.HasValue)
                return ServiceResult<TestSectionDto>.BadRequest("INVALID_PARENT", "Sections support at most 2 levels.");
        }

        var siblings = module.Sections.Where(s => s.ParentId == request.ParentId).ToList();
        var nextIdx = siblings.Count + 1;
        var maxPos = siblings.Count == 0 ? 0 : siblings.Max(s => s.Position);
        var code = parent != null
            ? $"{parent.Code.TrimEnd('.')}.{nextIdx}"
            : $"{nextIdx}.";

        var section = new TestSection
        {
            ModuleId = moduleId,
            ParentId = request.ParentId,
            Code = code,
            Title = title,
            AnchorId = string.IsNullOrWhiteSpace(request.AnchorId) ? null : request.AnchorId.Trim(),
            Position = maxPos + 1
        };
        _db.TestSections.Add(section);
        module.Plan.UpdatedAt = _clock.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct);

        await RecodeModuleSectionsAsync(moduleId, ct);

        var reloaded = await _db.TestSections.AsNoTracking().FirstAsync(s => s.Id == section.Id, ct);
        return ServiceResult<TestSectionDto>.Created(new TestSectionDto
        {
            SectionId = reloaded.Id,
            ModuleId = reloaded.ModuleId,
            ParentId = reloaded.ParentId,
            Code = reloaded.Code,
            Title = reloaded.Title,
            AnchorId = reloaded.AnchorId,
            ReqChanged = false,
            Position = reloaded.Position
        });
    }

    public async Task<ServiceResult<TestSectionDto>> UpdateSectionAsync(
        long sectionId,
        UpdateTestSectionRequest request,
        CancellationToken ct = default)
    {
        var section = await _db.TestSections
            .Include(s => s.Module)
                .ThenInclude(m => m.Plan)
            .FirstOrDefaultAsync(s => s.Id == sectionId, ct);
        if (section == null)
            return ServiceResult<TestSectionDto>.NotFound("SECTION_NOT_FOUND", "Test section not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(section.Module.Plan.ProjectId, PermissionKeys.TestCaseUpdate, ct);
        if (!auth.IsSuccess) return auth.Cast<TestSectionDto>();

        if (!string.IsNullOrWhiteSpace(request.Title))
            section.Title = request.Title.Trim();
        if (request.AnchorId != null)
            section.AnchorId = string.IsNullOrWhiteSpace(request.AnchorId) ? null : request.AnchorId.Trim();
        if (request.Position.HasValue)
            section.Position = request.Position.Value;

        section.Module.Plan.UpdatedAt = _clock.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct);

        await RecodeModuleSectionsAsync(section.ModuleId, ct);

        var reloaded = await _db.TestSections.AsNoTracking().FirstAsync(s => s.Id == section.Id, ct);
        return ServiceResult<TestSectionDto>.Success(new TestSectionDto
        {
            SectionId = reloaded.Id,
            ModuleId = reloaded.ModuleId,
            ParentId = reloaded.ParentId,
            Code = reloaded.Code,
            Title = reloaded.Title,
            AnchorId = reloaded.AnchorId,
            ReqChanged = false,
            Position = reloaded.Position
        });
    }

    public async Task<ServiceResult> DeleteSectionAsync(long sectionId, CancellationToken ct = default)
    {
        var section = await _db.TestSections
            .Include(s => s.Module)
                .ThenInclude(m => m.Plan)
            .FirstOrDefaultAsync(s => s.Id == sectionId, ct);
        if (section == null)
            return ServiceResult.NotFound("SECTION_NOT_FOUND", "Test section not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(section.Module.Plan.ProjectId, PermissionKeys.TestCaseDelete, ct);
        if (!auth.IsSuccess) return auth;

        var moduleId = section.ModuleId;
        var now = _clock.GetUtcNow().UtcDateTime;

        await _db.ExecuteInTransactionAsync(async innerCt =>
        {
            var childIds = await _db.TestSections
                .Where(s => s.ParentId == sectionId)
                .Select(s => s.Id)
                .ToListAsync(innerCt);

            var allSecIds = new List<long>(childIds) { sectionId };

            var caseIds = await _db.TestCases
                .Where(tc => allSecIds.Contains(tc.SectionId))
                .Select(tc => tc.Id)
                .ToListAsync(innerCt);

            if (caseIds.Count > 0)
            {
                await _db.TestActivities.Where(a => a.TestCaseId.HasValue && caseIds.Contains(a.TestCaseId.Value))
                    .ExecuteUpdateAsync(s => s.SetProperty(a => a.TestCaseId, (long?)null), innerCt);
                await _db.TestResults.Where(r => caseIds.Contains(r.TestCaseId)).ExecuteDeleteAsync(innerCt);
                await _db.TestRetestFlags.Where(f => caseIds.Contains(f.TestCaseId)).ExecuteDeleteAsync(innerCt);
                await _db.TestCaseRequirementLinks.Where(l => caseIds.Contains(l.TestCaseId)).ExecuteDeleteAsync(innerCt);
                await _db.TestCases.Where(tc => allSecIds.Contains(tc.SectionId)).ExecuteDeleteAsync(innerCt);
            }

            if (childIds.Count > 0)
            {
                await _db.TestSections.Where(s => childIds.Contains(s.Id)).ExecuteDeleteAsync(innerCt);
            }

            await _db.TestSections.Where(s => s.Id == sectionId).ExecuteDeleteAsync(innerCt);
            section.Module.Plan.UpdatedAt = now;
            await _db.SaveChangesAsync(innerCt);
            return true;
        }, ct);

        await RecodeModuleSectionsAsync(moduleId, ct);
        return ServiceResult.NoContent();
    }

    // =========================================================================
    // 4. Assignments & Sheet Layout
    // =========================================================================

    public async Task<ServiceResult<ItemsDto<TestAssignmentDto>>> SetAssignmentAsync(
        long planId,
        SetTestAssignmentRequest request,
        CancellationToken ct = default)
    {
        var plan = await _db.TestPlans.FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan == null)
            return ServiceResult<ItemsDto<TestAssignmentDto>>.NotFound("TEST_PLAN_NOT_FOUND", "Test plan not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(plan.ProjectId, PermissionKeys.TestCaseUpdate, ct);
        if (!auth.IsSuccess) return auth.Cast<ItemsDto<TestAssignmentDto>>();

        var items = request.Items ?? new List<AssignmentInputItemDto>();
        if (items.Count == 0 && request.ModuleId.HasValue && request.EnvId.HasValue)
        {
            items.Add(new AssignmentInputItemDto
            {
                ModuleId = request.ModuleId.Value,
                EnvId = request.EnvId.Value,
                AssigneeId = request.AssigneeId
            });
        }

        var moduleIds = await _db.TestModules
            .Where(m => m.PlanId == planId)
            .ToDictionaryAsync(m => m.Id, m => m.Name, ct);

        var envNames = await _db.TestEnvironments
            .Where(e => e.PlanId == planId && !e.IsDeleted)
            .ToDictionaryAsync(e => e.Id, e => e.Name, ct);

        var existingList = await _db.TestAssignments
            .Where(a => moduleIds.Keys.Contains(a.ModuleId))
            .ToListAsync(ct);

        var now = _clock.GetUtcNow().UtcDateTime;
        var userId = _currentUser.UserId;

        foreach (var item in items)
        {
            if (!moduleIds.ContainsKey(item.ModuleId) || !envNames.ContainsKey(item.EnvId))
                continue;

            var existing = existingList.FirstOrDefault(a => a.ModuleId == item.ModuleId && a.EnvId == item.EnvId);
            if (!item.AssigneeId.HasValue || item.AssigneeId.Value <= 0)
            {
                if (existing != null)
                {
                    _db.TestAssignments.Remove(existing);
                    existingList.Remove(existing);
                    _db.TestActivities.Add(new TestActivity
                    {
                        PlanId = planId,
                        Type = TestActivityTypes.Assign,
                        At = now,
                        ActorId = userId,
                        EnvId = item.EnvId,
                        ModuleId = item.ModuleId,
                        Text = $"Unassigned {moduleIds[item.ModuleId]} on {envNames[item.EnvId]}"
                    });
                }
            }
            else
            {
                var changed = existing == null || existing.AssigneeId != item.AssigneeId.Value;
                if (existing == null)
                {
                    var created = new TestAssignment
                    {
                        ModuleId = item.ModuleId,
                        EnvId = item.EnvId,
                        AssigneeId = item.AssigneeId.Value
                    };
                    _db.TestAssignments.Add(created);
                    existingList.Add(created);
                }
                else
                {
                    existing.AssigneeId = item.AssigneeId.Value;
                }

                if (changed)
                {
                    var assigneeUser = await GetUserRefAsync(item.AssigneeId.Value, ct);
                    _db.TestActivities.Add(new TestActivity
                    {
                        PlanId = planId,
                        Type = TestActivityTypes.Assign,
                        At = now,
                        ActorId = userId,
                        EnvId = item.EnvId,
                        ModuleId = item.ModuleId,
                        Text = $"Assigned {moduleIds[item.ModuleId]} on {envNames[item.EnvId]} to {assigneeUser.FullName}"
                    });
                }
            }
        }

        plan.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);

        var updatedAssigns = await _db.TestAssignments
            .AsNoTracking()
            .Include(a => a.Assignee)
            .Where(a => moduleIds.Keys.Contains(a.ModuleId))
            .Select(a => new TestAssignmentDto
            {
                ModuleId = a.ModuleId,
                EnvId = a.EnvId,
                Assignee = MapUserRef(a.Assignee)
            })
            .ToListAsync(ct);

        return ServiceResult<ItemsDto<TestAssignmentDto>>.Success(new ItemsDto<TestAssignmentDto>(updatedAssigns));
    }

    public async Task<ServiceResult<SheetLayoutDto>> SaveSheetLayoutAsync(
        long planId,
        SheetLayoutDto request,
        CancellationToken ct = default)
    {
        var plan = await _db.TestPlans.FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan == null)
            return ServiceResult<SheetLayoutDto>.NotFound("TEST_PLAN_NOT_FOUND", "Test plan not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(plan.ProjectId, PermissionKeys.TestCaseUpdate, ct);
        if (!auth.IsSuccess) return auth.Cast<SheetLayoutDto>();

        var layout = NormalizeLayout(request);
        plan.SheetLayout = JsonSerializer.Serialize(layout, JsonOptions);
        plan.UpdatedAt = _clock.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct);

        return ServiceResult<SheetLayoutDto>.Success(layout);
    }

    // =========================================================================
    // 5. Test Cases
    // =========================================================================

    public async Task<ServiceResult<ItemsDto<TestCaseDto>>> ListProjectTestCasesAsync(
        long projectId,
        ListTestCasesQuery query,
        CancellationToken ct = default)
    {
        var auth = await _accessGuard.EnsureProjectPermissionAsync(projectId, PermissionKeys.TestCaseView, ct);
        if (!auth.IsSuccess) return auth.Cast<ItemsDto<TestCaseDto>>();

        var dbQuery = _db.TestCases
            .AsNoTracking()
            .Where(tc => tc.ProjectId == projectId);

        if (query.PlanId.HasValue)
            dbQuery = dbQuery.Where(tc => tc.PlanId == query.PlanId.Value);

        if (query.ModuleId.HasValue)
            dbQuery = dbQuery.Where(tc => tc.ModuleId == query.ModuleId.Value);

        if (!query.IncludeDeleted)
            dbQuery = dbQuery.Where(tc => tc.Status == TestCaseStatuses.Active);

        if (query.DocumentId.HasValue)
        {
            var docId = query.DocumentId.Value;
            if (!string.IsNullOrWhiteSpace(query.ReqUid))
            {
                var uid = query.ReqUid.Trim().ToUpperInvariant();
                dbQuery = dbQuery.Where(tc => tc.RequirementLinks.Any(l => l.DocumentId == docId && l.ReqUid == uid));
            }
            else
            {
                dbQuery = dbQuery.Where(tc =>
                    tc.Module.DocumentId == docId ||
                    tc.RequirementLinks.Any(l => l.DocumentId == docId));
            }
        }
        else if (!string.IsNullOrWhiteSpace(query.ReqUid))
        {
            var uid = query.ReqUid.Trim().ToUpperInvariant();
            dbQuery = dbQuery.Where(tc => tc.RequirementLinks.Any(l => l.ReqUid == uid));
        }

        var ids = await dbQuery
            .OrderBy(tc => tc.ModuleId)
            .ThenBy(tc => tc.SectionId)
            .ThenBy(tc => tc.Position)
            .ThenBy(tc => tc.Id)
            .Select(tc => tc.Id)
            .ToListAsync(ct);

        var dtos = await LoadAndMapTestCasesAsync(projectId, ids, ct);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            dtos = dtos.Where(c =>
                c.Code.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                c.ModuleCode.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                c.Title.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                (c.Preconditions != null && c.Preconditions.Contains(s, StringComparison.OrdinalIgnoreCase)) ||
                (c.Actual != null && c.Actual.Contains(s, StringComparison.OrdinalIgnoreCase)) ||
                c.Steps.Any(st => st.Action.Contains(s, StringComparison.OrdinalIgnoreCase) || st.Expected.Contains(s, StringComparison.OrdinalIgnoreCase)) ||
                c.Requirements.Any(r =>
                    (r.ReqKey != null && r.ReqKey.Contains(s, StringComparison.OrdinalIgnoreCase)) ||
                    (r.Title != null && r.Title.Contains(s, StringComparison.OrdinalIgnoreCase))) ||
                (c.Bug != null && (c.Bug.Key.Contains(s, StringComparison.OrdinalIgnoreCase) || c.Bug.Title.Contains(s, StringComparison.OrdinalIgnoreCase)))
            ).ToList();
        }

        return ServiceResult<ItemsDto<TestCaseDto>>.Success(new ItemsDto<TestCaseDto>(dtos));
    }

    public async Task<ServiceResult<TestCaseDto>> GetTestCaseAsync(
        long testCaseId,
        CancellationToken ct = default)
    {
        var tc = await _db.TestCases
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == testCaseId, ct);
        if (tc == null)
            return ServiceResult<TestCaseDto>.NotFound("TEST_CASE_NOT_FOUND", "Test case not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(tc.ProjectId, PermissionKeys.TestCaseView, ct);
        if (!auth.IsSuccess) return auth.Cast<TestCaseDto>();

        var dtos = await LoadAndMapTestCasesAsync(tc.ProjectId, new List<long> { testCaseId }, ct);
        return ServiceResult<TestCaseDto>.Success(dtos.First());
    }

    public async Task<ServiceResult<TestCaseDto>> CreateProjectTestCaseAsync(
        long projectId,
        CreateTestCaseRequest request,
        CancellationToken ct = default)
    {
        var section = await _db.TestSections
            .AsNoTracking()
            .Include(s => s.Module)
                .ThenInclude(m => m.Plan)
            .FirstOrDefaultAsync(s => s.Id == request.SectionId && s.Module.Plan.ProjectId == projectId, ct);
        if (section == null)
            return ServiceResult<TestCaseDto>.NotFound("SECTION_NOT_FOUND", "Test section not found in project.");

        return await CreateTestCaseAsync(section.Module.PlanId, request, ct);
    }

    public async Task<ServiceResult<TestCaseDto>> CreateTestCaseAsync(
        long planId,
        CreateTestCaseRequest request,
        CancellationToken ct = default)
    {
        var plan = await _db.TestPlans.FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan == null)
            return ServiceResult<TestCaseDto>.NotFound("TEST_PLAN_NOT_FOUND", "Test plan not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(plan.ProjectId, PermissionKeys.TestCaseCreate, ct);
        if (!auth.IsSuccess) return auth.Cast<TestCaseDto>();

        var section = await _db.TestSections
            .Include(s => s.Module)
            .FirstOrDefaultAsync(s => s.Id == request.SectionId && s.Module.PlanId == planId, ct);
        if (section == null)
            return ServiceResult<TestCaseDto>.NotFound("SECTION_NOT_FOUND", "Test section not found in plan.");

        var priority = TestCasePriorities.Normalize(request.Priority);
        var category = request.Category != null
            ? request.Category.Trim()
            : (section.ParentId.HasValue ? section.Title : string.Empty);

        var code = await AllocateNextTcCodeAsync(planId, ct);
        section.Module.CaseSeq += 1;

        var siblings = await _db.TestCases
            .Where(tc => tc.SectionId == section.Id)
            .OrderBy(tc => tc.Position)
            .ThenBy(tc => tc.Id)
            .ToListAsync(ct);

        int newPos;
        if (request.AfterTestCaseId.HasValue)
        {
            var afterCase = siblings.FirstOrDefault(x => x.Id == request.AfterTestCaseId.Value);
            if (afterCase != null)
            {
                newPos = afterCase.Position + 1;
                foreach (var s in siblings.Where(x => x.Position >= newPos))
                {
                    s.Position += 1;
                }
            }
            else
            {
                newPos = (siblings.Count == 0 ? 0 : siblings.Max(x => x.Position)) + 1;
            }
        }
        else
        {
            newPos = (siblings.Count == 0 ? 0 : siblings.Max(x => x.Position)) + 1;
        }

        var userId = _currentUser.UserId;
        var now = _clock.GetUtcNow().UtcDateTime;

        var stepsJson = JsonSerializer.Serialize(request.Steps ?? new List<TestStepDto>(), JsonOptions);
        var customJson = JsonSerializer.Serialize(request.CustomFields ?? new Dictionary<string, object?>(), JsonOptions);

        var tc = new TestCase
        {
            ProjectId = plan.ProjectId,
            PlanId = planId,
            ModuleId = section.ModuleId,
            SectionId = section.Id,
            Code = code,
            Title = request.Title?.Trim() ?? string.Empty,
            Preconditions = request.Preconditions?.Trim() ?? string.Empty,
            Steps = stepsJson,
            Actual = request.Actual?.Trim() ?? string.Empty,
            AiSuggestedReqs = request.AiSuggestedReqs == true,
            Priority = priority,
            Category = category,
            Browser = request.Browser?.Trim() ?? string.Empty,
            CustomFields = customJson,
            Status = TestCaseStatuses.Active,
            Position = newPos,
            CreatedById = userId,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.TestCases.Add(tc);
        await _db.SaveChangesAsync(ct);

        if (request.RequirementRefs != null && request.RequirementRefs.Count > 0)
        {
            await SyncTestCaseReqLinksAsync(tc, plan.ProjectId, request.RequirementRefs, now, ct);
        }

        if (request.CustomFields != null &&
            (request.CustomFields.TryGetValue("figmaUrl", out var rawFigmaUrl) ||
             request.CustomFields.TryGetValue("designUrl", out rawFigmaUrl)) &&
            rawFigmaUrl != null)
        {
            var figmaUrlStr = rawFigmaUrl.ToString();
            if (!string.IsNullOrWhiteSpace(figmaUrlStr))
            {
                await _figmaWatchService.LinkTestCaseAsync(tc.Id, figmaUrlStr, ct);
            }
        }

        _db.TestActivities.Add(new TestActivity
        {
            PlanId = planId,
            Type = TestActivityTypes.TestCase,
            At = now,
            ActorId = userId,
            ModuleId = tc.ModuleId,
            TestCaseId = tc.Id,
            Text = $"Created {tc.Code} · {tc.Title}"
        });

        plan.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);

        var dtos = await LoadAndMapTestCasesAsync(plan.ProjectId, new List<long> { tc.Id }, ct);
        return ServiceResult<TestCaseDto>.Created(dtos.First());
    }

    public async Task<ServiceResult<TestCaseDto>> UpdateTestCaseAsync(
        long testCaseId,
        UpdateTestCaseRequest request,
        CancellationToken ct = default)
    {
        var tc = await _db.TestCases
            .Include(x => x.Plan)
            .Include(x => x.RequirementLinks)
            .FirstOrDefaultAsync(x => x.Id == testCaseId, ct);
        if (tc == null)
            return ServiceResult<TestCaseDto>.NotFound("TEST_CASE_NOT_FOUND", "Test case not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(tc.ProjectId, PermissionKeys.TestCaseUpdate, ct);
        if (!auth.IsSuccess) return auth.Cast<TestCaseDto>();

        if (tc.Plan.Status == TestPlanStatuses.HandedOver)
            return ServiceResult<TestCaseDto>.BadRequest("PLAN_HANDED_OVER", "Test plan has already been handed over.");

        var now = _clock.GetUtcNow().UtcDateTime;
        await ApplyCaseChangesAsync(tc, request, now, ct);

        tc.Plan.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);

        var dtos = await LoadAndMapTestCasesAsync(tc.ProjectId, new List<long> { tc.Id }, ct);
        return ServiceResult<TestCaseDto>.Success(dtos.First());
    }

    public async Task<ServiceResult<ItemsDto<TestCaseDto>>> BulkUpdateTestCasesAsync(
        long planId,
        BulkUpdateTestCasesRequest request,
        CancellationToken ct = default)
    {
        var plan = await _db.TestPlans.FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan == null)
            return ServiceResult<ItemsDto<TestCaseDto>>.NotFound("TEST_PLAN_NOT_FOUND", "Test plan not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(plan.ProjectId, PermissionKeys.TestCaseUpdate, ct);
        if (!auth.IsSuccess) return auth.Cast<ItemsDto<TestCaseDto>>();

        if (plan.Status == TestPlanStatuses.HandedOver)
            return ServiceResult<ItemsDto<TestCaseDto>>.BadRequest("PLAN_HANDED_OVER", "Test plan has already been handed over.");

        var items = request.Items ?? new List<BulkUpdateTestCaseItemDto>();
        if (items.Count == 0)
            return ServiceResult<ItemsDto<TestCaseDto>>.Success(new ItemsDto<TestCaseDto>());

        var caseIds = items.Select(i => i.TestCaseId).Distinct().ToList();
        var cases = await _db.TestCases
            .Include(tc => tc.RequirementLinks)
            .Where(tc => tc.PlanId == planId && caseIds.Contains(tc.Id))
            .ToListAsync(ct);

        var byId = cases.ToDictionary(c => c.Id);
        var now = _clock.GetUtcNow().UtcDateTime;

        foreach (var item in items)
        {
            if (byId.TryGetValue(item.TestCaseId, out var tc) && item.Changes != null)
            {
                await ApplyCaseChangesAsync(tc, item.Changes, now, ct);
            }
        }

        plan.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);

        var dtos = await LoadAndMapTestCasesAsync(plan.ProjectId, cases.Select(c => c.Id).ToList(), ct);
        return ServiceResult<ItemsDto<TestCaseDto>>.Success(new ItemsDto<TestCaseDto>(dtos));
    }

    public async Task<ServiceResult> DeleteTestCaseAsync(long testCaseId, CancellationToken ct = default)
    {
        var tc = await _db.TestCases
            .Include(x => x.Plan)
            .FirstOrDefaultAsync(x => x.Id == testCaseId, ct);
        if (tc == null)
            return ServiceResult.NotFound("TEST_CASE_NOT_FOUND", "Test case not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(tc.ProjectId, PermissionKeys.TestCaseDelete, ct);
        if (!auth.IsSuccess) return auth;

        var now = _clock.GetUtcNow().UtcDateTime;
        tc.Status = TestCaseStatuses.Deleted;
        tc.UpdatedAt = now;
        tc.Plan.UpdatedAt = now;

        _db.TestActivities.Add(new TestActivity
        {
            PlanId = tc.PlanId,
            Type = TestActivityTypes.TestCase,
            At = now,
            ActorId = _currentUser.UserId,
            ModuleId = tc.ModuleId,
            TestCaseId = tc.Id,
            Text = $"Deleted {tc.Code} · {tc.Title}"
        });

        await _db.SaveChangesAsync(ct);
        return ServiceResult.NoContent();
    }

    // =========================================================================
    // 6. Execution Results & Bugs
    // =========================================================================

    public async Task<ServiceResult<RecordTestResultsResponse>> RecordSingleResultAsync(
        long testCaseId,
        RecordSingleResultRequest request,
        CancellationToken ct = default)
    {
        var tc = await _db.TestCases
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == testCaseId, ct);
        if (tc == null)
            return ServiceResult<RecordTestResultsResponse>.NotFound("TEST_CASE_NOT_FOUND", "Test case not found.");

        return await RecordResultsAsync(tc.PlanId, new RecordTestResultsRequest
        {
            EnvId = request.EnvId,
            TestCaseIds = new List<long> { testCaseId },
            Result = request.Result
        }, ct);
    }

    public async Task<ServiceResult<RecordTestResultsResponse>> RecordResultsAsync(
        long planId,
        RecordTestResultsRequest request,
        CancellationToken ct = default)
    {
        var plan = await _db.TestPlans.FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan == null)
            return ServiceResult<RecordTestResultsResponse>.NotFound("TEST_PLAN_NOT_FOUND", "Test plan not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(plan.ProjectId, PermissionKeys.TestRunExecute, ct);
        if (!auth.IsSuccess) return auth.Cast<RecordTestResultsResponse>();

        var resultVal = request.Result?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!TestResultValues.IsValid(resultVal))
        {
            return ServiceResult<RecordTestResultsResponse>.ValidationFailure(new Dictionary<string, string[]>
            {
                ["result"] = new[] { "Result must be PASS, FAIL, NA, or UNTEST." }
            });
        }

        var env = await _db.TestEnvironments
            .Include(e => e.Builds)
            .FirstOrDefaultAsync(e => e.Id == request.EnvId && e.PlanId == planId && !e.IsDeleted, ct);
        if (env == null)
            return ServiceResult<RecordTestResultsResponse>.NotFound("ENVIRONMENT_NOT_FOUND", "Test environment not found.");

        var currentBuild = env.Builds
            .OrderByDescending(b => b.DeployedAt)
            .ThenByDescending(b => b.Id)
            .FirstOrDefault()?.Version ?? "v1.0";

        var caseIds = (request.TestCaseIds ?? new List<long>()).Distinct().ToList();
        if (caseIds.Count == 0)
            return ServiceResult<RecordTestResultsResponse>.Success(new RecordTestResultsResponse());

        var cases = await _db.TestCases
            .Include(tc => tc.Results.Where(r => r.EnvId == request.EnvId))
            .Include(tc => tc.RetestFlags.Where(f => f.EnvId == request.EnvId))
            .Include(tc => tc.BugIssue)
                .ThenInclude(i => i!.Status)
            .Where(tc => tc.PlanId == planId && caseIds.Contains(tc.Id) && tc.Status == TestCaseStatuses.Active)
            .ToListAsync(ct);

        var moduleIds = cases.Select(c => c.ModuleId).Distinct().ToList();
        var assignments = await _db.TestAssignments
            .AsNoTracking()
            .Where(a => a.EnvId == request.EnvId && moduleIds.Contains(a.ModuleId))
            .ToDictionaryAsync(a => a.ModuleId, a => a.AssigneeId, ct);

        var userId = _currentUser.UserId;
        var now = _clock.GetUtcNow().UtcDateTime;

        var needBug = new List<long>();
        var reopened = new List<IssueKeyRefDto>();
        var reopenedIssueIds = new HashSet<long>();

        foreach (var tc in cases)
        {
            var latestRes = tc.Results
                .OrderByDescending(r => r.At)
                .ThenByDescending(r => r.Id)
                .FirstOrDefault();

            var hasFlag = tc.RetestFlags.Any();
            var prevStatus = hasFlag ? "RETEST" : (latestRes?.Result ?? TestResultValues.Untest);

            // Clear retest flags on this environment
            if (tc.RetestFlags.Count > 0)
            {
                _db.TestRetestFlags.RemoveRange(tc.RetestFlags);
            }

            long? behalfOfId = null;
            if (assignments.TryGetValue(tc.ModuleId, out var assigneeId) && assigneeId != userId)
            {
                behalfOfId = assigneeId;
            }

            if (resultVal == TestResultValues.Untest)
            {
                if (tc.Results.Count > 0)
                {
                    _db.TestResults.RemoveRange(tc.Results);
                }
            }
            else
            {
                _db.TestResults.Add(new TestResult
                {
                    TestCaseId = tc.Id,
                    EnvId = request.EnvId,
                    Build = currentBuild,
                    Result = resultVal,
                    ById = userId,
                    At = now,
                    BehalfOfId = behalfOfId
                });
            }

            tc.UpdatedAt = now;

            _db.TestActivities.Add(new TestActivity
            {
                PlanId = planId,
                Type = TestActivityTypes.Result,
                At = now,
                ActorId = userId,
                EnvId = request.EnvId,
                ModuleId = tc.ModuleId,
                TestCaseId = tc.Id,
                FromStatus = prevStatus,
                ToResult = resultVal,
                Build = currentBuild,
                BehalfOfId = behalfOfId,
                Text = string.Empty
            });

            if (resultVal == TestResultValues.Fail)
            {
                if (!tc.BugIssueId.HasValue)
                {
                    needBug.Add(tc.Id);
                }
                else if (tc.BugIssue != null &&
                         !tc.BugIssue.IsDeleted &&
                         tc.BugIssue.Status.Category == StatusCategories.Done)
                {
                    var reopenStatus = await _db.WorkflowStatuses
                        .AsNoTracking()
                        .Where(ws => ws.PhaseId == tc.BugIssue.Status.PhaseId &&
                                     ws.IsActive &&
                                     ws.Category != StatusCategories.Done)
                        .OrderByDescending(ws => ws.Category == StatusCategories.InProgress)
                        .ThenBy(ws => ws.Position)
                        .FirstOrDefaultAsync(ct);

                    if (reopenStatus != null)
                    {
                        var oldStatusName = tc.BugIssue.Status.Name;
                        tc.BugIssue.StatusId = reopenStatus.Id;
                        tc.BugIssue.ResolvedAt = null;
                        tc.BugIssue.UpdatedAt = now;

                        _db.IssueActivities.Add(new IssueActivity
                        {
                            IssueId = tc.BugIssue.Id,
                            Action = IssueActivityActions.Transitioned,
                            Note = $"Status changed from \"{oldStatusName}\" to \"{reopenStatus.Name}\" — Automatically reopened because {tc.Code} failed on {env.Name} ({currentBuild})",
                            ActorId = userId,
                            CreatedAt = now
                        });
                    }

                    if (reopenedIssueIds.Add(tc.BugIssue.Id))
                    {
                        reopened.Add(new IssueKeyRefDto
                        {
                            IssueId = tc.BugIssue.Id,
                            Key = tc.BugIssue.Key
                        });
                    }

                    _db.TestActivities.Add(new TestActivity
                    {
                        PlanId = planId,
                        Type = TestActivityTypes.Bug,
                        At = now,
                        ActorId = userId,
                        EnvId = request.EnvId,
                        ModuleId = tc.ModuleId,
                        TestCaseId = tc.Id,
                        Text = $"Reopened {tc.BugIssue.Key} because retest failed"
                    });
                }
            }
        }

        plan.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);

        var dtos = await LoadAndMapTestCasesAsync(plan.ProjectId, cases.Select(c => c.Id).ToList(), ct);
        return ServiceResult<RecordTestResultsResponse>.Success(new RecordTestResultsResponse
        {
            Items = dtos,
            NeedBug = needBug,
            Reopened = reopened
        });
    }

    public async Task<ServiceResult<ReportTestBugResponse>> ReportBugAsync(
        long planId,
        ReportTestBugRequest request,
        CancellationToken ct = default)
    {
        var plan = await _db.TestPlans.FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan == null)
            return ServiceResult<ReportTestBugResponse>.NotFound("TEST_PLAN_NOT_FOUND", "Test plan not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(plan.ProjectId, PermissionKeys.TestRunExecute, ct);
        if (!auth.IsSuccess) return auth.Cast<ReportTestBugResponse>();

        var caseIds = (request.TestCaseIds ?? new List<long>()).Distinct().ToList();
        if (caseIds.Count == 0)
        {
            return ServiceResult<ReportTestBugResponse>.ValidationFailure(new Dictionary<string, string[]>
            {
                ["testCaseIds"] = new[] { "At least 1 test case is required." }
            });
        }

        var cases = await _db.TestCases
            .Include(tc => tc.RequirementLinks)
            .Where(tc => tc.PlanId == planId && caseIds.Contains(tc.Id))
            .ToListAsync(ct);

        if (cases.Count == 0)
            return ServiceResult<ReportTestBugResponse>.NotFound("TEST_CASE_NOT_FOUND", "Test cases not found.");

        var reqRefs = cases
            .SelectMany(tc => tc.RequirementLinks)
            .GroupBy(l => (l.DocumentId, ReqUid: l.ReqUid.ToUpperInvariant()))
            .Select(g => new RequirementRefInputDto
            {
                DocumentId = g.Key.DocumentId,
                ReqUid = g.Key.ReqUid
            })
            .ToList();

        var firstCase = cases[0];
        var title = !string.IsNullOrWhiteSpace(request.Title)
            ? request.Title.Trim()
            : $"[Bug] {firstCase.Code}: {firstCase.Title}";

        var issuePriority = (request.Priority?.Trim().ToUpperInvariant()) switch
        {
            "HIGHEST" => IssuePriorities.Highest,
            "HIGH" => IssuePriorities.High,
            "LOW" or "LOWEST" => IssuePriorities.Low,
            _ => IssuePriorities.High
        };

        var createIssueRes = await _kanbanService.CreateIssueAsync(plan.ProjectId, new CreateIssueRequest
        {
            Type = IssueTypes.Bug,
            Title = title,
            Description = request.Description,
            Priority = issuePriority,
            RequirementRefs = reqRefs
        }, ct);

        if (!createIssueRes.IsSuccess || createIssueRes.Data == null)
            return createIssueRes.Cast<ReportTestBugResponse>();

        var createdIssue = createIssueRes.Data;
        var now = _clock.GetUtcNow().UtcDateTime;
        var userId = _currentUser.UserId;

        foreach (var tc in cases)
        {
            tc.BugIssueId = createdIssue.IssueId;
            tc.UpdatedAt = now;

            _db.TestActivities.Add(new TestActivity
            {
                PlanId = planId,
                Type = TestActivityTypes.Bug,
                At = now,
                ActorId = userId,
                EnvId = request.EnvId > 0 ? request.EnvId : null,
                ModuleId = tc.ModuleId,
                TestCaseId = tc.Id,
                Text = $"Created {createdIssue.Key} on Kanban"
            });
        }

        plan.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);

        var dtos = await LoadAndMapTestCasesAsync(plan.ProjectId, cases.Select(c => c.Id).ToList(), ct);
        return ServiceResult<ReportTestBugResponse>.Created(new ReportTestBugResponse
        {
            Issue = new IssueKeyRefDto
            {
                IssueId = createdIssue.IssueId,
                Key = createdIssue.Key
            },
            Items = dtos
        });
    }

    public async Task<ServiceResult<ItemsDto<TestCaseDto>>> LinkBugAsync(
        long planId,
        long issueId,
        LinkTestBugRequest request,
        CancellationToken ct = default)
    {
        var plan = await _db.TestPlans.FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan == null)
            return ServiceResult<ItemsDto<TestCaseDto>>.NotFound("TEST_PLAN_NOT_FOUND", "Test plan not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(plan.ProjectId, PermissionKeys.TestRunExecute, ct);
        if (!auth.IsSuccess) return auth.Cast<ItemsDto<TestCaseDto>>();

        var issue = await _db.Issues
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == issueId && i.ProjectId == plan.ProjectId && !i.IsDeleted, ct);
        if (issue == null)
            return ServiceResult<ItemsDto<TestCaseDto>>.NotFound("ISSUE_NOT_FOUND", "Bug issue not found.");

        var caseIds = (request.TestCaseIds ?? new List<long>()).Distinct().ToList();
        var cases = await _db.TestCases
            .Where(tc => tc.PlanId == planId && caseIds.Contains(tc.Id))
            .ToListAsync(ct);

        var now = _clock.GetUtcNow().UtcDateTime;
        var userId = _currentUser.UserId;

        foreach (var tc in cases)
        {
            tc.BugIssueId = issue.Id;
            tc.UpdatedAt = now;

            _db.TestActivities.Add(new TestActivity
            {
                PlanId = planId,
                Type = TestActivityTypes.Bug,
                At = now,
                ActorId = userId,
                ModuleId = tc.ModuleId,
                TestCaseId = tc.Id,
                Text = $"Linked to existing bug {issue.Key}"
            });
        }

        plan.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);

        var dtos = await LoadAndMapTestCasesAsync(plan.ProjectId, cases.Select(c => c.Id).ToList(), ct);
        return ServiceResult<ItemsDto<TestCaseDto>>.Success(new ItemsDto<TestCaseDto>(dtos));
    }

    // =========================================================================
    // 7. Handover & Reopen
    // =========================================================================

    public async Task<ServiceResult<TestPlanDto>> HandoverPlanAsync(
        long planId,
        HandoverTestPlanRequest request,
        CancellationToken ct = default)
    {
        var plan = await _db.TestPlans.FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan == null)
            return ServiceResult<TestPlanDto>.NotFound("TEST_PLAN_NOT_FOUND", "Test plan not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(plan.ProjectId, PermissionKeys.TestRunExecute, ct);
        if (!auth.IsSuccess) return auth.Cast<TestPlanDto>();

        var userId = _currentUser.UserId;
        var now = _clock.GetUtcNow().UtcDateTime;
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        plan.Status = TestPlanStatuses.HandedOver;
        plan.HandoverById = userId;
        plan.HandoverAt = now;
        plan.HandoverNote = note;
        plan.UpdatedAt = now;

        _db.TestActivities.Add(new TestActivity
        {
            PlanId = planId,
            Type = TestActivityTypes.Handover,
            At = now,
            ActorId = userId,
            Text = $"Confirmed test handover{(note != null ? $" — \"{note}\"" : string.Empty)}"
        });

        await _db.SaveChangesAsync(ct);

        var dto = await BuildPlanDtoAsync(planId, ct);
        return ServiceResult<TestPlanDto>.Success(dto!);
    }

    public async Task<ServiceResult<TestPlanDto>> ReopenPlanAsync(long planId, CancellationToken ct = default)
    {
        var plan = await _db.TestPlans.FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan == null)
            return ServiceResult<TestPlanDto>.NotFound("TEST_PLAN_NOT_FOUND", "Test plan not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(plan.ProjectId, PermissionKeys.TestRunExecute, ct);
        if (!auth.IsSuccess) return auth.Cast<TestPlanDto>();

        var userId = _currentUser.UserId;
        var now = _clock.GetUtcNow().UtcDateTime;

        plan.Status = TestPlanStatuses.Open;
        plan.HandoverById = null;
        plan.HandoverAt = null;
        plan.HandoverNote = null;
        plan.UpdatedAt = now;

        _db.TestActivities.Add(new TestActivity
        {
            PlanId = planId,
            Type = TestActivityTypes.Handover,
            At = now,
            ActorId = userId,
            Text = "Reopened test plan"
        });

        await _db.SaveChangesAsync(ct);

        var dto = await BuildPlanDtoAsync(planId, ct);
        return ServiceResult<TestPlanDto>.Success(dto!);
    }

    // =========================================================================
    // 8. Activities, Traceability & AI
    // =========================================================================

    public async Task<ServiceResult<ItemsDto<TestActivityDto>>> ListActivitiesAsync(
        long planId,
        ListActivitiesQuery query,
        CancellationToken ct = default)
    {
        var plan = await _db.TestPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan == null)
            return ServiceResult<ItemsDto<TestActivityDto>>.NotFound("TEST_PLAN_NOT_FOUND", "Test plan not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(plan.ProjectId, PermissionKeys.TestCaseView, ct);
        if (!auth.IsSuccess) return auth.Cast<ItemsDto<TestActivityDto>>();

        var dbQuery = _db.TestActivities
            .AsNoTracking()
            .Include(a => a.Actor)
            .Include(a => a.BehalfOf)
            .Where(a => a.PlanId == planId);

        if (query.TestCaseId.HasValue)
            dbQuery = dbQuery.Where(a => a.TestCaseId == query.TestCaseId.Value);
        if (query.ActorId.HasValue)
            dbQuery = dbQuery.Where(a => a.ActorId == query.ActorId.Value);
        if (query.ModuleId.HasValue)
            dbQuery = dbQuery.Where(a => a.ModuleId == query.ModuleId.Value);
        if (!string.IsNullOrWhiteSpace(query.Type))
        {
            var t = query.Type.Trim().ToUpperInvariant();
            dbQuery = dbQuery.Where(a => a.Type == t);
        }

        var list = await dbQuery
            .OrderByDescending(a => a.At)
            .ThenByDescending(a => a.Id)
            .Take(200)
            .ToListAsync(ct);

        var fallbackActor = await GetUserRefAsync(plan.CreatedById, ct);

        var dtos = list.Select(a => new TestActivityDto
        {
            ActivityId = a.Id,
            PlanId = a.PlanId,
            Type = NormalizeActivityType(a.Type),
            At = FormatUtc(a.At),
            Actor = a.Actor != null ? MapUserRef(a.Actor) : fallbackActor,
            EnvId = a.EnvId,
            ModuleId = a.ModuleId,
            TestCaseId = a.TestCaseId,
            From = a.FromStatus,
            To = a.ToResult,
            Build = a.Build,
            BehalfOf = a.BehalfOf != null ? MapUserRef(a.BehalfOf) : null,
            Text = a.Text ?? string.Empty
        }).ToList();

        return ServiceResult<ItemsDto<TestActivityDto>>.Success(new ItemsDto<TestActivityDto>(dtos));
    }

    public async Task<ServiceResult<ItemsDto<TraceRowDto>>> GetTraceabilityAsync(
        long planId,
        long? envId,
        CancellationToken ct = default)
    {
        var plan = await _db.TestPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan == null)
            return ServiceResult<ItemsDto<TraceRowDto>>.NotFound("TEST_PLAN_NOT_FOUND", "Test plan not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(plan.ProjectId, PermissionKeys.TestCaseView, ct);
        if (!auth.IsSuccess) return auth.Cast<ItemsDto<TraceRowDto>>();

        var targetEnvId = envId;
        if (!targetEnvId.HasValue)
        {
            targetEnvId = await _db.TestEnvironments
                .AsNoTracking()
                .Where(e => e.PlanId == planId && !e.IsDeleted)
                .OrderBy(e => e.Position)
                .ThenBy(e => e.Id)
                .Select(e => (long?)e.Id)
                .FirstOrDefaultAsync(ct);
        }

        var docs = await _db.DocumentNodes
            .AsNoTracking()
            .Where(d => d.ProjectId == plan.ProjectId && !d.IsDeleted && d.Kind == DocumentNodeKinds.Page)
            .OrderBy(d => d.SortOrder)
            .ThenBy(d => d.Id)
            .ToListAsync(ct);

        var verIds = docs
            .Select(d => d.BaselineVersionId ?? d.CurrentVersionId)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .Distinct()
            .ToList();

        var reqItems = verIds.Count == 0
            ? new List<RequirementItem>()
            : await _db.RequirementItems
                .AsNoTracking()
                .Where(r => verIds.Contains(r.VersionId))
                .OrderBy(r => r.SortOrder)
                .ToListAsync(ct);

        var activeCases = await _db.TestCases
            .AsNoTracking()
            .Include(tc => tc.RequirementLinks)
            .Include(tc => tc.Results)
            .Include(tc => tc.RetestFlags)
            .Include(tc => tc.BugIssue)
            .Where(tc => tc.PlanId == planId && tc.Status == TestCaseStatuses.Active)
            .OrderBy(tc => tc.ModuleId)
            .ThenBy(tc => tc.SectionId)
            .ThenBy(tc => tc.Position)
            .ThenBy(tc => tc.Id)
            .ToListAsync(ct);

        var rows = new List<TraceRowDto>();
        foreach (var doc in docs)
        {
            var verId = doc.BaselineVersionId ?? doc.CurrentVersionId;
            if (!verId.HasValue) continue;

            var docReqs = reqItems.Where(r => r.VersionId == verId.Value).ToList();
            if (docReqs.Count == 0) continue;

            var docCode = !string.IsNullOrWhiteSpace(doc.Code) ? doc.Code : $"DOC-{doc.Id}";
            foreach (var r in docReqs)
            {
                var matchingCases = activeCases
                    .Where(tc => tc.RequirementLinks.Any(l =>
                        l.DocumentId == doc.Id &&
                        string.Equals(l.ReqUid, r.ReqUid, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                var changed = matchingCases.Any(tc => tc.RequirementLinks.Any(l =>
                    l.DocumentId == doc.Id &&
                    string.Equals(l.ReqUid, r.ReqUid, StringComparison.OrdinalIgnoreCase) &&
                    l.ChangedVersionLabel != null));

                var traceCases = new List<TraceCaseDto>();
                var bugs = new Dictionary<long, IssueKeyRefDto>();

                foreach (var tc in matchingCases)
                {
                    var effStatus = targetEnvId.HasValue
                        ? TestStatusEvaluator.ComputeEffectiveEnvStatus(tc, targetEnvId.Value)
                        : TestResultValues.Untest;

                    traceCases.Add(new TraceCaseDto
                    {
                        TestCaseId = tc.Id,
                        ModuleId = tc.ModuleId,
                        Code = tc.Code,
                        Title = tc.Title,
                        Status = effStatus,
                        Ai = tc.AiSuggestedReqs
                    });

                    if (tc.BugIssue != null && !tc.BugIssue.IsDeleted)
                    {
                        bugs[tc.BugIssue.Id] = new IssueKeyRefDto
                        {
                            IssueId = tc.BugIssue.Id,
                            Key = tc.BugIssue.Key
                        };
                    }
                }

                string rowStatus;
                if (traceCases.Count == 0)
                    rowStatus = "NO_TESTS";
                else if (traceCases.Any(c => c.Status == TestResultValues.Fail))
                    rowStatus = "FAILING";
                else if (traceCases.All(c => c.Status is TestResultValues.Pass or TestResultValues.Na))
                    rowStatus = "PASSED";
                else
                    rowStatus = "IN_PROGRESS";

                rows.Add(new TraceRowDto
                {
                    DocumentId = doc.Id,
                    DocumentCode = docCode,
                    DocumentTitle = doc.Title,
                    ReqUid = r.ReqUid,
                    ReqKey = $"{docCode}:{r.ReqUid}",
                    Title = r.Title,
                    AnchorId = r.AnchorId,
                    Changed = changed,
                    Status = rowStatus,
                    Cases = traceCases,
                    Bugs = bugs.Values.ToList()
                });
            }
        }

        return ServiceResult<ItemsDto<TraceRowDto>>.Success(new ItemsDto<TraceRowDto>(rows));
    }

    public async Task<ServiceResult<ItemsDto<SuggestedTestCaseDto>>> SuggestTestCasesAsync(
        long projectId,
        SuggestTestCasesRequest request,
        CancellationToken ct = default)
    {
        var auth = await _accessGuard.EnsureProjectPermissionAsync(projectId, PermissionKeys.TestCaseCreate, ct);
        if (!auth.IsSuccess) return auth.Cast<ItemsDto<SuggestedTestCaseDto>>();

        var aiAuth = await _accessGuard.EnsureProjectPermissionAsync(projectId, PermissionKeys.AiUse, ct);
        if (!aiAuth.IsSuccess) return aiAuth.Cast<ItemsDto<SuggestedTestCaseDto>>();

        var aiEnabled = await _db.Projects
            .AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => (bool?)p.AiEnabled)
            .SingleOrDefaultAsync(ct);
        if (aiEnabled != true)
        {
            return ServiceResult<ItemsDto<SuggestedTestCaseDto>>.Forbidden(
                "AI_DISABLED",
                "AI features are disabled for this project.");
        }

        var refs = request.Refs ?? new List<RequirementRefInputDto>();
        if (refs.Count == 0)
            return ServiceResult<ItemsDto<SuggestedTestCaseDto>>.Success(new ItemsDto<SuggestedTestCaseDto>());

        if (request.Designs is { Count: > MaxDesignsPerGeneration })
        {
            return ServiceResult<ItemsDto<SuggestedTestCaseDto>>.ValidationFailure(new Dictionary<string, string[]>
            {
                ["designs"] = new[] { $"Tối đa {MaxDesignsPerGeneration} thiết kế mỗi lượt." }
            });
        }

        var planDto = request.PlanId.HasValue
            ? await BuildPlanDtoAsync(request.PlanId.Value, ct)
            : null;

        var docIds = refs.Select(r => r.DocumentId).Distinct().ToList();
        var docs = await _db.DocumentNodes
            .AsNoTracking()
            .Where(d => d.ProjectId == projectId && docIds.Contains(d.Id) && !d.IsDeleted)
            .ToDictionaryAsync(d => d.Id, ct);

        var verIds = docs.Values
            .Select(d => d.BaselineVersionId ?? d.CurrentVersionId)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .Distinct()
            .ToList();

        var reqItems = verIds.Count == 0
            ? new List<RequirementItem>()
            : await _db.RequirementItems
                .AsNoTracking()
                .Where(r => verIds.Contains(r.VersionId))
                .ToListAsync(ct);

        var docHeadingsByDoc = new Dictionary<long, List<DocSectionDto>>();
        if (verIds.Count > 0)
        {
            var versions = await _db.DocumentVersions
                .AsNoTracking()
                .Where(v => verIds.Contains(v.Id))
                .ToListAsync(ct);

            foreach (var v in versions)
            {
                docHeadingsByDoc[v.DocumentId] = DocumentContentHelper.ExtractDocSections(v.Content);
            }
        }

        var selectedRequirements = new List<(RequirementRefInputDto Reference, RequirementItem Requirement,
            DocumentNode Document, string? SectionAnchor, string SectionTitle, List<string> AcceptanceCriteria)>();

        foreach (var rf in refs)
        {
            if (!docs.TryGetValue(rf.DocumentId, out var doc))
                continue;

            var verId = doc.BaselineVersionId ?? doc.CurrentVersionId;
            if (!verId.HasValue) continue;

            var req = reqItems.FirstOrDefault(r =>
                r.VersionId == verId.Value &&
                string.Equals(r.ReqUid, rf.ReqUid, StringComparison.OrdinalIgnoreCase));
            if (req == null) continue;

            var docSections = docHeadingsByDoc.GetValueOrDefault(doc.Id) ?? new List<DocSectionDto>();
            var parentHeading = FindParentHeading(docSections, req.AnchorId, req.ReqUid);
            var sectionAnchor = parentHeading?.AnchorId ?? req.AnchorId;
            var sectionTitle = parentHeading != null
                ? StripHeadingNumber(parentHeading.Title)
                : req.Title;

            var criteria = SafeDeserialize<List<string>>(req.AcceptanceCriteria) ?? new List<string>();
            selectedRequirements.Add((rf, req, doc, sectionAnchor, sectionTitle, criteria));
        }

        if (selectedRequirements.Count == 0)
            return ServiceResult<ItemsDto<SuggestedTestCaseDto>>.Success(new ItemsDto<SuggestedTestCaseDto>());

        // Existing test case titles in the plan (for duplicate avoidance - Section 5)
        var existingTitles = request.PlanId.HasValue
            ? await _db.TestCases
                .AsNoTracking()
                .Where(tc => tc.PlanId == request.PlanId.Value && tc.Status != TestCaseStatuses.Deleted)
                .Select(tc => tc.Title)
                .OrderBy(title => title)
                .Take(100)
                .ToListAsync(ct)
            : new List<string>();

        // Resolve attached designs (never fail the whole request if one design fails)
        var resolvedDesigns = new List<(TestCaseDesignInput Input, string? ThumbnailUrl)>();
        if (request.Designs is { Count: > 0 })
        {
            foreach (var d in request.Designs)
            {
                var reqIdx = selectedRequirements.FindIndex(sr =>
                    sr.Document.Id == d.DocumentId &&
                    string.Equals(sr.Requirement.ReqUid, d.ReqUid, StringComparison.OrdinalIgnoreCase));
                if (reqIdx < 0 || d.Source == null) continue;

                var kind = d.Source.Kind?.Trim().ToLowerInvariant();
                if (kind == "figma" && !string.IsNullOrWhiteSpace(d.Source.Url))
                {
                    var figma = await _figmaService.FetchNodeImageForSuggestAsync(d.Source.Url, ct);
                    await _figmaWatchService.RegisterFileAsync(d.Source.Url, figma?.Name, ct);
                    if (figma != null)
                    {
                        var idx = resolvedDesigns.Count;
                        resolvedDesigns.Add((new TestCaseDesignInput(idx, reqIdx, figma.Name, figma.ImageBytes, figma.MimeType), figma.ThumbnailUrl));
                    }
                }
                else if (kind == "image" && TryDecodeDataUrlImage(d.Source.DataUrl, out var bytes, out var mime))
                {
                    var idx = resolvedDesigns.Count;
                    var name = string.IsNullOrWhiteSpace(d.Source.Name) ? $"image-{idx + 1}" : d.Source.Name.Trim();
                    resolvedDesigns.Add((new TestCaseDesignInput(idx, reqIdx, name, bytes, mime), null));
                }
            }
        }

        var generationContexts = selectedRequirements
            .Select((item, index) =>
            {
                var constraints = (SafeDeserialize<List<ConstraintRowDto>>(item.Requirement.Constraints) ?? new List<ConstraintRowDto>())
                    .Select(c => JsonSerializer.Serialize(c, JsonOptions))
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();

                return new TestCaseGenerationContextDto
                {
                    RequirementIndex = index,
                    RequirementUid = item.Requirement.ReqUid,
                    Title = item.Requirement.Title,
                    Description = item.Requirement.BodyPreview,
                    Actor = NullIfEmpty(item.Requirement.Actor),
                    Preconditions = NullIfEmpty(item.Requirement.Preconditions),
                    Constraints = constraints,
                    AcceptanceCriteria = item.AcceptanceCriteria,
                    ExistingTestCaseTitles = existingTitles,
                    DesignNames = resolvedDesigns.Where(rd => rd.Input.RequirementIndex == index).Select(rd => rd.Input.Name).ToList()
                };
            })
            .ToList();

        var drafts = new List<AiGeneratedTestCaseDraftDto>();
        for (var start = 0; start < generationContexts.Count; start += AiTestCaseBatchSize)
        {
            var batchContexts = generationContexts
                .Skip(start)
                .Take(AiTestCaseBatchSize)
                .Select((context, index) => new TestCaseGenerationContextDto
                {
                    RequirementIndex = index,
                    RequirementUid = context.RequirementUid,
                    Title = context.Title,
                    Description = context.Description,
                    Actor = context.Actor,
                    Preconditions = context.Preconditions,
                    Constraints = context.Constraints,
                    AcceptanceCriteria = context.AcceptanceCriteria,
                    ExistingTestCaseTitles = context.ExistingTestCaseTitles,
                    DesignNames = context.DesignNames
                })
                .ToList();

            var batchDesigns = resolvedDesigns
                .Select((design, globalIndex) => (Design: design, GlobalIndex: globalIndex))
                .Where(x => x.Design.Input.RequirementIndex >= start &&
                            x.Design.Input.RequirementIndex < start + batchContexts.Count)
                .ToList();
            var batchDesignInputs = batchDesigns
                .Select((x, localIndex) => new TestCaseDesignInput(
                    localIndex,
                    x.Design.Input.RequirementIndex - start,
                    x.Design.Input.Name,
                    x.Design.Input.ImageBytes,
                    x.Design.Input.MimeType))
                .ToList();

            try
            {
                var batchDrafts = await _aiService.GenerateTestCaseDraftsAsync(batchContexts, batchDesignInputs, ct);
                foreach (var draft in batchDrafts)
                {
                    draft.RequirementIndex += start;
                    if (draft.DesignIndex is int localDesignIndex &&
                        localDesignIndex >= 0 &&
                        localDesignIndex < batchDesigns.Count)
                        draft.DesignIndex = batchDesigns[localDesignIndex].GlobalIndex;
                    else
                        draft.DesignIndex = null;
                    drafts.Add(draft);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (drafts.Count == 0)
                {
                    return ServiceResult<ItemsDto<SuggestedTestCaseDto>>.Failure(
                        StatusCodes.Status502BadGateway,
                        "AI_GENERATION_FAILED",
                        $"Không thể sinh test case từ AI: {ex.Message}");
                }
            }
        }

        var suggestions = drafts
            .Where(draft => draft.RequirementIndex >= 0 && draft.RequirementIndex < selectedRequirements.Count)
            .Select((draft, index) =>
        {
            var source = selectedRequirements[draft.RequirementIndex];
            var isFromDesign = draft.DesignIndex.HasValue && draft.DesignIndex.Value >= 0 && draft.DesignIndex.Value < resolvedDesigns.Count;
            var category = isFromDesign ? "Giao diện" : draft.Category;
            SuggestedTestCaseDesignDto? designDto = null;
            if (isFromDesign)
            {
                var rd = resolvedDesigns[draft.DesignIndex!.Value];
                designDto = new SuggestedTestCaseDesignDto
                {
                    Name = rd.Input.Name,
                    ThumbnailUrl = rd.ThumbnailUrl
                };
            }

            return new SuggestedTestCaseDto
            {
                TempId = $"ai-{index + 1}",
                Title = draft.Title,
                Preconditions = draft.Preconditions,
                Priority = draft.Priority,
                Category = category,
                Source = isFromDesign ? "FIGMA" : "AI",
                Design = designDto,
                Steps = draft.Steps,
                RequirementRef = new RequirementRefInputDto
                {
                    DocumentId = source.Document.Id,
                    ReqUid = source.Requirement.ReqUid
                },
                FromCriterion = draft.AcceptanceCriterionIndex.HasValue
                    && draft.AcceptanceCriterionIndex.Value >= 0
                    && draft.AcceptanceCriterionIndex.Value < source.AcceptanceCriteria.Count
                    ? source.AcceptanceCriteria[draft.AcceptanceCriterionIndex.Value]
                    : null,
                Placement = ComputePlacement(
                    planDto,
                    source.Document.Id,
                    source.Document.Title,
                    source.SectionAnchor,
                    source.SectionTitle,
                    category)
            };
        }).ToList();

        return ServiceResult<ItemsDto<SuggestedTestCaseDto>>.Success(new ItemsDto<SuggestedTestCaseDto>(suggestions));
    }

    private static bool TryDecodeDataUrlImage(string? dataUrl, out byte[] bytes, out string mimeType)
    {
        bytes = Array.Empty<byte>();
        mimeType = string.Empty;

        if (string.IsNullOrWhiteSpace(dataUrl)) return false;
        var commaIdx = dataUrl.IndexOf(',');
        if (commaIdx <= 0) return false;

        var header = dataUrl[..commaIdx];
        if (!header.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
            !header.Contains(";base64", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var base64Part = dataUrl[(commaIdx + 1)..].Trim();
        // Quick length check before allocating (5MB * 4/3 + padding)
        if (base64Part.Length > 7_000_000) return false;

        try
        {
            bytes = Convert.FromBase64String(base64Part);
        }
        catch (FormatException)
        {
            return false;
        }

        if (bytes.Length == 0 || bytes.Length > 5 * 1024 * 1024)
            return false;

        var detected = FigmaService.DetectImageMimeType(bytes);
        if (detected == null) return false;

        mimeType = detected;
        return true;
    }

    public async Task<ServiceResult<ItemsDto<TestCaseDto>>> AiApplyTestCasesAsync(
        long planId,
        AiApplyTestCasesRequest request,
        CancellationToken ct = default)
    {
        var plan = await _db.TestPlans.FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan == null)
            return ServiceResult<ItemsDto<TestCaseDto>>.NotFound("TEST_PLAN_NOT_FOUND", "Test plan not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(plan.ProjectId, PermissionKeys.TestCaseCreate, ct);
        if (!auth.IsSuccess) return auth.Cast<ItemsDto<TestCaseDto>>();

        var items = request.Items ?? new List<AiApplyItemDto>();
        if (items.Count == 0)
            return ServiceResult<ItemsDto<TestCaseDto>>.Success(new ItemsDto<TestCaseDto>());

        var normalizedCategories = new List<string>(items.Count);
        var categoryErrors = new Dictionary<string, string[]>();
        for (var i = 0; i < items.Count; i++)
        {
            var category = items[i].Category ?? items[i].Placement?.Category ?? "Functional";
            if (TryNormalizeAiTestCaseCategory(category, out var normalizedCategory))
            {
                normalizedCategories.Add(normalizedCategory);
            }
            else
            {
                categoryErrors[$"items[{i}].category"] = new[]
                {
                    "Category must be FUNCTIONAL, BOUNDARY, NEGATIVE, UI, SECURITY, or PERFORMANCE."
                };
                normalizedCategories.Add(string.Empty);
            }
        }

        if (categoryErrors.Count > 0)
            return ServiceResult<ItemsDto<TestCaseDto>>.ValidationFailure(categoryErrors);

        return await _db.ExecuteInTransactionAsync(
            innerCt => ApplyAiTestCaseItemsAsync(planId, plan.ProjectId, items, normalizedCategories, innerCt),
            ct);
    }

    private async Task<ServiceResult<ItemsDto<TestCaseDto>>> ApplyAiTestCaseItemsAsync(
        long planId,
        long projectId,
        IReadOnlyList<AiApplyItemDto> items,
        IReadOnlyList<string> normalizedCategories,
        CancellationToken ct)
    {
        var createdIds = new List<long>();
        for (var itemIndex = 0; itemIndex < items.Count; itemIndex++)
        {
            var item = items[itemIndex];
            var p = item.Placement ?? new TestPlacementDto
            {
                DocumentId = item.RequirementRef?.DocumentId ?? 0,
                SectionTitle = "Test Section",
                Category = item.Category ?? "Functional"
            };

            // 1. Ensure Module
            long? moduleId = p.ModuleId;
            if (!moduleId.HasValue || !await _db.TestModules.AnyAsync(m => m.Id == moduleId.Value && m.PlanId == planId, ct))
            {
                var existingMod = p.DocumentId > 0
                    ? await _db.TestModules.FirstOrDefaultAsync(m => m.PlanId == planId && m.DocumentId == p.DocumentId, ct)
                    : await _db.TestModules.FirstOrDefaultAsync(m => m.PlanId == planId, ct);

                if (existingMod != null)
                {
                    moduleId = existingMod.Id;
                }
                else
                {
                    var modRes = await CreateModuleAsync(planId, new CreateTestModuleRequest
                    {
                        DocumentId = p.DocumentId > 0 ? p.DocumentId : null,
                        AnchorIds = !string.IsNullOrWhiteSpace(p.AnchorId) ? new List<string> { p.AnchorId! } : null
                    }, ct);
                    if (!modRes.IsSuccess || modRes.Data == null)
                        return ServiceResult<ItemsDto<TestCaseDto>>.FromError(modRes);
                    moduleId = modRes.Data.ModuleId;
                }
            }

            // 2. Ensure Top-Level Section
            long? sectionId = p.SectionId;
            if (!sectionId.HasValue || !await _db.TestSections.AnyAsync(s => s.Id == sectionId.Value && s.ModuleId == moduleId.Value, ct))
            {
                var modSections = await _db.TestSections
                    .Where(s => s.ModuleId == moduleId.Value && s.ParentId == null)
                    .ToListAsync(ct);

                var matchedSec = (!string.IsNullOrWhiteSpace(p.AnchorId)
                        ? modSections.FirstOrDefault(s => string.Equals(s.AnchorId, p.AnchorId, StringComparison.OrdinalIgnoreCase))
                        : null)
                    ?? modSections.FirstOrDefault(s => string.Equals(s.Title.Trim(), (p.SectionTitle ?? "").Trim(), StringComparison.OrdinalIgnoreCase));

                if (matchedSec != null)
                {
                    sectionId = matchedSec.Id;
                }
                else
                {
                    var secRes = await CreateSectionAsync(moduleId.Value, new CreateTestSectionRequest
                    {
                        ParentId = null,
                        Title = !string.IsNullOrWhiteSpace(p.SectionTitle) ? p.SectionTitle : "Test Section",
                        AnchorId = p.AnchorId
                    }, ct);
                    if (!secRes.IsSuccess || secRes.Data == null)
                        return ServiceResult<ItemsDto<TestCaseDto>>.FromError(secRes);
                    sectionId = secRes.Data.SectionId;
                }
            }

            // 3. Ensure Group (child section)
            long? groupId = p.GroupId;
            var groupCategory = !string.IsNullOrWhiteSpace(p.Category)
                ? p.Category.Trim()
                : (!string.IsNullOrWhiteSpace(item.Category) ? item.Category.Trim() : "Functional");

            if (!groupId.HasValue || !await _db.TestSections.AnyAsync(s => s.Id == groupId.Value && s.ModuleId == moduleId.Value, ct))
            {
                var childGroup = await _db.TestSections
                    .FirstOrDefaultAsync(s => s.ModuleId == moduleId.Value &&
                                              s.ParentId == sectionId.Value &&
                                              s.Title.ToLower() == groupCategory.ToLower(), ct);
                if (childGroup != null)
                {
                    groupId = childGroup.Id;
                }
                else
                {
                    var grpRes = await CreateSectionAsync(moduleId.Value, new CreateTestSectionRequest
                    {
                        ParentId = sectionId.Value,
                        Title = groupCategory
                    }, ct);
                    if (!grpRes.IsSuccess || grpRes.Data == null)
                        return ServiceResult<ItemsDto<TestCaseDto>>.FromError(grpRes);
                    groupId = grpRes.Data.SectionId;
                }
            }

            var reqRefs = item.RequirementRef != null && item.RequirementRef.DocumentId > 0 && !string.IsNullOrWhiteSpace(item.RequirementRef.ReqUid)
                ? new List<RequirementRefInputDto> { item.RequirementRef }
                : new List<RequirementRefInputDto>();

            var tcRes = await CreateTestCaseAsync(planId, new CreateTestCaseRequest
            {
                SectionId = groupId.Value,
                Title = item.Title,
                Preconditions = item.Preconditions,
                Priority = item.Priority,
                Category = normalizedCategories[itemIndex],
                Steps = item.Steps,
                RequirementRefs = reqRefs,
                AiSuggestedReqs = true
            }, ct);

            if (!tcRes.IsSuccess || tcRes.Data == null)
                return ServiceResult<ItemsDto<TestCaseDto>>.FromError(tcRes);
            createdIds.Add(tcRes.Data.TestCaseId);
        }

        if (createdIds.Count > 0)
        {
            _db.TestActivities.Add(new TestActivity
            {
                PlanId = planId,
                Type = TestActivityTypes.TestCase,
                At = _clock.GetUtcNow().UtcDateTime,
                ActorId = _currentUser.UserId,
                Text = $"Generated {createdIds.Count} test case(s) from requirements (AI)"
            });
            await _db.SaveChangesAsync(ct);
        }

        var dtos = await LoadAndMapTestCasesAsync(projectId, createdIds, ct);
        return ServiceResult<ItemsDto<TestCaseDto>>.Created(new ItemsDto<TestCaseDto>(dtos));
    }

    private static bool TryNormalizeAiTestCaseCategory(string? category, out string normalized)
    {
        normalized = (category?.Trim().ToUpperInvariant()) switch
        {
            "FUNCTIONAL" or "FLOW" or "CHỨC NĂNG" => TestCaseCategories.Functional,
            "BOUNDARY" or "VALIDATE" => TestCaseCategories.Boundary,
            "NEGATIVE" => TestCaseCategories.Negative,
            "UI" or "GIAO DIỆN" => TestCaseCategories.Ui,
            "SECURITY" or "PHÂN QUYỀN" => TestCaseCategories.Security,
            "PERFORMANCE" => TestCaseCategories.Performance,
            _ => string.Empty
        };
        return normalized.Length > 0;
    }

    // =========================================================================
    // Internal Builders & Helpers
    // =========================================================================

    private async Task<TestPlanDto?> BuildPlanDtoAsync(long planId, CancellationToken ct)
    {
        var plan = await _db.TestPlans
            .AsNoTracking()
            .Include(p => p.HandoverBy)
            .FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan == null) return null;

        var envs = await _db.TestEnvironments
            .AsNoTracking()
            .Include(e => e.Builds)
                .ThenInclude(b => b.DeployedBy)
            .Where(e => e.PlanId == planId && !e.IsDeleted)
            .OrderBy(e => e.Position)
            .ThenBy(e => e.Id)
            .ToListAsync(ct);

        var envDtos = new List<TestEnvironmentDto>(envs.Count);
        foreach (var e in envs)
        {
            envDtos.Add(await MapEnvironmentDtoAsync(e, ct));
        }

        var modules = await BuildPlanModulesDtoAsync(plan.ProjectId, planId, ct);
        var moduleIds = modules.Select(m => m.ModuleId).ToList();

        var assignments = moduleIds.Count == 0
            ? new List<TestAssignmentDto>()
            : await _db.TestAssignments
                .AsNoTracking()
                .Include(a => a.Assignee)
                .Where(a => moduleIds.Contains(a.ModuleId) && !a.Environment.IsDeleted)
                .Select(a => new TestAssignmentDto
                {
                    ModuleId = a.ModuleId,
                    EnvId = a.EnvId,
                    Assignee = MapUserRef(a.Assignee)
                })
                .ToListAsync(ct);

        var members = await _db.ProjectMembers
            .AsNoTracking()
            .Include(pm => pm.User)
            .Include(pm => pm.Role)
            .Where(pm => pm.ProjectId == plan.ProjectId && pm.IsActive)
            .OrderBy(pm => pm.JoinedAt)
            .ToListAsync(ct);

        var people = members.Select(pm =>
        {
            var roleCode = pm.Role?.Code ?? string.Empty;
            var roleName = pm.Role?.Name ?? string.Empty;
            var isQa = Regex.IsMatch(roleCode + " " + roleName, "qa|test|quality", RegexOptions.IgnoreCase);
            return new TestPersonDto
            {
                UserId = pm.UserId,
                FullName = pm.User.FullName,
                Email = pm.User.Email,
                AvatarUrl = pm.User.AvatarUrl,
                Qa = isQa
            };
        }).ToList();

        TestHandoverDto? handover = null;
        if (plan.Status == TestPlanStatuses.HandedOver && plan.HandoverAt.HasValue && plan.HandoverBy != null)
        {
            handover = new TestHandoverDto
            {
                By = MapUserRef(plan.HandoverBy),
                At = FormatUtc(plan.HandoverAt.Value),
                Note = plan.HandoverNote
            };
        }

        return new TestPlanDto
        {
            PlanId = plan.Id,
            ProjectId = plan.ProjectId,
            Name = plan.Name,
            StartDate = FormatOptionalDate(plan.StartDate),
            EndDate = FormatOptionalDate(plan.EndDate),
            Status = plan.Status,
            CreatedAt = FormatUtc(plan.CreatedAt),
            Handover = handover,
            Environments = envDtos,
            Modules = modules,
            Assignments = assignments,
            Layout = ParseLayout(plan.SheetLayout),
            People = people
        };
    }

    private async Task<TestEnvironmentDto> MapEnvironmentDtoAsync(TestEnvironment env, CancellationToken ct)
    {
        var builds = env.Builds
            .OrderByDescending(b => b.DeployedAt)
            .ThenByDescending(b => b.Id)
            .ToList();

        var allIssueIds = new HashSet<long>();
        var parsedIssueIdsByBuild = new Dictionary<long, List<long>>();
        foreach (var b in builds)
        {
            var ids = SafeDeserialize<List<long>>(b.FixedIssueIds) ?? new List<long>();
            parsedIssueIdsByBuild[b.Id] = ids;
            foreach (var id in ids) allIssueIds.Add(id);
        }

        var issueKeys = allIssueIds.Count == 0
            ? new Dictionary<long, string>()
            : await _db.Issues
                .AsNoTracking()
                .Where(i => allIssueIds.Contains(i.Id))
                .ToDictionaryAsync(i => i.Id, i => i.Key, ct);

        var resultCounts = await _db.TestResults
            .AsNoTracking()
            .Where(r => r.EnvId == env.Id)
            .GroupBy(r => r.Build)
            .Select(g => new { Build = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Build, x => x.Count, StringComparer.OrdinalIgnoreCase, ct);

        var buildDtos = builds.Select(b => new TestBuildDto
        {
            BuildId = b.Id,
            Version = b.Version,
            DeployedAt = FormatUtc(b.DeployedAt),
            DeployedBy = b.DeployedBy != null ? MapUserRef(b.DeployedBy) : null,
            FixedBugs = parsedIssueIdsByBuild[b.Id]
                .Select(id => new IssueKeyRefDto
                {
                    IssueId = id,
                    Key = issueKeys.GetValueOrDefault(id, $"#{id}")
                })
                .ToList(),
            ResultCount = resultCounts.GetValueOrDefault(b.Version, 0)
        }).ToList();

        return new TestEnvironmentDto
        {
            EnvId = env.Id,
            Name = env.Name,
            Url = NullIfEmpty(env.Url),
            Note = NullIfEmpty(env.Note),
            Position = env.Position,
            CurrentBuild = builds.FirstOrDefault()?.Version,
            Builds = buildDtos
        };
    }

    private async Task<List<TestModuleDto>> BuildPlanModulesDtoAsync(long projectId, long planId, CancellationToken ct)
    {
        var modules = await _db.TestModules
            .AsNoTracking()
            .Include(m => m.Document)
            .Include(m => m.Sections)
            .Where(m => m.PlanId == planId)
            .OrderBy(m => m.Position)
            .ThenBy(m => m.Id)
            .ToListAsync(ct);

        if (modules.Count == 0) return new List<TestModuleDto>();

        var staleSecIds = await _db.TestCases
            .AsNoTracking()
            .Where(tc => tc.PlanId == planId &&
                         tc.Status == TestCaseStatuses.Active &&
                         tc.RequirementLinks.Any(l => l.ChangedVersionLabel != null))
            .Select(tc => tc.SectionId)
            .Distinct()
            .ToListAsync(ct);
        var staleSecSet = staleSecIds.ToHashSet();

        return modules.Select(m => MapModuleDto(m, staleSecSet)).ToList();
    }

    private async Task<TestModuleDto?> BuildSingleModuleDtoAsync(long moduleId, CancellationToken ct)
    {
        var module = await _db.TestModules
            .AsNoTracking()
            .Include(m => m.Document)
            .Include(m => m.Sections)
            .FirstOrDefaultAsync(m => m.Id == moduleId, ct);
        if (module == null) return null;

        var staleSecIds = await _db.TestCases
            .AsNoTracking()
            .Where(tc => tc.ModuleId == moduleId &&
                         tc.Status == TestCaseStatuses.Active &&
                         tc.RequirementLinks.Any(l => l.ChangedVersionLabel != null))
            .Select(tc => tc.SectionId)
            .Distinct()
            .ToListAsync(ct);

        return MapModuleDto(module, staleSecIds.ToHashSet());
    }

    private static TestModuleDto MapModuleDto(TestModule m, HashSet<long> staleSecIds)
    {
        var docCode = m.Document != null && !string.IsNullOrWhiteSpace(m.Document.Code)
            ? m.Document.Code
            : (m.DocumentId.HasValue ? $"DOC-{m.DocumentId.Value}" : m.ShortCode);
        var docTitle = m.Document?.Title ?? m.Name;

        var allSecs = m.Sections
            .OrderBy(s => s.Position)
            .ThenBy(s => s.Id)
            .ToList();

        var topSecs = allSecs.Where(s => s.ParentId == null).ToList();
        var childrenByParent = allSecs
            .Where(s => s.ParentId.HasValue)
            .GroupBy(s => s.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var sectionDtos = new List<TestSectionDto>(allSecs.Count);
        foreach (var top in topSecs)
        {
            var children = childrenByParent.GetValueOrDefault(top.Id) ?? new List<TestSection>();
            var topReqChanged = staleSecIds.Contains(top.Id) || children.Any(c => staleSecIds.Contains(c.Id));
            sectionDtos.Add(new TestSectionDto
            {
                SectionId = top.Id,
                ModuleId = top.ModuleId,
                ParentId = null,
                Code = top.Code,
                Title = top.Title,
                AnchorId = top.AnchorId,
                ReqChanged = topReqChanged,
                Position = top.Position
            });

            foreach (var ch in children)
            {
                sectionDtos.Add(new TestSectionDto
                {
                    SectionId = ch.Id,
                    ModuleId = ch.ModuleId,
                    ParentId = ch.ParentId,
                    Code = ch.Code,
                    Title = ch.Title,
                    AnchorId = ch.AnchorId,
                    ReqChanged = staleSecIds.Contains(ch.Id),
                    Position = ch.Position
                });
            }
        }

        return new TestModuleDto
        {
            ModuleId = m.Id,
            DocumentId = m.DocumentId ?? 0,
            DocumentCode = docCode,
            DocumentTitle = docTitle,
            Name = m.Name,
            ShortCode = m.ShortCode,
            Position = m.Position,
            Sections = sectionDtos
        };
    }

    private async Task<List<TestCaseDto>> LoadAndMapTestCasesAsync(
        long projectId,
        List<long> caseIds,
        CancellationToken ct)
    {
        if (caseIds.Count == 0) return new List<TestCaseDto>();

        var cases = await _db.TestCases
            .AsNoTracking()
            .Include(tc => tc.Module)
            .Include(tc => tc.RequirementLinks)
                .ThenInclude(l => l.Document)
            .Include(tc => tc.BugIssue)
                .ThenInclude(i => i!.Status)
            .Include(tc => tc.Results)
                .ThenInclude(r => r.By)
            .Include(tc => tc.Results)
                .ThenInclude(r => r.BehalfOf)
            .Include(tc => tc.RetestFlags)
                .ThenInclude(f => f.BugIssue)
            .Where(tc => caseIds.Contains(tc.Id))
            .ToListAsync(ct);

        if (cases.Count == 0) return new List<TestCaseDto>();

        // Compute ModuleCode per module (e.g. PHI-1, PHI-2) matching testMockStore.ts
        var moduleIds = cases.Select(c => c.ModuleId).Distinct().ToList();
        var allModuleCases = await _db.TestCases
            .AsNoTracking()
            .Include(tc => tc.Module)
            .Include(tc => tc.Section)
            .Where(tc => moduleIds.Contains(tc.ModuleId) && tc.Status == TestCaseStatuses.Active)
            .OrderBy(tc => tc.ModuleId)
            .ThenBy(tc => tc.Section.ParentId ?? tc.SectionId)
            .ThenBy(tc => tc.Section.Position)
            .ThenBy(tc => tc.Position)
            .ThenBy(tc => tc.Id)
            .Select(tc => new { tc.Id, tc.ModuleId, tc.Module.ShortCode })
            .ToListAsync(ct);

        var moduleCodeMap = new Dictionary<long, string>();
        foreach (var grp in allModuleCases.GroupBy(x => x.ModuleId))
        {
            var idx = 1;
            foreach (var item in grp)
            {
                moduleCodeMap[item.Id] = $"{item.ShortCode}-{idx++}";
            }
        }

        // Load requirement details for linked requirements
        var docIds = cases
            .SelectMany(tc => tc.RequirementLinks.Select(l => l.DocumentId))
            .Distinct()
            .ToList();

        var docs = docIds.Count == 0
            ? new Dictionary<long, DocumentNode>()
            : await _db.DocumentNodes
                .AsNoTracking()
                .Where(d => docIds.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, ct);

        var verIds = docs.Values
            .Select(d => d.BaselineVersionId ?? d.CurrentVersionId)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .Distinct()
            .ToList();

        var reqItems = verIds.Count == 0
            ? new List<RequirementItem>()
            : await _db.RequirementItems
                .AsNoTracking()
                .Where(r => verIds.Contains(r.VersionId))
                .ToListAsync(ct);

        var reqLookup = new Dictionary<(long DocId, string ReqUid), RequirementItem>();
        foreach (var r in reqItems)
        {
            reqLookup[(r.DocumentId, r.ReqUid.ToUpperInvariant())] = r;
        }

        var orderMap = caseIds
            .Select((id, index) => (id, index))
            .ToDictionary(x => x.id, x => x.index);

        return cases
            .OrderBy(c => orderMap.GetValueOrDefault(c.Id, int.MaxValue))
            .Select(tc =>
            {
                var steps = SafeDeserialize<List<TestStepDto>>(tc.Steps) ?? new List<TestStepDto>();
                var customFields = SafeDeserialize<Dictionary<string, object?>>(tc.CustomFields) ?? new Dictionary<string, object?>();

                var reqDtos = tc.RequirementLinks
                    .OrderBy(l => l.LinkedAt)
                    .Select(l =>
                    {
                        var doc = docs.GetValueOrDefault(l.DocumentId) ?? l.Document;
                        var docCode = doc != null && !string.IsNullOrWhiteSpace(doc.Code)
                            ? doc.Code
                            : $"DOC-{l.DocumentId}";
                        var docTitle = doc?.Title ?? string.Empty;
                        reqLookup.TryGetValue((l.DocumentId, l.ReqUid.ToUpperInvariant()), out var rItem);

                        return new ReqRefDto
                        {
                            DocumentId = l.DocumentId,
                            DocumentCode = docCode,
                            DocumentTitle = docTitle,
                            ReqUid = l.ReqUid,
                            ReqKey = rItem != null ? $"{docCode}:{l.ReqUid}" : null,
                            Title = rItem?.Title,
                            AnchorId = rItem?.AnchorId,
                            Changed = l.ChangedVersionLabel != null,
                            Freshness = rItem == null ? null : (l.ChangedVersionLabel != null ? "STALE" : "FRESH"),
                            ChangedNote = l.ChangedVersionLabel != null ? $"Đổi trong bản {l.ChangedVersionLabel}" : null
                        };
                    })
                    .ToList();

                TestBugRefDto? bugDto = null;
                if (tc.BugIssue != null && !tc.BugIssue.IsDeleted)
                {
                    bugDto = new TestBugRefDto
                    {
                        IssueId = tc.BugIssue.Id,
                        Key = tc.BugIssue.Key,
                        Title = tc.BugIssue.Title,
                        Priority = tc.BugIssue.Priority,
                        StatusName = tc.BugIssue.Status?.Name ?? "Open",
                        StatusCategory = tc.BugIssue.Status?.Category ?? StatusCategories.Todo
                    };
                }

                // Results sorted chronologically (old -> new) so results[results.length - 1] is the latest!
                var resultDtos = tc.Results
                    .OrderBy(r => r.At)
                    .ThenBy(r => r.Id)
                    .Select(r => new TestResultDto
                    {
                        ResultId = r.Id,
                        EnvId = r.EnvId,
                        Build = r.Build,
                        Result = r.Result,
                        By = MapUserRef(r.By),
                        At = FormatUtc(r.At),
                        BehalfOf = r.BehalfOf != null ? MapUserRef(r.BehalfOf) : null
                    })
                    .ToList();

                var retestDtos = tc.RetestFlags
                    .OrderBy(f => f.EnvId)
                    .Select(f => new RetestFlagDto
                    {
                        EnvId = f.EnvId,
                        Reason = NormalizeRetestReason(f.Reason),
                        Bug = f.BugIssue != null
                            ? new IssueKeyRefDto { IssueId = f.BugIssue.Id, Key = f.BugIssue.Key }
                            : null,
                        Build = f.Build,
                        ReqKey = f.ReqKey,
                        At = FormatUtc(f.At)
                    })
                    .ToList();

                var modCode = moduleCodeMap.GetValueOrDefault(tc.Id, $"{tc.Module?.ShortCode ?? "TC"}-0");

                return new TestCaseDto
                {
                    TestCaseId = tc.Id,
                    PlanId = tc.PlanId,
                    ModuleId = tc.ModuleId,
                    SectionId = tc.SectionId,
                    Code = tc.Code,
                    ModuleCode = modCode,
                    Title = tc.Title,
                    Preconditions = NullIfEmpty(tc.Preconditions),
                    Steps = steps,
                    Actual = NullIfEmpty(tc.Actual),
                    Requirements = reqDtos,
                    AiSuggestedReqs = tc.AiSuggestedReqs,
                    Priority = TestCasePriorities.Normalize(tc.Priority),
                    Category = NullIfEmpty(tc.Category),
                    Browser = NullIfEmpty(tc.Browser),
                    CustomFields = customFields,
                    CellStyles = DeserializeCellStyles(tc.CellStyles),
                    Status = tc.Status,
                    Position = tc.Position,
                    Bug = bugDto,
                    Results = resultDtos,
                    Retest = retestDtos,
                    UpdatedAt = FormatUtc(tc.UpdatedAt)
                };
            })
            .ToList();
    }

    private async Task ApplyCaseChangesAsync(
        TestCase tc,
        UpdateTestCaseRequest changes,
        DateTime now,
        CancellationToken ct)
    {
        if (changes.SectionId.HasValue && changes.SectionId.Value != tc.SectionId)
        {
            var targetSec = await _db.TestSections
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == changes.SectionId.Value && s.Module.PlanId == tc.PlanId, ct);
            if (targetSec != null)
            {
                tc.SectionId = targetSec.Id;
                tc.ModuleId = targetSec.ModuleId;
            }
        }

        if (changes.Title != null)
            tc.Title = changes.Title.Trim();

        if (changes.PreconditionsSpecified)
            tc.Preconditions = changes.Preconditions?.Trim() ?? string.Empty;

        if (changes.Steps != null)
            tc.Steps = JsonSerializer.Serialize(changes.Steps, JsonOptions);

        if (changes.ActualSpecified)
            tc.Actual = changes.Actual?.Trim() ?? string.Empty;

        if (changes.AiSuggestedReqs.HasValue)
            tc.AiSuggestedReqs = changes.AiSuggestedReqs.Value;

        if (!string.IsNullOrWhiteSpace(changes.Priority))
            tc.Priority = TestCasePriorities.Normalize(changes.Priority);

        if (changes.CategorySpecified)
            tc.Category = changes.Category?.Trim() ?? string.Empty;

        if (changes.BrowserSpecified)
            tc.Browser = changes.Browser?.Trim() ?? string.Empty;

        if (changes.CustomFields != null)
            tc.CustomFields = JsonSerializer.Serialize(changes.CustomFields, JsonOptions);

        if (changes.CellStyles != null)
        {
            var merged = DeserializeCellStyles(tc.CellStyles);
            foreach (var (rawKey, rawStyle) in changes.CellStyles)
            {
                var key = rawKey?.Trim();
                if (string.IsNullOrEmpty(key) || key.Length > 64) continue;

                var cleaned = CleanCellStyle(rawStyle);
                if (cleaned == null)
                    merged.Remove(key);
                else
                    merged[key] = cleaned;
            }
            tc.CellStyles = JsonSerializer.Serialize(merged, JsonOptions);
        }

        if (!string.IsNullOrWhiteSpace(changes.Status))
        {
            var st = changes.Status.Trim().ToUpperInvariant();
            if (TestCaseStatuses.IsValid(st))
                tc.Status = st;
        }

        if (changes.Position.HasValue)
            tc.Position = changes.Position.Value;

        if (changes.RequirementRefs != null)
        {
            await SyncTestCaseReqLinksAsync(tc, tc.ProjectId, changes.RequirementRefs, now, ct);
        }

        var hasNonStyleChange =
            changes.SectionId.HasValue ||
            changes.Title != null ||
            changes.PreconditionsSpecified ||
            changes.Steps != null ||
            changes.ActualSpecified ||
            changes.AiSuggestedReqs.HasValue ||
            !string.IsNullOrWhiteSpace(changes.Priority) ||
            changes.CategorySpecified ||
            changes.BrowserSpecified ||
            changes.CustomFields != null ||
            !string.IsNullOrWhiteSpace(changes.Status) ||
            changes.Position.HasValue ||
            changes.RequirementRefs != null;

        if (hasNonStyleChange)
        {
            tc.UpdatedAt = now;
        }
    }

    private static Dictionary<string, CellStyleDto> DeserializeCellStyles(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, CellStyleDto>();
        try
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, CellStyleDto?>>(json, JsonOptions);
            if (raw == null) return new Dictionary<string, CellStyleDto>();
            var result = new Dictionary<string, CellStyleDto>();
            foreach (var (k, v) in raw)
            {
                var key = k?.Trim();
                if (string.IsNullOrEmpty(key) || key.Length > 64) continue;
                var cleaned = CleanCellStyle(v);
                if (cleaned != null) result[key] = cleaned;
            }
            return result;
        }
        catch
        {
            return new Dictionary<string, CellStyleDto>();
        }
    }

    private static CellStyleDto? CleanCellStyle(CellStyleDto? raw)
    {
        if (raw == null) return null;

        bool? b = raw.B == true ? true : null;
        bool? i = raw.I == true ? true : null;
        bool? u = raw.U == true ? true : null;
        bool? s = raw.S == true ? true : null;
        int? size = raw.Size is 11 or 12 or 14 or 16 or 18 or 20 or 24 ? raw.Size : null;
        string? color = !string.IsNullOrWhiteSpace(raw.Color) && HexColorRegex.IsMatch(raw.Color.Trim())
            ? raw.Color.Trim().ToLowerInvariant()
            : null;
        string? fill = !string.IsNullOrWhiteSpace(raw.Fill) && HexColorRegex.IsMatch(raw.Fill.Trim())
            ? raw.Fill.Trim().ToLowerInvariant()
            : null;
        string? align = raw.Align?.Trim().ToLowerInvariant() is "center" or "right"
            ? raw.Align.Trim().ToLowerInvariant()
            : null;

        if (b == null && i == null && u == null && s == null && size == null && color == null && fill == null && align == null)
            return null;

        return new CellStyleDto
        {
            B = b,
            I = i,
            U = u,
            S = s,
            Size = size,
            Color = color,
            Fill = fill,
            Align = align
        };
    }

    private async Task SyncTestCaseReqLinksAsync(
        TestCase tc,
        long projectId,
        List<RequirementRefInputDto> refs,
        DateTime now,
        CancellationToken ct)
    {
        var existing = await _db.TestCaseRequirementLinks
            .Where(l => l.TestCaseId == tc.Id)
            .ToListAsync(ct);

        if (existing.Count > 0)
        {
            _db.TestCaseRequirementLinks.RemoveRange(existing);
        }

        var normalizedRefs = refs
            .Where(r => r.DocumentId > 0 && !string.IsNullOrWhiteSpace(r.ReqUid))
            .Select(r => (DocumentId: r.DocumentId, ReqUid: r.ReqUid.Trim().ToUpperInvariant()))
            .Distinct()
            .ToList();

        if (normalizedRefs.Count == 0) return;

        var docIds = normalizedRefs.Select(r => r.DocumentId).Distinct().ToList();
        var docs = await _db.DocumentNodes
            .AsNoTracking()
            .Where(d => d.ProjectId == projectId && docIds.Contains(d.Id) && !d.IsDeleted)
            .ToDictionaryAsync(d => d.Id, ct);

        var verIds = docs.Values
            .Select(d => d.BaselineVersionId ?? d.CurrentVersionId)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .Distinct()
            .ToList();

        var reqItems = verIds.Count == 0
            ? new List<RequirementItem>()
            : await _db.RequirementItems
                .AsNoTracking()
                .Where(r => verIds.Contains(r.VersionId))
                .ToListAsync(ct);

        foreach (var (docId, reqUid) in normalizedRefs)
        {
            if (!docs.TryGetValue(docId, out var doc)) continue;
            var verId = doc.BaselineVersionId ?? doc.CurrentVersionId;
            var rItem = verId.HasValue
                ? reqItems.FirstOrDefault(r => r.VersionId == verId.Value && string.Equals(r.ReqUid, reqUid, StringComparison.OrdinalIgnoreCase))
                : null;

            _db.TestCaseRequirementLinks.Add(new TestCaseRequirementLink
            {
                TestCaseId = tc.Id,
                DocumentId = docId,
                ReqUid = reqUid,
                ReqHash = rItem?.ContentHash ?? string.Empty,
                ChangedVersionLabel = null,
                LinkedAt = now
            });
        }

        // Also clear any REQ_CHANGED retest flags on this test case when requirements are explicitly re-linked/acknowledged
        var reqFlags = await _db.TestRetestFlags
            .Where(f => f.TestCaseId == tc.Id && f.Reason == TestRetestReasons.ReqChanged)
            .ToListAsync(ct);
        if (reqFlags.Count > 0)
        {
            _db.TestRetestFlags.RemoveRange(reqFlags);
        }
    }

    private async Task<string> AllocateNextTcCodeAsync(long planId, CancellationToken ct)
    {
        var codes = await _db.TestCases
            .AsNoTracking()
            .Where(tc => tc.PlanId == planId)
            .Select(tc => tc.Code)
            .ToListAsync(ct);

        var maxNum = 0;
        foreach (var c in codes)
        {
            var m = Regex.Match(c ?? string.Empty, @"^TC[_-](\d+)$", RegexOptions.IgnoreCase);
            if (m.Success && int.TryParse(m.Groups[1].Value, out var n) && n > maxNum)
            {
                maxNum = n;
            }
        }

        if (maxNum == 0 && codes.Count > 0)
            maxNum = codes.Count;

        return $"TC_{maxNum + 1}";
    }

    private async Task RecodeModuleSectionsAsync(long moduleId, CancellationToken ct)
    {
        var all = await _db.TestSections
            .Where(s => s.ModuleId == moduleId)
            .OrderBy(s => s.Position)
            .ThenBy(s => s.Id)
            .ToListAsync(ct);

        var tops = all.Where(s => s.ParentId == null).ToList();
        for (var i = 0; i < tops.Count; i++)
        {
            var top = tops[i];
            top.Code = $"{i + 1}.";
            top.Position = i + 1;

            var children = all.Where(s => s.ParentId == top.Id).ToList();
            for (var j = 0; j < children.Count; j++)
            {
                children[j].Code = $"{i + 1}.{j + 1}";
                children[j].Position = j + 1;
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    private static List<DocSectionDto> DefaultModuleSections(List<DocSectionDto> all)
    {
        if (all.Count == 0) return new List<DocSectionDto>();

        var withReq = all.Where(s => s.ReqUids != null && s.ReqUids.Count > 0).ToList();
        if (withReq.Count > 0) return withReq;

        var top = all.Where(s => s.Level <= 2).ToList();
        return top.Count > 0 ? top : all;
    }

    private static DocSectionDto? FindParentHeading(List<DocSectionDto> sections, string? reqAnchorId, string reqUid)
    {
        if (sections.Count == 0) return null;

        var byReqUid = sections.FirstOrDefault(s =>
            s.ReqUids != null && s.ReqUids.Any(u => string.Equals(u, reqUid, StringComparison.OrdinalIgnoreCase)));
        if (byReqUid != null) return byReqUid;

        var byAnchor = !string.IsNullOrWhiteSpace(reqAnchorId)
            ? sections.FirstOrDefault(s => string.Equals(s.AnchorId, reqAnchorId, StringComparison.OrdinalIgnoreCase))
            : null;
        if (byAnchor != null) return byAnchor;

        return sections.FirstOrDefault();
    }

    private static TestPlacementDto ComputePlacement(
        TestPlanDto? plan,
        long documentId,
        string documentTitle,
        string? anchorId,
        string sectionTitle,
        string category)
    {
        var cleanSectionTitle = string.IsNullOrWhiteSpace(sectionTitle) ? documentTitle : StripHeadingNumber(sectionTitle);
        var cleanCategory = string.IsNullOrWhiteSpace(category) ? "Functional" : category.Trim();

        var mod = plan?.Modules.FirstOrDefault(m => m.DocumentId == documentId);
        var topSections = mod?.Sections.Where(s => s.ParentId == null).ToList() ?? new List<TestSectionDto>();
        var sec = (!string.IsNullOrWhiteSpace(anchorId)
                ? topSections.FirstOrDefault(s => string.Equals(s.AnchorId, anchorId, StringComparison.OrdinalIgnoreCase))
                : null)
            ?? topSections.FirstOrDefault(s => string.Equals(s.Title.Trim(), cleanSectionTitle.Trim(), StringComparison.OrdinalIgnoreCase));

        var grp = sec != null && mod != null
            ? mod.Sections.FirstOrDefault(s => s.ParentId == sec.SectionId && string.Equals(s.Title.Trim(), cleanCategory, StringComparison.OrdinalIgnoreCase))
            : null;

        return new TestPlacementDto
        {
            ModuleId = mod?.ModuleId,
            SectionId = sec?.SectionId,
            GroupId = grp?.SectionId,
            DocumentId = documentId,
            AnchorId = anchorId,
            SectionTitle = sec?.Title ?? cleanSectionTitle,
            Category = grp?.Title ?? cleanCategory
        };
    }

    private static string StripHeadingNumber(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return string.Empty;
        return Regex.Replace(title.Trim(), @"^\d+(\.\d+)*\.?\s+", string.Empty);
    }

    private static string MakeShortCode(string name, HashSet<string> existing)
    {
        var normalized = RemoveDiacritics(name).ToUpperInvariant();
        var words = Regex.Matches(normalized, @"[A-Z0-9]+")
            .Select(m => m.Value)
            .ToList();

        var baseCode = words.Count >= 2
            ? string.Concat(words.Select(w => w[0]))
            : (words.FirstOrDefault() ?? "MOD");

        if (baseCode.Length > 4) baseCode = baseCode[..4];
        if (string.IsNullOrWhiteSpace(baseCode)) baseCode = "MOD";

        var code = baseCode;
        var idx = 2;
        while (existing.Contains(code))
        {
            code = $"{baseCode}{idx++}";
        }
        return code;
    }

    private static string RemoveDiacritics(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var formD = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (var ch in formD)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (uc != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch switch
                {
                    'đ' => 'd',
                    'Đ' => 'D',
                    _ => ch
                });
            }
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static SheetLayoutDto ParseLayout(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new SheetLayoutDto();

        try
        {
            var parsed = JsonSerializer.Deserialize<SheetLayoutDto>(json, JsonOptions);
            return NormalizeLayout(parsed);
        }
        catch
        {
            return new SheetLayoutDto();
        }
    }

    private static SheetLayoutDto NormalizeLayout(SheetLayoutDto? input)
    {
        var def = new SheetLayoutDto();
        if (input == null) return def;

        var order = (input.Order ?? new List<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        if (order.Count == 0)
            order = def.Order;

        return new SheetLayoutDto
        {
            Order = order,
            Labels = input.Labels ?? new Dictionary<string, string>(),
            Widths = input.Widths ?? new Dictionary<string, int>(),
            CustomColumns = input.CustomColumns ?? new List<CustomColumnDto>()
        };
    }

    private static DateOnly ParseOptionalDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr))
            return DateOnly.MinValue;

        if (DateOnly.TryParseExact(dateStr.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d;

        if (DateOnly.TryParse(dateStr.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var d2))
            return d2;

        return DateOnly.MinValue;
    }

    private static string? FormatOptionalDate(DateOnly d)
        => d == DateOnly.MinValue || d.Year <= 1900
            ? null
            : d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string? NullIfEmpty(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s;

    private static string NormalizeRetestReason(string? reason)
        => (reason?.Trim().ToUpperInvariant()) switch
        {
            "BUG_FIXED" => "BUG_FIXED",
            "REQ_CHANGED" => "REQ_CHANGED",
            _ => "REGRESSION"
        };

    private static string NormalizeActivityType(string? type)
        => (type?.Trim().ToUpperInvariant()) switch
        {
            "DEPLOY" => "BUILD",
            "CASE" => "TESTCASE",
            var t when !string.IsNullOrWhiteSpace(t) => t,
            _ => "SETUP"
        };

    private async Task<UserRefDto> GetUserRefAsync(long userId, CancellationToken ct)
    {
        var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, ct);
        return u != null
            ? MapUserRef(u)
            : new UserRefDto { UserId = userId, FullName = "System", Email = "" };
    }
}
