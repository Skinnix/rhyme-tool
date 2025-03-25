using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.Compoetry.Maui.Views;

public class PersistentIntValueConverter : IValueConverter
{
	private int validValue;

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is int intValue)
			validValue = intValue;

		return validValue.ToString();
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is not string stringValue)
			return validValue;

		if (string.IsNullOrWhiteSpace(stringValue))
		{
			validValue = 0;
			return "0";
		}

		if (int.TryParse(stringValue, out var intValue))
		{
			validValue = intValue;
			return validValue.ToString();
		}

		var replaced = stringValue.Replace("-", string.Empty);
		var minusCount = stringValue.Length - replaced.Length;
		if (int.TryParse(replaced, out intValue))
		{
			if (minusCount % 2 == 1)
				intValue = -intValue;

			validValue = intValue;
			return validValue.ToString();
		}

		return validValue.ToString();
	}
}
