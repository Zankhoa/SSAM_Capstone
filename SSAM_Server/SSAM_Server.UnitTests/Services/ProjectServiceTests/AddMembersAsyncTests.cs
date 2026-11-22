using Microsoft.AspNetCore.Http;
using Moq;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Projects;
using SSAM_Server.Interfaces.Security;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.ProjectServiceTests;

public class AddMembersAsyncTests : ProjectServiceTestBase
{
    private ProjectAccessContext SetupValidAdd()
    {
        var access = SetupProjectAccess(permissions: new[] { PermissionKeys.ProjectMemberManage });
        WorkspaceRepositoryMock
            .Setup(r => r.IsMemberAsync(100, It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        ProjectRepositoryMock
            .Setup(r => r.IsActiveMemberAsync(200, It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        ProjectRepositoryMock
            .Setup(r => r.GetMemberRecordAsync(200, It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProjectMember?)null);
        ProjectRepositoryMock
            .Setup(r => r.GetMemberDtoAsync(200, 100, It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns<long, long, long, CancellationToken>((_, _, userId, _) => Task.FromResult<ProjectMemberDto?>(CreateMemberDto(userId)));
        return access;
    }

    // UTC001: Adding a valid workspace member creates an active project membership and returns its DTO.
    [Fact]
    public async Task AddMembersAsync_WithValidMember_ReturnsCreatedAndAddsMember()
    {
        SetupValidAdd();
        ProjectMember? added = null;
        ProjectRepositoryMock
            .Setup(r => r.AddProjectMemberAsync(It.IsAny<ProjectMember>(), It.IsAny<CancellationToken>()))
            .Callback<ProjectMember, CancellationToken>((member, _) => added = member)
            .Returns(Task.CompletedTask);

        var result = await ProjectService.AddMembersAsync(200, new AddProjectMembersRequestDto
        {
            Members = new List<ProjectMemberInputDto> { new() { UserId = 20, RoleId = 52 } }
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.NotNull(added);
        Assert.Equal(20, added.UserId);
        Assert.Equal(52, added.RoleId);
        Assert.True(added.IsActive);
        Assert.Single(result.Data!.Items);
    }

    // UTC002: Adding an inactive historical member reactivates and updates the existing record.
    [Fact]
    public async Task AddMembersAsync_WhenInactiveRecordExists_ReactivatesRecord()
    {
        SetupValidAdd();
        var previous = new ProjectMember { ProjectId = 200, UserId = 20, RoleId = 51, IsActive = false };
        ProjectRepositoryMock.Setup(r => r.GetMemberRecordAsync(200, 20, It.IsAny<CancellationToken>())).ReturnsAsync(previous);

        var result = await ProjectService.AddMembersAsync(200, new AddProjectMembersRequestDto
        {
            Members = new List<ProjectMemberInputDto> { new() { UserId = 20, RoleId = 52 } }
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.True(previous.IsActive);
        Assert.Equal(52, previous.RoleId);
        ProjectRepositoryMock.Verify(r => r.AddProjectMemberAsync(It.IsAny<ProjectMember>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC003: An empty member list is rejected.
    [Fact]
    public async Task AddMembersAsync_WhenMemberListIsEmpty_ReturnsValidationError()
    {
        SetupValidAdd();

        var result = await ProjectService.AddMembersAsync(200, new AddProjectMembersRequestDto(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
    }

    // UTC004: A user who is not in the workspace cannot be added to the project.
    [Fact]
    public async Task AddMembersAsync_WhenUserIsNotWorkspaceMember_ReturnsBadRequest()
    {
        SetupValidAdd();
        WorkspaceRepositoryMock.Setup(r => r.IsMemberAsync(100, 20, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await ProjectService.AddMembersAsync(200, new AddProjectMembersRequestDto
        {
            Members = new List<ProjectMemberInputDto> { new() { UserId = 20, RoleId = 52 } }
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("NOT_WORKSPACE_MEMBER", result.ErrorCode);
    }

    // UTC005: An existing active project member cannot be added again.
    [Fact]
    public async Task AddMembersAsync_WhenUserIsAlreadyMember_ReturnsConflict()
    {
        SetupValidAdd();
        ProjectRepositoryMock.Setup(r => r.IsActiveMemberAsync(200, 20, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await ProjectService.AddMembersAsync(200, new AddProjectMembersRequestDto
        {
            Members = new List<ProjectMemberInputDto> { new() { UserId = 20, RoleId = 52 } }
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("ALREADY_MEMBER", result.ErrorCode);
    }

    // UTC006: An archived project cannot accept new members.
    [Fact]
    public async Task AddMembersAsync_WhenProjectArchived_ReturnsConflict()
    {
        SetupProjectAccess(permissions: new[] { PermissionKeys.ProjectMemberManage }, projectActive: false);

        var result = await ProjectService.AddMembersAsync(200, new AddProjectMembersRequestDto
        {
            Members = new List<ProjectMemberInputDto> { new() { UserId = 20, RoleId = 52 } }
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("PROJECT_ARCHIVED", result.ErrorCode);
    }

    // UTC007: A missing project role returns not found before persistence.
    [Fact]
    public async Task AddMembersAsync_WhenRoleIsMissing_ReturnsNotFound()
    {
        SetupValidAdd();
        CompanyRepositoryMock.Setup(r => r.GetRoleByIdAsync(10, 52, It.IsAny<CancellationToken>())).ReturnsAsync((CompanyRole?)null);

        var result = await ProjectService.AddMembersAsync(200, new AddProjectMembersRequestDto
        {
            Members = new List<ProjectMemberInputDto> { new() { UserId = 20, RoleId = 52 } }
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
    }

    // UTC008: A role outside project scope is rejected.
    [Fact]
    public async Task AddMembersAsync_WhenRoleHasWrongScope_ReturnsBadRequest()
    {
        SetupValidAdd();
        CompanyRepositoryMock.Setup(r => r.GetRoleByIdAsync(10, 52, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyRole { Id = 52, CompanyId = 10, Scope = RoleScopes.Workspace, IsActive = true });

        var result = await ProjectService.AddMembersAsync(200, new AddProjectMembersRequestDto
        {
            Members = new List<ProjectMemberInputDto> { new() { UserId = 20, RoleId = 52 } }
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("ROLE_SCOPE_MISMATCH", result.ErrorCode);
    }
}
