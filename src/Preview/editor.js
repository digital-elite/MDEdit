// The preview's edit layer.
//
// Elements the renderer marked with a source range are made editable; anything
// else is read-only by construction. When one of them changes, its content is
// converted back to inline markdown and posted to the host, which splices it
// over the range the element came from.
//
// The listeners are delegated from the document and registered once, so they
// survive the body being replaced wholesale on every render and never need to
// be re-attached.
(function () {
    'use strict';

    var mdedit = window.mdedit = window.mdedit || {};

    var START_ATTRIBUTE = 'data-md-start';
    var END_ATTRIBUTE = 'data-md-end';
    var EDITABLE_SELECTOR = '[' + START_ATTRIBUTE + ']';
    var SEND_DELAY_MS = 300;

    var pendingElement = null;
    var pendingTimer = 0;

    // True while an IME composition is open. The intermediate text is not yet
    // what the user means, so nothing is sent until it closes.
    var composing = false;

    // True while the host is replacing the body. Guards against treating our
    // own render as if it were the user typing.
    var applyingContent = false;

    // Identifies the render the current ranges belong to. The host stamps it on
    // every setContent and rejects edits carrying an older one, so offsets left
    // over from a superseded render can never be spliced into the source.
    var generation = 0;

    // Whether an edit has been accepted since the last full render. Drives the
    // resync request when the caret finally leaves.
    var dirtySinceRender = false;

    function post(message) {
        if (!window.chrome || !window.chrome.webview) return;
        window.chrome.webview.postMessage(JSON.stringify(message));
    }

    function editableFor(node) {
        if (!node) return null;
        var element = node.nodeType === 1 ? node : node.parentElement;
        return element ? element.closest(EDITABLE_SELECTOR) : null;
    }

    function cancelPending() {
        if (pendingTimer) {
            clearTimeout(pendingTimer);
            pendingTimer = 0;
        }
        pendingElement = null;
    }

    function flush() {
        var element = pendingElement;
        cancelPending();

        if (!element || composing) return;

        // A re-render between the edit and this flush detached the element, so
        // its range no longer describes anything in the current document.
        if (!element.isConnected) return;

        dirtySinceRender = true;

        post({
            type: 'edit',
            generation: generation,
            start: parseInt(element.getAttribute(START_ATTRIBUTE), 10),
            end: parseInt(element.getAttribute(END_ATTRIBUTE), 10),
            markdown: mdedit.toInlineMarkdown(element)
        });
    }

    function schedule(element) {
        // Moving to a different block must not discard the edit to the previous
        // one that is still sitting in the debounce.
        if (pendingElement && pendingElement !== element) flush();

        pendingElement = element;
        if (pendingTimer) clearTimeout(pendingTimer);
        pendingTimer = setTimeout(flush, SEND_DELAY_MS);
    }

    document.addEventListener('input', function (event) {
        if (applyingContent || composing) return;
        var element = editableFor(event.target);
        if (element) schedule(element);
    });

    document.addEventListener('compositionstart', function () {
        composing = true;
    });

    document.addEventListener('compositionend', function (event) {
        composing = false;
        var element = editableFor(event.target);
        if (element) schedule(element);
    });

    // Leaving the block, or the window, must not lose a debounced edit.
    document.addEventListener('focusout', function (event) {
        flush();

        if (!dirtySinceRender) return;

        // Moving straight into another block is still editing; re-rendering now
        // would replace the element the caret just landed in.
        if (editableFor(event.relatedTarget)) return;

        // Edited text can reparse into something else entirely - a paragraph
        // starting with "# " is a heading now - and the preview cannot show
        // that without being rebuilt. Once the caret is gone, it is safe to.
        dirtySinceRender = false;
        post({ type: 'resync', generation: generation });
    });

    // Alt-tabbing away is not the same as leaving the block: flush the pending
    // edit, but leave the caret and the rendering alone.
    window.addEventListener('blur', function () { flush(); });

    document.addEventListener('keydown', function (event) {
        if (event.key !== 'Enter') return;
        if (!editableFor(event.target)) return;

        // Splitting or joining blocks is a structural change, and a structural
        // change cannot be expressed as a replacement of one source range.
        // Refusing it outright beats corrupting the document quietly.
        event.preventDefault();
    });

    document.addEventListener('paste', function (event) {
        var element = editableFor(event.target);
        if (!element) return;

        // Pasting rich content would drop arbitrary markup into the block, so
        // take the plain text only.
        event.preventDefault();
        var clipboard = event.clipboardData || window.clipboardData;
        var text = clipboard ? clipboard.getData('text') : '';
        if (!text) return;

        // Newlines would split the block, or break the row when the block is a
        // table cell. The user cannot type one either, so paste matches.
        document.execCommand('insertText', false, text.replace(/\s*\r?\n\s*/g, ' '));
    });

    document.addEventListener('drop', function (event) {
        // A drop carries markup the same way a rich paste does.
        if (editableFor(event.target)) event.preventDefault();
    });

    /// Replaces the rendered content and marks the editable blocks. The host
    /// calls this instead of assigning to innerHTML so that the edit layer
    /// stays in charge of what the new content means.
    mdedit.setContent = function (html, contentGeneration) {
        applyingContent = true;
        try {
            cancelPending();
            generation = contentGeneration;
            dirtySinceRender = false;
            document.body.innerHTML = html;

            var elements = document.querySelectorAll(EDITABLE_SELECTOR);
            for (var i = 0; i < elements.length; i++) {
                elements[i].setAttribute('contenteditable', 'true');
            }
        } finally {
            applyingContent = false;
        }
    };

    /// Shifts the recorded source ranges to account for an edit the host has
    /// accepted. This is what lets an edit be applied without re-rendering:
    /// the caret survives because the DOM is left alone, and the ranges stay
    /// truthful because they move by the length the edit changed.
    mdedit.applyEdit = function (start, oldEnd, newEnd) {
        var delta = newEnd - oldEnd;
        var elements = document.querySelectorAll(EDITABLE_SELECTOR);

        for (var i = 0; i < elements.length; i++) {
            var element = elements[i];
            var elementStart = parseInt(element.getAttribute(START_ATTRIBUTE), 10);
            var elementEnd = parseInt(element.getAttribute(END_ATTRIBUTE), 10);

            if (elementStart === start && elementEnd === oldEnd) {
                element.setAttribute(END_ATTRIBUTE, String(newEnd));
            } else if (elementStart > oldEnd) {
                element.setAttribute(START_ATTRIBUTE, String(elementStart + delta));
                element.setAttribute(END_ATTRIBUTE, String(elementEnd + delta));
            }
        }
    };

    /// Whether the caret is currently inside an editable block. The host uses
    /// this to decide when a full re-render is safe.
    mdedit.isEditing = function () {
        return !!editableFor(document.activeElement);
    };
})();
