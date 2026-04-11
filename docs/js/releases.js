/* Simitone Releases — renders release list from GitHub API */

(function () {
    var container = document.getElementById('releases-container');
    if (!container) return;

    var REPO = 'alexjyong/simitone';
    var API_URL = 'https://api.github.com/repos/' + REPO + '/releases';
    var GITHUB_RELEASES = 'https://github.com/' + REPO + '/releases';

    function showError() {
        container.innerHTML =
            '<div class="releases-error">' +
                '<p>Unable to load releases from GitHub.</p>' +
                '<a href="' + GITHUB_RELEASES + '" class="btn btn-primary" target="_blank" rel="noopener">' +
                    'View Releases on GitHub' +
                '</a>' +
            '</div>';
    }

    function formatDate(dateStr) {
        var d = new Date(dateStr);
        var months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
        return months[d.getMonth()] + ' ' + d.getDate() + ', ' + d.getFullYear();
    }

    /* Simple markdown → HTML renderer (handles what GitHub release notes use) */
    function renderMarkdown(md) {
        if (!md || !md.trim()) return '<p class="release-notes-empty">No release notes provided.</p>';

        var lines = md.split('\n');
        var html = '';
        var inList = false;

        for (var i = 0; i < lines.length; i++) {
            var line = lines[i];

            // Empty line → close list, add paragraph break
            if (!line.trim()) {
                if (inList) { html += '</ul>'; inList = false; }
                continue;
            }

            // Headings: ### text
            var headingMatch = line.match(/^(#{1,6})\s+(.+)/);
            if (headingMatch) {
                if (inList) { html += '</ul>'; inList = false; }
                var level = headingMatch[1].length;
                html += '<h4>' + inlineFormat(headingMatch[2]) + '</h4>';
                continue;
            }

            // List item: - text or * text
            var listMatch = line.match(/^\s*[-*]\s+(.+)/);
            if (listMatch) {
                if (!inList) { html += '<ul>'; inList = true; }
                html += '<li>' + inlineFormat(listMatch[1]) + '</li>';
                continue;
            }

            // Regular paragraph
            if (inList) { html += '</ul>'; inList = false; }
            html += '<p>' + inlineFormat(line) + '</p>';
        }
        if (inList) html += '</ul>';
        return html;
    }

    /* Inline formatting: **bold**, *italic*, `code`, [link](url), raw URLs */
    function inlineFormat(text) {
        // Escape HTML first (prevent injection)
        text = text
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;');

        return text
            // Inline code: `text` — do first, protect contents
            .replace(/`([^`]+)`/g, function (match, code) {
                return '<code>' + code + '</code>';
            })
            // Markdown links: [text](url)
            .replace(/\[([^\]]+)\]\((https?:\/\/[^\s)]+)\)/g, '<a href="$2" target="_blank" rel="noopener">$1</a>')
            // Raw URLs — must not be inside an href attribute or already linked
            .replace(/(?<![="'])https?:\/\/[^\s"<>]+/g, '<a href="$&" target="_blank" rel="noopener">$&</a>')
            // Bold: **text**
            .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
            // Italic: *text*
            .replace(/\*([^*]+)\*/g, '<em>$1</em>');
    }

    function getDownloadUrl(assets, keyword) {
        for (var i = 0; i < assets.length; i++) {
            if (assets[i].name.indexOf(keyword) !== -1) {
                return assets[i].browser_download_url;
            }
        }
        return null;
    }

    function renderRelease(release) {
        var tag = release.tag_name;
        var name = release.name || tag;
        var date = formatDate(release.published_at);
        var body = renderMarkdown(release.body);
        var windowsUrl = getDownloadUrl(release.assets, 'Windows') ||
                         getDownloadUrl(release.assets, 'windows');
        var linuxUrl = getDownloadUrl(release.assets, 'Linux') ||
                       getDownloadUrl(release.assets, 'linux');

        var downloadsHtml = '';
        if (windowsUrl || linuxUrl) {
            downloadsHtml = '<div class="release-downloads">';
            if (windowsUrl) {
                downloadsHtml += '<a href="' + windowsUrl + '" class="btn btn-sm btn-primary" target="_blank" rel="noopener">Download Windows</a>';
            }
            if (linuxUrl) {
                downloadsHtml += '<a href="' + linuxUrl + '" class="btn btn-sm btn-secondary" target="_blank" rel="noopener">Download Linux</a>';
            }
            downloadsHtml += '</div>';
        }

        return '<article class="release-card">' +
            '<div class="release-header">' +
                '<div class="release-title-group">' +
                    '<h2 class="release-title">' + name + '</h2>' +
                '</div>' +
                '<span class="release-date">' + date + '</span>' +
            '</div>' +
            '<div class="release-body">' + body + '</div>' +
            downloadsHtml +
        '</article>';
    }

    fetch(API_URL)
        .then(function (res) {
            if (!res.ok) throw new Error('HTTP ' + res.status);
            return res.json();
        })
        .then(function (releases) {
            if (!releases || releases.length === 0) {
                container.innerHTML = '<p>No releases found.</p>';
                return;
            }
            // Filter out prereleases
            var stable = releases.filter(function (r) { return !r.prerelease; });
            if (stable.length === 0) {
                container.innerHTML =
                    '<p>No stable releases found.</p>' +
                    '<p>You can also <a href="' + GITHUB_RELEASES + '" target="_blank" rel="noopener">browse releases on GitHub</a> directly.</p>';
                return;
            }
            var html = '';
            for (var i = 0; i < stable.length; i++) {
                html += renderRelease(stable[i]);
            }
            container.innerHTML = html;
        })
        .catch(function () {
            showError();
        });
})();
