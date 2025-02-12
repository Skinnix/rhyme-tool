using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Skinnix.RhymeTool.Client;

partial class App
{
	private ErrorBoundary? errorBoundary;

	protected override async Task OnParametersSetAsync()
	{
		await base.OnParametersSetAsync();

		navigation.LocationChanged += OnLocationChanged;

		if (OperatingSystem.IsBrowser())
			await js.InvokeVoidAsync("enableSynchronousInvoke");
	}

	private async void OnLocationChanged(object? sender, LocationChangedEventArgs args)
	{
		await js.InvokeVoidAsync("hideAllOffcanvases");
	}

	private async Task ReloadAfterError()
	{
		errorBoundary?.Recover();

		await js.InvokeVoidAsync("location.reload");
	}
}
