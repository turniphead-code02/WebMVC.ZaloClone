# 📘 QUY ƯỚC LÀM VIỆC NHÓM — ZALO CLONE (ASP.NET Core MVC)

> **Mọi thành viên bắt buộc đọc file này trước khi bắt đầu code.**
> File này ưu tiên tối đa cho môi trường **Windows + Visual Studio Community**. (macOS chỉ dùng làm máy phụ của Lead).

---

## 0. TL;DR — 10 điều "sống còn" của nhóm

1. **Cấm tự ý sửa Database / Entity / Migration:** Chỉ Lead (Cặp 5 hỗ trợ) tạo migration. Thành viên khác chỉ chạy lệnh cập nhật DB. Cần thêm cột/bảng → tạo Issue hoặc nhắn Lead.
2. **KHÔNG dùng Pull Request (PR):** Để làm việc nhanh nhất, không ai phải ngồi chờ ai duyệt code. Cả nhóm làm việc trực tiếp trên nhánh `develop`.
3. **Quy tắc vàng: LUÔN PULL TRƯỚC KHI PUSH:** Sáng mở máy bấm **Pull**, trước khi bấm **Push** cũng bấm **Pull** để lấy code mới nhất của bạn khác.
4. **Chỉ code trong vùng của cặp mình:** Huy & Tùng (Account), Tài & Hào (Friend), Khôi & Sang (Chat), Hải & Đ.Long (Group/Admin). Tuyệt đối không sửa file của cặp khác (xem [mục 2](#2-phân-công--vùng-code-ownership)).
5. **Không bao giờ commit mật khẩu, file `.env`, thư mục `uploads/` lên Git.**
6. Controller **mỏng** — toàn bộ logic xử lý nghiệp vụ viết trong `Services/`.
7. Không truyền trực tiếp Entity ra View → bắt buộc dùng **ViewModel**.
8. Sự kiện SignalR **chỉ dùng đúng tên đã khai báo** trong [mục 7](#7-signalr-real-time) để không bị lệch nhau giữa các cặp.
9. Thời gian lưu DB dạng **UTC** (`DateTimeOffset.UtcNow`), chỉ đổi sang giờ Việt Nam khi hiển thị lên giao diện.
10. Nhánh `main` là bản chốt nộp bài/demo — **CHỈ Lead** mới được merge từ `develop` sang `main`.

---

## 1. Tech Stack & Môi trường chuẩn

| Thành phần | Phiên bản / Công cụ | Ghi chú |
|---|---|---|
| **Hệ điều hành chuẩn** | **Windows 10 / 11** | Đa số nhóm dùng Windows |
| **IDE chính** | **Visual Studio 2026 Community** | Đã tích hợp sẵn .NET 10 SDK, Git, Docker tooling |
| **.NET SDK** | **.NET 10 (LTS)** | Chốt cố định bằng file `global.json` ở thư mục gốc |
| **Database** | **PostgreSQL 16** | Chạy bằng Docker Desktop (backend WSL 2) |
| **Thư viện DB** | EF Core 10 + Npgsql 10 | Đã bật tự động chuyển tên thành `snake_case` |
| **Real-time** | ASP.NET Core SignalR | Client dùng thư viện JS `@microsoft/signalr` |
| **Hệ điều hành phụ** | macOS (chỉ riêng Lead) | Dùng Rider / VS Code + Terminal (xem mục 13) |

---

## 2. Phân công & Vùng code (Ownership)

Để 9 người code chung mà **không bao giờ bị xung đột (conflict) Git**, mỗi cặp chỉ làm việc trong các file sau:

| Cặp | Thành viên | Mảng phụ trách | Thư mục & File được phép sửa |
|---|---|---|---|
| **Lead + Cặp 5** | Lead, PLong, QLong | Khung hệ thống, DB, Docker, SignalR Hub | `Program.cs`, `Data/`, `Models/Entities/`, `Migrations/`, `Hubs/`, `Views/Shared/`, `docker-compose.yml` |
| **Cặp 1** | Huy, Tùng | Tài khoản & Trang cá nhân | `Controllers/AccountController.cs`<br>`Controllers/ProfileController.cs`<br>`Views/Account/`<br>`Views/Profile/`<br>`Services/Account/` |
| **Cặp 2** | Tài, Hào | Danh bạ & Kết bạn | `Controllers/FriendController.cs`<br>`Views/Friend/`<br>`Services/Friend/` |
| **Cặp 3** | Khôi, Sang | Chat 1-1 & Gửi File/Ảnh | `Controllers/ChatController.cs`<br>`Views/Chat/`<br>`Services/Chat/`<br>`wwwroot/js/chat/` |
| **Cặp 4** | Hải, Đ.Long | Chat nhóm & Trang Admin | `Controllers/GroupController.cs`<br>`Controllers/AdminController.cs`<br>`Views/Group/`<br>`Views/Admin/`<br>`Services/Group/`<br>`Services/Admin/` |

> ⚠️ **File dùng chung** (`_Layout.cshtml`, `site.css`, `site.js`, `Program.cs`, `AppDbContext.cs`, `ChatHub.cs`): Nếu cặp nào cần bổ sung thẻ script, CSS hoặc đăng ký service mới → nhắn tin cho Lead hoặc Cặp 5 để thêm vào, không tự ý sửa.

---

## 3. Cấu trúc thư mục dự án

```
WebMVC.Zalo/                       # Thư mục gốc repo
├── .github/
│   ├── workflows/build.yml        # CI: tự động kiểm tra build khi push lên develop/main
│   └── ISSUE_TEMPLATE/            # Mẫu báo bug, yêu cầu sửa DB
├── docs/                          # Tài liệu đồ án, phân công tuần
├── src/
│   └── ZaloClone/                 # Project ASP.NET Core MVC
│       ├── Controllers/           # Controller từng cặp
│       ├── Services/              # Logic nghiệp vụ chia theo cặp
│       ├── Models/
│       │   ├── Entities/          # 9 bảng Database (Lead quản lý)
│       │   └── ViewModels/        # ViewModel của từng cặp
│       ├── Views/                 # Giao diện Razor Views
│       ├── Data/                  # AppDbContext & SeedData
│       ├── Hubs/                  # ChatHub (SignalR)
│       ├── Infrastructure/        # Helper dùng chung (UserPair, Claims, ApiResponse)
│       ├── Migrations/            # EF Core Migrations (Lead quản lý)
│       └── wwwroot/               # CSS, JS, ảnh tĩnh, uploads/
├── ZaloClone.sln                  # File Solution (click đúp để mở bằng Visual Studio)
├── docker-compose.yml             # Cấu hình chạy PostgreSQL + pgAdmin
├── .env.example                   # Mẫu cấu hình môi trường (copy thành .env)
├── .editorconfig                  # Tự động căn chỉnh format code chuẩn trong VS
├── .gitattributes                 # Tự động sửa lỗi xuống dòng Windows CRLF / Linux LF
├── .gitignore
├── schema.sql                     # Bản thiết kế DB tham chiếu
└── README.md
```

---

## 4. Hướng dẫn thiết lập máy (Dành cho thành viên Windows)

### 4.1 Cài đặt phần mềm (Chỉ làm 1 lần đầu)
1. **Visual Studio 2026 Community:** Khi cài đặt, tick chọn mục **ASP.NET and web development** (đã bao gồm sẵn .NET 10 SDK).
2. **Docker Desktop:** Tải từ [docker.com](https://www.docker.com/products/docker-desktop). Khi cài tick chọn **Use WSL 2 instead of Hyper-V**. Cài xong mở Docker Desktop lên.
3. **Git for Windows:** Tải từ [git-scm.com](https://git-scm.com).

### 4.2 Lấy code và mở dự án
1. Mở PowerShell hoặc Git Bash:
   ```powershell
   git clone <đường-dẫn-repo>
   cd WebMVC.Zalo
   git checkout develop
   ```
2. **Khởi động Database:**
   ```powershell
   Copy-Item .env.example .env
   notepad .env     # Đặt mật khẩu POSTGRES_PASSWORD rồi lưu lại
   docker compose up -d
   ```
3. **Cấu hình chuỗi kết nối (User Secrets):**
   - Click đúp chuột vào file `ZaloClone.sln` để mở Visual Studio.
   - Trong cửa sổ **Solution Explorer**, click chuột phải vào project `ZaloClone` → chọn **Manage User Secrets**.
   - Dán nội dung sau vào file `secrets.json` vừa mở (thay mật khẩu đã đặt ở file `.env`):
     ```json
     {
       "ConnectionStrings": {
         "Default": "Host=localhost;Port=5432;Database=ZaloCloneDb;Username=postgres;Password=<mật-khẩu-trong-.env>"
       }
     }
     ```
   - Nhấn `Ctrl + S` để lưu.
4. **Tạo bảng dữ liệu ban đầu:**
   - Trong Visual Studio, mở menu: **Tools** › **NuGet Package Manager** › **Package Manager Console**.
   - Gõ lệnh:
     ```powershell
     Update-Database
     ```
   - Đợi hiện chữ `Done.` là toàn bộ 10 bảng dữ liệu và trigger đã được tạo thành công!
5. **Chạy ứng dụng:**
   - Nhấn phím **F5** (hoặc nút Start màu xanh trên thanh công cụ Visual Studio).
   - Đăng nhập bằng tài khoản mẫu có sẵn:
     - Admin: `admin@zalo.local` / Mật khẩu: `Test@123`
     - User: `user1@zalo.local` đến `user5@zalo.local` / Mật khẩu: `Test@123`

---

## 5. Quy trình làm việc hàng ngày trong Visual Studio (KHÔNG DÙNG PULL REQUEST)

Để tránh tình trạng chờ đợi duyệt PR, cả nhóm làm việc trực tiếp trên nhánh `develop` theo 4 bước cực kỳ đơn giản bằng chuột:

```
[Sáng: Bấm PULL] ──> [Code trong thư mục của cặp mình] ──> [Bấm F5 Test] ──> [Bấm COMMIT & PUSH]
```

### Bước 1: Lấy code mới nhất (Pull)
- Mỗi khi bắt đầu ngồi vào bàn làm việc: Trong Visual Studio, nhìn xuống góc dưới cùng bên phải (hoặc mở cửa sổ **Git Changes**), bấm vào biểu tượng **mũi tên trỏ xuống (Pull)** để tải toàn bộ code mới nhất của các bạn khác về.

### Bước 2: Code và kiểm tra
- Chỉ tạo và sửa file trong đúng thư mục được phân công của cặp mình (xem lại bảng ở [mục 2](#2-phân-công--vùng-code-ownership)).
- Nhấn **F5** để chạy thử, đảm bảo trang web của mình chạy tốt, không báo lỗi đỏ, không làm gãy tính năng khác.

### Bước 3: Đẩy code lên (Commit & Push)
1. Mở cửa sổ **Git Changes** (nếu không thấy: vào menu **View** › **Git Changes** hoặc nhấn phím tắt `Ctrl + 0, Ctrl + G`).
2. Nhập nội dung commit vào ô mô tả theo mẫu:
   ```
   feat(friend): làm xong chức năng tìm kiếm bạn bè qua số điện thoại
   fix(chat): sửa lỗi không cuộn xuống tin nhắn mới nhất
   ```
3. Bấm vào mũi tên cạnh nút Commit All → chọn **Commit All and Push** (hoặc bấm Commit All rồi bấm nút **mũi tên trỏ lên (Push)**).
4. Code của bạn đã lên nhánh `develop` ngay lập tức!

### Nếu gặp báo lỗi "Push rejected" (do bạn khác vừa push trước mình):
1. Đừng hoảng, chỉ cần bấm nút **Pull** (hoặc **Sync**).
2. Vì các cặp viết trên các file riêng biệt nên Visual Studio sẽ tự động ghép (Auto-merge) thành công mà không có xung đột nào.
3. Bấm **Push** lại là xong.

---

## 6. Quy tắc Database (EF Core Migrations)

- **Tuyệt đối không tự chạy `Add-Migration` trên máy cá nhân.** Nếu 9 người cùng tạo migration, database sẽ bị vỡ hoàn toàn.
- **Ai cần thêm bảng/cột mới:** Nhắn tin cho Lead (Cặp 5 hỗ trợ). Lead sẽ thêm vào `AppDbContext`, tạo migration chuẩn và push lên `develop`.
- **Khi Lead thông báo "Đã có migration mới":**
  1. Bạn chỉ cần bấm **Pull** code về.
  2. Mở **Package Manager Console** trong Visual Studio, gõ:
     ```powershell
     Update-Database
     ```
  3. Xong! Máy bạn đã có cấu trúc DB mới nhất.

---

## 7. SignalR (Real-time) — Bảng tên sự kiện thống nhất

Để cặp Chat (Khôi, Sang), cặp Nhóm (Hải, Đ.Long) và cặp Kết bạn (Tài, Hào) kết nối với nhau không bị lệch tên hàm, tất cả bắt buộc dùng đúng tên sự kiện sau:

| Tên sự kiện | Hướng | Dữ liệu gửi kèm (Payload) | Cặp sử dụng |
|---|---|---|---|
| `SendMessage` | Client → Server | `{ conversationId, content, replyToId? }` | Cặp 3, 4 |
| `Typing` | Client → Server | `{ conversationId }` | Cặp 3, 4 |
| `ReceiveMessage` | Server → Client | Đối tượng tin nhắn vừa gửi | Cặp 3, 4 |
| `UserTyping` | Server → Client | `{ conversationId, userId }` | Cặp 3, 4 |
| `PresenceChanged` | Server → Client | `{ userId, isOnline, lastActiveAt }` | Cặp 3 |
| `FriendRequestReceived` | Server → Client | Đối tượng lời mời kết bạn | Cặp 2 |
| `FriendshipChanged` | Server → Client | `{ userId, action: "accepted"\|"removed"\|"blocked" }` | Cặp 2 |
| `ForceLogout` | Server → Client | `{ reason: "Tài khoản của bạn đã bị khóa" }` | Cặp 1, 4 |

> **Quy tắc gửi ảnh / file:** Upload file bằng form HTTP bình thường (`POST /Chat/UploadFile`) trước để lưu vào server và lấy đường dẫn URL, sau đó mới gửi tin nhắn qua SignalR chứa đường dẫn đó. **Cấm bắn trực tiếp file nhị phân qua SignalR.**

---

## 8. Quy chuẩn lưu trữ File & Ảnh (Avatar, Chat)

- File tải lên được lưu vào thư mục `wwwroot/uploads/{loại}/{yyyy}/{MM}/{tên-guid}.ext`.
- **Dùng chung service đã viết sẵn:** Inject `IFileStorageService` vào Controller/Service của bạn để lưu và xóa file:
  ```csharp
  // Ví dụ lưu avatar:
  string storageKey = await _fileStorageService.SaveFileAsync(avatarFile, "avatars");
  // Lấy đường dẫn hiển thị ra HTML:
  string url = _fileStorageService.GetRelativeUrl(storageKey); // "/uploads/avatars/..."
  ```
- Giới hạn dung lượng:
  - Avatar, ảnh nhóm: tối đa **5 MB** (`.jpg`, `.jpeg`, `.png`, `.webp`)
  - Ảnh gửi trong chat: tối đa **10 MB**
  - File đính kèm: tối đa **50 MB** (`.pdf`, `.docx`, `.xlsx`, `.pptx`, `.zip`, `.txt`)

---

## 9. Quy ước code C# & Bảo mật

1. **Kiểm tra đăng nhập:**
   - Mọi Controller (trừ trang Đăng nhập / Đăng ký) bắt buộc phải có thuộc tính `[Authorize]`.
   - Trang Admin bắt buộc có `[Authorize(Roles = "Admin")]`.
2. **Lấy UserId người đang đăng nhập:**
   - Dùng hàm mở rộng có sẵn: `Guid currentUserId = User.GetUserId();` (không tự đọc claim thủ công).
3. **Bảo mật quyền sở hữu (CỰC KỲ QUAN TRỌNG):**
   - Người dùng chỉ được sửa avatar của chính mình, chỉ được xem tin nhắn của phòng mình tham gia, chỉ được thu hồi tin do chính mình gửi. Luôn kiểm tra `senderId == currentUserId` ở tầng Service.
4. **Chống tấn công CSRF:**
   - Mọi form gửi dữ liệu POST phải có `@Html.AntiForgeryToken()` và trên Action Controller phải có `[ValidateAntiForgeryToken]`.
5. **Định dạng dữ liệu trả về cho AJAX:**
   - Dùng class chuẩn `ApiResponse<T>`:
     ```csharp
     return Json(ApiResponse.Ok("Đã gửi lời mời kết bạn"));
     return Json(ApiResponse.Fail("Người dùng này đã chặn bạn"));
     ```

---

## 10. Giao diện (Frontend)

- Dùng chung khung `Views/Shared/_Layout.cshtml` và thư viện Bootstrap 5 có sẵn.
- **Không cài thêm thư viện CSS nặng khác** (như Tailwind, AntDesign) để tránh xung đột giao diện.
- Đặt tên class CSS phải có tiền tố của module:
  - Cặp 1: `.profile-avatar`, `.account-login-box`
  - Cặp 2: `.friend-list-item`, `.friend-search-input`
  - Cặp 3: `.chat-bubble-left`, `.chat-bubble-right`, `.chat-input-area`
  - Cặp 4: `.group-member-card`, `.admin-stat-widget`

---

## 11. Tiêu chí hoàn thành (Definition of Done)

Một tính năng của cặp bạn chỉ được coi là hoàn thành khi:
- [ ] Bấm **F5** build không có lỗi, không warning.
- [ ] Đã kiểm tra các trường hợp người dùng nhập sai (để trống, nhập chữ vào ô số điện thoại...).
- [ ] Tính năng real-time đã test bằng **2 trình duyệt khác nhau** (ví dụ 1 bên Chrome thường, 1 bên Chrome ẩn danh) đăng nhập 2 tài khoản chat qua lại thấy cập nhật ngay lập tức.
- [ ] Đã bấm **Pull** lấy code mới nhất và **Push** lên nhánh `develop` thành công.

---

## 12. Kế hoạch hàng tuần (Theo đồ án)

- **Tuần 1:** Dựng khung hệ thống, Docker, kết nối PostgreSQL, Seed data (Lead & Cặp 5 đã hoàn tất).
- **Tuần 2:** Cặp 1 hoàn thành Đăng nhập/Đăng ký; Cặp 3 & 4 dựng xong khung giao diện HTML/CSS chat Zalo; Cặp 2 làm danh bạ.
- **Tuần 3 (Quan trọng nhất):** Ghép nối đăng nhập vào Tìm kiếm & Kết bạn (Cặp 2); Hoàn thành chat 1-1 chữ và gửi ảnh/file real-time (Cặp 3).
- **Tuần 4:** Nâng cấp lên Chat nhóm (Cặp 4); Hoàn thiện thu hồi tin nhắn và trang quản trị Admin khóa tài khoản (Cặp 4, Cặp 1).
- **Tuần 5:** Đóng băng code, test chéo giữa các cặp, chụp ảnh màn hình và hoàn thiện báo cáo Word + Slide.

---

## 13. Ghi chú riêng cho Lead (Môi trường macOS)

*(Phần này chỉ dành riêng cho Lead máy Mac, các thành viên Windows có thể bỏ qua)*:
- Cài đặt SDK: Đã cấu hình .NET 10 qua `~/.dotnet/dotnet` và file `global.json`.
- Chạy ứng dụng bằng terminal: `dotnet watch run --project src/ZaloClone`.
- Chạy Docker: `docker compose up -d`.
- Tạo migration mới khi có yêu cầu:
  ```bash
  cd src/ZaloClone
  dotnet ef migrations add <TenMigration>
  git add . && git commit -m "db: <mo ta>" && git push origin develop
  ```
- Khi hết tuần 5 hoặc chuẩn bị demo: Lead kiểm tra nhánh `develop` chạy ổn định rồi merge sang nhánh `main`:
  ```bash
  git checkout main
  git merge develop
  git push origin main
  ```
