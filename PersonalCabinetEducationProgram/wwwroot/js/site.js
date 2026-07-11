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

    document.querySelectorAll("table").forEach(initializeSortableTable);

    function initializeSortableTable(table) {
        const headerRow = table.tHead?.rows[table.tHead.rows.length - 1];
        if (!headerRow || table.tBodies.length === 0) {
            return;
        }

        Array.from(headerRow.cells).forEach((header, columnIndex) => {
            const title = header.textContent.trim().toLocaleLowerCase("ru");
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
