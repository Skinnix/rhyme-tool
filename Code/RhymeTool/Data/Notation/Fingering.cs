using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool.Data.Notation;

public sealed record Fingering(IReadOnlyList<FingeringPosition> Positions, bool EndsWithSeparator)
{
	public const char SEPARATOR = '-';

	public static int TryRead(ISheetEditorFormatter? formatter, ReadOnlySpan<char> s, out Fingering? fingering, int minLength = 3)
		=> formatter?.TryReadFingering(s, out fingering, minLength) ?? TryRead(s, out fingering, minLength);

	public static int TryRead(ReadOnlySpan<char> s, out Fingering? fingering, int minLength = 3)
	{
		fingering = null;
		if (s.Length < minLength)
			return -1;

		//Lese Positionen
		List<FingeringPosition>? oldPositions = null;
		bool endsWithSeparator = false;
		var positions = new List<FingeringPosition>();
		var enumerator = s.GetEnumerator();
		var read = 0;
		var usingSeparator = false;
		int? currentPositionValue = null;
		while (enumerator.MoveNext())
		{
			var c = enumerator.Current;
			if (c == SEPARATOR)
			{
				if (positions.Count == 0)
					return -1;

				if (endsWithSeparator)
					break;

				if (!usingSeparator)
				{
					//Kombiniere die vorherigen Positionen
					var newPositions = new List<FingeringPosition>();
					int? newPositionValue = null;
					foreach (var position in positions)
					{
						if (position == FingeringPosition.X)
						{
							if (newPositionValue != null)
							{
								newPositions.Add(new FingeringPosition(newPositionValue.Value));
								newPositionValue = null;
							}

							newPositions.Add(position);
							continue;
						}

						if (newPositionValue is null)
						{
							newPositionValue = position.Fret;
						}
						else if (newPositionValue >= 10)
						{
							newPositions.Add(new FingeringPosition(newPositionValue.Value));
							newPositionValue = position.Fret;
						}
						else
						{
							newPositionValue = newPositionValue.Value * 10 + position.Fret;
						}
					}
					if (newPositionValue is not null)
						newPositions.Add(new FingeringPosition(newPositionValue.Value));
					oldPositions = positions;
					positions = newPositions;
					usingSeparator = true;
					endsWithSeparator = true;
				}
				else if (currentPositionValue is not null)
				{
					positions.Add(new FingeringPosition(currentPositionValue.Value));
					currentPositionValue = null;
					oldPositions = null;
					endsWithSeparator = true;
				}
				else
					break;

				read++;
				continue;
			}
			else if (c == 'x')
			{
				if (currentPositionValue is not null)
				{
					positions.Add(new FingeringPosition(currentPositionValue.Value));
					currentPositionValue = null;
				}

				positions.Add(FingeringPosition.X);
			}
			else if (char.IsDigit(c))
			{
				if (currentPositionValue >= 10)
				{
					positions.Add(new FingeringPosition(currentPositionValue.Value));
					currentPositionValue = c - '0';
				}
				else if (currentPositionValue is not null)
				{
					currentPositionValue = currentPositionValue.Value * 10 + (c - '0');
				}
				else if (usingSeparator)
				{
					currentPositionValue = c - '0';
				}
				else
				{
					positions.Add(new FingeringPosition(c - '0'));
				}
			}
			else
			{
				break;
			}

			oldPositions = null;
			endsWithSeparator = false;
			read++;
		}

		//Füge ggf. die letzte, angefangene Position hinzu
		if (currentPositionValue is not null)
			positions.Add(new FingeringPosition(currentPositionValue.Value));

		//Falls direkt nach einem Trennzeichen abgebrochen wurde, verwende die alten Positionen
		else if (oldPositions is not null)
		{
			positions = oldPositions;
		}

		//Mindestens zwei Saiten müssen gelesen worden sein
		if (positions.Count < minLength)
			return -1;

		fingering = new Fingering(positions, endsWithSeparator);
		return read;
	}

	public override string ToString() => ToString(null);

	public string ToString(ISheetFormatter? formatter)
	{
		var s = Positions.Any(p => p.Fret >= 10)
			? string.Join(SEPARATOR, Positions.Select(p => p.ToString()))
			: string.Join("", Positions.Select(p => p.ToString()));

		if (EndsWithSeparator)
			s += SEPARATOR;

		return s;
	}
}

public record struct FingeringPosition(int Fret)
{
	private const int fretX = -1;

	public static FingeringPosition X { get; } = new(fretX);

	public override string ToString()
		=> Fret == fretX ? "x"
		//: Fret >= 10 ? $"({Fret})"
		: Fret.ToString();
}
