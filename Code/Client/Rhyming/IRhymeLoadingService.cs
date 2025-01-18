using System.Runtime.Serialization;
using Skinnix.RhymeTool.Rhyming;

namespace Skinnix.RhymeTool.Client.Rhyming;

public interface IRhymeLoadingService
{
	Task<RhymeHelper> LoadRhymeHelperAsync();
}

public abstract class RhymeLoadingServiceBase : IRhymeLoadingService
{
	private readonly object syncRoot = new();

	private RhymeHelper? helper;
	private Task<RhymeHelper>? loadTask;

	public Task<RhymeHelper> LoadRhymeHelperAsync()
	{
		lock (syncRoot)
		{
			if (helper is not null)
			{
				loadTask = null;
				return Task.FromResult(helper);
			}
			return loadTask ??= LoadInner();
		}
	}

	protected virtual async Task<RhymeHelper> LoadInner()
	{
		using (var wordsBuilder = new WordList.Builder())
		using (var reader = await CreateWordDataReaderAsync())
		{
			string? line;
			while ((line = reader.ReadLine()) != null)
			{
				var split = line.Split(';', 3);
				if (split.Length < 3)
					continue;

				var word = split[0];
				var ipa = split[1].Trim('/');

				if (string.IsNullOrWhiteSpace(word) || string.IsNullOrWhiteSpace(ipa) || !byte.TryParse(split[2], out var frequency))
					continue;

				wordsBuilder.TryAdd(new IpaWord(word, ipa), new(frequency));
			}

			return helper = await CreateRhymeHelper(wordsBuilder.Build());
		}
	}

	protected abstract Task<StreamReader> CreateWordDataReaderAsync();

	protected virtual Task<RhymeHelper> CreateRhymeHelper(WordList wordList)
		=> Task.FromResult(new RhymeHelper(wordList));
}
