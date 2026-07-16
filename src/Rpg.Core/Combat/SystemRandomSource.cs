namespace RpgGame.Core.Combat;

/// <summary>Application-lifetime random adapter backed by the platform random generator.</summary>
public sealed class SystemRandomSource : IRandomSource
{
	private readonly Random _random = Random.Shared;

	/// <inheritdoc />
	public int Next(int minInclusive, int maxExclusive) =>
		_random.Next(minInclusive, maxExclusive);
}
