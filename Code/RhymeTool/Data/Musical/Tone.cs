using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool.Data.Musical;

public enum Tone : int
{
	C = 0,
	CSharp = 1,
	D = 2,
	DSharp = 3,
	E = 4,
	F = 5,
	FSharp = 6,
	G = 7,
	GSharp = 8,
	A = 9,
	ASharp = 10,
	B = 11
}

public static class ToneExtensions
{
	public static Tone Transpose(this Tone tone, int transpose)
	{
		var toneValue = (int)tone + transpose;
		if (toneValue < 0)
			toneValue += 12;
		else if (toneValue >= 12)
			toneValue -= 12;
		return (Tone)toneValue;
	}
}