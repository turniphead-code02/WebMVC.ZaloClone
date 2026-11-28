using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZaloClone.Data;
using ZaloClone.Infrastructure;
using ZaloClone.Services.Chat;
using ZaloClone.Services.Storage;

namespace ZaloClone.Controllers;

[Authorize]
public class ChatController : Controller
{
    private readonly IChatService _chatService;
    private readonly AppDbContext _db;
    private readonly IFileStorageService _storageService;

    public ChatController(IChatService chatService, AppDbContext db, IFileStorageService storageService)
    {
        _chatService = chatService;
        _db = db;
        _storageService = storageService;
    }

    /// <summary>
    /// Phục vụ file đính kèm có kiểm tra quyền thành viên & trạng thái thu hồi (CHAT-14, CHAT-17).
    /// </summary>
    [HttpGet("Chat/File/{attachmentId:guid}")]
    public async Task<IActionResult> DownloadFile(Guid attachmentId)
    {
        var userId = User.GetUserId();
        var attachment = await _db.MessageAttachments
            .AsNoTracking()
            .Include(a => a.Message)
            .FirstOrDefaultAsync(a => a.Id == attachmentId);

        if (attachment == null || attachment.Message.IsRecalled)
        {
            return NotFound("Tệp không tồn tại hoặc đã bị thu hồi.");
        }

        var isMember = await _db.ConversationMembers
            .AnyAsync(m => m.ConversationId == attachment.Message.ConversationId && m.UserId == userId && m.LeftAt == null);
        if (!isMember)
        {
            return Forbid();
        }

        var physicalPath = _storageService.GetPhysicalPath(attachment.StorageKey);
        if (string.IsNullOrEmpty(physicalPath) || !System.IO.File.Exists(physicalPath))
        {
            return NotFound("Không tìm thấy tệp trên máy chủ.");
        }

        var contentType = string.IsNullOrWhiteSpace(attachment.FileType) ? "application/octet-stream" : attachment.FileType;
        return PhysicalFile(physicalPath, contentType, attachment.FileName, enableRangeProcessing: true);
    }

    [HttpGet]
    public IActionResult Index(Guid? conversationId)
    {
        return RedirectToAction("Index", "Home", new { conversationId });
    }

    [HttpGet]
    public async Task<IActionResult> Messages(Guid conversationId, int skip = 0, int take = 50)
    {
        var userId = User.GetUserId();
        var result = await _chatService.GetMessagesWithMetaAsync(conversationId, userId, skip, take);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Json(result);
    }

    [HttpPost]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageDto dto)
    {
        var userId = User.GetUserId();
        if (dto == null || dto.ConversationId == Guid.Empty || string.IsNullOrWhiteSpace(dto.Content))
        {
            return BadRequest(ApiResponse.Fail("Dữ liệu không hợp lệ."));
        }

        var result = await _chatService.SendTextMessageAsync(dto.ConversationId, userId, dto.Content, dto.ReplyToId);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Json(result);
    }

    [HttpPost]
    public async Task<IActionResult> UploadFile(Guid conversationId, IFormFile file, Guid? replyToId = null)
    {
        var userId = User.GetUserId();
        if (conversationId == Guid.Empty || file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse.Fail("File đính kèm không hợp lệ."));
        }

        var result = await _chatService.SendFileAttachmentAsync(conversationId, userId, file, replyToId);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Json(result);
    }

    [HttpPost]
    public async Task<IActionResult> RecallMessage([FromBody] RecallMessageDto dto)
    {
        var userId = User.GetUserId();
        if (dto == null || dto.MessageId == Guid.Empty)
        {
            return BadRequest(ApiResponse.Fail("Dữ liệu không hợp lệ."));
        }

        var result = await _chatService.RecallMessageAsync(dto.MessageId, userId);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Json(result);
    }

    [HttpPost]
    public async Task<IActionResult> TogglePinMessage([FromBody] PinMessageDto dto)
    {
        var userId = User.GetUserId();
        if (dto == null || dto.MessageId == Guid.Empty)
        {
            return BadRequest(ApiResponse.Fail("Dữ liệu không hợp lệ."));
        }

        var result = await _chatService.TogglePinMessageAsync(dto.MessageId, userId);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Json(result);
    }

    [HttpGet]
    public async Task<IActionResult> Attachments(Guid conversationId)
    {
        var userId = User.GetUserId();
        var result = await _chatService.GetAttachmentsAsync(conversationId, userId);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Json(result);
    }
}

public class SendMessageDto
{
    public Guid ConversationId { get; set; }
    public string Content { get; set; } = string.Empty;
    public Guid? ReplyToId { get; set; }
}

public class RecallMessageDto
{
    public Guid MessageId { get; set; }
}

public class PinMessageDto
{
    public Guid MessageId { get; set; }
}
