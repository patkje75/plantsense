// Docs page enhancements: copy-to-clipboard on code blocks.

// navigator.clipboard requires a secure context (HTTPS/localhost); PlantSense is commonly
// reached over plain http://<PI_IP>:8080, so fall back to the classic textarea+execCommand
// trick when the modern API isn't available.
function legacyCopy(text) {
    var textarea = document.createElement('textarea');
    textarea.value = text;
    textarea.style.position = 'fixed';
    textarea.style.opacity = '0';
    document.body.appendChild(textarea);
    textarea.focus();
    textarea.select();
    try {
        document.execCommand('copy');
    } catch (e) {
        // Nothing more we can do — the copy silently fails on very old/locked-down browsers.
    }
    document.body.removeChild(textarea);
}

function copyToClipboard(text, feedbackEl) {
    function flashCopied() {
        if (!feedbackEl) return;
        var original = feedbackEl.textContent;
        feedbackEl.textContent = 'Copied!';
        setTimeout(function () { feedbackEl.textContent = original; }, 1200);
    }

    if (navigator.clipboard && window.isSecureContext) {
        navigator.clipboard.writeText(text).then(flashCopied).catch(function () {
            legacyCopy(text);
            flashCopied();
        });
    } else {
        legacyCopy(text);
        flashCopied();
    }
}

function initCodeCopyButtons() {
    document.querySelectorAll('pre.bg-light').forEach(function (pre) {
        var code = pre.querySelector('code');
        if (!code || pre.classList.contains('docs-code-wrap')) return;

        pre.classList.add('docs-code-wrap');
        var btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'btn btn-sm btn-outline-secondary copy-btn';
        btn.textContent = 'Copy';
        btn.setAttribute('aria-label', 'Copy code block to clipboard');
        btn.onclick = function () { copyToClipboard(code.textContent, btn); };
        pre.appendChild(btn);
    });
}

document.addEventListener('DOMContentLoaded', function () {
    initCodeCopyButtons();
});
