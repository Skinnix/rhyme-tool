using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Skinnix.RhymeTool;

namespace Skinnix.Compoetry.Maui.Views;

public class IntValueBehavior : Behavior<Entry>
{
	private readonly WeakDictionary<Entry, Observer> observers = new();

	protected override void OnAttachedTo(Entry entry)
	{
		observers[entry] = new Observer(entry);
		base.OnAttachedTo(entry);
	}

	protected override void OnDetachingFrom(Entry entry)
	{
		if (observers.TryGetValue(entry, out var observer))
		{
			observer.Dispose();
			observers.Remove(entry);
		}

		base.OnDetachingFrom(entry);
	}

	private class Observer : IDisposable
	{
		private readonly Entry entry;
		private int validValue;

		public Observer(Entry entry)
		{
			this.entry = entry;
			entry.TextChanged += OnEntryTextChanged;

			int.TryParse(entry.Text, out validValue);
		}

		public void Dispose()
		{
			entry.TextChanged -= OnEntryTextChanged;
		}

		private void OnEntryTextChanged(object? sender, TextChangedEventArgs args)
		{
			if (string.IsNullOrWhiteSpace(args.NewTextValue))
			{
				validValue = 0;
				entry.Text = "0";
				return;
			}

			if (int.TryParse(args.NewTextValue, out int value))
			{
				validValue = value;
				var validValueString = validValue.ToString();
				if (validValueString != args.NewTextValue)
					entry.Text = validValueString;

				return;
			}

			var replaced = args.NewTextValue.Replace("-", string.Empty);
			var minusCount = args.NewTextValue.Length - replaced.Length;
			if (int.TryParse(replaced, out value))
			{
				if (minusCount % 2 == 1)
					value = -value;

				validValue = value;
				var validValueString = validValue.ToString();
				if (validValueString != args.NewTextValue)
					entry.Text = validValueString;

				return;
			}

			entry.Text = validValue.ToString();
		}
	}
}