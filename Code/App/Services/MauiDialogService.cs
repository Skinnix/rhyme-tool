using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.JSInterop;
using Skinnix.RhymeTool.Client.Services;

namespace Skinnix.RhymeTool.MauiBlazor.Services;

internal class MauiDialogService(IMauiUiService uiService, IJSRuntime js) : IDialogService
{
	public ValueTask ShowErrorAsync(string message, string? title)
		=> new(uiService.MainPage.DisplayAlert(title, message, "OK"));

	public ValueTask<bool> ConfirmAsync(string message, string? title)
		=> new(uiService.MainPage.DisplayAlert(title, message, "Ja", "Nein"));

	public ValueTask ShowToast(string message, string title, TimeSpan delay)
		=> js.InvokeVoidAsync("showToast", message, title, delay.TotalMilliseconds);
}
