using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ZaloClone.Infrastructure;
using ZaloClone.Models.Entities;

namespace ZaloClone.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SeedData");

        // 1. Khởi tạo tài khoản mẫu nếu chưa có
        if (!await context.Users.AnyAsync())
        {
            logger.LogInformation("Khởi tạo danh sách người dùng mẫu (Users)...");
            var users = new List<User>();

            // Admin
            var admin = new User
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Email = "admin@zalo.local",
                PhoneNumber = "0900000001",
                DisplayName = "Quản Trị Viên",
                Role = "Admin",
                Bio = "Quản trị viên hệ thống Zalo Clone"
            };
            admin.PasswordHash = hasher.HashPassword(admin, "Test@123");
            users.Add(admin);

            // User 1 đến 5
            var userNames = new[] { "Nguyễn Văn Huy", "Trần Đình Tùng", "Lê Tấn Tài", "Phạm Văn Hào", "Ngô Đăng Khôi" };
            for (int i = 0; i < userNames.Length; i++)
            {
                var userNum = i + 1;
                var user = new User
                {
                    Id = Guid.Parse($"00000000-0000-0000-0000-00000000000{userNum + 1}"),
                    Email = $"user{userNum}@zalo.local",
                    PhoneNumber = $"090000000{userNum + 1}",
                    DisplayName = userNames[i],
                    Role = "User",
                    Bio = $"Thành viên nhóm dự án ({userNames[i]})"
                };
                user.PasswordHash = hasher.HashPassword(user, "Test@123");
                users.Add(user);
            }

            await context.Users.AddRangeAsync(users);
            await context.SaveChangesAsync();
            logger.LogInformation("Khởi tạo thành công {Count} tài khoản mẫu.", users.Count);
        }

        // 2. Khởi tạo hội thoại và tin nhắn mẫu nếu chưa có
        if (!await context.Conversations.AnyAsync())
        {
            logger.LogInformation("Khởi tạo hội thoại và tin nhắn mẫu theo thiết kế 4 cột...");

            var admin = await context.Users.FirstAsync(u => u.Email == "admin@zalo.local");
            var user1 = await context.Users.FirstAsync(u => u.Email == "user1@zalo.local");
            var user2 = await context.Users.FirstAsync(u => u.Email == "user2@zalo.local");
            var user3 = await context.Users.FirstAsync(u => u.Email == "user3@zalo.local");
            var user4 = await context.Users.FirstAsync(u => u.Email == "user4@zalo.local");
            var user5 = await context.Users.FirstAsync(u => u.Email == "user5@zalo.local");

            var allUsers = new[] { admin, user1, user2, user3, user4, user5 };
            var now = DateTimeOffset.UtcNow;

            // A. NHÓM CHAT CHÍNH: "Nhóm Đồ Án Zalo Clone"
            var groupConv = new Conversation
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000001"),
                IsGroup = true,
                Title = "Nhóm Đồ Án Zalo Clone",
                CreatedById = user1.Id,
                CreatedAt = now.AddDays(-2),
                UpdatedAt = now
            };
            context.Conversations.Add(groupConv);

            // Thêm thành viên vào nhóm
            foreach (var u in allUsers)
            {
                context.ConversationMembers.Add(new ConversationMember
                {
                    Id = Guid.NewGuid(),
                    ConversationId = groupConv.Id,
                    UserId = u.Id,
                    Role = u.Id == user1.Id ? "Admin" : "Member",
                    JoinedAt = now.AddDays(-2)
                });
            }

            // Tin nhắn trong nhóm
            var msg1 = new Message
            {
                Id = Guid.NewGuid(),
                ConversationId = groupConv.Id,
                SenderId = user2.Id, // Tùng
                Content = "Chào cả nhóm! Giao diện đăng nhập và layout đã hoàn thành rồi nhé.",
                MessageType = "Text",
                CreatedAt = now.AddHours(-3)
            };
            var msg2 = new Message
            {
                Id = Guid.NewGuid(),
                ConversationId = groupConv.Id,
                SenderId = user3.Id, // Tài
                Content = "Quá đỉnh luôn! Backend tìm kiếm bạn bè và gửi lời mời kết bạn cũng đã chạy ngon.",
                MessageType = "Text",
                CreatedAt = now.AddHours(-2).AddMinutes(15)
            };
            var msg3 = new Message
            {
                Id = Guid.NewGuid(),
                ConversationId = groupConv.Id,
                SenderId = user5.Id, // Khôi
                Content = "SignalR WebSocket đã kết nối 2 chiều real-time, test bắn tin nhắn tức thời siêu mượt!",
                MessageType = "Text",
                CreatedAt = now.AddHours(-1).AddMinutes(30)
            };
            var msg4 = new Message
            {
                Id = Guid.NewGuid(),
                ConversationId = groupConv.Id,
                SenderId = user4.Id, // Hào
                Content = "Mình đã upload tài liệu kiến trúc hệ thống và hướng dẫn cài đặt lên mục Directory bên phải rồi nha mọi người 📑",
                MessageType = "Text",
                CreatedAt = now.AddMinutes(-20)
            };
            var msg5 = new Message
            {
                Id = Guid.NewGuid(),
                ConversationId = groupConv.Id,
                SenderId = user1.Id, // Huy
                Content = "Tuyệt vời anh em! Chiều nay họp tổng duyệt trước khi nộp đồ án cho thầy nhé!",
                MessageType = "Text",
                CreatedAt = now.AddMinutes(-5)
            };

            context.Messages.AddRange(msg1, msg2, msg3, msg4, msg5);

            // File đính kèm mẫu (Khớp với cột Directory trong ảnh thiết kế)
            context.MessageAttachments.AddRange(
                new MessageAttachment
                {
                    Id = Guid.NewGuid(),
                    MessageId = msg4.Id,
                    FileName = "DacTa_KienTruc_ZaloClone.pdf",
                    FileType = "application/pdf",
                    FileSize = 9 * 1024 * 1024, // 9 MB
                    StorageKey = "samples/DacTa_KienTruc_ZaloClone.pdf",
                    CreatedAt = now.AddMinutes(-20)
                },
                new MessageAttachment
                {
                    Id = Guid.NewGuid(),
                    MessageId = msg4.Id,
                    FileName = "ThietKe_UI_ZaloWeb.png",
                    FileType = "image/png",
                    FileSize = 4 * 1024 * 1024, // 4 MB
                    StorageKey = "samples/ThietKe_UI_ZaloWeb.png",
                    CreatedAt = now.AddMinutes(-20)
                },
                new MessageAttachment
                {
                    Id = Guid.NewGuid(),
                    MessageId = msg4.Id,
                    FileName = "HuongDan_CaiDat_MoiTruong.docx",
                    FileType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    FileSize = 555 * 1024, // 555 KB
                    StorageKey = "samples/HuongDan_CaiDat_MoiTruong.docx",
                    CreatedAt = now.AddMinutes(-20)
                },
                new MessageAttachment
                {
                    Id = Guid.NewGuid(),
                    MessageId = msg4.Id,
                    FileName = "SourceCode_Config_Docker.zip",
                    FileType = "application/zip",
                    FileSize = 24 * 1024 * 1024, // 24 MB
                    StorageKey = "samples/SourceCode_Config_Docker.zip",
                    CreatedAt = now.AddMinutes(-20)
                }
            );

            // B. CÁC HỘI THOẠI 1-1 (DIRECT CHAT) CHO USER1 VỚI CÁC THÀNH VIÊN
            var directPartners = new[]
            {
                (Partner: user2, Msg: "Tùng ơi tí gửi mình link Figma nhé", Time: now.AddHours(-1)),
                (Partner: user3, Msg: "Tài test lại API kết bạn giúp mình nha", Time: now.AddHours(-4)),
                (Partner: user4, Msg: "Đã review xong code Admin dashboard", Time: now.AddDays(-1)),
                (Partner: admin, Msg: "Hệ thống Zalo Clone đã sẵn sàng kiểm thử", Time: now.AddDays(-2))
            };

            foreach (var item in directPartners)
            {
                var (u1, u2) = UserPair.Order(user1.Id, item.Partner.Id);
                var directConv = new Conversation
                {
                    Id = Guid.NewGuid(),
                    IsGroup = false,
                    Title = item.Partner.DisplayName,
                    CreatedById = user1.Id,
                    CreatedAt = item.Time.AddHours(-1),
                    UpdatedAt = item.Time
                };
                context.Conversations.Add(directConv);

                context.DirectConversations.Add(new DirectConversation
                {
                    ConversationId = directConv.Id,
                    IsGroup = false,
                    UserId1 = u1,
                    UserId2 = u2
                });

                context.ConversationMembers.Add(new ConversationMember
                {
                    Id = Guid.NewGuid(),
                    ConversationId = directConv.Id,
                    UserId = user1.Id,
                    Role = "Member",
                    JoinedAt = item.Time.AddHours(-1)
                });
                context.ConversationMembers.Add(new ConversationMember
                {
                    Id = Guid.NewGuid(),
                    ConversationId = directConv.Id,
                    UserId = item.Partner.Id,
                    Role = "Member",
                    JoinedAt = item.Time.AddHours(-1)
                });

                var directMsg = new Message
                {
                    Id = Guid.NewGuid(),
                    ConversationId = directConv.Id,
                    SenderId = item.Partner.Id,
                    Content = item.Msg,
                    MessageType = "Text",
                    CreatedAt = item.Time
                };
                context.Messages.Add(directMsg);
            }

            // Lưu lần 1 để tạo Conversation và Message (tránh circular dependency)
            await context.SaveChangesAsync();

            // Cập nhật LastMessageId sau khi Message đã tồn tại trong DB
            groupConv.LastMessageId = msg5.Id;
            foreach (var conv in context.Conversations.Local.Where(c => !c.IsGroup))
            {
                var latestMsg = context.Messages.Local
                    .Where(m => m.ConversationId == conv.Id)
                    .OrderByDescending(m => m.CreatedAt)
                    .FirstOrDefault();
                if (latestMsg != null)
                {
                    conv.LastMessageId = latestMsg.Id;
                }
            }

            await context.SaveChangesAsync();
            logger.LogInformation("Khởi tạo thành công nhóm chat và hội thoại 1-1 mẫu.");
        }
    }
}
