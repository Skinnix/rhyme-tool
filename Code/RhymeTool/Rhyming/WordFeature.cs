﻿namespace Skinnix.RhymeTool.Rhyming;

public abstract class WordFeature
{
	public abstract string? GetFeatureValue<TWord>(TWord word)
		where TWord : IRhymableWord;
}