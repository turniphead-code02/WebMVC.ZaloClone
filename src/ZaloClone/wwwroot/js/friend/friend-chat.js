(function () {
    'use strict';

    const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
    const messageBox = document.getElementById('friendActionMessage');

    function showError(message) {
        if (!messageBox) return;
        messageBox.textContent = message;
        messageBox.className = 'alert alert-danger';
    }

    document.addEventListener('click', async function (event) {
        const button = event.target.closest('[data-start-chat]');
        if (!button) return;

        if (!tokenInput) {
            showError('Không tìm thấy mã xác thực. Hãy tải lại trang rồi thử lại.');
            return;
        }

        button.disabled = true;
        try {
            const response = await fetch(button.dataset.startChatUrl, {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8' },
                body: new URLSearchParams({
                    friendId: button.dataset.startChat,
                    __RequestVerificationToken: tokenInput.value
                })
            });
            const result = await response.json();
            if (!response.ok || !result.success || !result.redirectUrl) {
                throw new Error(result.message || 'Không thể mở cuộc trò chuyện.');
            }
            window.location.href = result.redirectUrl;
        } catch (error) {
            showError(error.message || 'Có lỗi xảy ra. Vui lòng thử lại.');
            button.disabled = false;
        }
    });
})();
