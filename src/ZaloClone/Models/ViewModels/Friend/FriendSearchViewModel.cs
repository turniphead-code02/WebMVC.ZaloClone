namespace ZaloClone.Models.ViewModels.Friend;

//Các trạng thái quan hệ giữa người đang đăng nhập và người được tìm thấy.
public static class FriendRelation
{
    public const string None = "None";                       // Chưa có quan hệ -> hiện nút "Kết bạn"
    public const string Friend = "Friend";                   // Đã là bạn bè
    public const string RequestSent = "RequestSent";         // Mình đã gửi lời mời -> hiện nút "Hủy lời mời"
    public const string RequestReceived = "RequestReceived"; // Họ đã gửi lời mời cho mình
    public const string Blocked = "Blocked";                 // Mình đã chặn họ
}

public class UserSearchResultViewModel
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public string RelationStatus { get; set; } = FriendRelation.None;
    public Guid? RequestId { get; set; }
}

public class SentFriendRequestViewModel
{
    public Guid RequestId { get; set; }
    public Guid ReceiverId { get; set; }
    public string ReceiverName { get; set; } = string.Empty;
    public string? ReceiverAvatarUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class FriendSearchPageViewModel
{
    public List<SentFriendRequestViewModel> SentRequests { get; set; } = new();
}
