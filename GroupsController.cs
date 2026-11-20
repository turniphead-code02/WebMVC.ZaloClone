using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebMVCZaloClone.Data;
using WebMVCZaloClone.Models;
using WebMVCZaloClone.Services;
using WebMVCZaloClone.ViewModels;

namespace WebMVCZaloClone.Controllers;

[Authorize]
public class GroupsController : Controller
{
    // Giống Zalo: nhóm cần ít nhất 3 người (người tạo + 2 người khác).
    public const int MinOtherMembers = 2;
    private const int PageSize = 50;
    private const int MediaPanelSize = 40;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly GroupImageStorage _images;
    private readonly GroupFileStorage _files;
    private readonly GroupNotifier _notifier;

    public GroupsController(ApplicationDbContext db, UserManager<ApplicationUser> users,
        GroupImageStorage images, GroupFileStorage files, GroupNotifier notifier)
    {
        _db = db; _users = users; _images = images; _files = files; _notifier = notifier;
    }

    private string CurrentUserId => _users.GetUserId(User)!;

    // ===== Trang =====

    public async Task<IActionResult> Index()
    {
        return View(await _db.LoadConversationsAsync(CurrentUserId));
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewBag.Users = await OtherUsersAsync();
        return View(new CreateGroupViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateGroupViewModel model)
    {
        var me = await _users.GetUserAsync(User);
        if (me == null) return Challenge();

        var memberIds = await _users.Users
            .Where(u => model.MemberIds.Contains(u.Id) && u.Id != me.Id)
            .Select(u => u.Id).ToListAsync();
        if (memberIds.Count < MinOtherMembers)
            ModelState.AddModelError(nameof(model.MemberIds), $"Vui lòng chọn ít nhất {MinOtherMembers} thành viên khác.");

        string? imageUrl = null;
        if (ModelState.IsValid && model.Image is { Length: > 0 })
        {
            var (url, error) = await _images.SaveAsync(model.Image);
            if (error != null) ModelState.AddModelError(nameof(model.Image), error);
            imageUrl = url;
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Users = await OtherUsersAsync();
            return View(model);
        }

        // Nhóm + thành viên + tin hệ thống được lưu trong một SaveChanges (một transaction).
        var group = new ChatGroup { Name = model.Name.Trim(), ImageUrl = imageUrl, CreatedById = me.Id };
        group.Members.Add(new GroupMember { UserId = me.Id, IsAdmin = true });
        foreach (var id in memberIds)
            group.Members.Add(new GroupMember { UserId = id });
        group.Messages.Add(new GroupMessage
        {
            SenderId = me.Id, Sender = me, Type = MessageType.System,
            Content = $"{me.DisplayName} đã tạo nhóm \"{group.Name}\""
        });

        _db.ChatGroups.Add(group);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch
        {
            _images.Delete(imageUrl);
            throw;
        }

        // Thêm mọi thành viên (đang online) vào kênh realtime của nhóm và hiện nhóm mới ở danh sách bên trái.
        var conversation = new ConversationItem
        {
            Id = group.Id, Name = group.Name, ImageUrl = group.ImageUrl,
            Preview = group.Messages.First().Content, LastActivity = group.CreatedAt
        };
        foreach (var id in memberIds.Append(me.Id))
        {
            await _notifier.AddToChannelAsync(id, group.Id);
            if (id != me.Id) await _notifier.AddedToGroupAsync(id, conversation);
        }

        return RedirectToAction(nameof(Chat), new { id = group.Id });
    }

    public async Task<IActionResult> Chat(int id)
    {
        var group = await _db.ChatGroups.FirstOrDefaultAsync(g => g.Id == id);
        if (group == null || !await IsMember(id))
        {
            TempData["Error"] = "Nhóm không tồn tại hoặc bạn không còn là thành viên.";
            return RedirectToAction(nameof(Index));
        }

        var (messages, hasMore) = await LoadMessagesAsync(id, null);
        var vm = new ChatViewModel
        {
            Group = group,
            IsAdmin = await IsAdmin(id),
            Members = await _db.LoadMembersAsync(id),
            Messages = messages,
            HasMoreMessages = hasMore,
            Files = (await _db.GroupMessages
                    .Where(m => m.ChatGroupId == id && m.Type == MessageType.File)
                    .OrderByDescending(m => m.Id).Take(MediaPanelSize)
                    .Include(m => m.Sender).ToListAsync())
                .Select(MessageDto.From).ToList(),
            AllUsers = (await _users.Users.ToListAsync())
                .Select(u => new UserOptionDto(u.Id, u.DisplayName)).OrderBy(u => u.Name).ToList(),
            Conversations = await _db.LoadConversationsAsync(CurrentUserId)
        };
        return View(vm);
    }

    // ===== API JSON (gọi bằng fetch từ trang Chat) =====

    [HttpGet]
    public async Task<IActionResult> Messages(int id, int? beforeId)
    {
        if (!await IsMember(id)) return Fail("Bạn không còn là thành viên nhóm này.", 403);
        var (messages, hasMore) = await LoadMessagesAsync(id, beforeId);
        return Json(new { messages, hasMore });
    }

    // Gửi file: upload qua HTTP (SignalR không hợp để truyền file lớn), sau đó phát realtime như tin nhắn thường.
    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(GroupFileStorage.MaxBytes + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = GroupFileStorage.MaxBytes + 1024 * 1024)]
    public async Task<IActionResult> SendFile(int groupId, IFormFile? file)
    {
        if (!await IsMember(groupId)) return Fail("Bạn không còn là thành viên nhóm này.", 403);
        if (file == null || file.Length == 0) return Fail("Vui lòng chọn file.");
        var me = await _users.GetUserAsync(User);
        if (me == null) return Fail("Phiên đăng nhập đã hết hạn.", 401);

        var (saved, error) = await _files.SaveAsync(file);
        if (saved == null) return Fail(error!);

        var message = new GroupMessage
        {
            ChatGroupId = groupId, SenderId = me.Id, Sender = me, Type = MessageType.File, SentAt = DateTime.UtcNow,
            Content = saved.FileName, FileName = saved.FileName, FilePath = saved.StoredName,
            FileSize = saved.Size, FileContentType = saved.ContentType
        };
        _db.GroupMessages.Add(message);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch
        {
            _files.Delete(saved.StoredName);
            throw;
        }

        await _notifier.MessagesAsync(new[] { message });
        return Ok(new { ok = true });
    }

    // Tải/xem file: chỉ thành viên nhóm. Ảnh hiển thị inline, file khác luôn tải về (không cho trình duyệt render HTML...).
    [HttpGet]
    public async Task<IActionResult> Attachment(int id)
    {
        var message = await _db.GroupMessages.FirstOrDefaultAsync(m => m.Id == id && m.Type == MessageType.File);
        if (message == null || !await IsMember(message.ChatGroupId)) return NotFound();
        var path = _files.GetPath(message.FilePath);
        if (path == null) return NotFound();

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return GroupFileStorage.IsImage(message.FileName)
            ? PhysicalFile(path, message.FileContentType ?? "application/octet-stream")
            : PhysicalFile(path, "application/octet-stream", message.FileName);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMembers(int groupId, List<string> userIds)
    {
        if (!await IsAdmin(groupId)) return Fail("Chỉ quản trị viên mới được thêm thành viên.", 403);
        var group = await _db.ChatGroups.FindAsync(groupId);
        var me = await _users.GetUserAsync(User);
        if (group == null || me == null) return Fail("Không tìm thấy nhóm.", 404);

        // Bỏ qua id không tồn tại hoặc đã là thành viên.
        var existingIds = await _db.GroupMembers.Where(m => m.ChatGroupId == groupId).Select(m => m.UserId).ToListAsync();
        var users = (await _users.Users.Where(u => userIds.Contains(u.Id) && !existingIds.Contains(u.Id)).ToListAsync())
            .OrderBy(u => u.DisplayName).ToList();
        if (users.Count == 0) return Fail("Vui lòng chọn ít nhất một người chưa có trong nhóm.");

        foreach (var user in users)
            _db.GroupMembers.Add(new GroupMember { ChatGroupId = groupId, UserId = user.Id });
        var sys = GroupNotifier.SystemMessage(groupId, me,
            $"{me.DisplayName} đã thêm {string.Join(", ", users.Select(u => u.DisplayName))} vào nhóm");
        _db.GroupMessages.Add(sys);
        await _db.SaveChangesAsync();

        var conversation = new ConversationItem
        {
            Id = group.Id, Name = group.Name, ImageUrl = group.ImageUrl, Preview = sys.Content, LastActivity = sys.SentAt
        };
        foreach (var user in users)
        {
            await _notifier.AddToChannelAsync(user.Id, groupId);
            await _notifier.AddedToGroupAsync(user.Id, conversation);
        }
        await _notifier.MessagesAsync(new[] { sys });
        await _notifier.MembersChangedAsync(groupId);
        return Ok(new { added = users.Count });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveMember(int groupId, string userId)
    {
        if (!await IsAdmin(groupId)) return Fail("Chỉ quản trị viên mới được xóa thành viên.", 403);
        if (userId == CurrentUserId) return Fail("Hãy dùng nút \"Rời nhóm\" để tự rời khỏi nhóm.");

        var member = await _db.GroupMembers.Include(m => m.User)
            .FirstOrDefaultAsync(m => m.ChatGroupId == groupId && m.UserId == userId);
        var me = await _users.GetUserAsync(User);
        if (member == null || me == null) return Fail("Người này không còn trong nhóm.", 404);

        _db.GroupMembers.Remove(member);
        var sys = GroupNotifier.SystemMessage(groupId, me, $"{me.DisplayName} đã xóa {member.User!.DisplayName} khỏi nhóm");
        _db.GroupMessages.Add(sys);
        await _db.SaveChangesAsync();

        // Gỡ khỏi kênh realtime TRƯỚC khi phát tin, để người bị xóa không nhận thêm tin nào nữa.
        await _notifier.RemoveFromChannelAsync(userId, groupId);
        await _notifier.RemovedFromGroupAsync(userId, groupId, "Bạn đã bị xóa khỏi nhóm.");
        await _notifier.MessagesAsync(new[] { sys });
        await _notifier.MembersChangedAsync(groupId);
        return Ok(new { ok = true });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetAdmin(int groupId, string userId, bool isAdmin)
    {
        if (!await IsAdmin(groupId)) return Fail("Chỉ quản trị viên mới được phân quyền.", 403);
        if (userId == CurrentUserId) return Fail("Không thể tự thay đổi quyền của mình.");

        var member = await _db.GroupMembers.Include(m => m.User)
            .FirstOrDefaultAsync(m => m.ChatGroupId == groupId && m.UserId == userId);
        var me = await _users.GetUserAsync(User);
        if (member == null || me == null) return Fail("Người này không còn trong nhóm.", 404);
        if (member.IsAdmin == isAdmin) return Ok(new { ok = true });

        member.IsAdmin = isAdmin;
        var sys = GroupNotifier.SystemMessage(groupId, me, isAdmin
            ? $"{me.DisplayName} đã đặt {member.User!.DisplayName} làm quản trị viên"
            : $"{me.DisplayName} đã gỡ quyền quản trị viên của {member.User!.DisplayName}");
        _db.GroupMessages.Add(sys);
        await _db.SaveChangesAsync();

        await _notifier.MessagesAsync(new[] { sys });
        await _notifier.MembersChangedAsync(groupId);
        return Ok(new { ok = true });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Leave(int groupId)
    {
        var me = await _users.GetUserAsync(User);
        var group = await _db.ChatGroups.Include(g => g.Members).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(g => g.Id == groupId);
        var mine = group?.Members.FirstOrDefault(m => m.UserId == me?.Id);
        if (me == null || group == null || mine == null) return Fail("Bạn không còn là thành viên nhóm này.", 404);

        var redirect = Url.Action(nameof(Index));
        var others = group.Members.Where(m => m.UserId != me.Id).ToList();

        // Người cuối cùng rời nhóm -> xóa nhóm (cascade xóa thành viên và tin nhắn).
        if (others.Count == 0)
        {
            var imageUrl = group.ImageUrl;
            var storedFiles = await _db.GroupMessages
                .Where(m => m.ChatGroupId == groupId && m.FilePath != null).Select(m => m.FilePath!).ToListAsync();
            _db.ChatGroups.Remove(group);
            await _db.SaveChangesAsync();
            _images.Delete(imageUrl);
            storedFiles.ForEach(_files.Delete);
            await _notifier.RemoveFromChannelAsync(me.Id, groupId);
            await _notifier.RemovedFromGroupAsync(me.Id, groupId, "Bạn đã rời nhóm.");
            return Ok(new { redirect });
        }

        var systemMessages = new List<GroupMessage>
        {
            GroupNotifier.SystemMessage(groupId, me, $"{me.DisplayName} đã rời nhóm")
        };

        // Admin cuối cùng rời nhóm -> chuyển quyền cho thành viên vào nhóm sớm nhất.
        if (mine.IsAdmin && !others.Any(m => m.IsAdmin))
        {
            var heir = others.OrderBy(m => m.JoinedAt).ThenBy(m => m.Id).First();
            heir.IsAdmin = true;
            systemMessages.Add(GroupNotifier.SystemMessage(groupId, me,
                $"{heir.User!.DisplayName} đã trở thành quản trị viên nhóm"));
        }

        _db.GroupMembers.Remove(mine);
        _db.GroupMessages.AddRange(systemMessages);
        await _db.SaveChangesAsync();

        await _notifier.RemoveFromChannelAsync(me.Id, groupId);
        await _notifier.RemovedFromGroupAsync(me.Id, groupId, "Bạn đã rời nhóm.");
        await _notifier.MessagesAsync(systemMessages);
        await _notifier.MembersChangedAsync(groupId);
        return Ok(new { redirect });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateGroup(int groupId, string? name, IFormFile? image)
    {
        if (!await IsAdmin(groupId)) return Fail("Chỉ quản trị viên mới được đổi tên/ảnh nhóm.", 403);
        var group = await _db.ChatGroups.FindAsync(groupId);
        var me = await _users.GetUserAsync(User);
        if (group == null || me == null) return Fail("Không tìm thấy nhóm.", 404);

        name = name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length < 2 || name.Length > 80)
            return Fail("Tên nhóm phải từ 2 đến 80 ký tự.");

        var systemMessages = new List<GroupMessage>();
        if (name != group.Name)
        {
            group.Name = name;
            systemMessages.Add(GroupNotifier.SystemMessage(groupId, me, $"{me.DisplayName} đã đổi tên nhóm thành \"{name}\""));
        }

        string? oldImage = null;
        if (image is { Length: > 0 })
        {
            var (url, error) = await _images.SaveAsync(image);
            if (error != null) return Fail(error);
            oldImage = group.ImageUrl;
            group.ImageUrl = url;
            systemMessages.Add(GroupNotifier.SystemMessage(groupId, me, $"{me.DisplayName} đã đổi ảnh nhóm"));
        }

        if (systemMessages.Count == 0) return Ok(new { ok = true });

        _db.GroupMessages.AddRange(systemMessages);
        await _db.SaveChangesAsync();
        _images.Delete(oldImage);

        await _notifier.GroupUpdatedAsync(group);
        await _notifier.MessagesAsync(systemMessages);
        return Ok(new { ok = true });
    }

    // ===== Helpers =====

    private IActionResult Fail(string error, int status = 400) => StatusCode(status, new { error });

    private async Task<List<ApplicationUser>> OtherUsersAsync() =>
        (await _users.Users.Where(u => u.Id != CurrentUserId).ToListAsync())
            .OrderBy(u => u.DisplayName).ToList();

    /// <summary>Lấy tối đa PageSize tin cũ hơn beforeId (hoặc mới nhất nếu null), trả về theo thứ tự tăng dần.</summary>
    private async Task<(List<MessageDto> Messages, bool HasMore)> LoadMessagesAsync(int groupId, int? beforeId)
    {
        var query = _db.GroupMessages.Where(m => m.ChatGroupId == groupId);
        if (beforeId.HasValue) query = query.Where(m => m.Id < beforeId.Value);

        var page = await query.OrderByDescending(m => m.Id)
            .Take(PageSize + 1)
            .Include(m => m.Sender)
            .ToListAsync();

        var hasMore = page.Count > PageSize;
        var messages = page.Take(PageSize).Reverse().Select(MessageDto.From).ToList();
        return (messages, hasMore);
    }

    private Task<bool> IsMember(int groupId) =>
        _db.GroupMembers.AnyAsync(m => m.ChatGroupId == groupId && m.UserId == CurrentUserId);

    private Task<bool> IsAdmin(int groupId) =>
        _db.GroupMembers.AnyAsync(m => m.ChatGroupId == groupId && m.UserId == CurrentUserId && m.IsAdmin);
}
