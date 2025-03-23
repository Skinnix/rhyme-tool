using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.Platform;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml;
using Windows.UI.Core;
using Microsoft.Maui.Controls;
using Microsoft.Maui;

namespace Skinnix.Compoetry.Maui;

public static class CursorExtensions
{
	public static void SetCustomCursor(this VisualElement element, CursorIcon cursor, IMauiContext? mauiContext = null)
	{
		mauiContext ??= element.Handler?.MauiContext
			?? App.Current?.Handler?.MauiContext;
		ArgumentNullException.ThrowIfNull(mauiContext);
		UIElement view = element.ToPlatform(mauiContext);
		view.PointerEntered += ViewOnPointerEntered;
		view.PointerExited += ViewOnPointerExited;
		void ViewOnPointerExited(object sender, PointerRoutedEventArgs e)
		{
			view.ChangeCursor(CursorIcon.Arrow);
		}
		void ViewOnPointerEntered(object sender, PointerRoutedEventArgs e)
		{
			view.ChangeCursor(cursor);
		}
	}

	public static void ChangeCursor(this VisualElement element, CursorIcon cursor, IMauiContext? mauiContext = null)
	{
		mauiContext ??= element.Handler?.MauiContext
			?? App.Current?.Handler?.MauiContext;
		ArgumentNullException.ThrowIfNull(mauiContext);
		var view = element.ToPlatform(mauiContext);

		view.ChangeCursor(cursor);
	}

	public static void ChangeCursor(this UIElement uiElement, CursorIcon cursor)
		=> uiElement.ChangeCursor(GetCursor(cursor));
	public static void ChangeCursor(this UIElement uiElement, CoreCursorType cursor)
		=> uiElement.ChangeCursor(new CoreCursor(cursor, 1));
	public static void ChangeCursor(this UIElement uiElement, CoreCursor cursor)
		=> uiElement.ChangeCursor(InputCursor.CreateFromCoreCursor(cursor));
	public static void ChangeCursor(this UIElement uiElement, InputCursor cursor)
	{
		Type type = typeof(UIElement);
		type.InvokeMember("ProtectedCursor", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.SetProperty | BindingFlags.Instance, null, uiElement, new object[] { cursor });
	}

	static CoreCursorType GetCursor(CursorIcon cursor)
	{
		return cursor switch
		{
			CursorIcon.Hand => CoreCursorType.Hand,
			CursorIcon.IBeam => CoreCursorType.IBeam,
			CursorIcon.Cross => CoreCursorType.Cross,
			CursorIcon.Arrow => CoreCursorType.Arrow,
			CursorIcon.SizeAll => CoreCursorType.SizeAll,
			CursorIcon.Wait => CoreCursorType.Wait,
			_ => CoreCursorType.Arrow,
		};
	}
}
