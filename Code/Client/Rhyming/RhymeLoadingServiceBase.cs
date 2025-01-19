namespace Skinnix.RhymeTool.Client.Rhyming;

public abstract class RhymeLoadingServiceBase : IRhymeLoadingService
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

	protected abstract Task<RhymeHelper> LoadInner();
}
