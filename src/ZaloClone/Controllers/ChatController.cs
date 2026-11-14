using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ZaloClone.Infrastructure;
using ZaloClone.Services.Chat;

namespace ZaloClone.Controllers;

[Authorize]
public class ChatController : Controller
{
    private readonly IChatService _chatService;

    public ChatController(IChatService chatService)
    {
        _chatService = chatService;
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
        var result = await _chatService.GetMessagesAsync(conversationId, userId, skip, take);
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

        var result = await _chatService.SendTextMessageAsync(dto.ConversationId, userId, dto.Content);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Json(result);
    }

    [HttpPost]
    public async Task<IActionResult> UploadFile(Guid conversationId, IFormFile file)
    {
        var userId = User.GetUserId();
        if (conversationId == Guid.Empty || file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse.Fail("File đính kèm không hợp lệ."));
        }

        var result = await _chatService.SendFileAttachmentAsync(conversationId, userId, file);
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
}

public class RecallMessageDto
{
    public Guid MessageId { get; set; }
}
