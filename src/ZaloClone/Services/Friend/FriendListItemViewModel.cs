namespace ZaloClone.Models.ViewModels.Friend;

public class FriendListItemViewModel
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
}

public class IncomingFriendRequestViewModel
{
    public Guid RequestId { get; set; }
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
}
