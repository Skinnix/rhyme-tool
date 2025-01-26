namespace Skinnix.Dictionaries.Rhyming;

[Obsolete]
public interface IRhymableWord
{
	string Word { get; }

	TDetail? TryGetDetail<TDetail>()
		where TDetail : IWordDetail
		=> this is TDetail detail ? detail : default;
}
