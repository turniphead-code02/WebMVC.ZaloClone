using ZaloClone.Models.Entities;

namespace ZaloClone.Models.ViewModels.Chat;

public class ChatViewModel
{
    public User CurrentUser { get; set; } = null!;
    public List<ConversationSummaryDto> Conversations { get; set; } = new();
    public ConversationDetailDto? ActiveConversation { get; set; }
}

public class ConversationSummaryDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public bool IsGroup { get; set; }
    public string? LastMessageContent { get; set; }
    public DateTimeOffset? LastMessageTime { get; set; }
    public string FormattedTime { get; set; } = string.Empty;
    public string TagText { get; set; } = string.Empty;
    public string TagColor { get; set; } = "primary";
    public bool IsActive { get; set; }
    public int UnreadCount { get; set; }
    public bool IsOnline { get; set; }
}

public class ConversationDetailDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public bool IsGroup { get; set; }
    public bool IsOnline { get; set; }
    public List<MessageDto> Messages { get; set; } = new();
    public List<MemberDto> Members { get; set; } = new();
    public List<AttachmentDto> Attachments { get; set; } = new();
}

public class MessageDto
{
    public Guid Id { get; set; }
    public Guid SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string? SenderAvatar { get; set; }
    public string Content { get; set; } = string.Empty;
    public string MessageType { get; set; } = "Text";
    public DateTimeOffset CreatedAt { get; set; }
    public string FormattedTime { get; set; } = string.Empty;
    public bool IsSelf { get; set; }
}

public class MemberDto
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Role { get; set; } = "Member";
    public string Bio { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
}

public class AttachmentDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string FormattedSize { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string IconClass { get; set; } = "bi-file-earmark";
    public string BadgeColor { get; set; } = "secondary";
}
