(() => {
    const polling = document.getElementById('documentAnalysisPolling');
    if (!polling) return;
    const message = document.getElementById('analysisMessage');
    const poll = async () => {
        try {
            const response = await fetch(polling.dataset.statusUrl, { credentials: 'same-origin', cache: 'no-store' });
            if (!response.ok || !response.headers.get('content-type')?.includes('application/json')) {
                message.textContent = 'Не удалось обновить состояние. Проверьте доступ или обновите страницу позже.';
                return;
            }
            const status = await response.json();
            if (!status.pending) { window.location.reload(); return; }
            message.textContent = status.message;
            window.setTimeout(poll, 5000);
        } catch {
            message.textContent = 'Связь прервана. Проверка продолжится на сервере; обновите страницу позже.';
        }
    };
    window.setTimeout(poll, 5000);
})();
