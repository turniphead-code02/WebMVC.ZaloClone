namespace ZaloClone.Models.Entities;

public class Message
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public Guid? SenderId { get; set; }
    public string? Content { get; set; }
    public string MessageType { get; set; } = "Text"; // 'Text', 'Image', 'File', 'System'
    public Guid? ReplyToId { get; set; }
    public bool IsPinned { get; set; }
    public DateTimeOffset? PinnedAt { get; set; }
    public Guid? PinnedById { get; set; }
    public bool IsRecalled { get; set; }
    public DateTimeOffset? RecalledAt { get; set; }
    public DateTimeOffset? EditedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Conversation Conversation { get; set; } = null!;
    public User? Sender { get; set; }
    public Message? ReplyTo { get; set; }
    public ICollection<Message> Replies { get; set; } = new List<Message>();
    public User? PinnedBy { get; set; }
    public ICollection<MessageAttachment> Attachments { get; set; } = new List<MessageAttachment>();
}
