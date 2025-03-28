﻿using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text;
using Skinnix.RhymeTool;

namespace Skinnix.RhymeTool.Data.Notation;

public enum ChordQuality : byte
{
    [EnumName("")]
    Major,

    [EnumName("min", "m", "-", PreferredName = "m", Blacklist = ["ma"])]
    Minor,

    [EnumName("0", "o", "dim")]
    Diminished,

    [EnumName("+", "aug")]
    Augmented,
}

public sealed record Chord(Note Root, ChordQuality Quality, string OriginalText)
{
	public Note? Bass { get; init; }
	public IReadOnlyList<ChordAlteration> Alterations { get; init; } = [];

	public ChordFormat Format(ISheetFormatter? formatter)
		=> (formatter ?? DefaultSheetFormatter.Instance).Format(this);

	public override string ToString()
	{
		var sb = new StringBuilder();
		sb.Append(Root);
		sb.Append(Quality.GetDisplayName());

		var firstAlteration = true;
		foreach (var alteration in Alterations)
		{
			if (firstAlteration)
				firstAlteration = false;
			else
				sb.Append('/');

			sb.Append(alteration);
		}

		if (Bass is not null)
			sb.Append($"/{Bass}");

		return sb.ToString();
	}

	public string ToString(ISheetFormatter? formatter)
		=> formatter?.ToString(this) ?? ToString();

	public static int TryRead(ISheetEditorFormatter? formatter, ReadOnlySpan<char> s, out Chord? chord)
		=> formatter?.TryReadChord(s, out chord) ?? TryRead(s, out chord);

	public static int TryRead(ReadOnlySpan<char> s, out Chord? chord)
	{
		chord = null;
		if (s.IsEmpty)
			return -1;

		//Lese Note
		var noteLength = Note.TryRead(s, out var note);
		if (noteLength == -1)
			return -1;

		//Lese Akkordtyp
		var offset = noteLength;
		var qualityLength = EnumExtensions.TryRead(s[offset..], out ChordQuality quality);
		if (qualityLength != -1)
			offset += qualityLength;

		//Slash als nächstes?
		var slashRead = false;
		if (offset < s.Length && s[offset] == '/')
		{
			slashRead = true;
			offset++;
		}

		//Lese Alterationen
		var alterations = new List<ChordAlteration>();
		Note? bassNote = null;
		while (offset < s.Length)
		{
			//Lese Alteration
			var alterationLength = ChordAlteration.TryRead(s[offset..], out var alteration);
			if (alterationLength != -1 && (!slashRead || alterations.Count > 0))
			{
				slashRead = false;
				alterations.Add(alteration);
				offset += alterationLength;

				//Kein Slash als nächstes?
				if (offset >= s.Length || s[offset] != '/')
					break;

				//Lese Slash
				slashRead = true;
				offset++;
			}
			else if (slashRead)
			{
				//Lese Bassnote
				slashRead = false;
				var bassNoteLength = Note.TryRead(s[offset..], out var bassNoteRead);
				if (bassNoteLength != -1)
				{
					offset += bassNoteLength;
					bassNote = bassNoteRead;
					break;
				}

				//Keine Bassnote
				break;
			}
			else
			{
				//Weder Alteration noch Bassnote
				break;
			}
		}

		//Wurde am Ende ein Slash gelesen, der nicht mehr dazugehört?
		if (slashRead)
			offset--;

		//Erfolgreich gelesen
		chord = new Chord(note, quality, new string(s[0..offset]))
		{
			Bass = bassNote,
			Alterations = alterations
		};
		return offset;
	}
	public readonly record struct ChordFormat(Chord Chord,
		Note.NoteFormat Root, string Quality, ChordAlteration.AlterationFormat[] Alterations, Note.NoteFormat? Bass = null)
	{
		public override string ToString()
		{
			var sb = new StringBuilder();
			sb.Append(Root);
			sb.Append(Quality);

			sb.Append(string.Join('/', Alterations));

			if (Bass is not null)
				sb.Append('/').Append(Bass);

			return sb.ToString();
		}
	}
}
