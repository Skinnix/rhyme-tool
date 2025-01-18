using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool.Rhyming;

public class WordList : IpaRhymeList<RhymeListWordData>
{
	protected WordList(InstanceData data)
		: base(data)
	{ }

	public static WordList Read(BinaryReader reader)
		=> new(Read(reader, r => new RhymeListWordData(r.ReadByte())));

	public class Builder : Builder<IWordIpa>
	{
		public new WordList Build()
			=> (WordList)base.Build();

		private protected override WordList CreateInstance(InstanceData data)
			=> new WordList(data);
	}
}
