using Microsoft.AspNetCore.Mvc;

namespace ZaloClone.Controllers;

public class ChatController : Controller
{
    [HttpGet]
    public IActionResult Index(int? conversationId)
    {
        ViewBag.ConversationId = conversationId;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> GetMessagesAsync(int conversationId)
    {
        return Ok(new { conversationId = conversationId, messages = new List<object>() });
    }

    [HttpPost]
    public async Task<IActionResult> UploadFileAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "File không hợp lệ hoặc rỗng." });
        }

        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var fileUrl = $"/uploads/{uniqueFileName}";
        return Ok(new
        {
            url = fileUrl,
            fileName = file.FileName,
            fileType = file.ContentType
        });
    }
}
