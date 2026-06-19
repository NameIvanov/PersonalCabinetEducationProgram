// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
window.validateUploadFile = function (input) {
    if (!input.files || input.files.length === 0) {
        return false;
    }

    const maxSize = Number(input.dataset.maxSize || 0);
    if (maxSize > 0 && input.files[0].size > maxSize) {
        input.value = "";
        window.alert("Размер файла не должен превышать 50 МБ.");
        return false;
    }

    return true;
};
