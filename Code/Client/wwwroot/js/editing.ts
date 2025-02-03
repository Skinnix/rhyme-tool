declare interface Destructible {
	destroy(): void;
}

declare interface AwaitingModification {

}

declare interface ObservedModification {
	mutations: MutationRecord[];
}

declare interface LineIdentifier {
	metaline: string;
	line: number | null;
}

declare interface TextIndex<T> {
	node: Node;
	offset: number;
	data: T;
}

declare interface NodeSelectionAnchor {
	node: Node,
	offset: number,
}

declare interface AnchorSelection<TAnchor> {
	start: TAnchor;
	end: TAnchor;
}

declare interface NodeSelection {
	start: NodeSelectionAnchor,
	end: NodeSelectionAnchor,
}

declare interface MetalineLineAnchor extends LineIdentifier {
	offset: number;
	lineNode?: Node;
}

declare interface LineNodeAnchor {
	offset: number;
	lineNode: Node;
}

declare type InputType = 'insertText' | 'deleteContentBackward' | 'deleteContentForward' | 'deleteByDrag' | 'insertTextAfterCompose' | 'deleteContentAfterCompose'
	| 'historyUndo' | 'historyRedo';

declare interface ModificationData {
	inputType: InputType,
	data: string | null,
	editRangeStart?: number,
	editRangeEnd?: number,
	editRange?: AnchorSelection<MetalineLineAnchor> | null,
	afterCompose: boolean,
}

declare interface MetalineEditResult {
	success: boolean,
	willRender: boolean,
	selection: AnchorSelection<MetalineLineAnchor> | null,
	failReason?: {
		label: string,
		code: string,
	},
}

declare interface EditContextState {
	text: string;
	selectionStart: number;
	selectionEnd: number;
}

declare type EditorCallback = (editor: ModificationEditor, data: ModificationData, selectionRange: Range) => void;
declare type ModificationHandler = (modification: ObservedModification) => void;

function registerModificationEditor(editor: HTMLElement, reference: BlazorDotNetReference, callbackName: string): ModificationEditor {
	let callback: EditorCallback = (editor, data) => {
		var selection = editor.getCurrentSelection();
		var eventData = {
			selection: selection,
			editRange: data.editRange,
			...data
		};
		setTimeout(() => {
			reference.invokeMethodAsync<MetalineEditResult>(callbackName, eventData).then(result => {
				editor.setCurrentSelection(result.selection);
			});
		}, 0);
	};
	return new ModificationEditor(editor, callback);
}

class Debouncer {
	private interval: ReturnType<typeof setInterval> | null;
	private handler: TimerHandler | null;

	constructor(private timeout: number) {

	}
	
	public debounce(handler: () => void, debounce: boolean) {
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
		
		this.handler = () => {
			console.log("debounce: invoke");
			this.interval = null;
			this.handler = null;
			handler();
		};
		setTimeout(this.handler, this.timeout);
		console.log("debounce: started");
	}
}

class ModificationEditor implements Destructible {
	static FIRST_LINE_ID = -1;
	static LAST_LINE_ID = -2;

	private currentState: EditContextState;
	private editContext: EditContext;
	private lines: { node: HTMLElement, offset: number, length: number }[];

	private isAfterCompose: boolean;

	constructor(public editor: HTMLElement, private callback: EditorCallback) {
		//let selection = SelectionHelper.getGlobalOffset(getSelection(), editor);
		this.editContext = new EditContext();
		this.updateFromElement();

		this.editContext.addEventListener('textupdate', this.handleTextUpdate.bind(this));
		this.editContext.addEventListener('compositionend', this.handleCompositionEnd.bind(this));
		this.editor.addEventListener('keydown', this.handleKeyDown.bind(this));
		this.editor.addEventListener('paste', this.handlePaste.bind(this));
		this.editor.addEventListener('beforeinput', this.handleBeforeInput.bind(this));

		(this.editor as any).editContext = this.editContext;
	}

	public destroy() {
		this.editContext.removeEventListener('textupdate', this.handleTextUpdate.bind(this));
		this.editContext.removeEventListener('compositionend', this.handleCompositionEnd.bind(this));
		this.editor.removeEventListener('keydown', this.handleKeyDown.bind(this));
		this.editor.removeEventListener('paste', this.handlePaste.bind(this));
		this.editor.removeEventListener('beforeinput', this.handleBeforeInput.bind(this));
	}

	private handleTextUpdate(event: TextUpdateEvent) {
		event.stopImmediatePropagation();
		event.preventDefault();

		//Hole aktuelle Auswahl
		console.log(event);
		let currentRange = getSelection()?.getRangeAt(0);

		//Keine Auswahl?
		if (!currentRange)
			return;

		//Simuliere InputType
		let inputType: InputType = event.text ? 'insertText'
			: event.updateRangeStart < this.currentState.selectionStart ? 'deleteContentBackward'
			: 'deleteContentForward';

		//Finde Range
		let lineStart = this.currentState.text.lastIndexOf('\n', event.updateRangeStart - 1);
		let lineEnd = this.currentState.text.lastIndexOf('\n', event.updateRangeEnd - 1);
		let lineOffsetStart = event.updateRangeStart - lineStart - 1;
		let lineOffsetEnd = event.updateRangeEnd - lineEnd - 1;

		//Führe die Bearbeitung durch
		this.callback(this, {
			inputType: inputType,
			//editRange: this.getLineSelection(currentRange, false),
			editRangeStart: lineOffsetStart,
			editRangeEnd: lineOffsetEnd,
			afterCompose: this.isAfterCompose,
			data: event.text
		}, currentRange);
	}

	private handleKeyDown(event: KeyboardEvent): void {
		// keyCode === 229 is a special code that indicates an IME event.
		if (event.isComposing || event.keyCode === 229)
			return;
		
		//Hole aktuelle Auswahl
		console.log(event);
		let currentRange = getSelection()?.getRangeAt(0);

		//Keine Auswahl?
		if (!currentRange)
			return;

		if (event.key === "Enter") {
			//Führe die Bearbeitung durch
			this.callback(this, {
				inputType: 'insertText',
				data: '\n',
				afterCompose: false,
			}, currentRange);
		} else if (event.key == 'z' && event.ctrlKey) {
			//Undo
			this.callback(this, {
				inputType: 'historyUndo',
				data: null,
				afterCompose: false,
			}, currentRange);
		} else if (event.key == 'y' && event.ctrlKey) {
			//Undo
			this.callback(this, {
				inputType: 'historyRedo',
				data: null,
				afterCompose: false,
			}, currentRange);
		}
	}

	private handlePaste(event: ClipboardEvent): void {
		//Hole aktuelle Auswahl
		console.log(event);
		let currentRange = getSelection()?.getRangeAt(0);

		//Keine Auswahl?
		if (!currentRange)
			return;

		let text = event.clipboardData?.getData('text');
		if (!text)
			return;

		//Führe die Bearbeitung durch
		this.callback(this, {
			inputType: 'insertText',
			data: text,
			afterCompose: false,
		}, currentRange);
	}

	private handleCompositionEnd(event: CompositionEvent) {
		this.isAfterCompose = true;
		requestIdleCallback(() => {
			this.isAfterCompose = false;
		});
	}

	private handleBeforeInput(event: InputEvent) {
		var currentRange = getSelection()?.getRangeAt(0);
		if (!currentRange)
			return;

		if (event.inputType == 'historyUndo' || event.inputType == 'historyRedo') {
			//Führe die Bearbeitung durch
			this.callback(this, {
				inputType: event.inputType,
				data: null,
				afterCompose: false,
			}, currentRange);
		}
	}

	public updateFromElement(text?: string | null | boolean, selection?: Selection | AnchorSelection<MetalineLineAnchor> | null | boolean): void {
		if (text !== false) {
			if (typeof text !== 'string') {
				text = '';
				let lines = this.editor.querySelectorAll('.line');
				this.lines = new Array(lines.length);
				let i = 0;
				let currentOffset = 0;
				lines.forEach(l => {
					let content = l.textContent + "\n";
					/*if (content === "\n\n")
						content = "\n";*/

					text += content;
					this.lines[i++] = {
						node: l as HTMLElement,
						offset: currentOffset,
						length: content.length
					};
					currentOffset += content.length;
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
				let startFound = false;
				let endFound = false;
				let start = selection.start.offset;
				let end = selection.end.offset;
				for (var i = 0; i < this.lines.length; i++) {
					let line = this.lines[i];
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

					console.log(this.editContext.text.substring(0, start) + '§' + this.editContext.text.substring(start));
				}
			}
		}

		this.currentState = {
			text: this.editContext.text,
			selectionStart: this.editContext.selectionStart,
			selectionEnd: this.editContext.selectionEnd
		};
	}

	public getCurrentSelection(documentSelection?: Selection | undefined, includeNode?: boolean): AnchorSelection<MetalineLineAnchor> | null {
		if (documentSelection === undefined)
			documentSelection = getSelection() || undefined;

		if (!documentSelection || documentSelection.rangeCount == 0)
			return null;
		let range = getSelection()?.getRangeAt(0);
		if (!range)
			return null;

		if (!this.editor.contains(range.startContainer) || !this.editor.contains(range.endContainer))
			return null;

		return this.getLineSelection(range, includeNode);
	}

	public setCurrentSelection(lineSelection: AnchorSelection<MetalineLineAnchor> | null): Selection | null {
		//Nichts auswählen?
		let documentSelection = getSelection();
		if (!documentSelection)
			throw new Error("Keine Auswahl möglich");
		if (!lineSelection) {
			if (documentSelection.rangeCount != 0)
				documentSelection.removeAllRanges();

			return documentSelection;
		}

		//Hole/erzeuge Range
		let range: Range;
		if (documentSelection.rangeCount) {
			range = documentSelection.getRangeAt(0);
		} else {
			range = document.createRange();
			documentSelection.addRange(range);
		}

		//Setze Range
		this.setLineSelectionRange(documentSelection, range, lineSelection);
		return documentSelection;
	}

	public setLineSelectionRange(documentSelection: Selection, range: Range, lineSelection: AnchorSelection<MetalineLineAnchor> | AnchorSelection<LineNodeAnchor>) {
		let startLine: Node | null | undefined = lineSelection.start.lineNode;
		let endLine: Node | null | undefined = lineSelection.end.lineNode;

		console.log('selecting', lineSelection);

		if (!(startLine instanceof HTMLElement && endLine instanceof HTMLElement)) {
			let metalineLineSelection = <AnchorSelection<MetalineLineAnchor>>lineSelection;

			let startMetaline = startLine ? null : this.editor.querySelector(`.metaline[data-metaline="${metalineLineSelection.start.metaline}"]`);
			let endMetaline = metalineLineSelection.end.metaline == metalineLineSelection.start.metaline ? startMetaline
				: endLine ? null : this.editor.querySelector(`.metaline[data-metaline="${metalineLineSelection.end.metaline}"]`);
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

		//Setze die Range
		let start = SelectionHelper.getNodeAndOffset(startLine, lineSelection.start.offset);
		range.setStart(start.node, start.offset);
		let end = startLine === endLine && lineSelection.end.offset == lineSelection.start.offset ? start
			: SelectionHelper.getNodeAndOffset(endLine, lineSelection.end.offset);
		range.setEnd(end.node, end.offset);

		function findLine(metaline: Element, line: number): Element | null {
			if (line === ModificationEditor.FIRST_LINE_ID) {
				return metaline.querySelector('.line[data-line]');
			} else if (line === ModificationEditor.LAST_LINE_ID) {
				let lines = metaline.querySelectorAll('.line[data-line]');
				return lines[lines.length - 1];
			}

			return metaline.querySelector(`.line[data-line="${line}"]`);
		}
	}

	public getLineSelection(range: AbstractRange, includeNode?: boolean): AnchorSelection<MetalineLineAnchor> | null {
		//Finde Offset des Startknotens
		let start = this.getTextIndex(range.startContainer, this.getLineInfo, range.startOffset);
		if (!start)
			return null;
		
		//Finde Offset des Endknotens
		let end = range.endContainer !== range.startContainer ? this.getTextIndex(range.endContainer, this.getLineInfo, range.endOffset) : {
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
		}
	}

	private getTextIndex<T>(node: Node, stopCondition: (node: Node | null) => T | null, offset?: number): TextIndex<T> | null {
		if (!offset)
			offset = 0;

		let data = stopCondition(node);
		let current: Node | null;
		for (current = node; current && !data; current = current.parentElement, data = stopCondition(current)) {
			//Addiere die Länge aller Siblings
			for (var sibling = current.previousSibling; sibling; sibling = sibling.previousSibling) {
				if (sibling.nodeType == Node.COMMENT_NODE)
					continue;

				offset += sibling.textContent?.length || 0;
			}
		}

		if (!data || !current)
			return null;

		return {
			node: current,
			offset: offset,
			data: data
		};
	}

	private getNode(node: Node, offset: number): NodeSelectionAnchor {
		if (offset < 0)
			return getFromEnd(node, -offset - 1);
		else
			return getFromStart(node, offset);

		function getFromStart(node: Node, offset: number) {
			if (!(node instanceof HTMLElement))
				return {
					node: node,
					offset: offset,
				};

			let currentOffset = 0;
			for (let i = 0; i < node.childNodes.length; i++) {
				let child = node.childNodes[i];
				if (child.nodeType == Node.COMMENT_NODE)
					continue;

				var offsetAfter = currentOffset + (child.textContent?.length ?? 0);
				if (offset < offsetAfter || (offsetAfter == offset && i == node.childNodes.length - 1))
					return getFromStart(child, offset - currentOffset);

				currentOffset = offsetAfter;
			}

			return {
				node: node,
				offset: offset,
			};
		}

		function getFromEnd(node: Node, offset: number) {
			if (!(node instanceof HTMLElement)) {
				if (!node.textContent)
					throw new Error("Kein Textknoten gefunden");
				return {
					node: node,
					offset: node.textContent.length - offset,
				};
			}

			let currentEndOffset = 0;
			for (let i = node.childNodes.length - 1; i >= 0; i--) {
				let child = node.childNodes[i];
				if (child.nodeType == Node.COMMENT_NODE)
					continue;

				var offsetBefore = currentEndOffset + (child.textContent?.length ?? 0);
				if (offset < offsetBefore || (offsetBefore == offset && i == 0))
					return getFromEnd(child, offset - currentEndOffset);

				currentEndOffset = offsetBefore;
			}

			return {
				node: node,
				offset: offset,
			};
		}
	}

	private getLineInfo(node: Node): LineIdentifier | null {
		if (!(node instanceof HTMLElement))
			return null;
		let nodeElement: HTMLElement | null | undefined = <HTMLElement>node;

		let lineId: number | null = null;
		if (node.classList.contains('line')) {
			lineId = parseInt(nodeElement.getAttribute('data-line') ?? '');
			nodeElement = nodeElement.parentElement?.parentElement;
		} else if (nodeElement.classList.contains('metaline-lines')) {
			nodeElement = nodeElement.parentElement;
		} else if (!nodeElement.classList.contains('metaline')) {
			return null;
		}

		let metalineId = nodeElement?.getAttribute('data-metaline');
		if (metalineId == null || lineId == null)
			return null;

		return {
			metaline: metalineId,
			line: lineId
		};
	}
}

class SelectionHelper {
	static getNodeAndOffset(node: Node, offset: number): { node: Node, offset: number } {
		if (offset < 0)
			return SelectionHelper.getNodeAndOffsetFromEnd(node, offset);

		let iterator = document.createNodeIterator(node, NodeFilter.SHOW_TEXT);
		let currentOffset = 0;
		let current = iterator.nextNode();
		while (current && currentOffset + (current.textContent?.length ?? 0) < offset) {
			currentOffset += current.textContent?.length ?? 0;
			let next = iterator.nextNode();
			if (!next)
				return {
					node: current,
					offset: (current.textContent?.length ?? 0),
				};
			current = next;
		}

		return {
			node: current ?? node,
			offset: offset - currentOffset,
		};
	}

	private static getNodeAndOffsetFromEnd(node: Node, offset: number): { node: Node, offset: number } {
		let iterator = document.createNodeIterator(node, NodeFilter.SHOW_TEXT);
		let allNodes = [];
		let current = iterator.nextNode();
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
		let currentOffset = 0;
		for (let i = allNodes.length - 1; i >= 0; i--) {
			current = allNodes[i];
			if (currentOffset + (current.textContent?.length ?? 0) >= offset)
				break;

			currentOffset += current.textContent?.length ?? 0;
		}

		current ??= node;
		if (!current.textContent)
			throw new Error("Kein Textknoten gefunden");
		return {
			node: current,
			offset: current.textContent.length - (offset - currentOffset),
		};
	}

	public static getGlobalOffset(selection: Selection | null, editor: HTMLElement) {
		if (!selection)
			return null;

		const treeWalker = document.createTreeWalker(editor, NodeFilter.SHOW_TEXT);

		let anchorNodeFound = false;
		let extentNodeFound = false;
		let anchorOffset = 0;
		let extentOffset = 0;

		while (treeWalker.nextNode()) {
			const node = treeWalker.currentNode;
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
					anchorOffset += node.textContent?.length ?? 0;
				}
				if (!extentNodeFound) {
					extentOffset += node.textContent?.length ?? 0;
				}
			}
		}

		if (!anchorNodeFound || !extentNodeFound) {
			return null;
		}

		return { start: anchorOffset, end: extentOffset };
	}
}
