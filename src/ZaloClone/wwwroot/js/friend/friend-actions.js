(function () {
    'use strict';

    const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
    const messageBox = document.getElementById('friendActionMessage');

    function showMessage(message, isError) {
        if (!messageBox) return;
        messageBox.textContent = message || '';
        messageBox.className = message
            ? 'alert ' + (isError ? 'alert-danger' : 'alert-success')
            : 'alert d-none';
    }

    document.addEventListener('click', async function (event) {
        const requestButton = event.target.closest('[data-request-action]');
        const blockButton = event.target.closest('[data-friend-block]');
        const button = requestButton || blockButton;
        if (!button) return;

        if (!tokenInput) {
            showMessage('Không tìm thấy mã xác thực. Hãy tải lại trang rồi thử lại.', true);
            return;
        }

        if (blockButton && !window.confirm('Bạn chắc chắn muốn chặn người này?')) return;

        const values = requestButton
            ? { requestId: button.dataset.requestId }
            : { friendId: button.dataset.friendBlock };

        button.disabled = true;
        try {
            const response = await fetch(button.dataset.url, {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8' },
                body: new URLSearchParams({ ...values, __RequestVerificationToken: tokenInput.value })
            });
            const result = await response.json();
            if (!response.ok || !result.success) {
                throw new Error(result.message || 'Thao tác chưa thực hiện được.');
            }

            showMessage(result.message || 'Đã cập nhật.', false);
            const row = requestButton
                ? button.closest('[data-request-row]')
                : button.closest('[data-friend-row]');
            if (row) row.remove();
        } catch (error) {
            showMessage(error.message || 'Có lỗi xảy ra. Vui lòng thử lại.', true);
            button.disabled = false;
        }
    });
})();
