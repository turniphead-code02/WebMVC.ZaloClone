// Zalo Clone - Modern 4-Column Chat Engine (SignalR + Fetch API)
document.addEventListener("DOMContentLoaded", function () {
    const chatStream = document.getElementById("chatStream");
    const inputMessage = document.getElementById("inputMessage");
    const btnSend = document.getElementById("btnSend");
    const currentUserId = document.body.dataset.userId;
    let activeConvId = document.body.dataset.activeConvId;

    // Tự động cuộn xuống tin nhắn cuối cùng khi mở trang
    if (chatStream) {
        chatStream.scrollTop = chatStream.scrollHeight;
    }

    // 1. KHỞI TẠO KẾT NỐI SIGNALR WEBSOCKET
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/chathub")
        .withAutomaticReconnect([0, 2000, 5000, 10000])
        .build();

    // Nhận tin nhắn mới tức thời từ Server
    connection.on("ReceiveMessage", function (msg) {
        console.log("Tin nhắn mới nhận:", msg);

        // A. Cập nhật vào dòng tin nhắn nếu đang mở đúng cuộc trò chuyện này
        if (msg.conversationId === activeConvId && chatStream) {
            const isSelf = msg.senderId === currentUserId;
            const initials = msg.senderName ? msg.senderName.split(' ').map(n => n[0]).slice(-2).join('') : 'U';

            const row = document.createElement("div");
            row.className = `msg-row ${isSelf ? 'outgoing' : 'incoming'}`;
            row.innerHTML = `
                <div class="msg-avatar" title="${escapeHtml(msg.senderName)}">${initials}</div>
                <div class="msg-bubble-wrap">
                    ${!isSelf ? `<div class="msg-sender-name">${escapeHtml(msg.senderName)}</div>` : ''}
                    <div class="msg-bubble">${escapeHtml(msg.content)}</div>
                    <div class="msg-time">${msg.createdAt || 'Vừa xong'}</div>
                </div>
            `;
            chatStream.appendChild(row);
            chatStream.scrollTop = chatStream.scrollHeight;
        }

        // B. Cập nhật preview tin nhắn ở Cột 2 (Danh sách hội thoại)
        const convElement = document.querySelector(`.conv-item[data-id="${msg.conversationId}"]`);
        if (convElement) {
            const previewEl = convElement.querySelector(".conv-preview");
            const timeEl = convElement.querySelector(".conv-time");
            if (previewEl) {
                const prefix = msg.senderId === currentUserId ? "Bạn: " : "";
                previewEl.textContent = prefix + msg.content;
            }
            if (timeEl) {
                timeEl.textContent = "Vừa xong";
            }
            // Đưa cuộc hội thoại có tin nhắn mới nhất lên đầu danh sách
            const parent = convElement.parentElement;
            if (parent && parent.firstChild !== convElement) {
                parent.insertBefore(convElement, parent.firstChild);
            }
        }
    });

    // Bắt đầu kết nối
    connection.start()
        .then(() => console.log(" SignalR ChatHub đã kết nối thành công!"))
        .catch(err => console.error(" Lỗi kết nối SignalR:", err));

    // 2. GỬI TIN NHẮN
    async function sendMessage() {
        if (!inputMessage) return;
        const content = inputMessage.value.trim();
        if (!content || !activeConvId) return;

        inputMessage.value = "";
        inputMessage.focus();

        try {
            // Ưu tiên gửi qua SignalR Hub
            if (connection.state === signalR.HubConnectionState.Connected) {
                await connection.invoke("SendMessage", activeConvId, content);
            } else {
                // Fallback qua HTTP POST nếu WebSocket gián đoạn
                const res = await fetch("/Home/SendMessage", {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ conversationId: activeConvId, content: content })
                });
                const data = await res.json();
                if (!data.success) {
                    alert(data.message || "Không thể gửi tin nhắn!");
                }
            }
        } catch (err) {
            console.error("Lỗi khi gửi tin nhắn:", err);
        }
    }

    if (btnSend) {
        btnSend.addEventListener("click", sendMessage);
    }

    if (inputMessage) {
        inputMessage.addEventListener("keydown", function (e) {
            if (e.key === "Enter" && !e.shiftKey) {
                e.preventDefault();
                sendMessage();
            }
        });
    }

    // 3. CHUYỂN ĐỔI HỘI THOẠI MƯỢT MÀ KHI CLICK Ở CỘT 2
    document.querySelectorAll(".conv-item").forEach(item => {
        item.addEventListener("click", async function (e) {
            e.preventDefault();
            const targetId = this.dataset.id;
            if (targetId === activeConvId) return;

            // Đổi active class ở cột 2
            document.querySelectorAll(".conv-item").forEach(el => el.classList.remove("active"));
            this.classList.add("active");
            activeConvId = targetId;
            document.body.dataset.activeConvId = targetId;

            // Đổi URL trên thanh địa chỉ mà không reload trang
            history.pushState(null, "", `/?conversationId=${targetId}`);

            // Cập nhật dữ liệu từ API
            try {
                const res = await fetch(`/Home/GetConversationData?conversationId=${targetId}`);
                const data = await res.json();
                if (data.success) {
                    renderConversation(data, this);
                }
            } catch (err) {
                console.error("Lỗi khi tải cuộc trò chuyện:", err);
            }
        });
    });

    function renderConversation(data, convItem) {
        // Cập nhật Cột 3 Header
        const convName = convItem.querySelector(".conv-name").textContent;
        const chatHeaderName = document.getElementById("chatHeaderName");
        if (chatHeaderName) chatHeaderName.textContent = convName;

        // Cập nhật Cột 3 Messages
        if (chatStream) {
            chatStream.innerHTML = "";
            if (data.messages && data.messages.length > 0) {
                data.messages.forEach(msg => {
                    const initials = msg.senderName ? msg.senderName.split(' ').map(n => n[0]).slice(-2).join('') : 'U';
                    const row = document.createElement("div");
                    row.className = `msg-row ${msg.isSelf ? 'outgoing' : 'incoming'}`;
                    row.innerHTML = `
                        <div class="msg-avatar" title="${escapeHtml(msg.senderName)}">${initials}</div>
                        <div class="msg-bubble-wrap">
                            ${!msg.isSelf ? `<div class="msg-sender-name">${escapeHtml(msg.senderName)}</div>` : ''}
                            <div class="msg-bubble">${escapeHtml(msg.content)}</div>
                            <div class="msg-time">${msg.formattedTime}</div>
                        </div>
                    `;
                    chatStream.appendChild(row);
                });
            } else {
                chatStream.innerHTML = `<div class="text-center text-muted py-5">Chưa có tin nhắn nào trong cuộc trò chuyện này. Hãy gửi lời chào nhé! 👋</div>`;
            }
            chatStream.scrollTop = chatStream.scrollHeight;
        }

        // Cập nhật Cột 4 Members
        const memberList = document.getElementById("directoryMemberList");
        const memberCount = document.getElementById("directoryMemberCount");
        if (memberList && data.members) {
            if (memberCount) memberCount.textContent = data.members.length;
            memberList.innerHTML = data.members.map(m => {
                const initials = m.displayName ? m.displayName.split(' ').map(n => n[0]).slice(-2).join('') : 'U';
                return `
                    <div class="member-item">
                        <div class="member-avatar">${initials}</div>
                        <div class="member-details">
                            <h6>${escapeHtml(m.displayName)}</h6>
                            <p>${escapeHtml(m.bio || (m.role === 'Admin' ? 'Trưởng nhóm' : 'Thành viên'))}</p>
                        </div>
                    </div>
                `;
            }).join('');
        }

        // Cập nhật Cột 4 Files
        const fileList = document.getElementById("directoryFileList");
        const fileCount = document.getElementById("directoryFileCount");
        if (fileList && data.attachments) {
            if (fileCount) fileCount.textContent = data.attachments.length;
            if (data.attachments.length > 0) {
                fileList.innerHTML = data.attachments.map(f => `
                    <div class="file-item">
                        <div class="file-left">
                            <div class="file-icon-box icon-box-${f.badgeColor}">
                                <i class="bi ${f.iconClass}"></i>
                            </div>
                            <div class="file-meta">
                                <h6 class="file-name" title="${escapeHtml(f.fileName)}">${escapeHtml(f.fileName)}</h6>
                                <p class="file-info">${f.formattedSize}</p>
                            </div>
                        </div>
                        <a href="/uploads/${escapeHtml(f.storageKey)}" download class="btn-file-dl" title="Tải về">
                            <i class="bi bi-download"></i>
                        </a>
                    </div>
                `).join('');
            } else {
                fileList.innerHTML = `<p class="text-muted small my-2">Không có file đính kèm nào.</p>`;
            }
        }
    }

    // 4. TÌM KIẾM HỘI THOẠI TRONG CỘT 2
    const searchConvInput = document.getElementById("searchConvInput");
    if (searchConvInput) {
        searchConvInput.addEventListener("input", function () {
            const query = this.value.toLowerCase().trim();
            document.querySelectorAll(".conv-item").forEach(item => {
                const name = item.querySelector(".conv-name")?.textContent.toLowerCase() || "";
                const preview = item.querySelector(".conv-preview")?.textContent.toLowerCase() || "";
                if (name.includes(query) || preview.includes(query)) {
                    item.style.display = "flex";
                } else {
                    item.style.display = "none";
                }
            });
        });
    }

    function escapeHtml(str) {
        if (!str) return '';
        const div = document.createElement('div');
        div.textContent = str;
        return div.innerHTML;
    }
});
