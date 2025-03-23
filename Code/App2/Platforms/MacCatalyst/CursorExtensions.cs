using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AppKit;

namespace Skinnix.Compoetry.Maui;

public static class CursorExtensions
{
	public static void SetCustomCursor(this IElement element, CursorIcon cursor, IMauiContext? mauiContext)
	{
		ArgumentNullException.ThrowIfNull(mauiContext);
		var view = element.ToPlatform(mauiContext);
		if (view.GestureRecognizers is not null)
		{
			foreach (var recognizer in view.GestureRecognizers.OfType<PointerUIHoverGestureRecognizer>())
			{
				view.RemoveGestureRecognizer(recognizer);
			}
		}
		view.AddGestureRecognizer(new PointerUIHoverGestureRecognizer(r =>
		{
			switch (r.State)
			{
				case UIGestureRecognizerState.Began:
					GetNSCursor(cursor).Set();
					break;
				case UIGestureRecognizerState.Ended:
					NSCursor.ArrowCursor.Set();
					break;
			}
		}));
	}
	static NSCursor GetNSCursor(CursorIcon cursor)
	{
		return cursor switch
		{
			CursorIcon.Hand => NSCursor.OpenHandCursor,
			CursorIcon.IBeam => NSCursor.IBeamCursor,
			CursorIcon.Cross => NSCursor.CrosshairCursor,
			CursorIcon.Arrow => NSCursor.ArrowCursor,
			CursorIcon.SizeAll => NSCursor.ResizeUpCursor,
			CursorIcon.Wait => NSCursor.OperationNotAllowedCursor,
			_ => NSCursor.ArrowCursor,
		};
	}
	class PointerUIHoverGestureRecognizer : UIHoverGestureRecognizer
	{
		public PointerUIHoverGestureRecognizer(Action<UIHoverGestureRecognizer> action) : base(action)
		{
		}
	}
}