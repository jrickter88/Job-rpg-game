using Godot;

namespace RpgGame.Exploration;

/// <summary>Pixel-art James marker used while exploring dungeon and town maps.</summary>
public partial class PlayerMarkerView : Node2D
{
    private string _facing = "south";
    private readonly Dictionary<string, Texture2D[]> _frames = new(StringComparer.Ordinal);
    private Texture2D? _currentFrame;
    private bool _walking;
    private double _animationTimer;
    private int _animationFrame;

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
        _facing = facing;
        _animationFrame = 0;
        _animationTimer = 0.0;
        _currentFrame = _frames.TryGetValue(facing, out Texture2D[]? frames)
            ? frames[0]
            : _frames["south"][0];
        QueueRedraw();
    }

    public void SetWalking(bool walking)
    {
        _walking = walking;
        if (!walking)
        {
            _animationFrame = 0;
            _animationTimer = 0.0;
            SetFacing(_facing);
        }
    }

    public override void _Process(double delta)
    {
        if (!_walking || !_frames.TryGetValue(_facing, out Texture2D[]? frames))
        {
            return;
        }

        _animationTimer += delta;
        if (_animationTimer < 0.16)
        {
            return;
        }

        _animationTimer = 0.0;
        _animationFrame = (_animationFrame + 1) % frames.Length;
        _currentFrame = frames[_animationFrame];
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_currentFrame is not null)
        {
            // Source frames are 16x24 pixel art; 2x nearest-neighbor scaling makes the
            // character readable against the 48x48 exploration tile without blur.
            DrawTextureRect(
                _currentFrame,
                new Rect2(-16.0f, -48.0f, 32.0f, 48.0f),
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
