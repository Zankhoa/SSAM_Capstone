namespace SSAM_Server.Models;

public static class SprintStatuses
{
    public const string Planned = "PLANNED";
    public const string Active = "ACTIVE";
    public const string Completed = "COMPLETED";

    public static bool IsValid(string? status) =>
        status is Planned or Active or Completed;
}

public static class IssueTypes
{
    public const string Task = "TASK";
    public const string Bug = "BUG";
    public const string Story = "STORY";
    public const string Design = "DESIGN";

    public static bool IsValid(string? type) =>
        type is Task or Bug or Story or Design;
}

public static class IssuePriorities
{
    public const string Highest = "HIGHEST";
    public const string High = "HIGH";
    public const string Medium = "MEDIUM";
    public const string Low = "LOW";

    public static bool IsValid(string? priority) =>
        priority is Highest or High or Medium or Low;
}

public static class IssueActivityActions
{
    public const string Created = "CREATED";
    public const string Updated = "UPDATED";
    public const string Transitioned = "TRANSITIONED";
    public const string Assigned = "ASSIGNED";
    public const string SprintChanged = "SPRINT_CHANGED";
    public const string ReqLinked = "REQ_LINKED";
    public const string ReqAcked = "REQ_ACKED";
    public const string ReqChanged = "REQ_CHANGED";
    public const string Commented = "COMMENTED";
    public const string Deleted = "DELETED";
}

public class Sprint : IHasUpdatedAt
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string? Goal { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string Status { get; set; } = SprintStatuses.Planned;
    public int? CapacityPoints { get; set; }
    public DateTime? CompletedAt { get; set; }

    public long CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Issue> Issues { get; set; } = new List<Issue>();
}

public class Issue : IHasUpdatedAt
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public string Key { get; set; } = string.Empty;
    public string Type { get; set; } = IssueTypes.Task;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public long StatusId { get; set; }
    public WorkflowStatus Status { get; set; } = null!;

    public string Priority { get; set; } = IssuePriorities.Medium;
    public int? Points { get; set; }

    public long? AssigneeId { get; set; }
    public User? Assignee { get; set; }

    public long? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public long ReporterId { get; set; }
    public User Reporter { get; set; } = null!;

    public long? SprintId { get; set; }
    public Sprint? Sprint { get; set; }

    public long? ParentId { get; set; }
    public Issue? Parent { get; set; }
    public ICollection<Issue> Subtasks { get; set; } = new List<Issue>();

    public string Rank { get; set; } = "000100";
    public string Labels { get; set; } = "[]";
    public DateOnly? DueDate { get; set; }
    public DateOnly? GoLiveDate { get; set; }
    public bool ReqChanged { get; set; } = false;
    public DateTime? ResolvedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;
    public byte[] RowVer { get; set; } = Array.Empty<byte>();

    public ICollection<IssueRequirementLink> RequirementLinks { get; set; } = new List<IssueRequirementLink>();
    public ICollection<IssueComment> Comments { get; set; } = new List<IssueComment>();
    public ICollection<IssueActivity> Activities { get; set; } = new List<IssueActivity>();
}

public class IssueRequirementLink
{
    public long IssueId { get; set; }
    public Issue Issue { get; set; } = null!;

    public long DocumentId { get; set; }
    public DocumentNode Document { get; set; } = null!;

    public string ReqUid { get; set; } = string.Empty;
    public string ReqHash { get; set; } = string.Empty;
    public string? ChangedVersionLabel { get; set; }
    public DateTime LinkedAt { get; set; } = DateTime.UtcNow;

    public long LinkedById { get; set; }
    public User LinkedBy { get; set; } = null!;
}

public class BoardWipLimit
{
    public long ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public long StatusId { get; set; }
    public WorkflowStatus Status { get; set; } = null!;

    public int WipLimit { get; set; }
}

public class IssueComment
{
    public long Id { get; set; }
    public long IssueId { get; set; }
    public Issue Issue { get; set; } = null!;

    public long? ParentId { get; set; }
    public IssueComment? Parent { get; set; }
    public ICollection<IssueComment> Replies { get; set; } = new List<IssueComment>();

    public string Body { get; set; } = string.Empty;
    public long AuthorId { get; set; }
    public User Author { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<IssueCommentMention> Mentions { get; set; } = new List<IssueCommentMention>();
}

public class IssueCommentMention
{
    public long CommentId { get; set; }
    public IssueComment Comment { get; set; } = null!;

    public long UserId { get; set; }
    public User User { get; set; } = null!;
}

public class IssueActivity
{
    public long Id { get; set; }
    public long IssueId { get; set; }
    public Issue Issue { get; set; } = null!;

    public string Action { get; set; } = string.Empty;
    public long? ActorId { get; set; }
    public User? Actor { get; set; }

    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class IssueAttachment
{
    public long Id { get; set; }
    public long IssueId { get; set; }
    public Issue Issue { get; set; } = null!;

    public string FileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long FileSize { get; set; }

    public long UploadedById { get; set; }
    public User UploadedBy { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
