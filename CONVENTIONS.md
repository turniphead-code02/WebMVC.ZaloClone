# 📘 QUY ƯỚC LÀM VIỆC NHÓM — ZALO CLONE (ASP.NET Core MVC)

> **Mọi thành viên bắt buộc đọc hết file này trước khi viết dòng code đầu tiên.**
> Muốn thay đổi quy ước → tạo PR sửa file này, Lead duyệt.

---

## 0. TL;DR — 10 điều phải nhớ

1. **Không ai tự sửa Database / Entity / Migration** ngoài Lead (Cặp 5 hỗ trợ). Cần thêm cột → tạo Issue nhãn `db-change`.
2. **Không push thẳng lên `main` hoặc `develop`.** Mọi thay đổi đi qua Pull Request, cần ít nhất 1 người duyệt.
3. **Chỉ code trong vùng của cặp mình** (xem [mục 2](#2-phân-công--vùng-code-ownership)). File dùng chung → báo Lead.
4. **Không commit mật khẩu, connection string, file `.env`, thư mục `bin/`, `obj/`, `wwwroot/uploads/`.**
5. Đặt tên: **C# PascalCase**, **DB snake_case**, **JS camelCase**, **CSS kebab-case có tiền tố module**.
6. Controller **mỏng** — logic nghiệp vụ nằm trong `Services/`.
7. Không truyền Entity ra View → dùng **ViewModel**.
8. Sự kiện SignalR **chỉ dùng tên đã khai báo** trong [mục 8](#8-signalr-real-time). Muốn thêm → báo Lead.
9. Thời gian lưu **UTC** (`DateTimeOffset.UtcNow`), chỉ đổi sang giờ VN khi hiển thị.
10. Pull `develop` mỗi sáng trước khi code, rebase trước khi tạo PR.

---

## 1. Tech Stack & Phiên bản (chốt cứng)

| Thành phần | Phiên bản | Ghi chú |
|---|---|---|
| .NET SDK | **10.0 (LTS)** | Chốt bằng `global.json` ở thư mục gốc |
| ASP.NET Core MVC | 10.x | Razor Views |
| SignalR | có sẵn trong ASP.NET Core | Client: `@microsoft/signalr` |
| EF Core + Npgsql | `Npgsql.EntityFrameworkCore.PostgreSQL` 10.x | Kèm `EFCore.NamingConventions` (snake_case) |
| PostgreSQL | **16** (Docker) | Schema yêu cầu tối thiểu PG15 |
| Hệ điều hành | **Windows 10/11 (chính)**, macOS | Lệnh trong tài liệu viết cho **PowerShell** trước, macOS ghi chú kèm |
| IDE | **Visual Studio 2026 Community** (Windows) · Rider / VS Code + C# Dev Kit (macOS) | VS 2022 **không** hỗ trợ .NET 10 → phải dùng VS 2026 |
| Docker | Docker Desktop (Windows dùng backend **WSL 2**) | |

> Ai cài sai phiên bản SDK → không build được → tự chịu. Kiểm tra: `dotnet --version`.

---

## 2. Phân công & Vùng code (Ownership)

| Cặp | Thành viên | Mảng | Được sửa |
|---|---|---|---|
| **Lead + Cặp 5** | Lead, PLong, QLong | Khung hệ thống, Docker, DB, SignalR gốc | `Program.cs`, `Data/`, `Models/Entities/`, `Migrations/`, `Hubs/`, `Views/Shared/_Layout.cshtml`, `docker-compose.yml` |
| **Cặp 1** | Huy, Tùng | Tài khoản & Hồ sơ | `AccountController`, `ProfileController`, `Views/Account/`, `Views/Profile/`, `Services/Account/` |
| **Cặp 2** | Tài, Hào | Danh bạ & Kết bạn | `FriendController`, `Views/Friend/`, `Services/Friend/` |
| **Cặp 3** | Khôi, Sang | Chat 1-1, File/Ảnh | `ChatController`, `Views/Chat/`, `Services/Chat/`, `wwwroot/js/chat/` |
| **Cặp 4** | Hải, Đ.Long | Chat nhóm & Admin | `GroupController`, `AdminController`, `Views/Group/`, `Views/Admin/`, `Services/Group/`, `Services/Admin/` |

**File dùng chung** (`_Layout.cshtml`, `site.css`, `site.js`, `Program.cs`, `AppDbContext.cs`, `ChatHub.cs`): chỉ Lead/Cặp 5 merge. Cặp khác cần sửa → mô tả trong Issue hoặc tạo PR nhỏ riêng chỉ chứa thay đổi đó.

> Gợi ý: tạo file `.github/CODEOWNERS` để GitHub tự gán reviewer theo thư mục.

---

## 3. Cấu trúc thư mục project

### 3.1 Cấu trúc repo
```
WebMVC.Zalo/                       # Thư mục gốc repo
├── .github/
│   ├── workflows/build.yml        # CI: tự build mỗi khi tạo PR
│   ├── ISSUE_TEMPLATE/            # Mẫu: db-change, bug, feature
│   ├── pull_request_template.md   # Checklist bắt buộc khi tạo PR
│   └── CODEOWNERS                 # Tự gán reviewer theo thư mục
├── docs/
│   ├── screenshots/<module>/      # Ảnh chụp cho báo cáo
│   └── Dự Án Web MVC Zalo Clone.pdf
├── src/
│   └── ZaloClone/                 # Project ASP.NET Core MVC (cấu trúc ở 3.2)
├── ZaloClone.slnx
├── docker-compose.yml
├── .env.example                   # Mẫu biến môi trường (KHÔNG chứa mật khẩu thật)
├── .editorconfig                  # Quy tắc format & đặt tên C# (IDE tự áp dụng)
├── .gitattributes                 # Chuẩn hóa xuống dòng (Windows CRLF ↔ repo LF)
├── .gitignore
├── global.json                    # Chốt phiên bản .NET SDK
├── schema.sql                     # Bản thiết kế DB tham chiếu
├── README.md
└── CONVENTIONS.md
```

### 3.2 Cấu trúc project `src/ZaloClone/`
```
ZaloClone/
├── Controllers/            # Mỗi cặp 1-2 controller, KHÔNG viết logic DB ở đây
├── Services/               # Logic nghiệp vụ, chia theo module
│   ├── Account/            #   IAccountService.cs + AccountService.cs
│   ├── Friend/
│   ├── Chat/
│   ├── Group/
│   └── Admin/
├── Hubs/                   # ChatHub.cs (Lead quản lý)
├── Data/                   # AppDbContext.cs, cấu hình Fluent API, SeedData.cs
├── Models/
│   ├── Entities/           # Ánh xạ bảng DB (Lead quản lý)
│   └── ViewModels/         # Theo module: ViewModels/Chat/ChatRoomViewModel.cs
├── Views/
│   ├── Shared/             # _Layout, partial dùng chung
│   └── <Module>/
├── wwwroot/
│   ├── css/<module>.css
│   ├── js/<module>/*.js
│   └── uploads/            # (gitignore) file người dùng tải lên
├── Infrastructure/         # Helper dùng chung: ClaimsPrincipalExtensions (GetUserId), UserPair, ApiResponse
├── Migrations/             # (Lead quản lý)
├── appsettings.json        # KHÔNG chứa mật khẩu
└── Program.cs
```

---

## 4. Môi trường local

> Mọi lệnh dưới đây chạy trong **PowerShell** (Windows Terminal) hoặc **Developer PowerShell** trong Visual Studio, mở tại thư mục gốc repo. Người dùng macOS chạy y hệt trong Terminal, chỗ nào khác sẽ ghi chú 🍎.

### 4.0 Cài đặt lần đầu (Windows)
| # | Công cụ | Cách cài | Kiểm tra |
|---|---|---|---|
| 1 | **WSL 2** | PowerShell (Run as Administrator): `wsl --install` → khởi động lại máy | `wsl --status` |
| 2 | **Docker Desktop** | https://www.docker.com/products/docker-desktop — khi cài chọn *Use WSL 2* | `docker compose version` |
| 3 | **Visual Studio 2026 Community** | https://visualstudio.microsoft.com — tick workload **ASP.NET and web development** (đã kèm .NET 10 SDK) | `dotnet --version` → `10.0.x` |
| 4 | **Git for Windows** | https://git-scm.com — giữ mặc định *Checkout Windows-style, commit Unix-style* | `git --version` |
| 5 | **EF Core CLI** | `dotnet tool install --global dotnet-ef` | `dotnet ef --version` |

Hoặc cài nhanh Docker, Git, .NET SDK bằng `winget` (vẫn cần cài Visual Studio từ trang chủ):
```powershell
winget install Docker.DockerDesktop Git.Git Microsoft.DotNet.SDK.10
```

Cấu hình Git **một lần** sau khi cài:
```powershell
git config --global user.name "Ho Ten"
git config --global user.email "email@github.com"
git config --global core.autocrlf true      # 🍎 macOS dùng: input
git config --global core.longpaths true     # Tránh lỗi đường dẫn dài trên Windows
git config --global pull.rebase true
```

> 🍎 macOS: `brew install --cask dotnet-sdk docker` và dùng Rider hoặc VS Code + C# Dev Kit (Visual Studio for Mac đã bị khai tử).
>
> .NET 6/7 đã hết hỗ trợ, .NET 8 hết hỗ trợ 11/2026 → nhóm dùng **.NET 10 LTS** (hỗ trợ đến 11/2028).

### 4.1 Chạy Database
```powershell
Copy-Item .env.example .env                # lần đầu, mở .env bằng Notepad/VS để điền mật khẩu   🍎 cp .env.example .env
docker compose up -d                       # khởi động PostgreSQL (Docker Desktop phải đang mở)
docker compose ps                          # container zalo_clone_db phải ở trạng thái "healthy"
docker compose --profile tools up -d       # (tùy chọn) thêm pgAdmin tại http://localhost:5050
```
> Kết nối pgAdmin tới DB: Host = `postgres-db`, Port = `5432`, user/mật khẩu như trong `.env`.

### 4.2 Connection string — dùng User Secrets, KHÔNG ghi vào `appsettings.json`
**Cách 1 — Visual Studio:** chuột phải project `ZaloClone` → **Manage User Secrets** → dán:
```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=ZaloCloneDb;Username=postgres;Password=<mật-khẩu-trong-.env>"
  }
}
```
**Cách 2 — dòng lệnh** (Windows & macOS giống nhau):
```powershell
cd src/ZaloClone
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=ZaloCloneDb;Username=postgres;Password=<mật-khẩu>"
```

### 4.3 Tạo bảng
| Visual Studio — Package Manager Console | Dòng lệnh (PowerShell / 🍎 Terminal) |
|---|---|
| `Update-Database` | `dotnet ef database update --project src/ZaloClone` |

Reset DB sạch (PowerShell — chạy từng lệnh):
```powershell
docker compose down -v
docker compose up -d
dotnet ef database update --project src/ZaloClone
```

### 4.4 Lưu ý khi nhóm dùng lẫn Windows & macOS
| Vấn đề | Quy tắc |
|---|---|
| **Xuống dòng CRLF/LF** | Đã xử lý tự động bằng `.gitattributes`. Không tự ý "Convert line endings" cả file. Nếu PR hiện **toàn bộ file bị đổi** → báo Lead |
| **Hoa/thường trong tên file** | Windows không phân biệt `chat.js` và `Chat.js`, nhưng Docker/Linux/CI **có**. Đổi tên chỉ khác hoa/thường phải dùng `git mv old New`. Đường dẫn trong code phải **khớp chính xác** hoa/thường |
| **Dấu phân cách đường dẫn** | Đường dẫn file trên ổ đĩa: dùng `Path.Combine(...)`, không viết cứng `\` hay `/`. Đường dẫn lưu DB (`storage_key`) và URL: **luôn dùng `/`** |
| **Tên file/thư mục** | Không dấu tiếng Việt, không khoảng trắng: `ChatRoom.cshtml` ✅, `Phòng Chat.cshtml` ❌ |
| **Port 5432 bị chiếm** | Máy Windows đã cài PostgreSQL bản cài đặt → đặt `POSTGRES_PORT=5433` trong `.env` và sửa `Port=5433` trong connection string |
| **Encoding** | Lưu file **UTF-8** (VS 2026 mặc định). Không dùng Windows-1258 — tiếng Việt sẽ bị lỗi font |
| **Script** | Không commit file `.bat`/`.sh` chỉ chạy được trên một hệ điều hành; ưu tiên lệnh `dotnet`/`docker` dùng được ở cả hai |

---

## 5. Quy tắc Database (QUAN TRỌNG NHẤT)

### 5.1 Ai được làm gì
- **Chỉ Lead** tạo migration. Thành viên khác **chỉ** cập nhật DB:
  | Việc | Visual Studio (Package Manager Console) | Dòng lệnh |
  |---|---|---|
  | Cập nhật DB (mọi người) | `Update-Database` | `dotnet ef database update --project src/ZaloClone` |
  | Tạo migration (**chỉ Lead**) | `Add-Migration <TenMigration>` | `dotnet ef migrations add <TenMigration> --project src/ZaloClone` |
- Tên migration: PascalCase, mô tả thay đổi — `InitialCreate`, `AddEditedAtToMessages`.
- Cần thay đổi DB → tạo Issue nhãn `db-change` theo mẫu: *Bảng / Cột / Kiểu / Lý do / Ai cần*.
- Lead gom thay đổi, tạo migration, merge vào `develop`, nhắn nhóm pull về.

### 5.2 Nguồn chuẩn (Source of Truth)
- **EF Core Migrations là nguồn chuẩn duy nhất** để tạo DB.
- `schema.sql` là **bản thiết kế tham chiếu**. Những thứ EF không tự sinh được (trigger, function `uuid_generate_v7`, CHECK phức tạp, partial index) được đưa vào migration bằng `migrationBuilder.Sql(...)`.
- Khi schema thay đổi, Lead cập nhật cả `schema.sql` để tài liệu luôn khớp.

### 5.3 Đặt tên
| Đối tượng | Quy ước | Ví dụ |
|---|---|---|
| Bảng | snake_case, số nhiều | `friend_requests` |
| Cột | snake_case | `last_message_at` |
| Khóa chính | `id` (UUID) | |
| Khóa ngoại | `<bảng_số_ít>_id` | `conversation_id` |
| Boolean | `is_` / `has_` | `is_deleted` |
| Thời gian | `_at`, kiểu `timestamptz` | `created_at` |
| Index / Unique / Check | `idx_` / `uq_` / `chk_` | `idx_messages_pagination` |
| Entity C# | PascalCase, số ít | `FriendRequest.LastMessageAt` |

→ Bật `options.UseNpgsql(...).UseSnakeCaseNamingConvention()` để C# PascalCase tự map sang snake_case.

### 5.4 Quy tắc dữ liệu
- **Xóa mềm** user (`is_deleted`, `deleted_at`) — không bao giờ `DELETE` user thật.
- Email lưu **chữ thường + đã trim**; Số điện thoại lưu đã trim.
- Cặp người dùng trong `friendships` / `direct_conversations`: luôn **ID nhỏ hơn là `user_id_1`**. Dùng helper chung `UserPair.Order(a, b)` — không tự viết lại.
- Không lưu file nhị phân vào DB — chỉ lưu đường dẫn (xem [mục 9](#9-upload-file--ảnh)).
- Truy vấn danh sách **phải phân trang** (tin nhắn: cursor theo `created_at, id`; danh sách khác: tối đa 50 bản ghi/trang).
- Dùng `AsNoTracking()` cho truy vấn chỉ đọc.

---

## 6. Quy trình Git

### 6.1 Nhánh
```
main        ← bản ổn định để demo/nộp (chỉ Lead merge từ develop)
develop     ← nhánh tích hợp hằng ngày
feature/<module>-<mô-tả-ngắn>     vd: feature/friend-send-request
fix/<module>-<mô-tả-ngắn>         vd: fix/chat-recall-not-realtime
db/<mô-tả>                         (chỉ Lead)
```

### 6.2 Commit message (Conventional Commits)
```
<type>(<module>): <mô tả ngắn bằng tiếng Việt, viết thường, không dấu chấm cuối>

type: feat | fix | refactor | style | docs | chore | db
module: account | profile | friend | chat | group | admin | core
```
Ví dụ: `feat(friend): gửi và hủy lời mời kết bạn`, `fix(chat): thu hồi tin không cập nhật bên người nhận`.

### 6.3 Pull Request
- 1 PR = 1 tính năng/1 lỗi, **dưới ~400 dòng thay đổi**.
- Trước khi tạo PR (PowerShell / 🍎 Terminal — chạy từng dòng):
  ```powershell
  git fetch origin
  git rebase origin/develop
  dotnet build      # phải build sạch, không warning mới
  ```
  Dùng giao diện Visual Studio: cửa sổ **Git Changes** → **Fetch** → menu **Git › Manage Branches** → chuột phải `origin/develop` → **Rebase 'nhánh-của-bạn' onto 'origin/develop'**.
- Mô tả PR gồm: *Làm gì · Cách test · Ảnh chụp màn hình (nếu có giao diện)*.
- Reviewer: **người cùng cặp + 1 người cặp khác** (tuần 5 bắt buộc review chéo).
- Merge kiểu **Squash and merge**; xóa nhánh sau khi merge.
- Gặp conflict ở file không thuộc vùng của mình → **dừng lại, hỏi chủ file**, không tự chọn "Accept mine".

---

## 7. Quy ước code C#

### 7.1 Đặt tên
| Loại | Quy ước | Ví dụ |
|---|---|---|
| Class, Method, Property | PascalCase | `FriendService.SendRequestAsync` |
| Interface | `I` + PascalCase | `IFriendService` |
| Biến cục bộ, tham số | camelCase | `receiverId` |
| Field private | `_camelCase` | `_dbContext` |
| Method bất đồng bộ | hậu tố `Async` | `GetInboxAsync` |
| ViewModel | hậu tố `ViewModel` | `ProfileEditViewModel` |

### 7.2 Kiến trúc
```
Controller  →  Service (interface)  →  AppDbContext
   │                 │
   └─ ViewModel ←────┘   (Service trả về ViewModel/DTO, KHÔNG trả Entity cho View)
```
- Đăng ký Service bằng DI trong `Program.cs`: `builder.Services.AddScoped<IFriendService, FriendService>();`
- Controller chỉ: nhận request → validate → gọi service → trả View/JSON.
- Dùng `async/await` cho mọi thao tác DB/IO. **Không** dùng `.Result` hay `.Wait()`.
- Lấy UserId hiện tại qua helper chung `User.GetUserId()` (extension method do Lead viết), không tự parse claim.

### 7.3 Bảo mật bắt buộc
- Mọi controller (trừ Đăng nhập/Đăng ký) gắn `[Authorize]`; trang Admin gắn `[Authorize(Roles = "Admin")]`.
- Mọi form POST có `@Html.AntiForgeryToken()` và action có `[ValidateAntiForgeryToken]`.
- **Luôn kiểm tra quyền sở hữu**: user chỉ được đọc/gửi tin trong hội thoại mình là thành viên, chỉ thu hồi tin của chính mình… Không tin `id` gửi lên từ client.
- Mật khẩu băm bằng `PasswordHasher<User>` của ASP.NET Core, không tự viết thuật toán băm.
- Không dùng `@Html.Raw()` với nội dung người dùng nhập (chống XSS).
- Tài khoản bị Admin khóa (`is_locked`) → phải bị đăng xuất ngay: kiểm tra trong cookie validation **và** gửi sự kiện SignalR `ForceLogout`.

### 7.4 Validation & lỗi
- Validate bằng DataAnnotations trên ViewModel (`[Required]`, `[EmailAddress]`, `[StringLength]`) + kiểm tra `ModelState.IsValid`.
- Thông báo lỗi hiển thị cho người dùng bằng **tiếng Việt**.
- Không `catch` rồi nuốt lỗi. Ghi log bằng `ILogger<T>`, không dùng `Console.WriteLine`.
- API trả JSON theo format thống nhất:
  ```json
  { "success": true, "data": { ... }, "message": null }
  { "success": false, "data": null, "message": "Bạn đã gửi lời mời cho người này rồi" }
  ```

---

## 8. SignalR (Real-time)

- **Chỉ có 1 Hub:** `Hubs/ChatHub.cs`, endpoint `/hubs/chat`, do Lead/Cặp 5 quản lý.
- Mỗi hội thoại là 1 SignalR Group tên `conv:{conversationId}`; mỗi user có group riêng `user:{userId}`.
- Khi user mở kết nối → server tự add vào tất cả group hội thoại đang tham gia.
- Server **luôn lưu DB trước, phát sự kiện sau**.

### 8.1 Danh sách sự kiện (contract chung)
| Hướng | Tên sự kiện | Payload | Dùng bởi |
|---|---|---|---|
| Client → Server | `SendMessage` | `{ conversationId, content, replyToId? }` | Cặp 3, 4 |
| Client → Server | `MarkAsRead` | `{ conversationId, messageId }` | Cặp 3, 4 |
| Client → Server | `Typing` | `{ conversationId }` | Cặp 3, 4 |
| Server → Client | `ReceiveMessage` | `MessageDto` | Cặp 3, 4 |
| Server → Client | `MessageRecalled` | `{ conversationId, messageId }` | Cặp 3 |
| Server → Client | `MessageRead` | `{ conversationId, userId, messageId }` | Cặp 3 |
| Server → Client | `UserTyping` | `{ conversationId, userId }` | Cặp 3, 4 |
| Server → Client | `PresenceChanged` | `{ userId, isOnline, lastActiveAt }` | Cặp 3 |
| Server → Client | `ConversationUpdated` | `{ conversationId, title?, avatarUrl? }` | Cặp 4 |
| Server → Client | `MemberChanged` | `{ conversationId, userId, action: "added"\|"removed"\|"left" }` | Cặp 4 |
| Server → Client | `FriendRequestReceived` | `FriendRequestDto` | Cặp 2 |
| Server → Client | `FriendshipChanged` | `{ userId, action: "accepted"\|"removed"\|"blocked" }` | Cặp 2 |
| Server → Client | `ForceLogout` | `{ reason }` | Cặp 1, 4 |

> Gửi file/ảnh: upload qua HTTP (`POST /Chat/Upload`) trước → nhận về `attachmentId` → mới gửi tin qua `SendMessage`. **Không gửi file qua SignalR.**

---

## 9. Upload file & ảnh

- Lưu tại `wwwroot/uploads/{loại}/{yyyy}/{MM}/{guid}{.ext}` — `loại` ∈ `avatars | messages | groups`.
- **Đổi tên file thành GUID**, giữ tên gốc trong cột `file_name` (chống trùng & chống path traversal).
- DB chỉ lưu **đường dẫn tương đối** (cột `storage_key`), vd: `uploads/messages/2026/10/3f2a...png`. URL đầy đủ build ở tầng app → sau này đổi sang Cloudinary chỉ sửa 1 chỗ.
- Dùng chung 1 service `IFileStorageService` (Lead viết), không mỗi cặp tự viết code lưu file.
- Giới hạn:
  | Loại | Đuôi cho phép | Tối đa |
  |---|---|---|
  | Avatar / ảnh nhóm | `.jpg .jpeg .png .webp` | 5 MB |
  | Ảnh trong chat | `.jpg .jpeg .png .gif .webp` | 10 MB |
  | File đính kèm | `.pdf .docx .xlsx .pptx .zip .txt` | 50 MB |
- Kiểm tra cả đuôi file **và** MIME type ở server, không chỉ dựa vào client.

---

## 10. Frontend (Razor View, CSS, JS)

- Dùng chung `_Layout.cshtml` và Bootstrap 5 (đã cài sẵn trong template). **Không cài thêm framework CSS khác.**
- CSS: mỗi module 1 file `wwwroot/css/<module>.css`, class có **tiền tố module** để không đè nhau:
  `.chat-message`, `.friend-card`, `.group-header`, `.admin-table`.
- JS: mỗi module 1 thư mục `wwwroot/js/<module>/`. **Không viết JS inline** trong `.cshtml` (trừ khởi tạo biến từ server).
- Kết nối SignalR dùng chung 1 file `wwwroot/js/core/signalr-connection.js` (Lead viết) — các module chỉ `connection.on(...)` sự kiện của mình.
- Partial view đặt tên bắt đầu bằng `_`: `_MessageItem.cshtml`, `_FriendCard.cshtml`.
- Màu sắc, font dùng biến CSS chung trong `site.css` (`--zalo-primary: #0068ff;`…), không hard-code mã màu.

---

## 11. Thời gian & Ngôn ngữ

- Lưu DB: `timestamptz`, C# dùng `DateTimeOffset.UtcNow`.
- Hiển thị: đổi sang `Asia/Ho_Chi_Minh` (UTC+7), định dạng `HH:mm` (hôm nay), `dd/MM` (năm nay), `dd/MM/yyyy` (năm khác).
- Giao diện & thông báo lỗi: **tiếng Việt**. Code, tên biến, tên bảng: **tiếng Anh**. Comment: tiếng Việt được phép.

---

## 12. Dữ liệu mẫu (Seed Data)

- Lead cung cấp `Data/SeedData.cs`, chạy tự động khi môi trường `Development`.
- Tài khoản mẫu thống nhất (mật khẩu chung `Test@123`):
  | Email | Vai trò |
  |---|---|
  | `admin@zalo.local` | Admin |
  | `user1@zalo.local` … `user10@zalo.local` | User |
- Cặp nào cần thêm dữ liệu mẫu → gửi Lead, không tự insert tay vào DB rồi code dựa trên dữ liệu đó.

---

## 13. Definition of Done — Khi nào được coi là "xong"

Một tính năng chỉ được báo **Done** khi:
- [ ] Build không lỗi, không warning mới.
- [ ] Chạy được trên máy **người khác** (không chỉ máy mình) — ít nhất **1 máy Windows**, DB tạo từ migration sạch.
- [ ] Đã test cả trường hợp lỗi (input rỗng, không có quyền, dữ liệu không tồn tại).
- [ ] Tính năng real-time đã test bằng **2 trình duyệt** (1 thường + 1 ẩn danh) với 2 tài khoản.
- [ ] Có `[Authorize]`, AntiForgery, kiểm tra quyền sở hữu.
- [ ] PR đã được duyệt và merge vào `develop`.
- [ ] Cập nhật trạng thái task trên bảng công việc (GitHub Projects/Trello).

---

## 14. Giao tiếp & Báo cáo

- **Kênh chính:** nhóm chat chung; vấn đề kỹ thuật cụ thể → tạo **GitHub Issue** (để tra lại được).
- **Daily (10 phút / tối):** mỗi người trả lời 3 câu: *Hôm qua làm gì? Hôm nay làm gì? Đang bị kẹt gì?*
- **Bị kẹt quá 2 giờ → phải hỏi**, không ngồi im.
- Nhãn Issue: `bug`, `feature`, `db-change`, `question`, `blocked`, `module:<tên>`.
- Mỗi người tự lưu ảnh chụp màn hình tính năng mình làm trong thư mục `docs/screenshots/<module>/` → phục vụ báo cáo tuần 5.

---

## 15. Các quyết định kỹ thuật đã chốt (ADR)

| # | Vấn đề | Quyết định | Lý do |
|---|---|---|---|
| 1 | Tạo DB bằng gì? | **EF Core Code-First Migrations** là nguồn chuẩn. Trigger, function, CHECK phức tạp, partial index trong `schema.sql` được nhúng bằng `migrationBuilder.Sql(...)` | Đúng kế hoạch PDF; ai cũng chỉ cần `dotnet ef database update` |
| 2 | Xác thực | **Cookie Authentication tự cấu hình** + `PasswordHasher<User>`. Không dùng ASP.NET Core Identity | Identity sinh thêm 7 bảng `AspNet*`, không khớp bảng `users`; đồ án cần hiểu và giải thích được luồng đăng nhập |
| 3 | Khóa tài khoản đá văng ngay | Cột `users.security_stamp` lưu trong claim; `OnValidatePrincipal` so khớp stamp + `is_locked` (cache ngắn 30s); đồng thời gửi SignalR `ForceLogout`. Khóa tài khoản / đổi mật khẩu → sinh stamp mới | Đáp ứng yêu cầu tuần 4 trong PDF |
| 3b | Quên mật khẩu | Token tạo bằng `IDataProtector.ToTimeLimitedDataProtector()` chứa `userId + security_stamp`, hết hạn 15 phút. Không cần bảng token | Token tự mất hiệu lực sau khi đổi mật khẩu (stamp đổi) |
| 4 | Lưu file | **Local** `wwwroot/uploads` qua `IFileStorageService` | Không phụ thuộc dịch vụ ngoài khi demo; đổi sang Cloudinary chỉ cần viết lớp mới |
| 5 | ID | `UUID`. Riêng `messages` dùng UUIDv7 (tăng dần theo thời gian) | Không lộ số lượng bản ghi; phân trang theo thời gian hiệu quả |
| 6 | Commit message | `type(module)` tiếng Anh + mô tả **tiếng Việt** | Dễ đọc với cả nhóm, vẫn theo chuẩn Conventional Commits |
| 7 | Phiên bản | .NET 10 LTS, PostgreSQL 16 | Còn được hỗ trợ dài hạn trong suốt thời gian làm đồ án |
| 8 | Môi trường chuẩn | **Windows + Visual Studio 2026 + PowerShell**; macOS được hỗ trợ nhưng là phụ | Đa số thành viên dùng Windows; tài liệu, lệnh, hướng dẫn ưu tiên Windows |

> Muốn thay đổi quyết định nào → mở Issue nhãn `question`, Lead chốt rồi cập nhật bảng này.

