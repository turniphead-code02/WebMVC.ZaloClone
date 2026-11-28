// Zalo Clone - Modern 4-Column Chat Engine (SignalR + Fetch API)
document.addEventListener("DOMContentLoaded", function () {
    const chatStream = document.getElementById("chatStream");
    const inputMessage = document.getElementById("inputMessage");
    const btnSend = document.getElementById("btnSend");
    const btnAttach = document.getElementById("btnAttach");
    const fileAttachmentInput = document.getElementById("fileAttachmentInput");
    const currentUserId = document.body.dataset.userId;
    let activeConvId = document.body.dataset.activeConvId;

    // Trạng thái Trả lời & Ghim tin nhắn
    let replyingTo = null; // { id, senderName, content }
    let currentPinnedMessageId = null;

    // Tự động cuộn xuống tin nhắn cuối cùng khi mở trang
    if (chatStream) {
        chatStream.scrollTop = chatStream.scrollHeight;
    }

    // 0. KHỞI TẠO GIAO DIỆN EMOJI, THANH GHIM & KHUNG TRẢ LỜI
    initEmojiPicker();
    const pinnedBanner = initPinnedBanner();
    const replyPreviewBar = initReplyPreviewBar();

    function initEmojiPicker() {
        const inputBar = document.querySelector(".chat-input-bar");
        if (!inputBar || !inputMessage) return;

        inputBar.style.position = "relative";

        const btnEmoji = document.createElement("button");
        btnEmoji.type = "button";
        btnEmoji.className = "btn-attach";
        btnEmoji.title = "Chèn biểu tượng cảm xúc";
        btnEmoji.innerHTML = `<i class="bi bi-emoji-smile"></i>`;

        const emojiPopup = document.createElement("div");
        emojiPopup.className = "shadow rounded-3 bg-white border p-2 d-none";
        emojiPopup.style.cssText = "position: absolute; bottom: 64px; left: 16px; z-index: 1050; width: 260px; display: grid; grid-template-columns: repeat(7, 1fr); gap: 4px;";

        const emojis = [
            "😀", "😂", "🤣", "😊", "🥰", "😍", "😘",
            "😎", "🤔", "😢", "😭", "😡", "😱", "😴",
            "👍", "👎", "👏", "🙏", "❤️", "💔", "🔥",
            "🎉", "✨", "💯", "🌹", "☕", "🍻", "👋"
        ];

        emojis.forEach(em => {
            const span = document.createElement("button");
            span.type = "button";
            span.className = "btn btn-sm btn-light p-1 fs-5 border-0";
            span.textContent = em;
            span.addEventListener("click", function () {
                inputMessage.value += em;
                inputMessage.focus();
            });
            emojiPopup.appendChild(span);
        });

        btnEmoji.addEventListener("click", function (e) {
            e.stopPropagation();
            emojiPopup.classList.toggle("d-none");
        });

        document.addEventListener("click", function (e) {
            if (!emojiPopup.contains(e.target) && e.target !== btnEmoji) {
                emojiPopup.classList.add("d-none");
            }
        });

        inputBar.insertBefore(btnEmoji, inputMessage);
        inputBar.appendChild(emojiPopup);
    }

    function initPinnedBanner() {
        const chatHeader = document.querySelector(".main-chat-panel .chat-header");
        if (!chatHeader) return null;

        const banner = document.createElement("div");
        banner.id = "pinnedMessageBanner";
        banner.className = "d-none align-items-center justify-content-between px-3 py-2 bg-light border-bottom";
        banner.style.cssText = "font-size: 13px; cursor: pointer;";
        banner.innerHTML = `
            <div class="d-flex align-items-center gap-2 text-truncate me-2" id="pinnedBannerJump" title="Bấm để đi tới tin nhắn đã ghim">
                <i class="bi bi-pin-angle-fill text-primary fs-6"></i>
                <div class="text-truncate">
                    <span class="fw-semibold text-primary me-1" id="pinnedBannerSender"></span>
                    <span class="text-muted" id="pinnedBannerContent"></span>
                </div>
            </div>
            <button type="button" class="btn btn-sm btn-outline-secondary py-0 px-2" id="btnUnpinBanner" title="Bỏ ghim tin nhắn">
                Bỏ ghim
            </button>
        `;

        chatHeader.insertAdjacentElement("afterend", banner);

        banner.querySelector("#pinnedBannerJump").addEventListener("click", () => {
            if (currentPinnedMessageId) {
                scrollToMessage(currentPinnedMessageId);
            }
        });

        banner.querySelector("#btnUnpinBanner").addEventListener("click", (e) => {
            e.stopPropagation();
            if (currentPinnedMessageId) {
                togglePinMessage(currentPinnedMessageId);
            }
        });

        return banner;
    }

    function updatePinnedBanner(pinData) {
        if (!pinnedBanner) return;

        if (!pinData || !pinData.messageId) {
            currentPinnedMessageId = null;
            pinnedBanner.classList.add("d-none");
            pinnedBanner.classList.remove("d-flex");
            return;
        }

        currentPinnedMessageId = pinData.messageId;
        pinnedBanner.querySelector("#pinnedBannerSender").textContent = `${pinData.senderName || "Tin nhắn"}:`;
        pinnedBanner.querySelector("#pinnedBannerContent").textContent = pinData.content || "";
        pinnedBanner.classList.remove("d-none");
        pinnedBanner.classList.add("d-flex");
    }

    function initReplyPreviewBar() {
        const inputBar = document.querySelector(".main-chat-panel .chat-input-bar");
        if (!inputBar) return null;

        const bar = document.createElement("div");
        bar.id = "replyPreviewBar";
        bar.className = "d-none align-items-center justify-content-between px-3 py-2 bg-light border-top";
        bar.style.cssText = "font-size: 13px;";
        bar.innerHTML = `
            <div class="d-flex align-items-center gap-2 text-truncate me-2" style="border-left: 3px solid #0d6efd; padding-left: 8px;">
                <div class="text-truncate">
                    <div class="fw-semibold text-primary" id="replyPreviewSender" style="font-size: 12px;"></div>
                    <div class="text-muted text-truncate" id="replyPreviewContent" style="max-width: 480px;"></div>
                </div>
            </div>
            <button type="button" class="btn-close btn-sm" id="btnCancelReply" aria-label="Hủy trả lời"></button>
        `;

        inputBar.insertAdjacentElement("beforebegin", bar);

        bar.querySelector("#btnCancelReply").addEventListener("click", clearReplyState);
        return bar;
    }

    function setReplyState(msg) {
        replyingTo = {
            id: msg.id,
            senderName: msg.senderName,
            content: msg.content
        };
        if (replyPreviewBar) {
            replyPreviewBar.querySelector("#replyPreviewSender").textContent = `Đang trả lời ${msg.senderName}`;
            replyPreviewBar.querySelector("#replyPreviewContent").textContent = msg.content;
            replyPreviewBar.classList.remove("d-none");
            replyPreviewBar.classList.add("d-flex");
        }
        if (inputMessage) inputMessage.focus();
    }

    function clearReplyState() {
        replyingTo = null;
        if (replyPreviewBar) {
            replyPreviewBar.classList.add("d-none");
            replyPreviewBar.classList.remove("d-flex");
        }
    }

    function scrollToMessage(messageId) {
        const target = document.querySelector(`.msg-row[data-message-id="${messageId}"]`);
        if (!target) return;

        target.scrollIntoView({ behavior: "smooth", block: "center" });
        const bubble = target.querySelector(".msg-bubble");
        if (bubble) {
            bubble.style.transition = "box-shadow 0.3s ease";
            bubble.style.boxShadow = "0 0 0 3px rgba(13, 110, 253, 0.55)";
            setTimeout(() => {
                bubble.style.boxShadow = "";
            }, 1500);
        }
    }

    // 1. KHỞI TẠO KẾT NỐI SIGNALR WEBSOCKET
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/chathub")
        .withAutomaticReconnect([0, 2000, 5000, 10000])
        .build();

    // Nhận tin nhắn mới tức thời từ Server
    connection.on("ReceiveMessage", function (msg) {
        if (msg.conversationId === activeConvId && chatStream) {
            if (msg.id && document.querySelector(`.msg-row[data-message-id="${msg.id}"]`)) {
                return;
            }
            const emptyHint = chatStream.querySelector(".text-center.text-muted");
            if (emptyHint) emptyHint.remove();

            const isSelf = msg.senderId === currentUserId;
            const row = buildMessageRow({
                id: msg.id,
                senderName: msg.senderName,
                senderAvatar: msg.senderAvatar,
                content: msg.content,
                messageType: msg.messageType,
                fileUrl: msg.fileUrl,
                fileName: msg.fileName,
                formattedTime: msg.createdAt || "Vừa xong",
                isSelf: isSelf,
                isRecalled: false,
                replyToId: msg.replyToId || null,
                replyToSenderName: msg.replyToSenderName || null,
                replyToContent: msg.replyToContent || null,
                isPinned: !!msg.isPinned
            });

            chatStream.appendChild(row);
            chatStream.scrollTop = chatStream.scrollHeight;

            if (typingIndicator) {
                typingIndicator.classList.add("d-none");
            }
            if (!isSelf && connection.state === signalR.HubConnectionState.Connected && msg.id) {
                connection.invoke("MarkAsRead", activeConvId, msg.id).catch(() => {});
            }

            if (msg.fileUrl) {
                refreshAttachments(activeConvId);
            }
        } else if (msg.senderId !== currentUserId) {
            // Tăng badge chưa đọc ở Cột 2 nếu đang mở hội thoại khác
            const convElement = document.querySelector(`.conv-item[data-id="${msg.conversationId}"]`);
            if (convElement) {
                const badge = convElement.querySelector(".unread-badge");
                if (badge) {
                    const nextCount = (parseInt(badge.dataset.unread || "0", 10) || 0) + 1;
                    badge.dataset.unread = String(nextCount);
                    badge.textContent = String(nextCount);
                    badge.classList.remove("d-none");
                }
            }
        }

        // Cập nhật preview tin nhắn ở Cột 2
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

    // Nhận sự kiện Thu hồi tin nhắn real-time từ Server
    connection.on("MessageRecalled", function (data) {
        const row = document.querySelector(`.msg-row[data-message-id="${data.messageId}"]`);
        if (row) {
            const bubble = row.querySelector(".msg-bubble");
            if (bubble) {
                bubble.innerHTML = `<em class="opacity-75">Tin nhắn đã được thu hồi</em>`;
            }
            row.querySelectorAll(".btn-recall-msg, .btn-reply-msg, .btn-pin-msg, .pin-badge").forEach(el => el.remove());
        }
        if (currentPinnedMessageId === data.messageId) {
            updatePinnedBanner(null);
        }
        refreshAttachments(activeConvId);
    });

    connection.on("ReceiveError", function (errorMessage) {
        if (errorMessage) alert(errorMessage);
    });

    // Nhận sự kiện Ghim / Bỏ ghim tin nhắn real-time từ Server
    connection.on("MessagePinned", function (data) {
        if (data.conversationId !== activeConvId) return;

        document.querySelectorAll(".msg-row").forEach(row => {
            const pinBtn = row.querySelector(".btn-pin-msg");
            const badge = row.querySelector(".pin-badge");
            if (badge) badge.remove();
            if (pinBtn) pinBtn.textContent = "Ghim";
        });

        if (data.isPinned) {
            updatePinnedBanner({
                messageId: data.messageId,
                senderName: data.senderName,
                content: data.content
            });

            const targetRow = document.querySelector(`.msg-row[data-message-id="${data.messageId}"]`);
            if (targetRow) {
                const pinBtn = targetRow.querySelector(".btn-pin-msg");
                if (pinBtn) pinBtn.textContent = "Bỏ ghim";
                const timeRow = targetRow.querySelector(".msg-time");
                if (timeRow && !timeRow.querySelector(".pin-badge")) {
                    const badge = document.createElement("i");
                    badge.className = "bi bi-pin-angle-fill text-warning me-1 pin-badge";
                    badge.title = "Đang ghim";
                    timeRow.prepend(badge);
                }
            }
        } else {
            updatePinnedBanner(null);
        }
    });

    // Nhận trạng thái Online/Offline thật từ Server
    connection.on("PresenceChanged", function (data) {
        if (!data || !data.userId) return;
        document.querySelectorAll(`.conv-item[data-other-user-id="${data.userId}"] .online-indicator`).forEach(dot => {
            dot.classList.toggle("d-none", !data.isOnline);
        });
        const headerStatus = document.getElementById("chatHeaderStatus");
        if (headerStatus && headerStatus.dataset.otherUserId === data.userId) {
            headerStatus.classList.toggle("offline", !data.isOnline);
            const label = headerStatus.querySelector("span:last-child");
            if (label) label.textContent = data.isOnline ? "Online" : "Offline";
        }
    });

    // Nhận trạng thái "Đang soạn tin..."
    const typingIndicator = document.getElementById("typingIndicator");
    let typingHideTimer = null;
    connection.on("UserTyping", function (data) {
        if (!data || data.conversationId !== activeConvId || data.userId === currentUserId || !typingIndicator) return;
        typingIndicator.textContent = `${data.senderName || "Ai đó"} đang soạn tin...`;
        typingIndicator.classList.remove("d-none");
        clearTimeout(typingHideTimer);
        typingHideTimer = setTimeout(() => {
            typingIndicator.classList.add("d-none");
        }, 2500);
    });

    connection.start()
        .then(() => console.log("SignalR ChatHub đã kết nối thành công!"))
        .catch(err => console.error("Lỗi kết nối SignalR:", err));

    // Đồng bộ lại danh sách tin nhắn (ID, ảnh/file, reply, pin, nút thu hồi) khi vừa tải trang (F5)
    if (activeConvId && chatStream) {
        syncCurrentConversationMessages(activeConvId);
    }

    async function syncCurrentConversationMessages(convId) {
        try {
            const [msgRes, attRes] = await Promise.all([
                fetch(`/Chat/Messages?conversationId=${convId}`),
                fetch(`/Chat/Attachments?conversationId=${convId}`)
            ]);
            const msgData = await msgRes.json();
            const attData = await attRes.json();

            if (msgData.success && Array.isArray(msgData.data)) {
                const attachments = attData.success ? (attData.data || []) : [];
                renderMessagesList(msgData.data, attachments);
            }
        } catch (err) {
            console.error("Lỗi đồng bộ tin nhắn:", err);
        }
    }

    function renderMessagesList(messages, attachments) {
        if (!chatStream) return;
        chatStream.innerHTML = "";

        if (!messages || messages.length === 0) {
            updatePinnedBanner(null);
            chatStream.innerHTML = `<div class="text-center text-muted py-5">Chưa có tin nhắn nào. Hãy gửi lời chào nhé! 👋</div>`;
            return;
        }

        let pinnedMsg = null;

        messages.forEach(msg => {
            let fileUrl = null;
            let fileName = null;

            if (msg.messageType === "Image" || msg.messageType === "File") {
                fileName = msg.content.replace(/^\[(Ảnh|Tệp)\]\s*/, "");
                const matched = attachments.find(a => a.fileName === fileName);
                if (matched) {
                    fileUrl = matched.id ? `/Chat/File/${matched.id}` : getStorageUrl(matched.storageKey);
                }
            }

            const isRecalled = msg.messageType === "Recalled" || msg.content === "Tin nhắn đã được thu hồi";
            if (msg.isPinned && !isRecalled) {
                pinnedMsg = {
                    messageId: msg.id,
                    senderName: msg.senderName,
                    content: msg.content
                };
            }

            const row = buildMessageRow({
                id: msg.id,
                senderName: msg.senderName,
                senderAvatar: msg.senderAvatar,
                content: msg.content,
                messageType: msg.messageType,
                fileUrl: fileUrl,
                fileName: fileName,
                formattedTime: msg.formattedTime,
                isSelf: msg.isSelf,
                isRecalled: isRecalled,
                replyToId: msg.replyToId || null,
                replyToSenderName: msg.replyToSenderName || null,
                replyToContent: msg.replyToContent || null,
                isPinned: !!msg.isPinned && !isRecalled
            });

            chatStream.appendChild(row);
        });

        updatePinnedBanner(pinnedMsg);
        chatStream.scrollTop = chatStream.scrollHeight;
    }

    function buildMessageRow(msg) {
        const initials = msg.senderName ? msg.senderName.split(" ").map(n => n[0]).slice(-2).join("") : "U";
        let bubbleContent = escapeHtml(msg.content);

        if (msg.isRecalled) {
            bubbleContent = `<em class="opacity-75">Tin nhắn đã được thu hồi</em>`;
        } else if (msg.messageType === "Image" && msg.fileUrl) {
            bubbleContent = `
                <a href="${escapeHtml(msg.fileUrl)}" target="_blank">
                    <img src="${escapeHtml(msg.fileUrl)}" alt="Ảnh" class="img-fluid rounded-3 mb-1" style="max-height: 200px;" />
                </a>
                <div class="small opacity-75">${escapeHtml(msg.fileName || "")}</div>`;
        } else if (msg.messageType === "File" && msg.fileUrl) {
            bubbleContent = `
                <div class="d-flex align-items-center gap-2">
                    <i class="bi bi-file-earmark-arrow-down fs-4"></i>
                    <a href="${escapeHtml(msg.fileUrl)}" download class="text-decoration-none fw-semibold ${msg.isSelf ? "text-white" : "text-primary"}">
                        ${escapeHtml(msg.fileName || "Tệp đính kèm")}
                    </a>
                </div>`;
        }

        let replyQuoteHtml = "";
        if (msg.replyToId && !msg.isRecalled) {
            const borderColor = msg.isSelf ? "rgba(255,255,255,0.85)" : "#0d6efd";
            const bgColor = msg.isSelf ? "rgba(255,255,255,0.18)" : "rgba(13,110,253,0.08)";
            replyQuoteHtml = `
                <div class="reply-quote-box mb-1 px-2 py-1 rounded-2" data-reply-target="${msg.replyToId}" style="cursor: pointer; font-size: 12px; border-left: 3px solid ${borderColor}; background: ${bgColor};">
                    <div class="fw-semibold" style="font-size: 11px;">${escapeHtml(msg.replyToSenderName || "Tin nhắn")}</div>
                    <div class="text-truncate" style="max-width: 240px; opacity: 0.85;">${escapeHtml(msg.replyToContent || "")}</div>
                </div>`;
        }

        const pinBadgeHtml = (msg.isPinned && !msg.isRecalled)
            ? `<i class="bi bi-pin-angle-fill text-warning me-1 pin-badge" title="Đang ghim"></i>`
            : "";

        const replyBtnHtml = (!msg.isRecalled && msg.id)
            ? `<button type="button" class="btn btn-link btn-sm text-muted p-0 ms-2 text-decoration-none btn-reply-msg" title="Trả lời tin nhắn" style="font-size: 11px;">Trả lời</button>`
            : "";

        const pinBtnHtml = (!msg.isRecalled && msg.id)
            ? `<button type="button" class="btn btn-link btn-sm text-muted p-0 ms-2 text-decoration-none btn-pin-msg" title="Ghim tin nhắn" style="font-size: 11px;">${msg.isPinned ? "Bỏ ghim" : "Ghim"}</button>`
            : "";

        const recallHtml = (msg.isSelf && !msg.isRecalled && msg.id)
            ? `<button type="button" class="btn btn-link btn-sm text-muted p-0 ms-2 text-decoration-none btn-recall-msg" data-id="${msg.id}" title="Thu hồi tin nhắn" style="font-size: 11px;">Thu hồi</button>`
            : "";

        const row = document.createElement("div");
        row.className = `msg-row ${msg.isSelf ? "outgoing" : "incoming"}`;
        if (msg.id) row.dataset.messageId = msg.id;

        const avatarHtml = msg.senderAvatar
            ? `<img src="${escapeHtml(msg.senderAvatar)}" alt="${escapeHtml(msg.senderName)}" class="avatar-img" />`
            : initials;

        row.innerHTML = `
            <div class="msg-avatar" title="${escapeHtml(msg.senderName)}">${avatarHtml}</div>
            <div class="msg-bubble-wrap">
                ${!msg.isSelf ? `<div class="msg-sender-name">${escapeHtml(msg.senderName)}</div>` : ""}
                <div class="msg-bubble">${replyQuoteHtml}${bubbleContent}</div>
                <div class="msg-time d-flex align-items-center ${msg.isSelf ? "justify-content-end" : ""}">
                    ${pinBadgeHtml}
                    <span>${msg.formattedTime || "Vừa xong"}</span>
                    ${replyBtnHtml}
                    ${pinBtnHtml}
                    ${recallHtml}
                </div>
            </div>
        `;

        const quoteBox = row.querySelector(".reply-quote-box");
        if (quoteBox) {
            quoteBox.addEventListener("click", () => scrollToMessage(msg.replyToId));
        }

        const btnReply = row.querySelector(".btn-reply-msg");
        if (btnReply) {
            btnReply.addEventListener("click", () => setReplyState(msg));
        }

        const btnPin = row.querySelector(".btn-pin-msg");
        if (btnPin) {
            btnPin.addEventListener("click", () => togglePinMessage(msg.id));
        }

        const btnRecall = row.querySelector(".btn-recall-msg");
        if (btnRecall) {
            btnRecall.addEventListener("click", () => recallMessage(msg.id));
        }

        return row;
    }

    async function togglePinMessage(messageId) {
        if (!messageId) return;
        try {
            const res = await fetch("/Chat/TogglePinMessage", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ messageId: messageId })
            });
            const data = await res.json();
            if (!data.success) {
                alert(data.message || "Không thể ghim/bỏ ghim tin nhắn.");
            }
        } catch (err) {
            console.error("Lỗi ghim tin nhắn:", err);
            alert("Lỗi kết nối khi ghim tin nhắn.");
        }
    }

    async function recallMessage(messageId) {
        if (!messageId) return;
        if (!confirm("Bạn có chắc chắn muốn thu hồi tin nhắn này?")) return;

        try {
            const res = await fetch("/Chat/RecallMessage", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ messageId: messageId })
            });
            const data = await res.json();
            if (!data.success) {
                alert(data.message || "Không thể thu hồi tin nhắn.");
            }
        } catch (err) {
            console.error("Lỗi thu hồi tin nhắn:", err);
            alert("Lỗi kết nối khi thu hồi tin nhắn.");
        }
    }

    // 2. GỬI TIN NHẮN VĂN BẢN (Hỗ trợ kèm ReplyToId, chống gửi trùng IME / double-trigger)
    let isSendingMessage = false;
    async function sendMessage() {
        if (isSendingMessage || !inputMessage) return;
        const content = inputMessage.value.trim();
        if (!content || !activeConvId) return;

        isSendingMessage = true;
        if (btnSend) btnSend.disabled = true;

        const replyId = replyingTo ? replyingTo.id : null;
        inputMessage.value = "";
        clearReplyState();
        inputMessage.focus();

        try {
            const res = await fetch("/Chat/SendMessage", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    conversationId: activeConvId,
                    content: content,
                    replyToId: replyId
                })
            });
            const data = await res.json();
            if (!data.success) {
                alert(data.message || "Không thể gửi tin nhắn!");
            }
        } catch (err) {
            console.error("Lỗi khi gửi tin nhắn:", err);
        } finally {
            isSendingMessage = false;
            if (btnSend) btnSend.disabled = false;
        }
    }

    if (btnSend) {
        btnSend.addEventListener("click", function (e) {
            e.preventDefault();
            sendMessage();
        });
    }

    let lastTypingSentAt = 0;
    if (inputMessage) {
        inputMessage.addEventListener("keydown", function (e) {
            if (e.isComposing || e.keyCode === 229) return;
            if (e.key === "Enter" && !e.shiftKey) {
                e.preventDefault();
                e.stopPropagation();
                sendMessage();
            }
        });
        inputMessage.addEventListener("input", function () {
            const now = Date.now();
            if (activeConvId && connection.state === signalR.HubConnectionState.Connected && now - lastTypingSentAt > 1500) {
                lastTypingSentAt = now;
                connection.invoke("Typing", activeConvId).catch(() => {});
            }
        });
    }

    // 3. GỬI TỆP ĐÍNH KÈM / ẢNH (Kiểm tra giới hạn 10MB Ảnh / 50MB File + hỗ trợ ReplyToId)
    if (btnAttach && fileAttachmentInput) {
        fileAttachmentInput.setAttribute("accept", ".jpg,.jpeg,.png,.webp,.gif,.pdf,.docx,.xlsx,.pptx,.zip,.txt");
        btnAttach.addEventListener("click", () => fileAttachmentInput.click());

        fileAttachmentInput.addEventListener("change", async function () {
            if (!this.files || this.files.length === 0 || !activeConvId) return;
            const file = this.files[0];

            const ext = file.name.substring(file.name.lastIndexOf(".")).toLowerCase();
            const imageExts = [".jpg", ".jpeg", ".png", ".webp", ".gif"];
            const fileExts = [".pdf", ".docx", ".xlsx", ".pptx", ".zip", ".txt"];
            const isImage = imageExts.includes(ext);
            const isDoc = fileExts.includes(ext);

            if (!isImage && !isDoc) {
                alert("Định dạng không hỗ trợ! Chỉ chấp nhận ảnh (.jpg, .png, .webp) hoặc file (.pdf, .docx, .xlsx, .pptx, .zip, .txt).");
                this.value = "";
                return;
            }

            const maxBytes = isImage ? 10 * 1024 * 1024 : 50 * 1024 * 1024;
            if (file.size > maxBytes) {
                alert(isImage ? "Ảnh vượt quá giới hạn 10 MB!" : "File đính kèm vượt quá giới hạn 50 MB!");
                this.value = "";
                return;
            }

            const formData = new FormData();
            formData.append("conversationId", activeConvId);
            formData.append("file", file);
            if (replyingTo && replyingTo.id) {
                formData.append("replyToId", replyingTo.id);
            }

            clearReplyState();

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
            const unreadBadge = this.querySelector(".unread-badge");
            if (unreadBadge) {
                unreadBadge.dataset.unread = "0";
                unreadBadge.textContent = "0";
                unreadBadge.classList.add("d-none");
            }
            if (typingIndicator) typingIndicator.classList.add("d-none");
            activeConvId = targetId;
            document.body.dataset.activeConvId = targetId;

            clearReplyState();
            updatePinnedBanner(null);
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

        const chatHeaderAvatar = document.getElementById("chatHeaderAvatar");
        const convAvatarEl = convItem.querySelector(".conv-avatar");
        if (chatHeaderAvatar && convAvatarEl) {
            chatHeaderAvatar.innerHTML = convAvatarEl.innerHTML;
        }

        const chatHeaderStatus = document.getElementById("chatHeaderStatus");
        if (chatHeaderStatus) {
            chatHeaderStatus.dataset.otherUserId = data.otherUserId || "";
            chatHeaderStatus.classList.toggle("offline", !data.isOnline);
            const label = chatHeaderStatus.querySelector("span:last-child");
            if (label) label.textContent = data.isOnline ? "Online" : "Offline";
        }

        // Gọi API /Chat/Messages để lấy đầy đủ trạng thái Reply, Pin, Recall, Ảnh/File
        syncCurrentConversationMessages(activeConvId);

        const btnOpenAddMembers = document.getElementById("btnOpenAddMembers");
        const groupActionsBar = document.getElementById("groupActionsBar");
        if (btnOpenAddMembers) btnOpenAddMembers.classList.toggle("d-none", !data.isGroup);
        if (groupActionsBar) groupActionsBar.classList.toggle("d-none", !data.isGroup);

        const memberList = document.getElementById("directoryMemberList");
        const memberCount = document.getElementById("directoryMemberCount");
        if (memberList && data.members) {
            if (memberCount) memberCount.textContent = data.members.length;
            memberList.innerHTML = data.members.map(m => {
                const initials = m.displayName ? m.displayName.split(" ").map(n => n[0]).slice(-2).join("") : "U";
                const avatarHtml = m.avatarUrl
                    ? `<img src="${escapeHtml(m.avatarUrl)}" alt="${escapeHtml(m.displayName)}" class="avatar-img" />`
                    : initials;
                const kickBtn = (data.isGroup && data.isCurrentUserAdmin && m.userId !== currentUserId)
                    ? `<button type="button" class="btn btn-link btn-sm text-danger p-0 btn-kick-member" data-user-id="${m.userId}" title="Xóa khỏi nhóm"><i class="bi bi-x-circle"></i></button>`
                    : "";
                return `
                    <div class="member-item justify-content-between">
                        <div class="d-flex align-items-center gap-2 min-width-0">
                            <div class="member-avatar flex-shrink-0">${avatarHtml}</div>
                            <div class="member-details text-truncate">
                                <h6 class="text-truncate">${escapeHtml(m.displayName)}</h6>
                                <p class="text-truncate">${escapeHtml(m.role === "Admin" ? "Trưởng nhóm" : (m.bio || "Thành viên"))}</p>
                            </div>
                        </div>
                        ${kickBtn}
                    </div>
                `;
            }).join("");
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
                    <a href="${f.id ? `/Chat/File/${f.id}` : escapeHtml(getStorageUrl(f.storageKey))}" download class="btn-file-dl" title="Tải về">
                        <i class="bi bi-download"></i>
                    </a>
                </div>
            `).join("");
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
                item.style.display = (name.includes(query) || preview.includes(query)) ? "flex" : "none";
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
                    `).join("");
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

    // 7. QUẢN LÝ NHÓM Ở CỘT 4 (Thêm thành viên, Xóa thành viên, Rời nhóm)
    const addGroupMembersModal = document.getElementById("addGroupMembersModal");
    const addMembersSelectionList = document.getElementById("addMembersSelectionList");
    const btnSubmitAddMembers = document.getElementById("btnSubmitAddMembers");
    const btnLeaveGroup = document.getElementById("btnLeaveGroup");
    const directoryMemberList = document.getElementById("directoryMemberList");

    if (addGroupMembersModal && addMembersSelectionList) {
        addGroupMembersModal.addEventListener("show.bs.modal", async function () {
            if (!activeConvId) return;
            addMembersSelectionList.innerHTML = `<div class="text-center text-muted py-3 small">Đang tải danh sách bạn bè...</div>`;
            try {
                const res = await fetch(`/Group/FriendsToInvite?groupId=${activeConvId}`);
                const data = await res.json();
                const available = (data.data || []).filter(f => !f.isAlreadyMember);
                if (data.success && available.length > 0) {
                    addMembersSelectionList.innerHTML = available.map(f => `
                        <div class="form-check d-flex align-items-center gap-2 py-1 border-bottom">
                            <input class="form-check-input add-member-checkbox" type="checkbox" value="${f.userId}" id="add_m_${f.userId}">
                            <label class="form-check-label d-flex align-items-center gap-2 cursor-pointer flex-grow-1" for="add_m_${f.userId}">
                                <strong>${escapeHtml(f.displayName)}</strong>
                                <span class="text-muted small">(${escapeHtml(f.email)})</span>
                            </label>
                        </div>
                    `).join("");
                } else {
                    addMembersSelectionList.innerHTML = `<div class="text-center text-muted py-3 small">Tất cả bạn bè của bạn đều đã ở trong nhóm này.</div>`;
                }
            } catch (err) {
                addMembersSelectionList.innerHTML = `<div class="text-danger small py-2">Không thể tải danh sách bạn bè.</div>`;
            }
        });
    }

    if (btnSubmitAddMembers) {
        btnSubmitAddMembers.addEventListener("click", async function () {
            const memberIds = Array.from(document.querySelectorAll(".add-member-checkbox:checked")).map(cb => cb.value);
            if (memberIds.length === 0) {
                alert("Vui lòng chọn ít nhất 1 người bạn.");
                return;
            }
            try {
                const res = await fetch("/Group/AddMembers", {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ groupId: activeConvId, memberIds })
                });
                const data = await res.json();
                if (data.success) {
                    window.location.href = `/?conversationId=${activeConvId}`;
                } else {
                    alert(data.message || "Không thể thêm thành viên.");
                }
            } catch (err) {
                alert("Lỗi kết nối khi thêm thành viên.");
            }
        });
    }

    if (directoryMemberList) {
        directoryMemberList.addEventListener("click", async function (e) {
            const btn = e.target.closest(".btn-kick-member");
            if (!btn || !activeConvId) return;
            const memberId = btn.dataset.userId;
            if (!confirm("Bạn có chắc chắn muốn xóa thành viên này khỏi nhóm?")) return;

            try {
                const res = await fetch("/Group/RemoveMember", {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ groupId: activeConvId, memberId })
                });
                const data = await res.json();
                if (data.success) {
                    window.location.href = `/?conversationId=${activeConvId}`;
                } else {
                    alert(data.message || "Không thể xóa thành viên.");
                }
            } catch (err) {
                alert("Lỗi kết nối khi xóa thành viên.");
            }
        });
    }

    if (btnLeaveGroup) {
        btnLeaveGroup.addEventListener("click", async function () {
            if (!activeConvId) return;
            if (!confirm("Bạn có chắc chắn muốn rời khỏi nhóm này?")) return;
            try {
                const res = await fetch("/Group/Leave", {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ groupId: activeConvId })
                });
                const data = await res.json();
                if (data.success) {
                    window.location.href = "/";
                } else {
                    alert(data.message || "Không thể rời nhóm.");
                }
            } catch (err) {
                alert("Lỗi kết nối khi rời nhóm.");
            }
        });
    }

    function getStorageUrl(storageKey) {
        if (!storageKey) return "#";
        const clean = storageKey.replace(/^\/+/, "");
        return clean.toLowerCase().startsWith("uploads/") ? `/${clean}` : `/uploads/${clean}`;
    }

    // 8. CUỘC GỌI THOẠI WEBRTC REAL-TIME QUA SIGNALR
    const btnStartCall = document.getElementById("btnStartCall");
    const callModalEl = document.getElementById("callModal");
    const callPeerName = document.getElementById("callPeerName");
    const callStatusText = document.getElementById("callStatusText");
    const callTimerEl = document.getElementById("callTimer");
    const callControlsIncoming = document.getElementById("callControlsIncoming");
    const callControlsActive = document.getElementById("callControlsActive");
    const btnAcceptCall = document.getElementById("btnAcceptCall");
    const btnRejectCall = document.getElementById("btnRejectCall");
    const btnEndCall = document.getElementById("btnEndCall");
    const btnToggleMute = document.getElementById("btnToggleMute");
    const remoteAudio = document.getElementById("remoteAudio");

    let callModal = callModalEl && window.bootstrap ? new bootstrap.Modal(callModalEl) : null;
    let peerConnection = null;
    let localStream = null;
    let callConvId = null;
    let incomingCallerId = null;
    let callTimerInterval = null;
    let callSeconds = 0;
    let isMuted = false;

    function startCallTimer() {
        stopCallTimer();
        callSeconds = 0;
        if (callTimerEl) {
            callTimerEl.textContent = "00:00";
            callTimerEl.classList.remove("d-none");
        }
        callTimerInterval = setInterval(() => {
            callSeconds++;
            const mins = String(Math.floor(callSeconds / 60)).padStart(2, "0");
            const secs = String(callSeconds % 60).padStart(2, "0");
            if (callTimerEl) callTimerEl.textContent = `${mins}:${secs}`;
        }, 1000);
    }

    function stopCallTimer() {
        if (callTimerInterval) {
            clearInterval(callTimerInterval);
            callTimerInterval = null;
        }
        if (callTimerEl) callTimerEl.classList.add("d-none");
    }

    async function createPeerConnection(targetConvId) {
        const pc = new RTCPeerConnection({
            iceServers: [{ urls: "stun:stun.l.google.com:19302" }]
        });

        pc.onicecandidate = (e) => {
            if (e.candidate) {
                connection.invoke("SendWebRtcSignal", targetConvId, JSON.stringify({ candidate: e.candidate })).catch(() => {});
            }
        };

        pc.ontrack = (e) => {
            if (remoteAudio && e.streams && e.streams[0]) {
                remoteAudio.srcObject = e.streams[0];
            }
        };

        try {
            localStream = await navigator.mediaDevices.getUserMedia({ audio: true, video: false });
            localStream.getTracks().forEach(track => pc.addTrack(track, localStream));
        } catch (err) {
            throw new Error("Không thể truy cập Microphone trên thiết bị này.");
        }

        return pc;
    }

    function cleanupCall() {
        stopCallTimer();
        if (peerConnection) {
            peerConnection.close();
            peerConnection = null;
        }
        if (localStream) {
            localStream.getTracks().forEach(t => t.stop());
            localStream = null;
        }
        if (remoteAudio) remoteAudio.srcObject = null;
        callConvId = null;
        incomingCallerId = null;
        isMuted = false;
        if (btnToggleMute) btnToggleMute.innerHTML = `<i class="bi bi-mic-fill"></i>`;
        if (callModal) callModal.hide();
    }

    function showCallUi(peerName, statusText, isIncoming) {
        if (callPeerName) callPeerName.textContent = peerName || "Cuộc gọi";
        if (callStatusText) callStatusText.textContent = statusText;
        if (callControlsIncoming) callControlsIncoming.style.setProperty("display", isIncoming ? "flex" : "none", "important");
        if (callControlsActive) callControlsActive.style.setProperty("display", isIncoming ? "none" : "flex", "important");
        if (callModal) callModal.show();
    }

    if (btnStartCall) {
        btnStartCall.addEventListener("click", async function () {
            if (!activeConvId) return;
            if (connection.state !== signalR.HubConnectionState.Connected) {
                alert("Chưa kết nối tới máy chủ SignalR.");
                return;
            }
            callConvId = activeConvId;
            const headerName = document.getElementById("chatHeaderName")?.textContent || "Cuộc gọi";
            showCallUi(headerName, "Đang đổ chuông...", false);
            try {
                peerConnection = await createPeerConnection(callConvId);
                await connection.invoke("CallUser", callConvId);
            } catch (err) {
                alert(err.message || "Không thể bắt đầu cuộc gọi.");
                cleanupCall();
            }
        });
    }

    connection.on("IncomingCall", function (data) {
        if (peerConnection) return; // Đang bận
        callConvId = data.conversationId;
        incomingCallerId = data.callerId;
        showCallUi(data.callerName || "Cuộc gọi đến", "Cuộc gọi thoại đến...", true);
    });

    if (btnAcceptCall) {
        btnAcceptCall.addEventListener("click", async function () {
            if (!callConvId || !incomingCallerId) return;
            showCallUi(callPeerName?.textContent, "Đang kết nối âm thanh...", false);
            try {
                peerConnection = await createPeerConnection(callConvId);
                await connection.invoke("AcceptCall", callConvId, incomingCallerId);
            } catch (err) {
                alert(err.message || "Không thể nghe máy.");
                await connection.invoke("RejectCall", callConvId, incomingCallerId, "Thiết bị không có Microphone.").catch(() => {});
                cleanupCall();
            }
        });
    }

    if (btnRejectCall) {
        btnRejectCall.addEventListener("click", async function () {
            if (callConvId && incomingCallerId) {
                await connection.invoke("RejectCall", callConvId, incomingCallerId, "Người nhận từ chối cuộc gọi.").catch(() => {});
            }
            cleanupCall();
        });
    }

    if (btnEndCall) {
        btnEndCall.addEventListener("click", async function () {
            if (callConvId) {
                await connection.invoke("EndCall", callConvId).catch(() => {});
            }
            cleanupCall();
        });
    }

    if (btnToggleMute) {
        btnToggleMute.addEventListener("click", function () {
            if (!localStream) return;
            isMuted = !isMuted;
            localStream.getAudioTracks().forEach(t => { t.enabled = !isMuted; });
            this.innerHTML = isMuted ? `<i class="bi bi-mic-mute-fill text-danger"></i>` : `<i class="bi bi-mic-fill"></i>`;
        });
    }

    connection.on("CallAccepted", async function (data) {
        if (!peerConnection || data.conversationId !== callConvId) return;
        if (callStatusText) callStatusText.textContent = "Đang trò chuyện";
        startCallTimer();
        const offer = await peerConnection.createOffer();
        await peerConnection.setLocalDescription(offer);
        await connection.invoke("SendWebRtcSignal", callConvId, JSON.stringify({ sdp: offer }));
    });

    connection.on("CallRejected", function (data) {
        alert(data.reason || "Cuộc gọi bị từ chối.");
        cleanupCall();
    });

    connection.on("CallEnded", function () {
        cleanupCall();
    });

    connection.on("ReceiveWebRtcSignal", async function (data) {
        if (!peerConnection || data.conversationId !== callConvId) return;
        const signal = JSON.parse(data.signalJson);
        if (signal.sdp) {
            await peerConnection.setRemoteDescription(new RTCSessionDescription(signal.sdp));
            if (signal.sdp.type === "offer") {
                const answer = await peerConnection.createAnswer();
                await peerConnection.setLocalDescription(answer);
                await connection.invoke("SendWebRtcSignal", callConvId, JSON.stringify({ sdp: answer }));
                if (callStatusText) callStatusText.textContent = "Đang trò chuyện";
                startCallTimer();
            }
        } else if (signal.candidate) {
            await peerConnection.addIceCandidate(new RTCIceCandidate(signal.candidate)).catch(() => {});
        }
    });

    function escapeHtml(str) {
        if (!str) return "";
        const div = document.createElement("div");
        div.textContent = str;
        return div.innerHTML;
    }
});
