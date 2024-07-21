using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.WebView.Maui;
using Skinnix.RhymeTool.Client.Services;

namespace Skinnix.RhymeTool.MauiBlazor.Services;

internal class MauiDialogService(IMauiUiService uiService) : IDialogService
{
	public Task ShowErrorAsync(string message, string? title)
		=> uiService.MainPage.DisplayAlert(title, message, "OK");

	public Task<bool> ConfirmAsync(string message, string? title)
		=> uiService.MainPage.DisplayAlert(title, message, "Ja", "Nein");
}
