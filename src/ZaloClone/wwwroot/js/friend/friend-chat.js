// Task 1 (Tài): nút "Nhắn tin".
// Cách dùng: đặt data-start-chat="<userId>" lên bất kỳ nút nào.
//   - Có thể thêm data-start-chat-url="<url>" để dùng URL tuỳ chỉnh,
//     nếu không thì mặc định POST /Friend/StartChat.
// Trang chỉ cần có @Html.AntiForgeryToken().
// Bấm nút -> POST -> server tìm/tạo DirectConversation -> redirect vào chat 1-1.
(function () {
    'use strict';

    function getToken() {
        const el = document.querySelector('input[name="__RequestVerificationToken"]');
        return el ? el.value : '';
    }

    function showError(text) {
        // Hỗ trợ cả 2 trang: trang Tìm bạn (#friendSearchMessage) và trang Danh sách bạn (#friendActionMessage)
        const box = document.getElementById('friendSearchMessage')
                 || document.getElementById('friendActionMessage');
        if (box) {
            box.textContent = text;
            box.className = box.id === 'friendSearchMessage'
                ? 'friend-search-message is-error'
                : 'alert alert-danger';
        } else {
            alert(text);
        }
    }

    document.addEventListener('click', async function (e) {
        const btn = e.target.closest('[data-start-chat]');
        if (!btn || btn.disabled) return;
        e.preventDefault();

        const friendId = btn.dataset.startChat;
        const url      = btn.dataset.startChatUrl || '/Friend/StartChat';
        const token    = getToken();

        if (!token) {
            showError('Không tìm thấy mã xác thực. Hãy tải lại trang rồi thử lại.');
            return;
        }

        const oldText = btn.textContent;
        btn.disabled  = true;
        btn.textContent = 'Đang mở...';

        try {
            const res = await fetch(url, {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': token,
                    'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8'
                },
                body: new URLSearchParams({ friendId: friendId }).toString()
            });

            if (!res.ok) throw new Error('HTTP ' + res.status);

            const json = await res.json();

            // Hỗ trợ 2 kiểu response từ server:
            //   { success, redirectUrl }   — theo convention cũ
            //   { success, data: { url } } — theo convention mới
            const redirectUrl = json.redirectUrl || (json.data && json.data.url);

            if (!json.success || !redirectUrl) {
                showError(json.message || 'Không thể mở cuộc trò chuyện.');
                btn.disabled    = false;
                btn.textContent = oldText;
                return;
            }

            window.location.href = redirectUrl;
        } catch (err) {
            showError('Có lỗi xảy ra, vui lòng thử lại.');
            btn.disabled    = false;
            btn.textContent = oldText;
        }
    });
})();
