namespace ZaloClone.Models.ViewModels.Friend;

// Kết quả của nút "Nhắn tin": id hội thoại 1-1 và đường dẫn để chuyển vào khung chat
public class DirectChatResultViewModel
{
    public Guid ConversationId { get; set; }
    public string Url { get; set; } = string.Empty;
}
