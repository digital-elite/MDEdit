// Converts the inline content of a rendered preview element back into markdown.
//
// Only inline constructs are handled on purpose. An editable element always
// corresponds to a source range holding inline markdown and nothing else, so
// block structure never reaches this code. Anything unrecognised is unwrapped
// to its text rather than guessed at, which keeps the output valid markdown
// even when the DOM contains something this converter does not model.
(function () {
    'use strict';

    var mdedit = window.mdedit = window.mdedit || {};

    var NODE_ELEMENT = 1;
    var NODE_TEXT = 3;

    function isAlphanumeric(ch) {
        return (ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9');
    }

    // An underscore only opens emphasis at a word boundary, so escaping every
    // one of them would turn snake_case identifiers into a mess of backslashes.
    function underscoreIsSignificant(text, i) {
        var before = i > 0 ? text.charAt(i - 1) : ' ';
        var after = i + 1 < text.length ? text.charAt(i + 1) : ' ';
        return !isAlphanumeric(before) || !isAlphanumeric(after);
    }

    // A lone tilde is ordinary prose ("~5 minutes"); only a doubled one is
    // strikethrough and needs escaping.
    function tildeIsSignificant(text, i) {
        return text.charAt(i - 1) === '~' || text.charAt(i + 1) === '~';
    }

    // A bare ampersand is fine, but one that heads something entity-shaped
    // would be decoded on the way back in and the literal text would be lost.
    function ampersandIsSignificant(text, i) {
        return /^&#?[a-zA-Z0-9]+;/.test(text.slice(i));
    }

    var ALWAYS_ESCAPED = '\\*`[]<>|';

    // A blank line would close the block being edited and split it in two,
    // which cannot be expressed as a replacement of a single source range.
    // Whatever produced it - a paste, a stray newline - it collapses here.
    function collapseBlankLines(text) {
        return text.replace(/[ \t]*\r?\n(?:[ \t]*\r?\n)+[ \t]*/g, '\n');
    }

    function escapeText(text) {
        var out = '';
        for (var i = 0; i < text.length; i++) {
            var ch = text.charAt(i);
            var escape =
                ALWAYS_ESCAPED.indexOf(ch) >= 0 ||
                (ch === '_' && underscoreIsSignificant(text, i)) ||
                (ch === '~' && tildeIsSignificant(text, i)) ||
                (ch === '&' && ampersandIsSignificant(text, i));
            out += escape ? '\\' + ch : ch;
        }
        return out;
    }

    // Code spans are literal, so the delimiter has to be longer than the
    // longest backtick run inside them, and content touching a backtick or a
    // space needs padding that the parser will strip back off.
    function codeSpan(text) {
        if (text === '') return '';

        var longestRun = 0;
        var run = 0;
        for (var i = 0; i < text.length; i++) {
            if (text.charAt(i) === '`') {
                run++;
                if (run > longestRun) longestRun = run;
            } else {
                run = 0;
            }
        }

        var fence = new Array(longestRun + 2).join('`');
        var first = text.charAt(0);
        var last = text.charAt(text.length - 1);
        var pad = (first === '`' || last === '`' || first === ' ' || last === ' ') ? ' ' : '';

        return fence + pad + text + pad + fence;
    }

    // Destinations containing spaces or unbalanced parens have to be wrapped in
    // angle brackets or the link will not parse back.
    function destination(url) {
        var balanced = 0;
        for (var i = 0; i < url.length; i++) {
            if (url.charAt(i) === '(') balanced++;
            else if (url.charAt(i) === ')') balanced--;
            if (balanced < 0) break;
        }
        if (/[\s<>]/.test(url) || balanced !== 0) {
            return '<' + url.replace(/([<>\\])/g, '\\$1') + '>';
        }
        return url;
    }

    function titleSuffix(node) {
        var title = node.getAttribute('title');
        if (!title) return '';
        return ' "' + title.replace(/([\\"])/g, '\\$1') + '"';
    }

    function convertChildren(node) {
        var out = '';
        for (var i = 0; i < node.childNodes.length; i++) {
            out += convertNode(node.childNodes[i]);
        }
        return out;
    }

    function convertNode(node) {
        if (node.nodeType === NODE_TEXT) return escapeText(collapseBlankLines(node.nodeValue));
        if (node.nodeType !== NODE_ELEMENT) return '';

        var tag = node.tagName.toLowerCase();

        // Read the raw attribute, never the resolved property: the preview is
        // loaded as about:blank, so node.href would rewrite relative links.
        if (tag === 'img') {
            return '![' + escapeText(node.getAttribute('alt') || '') + ']('
                + destination(node.getAttribute('src') || '') + titleSuffix(node) + ')';
        }

        // A hard break is two trailing spaces and a line ending. Markdig
        // already prints a newline after <br />, so emitting another one here
        // would make a blank line and split the block into two paragraphs.
        if (tag === 'br') {
            var next = node.nextSibling;
            var newlineFollows = next && next.nodeType === NODE_TEXT
                && /^[ \t]*\r?\n/.test(next.nodeValue);
            return newlineFollows ? '  ' : '  \n';
        }
        if (tag === 'code') return codeSpan(node.textContent);

        var inner = convertChildren(node);

        switch (tag) {
            case 'strong':
            case 'b':
                return inner === '' ? '' : '**' + inner + '**';
            case 'em':
            case 'i':
                return inner === '' ? '' : '*' + inner + '*';
            case 'del':
            case 's':
            case 'strike':
                return inner === '' ? '' : '~~' + inner + '~~';
            case 'a':
                return '[' + inner + '](' + destination(node.getAttribute('href') || '')
                    + titleSuffix(node) + ')';
            default:
                // Unknown wrapper: keep what the user can see, drop the markup.
                return inner;
        }
    }

    /// Returns the inline markdown for an element's content, ready to be
    /// spliced over the source range the element was rendered from.
    mdedit.toInlineMarkdown = function (element) {
        return convertChildren(element);
    };
})();
