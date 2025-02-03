var __assign = (this && this.__assign) || function () {
    __assign = Object.assign || function(t) {
        for (var s, i = 1, n = arguments.length; i < n; i++) {
            s = arguments[i];
            for (var p in s) if (Object.prototype.hasOwnProperty.call(s, p))
                t[p] = s[p];
        }
        return t;
    };
    return __assign.apply(this, arguments);
};
var __awaiter = (this && this.__awaiter) || function (thisArg, _arguments, P, generator) {
    function adopt(value) { return value instanceof P ? value : new P(function (resolve) { resolve(value); }); }
    return new (P || (P = Promise))(function (resolve, reject) {
        function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
        function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
        function step(result) { result.done ? resolve(result.value) : adopt(result.value).then(fulfilled, rejected); }
        step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
};
var __generator = (this && this.__generator) || function (thisArg, body) {
    var _ = { label: 0, sent: function() { if (t[0] & 1) throw t[1]; return t[1]; }, trys: [], ops: [] }, f, y, t, g = Object.create((typeof Iterator === "function" ? Iterator : Object).prototype);
    return g.next = verb(0), g["throw"] = verb(1), g["return"] = verb(2), typeof Symbol === "function" && (g[Symbol.iterator] = function() { return this; }), g;
    function verb(n) { return function (v) { return step([n, v]); }; }
    function step(op) {
        if (f) throw new TypeError("Generator is already executing.");
        while (g && (g = 0, op[0] && (_ = 0)), _) try {
            if (f = 1, y && (t = op[0] & 2 ? y["return"] : op[0] ? y["throw"] || ((t = y["return"]) && t.call(y), 0) : y.next) && !(t = t.call(y, op[1])).done) return t;
            if (y = 0, t) op = [op[0] & 2, t.value];
            switch (op[0]) {
                case 0: case 1: t = op; break;
                case 4: _.label++; return { value: op[1], done: false };
                case 5: _.label++; y = op[1]; op = [0]; continue;
                case 7: op = _.ops.pop(); _.trys.pop(); continue;
                default:
                    if (!(t = _.trys, t = t.length > 0 && t[t.length - 1]) && (op[0] === 6 || op[0] === 2)) { _ = 0; continue; }
                    if (op[0] === 3 && (!t || (op[1] > t[0] && op[1] < t[3]))) { _.label = op[1]; break; }
                    if (op[0] === 6 && _.label < t[1]) { _.label = t[1]; t = op; break; }
                    if (t && _.label < t[2]) { _.label = t[2]; _.ops.push(op); break; }
                    if (t[2]) _.ops.pop();
                    _.trys.pop(); continue;
            }
            op = body.call(thisArg, _);
        } catch (e) { op = [6, e]; y = 0; } finally { f = t = 0; }
        if (op[0] & 5) throw op[1]; return { value: op[0] ? op[1] : void 0, done: true };
    }
};
var __spreadArray = (this && this.__spreadArray) || function (to, from, pack) {
    if (pack || arguments.length === 2) for (var i = 0, l = from.length, ar; i < l; i++) {
        if (ar || !(i in from)) {
            if (!ar) ar = Array.prototype.slice.call(from, 0, i);
            ar[i] = from[i];
        }
    }
    return to.concat(ar || Array.prototype.slice.call(from));
};
var supportsSynchronousInvoke = false;
function enableSynchronousInvoke() {
    supportsSynchronousInvoke = true;
}
function invokeBlazor(reference, method) {
    var args = [];
    for (var _i = 2; _i < arguments.length; _i++) {
        args[_i - 2] = arguments[_i];
    }
    if (supportsSynchronousInvoke) {
        return new Promise(function (resolve, reject) {
            var result;
            try {
                result = reference.invokeMethod.apply(reference, __spreadArray([method], args, false));
            }
            catch (error) {
                reject(error);
                return;
            }
            resolve(result);
        });
    }
    else {
        return reference.invokeMethodAsync.apply(reference, __spreadArray([method], args, false));
    }
}
function hideAllOffcanvases() {
    document.querySelectorAll('.offcanvas').forEach(function (offcanvasEl) { var _a; return (_a = bootstrap.Offcanvas.getInstance(offcanvasEl)) === null || _a === void 0 ? void 0 : _a.hide(); });
}
function startScrollSpy(element) {
    var scrollSpy = bootstrap.ScrollSpy.getInstance(document.body);
    if (scrollSpy) {
        if (scrollSpy._config.target === element)
            return;
        scrollSpy.dispose();
    }
    scrollSpy = new bootstrap.ScrollSpy(document.body, {
        target: element
    });
}
function saveAsFile(data, filename) {
    var blob = new Blob([data], { type: 'application/octet-stream' });
    var url = URL.createObjectURL(blob);
    var a = document.createElement('a');
    a.style['display'] = 'none';
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
}
function registerDropDownHandler(element, reference) {
    element.addEventListener('show.bs.dropdown', function (event) {
        reference.invokeMethodAsync('OnDropDownShow');
    });
    element.addEventListener('hidden.bs.dropdown', function (event) {
        reference.invokeMethodAsync('OnDropDownHidden');
    });
}
function showToast(message, title, delay) {
    var toastContainer = document.querySelector('.toast-container');
    var toast = document.createElement('div');
    toast.classList.add('toast');
    toast.setAttribute('role', 'alert');
    toast.setAttribute('aria-live', 'assertive');
    toast.setAttribute('aria-atomic', 'true');
    toast.innerHTML = "\n<div class=\"toast-header\">\n\t<strong class=\"me-auto\">".concat(title, "</strong>\n\t<button type=\"button\" class=\"btn-close\" data-bs-dismiss=\"toast\" aria-label=\"Close\"></button>\n</div>\n<div class=\"toast-body\">").concat(message, "</div>");
    toastContainer.appendChild(toast);
    var bsToast = new bootstrap.Toast(toast, {
        animation: true,
        autohide: true,
        delay: delay || 5000
    });
    toast.addEventListener('hidden.bs.toast', function () {
        bsToast.dispose();
        toast.remove();
    });
    bsToast.show();
}
function registerResize(element, reference, callbackName) {
    var timeout;
    var handler = function () {
        var characterWidth = element.querySelector('.calculator').getBoundingClientRect().width;
        var line = element.querySelector('.line');
        var lineOffsetX = 0;
        if (line) {
            var topParent = line;
            for (; (topParent === null || topParent === void 0 ? void 0 : topParent.parentElement) != element; topParent = topParent === null || topParent === void 0 ? void 0 : topParent.parentElement)
                ;
            lineOffsetX = topParent.getBoundingClientRect().width - line.getBoundingClientRect().width;
        }
        var elementRect = element.getBoundingClientRect();
        var characters = Math.floor((elementRect.width - lineOffsetX) / characterWidth) || 0;
        if ('' + characters == element.getAttribute('data-characters')) {
            return;
        }
        else {
            element.setAttribute('data-characters', '' + characters);
            element.style['--characters'] = characters;
        }
        clearTimeout(timeout);
        timeout = setTimeout(function () {
            invokeBlazor(reference, callbackName, characters);
        }, 20);
    };
    var observer = new ResizeObserver(handler);
    observer.observe(element);
    handler();
    return {
        destroy: function () {
            observer.disconnect();
        },
    };
}
var promiseAfterRender = null;
var resolveAfterRender = null;
function notifyRenderFinished(componentName) {
    if (componentName)
        console.log("rerender: " + componentName);
    if (resolveAfterRender) {
        var resolve = resolveAfterRender;
        resolveAfterRender = null;
        resolve();
        console.log("rerender action executed");
    }
}
function invokeAfterRender(action) {
    if (!resolveAfterRender || !promiseAfterRender) {
        promiseAfterRender = new Promise(function (resolve) {
            resolveAfterRender = resolve;
        });
    }
    return promiseAfterRender = promiseAfterRender.then(action);
}
function isWaitingForRender() {
    return resolveAfterRender != null;
}
function registerChordEditor(wrapper, reference, callbackName) {
    var existingReference = wrapper['data-reference'];
    if (existingReference === reference) {
        return;
    }
    else if (existingReference) {
        var existingListener = wrapper['data-listener'];
        if (existingListener) {
            wrapper.removeEventListener('beforeinput', existingListener);
        }
    }
    var actionQueue = new ActionQueue();
    var editor = null;
    var selectionObserver = null;
    createEditor();
    return {
        notifyAfterRender: actionQueue.notifyRender.bind(actionQueue),
        destroy: function () {
            editor === null || editor === void 0 ? void 0 : editor.destroy();
            selectionObserver === null || selectionObserver === void 0 ? void 0 : selectionObserver.destroy();
        }
    };
    function createEditor() {
        var _this = this;
        var callback = function (editor, data, selectionRange) {
            console.log('awaiting queue');
            var wasBusy = actionQueue.isBusy;
            actionQueue.then(function () { return __awaiter(_this, void 0, void 0, function () {
                var selection, editRange, eventData, result, newSelection, currentSelectionRange, focusElement;
                return __generator(this, function (_a) {
                    switch (_a.label) {
                        case 0:
                            console.log('queue finished');
                            selection = editor.getCurrentSelection();
                            if (!selection)
                                throw new Error("Keine Auswahl vorhanden.");
                            editRange = selection;
                            if (!wasBusy) {
                                editRange = data.editRangeStart == undefined || data.editRangeEnd == undefined ? null : {
                                    start: {
                                        metaline: selection.start.metaline,
                                        line: selection.start.line,
                                        offset: data.editRangeStart,
                                    },
                                    end: {
                                        metaline: selection.end.metaline,
                                        line: selection.end.line,
                                        offset: data.editRangeEnd,
                                    }
                                };
                            }
                            eventData = __assign({ selection: selection, editRange: editRange, justSelected: selectionObserver.triggerJustSelected() }, data);
                            wrapper.classList.add('refreshing');
                            selectionObserver.pauseObservation();
                            selectionRange.collapse(false);
                            actionQueue.prepareForNextRender();
                            console.log("invoke Blazor");
                            return [4, invokeBlazor(reference, callbackName, eventData)];
                        case 1:
                            result = _a.sent();
                            console.log("check result");
                            if (result.failReason)
                                showToast("".concat(result.failReason.label, " (").concat(result.failReason.code, ")"), 'Bearbeitungsfehler', 5000);
                            if (!!result.willRender) return [3, 2];
                            actionQueue.notifyRender();
                            return [3, 4];
                        case 2: return [4, actionQueue.awaitRender()];
                        case 3:
                            _a.sent();
                            _a.label = 4;
                        case 4:
                            console.log("after render");
                            newSelection = null;
                            if (result.selection && data.inputType != 'deleteByDrag') {
                                newSelection = editor.setCurrentSelection(result.selection);
                            }
                            else {
                                newSelection = getSelection();
                                if ((newSelection === null || newSelection === void 0 ? void 0 : newSelection.rangeCount) == 1) {
                                    currentSelectionRange = newSelection.getRangeAt(0);
                                    currentSelectionRange.setStart(selectionRange.startContainer, selectionRange.startOffset);
                                    currentSelectionRange.setEnd(selectionRange.endContainer, selectionRange.endOffset);
                                }
                                else if (newSelection) {
                                    newSelection.removeAllRanges();
                                    newSelection.addRange(selectionRange);
                                }
                            }
                            editor.updateFromElement(null, false);
                            selectionObserver.refreshSelection();
                            for (focusElement = newSelection === null || newSelection === void 0 ? void 0 : newSelection.focusNode; focusElement && !('scrollIntoView' in focusElement); focusElement = focusElement.parentElement) { }
                            if (focusElement) {
                                focusElement.scrollIntoView({
                                    block: 'nearest',
                                    inline: 'nearest'
                                });
                            }
                            wrapper.classList.remove('refreshing');
                            return [2];
                    }
                });
            }); });
        };
        editor = new ModificationEditor(wrapper, callback);
        selectionObserver = new SelectionObserver(editor);
        if (window.isDebugging) {
            window.editor = editor;
            window.selectionObserver = selectionObserver;
        }
    }
}
var IS_EDIT_CONTEXT_SUPPORTED = "EditContext" in window;
function initializeEditContext(target, reference, output) {
    if (!IS_EDIT_CONTEXT_SUPPORTED) {
        return null;
    }
    var editContext = new EditContext({
        text: target.innerText
    });
    target.editContext = editContext;
    editContext.addEventListener("textupdate", function (event) {
        event.preventDefault();
        event.stopImmediatePropagation();
        console.log(event);
        output.innerText = editContext.text;
        reference.invokeMethodAsync("UpdateContent", event.text, event.updateRangeStart, event.updateRangeEnd).then(function (result) {
            if (!result) {
                restoreContextState();
                output.innerText = editContext.text;
                return;
            }
            output.innerText = editContext.text;
        });
    });
    editContext.addEventListener("textformatupdate", function (event) {
        console.log(event);
    });
    editContext.addEventListener("compositionstart", function (event) {
        console.log(event);
    });
    document.addEventListener("selectionchange", function () {
        var selection = document.getSelection();
        var offsets = fromSelectionToOffsets(selection, target);
        if (offsets) {
            updateSelection(offsets.start, offsets.end);
        }
    });
    output.innerText = editContext.text;
    var currentState = createContextState();
    return {
        afterRender: function () {
            updateText(target.innerText);
            var selection = document.getSelection();
            var offsets = fromSelectionToOffsets(selection, target);
            if (offsets) {
                updateSelection(offsets.start, offsets.end);
            }
            storeContextState();
            output.innerText = editContext.text;
        },
    };
    function createContextState() {
        return {
            text: editContext.text,
            selectionStart: editContext.selectionStart,
            selectionEnd: editContext.selectionEnd,
        };
    }
    function applyContextState(state) {
        editContext.updateText(0, editContext.text.length, state.text);
        editContext.updateSelection(state.selectionStart, state.selectionEnd);
    }
    function storeContextState() {
        return currentState = createContextState();
    }
    function restoreContextState() {
        applyContextState(currentState);
        return currentState;
    }
    function updateText(text) {
        editContext.updateText(0, editContext.text.length, text);
    }
    function updateSelection(start, end) {
        editContext.updateSelection(start, end);
        var selection = document.getSelection();
        if (selection) {
            editContext.updateSelectionBounds(selection.getRangeAt(0).getBoundingClientRect());
        }
    }
    function fromSelectionToOffsets(selection, editorEl) {
        var _a, _b, _c, _d;
        if (!selection)
            return null;
        var treeWalker = document.createTreeWalker(editorEl, NodeFilter.SHOW_TEXT);
        var anchorNodeFound = false;
        var extentNodeFound = false;
        var anchorOffset = 0;
        var extentOffset = 0;
        while (treeWalker.nextNode()) {
            var node = treeWalker.currentNode;
            if (node === selection.anchorNode) {
                anchorNodeFound = true;
                anchorOffset += selection.anchorOffset;
            }
            if (node === selection.focusNode) {
                extentNodeFound = true;
                extentOffset += selection.focusOffset;
            }
            if (node.nodeType == Node.TEXT_NODE) {
                if (!anchorNodeFound) {
                    anchorOffset += (_b = (_a = node.textContent) === null || _a === void 0 ? void 0 : _a.length) !== null && _b !== void 0 ? _b : 0;
                }
                if (!extentNodeFound) {
                    extentOffset += (_d = (_c = node.textContent) === null || _c === void 0 ? void 0 : _c.length) !== null && _d !== void 0 ? _d : 0;
                }
            }
        }
        if (!anchorNodeFound || !extentNodeFound) {
            return null;
        }
        return { start: anchorOffset, end: extentOffset };
    }
}
function registerModificationEditor(editor, reference, callbackName) {
    var callback = function (editor, data) {
        var selection = editor.getCurrentSelection();
        var eventData = __assign({ selection: selection, editRange: data.editRange }, data);
        setTimeout(function () {
            reference.invokeMethodAsync(callbackName, eventData).then(function (result) {
                editor.setCurrentSelection(result.selection);
            });
        }, 0);
    };
    return new ModificationEditor(editor, callback);
}
var Debouncer = (function () {
    function Debouncer(timeout) {
        this.timeout = timeout;
    }
    Debouncer.prototype.debounce = function (handler, debounce) {
        var _this = this;
        if (this.interval && this.handler) {
            console.log("debounce: bounced");
            clearInterval(this.interval);
            this.interval = setInterval(this.handler, this.timeout);
            return;
        }
        if (!debounce) {
            console.log("debounce: immediate");
            handler();
            return;
        }
        this.handler = function () {
            console.log("debounce: invoke");
            _this.interval = null;
            _this.handler = null;
            handler();
        };
        setTimeout(this.handler, this.timeout);
        console.log("debounce: started");
    };
    return Debouncer;
}());
var ModificationEditor = (function () {
    function ModificationEditor(editor, callback) {
        this.editor = editor;
        this.callback = callback;
        this.editContext = new EditContext();
        this.updateFromElement();
        this.editContext.addEventListener('textupdate', this.handleTextUpdate.bind(this));
        this.editContext.addEventListener('compositionend', this.handleCompositionEnd.bind(this));
        this.editor.addEventListener('keydown', this.handleKeyDown.bind(this));
        this.editor.addEventListener('paste', this.handlePaste.bind(this));
        this.editor.addEventListener('beforeinput', this.handleBeforeInput.bind(this));
        this.editor.editContext = this.editContext;
    }
    ModificationEditor.prototype.destroy = function () {
        this.editContext.removeEventListener('textupdate', this.handleTextUpdate.bind(this));
        this.editContext.removeEventListener('compositionend', this.handleCompositionEnd.bind(this));
        this.editor.removeEventListener('keydown', this.handleKeyDown.bind(this));
        this.editor.removeEventListener('paste', this.handlePaste.bind(this));
        this.editor.removeEventListener('beforeinput', this.handleBeforeInput.bind(this));
    };
    ModificationEditor.prototype.handleTextUpdate = function (event) {
        var _a;
        event.stopImmediatePropagation();
        event.preventDefault();
        console.log(event);
        var currentRange = (_a = getSelection()) === null || _a === void 0 ? void 0 : _a.getRangeAt(0);
        if (!currentRange)
            return;
        var inputType = event.text ? 'insertText'
            : event.updateRangeStart < this.currentState.selectionStart ? 'deleteContentBackward'
                : 'deleteContentForward';
        var lineStart = this.currentState.text.lastIndexOf('\n', event.updateRangeStart - 1);
        var lineEnd = this.currentState.text.lastIndexOf('\n', event.updateRangeEnd - 1);
        var lineOffsetStart = event.updateRangeStart - lineStart - 1;
        var lineOffsetEnd = event.updateRangeEnd - lineEnd - 1;
        this.callback(this, {
            inputType: inputType,
            editRangeStart: lineOffsetStart,
            editRangeEnd: lineOffsetEnd,
            afterCompose: this.isAfterCompose,
            data: event.text
        }, currentRange);
    };
    ModificationEditor.prototype.handleKeyDown = function (event) {
        var _a;
        if (event.isComposing || event.keyCode === 229)
            return;
        console.log(event);
        var currentRange = (_a = getSelection()) === null || _a === void 0 ? void 0 : _a.getRangeAt(0);
        if (!currentRange)
            return;
        if (event.key === "Enter") {
            this.callback(this, {
                inputType: 'insertText',
                data: '\n',
                afterCompose: false,
            }, currentRange);
        }
        else if (event.key == 'z' && event.ctrlKey) {
            this.callback(this, {
                inputType: 'historyUndo',
                data: null,
                afterCompose: false,
            }, currentRange);
        }
        else if (event.key == 'y' && event.ctrlKey) {
            this.callback(this, {
                inputType: 'historyRedo',
                data: null,
                afterCompose: false,
            }, currentRange);
        }
    };
    ModificationEditor.prototype.handlePaste = function (event) {
        var _a, _b;
        console.log(event);
        var currentRange = (_a = getSelection()) === null || _a === void 0 ? void 0 : _a.getRangeAt(0);
        if (!currentRange)
            return;
        var text = (_b = event.clipboardData) === null || _b === void 0 ? void 0 : _b.getData('text');
        if (!text)
            return;
        this.callback(this, {
            inputType: 'insertText',
            data: text,
            afterCompose: false,
        }, currentRange);
    };
    ModificationEditor.prototype.handleCompositionEnd = function (event) {
        var _this = this;
        this.isAfterCompose = true;
        requestIdleCallback(function () {
            _this.isAfterCompose = false;
        });
    };
    ModificationEditor.prototype.handleBeforeInput = function (event) {
        var _a;
        var currentRange = (_a = getSelection()) === null || _a === void 0 ? void 0 : _a.getRangeAt(0);
        if (!currentRange)
            return;
        if (event.inputType == 'historyUndo' || event.inputType == 'historyRedo') {
            this.callback(this, {
                inputType: event.inputType,
                data: null,
                afterCompose: false,
            }, currentRange);
        }
    };
    ModificationEditor.prototype.updateFromElement = function (text, selection) {
        var _this = this;
        if (text !== false) {
            if (typeof text !== 'string') {
                text = '';
                var lines = this.editor.querySelectorAll('.line');
                this.lines = new Array(lines.length);
                var i_1 = 0;
                var currentOffset_1 = 0;
                lines.forEach(function (l) {
                    var content = l.textContent + "\n";
                    text += content;
                    _this.lines[i_1++] = {
                        node: l,
                        offset: currentOffset_1,
                        length: content.length
                    };
                    currentOffset_1 += content.length;
                });
            }
            this.editContext.updateText(0, this.editContext.text.length, text);
            console.log(text);
        }
        if (selection !== false) {
            if (selection instanceof Selection)
                selection = this.getCurrentSelection(selection, true);
            else
                selection = this.getCurrentSelection(undefined, true);
            if (selection) {
                var startFound = false;
                var endFound = false;
                var start = selection.start.offset;
                var end = selection.end.offset;
                for (var i = 0; i < this.lines.length; i++) {
                    var line = this.lines[i];
                    if (line.node === selection.start.lineNode) {
                        if (start >= line.length)
                            start = line.length - 1;
                        start += line.offset;
                        startFound = true;
                        if (endFound)
                            break;
                    }
                    if (line.node === selection.end.lineNode) {
                        if (end >= line.length)
                            end = line.length - 1;
                        end += line.offset;
                        endFound = true;
                        if (startFound)
                            break;
                    }
                }
                if (startFound && endFound) {
                    this.editContext.updateSelection(start, end);
                    console.log(this.editContext.text.substring(0, start) + '�' + this.editContext.text.substring(start));
                }
            }
        }
        this.currentState = {
            text: this.editContext.text,
            selectionStart: this.editContext.selectionStart,
            selectionEnd: this.editContext.selectionEnd
        };
    };
    ModificationEditor.prototype.getCurrentSelection = function (documentSelection, includeNode) {
        var _a;
        if (documentSelection === undefined)
            documentSelection = getSelection() || undefined;
        if (!documentSelection || documentSelection.rangeCount == 0)
            return null;
        var range = (_a = getSelection()) === null || _a === void 0 ? void 0 : _a.getRangeAt(0);
        if (!range)
            return null;
        if (!this.editor.contains(range.startContainer) || !this.editor.contains(range.endContainer))
            return null;
        return this.getLineSelection(range, includeNode);
    };
    ModificationEditor.prototype.setCurrentSelection = function (lineSelection) {
        var documentSelection = getSelection();
        if (!documentSelection)
            throw new Error("Keine Auswahl m�glich");
        if (!lineSelection) {
            if (documentSelection.rangeCount != 0)
                documentSelection.removeAllRanges();
            return documentSelection;
        }
        var range;
        if (documentSelection.rangeCount) {
            range = documentSelection.getRangeAt(0);
        }
        else {
            range = document.createRange();
            documentSelection.addRange(range);
        }
        this.setLineSelectionRange(documentSelection, range, lineSelection);
        return documentSelection;
    };
    ModificationEditor.prototype.setLineSelectionRange = function (documentSelection, range, lineSelection) {
        var startLine = lineSelection.start.lineNode;
        var endLine = lineSelection.end.lineNode;
        console.log('selecting', lineSelection);
        if (!(startLine instanceof HTMLElement && endLine instanceof HTMLElement)) {
            var metalineLineSelection = lineSelection;
            var startMetaline = startLine ? null : this.editor.querySelector(".metaline[data-metaline=\"".concat(metalineLineSelection.start.metaline, "\"]"));
            var endMetaline = metalineLineSelection.end.metaline == metalineLineSelection.start.metaline ? startMetaline
                : endLine ? null : this.editor.querySelector(".metaline[data-metaline=\"".concat(metalineLineSelection.end.metaline, "\"]"));
            if (!startLine) {
                if (!startMetaline || metalineLineSelection.start.line === null)
                    throw new Error("Zeile nicht gefunden");
                startLine = findLine(startMetaline, metalineLineSelection.start.line);
                if (!startLine)
                    throw new Error("Zeile nicht gefunden");
            }
            if (!endLine) {
                if (!endMetaline || metalineLineSelection.end.line === null)
                    throw new Error("Zeile nicht gefunden");
                endLine = metalineLineSelection.end.metaline == metalineLineSelection.start.metaline && metalineLineSelection.end.line == metalineLineSelection.start.line ? startLine
                    : findLine(endMetaline, metalineLineSelection.end.line);
                if (!endLine)
                    throw new Error("Zeile nicht gefunden");
            }
        }
        var start = SelectionHelper.getNodeAndOffset(startLine, lineSelection.start.offset);
        range.setStart(start.node, start.offset);
        var end = startLine === endLine && lineSelection.end.offset == lineSelection.start.offset ? start
            : SelectionHelper.getNodeAndOffset(endLine, lineSelection.end.offset);
        range.setEnd(end.node, end.offset);
        function findLine(metaline, line) {
            if (line === ModificationEditor.FIRST_LINE_ID) {
                return metaline.querySelector('.line[data-line]');
            }
            else if (line === ModificationEditor.LAST_LINE_ID) {
                var lines = metaline.querySelectorAll('.line[data-line]');
                return lines[lines.length - 1];
            }
            return metaline.querySelector(".line[data-line=\"".concat(line, "\"]"));
        }
    };
    ModificationEditor.prototype.getLineSelection = function (range, includeNode) {
        var start = this.getTextIndex(range.startContainer, this.getLineInfo, range.startOffset);
        if (!start)
            return null;
        var end = range.endContainer !== range.startContainer ? this.getTextIndex(range.endContainer, this.getLineInfo, range.endOffset) : {
            offset: start.offset + range.endOffset - range.startOffset,
            data: start.data,
            node: start.node,
        };
        if (!end)
            return null;
        return {
            start: {
                metaline: start.data.metaline,
                line: start.data.line,
                offset: start.offset,
                lineNode: includeNode ? start.node : undefined,
            },
            end: {
                metaline: end.data.metaline,
                line: end.data.line,
                offset: end.offset,
                lineNode: includeNode ? end.node : undefined,
            }
        };
    };
    ModificationEditor.prototype.getTextIndex = function (node, stopCondition, offset) {
        var _a;
        if (!offset)
            offset = 0;
        var data = stopCondition(node);
        var current;
        for (current = node; current && !data; current = current.parentElement, data = stopCondition(current)) {
            for (var sibling = current.previousSibling; sibling; sibling = sibling.previousSibling) {
                if (sibling.nodeType == Node.COMMENT_NODE)
                    continue;
                offset += ((_a = sibling.textContent) === null || _a === void 0 ? void 0 : _a.length) || 0;
            }
        }
        if (!data || !current)
            return null;
        return {
            node: current,
            offset: offset,
            data: data
        };
    };
    ModificationEditor.prototype.getNode = function (node, offset) {
        if (offset < 0)
            return getFromEnd(node, -offset - 1);
        else
            return getFromStart(node, offset);
        function getFromStart(node, offset) {
            var _a, _b;
            if (!(node instanceof HTMLElement))
                return {
                    node: node,
                    offset: offset,
                };
            var currentOffset = 0;
            for (var i = 0; i < node.childNodes.length; i++) {
                var child = node.childNodes[i];
                if (child.nodeType == Node.COMMENT_NODE)
                    continue;
                var offsetAfter = currentOffset + ((_b = (_a = child.textContent) === null || _a === void 0 ? void 0 : _a.length) !== null && _b !== void 0 ? _b : 0);
                if (offset < offsetAfter || (offsetAfter == offset && i == node.childNodes.length - 1))
                    return getFromStart(child, offset - currentOffset);
                currentOffset = offsetAfter;
            }
            return {
                node: node,
                offset: offset,
            };
        }
        function getFromEnd(node, offset) {
            var _a, _b;
            if (!(node instanceof HTMLElement)) {
                if (!node.textContent)
                    throw new Error("Kein Textknoten gefunden");
                return {
                    node: node,
                    offset: node.textContent.length - offset,
                };
            }
            var currentEndOffset = 0;
            for (var i = node.childNodes.length - 1; i >= 0; i--) {
                var child = node.childNodes[i];
                if (child.nodeType == Node.COMMENT_NODE)
                    continue;
                var offsetBefore = currentEndOffset + ((_b = (_a = child.textContent) === null || _a === void 0 ? void 0 : _a.length) !== null && _b !== void 0 ? _b : 0);
                if (offset < offsetBefore || (offsetBefore == offset && i == 0))
                    return getFromEnd(child, offset - currentEndOffset);
                currentEndOffset = offsetBefore;
            }
            return {
                node: node,
                offset: offset,
            };
        }
    };
    ModificationEditor.prototype.getLineInfo = function (node) {
        var _a, _b;
        if (!(node instanceof HTMLElement))
            return null;
        var nodeElement = node;
        var lineId = null;
        if (node.classList.contains('line')) {
            lineId = parseInt((_a = nodeElement.getAttribute('data-line')) !== null && _a !== void 0 ? _a : '');
            nodeElement = (_b = nodeElement.parentElement) === null || _b === void 0 ? void 0 : _b.parentElement;
        }
        else if (nodeElement.classList.contains('metaline-lines')) {
            nodeElement = nodeElement.parentElement;
        }
        else if (!nodeElement.classList.contains('metaline')) {
            return null;
        }
        var metalineId = nodeElement === null || nodeElement === void 0 ? void 0 : nodeElement.getAttribute('data-metaline');
        if (metalineId == null || lineId == null)
            return null;
        return {
            metaline: metalineId,
            line: lineId
        };
    };
    ModificationEditor.FIRST_LINE_ID = -1;
    ModificationEditor.LAST_LINE_ID = -2;
    return ModificationEditor;
}());
var SelectionHelper = (function () {
    function SelectionHelper() {
    }
    SelectionHelper.getNodeAndOffset = function (node, offset) {
        var _a, _b, _c, _d, _e, _f;
        if (offset < 0)
            return SelectionHelper.getNodeAndOffsetFromEnd(node, offset);
        var iterator = document.createNodeIterator(node, NodeFilter.SHOW_TEXT);
        var currentOffset = 0;
        var current = iterator.nextNode();
        while (current && currentOffset + ((_b = (_a = current.textContent) === null || _a === void 0 ? void 0 : _a.length) !== null && _b !== void 0 ? _b : 0) < offset) {
            currentOffset += (_d = (_c = current.textContent) === null || _c === void 0 ? void 0 : _c.length) !== null && _d !== void 0 ? _d : 0;
            var next = iterator.nextNode();
            if (!next)
                return {
                    node: current,
                    offset: ((_f = (_e = current.textContent) === null || _e === void 0 ? void 0 : _e.length) !== null && _f !== void 0 ? _f : 0),
                };
            current = next;
        }
        return {
            node: current !== null && current !== void 0 ? current : node,
            offset: offset - currentOffset,
        };
    };
    SelectionHelper.getNodeAndOffsetFromEnd = function (node, offset) {
        var _a, _b, _c, _d;
        var iterator = document.createNodeIterator(node, NodeFilter.SHOW_TEXT);
        var allNodes = [];
        var current = iterator.nextNode();
        if (!current)
            return {
                node: node,
                offset: 0,
            };
        while (current) {
            allNodes.push(current);
            current = iterator.nextNode();
        }
        offset = -offset - 1;
        var currentOffset = 0;
        for (var i = allNodes.length - 1; i >= 0; i--) {
            current = allNodes[i];
            if (currentOffset + ((_b = (_a = current.textContent) === null || _a === void 0 ? void 0 : _a.length) !== null && _b !== void 0 ? _b : 0) >= offset)
                break;
            currentOffset += (_d = (_c = current.textContent) === null || _c === void 0 ? void 0 : _c.length) !== null && _d !== void 0 ? _d : 0;
        }
        current !== null && current !== void 0 ? current : (current = node);
        if (!current.textContent)
            throw new Error("Kein Textknoten gefunden");
        return {
            node: current,
            offset: current.textContent.length - (offset - currentOffset),
        };
    };
    SelectionHelper.getGlobalOffset = function (selection, editor) {
        var _a, _b, _c, _d;
        if (!selection)
            return null;
        var treeWalker = document.createTreeWalker(editor, NodeFilter.SHOW_TEXT);
        var anchorNodeFound = false;
        var extentNodeFound = false;
        var anchorOffset = 0;
        var extentOffset = 0;
        while (treeWalker.nextNode()) {
            var node = treeWalker.currentNode;
            if (node === selection.anchorNode) {
                anchorNodeFound = true;
                anchorOffset += selection.anchorOffset;
            }
            if (node === selection.focusNode) {
                extentNodeFound = true;
                extentOffset += selection.focusOffset;
            }
            if (node.nodeType == Node.TEXT_NODE) {
                if (!anchorNodeFound) {
                    anchorOffset += (_b = (_a = node.textContent) === null || _a === void 0 ? void 0 : _a.length) !== null && _b !== void 0 ? _b : 0;
                }
                if (!extentNodeFound) {
                    extentOffset += (_d = (_c = node.textContent) === null || _c === void 0 ? void 0 : _c.length) !== null && _d !== void 0 ? _d : 0;
                }
            }
        }
        if (!anchorNodeFound || !extentNodeFound) {
            return null;
        }
        return { start: anchorOffset, end: extentOffset };
    };
    return SelectionHelper;
}());
var ActionQueue = (function () {
    function ActionQueue() {
        this.queue = null;
        this.resolveRender = null;
        this.awaitRenderPromise = null;
    }
    Object.defineProperty(ActionQueue.prototype, "isBusy", {
        get: function () {
            return !!this.queue;
        },
        enumerable: false,
        configurable: true
    });
    ActionQueue.prototype.then = function (onfulfilled, onrejected) {
        var self = this;
        var promise = null;
        function checkRemove(next) {
            if (next && next.then) {
                return next.then(function (afterNext) {
                    return checkRemove(afterNext);
                });
            }
            if (self.queue === promise) {
                self.queue = null;
                console.log("Queue is empty");
            }
            else {
            }
            return next;
        }
        ;
        var handler = function (value) { return checkRemove(onfulfilled ? onfulfilled(value) : undefined); };
        if (this.queue) {
            this.queue = promise = this.queue.then(handler);
        }
        else {
            this.queue = promise = new Promise(function (resolve, reject) {
                resolve(handler(undefined));
            });
        }
        return this;
    };
    ActionQueue.prototype.prepareForNextRender = function () {
        if (this.resolveRender)
            return;
        var self = this;
        this.awaitRenderPromise = new Promise(function (resolve, reject) {
            self.resolveRender = function () {
                resolve();
                self.resolveRender = null;
                self.awaitRenderPromise = null;
            };
        });
    };
    ActionQueue.prototype.awaitRender = function () {
        if (!this.awaitRenderPromise)
            return;
        console.log("Awaiting next render");
        var promise = this.awaitRenderPromise;
        return promise;
    };
    ActionQueue.prototype.notifyRender = function () {
        console.log("Render called");
        if (!this.resolveRender)
            return;
        this.resolveRender();
    };
    return ActionQueue;
}());
var SelectionObserver = (function () {
    function SelectionObserver(modificationEditor) {
        this.modificationEditor = modificationEditor;
        this.supportsMultipleRanges = false;
        this.editor = modificationEditor.editor;
        this.editorWrapper = this.editor.parentElement;
        if (!this.editorWrapper)
            throw new Error("Editor hat kein Elternelement");
        this.customSelection = this.editorWrapper.querySelector('.custom-selection');
        if (!this.customSelection)
            throw new Error("Custom-Selection-Element nicht gefunden");
        document.addEventListener('selectionchange', this.handleSelectionChange.bind(this));
        this.editor.addEventListener('dragstart', this.handleDragStart.bind(this));
    }
    SelectionObserver.prototype.destroy = function () {
        document.removeEventListener('selectionchange', this.handleSelectionChange.bind(this));
        this.editor.removeEventListener('dragstart', this.handleDragStart.bind(this));
    };
    SelectionObserver.prototype.triggerJustSelected = function () {
        if (this.justSelected) {
            this.justSelected = false;
            return true;
        }
        return false;
    };
    SelectionObserver.prototype.refreshSelection = function () {
        this.isPaused = false;
        requestAnimationFrame(this.processSelectionChange.bind(this));
    };
    SelectionObserver.prototype.pauseObservation = function () {
        this.isPaused = true;
    };
    SelectionObserver.prototype.handleSelectionChange = function () {
        var _this = this;
        if (this.isPaused) {
            requestAnimationFrame((function () {
                _this.isPaused = false;
            }).bind(this));
            return;
        }
        this.processSelectionChange();
    };
    SelectionObserver.prototype.processSelectionChange = function () {
        var _a, _b;
        var documentSelection = getSelection();
        if (!documentSelection || documentSelection.rangeCount == 0)
            return this.resetCustomSelections();
        this.modificationEditor.updateFromElement(false, documentSelection);
        var range = documentSelection.getRangeAt(0);
        if (documentSelection.rangeCount > 1) {
            var firstRange = range;
            var lastRange = documentSelection.getRangeAt(documentSelection.rangeCount - 1);
            if (lastRange.startContainer.compareDocumentPosition(firstRange.startContainer) & Node.DOCUMENT_POSITION_FOLLOWING) {
                firstRange = lastRange;
                lastRange = range;
            }
            range = document.createRange();
            if (firstRange.endContainer.compareDocumentPosition(firstRange.startContainer) & Node.DOCUMENT_POSITION_FOLLOWING) {
                range.setStart(lastRange.endContainer, lastRange.endOffset);
                range.setEnd(firstRange.startContainer, firstRange.startOffset);
            }
            else {
                range.setStart(firstRange.startContainer, firstRange.startOffset);
                range.setEnd(lastRange.endContainer, lastRange.endOffset);
            }
        }
        if (!this.editor.contains(range.startContainer) || !this.editor.contains(range.endContainer))
            return this.resetCustomSelections();
        if (SelectionObserver.needsLineFix && this.fixSelection(documentSelection, range))
            return;
        var lineSelection = this.modificationEditor.getLineSelection(range, true);
        if (!lineSelection || lineSelection.start.metaline != lineSelection.end.metaline)
            return this.resetCustomSelections();
        var metaline = (_b = (_a = lineSelection.start.lineNode) === null || _a === void 0 ? void 0 : _a.parentElement) === null || _b === void 0 ? void 0 : _b.parentElement;
        if (!metaline)
            return this.resetCustomSelections();
        var selectionType = metaline.getAttribute('data-selection');
        if (!selectionType)
            return this.resetCustomSelections();
        switch (selectionType) {
            case 'box':
                this.adjustBoxSelection(documentSelection, range, lineSelection);
                break;
            default:
                this.resetCustomSelections();
                break;
        }
    };
    SelectionObserver.prototype.fixSelection = function (documentSelection, range) {
        var _a, _b, _c;
        var modified = false;
        var collapsed = range.collapsed;
        var newStart = null;
        if (!(range.startContainer instanceof Text)) {
            if (range.startContainer.childNodes.length > range.startOffset) {
                var node = range.startContainer.childNodes[range.startOffset];
                while (node.firstChild)
                    node = node.firstChild;
                newStart = { node: node, offset: 0 };
                modified = true;
            }
        }
        if (!collapsed) {
            if (!(range.endContainer instanceof Text)) {
                if (range.endContainer.childNodes.length > range.endOffset) {
                    var node = range.endContainer.childNodes[range.startOffset];
                    while (node.lastChild)
                        node = node.lastChild;
                    range.setEnd(node, (_b = (_a = node.textContent) === null || _a === void 0 ? void 0 : _a.length) !== null && _b !== void 0 ? _b : 0);
                    modified = true;
                }
            }
            if (newStart)
                range.setStart(newStart.node, newStart.offset);
        }
        else if (newStart) {
            range.setStart(newStart.node, newStart.offset);
            range.setEnd(newStart.node, newStart.offset);
        }
        var startContainer = range.endContainer;
        var startNode = startContainer instanceof HTMLElement ? startContainer : startContainer.parentElement;
        if (startNode === null || startNode === void 0 ? void 0 : startNode.classList.contains('suffix')) {
            if (((_c = startNode.previousSibling) === null || _c === void 0 ? void 0 : _c.classList.contains('suffix')) === false) {
                startNode = startNode.previousSibling;
                var node = SelectionHelper.getNodeAndOffset(startNode, -1);
                range.setEnd(node.node, node.offset);
                if (collapsed)
                    range.setStart(node.node, node.offset);
                modified = true;
            }
            else {
                range.setEnd(startContainer, 0);
                if (collapsed)
                    range.setStart(startContainer, 0);
                modified = true;
            }
        }
        if (modified)
            console.log('fixed');
        return modified;
    };
    SelectionObserver.prototype.resetCustomSelections = function () {
        this.customSelection.className = 'custom-selection';
        this.justSelected = true;
    };
    SelectionObserver.prototype.adjustBoxSelection = function (documentSelection, range, lineSelection) {
        if (!this.supportsMultipleRanges)
            return emulateBoxSelection(this, documentSelection, range, lineSelection);
        if (lineSelection.start.lineNode && lineSelection.end.lineNode && lineSelection.end.lineNode.compareDocumentPosition(lineSelection.start.lineNode) & Node.DOCUMENT_POSITION_FOLLOWING) {
            lineSelection = {
                start: lineSelection.end,
                end: lineSelection.start,
            };
        }
        var startLine = lineSelection.start.lineNode;
        var endLine = lineSelection.end.lineNode;
        if (!(startLine instanceof HTMLElement) || !(endLine instanceof HTMLElement))
            return this.resetCustomSelections();
        var lines = [startLine];
        for (var current = startLine.nextSibling; current != endLine; current = current.nextSibling) {
            if (!(current instanceof HTMLElement) || !current.classList.contains('line'))
                continue;
            lines.push(current);
        }
        lines.push(endLine);
        var startOffset = lineSelection.start.offset;
        var endOffset = lineSelection.end.offset;
        documentSelection.removeAllRanges();
        for (var i = 0; i < lines.length; i++) {
            var line = lines[i];
            var lineRange = document.createRange();
            documentSelection.addRange(lineRange);
            this.modificationEditor.setLineSelectionRange(documentSelection, lineRange, {
                start: {
                    lineNode: line,
                    offset: startOffset,
                },
                end: {
                    lineNode: line,
                    offset: endOffset,
                }
            });
        }
        if (documentSelection.rangeCount != lines.length) {
            this.supportsMultipleRanges = false;
            emulateBoxSelection(this, documentSelection, range, lineSelection);
        }
        function emulateBoxSelection(self, documentSelection, range, lineSelection) {
            var startCell = range.startContainer;
            var endCell = range.endContainer;
            if (!startCell || !endCell || lineSelection.start.line == null || lineSelection.end.line == null)
                return self.resetCustomSelections();
            console.log(lineSelection.start, lineSelection.end);
            console.log(range);
            if (lineSelection.start.line > lineSelection.end.line
                || (lineSelection.start.line == lineSelection.end.line && lineSelection.start.offset > lineSelection.end.offset)) {
                startCell = endCell;
                endCell = range.startContainer;
            }
            if (!(startCell instanceof HTMLElement))
                startCell = startCell === null || startCell === void 0 ? void 0 : startCell.parentNode;
            if (!(endCell instanceof HTMLElement))
                endCell = endCell === null || endCell === void 0 ? void 0 : endCell.parentNode;
            var selectionBox;
            if (range.collapsed) {
                if (range.startOffset != 0)
                    startCell = startCell === null || startCell === void 0 ? void 0 : startCell.nextSibling;
                if (!(startCell instanceof HTMLElement))
                    return self.resetCustomSelections();
                selectionBox = startCell.getBoundingClientRect();
            }
            else {
                if (lineSelection.start.offset == lineSelection.end.offset) {
                    endCell = endCell === null || endCell === void 0 ? void 0 : endCell.nextSibling;
                }
                if (lineSelection.end.offset >= lineSelection.start.offset) {
                    if (range.startOffset != 0)
                        startCell = startCell === null || startCell === void 0 ? void 0 : startCell.nextSibling;
                    if (range.endOffset == 0)
                        endCell = endCell === null || endCell === void 0 ? void 0 : endCell.previousSibling;
                }
                else {
                    if (range.startOffset == 0)
                        startCell = startCell === null || startCell === void 0 ? void 0 : startCell.previousSibling;
                    if (range.endOffset != 0)
                        endCell = endCell === null || endCell === void 0 ? void 0 : endCell.nextSibling;
                }
                if (!(startCell instanceof HTMLElement) || !(endCell instanceof HTMLElement))
                    return self.resetCustomSelections();
                var startBox = startCell.getBoundingClientRect();
                var endBox = endCell.getBoundingClientRect();
                selectionBox = SelectionObserver.getBoundingBox(startBox, endBox);
            }
            setBoxPosition(self, selectionBox);
            function setBoxPosition(self, box) {
                var editorRect = self.editor.getBoundingClientRect();
                var left = box.left - editorRect.left;
                var top = box.top - editorRect.top;
                var width = box.width;
                var height = box.height;
                var newPosition = top.toFixed(0) + ';' + left.toFixed(0) + ';' + documentSelection.toString().length;
                var newPositionFull = newPosition + ';' + width.toFixed(0) + ';' + height.toFixed(0);
                var currentPosition = self.customSelection['data-position'];
                if (currentPosition != newPositionFull) {
                    self.customSelection.style.top = top + 'px';
                    self.customSelection.style.left = left + 'px';
                    self.customSelection.style.width = width + 'px';
                    self.customSelection.style.height = height + 'px';
                    self.customSelection['data-position'] = newPositionFull;
                    if (!(currentPosition === null || currentPosition === void 0 ? void 0 : currentPosition.startsWith(newPosition)))
                        self.justSelected = true;
                }
                if (self.customSelection.className != 'custom-selection custom-selection-box') {
                    self.customSelection.className = 'custom-selection custom-selection-box';
                    self.justSelected = true;
                }
            }
        }
    };
    SelectionObserver.getBoundingBox = function () {
        var args = [];
        for (var _i = 0; _i < arguments.length; _i++) {
            args[_i] = arguments[_i];
        }
        var left = Number.MAX_VALUE;
        var top = Number.MAX_VALUE;
        var right = 0;
        var bottom = 0;
        for (var _a = 0, args_1 = args; _a < args_1.length; _a++) {
            var rect = args_1[_a];
            if (rect.left < left)
                left = rect.left;
            if (rect.right > right)
                right = rect.right;
            if (rect.top < top)
                top = rect.top;
            if (rect.bottom > bottom)
                bottom = rect.bottom;
        }
        return new DOMRect(left, top, right - left, bottom - top);
    };
    SelectionObserver.prototype.handleDragStart = function (event) {
        if (!(event.target instanceof Node))
            return;
        for (var current = event.target; current && current != this.editor; current = current.parentElement) {
            if (!(current instanceof HTMLElement))
                continue;
            var selectionType = current.getAttribute('data-selection');
            if (selectionType == 'box') {
                event.preventDefault();
                event.stopPropagation();
                return false;
            }
        }
    };
    SelectionObserver.needsLineFix = /Firefox/.test(navigator.userAgent);
    return SelectionObserver;
}());
function downloadFile(url, fileName) {
    var _a;
    var a = document.createElement('a');
    if (!fileName) {
        fileName = (_a = url.split('/').pop()) !== null && _a !== void 0 ? _a : null;
    }
    if (fileName)
        a.download = fileName;
    a.click();
}
//# sourceMappingURL=app.js.map