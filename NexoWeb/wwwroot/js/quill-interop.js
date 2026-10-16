(function () {
    'use strict';

    const _quills = {};

    window.quillInterop = {

        init: function (id, readOnly, initialHtml) {
            if (_quills[id]) {
                delete _quills[id];
            }
            // Clean up any Quill DOM left from a previous init on same element
            var container = document.getElementById(id);
            if (container) {
                var oldToolbar = container.previousElementSibling;
                if (oldToolbar && oldToolbar.classList.contains('ql-toolbar')) {
                    oldToolbar.parentNode.removeChild(oldToolbar);
                }
                container.innerHTML = '';
                container.className = '';
            }

            var toolbarOptions = readOnly ? false : [
                [{ 'size': ['small', false, 'large', 'huge'] }],
                ['bold', 'italic', 'underline', 'strike'],
                [{ 'color': [] }, { 'background': [] }],
                [{ 'header': [1, 2, 3, false] }],
                [{ 'align': [] }],
                [{ 'list': 'ordered' }, { 'list': 'bullet' }],
                [{ 'indent': '-1' }, { 'indent': '+1' }],
                ['clean']
            ];

            var q = new Quill('#' + id, {
                theme: 'snow',
                readOnly: !!readOnly,
                modules: { toolbar: toolbarOptions },
                placeholder: 'Escribe el contenido del documento aquí...'
            });

            if (initialHtml) {
                q.root.innerHTML = initialHtml;
            }

            _quills[id] = q;
        },

        getHtml: function (id) {
            var q = _quills[id];
            if (!q) return '';
            var html = q.root.innerHTML.trim();
            return (html === '<p><br></p>' || html === '') ? '' : html;
        },

        setHtml: function (id, html) {
            var q = _quills[id];
            if (!q) return;
            q.root.innerHTML = html || '';
        },

        insertText: function (id, text) {
            var q = _quills[id];
            if (!q) return;
            q.focus();
            var range = q.getSelection();
            var index = range ? range.index : Math.max(0, q.getLength() - 1);
            q.insertText(index, text, 'user');
            q.setSelection(index + text.length, 0);
        },

        destroy: function (id) {
            delete _quills[id];
        }
    };
})();
