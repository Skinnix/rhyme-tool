using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Skinnix.RhymeTool.Client.Data.Structure;

namespace Skinnix.RhymeTool.Client.Components.Editing
{
	partial class RhymeEditor
	{
		[Parameter] public RhymeDocument? Document { get; set; }

		private ElementReference editor;

		protected override async Task OnAfterRenderAsync(bool firstRender)
		{
			await base.OnAfterRenderAsync(firstRender);

			await js.InvokeVoidAsync("initializeEditor", editor);
		}
	}
}
