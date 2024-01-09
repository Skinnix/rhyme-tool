using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool.Data.Musical;

public sealed record Harmony : IReadOnlyCollection<Pitch>
{
	private readonly Pitch[] pitches;

	public int Count => pitches.Length;
	public Pitch this[int index] => pitches[index];

	public Harmony(params Pitch[] pitches) : this((IEnumerable<Pitch>)pitches) { }
	public Harmony(IEnumerable<Pitch> pitches)
	{
		this.pitches = pitches.ToArray();
	}

	System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => pitches.GetEnumerator();
	public IEnumerator<Pitch> GetEnumerator() => ((IEnumerable<Pitch>)pitches).GetEnumerator();
}
