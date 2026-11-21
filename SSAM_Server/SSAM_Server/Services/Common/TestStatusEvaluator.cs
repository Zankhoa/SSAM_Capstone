using SSAM_Server.Models;

namespace SSAM_Server.Services.Common;

/// <summary>
/// Evaluates effective execution statuses for test cases across environments.
/// </summary>
public static class TestStatusEvaluator
{
    /// <summary>
    /// Computes the effective status of a test case in a specific environment, accounting for active retest flags.
    /// </summary>
    public static string ComputeEffectiveEnvStatus(TestCase tc, long envId)
    {
        var hasFlag = tc.RetestFlags.Any(f => f.EnvId == envId);
        var latestRes = tc.Results
            .Where(res => res.EnvId == envId)
            .OrderByDescending(res => res.At)
            .ThenByDescending(res => res.Id)
            .FirstOrDefault()?.Result ?? TestResultValues.Untest;

        return hasFlag ? "RETEST" : latestRes;
    }

    /// <summary>
    /// Summarizes the worst execution status across all tested active environments for a test case.
    /// </summary>
    public static (string? LastResult, long? EnvId, bool IsRetest) ComputeWorstTestCaseResult(TestCase tc)
    {
        var activeEnvs = tc.Plan?.Environments
            .Where(e => !e.IsDeleted)
            .OrderBy(e => e.Position)
            .ThenBy(e => e.Id)
            .ToList() ?? new List<TestEnvironment>();

        var testedEnvs = new List<(long EnvId, string Status)>();
        foreach (var env in activeEnvs)
        {
            var hasFlag = tc.RetestFlags.Any(f => f.EnvId == env.Id);
            var envResults = tc.Results
                .Where(r => r.EnvId == env.Id)
                .OrderByDescending(r => r.At)
                .ThenByDescending(r => r.Id)
                .ToList();

            var hasNonUntest = envResults.Any(r => r.Result != TestResultValues.Untest);
            if (!hasFlag && !hasNonUntest)
                continue;

            var status = hasFlag
                ? "RETEST"
                : (envResults.FirstOrDefault()?.Result ?? TestResultValues.Untest);
            testedEnvs.Add((env.Id, status));
        }

        if (testedEnvs.Count == 0)
        {
            return (null, activeEnvs.FirstOrDefault()?.Id, false);
        }

        bool anyRetest = testedEnvs.Any(x => x.Status == "RETEST");

        // Priority: FAIL > RETEST / UNTEST > NA > PASS
        var failHit = testedEnvs.FirstOrDefault(x => x.Status == TestResultValues.Fail);
        if (failHit.EnvId != 0)
            return ("FAIL", failHit.EnvId, anyRetest);

        var retestHit = testedEnvs.FirstOrDefault(x => x.Status is "RETEST" or TestResultValues.Untest);
        if (retestHit.EnvId != 0)
            return ("NOT_RUN", retestHit.EnvId, anyRetest);

        var naHit = testedEnvs.FirstOrDefault(x => x.Status == TestResultValues.Na);
        if (naHit.EnvId != 0)
            return ("BLOCKED", naHit.EnvId, anyRetest);

        return ("PASS", testedEnvs[0].EnvId, anyRetest);
    }
}
