using System.Diagnostics.CodeAnalysis;
using Skinnix.Dictionaries.Rhyming;
using Skinnix.RhymeTool.Rhyming;

namespace Skinnix.RhymeTool.Client.Rhyming;

public abstract class CsvRhymeLoadingServiceBase : IRhymeLoadingService
{
	private readonly object syncRoot = new();

	private Task<RhymeHelper>? loadTask;

	public RhymeHelper? LoadedRhymeHelper { get; private set; }

	public Task<RhymeHelper> LoadRhymeHelperAsync()
	{
		lock (syncRoot)
		{
			if (LoadedRhymeHelper is not null)
			{
				loadTask = null;
				return Task.FromResult(LoadedRhymeHelper);
			}
			return loadTask ??= LoadInner();
		}
	}

	protected virtual async Task<RhymeHelper> LoadInner()
	{
		using (var wordsBuilder = new WordList.Builder())
		using (var reader = await GetReaderAsync())
		{
			string? line;
			while ((line = reader.ReadLine()) != null)
			{
				var split = line.Split(';', 3);
				if (split.Length < 3)
					continue;

				var word = split[0];
				var ipas = split[1].Trim('/');

				if (string.IsNullOrWhiteSpace(word) || string.IsNullOrWhiteSpace(ipas) || !sbyte.TryParse(split[2], out var frequency))
					continue;

				var ipaSplit = ipas.Split('§', StringSplitOptions.RemoveEmptyEntries);
				string? lastFullIpa = null;
				foreach (var ipa in ipaSplit)
				{
					var useIpa = ipa;
					if (ipa.Contains("..."))
					{
						if (lastFullIpa is null)
							continue;

						if (!TryRecombineIpa(lastFullIpa, useIpa, out useIpa))
							continue;
					}
					else
					{
						lastFullIpa = ipa;
					}

					wordsBuilder.TryAdd(word, useIpa, new(frequency));
				}
			}

			return LoadedRhymeHelper = await CreateRhymeHelper(wordsBuilder.Build());
		}
	}

	protected abstract Task<StreamReader> GetReaderAsync();

	protected virtual Task<RhymeHelper> CreateRhymeHelper(WordList wordList)
		=> Task.FromResult(new RhymeHelper(wordList));

	protected virtual bool TryRecombineIpa(string lastFullIpa, string ipa, [NotNullWhen(true)] out string? result)
	{
		result = IpaReplacer.ReplaceEllipses(lastFullIpa, ipa);
		return result is not null && result != lastFullIpa;
	}
}
