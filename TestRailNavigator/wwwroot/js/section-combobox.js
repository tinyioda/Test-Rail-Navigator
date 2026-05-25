// section-combobox.js
// Wires the _SectionCombobox partial:
//   * type or pick a breadcrumb → resolve to hidden section id
//   * paste a TestRail section URL (`?group_id=123` or `?section_id=123`) → auto-resolve
//   * type only `#123` → match by id suffix
//   * click the 📁 button → open a tree modal built lazily from the JSON blob the partial emits
//
// No network calls — all data is already in the DOM.

(function () {
    function parseSectionIdFromUrlOrText(raw) {
        if (!raw) return null;
        // TestRail uses both group_id (suite browser) and section_id (direct links).
        var m = raw.match(/[?&#](?:group_id|section_id)=(\d+)/i);
        if (m) return m[1];
        // Trailing "(#123)" or just "#123" in pasted text.
        var idMatch = raw.match(/#(\d+)\)?\s*$/);
        if (idMatch) return idMatch[1];
        return null;
    }

    function wire(root) {
        var input = root.querySelector('input[list]');
        if (!input) return;

        var hidden = document.getElementById(input.getAttribute('data-hidden-target'));
        if (!hidden) return;

        var datalist = document.getElementById(input.getAttribute('list'));
        if (!datalist) return;

        var options = Array.prototype.slice.call(datalist.querySelectorAll('option'));

        // Map id → label so URL/id resolution can rewrite the visible text to the full breadcrumb.
        var labelsById = {};
        options.forEach(function (o) {
            var id = o.getAttribute('data-id');
            if (id) labelsById[id] = o.value || '';
        });

        function setById(id) {
            var label = labelsById[id];
            if (label) {
                input.value = label;
                hidden.value = id;
                input.classList.remove('is-invalid');
                return true;
            }
            return false;
        }

        function resolve() {
            var raw = (input.value || '').trim();
            if (!raw) {
                hidden.value = '';
                input.classList.remove('is-invalid');
                return;
            }

            // URL or id-suffix paste first — rewrites the visible text to the full label.
            var pastedId = parseSectionIdFromUrlOrText(raw);
            if (pastedId && setById(pastedId)) return;

            // Exact (case-insensitive) match on the full "Breadcrumb (#id)" label.
            var needle = raw.toLowerCase();
            var hit = options.find(function (o) { return (o.value || '').toLowerCase() === needle; });
            if (hit) {
                hidden.value = hit.getAttribute('data-id') || '';
                input.classList.remove('is-invalid');
                return;
            }

            // Fallback — id hidden inside raw text (e.g. user pasted random text containing "(#123)").
            if (pastedId) {
                hidden.value = pastedId;
                input.classList.remove('is-invalid');
                return;
            }

            // No match — keep hidden empty, mark invalid so required/POST validation catches it.
            hidden.value = '';
            input.classList.add('is-invalid');
        }

        // Tree-modal wiring (lazy build on first open).
        var modalId = input.getAttribute('data-tree-modal');
        if (modalId) {
            var modal = document.getElementById(modalId);
            var json = document.querySelector('script[data-sections-for="' + input.id + '"]');
            if (modal && json) {
                var built = false;
                modal.addEventListener('show.bs.modal', function () {
                    if (!built) {
                        buildTree(modal, json, function (id) {
                            if (setById(id)) {
                                // Dismiss the modal + notify listeners that value changed.
                                input.dispatchEvent(new Event('change', { bubbles: true }));
                                var bs = window.bootstrap && window.bootstrap.Modal && window.bootstrap.Modal.getInstance(modal);
                                if (bs) bs.hide();
                            }
                        });
                        built = true;
                    }
                });
            }
        }

        // Recents (localStorage, per-suite). Renders up to 5 clickable chips of the last-used
        // section ids for this suite so the user doesn't have to re-type a favorite destination.
        var recentsKey = root.getAttribute('data-recents-key') || '';
        var recentsRow = root.querySelector('[data-recents-row]');
        var recentsChips = root.querySelector('[data-recents-chips]');
        var RECENTS_MAX = 5;

        function readRecents() {
            if (!recentsKey || !window.localStorage) return [];
            try {
                var raw = localStorage.getItem(recentsKey);
                var arr = raw ? JSON.parse(raw) : [];
                return Array.isArray(arr) ? arr.filter(function (x) { return typeof x === 'string'; }) : [];
            } catch (e) {
                return [];
            }
        }

        function writeRecents(ids) {
            if (!recentsKey || !window.localStorage) return;
            try { localStorage.setItem(recentsKey, JSON.stringify(ids.slice(0, RECENTS_MAX))); } catch (e) { /* quota */ }
        }

        function renderRecents() {
            if (!recentsRow || !recentsChips) return;
            var ids = readRecents().filter(function (id) { return !!labelsById[id]; });
            if (!ids.length) {
                recentsRow.classList.add('d-none');
                return;
            }
            recentsChips.innerHTML = '';
            ids.forEach(function (id) {
                var label = labelsById[id] || ('#' + id);
                var shortLabel = label.length > 50 ? '…' + label.slice(-47) : label;
                var chip = document.createElement('button');
                chip.type = 'button';
                chip.className = 'btn btn-sm btn-outline-secondary me-1 mb-1';
                chip.textContent = shortLabel;
                chip.title = label;
                chip.addEventListener('click', function () {
                    if (setById(id)) {
                        input.dispatchEvent(new Event('change', { bubbles: true }));
                    }
                });
                recentsChips.appendChild(chip);
            });
            recentsRow.classList.remove('d-none');
        }

        function pushRecent(id) {
            if (!id) return;
            var cur = readRecents().filter(function (x) { return x !== id; });
            cur.unshift(id);
            writeRecents(cur);
            renderRecents();
        }

        renderRecents();

        // When the user commits a value (blur / change), push the resolved id to recents.
        function commitRecent() {
            var id = hidden.value;
            if (id) pushRecent(id);
        }

        input.addEventListener('input', resolve);
        input.addEventListener('change', function () { resolve(); commitRecent(); });
        input.addEventListener('blur', function () { resolve(); commitRecent(); });

        resolve();
    }

    function buildTree(modal, jsonScript, onPick) {
        var root = modal.querySelector('[data-tree-root]');
        var filter = modal.querySelector('[data-tree-filter]');
        if (!root) return;

        var data;
        try {
            data = JSON.parse(jsonScript.textContent || '[]');
        } catch (e) {
            root.textContent = 'Could not load section tree.';
            return;
        }

        // Bucket children under each parent id (null = roots).
        var byParent = {};
        data.forEach(function (s) {
            var key = (s.parent_id == null) ? 'root' : String(s.parent_id);
            (byParent[key] || (byParent[key] = [])).push(s);
        });
        Object.keys(byParent).forEach(function (k) {
            byParent[k].sort(function (a, b) {
                return (a.name || '').toLowerCase().localeCompare((b.name || '').toLowerCase());
            });
        });

        function makeList(parent) {
            var kids = byParent[parent == null ? 'root' : String(parent)];
            if (!kids || !kids.length) return null;

            var ul = document.createElement('ul');
            ul.className = 'list-unstyled ms-3 mb-0 section-tree-list';

            kids.forEach(function (s) {
                var li = document.createElement('li');
                li.className = 'section-tree-node';

                var childList = makeList(s.id);
                var hasChildren = !!childList;

                var row = document.createElement('div');
                row.className = 'd-flex align-items-center';

                var toggle = document.createElement('button');
                toggle.type = 'button';
                toggle.className = 'btn btn-sm btn-link text-decoration-none p-0 me-1';
                toggle.style.width = '1.5rem';
                toggle.textContent = hasChildren ? '▸' : '·';
                toggle.setAttribute('aria-label', 'Expand');

                var pick = document.createElement('button');
                pick.type = 'button';
                pick.className = 'btn btn-sm btn-link text-start flex-grow-1 text-decoration-none';
                pick.textContent = s.name + ' (#' + s.id + ')';
                pick.setAttribute('data-section-id', String(s.id));
                pick.setAttribute('data-section-name', (s.name || '').toLowerCase());
                pick.addEventListener('click', function () { onPick(String(s.id)); });

                row.appendChild(toggle);
                row.appendChild(pick);
                li.appendChild(row);

                if (hasChildren) {
                    childList.style.display = 'none';
                    li.appendChild(childList);
                    toggle.addEventListener('click', function () {
                        var open = childList.style.display !== 'none';
                        childList.style.display = open ? 'none' : 'block';
                        toggle.textContent = open ? '▸' : '▾';
                    });
                } else {
                    toggle.disabled = true;
                }

                ul.appendChild(li);
            });

            return ul;
        }

        var rootList = makeList(null);
        root.innerHTML = '';
        if (rootList) root.appendChild(rootList);
        else root.textContent = 'No sections.';

        if (filter) {
            filter.addEventListener('input', function () {
                var q = (filter.value || '').trim().toLowerCase();
                var allNodes = root.querySelectorAll('.section-tree-node');
                if (!q) {
                    allNodes.forEach(function (n) { n.style.display = ''; });
                    return;
                }
                // Hide nodes whose label + descendant labels don't contain q.
                allNodes.forEach(function (n) {
                    var pick = n.querySelector('[data-section-id]');
                    var name = pick ? pick.getAttribute('data-section-name') : '';
                    var id = pick ? pick.getAttribute('data-section-id') : '';
                    var selfMatch = name.indexOf(q) !== -1 || id.indexOf(q) !== -1;
                    var descendantMatch = Array.prototype.some.call(
                        n.querySelectorAll('[data-section-id]'),
                        function (p) {
                            return (p.getAttribute('data-section-name') || '').indexOf(q) !== -1
                                || (p.getAttribute('data-section-id') || '').indexOf(q) !== -1;
                        });
                    n.style.display = (selfMatch || descendantMatch) ? '' : 'none';
                    // Auto-expand matching branches.
                    if (selfMatch || descendantMatch) {
                        var sub = n.querySelector('ul.section-tree-list');
                        if (sub) sub.style.display = 'block';
                    }
                });
            });
        }
    }

    function init() {
        document.querySelectorAll('[data-section-combobox]').forEach(wire);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
