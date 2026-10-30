namespace ZaloClone.Models.Entities;

public class DirectConversation
{
    public Guid ConversationId { get; set; }
    public bool IsGroup { get; set; } = false;
    public Guid UserId1 { get; set; }
    public Guid UserId2 { get; set; }

    public Conversation Conversation { get; set; } = null!;
    public User User1 { get; set; } = null!;
    public User User2 { get; set; } = null!;
}
