using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Skinnix.Dictionaries.Rhyming;

namespace Skinnix.RhymeTool.Rhyming;

public class WordList : IpaRhymeList<RhymeListWordData>
{
	protected WordList(InstanceData data)
		: base(data)
	{ }

	public void Write(BinaryWriter writer)
		=> Write(writer, (w, d) => w.Write(d.Frequency));

	public static WordList Read(BinaryReader reader)
		=> new(Read(reader, r => new RhymeListWordData(r.ReadSByte())));

	public new class Builder : IpaRhymeList<RhymeListWordData>.Builder
	{
		public new WordList Build()
			=> (WordList)base.Build();

		protected override WordList CreateInstance(InstanceData data)
			=> new WordList(data);
	}
}
