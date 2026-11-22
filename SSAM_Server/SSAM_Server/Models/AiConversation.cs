namespace SSAM_Server.Models;

public class AiConversation : IHasUpdatedAt
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public long? DocumentId { get; set; }
    public DocumentNode? Document { get; set; }
    public long UserId { get; set; }
    public User User { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public bool CanViewIssues { get; set; }
    public bool CanViewDocuments { get; set; }
    public bool CanViewTests { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<AiConversationMessage> Messages { get; set; } = new List<AiConversationMessage>();
}

public class AiConversationMessage
{
    public long Id { get; set; }
    public long ConversationId { get; set; }
    public AiConversation Conversation { get; set; } = null!;
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? CitationsJson { get; set; }
    public string? Model { get; set; }
    public bool? IsRealAi { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
