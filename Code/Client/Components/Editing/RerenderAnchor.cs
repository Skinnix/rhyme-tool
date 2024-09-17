using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Skinnix.RhymeTool.Client.Components.Editing;

public class RerenderAnchor : ComponentBase
{
	[Inject] public IJSRuntime js { get; set; } = null!;

	public Action? BeforeRender { get; set; }
	public Func<ValueTask>? AfterRender { get; set; }

	protected override bool ShouldRender() => true;

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		await base.OnAfterRenderAsync(firstRender);

		if (AfterRender is not null)
			await AfterRender();
	}

	public void TriggerRender()
	{
		BeforeRender?.Invoke();
		StateHasChanged();
	}
}
