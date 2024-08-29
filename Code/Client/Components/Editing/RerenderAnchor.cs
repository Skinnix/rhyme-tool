using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Skinnix.RhymeTool.Client.Components.Editing;

public class RerenderAnchor : ComponentBase
{
	[Inject] public IJSRuntime js { get; set; } = null!;

	private volatile Func<Task>? invokeOnRerender;

	protected override void BuildRenderTree(RenderTreeBuilder builder) { }

	protected override bool ShouldRender() => true;

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		await base.OnAfterRenderAsync(firstRender);

		Console.WriteLine("Rerender Anchor");

		if (invokeOnRerender is not null)
		{
			var task = invokeOnRerender();
			invokeOnRerender = null;
			await task;
		}
	}
}
