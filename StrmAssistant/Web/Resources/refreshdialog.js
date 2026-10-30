// YingYi P3-4 MVP: display read-only missing episode placeholders only.
// The server gates this resource with EnhanceMissingEpisodes (default OFF).
(function () {
    'use strict';

    function getContext(dialog) {
        var seriesId = dialog.getAttribute('data-yingyi-series-id');
        var seasonNumber = dialog.getAttribute('data-yingyi-season-number');
        if (!seriesId || seasonNumber === null || seasonNumber === '') return null;
        return { seriesId: seriesId, seasonNumber: seasonNumber };
    }

    function render(dialog, payload) {
        if (!payload || !Array.isArray(payload.Episodes) || !payload.Episodes.length) return;
        if (dialog.querySelector('.yingyi-missing-episodes')) return;

        var container = document.createElement('div');
        container.className = 'yingyi-missing-episodes';
        container.setAttribute('role', 'status');
        container.setAttribute('aria-label', 'Missing episodes');

        var title = document.createElement('p');
        title.textContent = 'Missing episodes (read-only)';
        container.appendChild(title);

        payload.Episodes.forEach(function (episode) {
            var item = document.createElement('span');
            item.className = 'yingyi-missing-episode';
            item.textContent = 'S' + episode.SeasonNumber + 'E' + episode.EpisodeNumber + ' (missing)';
            container.appendChild(item);
        });
        dialog.appendChild(container);
    }

    function resolveApiUrl(path) {
        // Prefer the official ApiClient: it knows the real application root
        // (usually "/emby") and keeps the request same-origin and authenticated.
        try {
            if (window.ApiClient && typeof window.ApiClient.getUrl === 'function') {
                return window.ApiClient.getUrl(path);
            }
        } catch (e) { /* fall through to the heuristic below */ }
        // Best-effort fallback: keep the request inside the same application root
        // as the current page ("/emby/web/..." -> "/emby/...", "/web/..." -> "/...").
        // A wrong guess only yields a failed fetch, which renders no placeholder.
        var first = (window.location && window.location.pathname || '/').split('/').filter(Boolean)[0] || '';
        return /^emby/i.test(first) ? '/' + first + path : path;
    }

    document.addEventListener('dialogopen', function (event) {
        var dialog = event.target;
        if (!dialog || !dialog.querySelector) return;

        var context = getContext(dialog);
        if (!context) return;

        var path = '/Shows/' + encodeURIComponent(context.seriesId) +
            '/MissingEpisodes?SeasonNumber=' + encodeURIComponent(context.seasonNumber);
        if (typeof window.fetch !== 'function') return;

        window.fetch(resolveApiUrl(path), { credentials: 'same-origin' })
            .then(function (response) { return response.ok ? response.json() : null; })
            .then(function (payload) { render(dialog, payload); })
            .catch(function () { /* advisory UI: never affect refresh/playback */ });
    });
}());
