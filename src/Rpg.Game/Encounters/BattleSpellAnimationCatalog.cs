using Godot;

namespace RpgGame.Encounters;

/// <summary>Stable IDs for battle spell animations selectable from ability JSON.</summary>
internal static class BattleSpellAnimationIds
{
	public const string Fire = "animation.spell.fire";
}

/// <summary>
/// Maps authored battle animation IDs to trusted presentation assets and playback settings.
/// </summary>
/// <remarks>
/// JSON chooses an ID; this Godot-only catalog owns resource paths, spritesheet regions,
/// frame counts, timing, and scale. Adding an animation is therefore one catalog entry plus
/// one battleAnimationId field on the ability record, without changing combat rules.
/// </remarks>
internal static class BattleSpellAnimationCatalog
{
	private static readonly IReadOnlyDictionary<string, BattleSpellAnimation> Animations =
		new Dictionary<string, BattleSpellAnimation>(StringComparer.Ordinal)
		{
			[BattleSpellAnimationIds.Fire] = new(
				BattleSpellAnimationIds.Fire,
				"res://game/assets/sprites/ff4spells.png",
				new Rect2(0, 1375, 704, 64),
				11,
				0.06f,
				1.0f,
				0.18f),
		};

	public static bool TryGet(
		string animationId,
		out BattleSpellAnimation animation) => Animations.TryGetValue(animationId, out animation!);
}

/// <summary>Trusted presentation settings for one battle spell animation.</summary>
internal sealed record BattleSpellAnimation(
	string Id,
	string AssetPath,
	Rect2 RegionRect,
	int FrameCount,
	float FrameDuration,
	float Scale,
	float FadeDuration);
