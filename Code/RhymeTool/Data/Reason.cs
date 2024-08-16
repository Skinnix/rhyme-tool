using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool.Data;

public abstract record ReasonBase(string Label, string Code);

public sealed record Reason : ReasonBase
{
	public Reason(string label, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string? filePath = null)
		: base(label, CreateCode(filePath, lineNumber))
	{
	}

	private static string CreateCode(string? filePath, int lineNumber)
	{
		if (filePath is null)
			return lineNumber.ToString();

		//Dateiname ohne Extension
		var fileName = Path.GetFileNameWithoutExtension(filePath);

		//Trenne CamelCase
		var parts = fileName.SplitAlternating(char.IsUpper).Take(4).ToArray();

		//Wie viele Teile gibt es?
		string namePart;
		switch (parts.Length)
		{
			case 4:
				namePart = $"{parts[0][0]}{parts[1][0]}{parts[2][0]}{parts[3][0]}";
				break;
			case 3:
				if (parts[0].Length >= 2)
					namePart = $"{parts[0][0..2]}{parts[1][0]}{parts[2][0]}";
				else if (parts[1].Length >= 2)
					namePart = $"{parts[0][0]}{parts[1][0..2]}{parts[2][0]}";
				else if (parts[2].Length >= 2)
					namePart = $"{parts[0][0]}{parts[1][0]}{parts[2][0..2]}";
				else
					namePart = $"{parts[0]}{parts[1]}{parts[2]}";
				break;
			case 2:
				if (parts[0].Length >= 2)
					if (parts[1].Length >= 2)
						namePart = $"{parts[0][0..2]}{parts[1][0..2]}";
					else if (parts[0].Length >= 3)
						namePart = $"{parts[0][0..3]}{parts[1][0]}";
					else
						namePart = $"{parts[0][0..2]}{parts[1][0]}";
				else if (parts[1].Length >= 3)
					namePart = $"{parts[0][0]}{parts[1][0..3]}";
				else
					namePart = $"{parts[0][0]}{parts[1]}";
				break;
			case 1:
				namePart = parts[0].Length >= 4 ? parts[0][0..4] : parts[0];
				break;
			default:
				namePart = fileName.Length >= 4 ? fileName[0..4] : fileName;
				break;
		}

		return $"{namePart}{lineNumber}";
	}
}