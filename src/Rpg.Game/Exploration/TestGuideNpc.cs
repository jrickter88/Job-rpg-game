using Godot;
using RpgGame.Core.State;

namespace RpgGame.Exploration;

/// <summary>The one game-specific NPC required by the Milestone 2 interaction slice.</summary>
public partial class TestGuideNpc : Node2D, IExplorationInteractable
{
	public const string SpokenFlagId = "flag.test-room.npc-spoken-to";
	public const string DialogueId = "dialogue.prologue.test-room-guide";

	/// <inheritdoc />
	public Vector2I TilePosition { get; } = new(7, 4);

	private bool _wasPreviouslySpokenTo;

	/// <inheritdoc />
	public ExplorationInteractionResult Interact(IGameSession session)
	{
		ArgumentNullException.ThrowIfNull(session);

		bool wasFirstInteraction = !session.GetEventFlag(SpokenFlagId);
		if (wasFirstInteraction)
		{
			session.SetEventFlag(SpokenFlagId);
		}

		return new ExplorationInteractionResult(DialogueId);
	}

	/// <inheritdoc />
	public void RefreshFromState(IGameSession session)
	{
		ArgumentNullException.ThrowIfNull(session);
		_wasPreviouslySpokenTo = session.GetEventFlag(SpokenFlagId);
		QueueRedraw();
	}

	public override void _Draw()
	{
		var body = new Rect2(new Vector2(-8, -8), new Vector2(16, 16));
		Color bodyColor = _wasPreviouslySpokenTo
			? new Color(0.25f, 0.78f, 0.47f)
			: new Color(0.93f, 0.55f, 0.20f);

		DrawRect(body, bodyColor);
		DrawRect(body, Colors.White, false, 2.0f);

		if (_wasPreviouslySpokenTo)
		{
			DrawCircle(new Vector2(0, -1), 3.0f, Colors.White);
		}
	}
}
