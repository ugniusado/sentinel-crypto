window.SentinelCrypto = {

    // ── Keyboard shortcuts ─────────────────────────────────────────────────
    registerShortcuts(dotnetRef) {
        const handler = (e) => {
            // Don't fire inside inputs
            const tag = document.activeElement?.tagName;
            const inInput = tag === 'INPUT' || tag === 'TEXTAREA';

            if ((e.metaKey || e.ctrlKey) && e.key === 'k') {
                e.preventDefault();
                dotnetRef.invokeMethodAsync('ToggleCommandPalette');
                return;
            }
            if (e.key === 'Escape') {
                dotnetRef.invokeMethodAsync('OnEscape');
                return;
            }
            if (!inInput && !e.metaKey && !e.ctrlKey && !e.altKey && !e.shiftKey) {
                const n = parseInt(e.key);
                if (n >= 1 && n <= 4) {
                    dotnetRef.invokeMethodAsync('SetView', n - 1);
                }
            }
        };
        document.addEventListener('keydown', handler);
        // Return cleanup handle
        return { dispose: () => document.removeEventListener('keydown', handler) };
    },

    disposeHandle(handle) {
        if (handle && typeof handle.dispose === 'function') handle.dispose();
    },

    // ── Utility ────────────────────────────────────────────────────────────
    focusSelector(selector) {
        setTimeout(() => document.querySelector(selector)?.focus(), 50);
    },

    applyBodyClass(cls, add) {
        document.body.classList.toggle(cls, add);
    }
};
