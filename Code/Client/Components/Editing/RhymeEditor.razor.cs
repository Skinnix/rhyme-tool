using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Skinnix.RhymeTool.Client.Data.Structure;

namespace Skinnix.RhymeTool.Client.Components.Editing
{
	partial class RhymeEditor : IDisposable
	{
		[Parameter] public RhymeDocument? Document { get; set; }

		private readonly object contentLock = new();

		private DotNetObjectReference<RhymeEditor>? self;
		private ElementReference content;
		private ElementReference editor;

		private Guid refreshGuid = Guid.NewGuid();

		#region Lifecycle
		protected override void OnInitialized()
		{
			base.OnInitialized();

			self = DotNetObjectReference.Create(this);
		}

		protected override async Task OnAfterRenderAsync(bool firstRender)
		{
			await base.OnAfterRenderAsync(firstRender);

			if (firstRender)
				await js.InvokeVoidAsync("initializeEditor", content, editor, self);
			else
				await js.InvokeVoidAsync("editorRenderingFinished", content, editor);
		}

		public void Dispose()
		{
			self?.Dispose();
		}
		#endregion

		[JSInvokable]
		public void InsertNewLine(int index)
		{
			if (Document == null)
				return;

			lock (contentLock)
			{
				var newLine = new TextRhymeNode();
				Document.Nodes.Insert(index, newLine);
			}

			Refresh();
		}

		[JSInvokable]
		public void OnContentModified(List<NodeModification> modifiedNodes)
		{
			if (Document == null)
				return;

			var changed = false;

			lock (contentLock)
			{
				foreach (var modification in modifiedNodes)
				{
					//added?
					if (modification.Id == null)
					{
						//get path
						var currentNodes = Document.Nodes;
						foreach (var step in modification.Path.SkipLast(1))
						{
							var child = Guid.TryParse(step, out var stepId) ? currentNodes.Find(stepId)
								: int.TryParse(step, out var stepIndex) ? currentNodes[stepIndex]
								: throw new FormatException("Invalid ID format");

							if (child is not CompositeRhymeNode composite)
								throw new InvalidOperationException("Node not found");
							currentNodes = composite.Nodes;
						}

						//create and insert node
						if (!int.TryParse(modification.Path[modification.Path.Count - 1], out var index))
							throw new FormatException("Invalid ID format: index expected");
						var newNode = CreateNode(modification);
						currentNodes.Insert(index, newNode);
						changed = true;
					}
					//removed?
					else if (modification.Content == null)
					{
						//remove node
						if (Document.Nodes.RemoveRecursive(modification.Id ?? throw new ArgumentException("ID expected")) != null)
							changed = true;
					}
					//modified
					else
					{
						//find node
						var node = Document.Nodes.FindRecursive(modification.Id ?? throw new ArgumentException("ID expected"))
							?? throw new InvalidOperationException("Node not found");

						//set content
						if (node is TextRhymeNode textNode)
						{
							if (textNode.Text != modification.Content)
							{
								textNode.Text = modification.Content;
								changed = true;
							}
						}
					}
				}
			}

			if (changed)
				Refresh();
		}

		private void Refresh()
		{
			refreshGuid = Guid.NewGuid();
			StateHasChanged();
		}

		private RhymeNode CreateNode(NodeModification modification)
			=> modification.Tag switch
			{
				"P" => new TextRhymeNode(modification.Content),
				_ => throw new ArgumentException("Invalid node tag")
			};

		public record NodeModification(
			string Action,
			string Tag,
			Guid? Id,
			string Content,
			List<string> Path);
	}
}
