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

Code liên quan: `Controllers/AccountController.cs`, `Services/Account/Accountservice.cs`, `Program.cs` (cookie `OnValidatePrincipal`).

| ID | Tình huống | Tiền điều kiện | Các bước | Mong đợi | Kết quả | Issue |
|---|---|---|---|---|---|---|
| ACC-01 | Đăng ký hợp lệ | Chưa đăng nhập | 1. Vào `/Account/Register` 2. Nhập email mới, tên 2–100 ký tự, mật khẩu ≥ 6 ký tự, xác nhận khớp 3. Bấm Đăng ký | Tự đăng nhập, về trang chủ, hiện "Đăng ký tài khoản thành công!" | ⬜ Chưa test | |
| ACC-02 | Đăng ký trùng email | Đã có `user1@zalo.local` | Đăng ký với `USER1@zalo.local` (khác hoa/thường) | Báo lỗi email đã tồn tại, không tạo tài khoản | ⬜ Chưa test | |
| ACC-03 | Đăng ký trùng số điện thoại | Có user mang SĐT X | Đăng ký với SĐT X viết dạng `+84…` | Báo lỗi SĐT đã tồn tại | ⬜ Chưa test | |
| ACC-04 | Validate form đăng ký | — | Lần lượt: bỏ trống email; email sai định dạng; tên 1 ký tự; mật khẩu 5 ký tự; xác nhận không khớp | Mỗi trường báo đúng thông báo tiếng Việt, không gửi form | ⬜ Chưa test | |
| ACC-05 | Đăng nhập bằng email | Có tài khoản | Đăng nhập `user1@zalo.local` / `Test@123` | Vào màn hình chat | ✅ Pass | |
| ACC-06 | Đăng nhập bằng SĐT | Tài khoản có SĐT | Đăng nhập bằng SĐT (thử cả dạng `09…` và `+849…`) | Đăng nhập thành công cả hai dạng | ⬜ Chưa test | |
| ACC-07 | Sai mật khẩu / tài khoản không tồn tại | — | Đăng nhập sai mật khẩu; rồi email không tồn tại | Cùng một thông báo "Tài khoản hoặc mật khẩu không chính xác." | ✅ Pass |  |
| ACC-08 | Tài khoản bị khóa đăng nhập | Admin đã khóa B | B đăng nhập đúng mật khẩu | Báo "Tài khoản của bạn đã bị khóa…", không vào được | ⬜ Chưa test | |
| ACC-09 | Tài khoản bị khóa / xóa mềm khi đang online | B đang mở trang chat | Admin khóa (hoặc xóa mềm) B → B tải lại trang hoặc gửi tin | B bị đẩy về trang Đăng nhập | ⬜ Chưa test | |
| ACC-10 | Ghi nhớ đăng nhập | — | Đăng nhập có tick "Ghi nhớ" → đóng hẳn trình duyệt → mở lại | Vẫn đăng nhập. Không tick thì phải đăng nhập lại | ⬜ Chưa test | |
| ACC-11 | ReturnUrl an toàn | Chưa đăng nhập | Mở `/Account/Login?ReturnUrl=https://google.com` rồi đăng nhập | Về `/`, không chuyển ra trang ngoài | ✅ Pass |  |
| ACC-12 | Đăng xuất | Đã đăng nhập | Bấm Đăng xuất → bấm Back của trình duyệt → F5 | Về trang Đăng nhập, hiện "Bạn đã đăng xuất thành công.", không xem lại được trang chat | ⬜ Chưa test | |
| ACC-13 | Trang yêu cầu đăng nhập | Chưa đăng nhập | Mở trực tiếp `/`, `/Profile`, `/Admin/Admin` | Chuyển về `/Account/Login?ReturnUrl=…` | ✅ Pass |  |
| ACC-14 | User thường vào trang Admin | Đăng nhập user thường | Mở `/Admin/Admin` | Trang "Từ chối truy cập" (`/Account/AccessDenied`) | ✅ Pass |  |
| ACC-15 | Quên mật khẩu → nhận OTP | — | `/Account/ForgotPassword` → nhập email đã đăng ký | Chuyển sang trang Đặt lại mật khẩu, hiện thông báo kèm OTP 6 số (chế độ test đồ án) | ⬜ Chưa test | |
| ACC-16 | Quên mật khẩu với tài khoản không tồn tại | — | Nhập email chưa đăng ký | Báo "Không tìm thấy tài khoản…" | ⬜ Chưa test | |
| ACC-17 | Đặt lại mật khẩu bằng OTP đúng | Đã làm ACC-15 | Nhập OTP đúng + mật khẩu mới hợp lệ | Về trang Đăng nhập; mật khẩu cũ không còn dùng được, mật khẩu mới đăng nhập được | ✅ Pass |  |
| ACC-18 | OTP sai / hết hạn / dùng lại | Đã làm ACC-15 | a) Nhập sai OTP b) Chờ > 10 phút c) Dùng lại OTP vừa dùng thành công | Cả 3 trường hợp báo "Mã OTP không chính xác hoặc đã hết hạn." | ⬜ Chưa test | |
| ACC-19 | Giới hạn số lần nhập sai OTP | Đã làm ACC-15 | Nhập sai OTP liên tục 10 lần | Bị chặn hoặc OTP bị hủy sau vài lần sai. **Code hiện không giới hạn**, có thể dò hết 900.000 mã trong 10 phút | ❌ Fail | #3 |
| ACC-20 | Đặt lại mật khẩu đăng xuất phiên cũ | A đang đăng nhập ở trình duyệt 1 | Ở trình duyệt 2 làm Quên mật khẩu → đặt lại mật khẩu → quay lại trình duyệt 1, F5 | Trình duyệt 1 bị đăng xuất (SecurityStamp đổi) | ⬜ Chưa test | |
| ACC-21 | QuickLogin chỉ dùng khi phát triển | Chưa đăng nhập | Mở `/Account/Login`, bấm vào một tài khoản mẫu (hoặc mở `/Account/QuickLogin?userId=00000000-0000-0000-0000-000000000001`) | Chỉ hoạt động ở môi trường Development; bản demo/nộp bài không có. **Hiện tại** trang Login luôn hiện danh sách tài khoản mẫu và vào thẳng được, kể cả Admin, không cần mật khẩu, ở mọi môi trường | ❌ Fail | #2 |


### 2. Profile & Avatar (PRO) — Tùng

Code liên quan: `Controllers/ProfileController.cs`, `Services/Account/Accountservice.cs`, `Services/Storage/LocalFileStorageService.cs` (avatar: `.jpg .jpeg .png .webp`, tối đa 5 MB).
Phần hiện avatar ở 4 cột (commit `ef63864`) mới nằm ở nhánh `tung/avatar-4-cot`, **chưa merge vào `develop`**, nên PRO-08 đang để ⏸.

| ID | Tình huống | Tiền điều kiện | Các bước | Mong đợi | Kết quả | Issue |
|---|---|---|---|---|---|---|
| PRO-01 | Xem trang cá nhân | Đã đăng nhập | Vào `/Profile` | Hiện đúng tên, email, SĐT, tiểu sử, avatar | ⬜ Chưa test | |
| PRO-02 | Sửa thông tin hợp lệ | Đã đăng nhập | Đổi tên hiển thị, SĐT, tiểu sử → Lưu | Báo thành công; tên mới hiện ngay ở thanh trên và Cột 1 mà không cần đăng nhập lại | ⬜ Chưa test | |
| PRO-03 | Validate sửa thông tin | Đã đăng nhập | Lần lượt: tên trống; tên 1 ký tự; SĐT > 20 ký tự; tiểu sử > 255 ký tự | Báo lỗi đúng trường, dữ liệu cũ giữ nguyên | ⬜ Chưa test | |
| PRO-04 | SĐT trùng người khác | B đã có SĐT X | A đổi SĐT thành X | Báo SĐT đã được sử dụng | ⬜ Chưa test | |
| PRO-05 | Đổi avatar hợp lệ | Đã đăng nhập | Tải ảnh `.png` < 5 MB | Ảnh mới hiện ở trang Profile; `/Profile/VerifyAvatarAndAuth` trả `avatarExistsOnDisk: true` | ⬜ Chưa test | |
| PRO-06 | Avatar sai định dạng | Đã đăng nhập | Tải `.gif`, `.pdf`, `.exe` đổi đuôi | Báo định dạng không hỗ trợ, avatar cũ giữ nguyên | ⬜ Chưa test | |
| PRO-07 | Avatar quá 5 MB | Đã đăng nhập | Tải ảnh `.jpg` 6 MB | Báo vượt giới hạn, avatar cũ giữ nguyên | ⬜ Chưa test | |
| PRO-08 | Avatar đồng bộ 4 cột | A, B là bạn, có hội thoại chung và nhóm chung | 1. A đổi avatar 2. A quay lại màn hình chat 3. B tải lại trang | Ảnh mới hiện ở Cột 1, danh sách hội thoại Cột 2, bong bóng chat Cột 3, danh sách thành viên Cột 4 (cả phía A và B) | ⏸ Chờ tính năng | |
| PRO-09 | Người chưa có avatar | Tài khoản mới đăng ký | Mở màn hình chat | Hiện chữ cái đầu của tên, không hiện ảnh vỡ | ⬜ Chưa test | |
| PRO-10 | Đổi mật khẩu hợp lệ | Đã đăng nhập | `/Profile/ChangePassword` → nhập đúng mật khẩu hiện tại + mật khẩu mới ≥ 6 ký tự | Báo thành công, **vẫn đăng nhập** ở trình duyệt hiện tại; lần sau chỉ đăng nhập được bằng mật khẩu mới | ⬜ Chưa test | |
| PRO-11 | Đổi mật khẩu sai mật khẩu hiện tại | Đã đăng nhập | Nhập sai mật khẩu hiện tại | Báo lỗi ở ô mật khẩu hiện tại, mật khẩu không đổi | ⬜ Chưa test | |
| PRO-12 | Validate đổi mật khẩu | Đã đăng nhập | Mật khẩu mới 5 ký tự; xác nhận không khớp | Báo lỗi đúng trường | ⬜ Chưa test | |
| PRO-13 | SecurityStamp: đăng xuất trình duyệt khác | A đăng nhập ở Chrome và Edge | Đổi mật khẩu ở Chrome → sang Edge bấm vào trang bất kỳ hoặc F5 | Edge bị đẩy về trang Đăng nhập; Chrome vẫn dùng bình thường | ⬜ Chưa test | |
| PRO-14 | SecurityStamp với kết nối real-time | Như PRO-13, Edge đang mở khung chat | Đổi mật khẩu ở Chrome → **không F5 Edge**, nhắn tin cho A từ tài khoản khác | Edge không còn nhận tin real-time (kết nối SignalR cũ bị ngắt). Ghi lại kết quả thực tế | ⬜ Chưa test | |


### 3. Chat 1-1 (CHAT) — Khôi, Sang

Code liên quan: `Controllers/HomeController.cs`, `Controllers/ChatController.cs`, `Services/Chat/ChatService.cs`, `Hubs/ChatHub.cs`, `wwwroot/js/chat/chat.js`.
Giao diện gửi tin qua hub `SendMessage`; khi mất kết nối thì gọi `POST /Chat/SendMessage`. `HomeController.SendMessage` hiện không được giao diện gọi tới.

**Chức năng cơ bản (đã có)**

| ID | Tình huống | Tiền điều kiện | Các bước | Mong đợi | Kết quả | Issue |
|---|---|---|---|---|---|---|
| CHAT-01 | Gửi tin văn bản real-time | A, B là bạn, có hội thoại 1-1, mở 2 trình duyệt | A gửi "xin chào" | B thấy tin ngay, không cần F5; tin đúng thứ tự, đúng giờ Việt Nam | ⬜ Chưa test | |
| CHAT-02 | Tin rỗng / chỉ khoảng trắng | Như CHAT-01 | Bấm gửi khi ô trống hoặc chỉ có dấu cách | Không gửi gì | ⬜ Chưa test | |
| CHAT-03 | Emoji và ký tự đặc biệt | Như CHAT-01 | Gửi emoji, tiếng Việt có dấu, `<b>test</b>`, `<script>alert(1)</script>` | Hiện đúng nguyên văn, không thực thi HTML/JS (cả lúc nhận real-time và sau F5) | ⬜ Chưa test | |
| CHAT-04 | Tin nhắn rất dài | Như CHAT-01 | Dán đoạn văn 10.000 ký tự rồi gửi | Bị chặn hoặc giới hạn độ dài hợp lý. **Code hiện không giới hạn độ dài** | ⬜ Chưa test | |
| CHAT-05 | Hội thoại hơn 50 tin | Hội thoại có > 50 tin | Gửi thêm tin mới → F5 / chuyển sang hội thoại khác rồi quay lại | Hiện các tin **mới nhất**. `GetConversationData` đang `OrderBy(CreatedAt).Take(50)` nên nghi chỉ lấy 50 tin **cũ nhất** | ❌ Fail | #6 |
| CHAT-06 | Gửi ảnh | Như CHAT-01 | Gửi `.jpg`/`.png`/`.gif`/`.webp` < 10 MB | Cả hai bên thấy ảnh ngay; F5 vẫn hiện ảnh | ⬜ Chưa test | |
| CHAT-07 | Gửi file | Như CHAT-01 | Gửi `.pdf`, `.docx`, `.xlsx`, `.zip`, `.txt` | Hiện tên + dung lượng; bấm tải về được | ⬜ Chưa test | |
| CHAT-08 | Link file sau khi F5 | Đã làm CHAT-06/07 | F5 → bấm ảnh/file trong khung chat và ở panel tệp đã gửi (Cột 4) | Mở/tải được. **Nghi lỗi:** `chat.js:178` và `:445` ghép `/uploads/${storageKey}` mà `storageKey` đã bắt đầu bằng `uploads/` → link `/uploads/uploads/…` bị 404 | ⬜ Chưa test | |
| CHAT-09 | Sai định dạng file | Như CHAT-01 | Gửi `.exe`, `.bat`, `.mp4` | Báo định dạng không hỗ trợ, không tạo tin | ⬜ Chưa test | |
| CHAT-10 | Vượt dung lượng | Như CHAT-01 | Gửi ảnh 11 MB; file 35 MB; file 51 MB | Báo vượt giới hạn bằng thông báo tiếng Việt. **Nghi lỗi:** không cấu hình `RequestSizeLimit`, Kestrel mặc định chặn ~28,6 MB nên file 30–50 MB có thể báo lỗi 413 dù quy định cho phép 50 MB | ⬜ Chưa test | |
| CHAT-11 | Thu hồi tin của mình | Như CHAT-01 | A thu hồi tin vừa gửi | Cả hai bên thấy "Tin nhắn đã được thu hồi" ngay | ⬜ Chưa test | |
| CHAT-12 | Tin đã thu hồi sau khi F5 | Đã làm CHAT-11 | Cả A và B F5, chuyển hội thoại rồi quay lại, xem dòng xem trước ở Cột 2 | Vẫn hiện "đã thu hồi" | ✅ Pass (test tự động, sau `3f472b9`; dòng xem trước Cột 2 cần test tay) | #5 |
| CHAT-13 | Không thu hồi được tin người khác | Như CHAT-01 | B gọi thu hồi tin của A (qua DevTools: `POST /Chat/RecallMessage` với `messageId` của A) | Bị từ chối | ✅ Pass |  |
| CHAT-14 | Thu hồi tin có file | Đã gửi file | Thu hồi tin chứa file → mở lại link file cũ | Link file không còn tải được. Code hiện giữ nguyên file trên ổ đĩa; ghi lại kết quả | ⬜ Chưa test | |

**Bảo mật**

| ID | Tình huống | Tiền điều kiện | Các bước | Mong đợi | Kết quả | Issue |
|---|---|---|---|---|---|---|
| CHAT-15 | Xem hội thoại không phải của mình | C không thuộc hội thoại A–B, biết `conversationId` | C đăng nhập, mở `/Home/GetConversationData?conversationId=<id A–B>` và `/?conversationId=<id A–B>` | Bị từ chối (403/404). **Nghi lỗi:** `GetConversationData` không kiểm tra thành viên nên trả về toàn bộ tin nhắn | ❌ Fail | #4 |
| CHAT-16 | Gửi tin vào hội thoại không phải của mình | Như CHAT-15 | C gọi `POST /Chat/SendMessage` với `conversationId` của A–B | Bị từ chối, A và B không nhận được gì | ✅ Pass |  |
| CHAT-17 | File chat truy cập công khai | Đã gửi file | Copy link file → mở bằng tab ẩn danh (chưa đăng nhập) | Ghi lại kết quả. File đang nằm trong `wwwroot/uploads` nên ai có link cũng tải được | ⬜ Chưa test | |
| CHAT-18 | Chặn sau khi đã có hội thoại | A, B có hội thoại 1-1 | A chặn B → B gửi tin vào hội thoại cũ | B không gửi được. **Nghi lỗi:** chỉ `StartChat` kiểm tra chặn, các đường gửi tin không kiểm tra | ⏸ Chờ tính năng | |

**Tính năng Sprint 2 (Khôi, Sang đang làm)**

| ID | Tình huống | Tiền điều kiện | Các bước | Mong đợi | Kết quả | Issue |
|---|---|---|---|---|---|---|
| CHAT-19 | Online / Offline thật | A, B là bạn | B đăng nhập → B đóng tab | Chấm xanh của B ở phía A bật/tắt theo. Hiện `HomeController` gán cứng `IsOnline = true` | ⏸ Chờ tính năng | |
| CHAT-20 | "Đang soạn tin…" | Như CHAT-01 | A gõ phím, không gửi | B thấy "Đang soạn tin…" ở Cột 3, tự tắt khi A ngừng gõ vài giây | ⏸ Chờ tính năng | |
| CHAT-21 | Badge tin chưa đọc | A đang ở hội thoại khác | B gửi 3 tin cho A | Cột 2 phía A hiện badge "3"; A mở hội thoại thì badge mất; F5 không hiện lại | ⏸ Chờ tính năng | |
| CHAT-22 | Trả lời tin nhắn | Như CHAT-01 | A chọn "Trả lời" một tin của B → gửi | Tin mới hiện kèm trích dẫn tin gốc ở cả 2 phía; tin gốc bị thu hồi thì trích dẫn hiện "đã thu hồi" | ⏸ Chờ tính năng | |
| CHAT-23 | Ghim tin nhắn | Như CHAT-01 | A ghim 1 tin → bỏ ghim | Tin ghim hiện trên đầu khung chat ở cả 2 phía; bỏ ghim thì mất | ⏸ Chờ tính năng | |
| CHAT-24 | Hội thoại mới nhận tin real-time | A, B vừa kết bạn, chưa có hội thoại | A bấm "Nhắn tin" (tạo hội thoại mới) → gửi tin, B **không F5** | B nhận được tin ngay. **Nghi lỗi:** hub chỉ cho vào nhóm `conv:{id}` lúc kết nối, hội thoại tạo sau chưa nhận được | ⏸ Chờ tính năng | |


### 4. Hạ tầng (INF) — Lead, Cặp 5

Code liên quan: `.github/workflows/build.yml`, `docker-compose.yml`, `global.json`, `Program.cs`, `Migrations/`, `Data/SeedData.cs`.

| ID | Tình huống | Tiền điều kiện | Các bước | Mong đợi | Kết quả | Issue |
|---|---|---|---|---|---|---|
| INF-01 | Build sạch như CI | Code mới nhất `develop` | `dotnet build ZaloClone.sln -c Release -warnaserror` | 0 warning, 0 error | ✅ Pass | |
| INF-02 | CI GitHub Actions | — | Push lên `develop`, mở tab Actions | Workflow `Build` xanh (restore, build, test) | ✅ Pass | |
| INF-03 | Dựng DB từ đầu | Docker Desktop chạy, có `.env` | `docker compose down -v` → `docker compose up -d` → `dotnet ef database update --project src/ZaloClone` | Container `zalo_clone_db` healthy, áp dụng hết migration không lỗi | ✅ Pass | |
| INF-04 | Seed dữ liệu mẫu | DB vừa tạo, môi trường Development | Chạy web lần đầu | Có `admin@zalo.local`, `user1@zalo.local`… đăng nhập được bằng `Test@123`, có sẵn hội thoại mẫu | ✅ Pass | |
| INF-05 | Seed không nhân đôi | Đã chạy web ít nhất 1 lần | Tắt web và chạy lại 2–3 lần | Không bị lỗi trùng khóa, không nhân đôi user/hội thoại | ⬜ Chưa test | |
| INF-06 | Thiếu chuỗi kết nối | Xóa User Secrets | Chạy web | Báo lỗi kết nối DB rõ ràng. Hiện `Program.cs` tự dùng mật khẩu mặc định `YourSecurePassword2026` ghi cứng trong code, nên ghi lại thông báo lỗi thực tế | ⬜ Chưa test | |
| INF-07 | Chạy profile https | — | `dotnet run --project src/ZaloClone --launch-profile https` | Mở được `https://localhost:7177`, không còn cảnh báo `Failed to determine the https port` | ⬜ Chưa test | |
| INF-08 | Hai endpoint SignalR | Đã đăng nhập | Mở DevTools → Network → WS | Client kết nối `/hubs/chat` (hoặc `/chathub`) thành công; ghi lại client đang dùng endpoint nào. `Program.cs` map cùng `ChatHub` vào cả 2 đường | ⬜ Chưa test | |
| INF-09 | Không commit bí mật | — | `git ls-files` tìm `.env`, `uploads/`, `appsettings.*.json` có mật khẩu | Không có file bí mật nào được commit | ⬜ Chưa test | |
| INF-10 | Clone mới trên máy khác | Máy chưa có repo | Làm đúng theo `README.md` mục cài đặt | Chạy được web, không cần bước ngoài README. Thiếu bước nào thì ghi lại (ví dụ: README chưa nhắc cài `dotnet-ef` khi không dùng Visual Studio) | ⬜ Chưa test | |


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
