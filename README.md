# 💬 Zalo Clone — ASP.NET Core MVC

Đồ án web chat real-time mô phỏng Zalo: kết bạn, chat 1-1, chat nhóm, gửi file/ảnh, trang quản trị.

**Công nghệ:** .NET 10 (LTS) · ASP.NET Core MVC · SignalR · EF Core · PostgreSQL 16 · Docker

> 📘 **Trước khi bắt đầu, mọi thành viên đọc [CONVENTIONS.md](CONVENTIONS.md).**

---

## 🚀 Hướng dẫn chạy dự án lần đầu (Dành cho máy Windows)

### 1. Phần mềm cần có
- **Visual Studio 2026 Community** (khi cài tick chọn mục **ASP.NET and web development**).
- **Docker Desktop** (tick chọn WSL 2).
- **Git for Windows**.

### 2. Các bước khởi động
1. **Lấy code:**
   ```powershell
   git clone <đường-dẫn-repo>
   cd WebMVC.Zalo
   git checkout develop
   ```
2. **Bật Database PostgreSQL:**
   ```powershell
   Copy-Item .env.example .env
   notepad .env     # Đặt mật khẩu POSTGRES_PASSWORD rồi lưu lại (Ctrl+S)
   docker compose up -d
   ```
3. **Cấu hình chuỗi kết nối trong Visual Studio:**
   - Click đúp chuột vào file **`ZaloClone.sln`** để mở Visual Studio.
   - Trong cửa sổ **Solution Explorer**, click chuột phải vào project `ZaloClone` › chọn **Manage User Secrets**.
   - Dán nội dung sau vào (thay bằng mật khẩu bạn vừa đặt ở file `.env`):
     ```json
     {
       "ConnectionStrings": {
         "Default": "Host=localhost;Port=5432;Database=ZaloCloneDb;Username=postgres;Password=<mật-khẩu-trong-.env>"
       }
     }
     ```
4. **Tạo bảng Database:**
   - Mở menu: **Tools** › **NuGet Package Manager** › **Package Manager Console**.
   - Gõ lệnh:
     ```powershell
     Update-Database
     ```
5. **Chạy web:**
   - Nhấn phím **F5** trên bàn phím.
   - Đăng nhập bằng tài khoản mẫu:
     - Admin: `admin@zalo.local` / Mật khẩu: `Test@123`
     - User: `user1@zalo.local` / Mật khẩu: `Test@123`

> 🍎 **Dành riêng cho máy macOS (Lead):**
> Các lệnh terminal tương đương: `cp .env.example .env` → `docker compose up -d` → `dotnet ef database update --project src/ZaloClone` → `dotnet watch run --project src/ZaloClone`.

---

## 🔄 Quy trình làm việc hàng ngày (Không dùng Pull Request)

Nhóm làm việc trực tiếp trên nhánh **`develop`**:
1. **Sáng mở máy:** Trong Visual Studio, bấm nút **Pull** (mũi tên xanh trỏ xuống ở góc dưới hoặc cửa sổ **Git Changes**) để lấy code mới nhất.
2. **Code & Test:** Chỉ code trong thư mục của cặp mình. Nhấn **F5** để chạy thử.
3. **Đẩy code lên:** Mở cửa sổ **Git Changes** › gõ tin nhắn commit › chọn **Commit All and Push**.

---

## 👥 Phân công công việc

| Cặp | Thành viên | Mảng phụ trách | Thư mục code chính |
|---|---|---|---|
| **Lead + Cặp 5** | Lead, PLong, QLong | Khung hệ thống, Database, Docker, SignalR Hub | `Data/`, `Models/Entities/`, `Hubs/`, `Program.cs` |
| **Cặp 1** | Huy, Tùng | Đăng ký, Đăng nhập, Trang cá nhân, Đổi mật khẩu | `Controllers/AccountController.cs`, `Views/Account/` |
| **Cặp 2** | Tài, Hào | Danh bạ, Tìm kiếm SĐT/Email, Gửi & Chấp nhận kết bạn | `Controllers/FriendController.cs`, `Views/Friend/` |
| **Cặp 3** | Khôi, Sang | Khung chat chính 1-1, Trạng thái online/đã xem, Gửi file & ảnh | `Controllers/ChatController.cs`, `Views/Chat/`, `wwwroot/js/chat/` |
| **Cặp 4** | Hải, Đ.Long | Tạo & Quản lý nhóm chat, Trang Admin thống kê & Khóa tài khoản | `Controllers/GroupController.cs`, `Controllers/AdminController.cs` |

---

## 🛠️ Lệnh thường dùng & Xử lý lỗi

| Thao tác | Lệnh / Cách làm |
|---|---|
| Bật / tắt DB | `docker compose up -d` / `docker compose down` |
| Mở giao diện xem DB pgAdmin | `docker compose --profile tools up -d` (mở http://localhost:5050) |
| Cập nhật DB khi Lead báo có migration mới | Visual Studio: mở Package Manager Console gõ `Update-Database` |
| Lỗi: `required variable POSTGRES_PASSWORD is missing` | Bạn chưa tạo file `.env` → chạy `Copy-Item .env.example .env` |
| Lỗi: `cannot connect to Docker engine` | Mở ứng dụng **Docker Desktop** trên Windows và đợi biểu tượng cá voi chuyển màu xanh |
| Lỗi: `port 5432 is already allocated` | Máy bạn đã cài sẵn PostgreSQL từ trước → mở `.env` sửa `POSTGRES_PORT=5433` và sửa `Port=5433` trong User Secrets |
