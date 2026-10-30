## 📝 Làm gì
<!-- Mô tả ngắn thay đổi. Liên kết Issue: Closes #123 -->

Closes #

## 🧩 Module
- [ ] account / profile (Cặp 1)
- [ ] friend (Cặp 2)
- [ ] chat (Cặp 3)
- [ ] group / admin (Cặp 4)
- [ ] core / db / hub (Lead, Cặp 5)

## 🧪 Cách test
<!-- Các bước để reviewer tự kiểm tra lại. Với real-time: dùng 2 trình duyệt, 2 tài khoản -->
1.
2.

## 📸 Ảnh chụp màn hình (nếu có giao diện)

## ✅ Checklist (Definition of Done — CONVENTIONS.md mục 13)
- [ ] Đã rebase code mới nhất từ `origin/develop`, không còn conflict
- [ ] `dotnet build` sạch, không có warning mới
- [ ] Chỉ sửa file trong vùng của cặp mình (hoặc đã báo chủ file)
- [ ] KHÔNG sửa Entity / Migration / `AppDbContext` (trừ Lead)
- [ ] Controller có `[Authorize]`, form POST có AntiForgery
- [ ] Đã kiểm tra quyền sở hữu dữ liệu (không tin id từ client)
- [ ] Đã test trường hợp lỗi (input rỗng, không có quyền, không tồn tại)
- [ ] Không commit `.env`, mật khẩu, `bin/`, `obj/`, `wwwroot/uploads/`
