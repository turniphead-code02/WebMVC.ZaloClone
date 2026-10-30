namespace ZaloClone.Models.Entities;

public class Conversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? Title { get; set; }
    public string? AvatarUrl { get; set; }
    public bool IsGroup { get; set; }
    public Guid? CreatedById { get; set; }
    public Guid? LastMessageId { get; set; }
    public string? LastMessagePreview { get; set; }
    public DateTimeOffset? LastMessageAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public User? CreatedBy { get; set; }
    public Message? LastMessage { get; set; }
    public DirectConversation? DirectConversation { get; set; }
    public ICollection<ConversationMember> Members { get; set; } = new List<ConversationMember>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
