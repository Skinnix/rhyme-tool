using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.Compoetry.Maui;

public class CursorBehavior
{
	public static readonly BindableProperty CursorProperty =
		BindableProperty.CreateAttached("Cursor", typeof(CursorIcon), typeof(CursorBehavior), CursorIcon.Arrow, propertyChanged: CursorChanged);

	private static void CursorChanged(BindableObject bindable, object oldValue, object newValue)
	{
		if (bindable is VisualElement element)
		{
			element.SetCustomCursor((CursorIcon)newValue, Application.Current?.Windows.FirstOrDefault()?.Handler.MauiContext);
		}
	}

	public static CursorIcon GetCursor(BindableObject view) => (CursorIcon)view.GetValue(CursorProperty);
	public static void SetCursor(BindableObject view, CursorIcon value) => view.SetValue(CursorProperty, value);
}