# Bảng Test Case E2E — Zalo Clone (Sprint 2)

> Phụ trách: **Cặp 5** — Q.Long, P.Long.
> Môi trường chuẩn: Windows, .NET 10, PostgreSQL 16 (Docker), chạy profile `http` (`http://localhost:5202`) hoặc `https` (`https://localhost:7177`).
> Tài khoản mẫu (SeedData): `admin@zalo.local`, `user1@zalo.local`… — mật khẩu `Test@123`.

## Quy ước

| Cột | Ý nghĩa |
|---|---|
| **ID** | `<Mã module>-<số>` — `ACC` Tài khoản, `PRO` Profile, `CHAT` Chat 1-1, `INF` Hạ tầng, `FRD` Bạn bè, `BLK` Chặn, `GRP` Nhóm, `ADM` Admin, `E2E` Luồng chính |
| **Tiền điều kiện** | Trạng thái cần có trước khi chạy (đã đăng nhập, đã là bạn bè…) |
| **Các bước** | Thao tác trên giao diện, đánh số `1.` `2.` `3.` |
| **Mong đợi** | Kết quả đúng theo yêu cầu |
| **Kết quả** | `⬜ Chưa test` · `✅ Pass` · `❌ Fail` · `⏸ Chờ tính năng` (tính năng Sprint 2 chưa merge) |
| **Issue** | Link GitHub Issue khi Fail (mẫu `.github/ISSUE_TEMPLATE/bug.md`) |

Khi một case **Fail**: mở Issue, ghi `#số` vào cột Issue, gắn người phụ trách module (xem `CONVENTIONS.md` mục 2).

---

## Phần Q.Long — Tài khoản · Profile · Chat 1-1 · Hạ tầng

### 1. Tài khoản (ACC) — Huy, Tùng

_Đang soạn._

### 2. Profile & Avatar (PRO) — Tùng

_Đang soạn._

### 3. Chat 1-1 (CHAT) — Khôi, Sang

_Đang soạn._

### 4. Hạ tầng (INF) — Lead, Cặp 5

_Đang soạn._

---

## Phần P.Long — Bạn bè · Chặn · Nhóm · Admin

### 5. Bạn bè (FRD) — Tài

_P.Long soạn._

### 6. Chặn người dùng (BLK) — Hào

_P.Long soạn._

### 7. Nhóm chat (GRP) — Hải

_P.Long soạn._

### 8. Quản trị (ADM) — Đ.Long

_P.Long soạn._

---

## 9. Luồng chính E2E (chạy chung Thứ 7, 2 trình duyệt)

| ID | Luồng | Kết quả | Issue |
|---|---|---|---|
| E2E-01 | Đăng ký A, B → A tìm và kết bạn B → B chấp nhận → nhắn tin 1-1 → gửi file → tạo nhóm A, B, C → thu hồi tin nhắn → Admin khóa B → B không đăng nhập được | ⬜ Chưa test | |
