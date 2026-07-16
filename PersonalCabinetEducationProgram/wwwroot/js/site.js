// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
window.validateUploadFile = function (input) {
    if (!input.files || input.files.length === 0) {
        return false;
    }

    const maxSize = Number(input.dataset.maxSize || 0);
    if (input.files.length > 20) {
        input.value = "";
        window.alert("За один раз можно выбрать не более 20 файлов.");
        return false;
    }

    for (const file of input.files) {
        if (maxSize > 0 && file.size > maxSize) {
            input.value = "";
            window.alert(`Файл «${file.name}» превышает допустимый размер 50 МБ.`);
            return false;
        }
    }

    return true;
};

document.addEventListener("DOMContentLoaded", function () {
    const collator = new Intl.Collator("ru", {
        numeric: true,
        sensitivity: "base"
    });

    document.querySelectorAll("[data-toggle-element-filters], [data-toggle-live-filters]").forEach(button => {
        button.addEventListener("click", function () {
            const rows = document.querySelectorAll(".live-filter-row");
            const shouldShow = Array.from(rows).some(row => row.classList.contains("d-none"));
            rows.forEach(row => row.classList.toggle("d-none", !shouldShow));
            button.setAttribute("aria-expanded", shouldShow ? "true" : "false");
        });
    });

    initializeLiveFilters();

    document.querySelectorAll("table").forEach(initializeSortableTable);

    function initializeLiveFilters() {
        const focusStorageKey = "liveFilterFocus";
        const timers = new WeakMap();

        restoreFilterFocus();

        document.querySelectorAll(".live-filter-row input[type='search']").forEach(input => {
            input.addEventListener("input", function () {
                scheduleSubmit(input, 600, true);
            });
        });

        document.querySelectorAll(".live-filter-row input[type='number']").forEach(input => {
            input.addEventListener("input", function () {
                if (input.validity.badInput || !input.validity.valid) {
                    return;
                }

                scheduleSubmit(input, 800, true);
            });
        });

        document.querySelectorAll(".live-filter-row select").forEach(control => {
            control.addEventListener("change", function () {
                scheduleSubmit(control, 0, false);
            });
        });

        document.querySelectorAll(".live-filter-row input[type='date']").forEach(control => {
            control.addEventListener("input", function () {
                if (control.validity.badInput ||
                    (control.value !== "" &&
                        (!/^\d{4}-\d{2}-\d{2}$/.test(control.value) || !control.validity.valid))) {
                    return;
                }

                scheduleSubmit(control, 1200, false);
            });
        });

        function scheduleSubmit(control, delay, rememberFocus) {
            const form = control.form;
            if (!form) {
                return;
            }

            const previousTimer = timers.get(form);
            if (previousTimer) {
                window.clearTimeout(previousTimer);
            }

            const timer = window.setTimeout(function () {
                ensureFiltersStayVisible(form);
                if (rememberFocus) {
                    try {
                        sessionStorage.setItem(focusStorageKey, JSON.stringify({
                            path: window.location.pathname,
                            formId: form.id,
                            name: control.name,
                            selectionStart: control.selectionStart ?? control.value.length
                        }));
                    } catch {
                        // Search still works when session storage is disabled.
                    }
                }

                form.requestSubmit();
            }, delay);

            timers.set(form, timer);
        }

        function ensureFiltersStayVisible(form) {
            if (form.querySelector("input[name='ShowFilters']")) {
                return;
            }

            const input = document.createElement("input");
            input.type = "hidden";
            input.name = "ShowFilters";
            input.value = "true";
            form.appendChild(input);
        }

        function restoreFilterFocus() {
            const rawState = sessionStorage.getItem(focusStorageKey);
            if (!rawState) {
                return;
            }

            sessionStorage.removeItem(focusStorageKey);
            try {
                const state = JSON.parse(rawState);
                if (state.path !== window.location.pathname) {
                    return;
                }

                const form = document.getElementById(state.formId);
                const control = form?.querySelector(`[name="${CSS.escape(state.name)}"]`) ??
                    document.querySelector(`[form="${CSS.escape(state.formId)}"][name="${CSS.escape(state.name)}"]`);
                if (!(control instanceof HTMLInputElement)) {
                    return;
                }

                control.focus({ preventScroll: true });
                if (control.type === "search" || control.type === "text") {
                    const cursor = Math.min(Number(state.selectionStart) || control.value.length, control.value.length);
                    control.setSelectionRange(cursor, cursor);
                }
            } catch {
                sessionStorage.removeItem(focusStorageKey);
            }
        }
    }

    function initializeSortableTable(table) {
        const headerRow = Array.from(table.tHead?.rows ?? [])
            .find(row => !row.classList.contains("live-filter-row"));
        if (!headerRow || table.tBodies.length === 0) {
            return;
        }

        Array.from(headerRow.cells).forEach((header, columnIndex) => {
            const title = header.textContent.trim().toLocaleLowerCase("ru");
            if (header.querySelector("a[href]")) {
                header.classList.add("table-sort-server");
                return;
            }
            const disabled = header.dataset.sortable === "false" || title === "действия";
            if (disabled) {
                header.classList.add("table-sort-disabled");
                return;
            }

            header.classList.add("table-sortable");
            header.tabIndex = 0;
            header.setAttribute("role", "button");
            header.setAttribute("aria-sort", "none");
            header.setAttribute("title", "Сортировать по столбцу");

            const indicator = document.createElement("span");
            indicator.className = "table-sort-indicator";
            indicator.setAttribute("aria-hidden", "true");
            indicator.textContent = "↕";
            header.appendChild(indicator);

            const sort = () => sortTable(table, headerRow, header, columnIndex);
            header.addEventListener("click", sort);
            header.addEventListener("keydown", event => {
                if (event.key === "Enter" || event.key === " ") {
                    event.preventDefault();
                    sort();
                }
            });
        });
    }

    function sortTable(table, headerRow, activeHeader, columnIndex) {
        const sameColumn = table.dataset.sortColumn === String(columnIndex);
        const direction = sameColumn && table.dataset.sortDirection === "asc" ? "desc" : "asc";
        table.dataset.sortColumn = String(columnIndex);
        table.dataset.sortDirection = direction;

        Array.from(headerRow.cells).forEach(header => {
            if (!header.classList.contains("table-sortable")) {
                return;
            }

            const isActive = header === activeHeader;
            header.setAttribute("aria-sort", isActive ? (direction === "asc" ? "ascending" : "descending") : "none");
            const indicator = header.querySelector(".table-sort-indicator");
            if (indicator) {
                indicator.textContent = isActive ? (direction === "asc" ? "↑" : "↓") : "↕";
            }
        });

        Array.from(table.tBodies).forEach(tbody => {
            const allRows = Array.from(tbody.rows);
            const sortableRows = allRows.filter(row =>
                row.cells.length > columnIndex &&
                !row.hasAttribute("data-sort-fixed") &&
                !row.querySelector("td[colspan]"));
            const fixedRows = allRows.filter(row => !sortableRows.includes(row));

            sortableRows.sort((leftRow, rightRow) => {
                const left = getSortValue(leftRow.cells[columnIndex]);
                const right = getSortValue(rightRow.cells[columnIndex]);
                if (left.type === "empty" && right.type === "empty") {
                    return 0;
                }
                if (left.type === "empty") {
                    return 1;
                }
                if (right.type === "empty") {
                    return -1;
                }
                const comparison = compareValues(left, right);
                return direction === "asc" ? comparison : -comparison;
            });

            sortableRows.concat(fixedRows).forEach(row => tbody.appendChild(row));
        });
    }

    function getSortValue(cell) {
        const raw = (cell.dataset.sortValue ?? cell.innerText ?? cell.textContent)
            .replace(/\s+/g, " ")
            .trim();

        if (raw === "" || raw === "—" || raw === "-") {
            return { type: "empty", value: "" };
        }

        const date = parseRussianDate(raw);
        if (date !== null) {
            return { type: "date", value: date };
        }

        const normalizedNumber = raw.replace(/\s/g, "").replace(",", ".");
        if (/^[+-]?\d+(?:\.\d+)?$/.test(normalizedNumber)) {
            return { type: "number", value: Number(normalizedNumber) };
        }

        return { type: "text", value: raw };
    }

    function parseRussianDate(value) {
        const match = value.match(/^(\d{2})\.(\d{2})\.(\d{4})(?:\s+(\d{2}):(\d{2})(?::(\d{2}))?)?$/);
        if (!match) {
            return null;
        }

        const [, day, month, year, hour = "0", minute = "0", second = "0"] = match;
        return Date.UTC(Number(year), Number(month) - 1, Number(day), Number(hour), Number(minute), Number(second));
    }

    function compareValues(left, right) {
        if (left.type === right.type && (left.type === "number" || left.type === "date")) {
            return left.value - right.value;
        }

        return collator.compare(String(left.value), String(right.value));
    }
});
