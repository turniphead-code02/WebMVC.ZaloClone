using ZaloClone.Infrastructure;
using ZaloClone.Models.ViewModels.Friend;

namespace ZaloClone.Services.Friend;

public interface IFriendRequestService
{
    Task<ApiResponse<UserSearchResultViewModel>> SearchUserAsync(Guid currentUserId, string? keyword);
    Task<ApiResponse<SentFriendRequestViewModel>> SendRequestAsync(Guid currentUserId, Guid receiverId);
    Task<ApiResponse> CancelRequestAsync(Guid currentUserId, Guid requestId);
    Task<List<SentFriendRequestViewModel>> GetSentRequestsAsync(Guid currentUserId);
    Task<List<FriendListItemViewModel>> GetFriendsAsync(Guid currentUserId);

    Task<List<IncomingFriendRequestViewModel>> GetIncomingRequestsAsync(Guid currentUserId);

    Task<ApiResponse> AcceptRequestAsync(Guid currentUserId, Guid requestId);

    Task<ApiResponse> RejectRequestAsync(Guid currentUserId, Guid requestId);

    Task<ApiResponse> BlockAsync(Guid currentUserId, Guid friendId);
}
