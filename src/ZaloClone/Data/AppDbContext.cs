using Microsoft.EntityFrameworkCore;
using ZaloClone.Models.Entities;

namespace ZaloClone.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<FriendRequest> FriendRequests => Set<FriendRequest>();
    public DbSet<Friendship> Friendships => Set<Friendship>();
    public DbSet<UserBlock> UserBlocks => Set<UserBlock>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<DirectConversation> DirectConversations => Set<DirectConversation>();
    public DbSet<ConversationMember> ConversationMembers => Set<ConversationMember>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<MessageAttachment> MessageAttachments => Set<MessageAttachment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. USERS
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).HasMaxLength(254).IsRequired();
            entity.Property(e => e.PhoneNumber).HasMaxLength(20);
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Bio).HasMaxLength(255);
            entity.Property(e => e.Role).HasMaxLength(20).HasDefaultValue("User");

            entity.HasIndex(e => e.Email)
                  .IsUnique()
                  .HasFilter("is_deleted = FALSE")
                  .HasDatabaseName("uq_users_email_active");

            entity.HasIndex(e => e.PhoneNumber)
                  .IsUnique()
                  .HasFilter("is_deleted = FALSE AND phone_number IS NOT NULL")
                  .HasDatabaseName("uq_users_phone_active");
        });

        // 2. FRIEND REQUESTS
        modelBuilder.Entity<FriendRequest>(entity =>
        {
            entity.ToTable("friend_requests");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("Pending");

            entity.HasOne(e => e.Sender)
                  .WithMany(u => u.SentFriendRequests)
                  .HasForeignKey(e => e.SenderId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Receiver)
                  .WithMany(u => u.ReceivedFriendRequests)
                  .HasForeignKey(e => e.ReceiverId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.ReceiverId, e.CreatedAt })
                  .HasFilter("status = 'Pending'")
                  .HasDatabaseName("idx_friend_requests_incoming");

            entity.HasIndex(e => new { e.SenderId, e.CreatedAt })
                  .HasFilter("status = 'Pending'")
                  .HasDatabaseName("idx_friend_requests_outgoing");
        });

        // 3. FRIENDSHIPS
        modelBuilder.Entity<Friendship>(entity =>
        {
            entity.ToTable("friendships");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId1).HasColumnName("user_id_1");
            entity.Property(e => e.UserId2).HasColumnName("user_id_2");

            entity.HasIndex(e => new { e.UserId1, e.UserId2 })
                  .IsUnique()
                  .HasDatabaseName("uq_friendship_pair");

            entity.HasIndex(e => e.UserId2)
                  .HasDatabaseName("idx_friendships_user2");

            entity.HasOne(e => e.User1)
                  .WithMany(u => u.FriendshipsAsUser1)
                  .HasForeignKey(e => e.UserId1)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User2)
                  .WithMany(u => u.FriendshipsAsUser2)
                  .HasForeignKey(e => e.UserId2)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // 4. USER BLOCKS
        modelBuilder.Entity<UserBlock>(entity =>
        {
            entity.ToTable("user_blocks");
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => new { e.BlockerId, e.BlockedId })
                  .IsUnique()
                  .HasDatabaseName("uq_blocker_blocked");

            entity.HasIndex(e => e.BlockedId)
                  .HasDatabaseName("idx_user_blocks_blocked");

            entity.HasOne(e => e.Blocker)
                  .WithMany(u => u.BlockedUsers)
                  .HasForeignKey(e => e.BlockerId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Blocked)
                  .WithMany(u => u.BlockedByUsers)
                  .HasForeignKey(e => e.BlockedId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // 5. CONVERSATIONS
        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.ToTable("conversations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(150);

            entity.HasAlternateKey(e => new { e.Id, e.IsGroup })
                  .HasName("uq_conversations_id_is_group");

            entity.HasOne(e => e.CreatedBy)
                  .WithMany()
                  .HasForeignKey(e => e.CreatedById)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.LastMessage)
                  .WithMany()
                  .HasForeignKey(e => new { e.Id, e.LastMessageId })
                  .HasPrincipalKey(m => new { m.ConversationId, m.Id })
                  .OnDelete(DeleteBehavior.ClientSetNull);
        });

        // 6. DIRECT CONVERSATIONS
        modelBuilder.Entity<DirectConversation>(entity =>
        {
            entity.ToTable("direct_conversations");
            entity.HasKey(e => e.ConversationId);
            entity.Property(e => e.UserId1).HasColumnName("user_id_1");
            entity.Property(e => e.UserId2).HasColumnName("user_id_2");

            entity.HasIndex(e => new { e.UserId1, e.UserId2 })
                  .IsUnique()
                  .HasDatabaseName("uq_direct_conversations_pair");

            entity.HasIndex(e => e.UserId2)
                  .HasDatabaseName("idx_direct_conversations_user2");

            entity.HasOne(e => e.Conversation)
                  .WithOne(c => c.DirectConversation)
                  .HasForeignKey<DirectConversation>(e => new { e.ConversationId, e.IsGroup })
                  .HasPrincipalKey<Conversation>(c => new { c.Id, c.IsGroup })
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User1)
                  .WithMany()
                  .HasForeignKey(e => e.UserId1)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.User2)
                  .WithMany()
                  .HasForeignKey(e => e.UserId2)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // 7. CONVERSATION MEMBERS
        modelBuilder.Entity<ConversationMember>(entity =>
        {
            entity.ToTable("conversation_members");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Role).HasMaxLength(20).HasDefaultValue("Member");

            entity.HasAlternateKey(e => new { e.ConversationId, e.UserId })
                  .HasName("uq_conversation_member");

            entity.HasIndex(e => new { e.UserId, e.ConversationId })
                  .HasFilter("left_at IS NULL")
                  .HasDatabaseName("idx_conversation_members_active_inbox");

            entity.HasOne(e => e.Conversation)
                  .WithMany(c => c.Members)
                  .HasForeignKey(e => e.ConversationId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                  .WithMany(u => u.ConversationMemberships)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.LastReadMessage)
                  .WithMany()
                  .HasForeignKey(e => new { e.ConversationId, e.LastReadMessageId })
                  .HasPrincipalKey(m => new { m.ConversationId, m.Id })
                  .OnDelete(DeleteBehavior.ClientSetNull);
        });

        // 8. MESSAGES
        modelBuilder.Entity<Message>(entity =>
        {
            entity.ToTable("messages");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MessageType).HasMaxLength(20).HasDefaultValue("Text");

            entity.HasAlternateKey(e => new { e.ConversationId, e.Id })
                  .HasName("uq_messages_conversation_id");

            entity.HasIndex(e => new { e.ConversationId, e.CreatedAt, e.Id })
                  .IsDescending(false, true, true)
                  .HasDatabaseName("idx_messages_pagination");

            entity.HasIndex(e => new { e.ConversationId, e.ReplyToId })
                  .HasFilter("reply_to_id IS NOT NULL")
                  .HasDatabaseName("idx_messages_reply");

            entity.HasIndex(e => new { e.ConversationId, e.PinnedAt })
                  .IsDescending(false, true)
                  .HasFilter("is_pinned = TRUE")
                  .HasDatabaseName("idx_messages_pinned");

            entity.HasOne(e => e.Conversation)
                  .WithMany(c => c.Messages)
                  .HasForeignKey(e => e.ConversationId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Sender)
                  .WithMany()
                  .HasForeignKey(e => e.SenderId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ReplyTo)
                  .WithMany(m => m.Replies)
                  .HasForeignKey(e => new { e.ConversationId, e.ReplyToId })
                  .HasPrincipalKey(m => new { m.ConversationId, m.Id })
                  .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.PinnedBy)
                  .WithMany()
                  .HasForeignKey(e => e.PinnedById)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // 9. MESSAGE ATTACHMENTS
        modelBuilder.Entity<MessageAttachment>(entity =>
        {
            entity.ToTable("message_attachments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.StorageKey).IsRequired();
            entity.Property(e => e.FileType).HasMaxLength(100).IsRequired();

            entity.HasIndex(e => e.MessageId)
                  .HasDatabaseName("idx_attachments_message_id");

            entity.HasOne(e => e.Message)
                  .WithMany(m => m.Attachments)
                  .HasForeignKey(e => e.MessageId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
