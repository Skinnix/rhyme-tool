using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Android.Views;
using Microsoft.Maui.Platform;

using Application = Android.App.Application;

namespace Skinnix.Compoetry.Maui;

public static class CursorExtensions
{
	public static void SetCustomCursor(this VisualElement visualElement, CursorIcon cursor, IMauiContext? mauiContext = null)
	{
		if (OperatingSystem.IsAndroidVersionAtLeast(24))
		{
			mauiContext ??= visualElement.Handler?.MauiContext
				?? App.Current?.Handler?.MauiContext;
			ArgumentNullException.ThrowIfNull(mauiContext);
			var view = visualElement.ToPlatform(mauiContext);
			view.PointerIcon = PointerIcon.GetSystemIcon(Application.Context, GetCursor(cursor));
		}
	}

	static PointerIconType GetCursor(CursorIcon cursor)
	{
#if ANDROID24_0_OR_GREATER
		return cursor switch
		{
			CursorIcon.Hand => PointerIconType.Hand,
			CursorIcon.IBeam => PointerIconType.AllScroll,
			CursorIcon.Cross => PointerIconType.Crosshair,
			CursorIcon.Arrow => PointerIconType.Arrow,
			CursorIcon.SizeAll => PointerIconType.TopRightDiagonalDoubleArrow,
			CursorIcon.Wait => PointerIconType.Wait,
			_ => PointerIconType.Default,
		};
#else
		return PointerIconType.Default;
#endif
	}
}
