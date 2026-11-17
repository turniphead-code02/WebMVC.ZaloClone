using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using ZaloClone.Infrastructure;
using ZaloClone.Models.ViewModels.Chat;

namespace ZaloClone.Services.Chat;

public interface IChatService
{
    Task<ApiResponse<List<MessageDto>>> GetMessagesAsync(Guid conversationId, Guid userId, int skip = 0, int take = 50);
    Task<ApiResponse<MessageDto>> SendTextMessageAsync(Guid conversationId, Guid senderId, string content);
    Task<ApiResponse<MessageDto>> SendFileAttachmentAsync(Guid conversationId, Guid senderId, IFormFile file);
    Task<ApiResponse> RecallMessageAsync(Guid messageId, Guid userId);
    Task<ApiResponse<List<AttachmentDto>>> GetAttachmentsAsync(Guid conversationId, Guid userId);
}
