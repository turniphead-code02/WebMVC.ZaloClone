# 💬 Zalo Clone — ASP.NET Core MVC

Đồ án web chat real-time mô phỏng Zalo: kết bạn, chat 1-1, chat nhóm, gửi file/ảnh, trang quản trị.

**Công nghệ:** .NET 10 · ASP.NET Core MVC · SignalR · EF Core · PostgreSQL 16 · Docker

> 📘 **Trước khi code, bắt buộc đọc [CONVENTIONS.md](CONVENTIONS.md).**

---

## 🚀 Chạy dự án lần đầu (Windows)

**Yêu cầu:** WSL 2, Docker Desktop, Visual Studio 2026 (workload *ASP.NET and web development*), Git for Windows.
Chi tiết cài đặt: [CONVENTIONS.md — mục 4.0](CONVENTIONS.md#40-cài-đặt-lần-đầu-windows).

Mở **PowerShell** (hoặc Windows Terminal) và chạy **từng dòng**:

```powershell
# 1. Lấy code
git clone <repo-url>
cd WebMVC.Zalo
git checkout develop

# 2. Khởi động PostgreSQL (mở Docker Desktop trước)
Copy-Item .env.example .env
notepad .env                    # đặt POSTGRES_PASSWORD rồi lưu lại
docker compose up -d

# 3. Cấu hình connection string (chỉ lưu trên máy bạn)
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=ZaloCloneDb;Username=postgres;Password=<mật-khẩu-trong-.env>" --project src/ZaloClone

# 4. Tạo bảng
dotnet tool install --global dotnet-ef
dotnet ef database update --project src/ZaloClone
```

**5. Chạy web:** mở `ZaloClone.slnx` bằng Visual Studio → nhấn **F5**.
(Hoặc dòng lệnh: `dotnet watch run --project src/ZaloClone`)

Đăng nhập bằng tài khoản mẫu: `admin@zalo.local` / `Test@123` hoặc `user1@zalo.local` / `Test@123`.

> 🍎 **macOS:** các lệnh giống hệt, chỉ thay `Copy-Item` → `cp`, `notepad` → `open -e`. Chạy web bằng Rider/VS Code hoặc `dotnet watch run --project src/ZaloClone`.

## 🛠️ Lệnh hay dùng

| Việc | Lệnh |
|---|---|
| Bật / tắt DB | `docker compose up -d` / `docker compose down` |
| Mở pgAdmin (http://localhost:5050) | `docker compose --profile tools up -d` |
| Xem log DB | `docker compose logs -f postgres-db` |
| Cập nhật DB theo migration mới | VS Package Manager Console: `Update-Database` · hoặc `dotnet ef database update --project src/ZaloClone` |
| Reset sạch DB | `docker compose down -v` → `docker compose up -d` → `dotnet ef database update --project src/ZaloClone` |
| Lấy code mới nhất | `git pull origin develop` (đã cấu hình `pull.rebase true`) |

## 👥 Phân công

| Cặp | Thành viên | Mảng |
|---|---|---|
| Lead + Cặp 5 | Lead, PLong, QLong | Docker, Database, SignalR gốc |
| Cặp 1 | Huy, Tùng | Tài khoản & Trang cá nhân |
| Cặp 2 | Tài, Hào | Danh bạ & Kết bạn |
| Cặp 3 | Khôi, Sang | Chat 1-1 & Gửi file/ảnh |
| Cặp 4 | Hải, Đ.Long | Chat nhóm & Trang Admin |

Chi tiết kế hoạch: [docs/](docs/) · Thiết kế DB: [schema.sql](schema.sql)

## ❓ Lỗi thường gặp

| Lỗi | Cách xử lý |
|---|---|
| `required variable POSTGRES_PASSWORD is missing` | Chưa tạo file `.env` → `Copy-Item .env.example .env` |
| `error during connect ... docker_engine` / `cannot find the file specified` | **Docker Desktop chưa mở.** Mở Docker Desktop, đợi biểu tượng cá voi chuyển xanh |
| `WSL 2 installation is incomplete` / `Virtualization not enabled` | Chạy `wsl --install` (Admin) rồi khởi động lại; nếu vẫn lỗi → bật **Virtualization (VT-x/AMD-V)** trong BIOS |
| `port 5432 is already allocated` | Máy đã cài PostgreSQL bản Windows → đặt `POSTGRES_PORT=5433` trong `.env` **và** `Port=5433` trong connection string (hoặc tắt service `postgresql-x64-*` trong *Services*) |
| `A compatible .NET SDK was not found` / VS báo không hỗ trợ `net10.0` | Cài .NET SDK 10 và dùng **Visual Studio 2026** (VS 2022 không hỗ trợ .NET 10) |
| `dotnet-ef : The term 'dotnet-ef' is not recognized` | Chạy `dotnet tool install --global dotnet-ef` rồi **mở lại PowerShell** |
| `running scripts is disabled on this system` | PowerShell (Admin): `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned` |
| `password authentication failed` | Mật khẩu trong user-secrets khác `.env`; nếu đã đổi `.env` sau khi tạo DB → `docker compose down -v` rồi chạy lại |
| `relation "users" does not exist` | Chưa chạy `Update-Database` / `dotnet ef database update` |
| `Filename too long` khi clone | `git config --global core.longpaths true` |
| PR hiện **toàn bộ file bị thay đổi** | Lỗi xuống dòng CRLF/LF → không commit, báo Lead (xem CONVENTIONS mục 4.4) |
