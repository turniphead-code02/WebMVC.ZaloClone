-- ==========================================================
-- YÊU CẦU: PostgreSQL >= 15 (dùng ON DELETE SET NULL (column_list))
-- gen_random_uuid() có sẵn từ PG13, không cần extension pgcrypto.
-- ==========================================================
BEGIN;

-- ==========================================================
-- 0. HÀM TIỆN ÍCH
-- ==========================================================

-- UUIDv7: tăng dần theo thời gian -> B-tree ít phân mảnh, sắp xếp theo id = theo thời gian.
-- Khi nâng cấp lên PG18 có thể thay bằng hàm có sẵn uuidv7().
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

-- Tự động cập nhật cột updated_at
CREATE OR REPLACE FUNCTION set_updated_at() RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at := now();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;


-- ==========================================================
-- 1. BẢNG USERS (TÀI KHOẢN & HỒ SƠ)
-- ==========================================================
CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    email VARCHAR(254) NOT NULL,          -- Lưu dạng đã chuẩn hóa (lowercase + trim) từ tầng app
    phone_number VARCHAR(20),
    password_hash TEXT NOT NULL,
    -- Đổi giá trị mỗi khi đổi mật khẩu / bị khóa -> vô hiệu hóa cookie đăng nhập và token quên mật khẩu cũ
    security_stamp UUID NOT NULL DEFAULT gen_random_uuid(),
    display_name VARCHAR(100) NOT NULL,
    avatar_url TEXT,
    bio VARCHAR(255),
    role VARCHAR(20) NOT NULL DEFAULT 'User' CHECK (role IN ('User', 'Admin')),
    is_locked BOOLEAN NOT NULL DEFAULT FALSE,
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at TIMESTAMPTZ,
    -- Lưu ý: cột cập nhật rất thường xuyên -> cân nhắc ghi qua Redis rồi flush định kỳ để tránh bloat
    last_active_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT chk_email_valid CHECK (email <> '' AND position('@' IN email) > 1),
    CONSTRAINT chk_email_normalized CHECK (email = lower(btrim(email))),
    CONSTRAINT chk_phone_normalized CHECK (phone_number IS NULL OR (phone_number <> '' AND phone_number = btrim(phone_number))),
    CONSTRAINT chk_display_name_not_blank CHECK (btrim(display_name) <> ''),
    CONSTRAINT chk_soft_delete CHECK (
        (is_deleted = TRUE AND deleted_at IS NOT NULL)
        OR
        (is_deleted = FALSE AND deleted_at IS NULL)
    )
);

-- Partial Unique Index: Cho phép tái sử dụng Email/SĐT nếu tài khoản cũ đã xóa mềm
-- Dữ liệu đã chuẩn hóa nên query WHERE email = $1 dùng thẳng được index
CREATE UNIQUE INDEX uq_users_email_active ON users (email) WHERE is_deleted = FALSE;
CREATE UNIQUE INDEX uq_users_phone_active ON users (phone_number)
WHERE is_deleted = FALSE AND phone_number IS NOT NULL;

CREATE TRIGGER trg_users_updated_at BEFORE UPDATE ON users
FOR EACH ROW EXECUTE FUNCTION set_updated_at();


-- ==========================================================
-- 2. QUAN HỆ BẠN BÈ & LỜI MỜI
-- ==========================================================

-- Bảng Lời mời kết bạn (giữ chiều sender -> receiver nên không ép thứ tự cặp)
CREATE TABLE friend_requests (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    sender_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    receiver_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    status VARCHAR(20) NOT NULL DEFAULT 'Pending'
        CHECK (status IN ('Pending', 'Accepted', 'Rejected', 'Cancelled')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chk_no_self_request CHECK (sender_id <> receiver_id)
);

-- Mỗi cặp người chỉ có tối đa 1 lời mời đang xử lý (bất kể chiều)
CREATE UNIQUE INDEX uq_pending_friend_request
ON friend_requests (LEAST(sender_id, receiver_id), GREATEST(sender_id, receiver_id))
WHERE status = 'Pending';

CREATE INDEX idx_friend_requests_incoming ON friend_requests(receiver_id, created_at DESC) WHERE status = 'Pending';
CREATE INDEX idx_friend_requests_outgoing ON friend_requests(sender_id, created_at DESC) WHERE status = 'Pending';

CREATE TRIGGER trg_friend_requests_updated_at BEFORE UPDATE ON friend_requests
FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- Bảng Bạn bè chính thức
-- Quy ước: user_id_1 < user_id_2 (tầng app sắp xếp trước khi insert/query)
CREATE TABLE friendships (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id_1 UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    user_id_2 UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    established_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chk_friendship_ordered CHECK (user_id_1 < user_id_2),
    CONSTRAINT uq_friendship_pair UNIQUE (user_id_1, user_id_2) -- Index này phục vụ luôn tra cứu theo user_id_1
);

CREATE INDEX idx_friendships_user2 ON friendships(user_id_2);

-- Bảng Chặn người dùng
CREATE TABLE user_blocks (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    blocker_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    blocked_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chk_no_self_block CHECK (blocker_id <> blocked_id),
    CONSTRAINT uq_blocker_blocked UNIQUE (blocker_id, blocked_id)
);

-- Tra cứu "ai đã chặn tôi" (kiểm tra chặn 2 chiều)
CREATE INDEX idx_user_blocks_blocked ON user_blocks(blocked_id);


-- ==========================================================
-- 3. CUỘC TRÒ CHUYỆN & THÀNH VIÊN
-- ==========================================================

-- Bảng Cuộc trò chuyện (Dùng chung 1-1 và Nhóm)
CREATE TABLE conversations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    title VARCHAR(150),
    avatar_url TEXT,
    is_group BOOLEAN NOT NULL DEFAULT FALSE,
    created_by_id UUID REFERENCES users(id) ON DELETE SET NULL,
    last_message_id UUID,                 -- FK được thêm sau khi tạo bảng messages
    last_message_preview TEXT,
    last_message_at TIMESTAMPTZ,          -- Mặc định NULL cho phòng mới tạo
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    -- Khóa kép để direct_conversations chỉ tham chiếu được hội thoại 1-1
    CONSTRAINT uq_conversations_id_is_group UNIQUE (id, is_group)
);
-- Ghi chú: Inbox luôn lọc theo user qua conversation_members rồi join sang đây,
-- nên KHÔNG tạo index sắp xếp toàn cục theo last_message_at (không được dùng tới).

CREATE TRIGGER trg_conversations_updated_at BEFORE UPDATE ON conversations
FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- Bảng Định danh chat 1-1 (Chống Race Condition)
-- Quy ước: user_id_1 < user_id_2
CREATE TABLE direct_conversations (
    conversation_id UUID PRIMARY KEY,
    is_group BOOLEAN NOT NULL DEFAULT FALSE CHECK (is_group = FALSE),
    user_id_1 UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    user_id_2 UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    CONSTRAINT chk_direct_users_ordered CHECK (user_id_1 < user_id_2),
    CONSTRAINT uq_direct_conversations_pair UNIQUE (user_id_1, user_id_2),
    CONSTRAINT fk_direct_conversation_not_group
        FOREIGN KEY (conversation_id, is_group)
        REFERENCES conversations(id, is_group)
        ON DELETE CASCADE
);

CREATE INDEX idx_direct_conversations_user2 ON direct_conversations(user_id_2);

-- Bảng Thành viên cuộc trò chuyện
CREATE TABLE conversation_members (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    conversation_id UUID NOT NULL REFERENCES conversations(id) ON DELETE CASCADE,
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    role VARCHAR(20) NOT NULL DEFAULT 'Member' CHECK (role IN ('Owner', 'Admin', 'Member')),
    joined_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    left_at TIMESTAMPTZ,                  -- Quản lý rời nhóm (Soft-delete thành viên)
    last_read_message_id UUID,            -- Mốc đã đọc chính xác (FK thêm sau khi tạo messages)
    last_read_at TIMESTAMPTZ,
    is_muted BOOLEAN NOT NULL DEFAULT FALSE,
    CONSTRAINT uq_conversation_member UNIQUE (conversation_id, user_id),
    CONSTRAINT chk_left_after_joined CHECK (left_at IS NULL OR left_at >= joined_at)
);

-- Tối ưu hóa truy vấn Inbox cho các cuộc trò chuyện đang tham gia
CREATE INDEX idx_conversation_members_active_inbox
ON conversation_members(user_id, conversation_id)
WHERE left_at IS NULL;


-- ==========================================================
-- 4. TIN NHẮN & ĐÍNH KÈM
-- ==========================================================

CREATE TABLE messages (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v7(),
    conversation_id UUID NOT NULL REFERENCES conversations(id) ON DELETE CASCADE,
    sender_id UUID,
    content TEXT,
    message_type VARCHAR(20) NOT NULL DEFAULT 'Text'
        CHECK (message_type IN ('Text', 'Image', 'File', 'System')),
    reply_to_id UUID,
    is_pinned BOOLEAN NOT NULL DEFAULT FALSE,
    pinned_at TIMESTAMPTZ,
    pinned_by_id UUID,
    is_recalled BOOLEAN NOT NULL DEFAULT FALSE,
    recalled_at TIMESTAMPTZ,
    edited_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    -- RÀNG BUỘC 1: Tin nhắn System không có người gửi, tin nhắn khác bắt buộc có người gửi
    CONSTRAINT chk_message_sender_type CHECK (
        (message_type = 'System' AND sender_id IS NULL)
        OR
        (message_type <> 'System' AND sender_id IS NOT NULL)
    ),

    -- RÀNG BUỘC 2: Người gửi bắt buộc phải có bản ghi thành viên trong hội thoại
    -- (việc "đang còn trong nhóm" được kiểm tra bởi trigger trg_messages_check_active_member)
    CONSTRAINT fk_messages_sender_is_member
        FOREIGN KEY (conversation_id, sender_id)
        REFERENCES conversation_members(conversation_id, user_id)
        ON DELETE RESTRICT,

    -- RÀNG BUỘC 3: Khóa kép phục vụ việc kiểm tra tin nhắn trả lời
    CONSTRAINT uq_messages_conversation_id UNIQUE (conversation_id, id),

    -- RÀNG BUỘC 4: Tin nhắn trả lời phải thuộc cùng hội thoại; khi xóa gốc chỉ đặt reply_to_id = NULL
    CONSTRAINT fk_messages_reply_in_same_conversation
        FOREIGN KEY (conversation_id, reply_to_id)
        REFERENCES messages(conversation_id, id)
        ON DELETE SET NULL (reply_to_id),

    -- RÀNG BUỘC 5: Tin nhắn Text (chưa thu hồi) phải có nội dung
    CONSTRAINT chk_text_has_content CHECK (
        message_type <> 'Text' OR is_recalled = TRUE
        OR (content IS NOT NULL AND btrim(content) <> '')
    ),

    -- RÀNG BUỘC 6: Trạng thái thu hồi / ghim nhất quán với mốc thời gian
    CONSTRAINT chk_recall_consistent CHECK (
        (is_recalled = TRUE AND recalled_at IS NOT NULL)
        OR
        (is_recalled = FALSE AND recalled_at IS NULL)
    ),
    CONSTRAINT chk_pin_consistent CHECK (
        (is_pinned = TRUE AND pinned_at IS NOT NULL AND pinned_by_id IS NOT NULL)
        OR
        (is_pinned = FALSE AND pinned_at IS NULL AND pinned_by_id IS NULL)
    ),

    -- RÀNG BUỘC 7: Người ghim phải là thành viên của hội thoại
    CONSTRAINT fk_messages_pinned_by_member
        FOREIGN KEY (conversation_id, pinned_by_id)
        REFERENCES conversation_members(conversation_id, user_id)
        ON DELETE RESTRICT
);

-- Index phân trang tin nhắn dạng con trỏ (Cursor Pagination)
CREATE INDEX idx_messages_pagination
ON messages(conversation_id, created_at DESC, id DESC);

-- Phục vụ ON DELETE SET NULL của reply_to_id và tra cứu "các tin trả lời tin X"
CREATE INDEX idx_messages_reply
ON messages(conversation_id, reply_to_id)
WHERE reply_to_id IS NOT NULL;

-- Danh sách tin đã ghim trong hội thoại
CREATE INDEX idx_messages_pinned
ON messages(conversation_id, pinned_at DESC)
WHERE is_pinned = TRUE;

-- Trigger: người gửi phải ĐANG là thành viên (left_at IS NULL) tại thời điểm gửi
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

-- FK hoãn lại: last_message_id / last_read_message_id phải thuộc đúng hội thoại
ALTER TABLE conversations
    ADD CONSTRAINT fk_conversations_last_message
    FOREIGN KEY (id, last_message_id)
    REFERENCES messages(conversation_id, id)
    ON DELETE SET NULL (last_message_id);

ALTER TABLE conversation_members
    ADD CONSTRAINT fk_members_last_read_message
    FOREIGN KEY (conversation_id, last_read_message_id)
    REFERENCES messages(conversation_id, id)
    ON DELETE SET NULL (last_read_message_id);

-- Bảng Tệp đính kèm
CREATE TABLE message_attachments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    message_id UUID NOT NULL REFERENCES messages(id) ON DELETE CASCADE,
    file_name VARCHAR(255) NOT NULL,
    storage_key TEXT NOT NULL,            -- Key trong object storage (S3/MinIO...), URL được build ở tầng app
    file_type VARCHAR(100) NOT NULL,      -- MIME type, vd: image/png
    file_size BIGINT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chk_attachment_size CHECK (file_size > 0 AND file_size <= 52428800) -- Tối đa 50MB
);

CREATE INDEX idx_attachments_message_id ON message_attachments(message_id);

COMMIT;

-- ==========================================================
-- CÁC RÀNG BUỘC CẦN ĐẢM BẢO Ở TẦNG ỨNG DỤNG
-- ==========================================================
-- * Tin 'Image'/'File' phải có ít nhất 1 attachment (insert cùng transaction).
-- * Nhóm luôn có ít nhất 1 'Owner'; chuyển quyền trước khi Owner rời nhóm.
-- * Hội thoại 1-1 có đúng 2 thành viên, khớp với direct_conversations.
-- * Không cho gửi lời mời kết bạn / nhắn tin khi một trong hai bên đã chặn.
-- * Sắp xếp cặp (user_id_1 < user_id_2) trước khi insert/query friendships, direct_conversations.
-- * Chuẩn hóa email (lower + trim) và số điện thoại trước khi lưu.
-- * Khi thu hồi tin cuối cùng, cập nhật lại conversations.last_message_preview.
