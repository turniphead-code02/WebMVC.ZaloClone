using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZaloClone.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    security_stamp = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    avatar_url = table.Column<string>(type: "text", nullable: true),
                    bio = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "User"),
                    is_locked = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_active_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "friend_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receiver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_friend_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_friend_requests_users_receiver_id",
                        column: x => x.receiver_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_friend_requests_users_sender_id",
                        column: x => x.sender_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "friendships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id_1 = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id_2 = table.Column<Guid>(type: "uuid", nullable: false),
                    established_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_friendships", x => x.id);
                    table.ForeignKey(
                        name: "fk_friendships_users_user_id_1",
                        column: x => x.user_id_1,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_friendships_users_user_id_2",
                        column: x => x.user_id_2,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_blocks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    blocker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    blocked_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_blocks", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_blocks_users_blocked_id",
                        column: x => x.blocked_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_blocks_users_blocker_id",
                        column: x => x.blocker_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "conversation_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Member"),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    left_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_read_message_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_muted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conversation_members", x => x.id);
                    table.UniqueConstraint("uq_conversation_member", x => new { x.conversation_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_conversation_members_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "conversations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    avatar_url = table.Column<string>(type: "text", nullable: true),
                    is_group = table.Column<bool>(type: "boolean", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_message_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_message_preview = table.Column<string>(type: "text", nullable: true),
                    last_message_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conversations", x => x.id);
                    table.UniqueConstraint("uq_conversations_id_is_group", x => new { x.id, x.is_group });
                    table.ForeignKey(
                        name: "fk_conversations_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "direct_conversations",
                columns: table => new
                {
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_group = table.Column<bool>(type: "boolean", nullable: false),
                    user_id_1 = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id_2 = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_direct_conversations", x => x.conversation_id);
                    table.ForeignKey(
                        name: "fk_direct_conversations_conversations_conversation_id_is_group",
                        columns: x => new { x.conversation_id, x.is_group },
                        principalTable: "conversations",
                        principalColumns: new[] { "id", "is_group" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_direct_conversations_users_user_id_1",
                        column: x => x.user_id_1,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_direct_conversations_users_user_id_2",
                        column: x => x.user_id_2,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_id = table.Column<Guid>(type: "uuid", nullable: true),
                    content = table.Column<string>(type: "text", nullable: true),
                    message_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Text"),
                    reply_to_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_pinned = table.Column<bool>(type: "boolean", nullable: false),
                    pinned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    pinned_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_recalled = table.Column<bool>(type: "boolean", nullable: false),
                    recalled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    edited_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_messages", x => x.id);
                    table.UniqueConstraint("uq_messages_conversation_id", x => new { x.conversation_id, x.id });
                    table.ForeignKey(
                        name: "fk_messages_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_messages_messages_conversation_id_reply_to_id",
                        columns: x => new { x.conversation_id, x.reply_to_id },
                        principalTable: "messages",
                        principalColumns: new[] { "conversation_id", "id" });
                    table.ForeignKey(
                        name: "fk_messages_users_pinned_by_id",
                        column: x => x.pinned_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_messages_users_sender_id",
                        column: x => x.sender_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "message_attachments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    storage_key = table.Column<string>(type: "text", nullable: false),
                    file_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_message_attachments", x => x.id);
                    table.ForeignKey(
                        name: "fk_message_attachments_messages_message_id",
                        column: x => x.message_id,
                        principalTable: "messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_conversation_members_active_inbox",
                table: "conversation_members",
                columns: new[] { "user_id", "conversation_id" },
                filter: "left_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_conversation_members_conversation_id_last_read_message_id",
                table: "conversation_members",
                columns: new[] { "conversation_id", "last_read_message_id" });

            migrationBuilder.CreateIndex(
                name: "ix_conversations_created_by_id",
                table: "conversations",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_conversations_id_last_message_id",
                table: "conversations",
                columns: new[] { "id", "last_message_id" });

            migrationBuilder.CreateIndex(
                name: "idx_direct_conversations_user2",
                table: "direct_conversations",
                column: "user_id_2");

            migrationBuilder.CreateIndex(
                name: "ix_direct_conversations_conversation_id_is_group",
                table: "direct_conversations",
                columns: new[] { "conversation_id", "is_group" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_direct_conversations_pair",
                table: "direct_conversations",
                columns: new[] { "user_id_1", "user_id_2" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_friend_requests_incoming",
                table: "friend_requests",
                columns: new[] { "receiver_id", "created_at" },
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "idx_friend_requests_outgoing",
                table: "friend_requests",
                columns: new[] { "sender_id", "created_at" },
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "idx_friendships_user2",
                table: "friendships",
                column: "user_id_2");

            migrationBuilder.CreateIndex(
                name: "uq_friendship_pair",
                table: "friendships",
                columns: new[] { "user_id_1", "user_id_2" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_attachments_message_id",
                table: "message_attachments",
                column: "message_id");

            migrationBuilder.CreateIndex(
                name: "idx_messages_pagination",
                table: "messages",
                columns: new[] { "conversation_id", "created_at", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "idx_messages_pinned",
                table: "messages",
                columns: new[] { "conversation_id", "pinned_at" },
                descending: new[] { false, true },
                filter: "is_pinned = TRUE");

            migrationBuilder.CreateIndex(
                name: "idx_messages_reply",
                table: "messages",
                columns: new[] { "conversation_id", "reply_to_id" },
                filter: "reply_to_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_messages_pinned_by_id",
                table: "messages",
                column: "pinned_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_messages_sender_id",
                table: "messages",
                column: "sender_id");

            migrationBuilder.CreateIndex(
                name: "idx_user_blocks_blocked",
                table: "user_blocks",
                column: "blocked_id");

            migrationBuilder.CreateIndex(
                name: "uq_blocker_blocked",
                table: "user_blocks",
                columns: new[] { "blocker_id", "blocked_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_users_email_active",
                table: "users",
                column: "email",
                unique: true,
                filter: "is_deleted = FALSE");

            migrationBuilder.CreateIndex(
                name: "uq_users_phone_active",
                table: "users",
                column: "phone_number",
                unique: true,
                filter: "is_deleted = FALSE AND phone_number IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_conversation_members_conversations_conversation_id",
                table: "conversation_members",
                column: "conversation_id",
                principalTable: "conversations",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_conversation_members_messages_conversation_id_last_read_mes",
                table: "conversation_members",
                columns: new[] { "conversation_id", "last_read_message_id" },
                principalTable: "messages",
                principalColumns: new[] { "conversation_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "fk_conversations_messages_id_last_message_id",
                table: "conversations",
                columns: new[] { "id", "last_message_id" },
                principalTable: "messages",
                principalColumns: new[] { "conversation_id", "id" });

            // 1. Hàm uuid_generate_v7
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION uuid_generate_v7() RETURNS UUID AS $$
    SELECT encode(
        set_bit(
            set_bit(
                overlay(uuid_send(gen_random_uuid())
                        PLACING substring(int8send(floor(extract(epoch FROM clock_timestamp()) * 1000)::BIGINT) FROM 3)
                        FROM 1 FOR 6),
                52, 1),
            53, 1),
        'hex')::UUID;
$$ LANGUAGE sql VOLATILE;

ALTER TABLE messages ALTER COLUMN id SET DEFAULT uuid_generate_v7();
");

            // 2. Hàm và trigger tự động cập nhật updated_at
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION set_updated_at() RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at := now();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_users_updated_at BEFORE UPDATE ON users
FOR EACH ROW EXECUTE FUNCTION set_updated_at();

CREATE TRIGGER trg_friend_requests_updated_at BEFORE UPDATE ON friend_requests
FOR EACH ROW EXECUTE FUNCTION set_updated_at();

CREATE TRIGGER trg_conversations_updated_at BEFORE UPDATE ON conversations
FOR EACH ROW EXECUTE FUNCTION set_updated_at();
");

            // 3. Unique index LEAST/GREATEST cho friend_requests
            migrationBuilder.Sql(@"
CREATE UNIQUE INDEX uq_pending_friend_request
ON friend_requests (LEAST(sender_id, receiver_id), GREATEST(sender_id, receiver_id))
WHERE status = 'Pending';
");

            // 4. Ràng buộc CHECK
            migrationBuilder.Sql(@"
ALTER TABLE users ADD CONSTRAINT chk_email_valid CHECK (email <> '' AND position('@' IN email) > 1);
ALTER TABLE users ADD CONSTRAINT chk_email_normalized CHECK (email = lower(btrim(email)));
ALTER TABLE users ADD CONSTRAINT chk_phone_normalized CHECK (phone_number IS NULL OR (phone_number <> '' AND phone_number = btrim(phone_number)));
ALTER TABLE users ADD CONSTRAINT chk_display_name_not_blank CHECK (btrim(display_name) <> '');
ALTER TABLE users ADD CONSTRAINT chk_soft_delete CHECK (
    (is_deleted = TRUE AND deleted_at IS NOT NULL) OR
    (is_deleted = FALSE AND deleted_at IS NULL)
);

ALTER TABLE friend_requests ADD CONSTRAINT chk_no_self_request CHECK (sender_id <> receiver_id);
ALTER TABLE friendships ADD CONSTRAINT chk_friendship_ordered CHECK (user_id_1 < user_id_2);
ALTER TABLE user_blocks ADD CONSTRAINT chk_no_self_block CHECK (blocker_id <> blocked_id);
ALTER TABLE direct_conversations ADD CONSTRAINT chk_direct_users_ordered CHECK (user_id_1 < user_id_2);
ALTER TABLE direct_conversations ADD CONSTRAINT chk_direct_is_not_group CHECK (is_group = FALSE);
ALTER TABLE conversation_members ADD CONSTRAINT chk_left_after_joined CHECK (left_at IS NULL OR left_at >= joined_at);

ALTER TABLE messages ADD CONSTRAINT chk_message_sender_type CHECK (
    (message_type = 'System' AND sender_id IS NULL) OR
    (message_type <> 'System' AND sender_id IS NOT NULL)
);
ALTER TABLE messages ADD CONSTRAINT chk_text_has_content CHECK (
    message_type <> 'Text' OR is_recalled = TRUE OR (content IS NOT NULL AND btrim(content) <> '')
);
ALTER TABLE messages ADD CONSTRAINT chk_recall_consistent CHECK (
    (is_recalled = TRUE AND recalled_at IS NOT NULL) OR
    (is_recalled = FALSE AND recalled_at IS NULL)
);
ALTER TABLE messages ADD CONSTRAINT chk_pin_consistent CHECK (
    (is_pinned = TRUE AND pinned_at IS NOT NULL AND pinned_by_id IS NOT NULL) OR
    (is_pinned = FALSE AND pinned_at IS NULL AND pinned_by_id IS NULL)
);
ALTER TABLE message_attachments ADD CONSTRAINT chk_attachment_size CHECK (file_size > 0 AND file_size <= 52428800);
");

            // 5. Trigger kiểm tra người gửi còn đang trong hội thoại
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION check_sender_is_active_member() RETURNS TRIGGER AS $$
BEGIN
    IF NEW.sender_id IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM conversation_members
        WHERE conversation_id = NEW.conversation_id
          AND user_id = NEW.sender_id
          AND left_at IS NULL
    ) THEN
        RAISE EXCEPTION 'User % is not an active member of conversation %', NEW.sender_id, NEW.conversation_id
            USING ERRCODE = 'foreign_key_violation';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_messages_check_active_member
BEFORE INSERT ON messages
FOR EACH ROW EXECUTE FUNCTION check_sender_is_active_member();
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_messages_conversations_conversation_id",
                table: "messages");

            migrationBuilder.DropTable(
                name: "conversation_members");

            migrationBuilder.DropTable(
                name: "direct_conversations");

            migrationBuilder.DropTable(
                name: "friend_requests");

            migrationBuilder.DropTable(
                name: "friendships");

            migrationBuilder.DropTable(
                name: "message_attachments");

            migrationBuilder.DropTable(
                name: "user_blocks");

            migrationBuilder.DropTable(
                name: "conversations");

            migrationBuilder.DropTable(
                name: "messages");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
