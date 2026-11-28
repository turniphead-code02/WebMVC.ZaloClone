# Theo dõi 18 tiêu chí chức năng

> Cặp 5 cập nhật. Lần đối chiếu gần nhất: **28/11/2026**, code `develop` tại `4f85c09`.
> Ai làm xong một mục thì sửa cột **Trạng thái** và ghi commit vào cột **Ghi chú**, rồi báo Cặp 5 kiểm tra lại.

**Ký hiệu:** ✅ Đạt đủ · ⚠️ Làm một phần · ❌ Chưa có

## Tổng quan

| Trạng thái | Số mục |
|---|---|
| ✅ Đạt đủ | 8 |
| ⚠️ Làm một phần | 4 |
| ❌ Chưa có | 6 |

## Bảng chi tiết

| # | Tiêu chí | Trạng thái | Phụ trách | Còn thiếu / Ghi chú |
|---|---|---|---|---|
| 1 | Thêm người dùng (đăng ký, gửi mail password) | ❌ | Huy, Tùng | Người dùng đang tự đặt mật khẩu khi đăng ký. Cần: form đăng ký bỏ ô mật khẩu → hệ thống sinh mật khẩu (mục 3) → gửi mail. Code SMTP đã có trong `Accountservice.cs` (dùng cho OTP) nhưng `SmtpHost` trong `appsettings.json` đang trống |
| 2 | Password ≥ 8 ký tự, có chữ hoa, chữ thường, số, ký tự đặc biệt | ⚠️ | Huy, Tùng | Mọi chỗ đặt mật khẩu chỉ yêu cầu 6 ký tự (`[MinLength(6)]` ở Register/Reset/ChangePassword, `minlength="6"` ở `Views/Profile/Index.cshtml`). Cần thêm kiểm tra độ phức tạp ở cả server và form |
| 3 | Password gửi mail do hệ thống sinh ngẫu nhiên, đúng chính sách mục 2 | ❌ | Huy, Tùng | Chưa có hàm sinh mật khẩu |
| 4 | Đăng nhập (bắt đổi password ở lần đăng nhập đầu) | ❌ | Huy, Tùng + **Lead** | Cần thêm cột kiểu `MustChangePassword` vào bảng `users` (**Lead tạo migration**), sau đó chặn mọi trang và chuyển về Đổi mật khẩu cho tới khi đổi xong |
| 5 | Liệt kê danh sách người dùng | ✅ | Đ.Long | Trang Admin có tìm kiếm, lọc, phân trang. Hỏi lại thầy xem có cần danh sách cho user thường không |
| 6 | Cập nhật profile (hình đại diện, password) | ✅ | Tùng, Huy | Đổi/xóa avatar (≤ 5 MB), đổi mật khẩu có kiểm tra mật khẩu cũ và đăng xuất phiên khác. Ngày sinh đang lưu ra file JSON, không vào DB |
| 7 | Hiển thị trạng thái online theo thời gian thực | ✅ | Khôi | `PresenceChanged` qua SignalR. Trạng thái lưu trong RAM nên chỉ đúng khi chạy 1 server |
| 8 | Hiển thị tin nhắn cá nhân, phân trang bằng thanh cuộn | ❌ | Khôi, Sang | API `/Chat/Messages?skip&take` đã có nhưng `chat.js` chưa gọi, chưa bắt sự kiện cuộn. Hiện chỉ xem được 50 tin mới nhất. Cần: cuộn lên đầu → tải thêm tin cũ, giữ nguyên vị trí đang xem |
| 9 | Hiển thị trạng thái đã xem / chưa xem | ⚠️ | Khôi | Đã có badge tin chưa đọc cho người nhận. **Người gửi chưa thấy "Đã xem".** Cần: `MarkAsRead` báo lại cho người gửi qua SignalR và hiện "Đã xem" dưới tin cuối |
| 10 | Gửi file cá nhân | ✅ | Sang | Tải file qua `/Chat/File/{id}` có kiểm tra thành viên. Lỗi nhỏ: sau F5, file được ghép với tin nhắn theo **tên file**, nên 2 file trùng tên có thể mở nhầm |
| 11 | Tạo group (ít nhất 3 thành viên) | ⚠️ | Hải | Chưa kiểm tra số lượng: tạo nhóm chỉ có một mình vẫn được. Cần chặn ở `GroupService.CreateGroupAsync` (người tạo + ít nhất 2 người) và ở form tạo nhóm |
| 12 | Hiển thị danh sách group mà thành viên tham gia | ✅ | Hải | Nằm chung trong danh sách hội thoại Cột 2. Nhóm mới tạo chưa tự hiện cho thành viên khác (sự kiện `GroupCreated` chưa có JS lắng nghe), phải F5 |
| 13 | Hiển thị tin nhắn group, phân trang bằng thanh cuộn | ❌ | Khôi, Sang | Giống mục 8, làm chung một lần là xong cả hai |
| 14 | Gửi file vào group | ✅ | Sang | Dùng chung luồng với mục 10 |
| 15 | Xóa thành viên group (chỉ owner) | ✅ | Hải | Server chặn nếu người thao tác không phải `Admin` của nhóm, nút chỉ hiện cho trưởng nhóm. Người bị xóa vẫn nhận tin real-time tới khi F5 |
| 16 | Thêm thành viên mới vào group (chỉ owner) | ⚠️ | Hải | **Thành viên nào cũng thêm được.** `GroupService.AddMembersAsync` chỉ kiểm tra là thành viên, chưa kiểm tra `Role == "Admin"`. Nút "Thêm" cũng hiện cho mọi người |
| 17 | Giải tán group (chỉ owner) | ❌ | Hải | Chưa có API và nút nào. Trưởng nhóm bấm "Rời nhóm" thì nhóm không còn ai quản lý |
| 18 | Ghim tin nhắn, hiển thị ở đầu cửa sổ chat | ✅ | Sang | Ghim/bỏ ghim real-time, có banner trên đầu. Lỗi nhỏ: tin ghim cũ hơn 50 tin mới nhất bị mất banner sau F5 |

## Thứ tự nên làm

1. **Mục 16, 11** (Hải): thêm vài dòng kiểm tra trong `GroupService`, nhanh nhất.
2. **Mục 17** (Hải): API giải tán nhóm + nút trong Cột 4.
3. **Mục 8, 13** (Khôi, Sang): phân trang khi cuộn, một lần làm xong cả hai.
4. **Mục 9** (Khôi): báo "Đã xem" cho người gửi.
5. **Mục 2 → 3 → 1 → 4** (Huy, Tùng, Lead): làm theo đúng thứ tự này vì mục sau cần mục trước. Mục 4 cần Lead tạo migration.

## Kiểm tra tự động

Một số mục có test trong `tests/ZaloClone.Tests`, chạy tự động trên CI. Chạy thử trên máy (cần bật Docker Desktop):

```powershell
dotnet test ZaloClone.sln
```

Bảng test case chi tiết xem ở [`TestCases.md`](TestCases.md).
