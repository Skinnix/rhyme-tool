using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool.Data.Notation;

public enum NoteValue
{
	[EnumName(" ")]
	Unknown = 0,

	[EnumName("𝅝")]
	Whole = 1,

	[EnumName("𝅗𝅥")]
	Half = 2,

	[EnumName("𝅘𝅥")]
	Quarter = 3,

	[EnumName("𝅘𝅥𝅮")]
	Eighth = 4,

	[EnumName("𝅘𝅥𝅯")]
	Sixteenth = 5,

	[EnumName("𝅘𝅥𝅰")]
	ThirtySecond = 6,

	[EnumName("𝅘𝅥𝅱")]
	SixtyFourth = 7,

	[EnumName("𝅘𝅥𝅲")]
	HundredTwentyEighth = 8,
}

public readonly record struct NoteLength(params NoteValue[] Values)
{
	public int Dots { get; init; }
	public bool IsRest { get; init; }

	public static NoteLength Create(NoteValue length, int count)
	{
		switch (count)
		{
			case 1:
				return new(length);
			case 2:
				return new(length - 1);
			case 3:
				return new(length - 1)
				{
					Dots = 1
				};
			case 4:
				return new(length - 2);
			case 5:
				return new(length - 2, length);
			case 6:
				return new(length - 2)
				{
					Dots = 1
				};
			case 7:
				return new(length - 2)
				{
					Dots = 2
				};
			case 8:
				return new(length - 3);
			case 9:
				return new(length - 3, length);
			case 10:
				return new(length - 3, length - 1);
			case 11:
				return new(length - 3, length - 1)
				{
					Dots = 1
				};
			case 12:
				return new(length - 3)
				{
					Dots = 1
				};
			case 13:
				return new(length - 3, length - 2)
				{
					Dots = 1
				};
			case 14:
				return new(length - 3, length - 2)
				{
					Dots = 2
				};
			case 15:
				return new(length - 3)
				{
					Dots = 3
				};
			case 16:
				return new(length - 4);
			default:
				return new(Enumerable.Repeat(length, count).ToArray());
		}
	}

	public override string ToString() => ToString(null);

	public string ToString(ISheetFormatter? formatter)
		=> formatter?.ToString(this)
		?? string.Join(null, Values.Select(EnumNameAttribute.GetDisplayName)) + new string('·', Dots);
}
