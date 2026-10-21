using SSAM_Server.DTOs.Projects;
using SSAM_Server.Models;

namespace SSAM_Server.Services;

public static class WorkflowValidator
{
    public static List<WorkflowIssueDto> ValidatePhaseWorkflow(
        IReadOnlyList<StatusInputDto> statuses,
        IReadOnlyList<TransitionInputDto> transitions,
        int phaseIndex = 0)
    {
        var issues = new List<WorkflowIssueDto>();

        void Push(string code, string message, string? statusKey = null, int? transitionIndex = null)
        {
            issues.Add(new WorkflowIssueDto
            {
                PhaseIndex = phaseIndex,
                Code = code,
                StatusKey = statusKey,
                TransitionIndex = transitionIndex,
                Message = message
            });
        }

        var active = statuses.Where(s => s.IsActive != false).ToList();

        string Label(string key)
        {
            var found = active.FirstOrDefault(s => s.EffectiveKey == key);
            var labelText = !string.IsNullOrWhiteSpace(found?.Name) ? found!.Name : key;
            return $"\"{labelText}\"";
        }

        if (active.Count == 0)
        {
            Push("NO_STATUS", "Phase chưa có trạng thái nào.");
            return issues;
        }

        var seenNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in active)
        {
            var trimmed = (s.Name ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                Push("STATUS_NAME_REQUIRED", "Có trạng thái chưa đặt tên.", statusKey: s.EffectiveKey);
                continue;
            }

            if (seenNames.ContainsKey(trimmed))
            {
                Push("DUPLICATE_STATUS_NAME", $"Tên trạng thái \"{trimmed}\" bị trùng.", statusKey: s.EffectiveKey);
            }
            seenNames[trimmed] = s.EffectiveKey;
        }

        var initials = active.Where(s => s.IsInitial).ToList();
        if (initials.Count == 0)
            Push("NO_INITIAL_STATUS", "Chưa chọn trạng thái khởi tạo (Initial).");
        if (initials.Count > 1)
            Push("MULTIPLE_INITIAL_STATUS", "Chỉ được có 1 trạng thái khởi tạo.");

        var doneKeys = active
            .Where(s => s.Category == StatusCategories.Done)
            .Select(s => s.EffectiveKey)
            .ToHashSet(StringComparer.Ordinal);

        if (doneKeys.Count == 0)
        {
            Push("NO_DONE_STATUS", "Cần ít nhất 1 trạng thái thuộc nhóm \"Hoàn thành\" (Done).");
        }

        var activeKeys = active.Select(s => s.EffectiveKey).ToHashSet(StringComparer.Ordinal);
        var pairs = new HashSet<string>(StringComparer.Ordinal);
        var edges = new List<(string From, string To)>();

        for (int i = 0; i < transitions.Count; i++)
        {
            var t = transitions[i];
            var fromKey = t.FromKey;
            var toKey = t.ToKey;
            var pair = $"{fromKey}->{toKey}";

            if (!activeKeys.Contains(fromKey) || !activeKeys.Contains(toKey))
            {
                Push("INVALID_TRANSITION", $"Luật chuyển #{i + 1} trỏ tới trạng thái không tồn tại.", transitionIndex: i);
                continue;
            }

            if (fromKey == toKey)
            {
                Push("INVALID_TRANSITION", $"Luật chuyển #{i + 1} có trạng thái đầu và cuối giống nhau.", transitionIndex: i);
                continue;
            }

            if (!pairs.Add(pair))
            {
                Push("INVALID_TRANSITION", $"Luật chuyển {Label(fromKey)} → {Label(toKey)} bị khai báo 2 lần.", transitionIndex: i);
                continue;
            }

            if (t.AllowedRoleIds == null || t.AllowedRoleIds.Count == 0)
            {
                Push("TRANSITION_NO_ROLE", $"Luật chuyển {Label(fromKey)} → {Label(toKey)} chưa chọn role được phép.", transitionIndex: i);
            }

            edges.Add((fromKey, toKey));
        }

        if (initials.Count == 1)
        {
            var reached = Walk(initials[0].EffectiveKey, edges, forward: true);
            foreach (var s in active)
            {
                if (!reached.Contains(s.EffectiveKey))
                {
                    Push(
                        "UNREACHABLE_STATUS",
                        $"Trạng thái {Label(s.EffectiveKey)} không thể đi tới từ trạng thái khởi tạo.",
                        statusKey: s.EffectiveKey);
                }
            }
        }

        if (doneKeys.Count > 0)
        {
            var canFinish = new HashSet<string>(StringComparer.Ordinal);
            foreach (var doneKey in doneKeys)
            {
                foreach (var k in Walk(doneKey, edges, forward: false))
                {
                    canFinish.Add(k);
                }
            }

            foreach (var s in active)
            {
                if (s.Category != StatusCategories.Done && !canFinish.Contains(s.EffectiveKey))
                {
                    Push(
                        "DEAD_END_STATUS",
                        $"Trạng thái {Label(s.EffectiveKey)} là ngõ cụt — không có đường đi tới trạng thái hoàn thành.",
                        statusKey: s.EffectiveKey);
                }
            }
        }

        return issues;
    }

    private static HashSet<string> Walk(string start, List<(string From, string To)> edges, bool forward)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal) { start };
        var queue = new Queue<string>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var (from, to) in edges)
            {
                var (a, b) = forward ? (from, to) : (to, from);
                if (a == current && visited.Add(b))
                {
                    queue.Enqueue(b);
                }
            }
        }

        return visited;
    }
}
