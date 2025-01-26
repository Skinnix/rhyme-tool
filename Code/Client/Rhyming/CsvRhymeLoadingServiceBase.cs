using System.Diagnostics.CodeAnalysis;
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
		RhymeWordList rhymeWordList;
		using (var builder = new RhymeWordList.Builder())
		using (var reader = await GetRhymeReaderAsync())
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

					var entry = new RhymeWordList.Entry(word, useIpa, frequency);
					builder.TryAdd(entry);
				}
			}

			rhymeWordList = builder.Build();
		}

		/*SpellingList spellingList;
		{
			var spellStreams = await GetSpellingStreamsAsync();
			using (var dictionaryStream = spellStreams.dictionary)
			using (var affixStream = spellStreams.affix)
			{
				spellingList = await SpellingList.LoadAsync(dictionaryStream, affixStream);
			}
		}*/

		WordFormList wordFormList;
		using (var builder = new WordFormList.Builder())
		using (var reader = await GetWordFormReaderAsync())
		{
			string? line;
			while ((line = reader.ReadLine()) != null)
			{
				var split = line.Split('§', 2);
				if (split.Length < 2)
					continue;

				var stem = split[0];
				var forms = split[1];

				if (string.IsNullOrWhiteSpace(stem) || string.IsNullOrWhiteSpace(forms))
					continue;

				var formsSplit = forms.Split('$', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
				builder.TryAdd(stem, formsSplit);
			}

			wordFormList = builder.Build();
		}

		ComparisonWordList comparisonWordList;
		using (var builder = new ComparisonWordList.Builder())
		using (var reader = await GetComparisonReaderAsync())
		{
			string? line;
			while ((line = reader.ReadLine()) != null)
			{
				var split = line.Split('\t', 2);
				if (split.Length < 2)
					continue;

				var word = split[0];
				var ipas = split[1];

				if (string.IsNullOrWhiteSpace(word) || string.IsNullOrWhiteSpace(ipas))
					continue;

				var ipaSplit = ipas.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
				foreach (var ipa in ipaSplit)
				{
					var useIpa = ipa.Trim('/');
					var entry = new ComparisonWordList.Entry(word, useIpa);
					builder.TryAdd(entry);
				}
			}

			comparisonWordList = builder.Build();
		}

		using (var file = File.OpenWrite(@"D:\Users\Hendrik\Desktop\Visual Studio Projects\rhyme-tool\Code\ClientWeb\wwwroot\Data\Dictionaries\DAWB_words4.bin"))
		using (var writer = new BinaryWriter(file))
		{
			rhymeWordList.Write(writer);
			writer.Flush();
			writer.Close();
			file.Close();
		}

		using (var file = File.OpenWrite(@"D:\Users\Hendrik\Desktop\Visual Studio Projects\rhyme-tool\Code\ClientWeb\wwwroot\Data\Dictionaries\wordforms.bin"))
		using (var writer = new BinaryWriter(file))
		{
			wordFormList.Write(writer);
			writer.Flush();
			writer.Close();
			file.Close();
		}

		using (var file = File.OpenWrite(@"D:\Users\Hendrik\Desktop\Visual Studio Projects\rhyme-tool\Code\ClientWeb\wwwroot\Data\Dictionaries\de-ipa.bin"))
		using (var writer = new BinaryWriter(file))
		{
			comparisonWordList.Write(writer);
			writer.Flush();
			writer.Close();
			file.Close();
		}

		return LoadedRhymeHelper = await CreateRhymeHelper(rhymeWordList, wordFormList, comparisonWordList);
	}

	protected abstract Task<StreamReader> GetRhymeReaderAsync();
	protected abstract Task<StreamReader> GetWordFormReaderAsync();
	protected abstract Task<StreamReader> GetComparisonReaderAsync();

	//protected abstract Task<(Stream dictionary, Stream affix)> GetSpellingStreamsAsync();

	protected virtual Task<RhymeHelper> CreateRhymeHelper(RhymeWordList rhymeWordList, WordFormList wordFormList, ComparisonWordList comparisonWordList)
		=> Task.FromResult(new RhymeHelper(rhymeWordList, wordFormList, comparisonWordList));

	protected virtual bool TryRecombineIpa(string lastFullIpa, string ipa, [NotNullWhen(true)] out string? result)
	{
		result = IpaReplacer.ReplaceEllipses(lastFullIpa, ipa);
		return result is not null && result != lastFullIpa;
	}
}
