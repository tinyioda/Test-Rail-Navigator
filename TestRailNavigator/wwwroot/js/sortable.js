document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('th.sortable').forEach(function (th) {
        th.addEventListener('click', function () {
            var table = th.closest('table');
            var tbody = table.querySelector('tbody');
            if (!tbody) return;
            var rows = Array.from(tbody.querySelectorAll(':scope > tr'));
            var colIndex = Array.from(th.parentElement.children).indexOf(th);
            var sortType = th.getAttribute('data-sort-type') || 'text';

            var isAsc = th.classList.contains('sort-asc');

            table.querySelectorAll('th.sortable').forEach(function (h) {
                h.classList.remove('sort-asc', 'sort-desc');
            });

            if (isAsc) {
                th.classList.add('sort-desc');
            } else {
                th.classList.add('sort-asc');
            }
            var direction = isAsc ? -1 : 1;

            rows.sort(function (a, b) {
                var cellA = a.children[colIndex];
                var cellB = b.children[colIndex];
                if (!cellA || !cellB) return 0;
                var valA = (cellA.textContent || '').trim();
                var valB = (cellB.textContent || '').trim();

                if (sortType === 'number') {
                    var numA = parseFloat(valA) || 0;
                    var numB = parseFloat(valB) || 0;
                    return (numA - numB) * direction;
                } else if (sortType === 'date') {
                    var dateA = valA === '\u2014' ? '' : valA;
                    var dateB = valB === '\u2014' ? '' : valB;
                    if (dateA === dateB) return 0;
                    if (dateA === '') return 1;
                    if (dateB === '') return -1;
                    return dateA.localeCompare(dateB) * direction;
                } else {
                    return valA.localeCompare(valB, undefined, { sensitivity: 'base' }) * direction;
                }
            });

            rows.forEach(function (row) {
                tbody.appendChild(row);
            });
        });
    });
});
