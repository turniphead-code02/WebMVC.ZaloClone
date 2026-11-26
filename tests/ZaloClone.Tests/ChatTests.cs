using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ZaloClone.Models.Entities;
using ZaloClone.Tests.Infrastructure;
using static ZaloClone.Tests.Infrastructure.TestHelpers;

namespace ZaloClone.Tests;

[Collection(AppCollection.Name)]
public class ChatTests(ZaloAppFactory factory)
{
    /// <summary>Tạo hội thoại 1-1 riêng cho từng test để không ảnh hưởng dữ liệu mẫu.</summary>
    private async Task<Guid> CreateConversationAsync(Guid a, Guid b, int messageCount = 1, Guid? sender = null)
    {
        var conversationId = Guid.NewGuid();
        await factory.WithDbAsync(async db =>
        {
            var now = DateTimeOffset.UtcNow;
            db.Conversations.Add(new Conversation { Id = conversationId, IsGroup = false, Title = "Test", CreatedById = a });
            db.ConversationMembers.Add(new ConversationMember { ConversationId = conversationId, UserId = a });
            db.ConversationMembers.Add(new ConversationMember { ConversationId = conversationId, UserId = b });
            for (var i = 1; i <= messageCount; i++)
            {
                db.Messages.Add(new Message
                {
                    Id = Guid.NewGuid(),
                    ConversationId = conversationId,
                    SenderId = sender ?? a,
                    Content = $"Tin {i}",
                    CreatedAt = now.AddMinutes(-messageCount + i)
                });
            }
            await db.SaveChangesAsync();
        });
        return conversationId;
    }

    private static async Task<List<string>> GetMessageContentsAsync(HttpClient client, Guid conversationId)
    {
        var json = await client.GetFromJsonAsync<JsonElement>($"/Home/GetConversationData?conversationId={conversationId}");
        return json.GetProperty("messages").EnumerateArray()
            .Select(m => m.GetProperty("content").GetString() ?? string.Empty)
            .ToList();
    }

    [Fact(DisplayName = "CHAT-13 Không thu hồi được tin nhắn của người khác")]
    public async Task RecallMessage_OfAnotherUser_IsRejected()
    {
        var conversationId = await CreateConversationAsync(User1Id, User2Id, sender: User2Id);
        Guid messageId = Guid.Empty;
        await factory.WithDbAsync(db =>
        {
            messageId = db.Messages.Single(m => m.ConversationId == conversationId).Id;
            return Task.CompletedTask;
        });
        var client = await LoginAsync(factory, "user1@zalo.local");

        var response = await client.PostAsJsonAsync("/Chat/RecallMessage", new { messageId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "CHAT-16 Không gửi được tin vào hội thoại mình không tham gia")]
    public async Task SendMessage_ToForeignConversation_IsRejected()
    {
        var conversationId = await CreateConversationAsync(User1Id, User2Id);
        var outsider = await LoginAsync(factory, "user3@zalo.local");

        var response = await outsider.PostAsJsonAsync("/Chat/SendMessage", new { conversationId, content = "xâm nhập" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "CHAT-15 Không đọc được tin nhắn của hội thoại mình không tham gia", Skip = "Bug #4 — bỏ Skip khi đã sửa")]
    public async Task GetConversationData_ForeignConversation_IsDenied()
    {
        var conversationId = await CreateConversationAsync(User1Id, User2Id);
        var outsider = await LoginAsync(factory, "user3@zalo.local");

        var response = await outsider.GetAsync($"/Home/GetConversationData?conversationId={conversationId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("Tin 1", body);
    }

    [Fact(DisplayName = "CHAT-12 Tin đã thu hồi không lộ nội dung gốc khi tải lại hội thoại")]
    public async Task GetConversationData_RecalledMessage_HidesOriginalContent()
    {
        var conversationId = await CreateConversationAsync(User1Id, User2Id);
        await factory.WithDbAsync(async db =>
        {
            var message = db.Messages.Single(m => m.ConversationId == conversationId);
            message.IsRecalled = true;
            message.RecalledAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        });
        var client = await LoginAsync(factory, "user1@zalo.local");

        var contents = await GetMessageContentsAsync(client, conversationId);

        Assert.DoesNotContain("Tin 1", contents);
    }

    [Fact(DisplayName = "CHAT-05 Hội thoại hơn 50 tin thì tải các tin mới nhất", Skip = "Bug #6 — bỏ Skip khi đã sửa")]
    public async Task GetConversationData_MoreThan50Messages_ReturnsLatest()
    {
        var conversationId = await CreateConversationAsync(User1Id, User2Id, messageCount: 55);
        var client = await LoginAsync(factory, "user1@zalo.local");

        var contents = await GetMessageContentsAsync(client, conversationId);

        Assert.Contains("Tin 55", contents);
    }
}
