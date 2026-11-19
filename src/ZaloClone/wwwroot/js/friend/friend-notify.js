// Task 2 (Cặp 2 - Tài): badge thông báo trên icon "Bạn bè" ở Cột 1 + toast real-time qua SignalR.
// Nghe 2 sự kiện đúng tên trong CONVENTIONS mục 7:
//   - FriendRequestReceived : có người gửi lời mời kết bạn cho mình
//   - FriendshipChanged     : { userId, action: "accepted" | "removed" | "blocked" }
(function () {
    'use strict';

    const userId = document.body.dataset.userId || '';
    const friendBtn = document.querySelector('.mini-sidebar a[href="/Friend/Search"]');
    if (!friendBtn) return;

    // Nạp CSS riêng của module (class có tiền tố friend-)
    const css = document.createElement('link');
    css.rel = 'stylesheet';
    css.href = '/css/friend/friend-notify.css';
    document.head.appendChild(css);

    // Badge gắn vào icon
    const badge = document.createElement('span');
    badge.className = 'friend-nav-badge';
    badge.hidden = true;
    friendBtn.appendChild(badge);

    // ---- Trạng thái ----
    // pending: số lời mời đến đang chờ (lấy từ server, luôn chính xác)
    // unseen : số lần "được chấp nhận kết bạn" chưa xem (lưu tạm ở trình duyệt, xóa khi bấm vào icon)
    const storageKey = 'zalo_friend_unseen_accepted_' + userId;
    let pending = 0;

    function readUnseen() {
        try { return parseInt(localStorage.getItem(storageKey) || '0', 10) || 0; } catch (e) { return 0; }
    }
    function writeUnseen(n) {
        try { localStorage.setItem(storageKey, String(n)); } catch (e) { /* bỏ qua */ }
    }

    function render() {
        const total = pending + readUnseen();
        badge.hidden = total <= 0;
        badge.textContent = total > 99 ? '99+' : String(total);
    }

    async function refreshPending() {
        try {
            const res = await fetch('/Friend/PendingCount', { credentials: 'same-origin' });
            if (!res.ok) return;
            const json = await res.json();
            if (json.success) {
                pending = json.data || 0;
                render();
            }
        } catch (e) { /* bỏ qua, badge giữ nguyên */ }
    }

    // Bấm vào icon Bạn bè = đã xem thông báo
    friendBtn.addEventListener('click', function () { writeUnseen(0); });

    // ---- Toast ----
    function showToast(title, text) {
        let host = document.getElementById('friendToastHost');
        if (!host) {
            host = document.createElement('div');
            host.id = 'friendToastHost';
            host.className = 'friend-toast-host';
            document.body.appendChild(host);
        }

        const toast = document.createElement('div');
        toast.className = 'friend-toast';

        const t = document.createElement('div');
        t.className = 'friend-toast-title';
        t.textContent = title;

        const b = document.createElement('div');
        b.className = 'friend-toast-body';
        b.textContent = text;

        toast.appendChild(t);
        toast.appendChild(b);
        host.appendChild(toast);

        setTimeout(function () { toast.remove(); }, 6000);
    }

    render();
    refreshPending();

    if (typeof signalR === 'undefined') return; // trang không nạp thư viện SignalR -> chỉ hiện số từ server

    const connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/chat')
        .withAutomaticReconnect([0, 2000, 5000, 10000])
        .build();

    connection.on('FriendRequestReceived', function (p) {
        const name = p && p.senderName ? p.senderName : 'Một người dùng';
        showToast('Lời mời kết bạn', name + ' đã gửi cho bạn lời mời kết bạn.');
        refreshPending();
    });

    connection.on('FriendshipChanged', function (p) {
        if (!p) return;
        if (p.action === 'accepted') {
            const name = p.displayName ? p.displayName : 'Người dùng';
            writeUnseen(readUnseen() + 1);
            render();
            showToast('Đã là bạn bè', name + ' đã chấp nhận lời mời kết bạn của bạn.');
        } else {
            refreshPending();
        }
    });

    connection.onreconnected(function () { refreshPending(); });

    connection.start().catch(function (err) {
        console.error('Friend notify: lỗi kết nối SignalR', err);
    });
})();
