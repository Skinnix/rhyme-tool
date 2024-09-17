declare interface AwaitingModification {

}

declare interface ObservedModification {
	mutations: MutationRecord[];
}

declare interface LineIdentifier {
	metaline: string,
	line: number | null,
}

declare interface TextIndex<T> {
	offset: number;
	data: T;
}

declare interface NodeSelectionAnchor {
	node: Node,
	offset: number,
}

declare interface NodeSelection {
	start: NodeSelectionAnchor,
	end: NodeSelectionAnchor,
}

declare interface LineSelectionAnchor extends LineIdentifier {
	offset: number,
}

declare interface LineSelection {
	start: LineSelectionAnchor,
	end: LineSelectionAnchor,
}

declare interface ModificationData {
	inputType: string,
	data: string,
	editRange?: LineSelection,
}

declare interface MetalineEditResult {
	success: boolean,
	willRender: boolean,
	selection: LineSelection | null,
	failReason?: {
		label: string,
		code: string,
	},
}

declare type EditorCallback = (editor: ModificationEditor, data: ModificationData, selectionRange: Range | null) => void;

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

class ModificationEditor {
	static FIRST_LINE_ID = -1;
	static LAST_LINE_ID = -2;

	private observer: MutationObserver;
	private observerRunning = false;
	private modificationHandlers: ((modification: ObservedModification) => void)[] = [];
	private observedModification: ObservedModification | null = null;
	private selectionBeforeInput: Range | null = null;
	private actualCompositionUpdate = false;

	constructor(private editor: HTMLElement, private callback: EditorCallback) {
		editor.addEventListener('beforeinput', this.handleBeforeInput.bind(this));
		editor.addEventListener('input', this.handleInput.bind(this));
		editor.addEventListener('compositionstart', event => {
			console.log('compositionstart', event);
		});
		editor.addEventListener('compositionupdate', event => {
			this.actualCompositionUpdate = true;
			console.log('compositionupdate', event);
		});
		editor.addEventListener('compositionend', event => {
			console.log('compositionend', event);
		});

		let self = this;
		this.observer = new MutationObserver(function (mutations) {
			self.observedModification = {
				mutations: mutations
			};
			return;

			//Wird gerade nicht beobachtet?
			let handler = self.modificationHandlers.shift();
			if (!handler)
				return;

			handler({
				mutations: mutations,
			});
		});
	}

	public stopRevertingModifications() {
		this.stopModificationObserver();
	}

	private handleBeforeInput(event: InputEvent) {
		event.preventDefault();
		event.stopImmediatePropagation();

		console.log(event);

		this.selectionBeforeInput = getSelection().getRangeAt(0)?.cloneRange();
		this.observedModification = null;
		this.startModificationObserver(modification => { });
		/*this.startModificationObserver(modification => {
			//Mache die Änderung rückgängig
			this.revertModification(modification);

			//Wiederherstellen der Auswahl
			let selection = getSelection();
			if (selectionRange) {
				if (selection.rangeCount == 1) {
					let currentRange = selection.getRangeAt(0);
					currentRange.setStart(selectionRange.startContainer, selectionRange.startOffset);
					currentRange.setEnd(selectionRange.endContainer, selectionRange.endOffset);
				} else {
					selection.removeAllRanges();
					selection.addRange(selectionRange);
				}
			} else {
				selection.removeAllRanges();
			}
		});*/

		let editRange: LineSelection | null = null;
		if (event.inputType == 'insertCompositionText') {
			//Findet gerade gar keine Komposition statt?
			if (!this.actualCompositionUpdate) {
				//Ungültiges Event
				return;
			}
			this.actualCompositionUpdate = false;

			//Ermittle den Bereich der Komposition
			let targetRange = event.getTargetRanges()[0];
			if (targetRange) {
				editRange = this.getLineSelection(targetRange);
			}
		}

		this.callback(this, {
			inputType: event.inputType,
			editRange: editRange,
			data: event.data
		}, this.selectionBeforeInput);
	}

	private handleInput(event: InputEvent) {
		if (this.observedModification) {
			this.revertModification(this.observedModification);
			this.observedModification = null;
		}

		//Wiederherstellen der Auswahl
		if (this.selectionBeforeInput) {
			let selection = getSelection();
			if (this.selectionBeforeInput) {
				if (selection.rangeCount == 1) {
					let currentRange = selection.getRangeAt(0);
					currentRange.setStart(this.selectionBeforeInput.startContainer, this.selectionBeforeInput.startOffset);
					currentRange.setEnd(this.selectionBeforeInput.endContainer, this.selectionBeforeInput.endOffset);
				} else {
					selection.removeAllRanges();
					selection.addRange(this.selectionBeforeInput);
				}
			} else {
				selection.removeAllRanges();
			}

			this.selectionBeforeInput = null;
		}
	}

	private startModificationObserver(handler: (modification: ObservedModification) => void) {
		this.modificationHandlers.push(handler);
		if (!this.observerRunning) {
			this.observer.observe(this.editor, {
				childList: true,
				subtree: true,
				characterData: true,
				characterDataOldValue: true
			});
			this.observerRunning = true;
		}
	}

	private stopModificationObserver() {
		//this.awaitingModification = null;
		//this.observer.disconnect();
		//this.observerRunning = false;
		this.modificationHandlers = [];
	}

	private revertModification(modification: ObservedModification) {
		var mutations = Array.from(modification.mutations.reverse());
		while (mutations.length != 0) {
			for (var m = 0; m < mutations.length; m++) {
				var mutation = mutations[m];
				if (mutation.type == 'childList') {
					if (mutation.removedNodes.length > 0) {
						if (mutation.nextSibling) {
							if (!mutation.target.contains(mutation.nextSibling))
								continue;

							for (var i = 0; i < mutation.removedNodes.length; i++) {
								var node = mutation.removedNodes[i];
								mutation.target.insertBefore(node, mutation.nextSibling);
							}
						} else {
							for (var i = 0; i < mutation.removedNodes.length; i++) {
								var node = mutation.removedNodes[i];
								mutation.target.appendChild(node);
							}
						}
					}

					if (mutation.addedNodes.length > 0) {
						var found = true;
						for (var j = 0; j < mutation.addedNodes.length; j++) {
							var node = mutation.addedNodes[j];
							if (!node.parentElement) {
								found = false;
								break;
							}
						}
						if (!found)
							continue;

						for (var j = 0; j < mutation.addedNodes.length; j++) {
							var node = mutation.addedNodes[j];
							node.parentElement.removeChild(node);
						}
					}
				} else if (mutation.type == 'characterData') {
					mutation.target.nodeValue = mutation.oldValue;
				}

				mutations.splice(m, 1);
				m--;
			}
		}
	}

	public getCurrentSelection(): LineSelection | null {
		let range = getSelection()?.getRangeAt(0);
		if (!range)
			return null;

		return this.getLineSelection(range);
	}

	public setCurrentSelection(selection: LineSelection | null) {
		let documentSelection = getSelection();
		if (!selection) {
			if (documentSelection.rangeCount != 0)
				documentSelection.removeAllRanges();

			return;
		}

		let range = documentSelection.getRangeAt(0);
		if (!range) {
			range = document.createRange();
			documentSelection.addRange(range);
		}

		let startMetaline = this.editor.querySelector(`.metaline[data-metaline="${selection.start.metaline}"]`);
		let endMetaline = selection.end.metaline == selection.start.metaline ? startMetaline
			: this.editor.querySelector(`.metaline[data-metaline="${selection.end.metaline}"]`);
		let startLine = findLine(startMetaline, selection.start.line);
		let endLine = selection.end.metaline == selection.start.metaline && selection.end.line == selection.start.line ? startMetaline
			: findLine(endMetaline, selection.end.line);

		let start = this.getNode(startLine, selection.start.offset);
		let end = selection.end.metaline == selection.start.metaline && selection.end.line == selection.start.line && selection.end.offset == selection.start.offset ? start
			: this.getNode(endLine, selection.end.offset);
		range.setStart(start.node, start.offset);
		range.setEnd(end.node, end.offset);

		function findLine(metaline: Element, line: number): Element {
			if (line === ModificationEditor.FIRST_LINE_ID) {
				return metaline.querySelector('.line[data-line]');
			} else if (line === ModificationEditor.LAST_LINE_ID) {
				let lines = metaline.querySelectorAll('.line[data-line]');
				return lines[lines.length - 1];
			}

			return metaline.querySelector(`.line[data-line="${selection.start.line}"]`);
		}
	}

	private getLineSelection(range: AbstractRange): LineSelection {
		//Finde Offset des Startknotens
		let start = this.getTextIndex(range.startContainer, this.getLineInfo, range.startOffset);

		//Finde Offset des Endknotens
		let end = range.endContainer !== range.startContainer ? this.getTextIndex(range.endContainer, this.getLineInfo, range.endOffset) : {
			offset: start.offset + range.endOffset - range.startOffset,
			data: start.data,
		};

		return {
			start: {
				metaline: start.data.metaline,
				line: start.data.line,
				offset: start.offset
			},
			end: {
				metaline: end.data.metaline,
				line: end.data.line,
				offset: end.offset
			}
		}
	}

	private getTextIndex<T>(node: Node, stopCondition: (node: Node) => T | null, offset?: number): TextIndex<T> {
		if (!offset)
			offset = 0;

		let data = stopCondition(node);
		for (let current = node; !data; current = current.parentElement, data = stopCondition(current)) {
			//Addiere die Länge aller Siblings
			for (var sibling = current.previousSibling; sibling; sibling = sibling.previousSibling) {
				if (sibling.nodeType == Node.COMMENT_NODE)
					continue;

				offset += sibling.textContent?.length || 0;
			}
		}

		return {
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

				var offsetAfter = currentOffset + child.textContent.length;
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
			if (!(node instanceof HTMLElement))
				return {
					node: node,
					offset: node.textContent.length - offset,
				};

			let currentEndOffset = 0;
			for (let i = node.childNodes.length - 1; i >= 0; i--) {
				let child = node.childNodes[i];
				if (child.nodeType == Node.COMMENT_NODE)
					continue;

				var offsetBefore = currentEndOffset + child.textContent.length;
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
		let nodeElement = <HTMLElement>node;

		let lineId: number | null = null;
		if (nodeElement.classList.contains('line')) {
			lineId = parseInt(nodeElement.getAttribute('data-line'));
			nodeElement = nodeElement.parentElement.parentElement;
		} else if (nodeElement.classList.contains('metaline-lines')) {
			nodeElement = nodeElement.parentElement;
		} else if (!nodeElement.classList.contains('metaline')) {
			return null;
		}

		let metalineId = nodeElement.getAttribute('data-metaline');
		return {
			metaline: metalineId,
			line: lineId
		};
	}
}