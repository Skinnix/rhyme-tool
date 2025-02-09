using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Skinnix.RhymeTool.Rhyming;

namespace Skinnix.RhymeTool.Client.Rhyming;

public abstract class BinaryRhymeLoadingServiceBase : RhymeLoadingServiceBase
{
	protected override async Task<RhymeHelper> LoadInner()
	{
		using (var reader = await GetRhymeReaderAsync())
		{
			return new(RhymeWordList.Read(reader), null, null);
		}
	}

	protected abstract Task<BinaryReader> GetRhymeReaderAsync();
}
