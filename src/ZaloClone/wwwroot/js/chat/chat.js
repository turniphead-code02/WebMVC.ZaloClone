// Zalo Clone - Modern 4-Column Chat Engine (SignalR + Fetch API)
document.addEventListener("DOMContentLoaded", function () {
    const chatStream = document.getElementById("chatStream");
    const inputMessage = document.getElementById("inputMessage");
    const btnSend = document.getElementById("btnSend");
    const btnAttach = document.getElementById("btnAttach");
    const fileAttachmentInput = document.getElementById("fileAttachmentInput");
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

            let bubbleContent = escapeHtml(msg.content);
            if (msg.messageType === 'Image' && msg.fileUrl) {
                bubbleContent = `<a href="${escapeHtml(msg.fileUrl)}" target="_blank"><img src="${escapeHtml(msg.fileUrl)}" alt="Ảnh" class="img-fluid rounded-3 mb-1" style="max-height: 200px;" /></a><br /><small>${escapeHtml(msg.fileName || '')}</small>`;
            } else if (msg.messageType === 'File' && msg.fileUrl) {
                bubbleContent = `<div class="d-flex align-items-center gap-2"><i class="bi bi-file-earmark-arrow-down fs-4"></i><div><a href="${escapeHtml(msg.fileUrl)}" download class="text-decoration-none fw-semibold ${isSelf ? 'text-white' : 'text-primary'}">${escapeHtml(msg.fileName || 'Tệp đính kèm')}</a></div></div>`;
            }

            const row = document.createElement("div");
            row.className = `msg-row ${isSelf ? 'outgoing' : 'incoming'}`;
            row.innerHTML = `
                <div class="msg-avatar" title="${escapeHtml(msg.senderName)}">${initials}</div>
                <div class="msg-bubble-wrap">
                    ${!isSelf ? `<div class="msg-sender-name">${escapeHtml(msg.senderName)}</div>` : ''}
                    <div class="msg-bubble">${bubbleContent}</div>
                    <div class="msg-time">${msg.createdAt || 'Vừa xong'}</div>
                </div>
            `;
            chatStream.appendChild(row);
            chatStream.scrollTop = chatStream.scrollHeight;

            // Nếu là file, tự động cập nhật danh sách Files ở Cột 4
            if (msg.fileUrl) {
                refreshAttachments(activeConvId);
            }
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

    // 2. GỬI TIN NHẮN VĂN BẢN
    async function sendMessage() {
        if (!inputMessage) return;
        const content = inputMessage.value.trim();
        if (!content || !activeConvId) return;

        inputMessage.value = "";
        inputMessage.focus();

        try {
            if (connection.state === signalR.HubConnectionState.Connected) {
                await connection.invoke("SendMessage", activeConvId, content);
            } else {
                const res = await fetch("/Chat/SendMessage", {
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

    // 3. GỬI TỆP ĐÍNH KÈM / ẢNH
    if (btnAttach && fileAttachmentInput) {
        btnAttach.addEventListener("click", () => fileAttachmentInput.click());

        fileAttachmentInput.addEventListener("change", async function () {
            if (!this.files || this.files.length === 0 || !activeConvId) return;
            const file = this.files[0];
            const formData = new FormData();
            formData.append("conversationId", activeConvId);
            formData.append("file", file);

            try {
                const res = await fetch("/Chat/UploadFile", {
                    method: "POST",
                    body: formData
                });
                const data = await res.json();
                if (!data.success) {
                    alert(data.message || "Tải lên tệp không thành công!");
                }
            } catch (err) {
                console.error("Lỗi khi upload file:", err);
                alert("Lỗi khi tải file lên máy chủ.");
            } finally {
                fileAttachmentInput.value = "";
            }
        });
    }

    // 4. CHUYỂN ĐỔI HỘI THOẠI MƯỢT MÀ KHI CLICK Ở CỘT 2
    document.querySelectorAll(".conv-item").forEach(item => {
        item.addEventListener("click", async function (e) {
            e.preventDefault();
            const targetId = this.dataset.id;
            if (targetId === activeConvId) return;

            document.querySelectorAll(".conv-item").forEach(el => el.classList.remove("active"));
            this.classList.add("active");
            activeConvId = targetId;
            document.body.dataset.activeConvId = targetId;

            history.pushState(null, "", `/?conversationId=${targetId}`);

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
        const convName = convItem.querySelector(".conv-name").textContent;
        const chatHeaderName = document.getElementById("chatHeaderName");
        if (chatHeaderName) chatHeaderName.textContent = convName;

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
                chatStream.innerHTML = `<div class="text-center text-muted py-5">Chưa có tin nhắn nào. Hãy gửi lời chào nhé! 👋</div>`;
            }
            chatStream.scrollTop = chatStream.scrollHeight;
        }

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

        updateFilesList(data.attachments || []);
    }

    async function refreshAttachments(convId) {
        try {
            const res = await fetch(`/Chat/Attachments?conversationId=${convId}`);
            const data = await res.json();
            if (data.success) {
                updateFilesList(data.data);
            }
        } catch (e) {
            console.error("Lỗi làm mới tệp:", e);
        }
    }

    function updateFilesList(attachments) {
        const fileList = document.getElementById("directoryFileList");
        const fileCount = document.getElementById("directoryFileCount");
        if (!fileList) return;

        if (fileCount) fileCount.textContent = attachments.length;
        if (attachments.length > 0) {
            fileList.innerHTML = attachments.map(f => `
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
            fileList.innerHTML = `<p class="text-muted small my-2">Chưa có tệp chia sẻ nào.</p>`;
        }
    }

    // 5. TÌM KIẾM HỘI THOẠI TRONG CỘT 2
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

    // 6. MODAL TẠO NHÓM CHAT MỚI
    const createGroupModal = document.getElementById("createGroupModal");
    const friendsSelectionList = document.getElementById("friendsSelectionList");
    const btnSubmitCreateGroup = document.getElementById("btnSubmitCreateGroup");
    const newGroupTitle = document.getElementById("newGroupTitle");

    if (createGroupModal && friendsSelectionList) {
        createGroupModal.addEventListener("show.bs.modal", async function () {
            friendsSelectionList.innerHTML = `<div class="text-center text-muted py-3 small">Đang tải danh sách bạn bè...</div>`;
            try {
                const res = await fetch("/Group/FriendsToInvite");
                const data = await res.json();
                if (data.success && data.data && data.data.length > 0) {
                    friendsSelectionList.innerHTML = data.data.map(f => `
                        <div class="form-check d-flex align-items-center gap-2 py-1 border-bottom">
                            <input class="form-check-input group-friend-checkbox" type="checkbox" value="${f.userId}" id="friend_${f.userId}">
                            <label class="form-check-label d-flex align-items-center gap-2 cursor-pointer flex-grow-1" for="friend_${f.userId}">
                                <strong>${escapeHtml(f.displayName)}</strong>
                                <span class="text-muted small">(${escapeHtml(f.email)})</span>
                            </label>
                        </div>
                    `).join('');
                } else {
                    friendsSelectionList.innerHTML = `<div class="text-center text-muted py-3 small">Chưa có bạn bè nào để tạo nhóm.<br /><a href="/Friend/Search" class="btn btn-sm btn-link">Tìm bạn bè ngay</a></div>`;
                }
            } catch (err) {
                friendsSelectionList.innerHTML = `<div class="text-danger small py-2">Không thể tải danh sách bạn bè.</div>`;
            }
        });
    }

    if (btnSubmitCreateGroup) {
        btnSubmitCreateGroup.addEventListener("click", async function () {
            const title = newGroupTitle?.value.trim();
            if (!title) {
                alert("Vui lòng nhập tên nhóm.");
                newGroupTitle?.focus();
                return;
            }

            const checkedFriendIds = Array.from(document.querySelectorAll(".group-friend-checkbox:checked"))
                .map(cb => cb.value);

            const formData = new FormData();
            formData.append("Title", title);
            checkedFriendIds.forEach(id => formData.append("MemberIds", id));

            this.disabled = true;
            this.textContent = "Đang tạo...";

            try {
                const res = await fetch("/Group/Create", {
                    method: "POST",
                    body: formData
                });
                const data = await res.json();
                if (data.success && data.data) {
                    window.location.href = `/?conversationId=${data.data.id}`;
                } else {
                    alert(data.message || "Tạo nhóm thất bại.");
                    this.disabled = false;
                    this.textContent = "Tạo nhóm";
                }
            } catch (err) {
                console.error("Lỗi tạo nhóm:", err);
                alert("Đã xảy ra lỗi khi tạo nhóm.");
                this.disabled = false;
                this.textContent = "Tạo nhóm";
            }
        });
    }

    function escapeHtml(str) {
        if (!str) return '';
        const div = document.createElement('div');
        div.textContent = str;
        return div.innerHTML;
    }
});
