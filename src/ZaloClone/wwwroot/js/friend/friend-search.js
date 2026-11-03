(function () {
    'use strict';

    const urls = {
        find: '/Friend/FindUser',
        send: '/Friend/SendRequest',
        cancel: '/Friend/CancelRequest'
    };

    const input = document.getElementById('friendSearchInput');
    const searchBtn = document.getElementById('friendSearchBtn');
    const messageBox = document.getElementById('friendSearchMessage');
    const resultBox = document.getElementById('friendSearchResult');
    const sentList = document.getElementById('friendSentList');
    const sentCount = document.getElementById('friendSentCount');
    const sentEmpty = document.getElementById('friendSentEmpty');
    const token = document.querySelector('input[name="__RequestVerificationToken"]').value;

    let currentUser = null; 

    function esc(s) {
        const d = document.createElement('div');
        d.textContent = s == null ? '' : String(s);
        return d.innerHTML;
    }

    function avatarHtml(name, url) {
        if (url) return '<img class="friend-avatar" src="' + esc(url) + '" alt="" />';
        const letter = name ? name.trim().charAt(0).toUpperCase() : '?';
        return '<div class="friend-avatar friend-avatar-fallback">' + esc(letter) + '</div>';
    }

    function showMessage(text, isError) {
        messageBox.textContent = text || '';
        messageBox.className = 'friend-search-message' + (text ? (isError ? ' is-error' : ' is-ok') : '');
    }

    async function post(url, data) {
        const res = await fetch(url, {
            method: 'POST',
            headers: {
                'RequestVerificationToken': token,
                'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8'
            },
            body: new URLSearchParams(data).toString()
        });
        if (!res.ok) throw new Error('HTTP ' + res.status);
        return res.json();
    }

    function updateSentCount() {
        const n = sentList.querySelectorAll('.friend-list-item').length;
        sentCount.textContent = n;
        sentEmpty.hidden = n > 0;
    }

    // Hiển thị kết quả tìm kiếm
    function renderResult(u) {
        let action = '';
        switch (u.relationStatus) {
            case 'None':
                action = '<button type="button" class="btn btn-primary btn-sm" data-act="send">Kết bạn</button>';
                break;
            case 'RequestSent':
                action = '<button type="button" class="btn btn-outline-secondary btn-sm" data-act="cancel">Hủy lời mời</button>';
                break;
            case 'RequestReceived':
                action = '<span class="friend-status-tag">Người này đã gửi lời mời cho bạn</span>';
                break;
            case 'Friend':
                action = '<span class="friend-status-tag">Đã là bạn bè</span>';
                break;
            case 'Blocked':
                action = '<span class="friend-status-tag">Bạn đã chặn người này</span>';
                break;
        }

        resultBox.innerHTML =
            '<div class="friend-list-item friend-result-card">' +
            avatarHtml(u.displayName, u.avatarUrl) +
            '<div class="friend-list-info">' +
            '<div class="friend-list-name">' + esc(u.displayName) + '</div>' +
            '<div class="friend-list-sub">' + esc(u.bio || '') + '</div>' +
            '</div>' + action + '</div>';
    }

    //Tìm kiếm
    async function search() {
        const keyword = input.value.trim();
        resultBox.innerHTML = '';
        currentUser = null;

        if (!keyword) {
            showMessage('Vui lòng nhập số điện thoại hoặc email.', true);
            return;
        }

        searchBtn.disabled = true;
        showMessage('Đang tìm...', false);
        try {
            const res = await fetch(urls.find + '?keyword=' + encodeURIComponent(keyword));
            if (!res.ok) throw new Error('HTTP ' + res.status);
            const json = await res.json();
            if (!json.success) {
                showMessage(json.message, true);
                return;
            }
            showMessage('', false);
            currentUser = json.data;
            renderResult(currentUser);
        } catch (e) {
            showMessage('Có lỗi xảy ra, vui lòng thử lại.', true);
        } finally {
            searchBtn.disabled = false;
        }
    }

    //Gửi lời mời
    async function sendRequest(btn) {
        btn.disabled = true;
        try {
            const json = await post(urls.send, { receiverId: currentUser.userId });
            if (!json.success) {
                showMessage(json.message, true);
                btn.disabled = false;
                return;
            }
            showMessage(json.message, false);

            const r = json.data;
            currentUser.relationStatus = 'RequestSent';
            currentUser.requestId = r.requestId;
            renderResult(currentUser);
            addSentItem(r);
        } catch (e) {
            showMessage('Có lỗi xảy ra, vui lòng thử lại.', true);
            btn.disabled = false;
        }
    }

    function addSentItem(r) {
        const time = new Date(r.createdAt).toLocaleString('vi-VN');
        const div = document.createElement('div');
        div.className = 'friend-list-item';
        div.dataset.requestId = r.requestId;
        div.innerHTML =
            avatarHtml(r.receiverName, r.receiverAvatarUrl) +
            '<div class="friend-list-info">' +
            '<div class="friend-list-name">' + esc(r.receiverName) + '</div>' +
            '<div class="friend-list-sub">Đã gửi lúc ' + esc(time) + '</div></div>' +
            '<button type="button" class="btn btn-outline-secondary btn-sm friend-cancel-btn" data-request-id="' +
            esc(r.requestId) + '">Hủy lời mời</button>';
        sentList.prepend(div);
        updateSentCount();
    }

    //Hủy lời mời
    async function cancelRequest(requestId, btn) {
        if (btn) btn.disabled = true;
        try {
            const json = await post(urls.cancel, { requestId: requestId });
            if (!json.success) {
                showMessage(json.message, true);
                if (btn) btn.disabled = false;
                return;
            }
            showMessage(json.message, false);

            const item = sentList.querySelector('[data-request-id="' + requestId + '"]');
            if (item) item.remove();
            updateSentCount();

            if (currentUser && currentUser.requestId === requestId) {
                currentUser.relationStatus = 'None';
                currentUser.requestId = null;
                renderResult(currentUser);
            }
        } catch (e) {
            showMessage('Có lỗi xảy ra, vui lòng thử lại.', true);
            if (btn) btn.disabled = false;
        }
    }

    //Gắn sự kiện
    searchBtn.addEventListener('click', search);
    input.addEventListener('keydown', function (e) {
        if (e.key === 'Enter') { e.preventDefault(); search(); }
    });

    resultBox.addEventListener('click', function (e) {
        const btn = e.target.closest('button[data-act]');
        if (!btn || !currentUser) return;
        if (btn.dataset.act === 'send') sendRequest(btn);
        if (btn.dataset.act === 'cancel') cancelRequest(currentUser.requestId, btn);
    });

    sentList.addEventListener('click', function (e) {
        const btn = e.target.closest('.friend-cancel-btn');
        if (btn) cancelRequest(btn.dataset.requestId, btn);
    });
})();
