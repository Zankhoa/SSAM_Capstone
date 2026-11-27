namespace SSAM_Server.Models;

public class DocumentCommentMention
{
    public long CommentId { get; set; }
    public DocumentComment Comment { get; set; } = null!;

    public long UserId { get; set; }
    public User User { get; set; } = null!;
}
