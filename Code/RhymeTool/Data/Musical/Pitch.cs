using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool.Data.Musical;

public readonly record struct Pitch
{
	private readonly int value;

	public Tone Tone => value < 0 ? (Tone)(value % 12 + 12) : (Tone)(value % 12);
	public int Octave => value / 12;

	public Pitch(Tone tone, int octave)
	{
		value = (octave * 12) + (int)tone;
	}

	public Pitch(int value)
	{
		this.value = value;
	}

	public Pitch Transpose(int transpose) => new(value + transpose);
}