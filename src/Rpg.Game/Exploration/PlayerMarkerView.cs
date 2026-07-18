using Godot;

namespace RpgGame.Exploration;

/// <summary>Pixel-art James marker used while exploring dungeon and town maps.</summary>
public partial class PlayerMarkerView : Node2D
{
    private string _facing = "south";
    private readonly Dictionary<string, Texture2D[]> _frames = new(StringComparer.Ordinal);
    private Texture2D? _currentFrame;
    private int _nextStepFrame;

    public override void _Ready()
    {
        _frames["north"] = LoadFrames("up");
        _frames["east"] = LoadFrames("right");
        _frames["south"] = LoadFrames("down");
        _frames["west"] = LoadFrames("left");
        _currentFrame = _frames["south"][0];
        QueueRedraw();
    }

    /// <summary>Updates the logical direction rendered on the next frame.</summary>
    public void SetFacing(string facing)
    {
        bool changed = !string.Equals(_facing, facing, StringComparison.Ordinal);
        _facing = facing;
        if (changed)
        {
            // Frame 1 is the facing/idle pose. The first successful step after
            // turning must use the alternate pose so a tap is visibly animated.
            _nextStepFrame = 1;
        }
        if (changed || !_frames.TryGetValue(facing, out Texture2D[]? frames))
        {
            _currentFrame = _frames.TryGetValue(facing, out frames)
                ? frames[0]
                : _frames["south"][0];
        }
        QueueRedraw();
    }

    public void AdvanceStepAnimation()
    {
        if (!_frames.TryGetValue(_facing, out Texture2D[]? frames))
        {
            return;
        }

        _currentFrame = frames[_nextStepFrame];
        _nextStepFrame = (_nextStepFrame + 1) % frames.Length;
        QueueRedraw();
    }

    public void AnimateTo(Vector2 targetPosition, Action completed)
    {
        ArgumentNullException.ThrowIfNull(completed);

        Tween tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Linear);
        tween.SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(this, "position", targetPosition, 0.22);
        tween.TweenCallback(Callable.From(completed));
    }

    public override void _Draw()
    {
        if (_currentFrame is not null)
        {
            // Source frames are authored for the native 16x16 exploration cell. Keep each
            // frame at native size so its directional proportions remain intact.
            Vector2 size = _currentFrame.GetSize();
            DrawTextureRect(
                _currentFrame,
                new Rect2(-size.X * 0.5f, -size.Y * 0.5f, size.X, size.Y),
                tile: false,
                modulate: Colors.White,
                transpose: false);
        }
    }

    private static Texture2D[] LoadFrames(string direction)
    {
        const string root = "res://game/assets/party/james/overworld/";
        return
        [
            ResourceLoader.Load<Texture2D>($"{root}james-{direction}1.png")
                ?? throw new InvalidDataException($"Missing James overworld frame '{direction}1'."),
            ResourceLoader.Load<Texture2D>($"{root}james-{direction}2.png")
                ?? throw new InvalidDataException($"Missing James overworld frame '{direction}2'."),
        ];
    }
}
