using Godot;
using RpgGame.Core.Combat.Formation;

namespace RpgGame.Encounters;

/// <summary>Converts validated core formation coordinates into battle-screen geometry.</summary>
/// <remarks>
/// Core defines rows, side-relative depth, footprints, bounds, and overlap. This disposable
/// Control only mirrors those logical cells onto the screen and draws them. It does not parse
/// content slots or decide whether placements are legal.
/// </remarks>
public partial class BattleFormationView : Control
{
    private const float CellWidth = 104.0f;
    private const float CellHeight = 42.0f;
    private const float GridTop = 56.0f;
    // These offsets center both grids inside the battle scene's 1080-pixel content width.
    private const float EnemyLeft = 168.0f;
    private const float FormationGap = 112.0f;
    private const float PartyLeft =
        EnemyLeft
        + (BattleFormationRules.EnemyColumnCount * CellWidth)
        + FormationGap;

    private IReadOnlyList<FormationPlacement> _enemyPlacements = [];
    private IReadOnlyList<FormationPlacement> _partyPlacements = [];
    private IReadOnlyDictionary<string, string> _labelByInstanceId =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private bool _initialized;

    /// <summary>Receives placements already built and validated by plain .NET core rules.</summary>
    public void Initialize(
        IReadOnlyList<FormationPlacement> enemyPlacements,
        IReadOnlyList<FormationPlacement> partyPlacements)
    {
        ArgumentNullException.ThrowIfNull(enemyPlacements);
        ArgumentNullException.ThrowIfNull(partyPlacements);
        if (_initialized)
        {
            throw new InvalidOperationException("BattleFormationView is already initialized.");
        }

        if (enemyPlacements.Any(placement => placement.Anchor.Side != BattleSide.Enemy))
        {
            throw new ArgumentException(
                "The enemy presentation list contains a non-enemy placement.",
                nameof(enemyPlacements));
        }

        if (partyPlacements.Any(placement => placement.Anchor.Side != BattleSide.Party))
        {
            throw new ArgumentException(
                "The party presentation list contains a non-party placement.",
                nameof(partyPlacements));
        }

        _enemyPlacements = enemyPlacements.ToArray();
        _partyPlacements = partyPlacements.ToArray();
        _labelByInstanceId = BuildDisplayLabels(_enemyPlacements, _partyPlacements);
        _initialized = true;

        CreateGridLabels();
        foreach (FormationPlacement placement in _enemyPlacements.Concat(_partyPlacements))
        {
            CreatePlacementLabel(placement);
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawGrid(BattleSide.Enemy);
        DrawGrid(BattleSide.Party);

        if (!_initialized)
        {
            return;
        }

        foreach (FormationPlacement placement in _enemyPlacements)
        {
            DrawPlacement(placement, new Color(0.70f, 0.23f, 0.29f));
        }

        foreach (FormationPlacement placement in _partyPlacements)
        {
            DrawPlacement(placement, new Color(0.18f, 0.45f, 0.78f));
        }
    }

    /// <summary>
    /// Returns the presentation-only label assigned during initialization.
    /// </summary>
    /// <remarks>
    /// Battle rules continue using stable instance IDs. Sharing this view-owned label mapping
    /// lets HP rows, target buttons, and the event log say the same friendly name without making
    /// display text part of core identity or duplicating occurrence numbering in several controls.
    /// </remarks>
    public string GetDisplayLabel(string instanceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        if (!_initialized)
        {
            throw new InvalidOperationException("BattleFormationView is not initialized.");
        }

        return _labelByInstanceId.TryGetValue(instanceId, out string? label)
            ? label
            : throw new KeyNotFoundException(
                $"Formation view has no combatant instance '{instanceId}'.");
    }

    private static Rect2 GetCellRectangle(FormationCell cell)
    {
        int visualColumn = cell.Side == BattleSide.Enemy
            ? BattleFormationRules.EnemyColumnCount - 1 - cell.Column
            : cell.Column;
        float left = cell.Side == BattleSide.Enemy ? EnemyLeft : PartyLeft;
        return new Rect2(
            new Vector2(
                left + (visualColumn * CellWidth),
                GridTop + (cell.Row * CellHeight)),
            new Vector2(CellWidth, CellHeight));
    }

    private static Rect2 GetPlacementRectangle(FormationPlacement placement)
    {
        IReadOnlyList<FormationCell> cells =
            BattleFormationRules.GetOccupiedCells(placement);
        float left = float.MaxValue;
        float top = float.MaxValue;
        float right = float.MinValue;
        float bottom = float.MinValue;

        foreach (FormationCell cell in cells)
        {
            Rect2 rectangle = GetCellRectangle(cell);
            left = Mathf.Min(left, rectangle.Position.X);
            top = Mathf.Min(top, rectangle.Position.Y);
            right = Mathf.Max(right, rectangle.Position.X + rectangle.Size.X);
            bottom = Mathf.Max(bottom, rectangle.Position.Y + rectangle.Size.Y);
        }

        return new Rect2(
            new Vector2(left, top),
            new Vector2(right - left, bottom - top));
    }

    private void DrawGrid(BattleSide side)
    {
        int columnCount = BattleFormationRules.GetColumnCount(side);
        var emptyColor = new Color(0.12f, 0.15f, 0.21f);
        var alternateColor = new Color(0.14f, 0.17f, 0.23f);
        var borderColor = new Color(0.46f, 0.52f, 0.64f);

        for (int row = 0; row < BattleFormationRules.RowCount; row++)
        {
            for (int column = 0; column < columnCount; column++)
            {
                Rect2 rectangle = GetCellRectangle(new FormationCell(side, row, column));
                DrawRect(rectangle, (row + column) % 2 == 0 ? emptyColor : alternateColor);
                DrawRect(rectangle, borderColor, filled: false, width: 1.5f);
            }
        }
    }

    private void DrawPlacement(FormationPlacement placement, Color fillColor)
    {
        Rect2 occupied = GetPlacementRectangle(placement);
        // Keep the colored rectangle on the same exact outer edge as the logical grid cells.
        // The earlier proof view inset the fill by several pixels, which made one-cell
        // enemies look like they were floating inside the grid instead of occupying a tile.
        var inset = new Rect2(
            occupied.Position + new Vector2(1.5f, 1.5f),
            occupied.Size - new Vector2(3.0f, 3.0f));
        DrawRect(inset, fillColor);
        DrawRect(inset, Colors.White, filled: false, width: 2.0f);
    }

    private void CreateGridLabels()
    {
        float enemyWidth = BattleFormationRules.EnemyColumnCount * CellWidth;
        float partyWidth = BattleFormationRules.PartyColumnCount * CellWidth;
        AddLabel(
            "ENEMY FORMATION — 4 ROWS × 4 COLUMNS",
            new Rect2(new Vector2(EnemyLeft, 0.0f), new Vector2(enemyWidth, 24.0f)),
            17,
            new Color(1.0f, 0.72f, 0.72f));
        AddLabel(
            "rear c3   ← depth →   front c0  →",
            new Rect2(new Vector2(EnemyLeft, 24.0f), new Vector2(enemyWidth, 24.0f)),
            14,
            new Color(0.76f, 0.82f, 0.94f));
        AddLabel(
            "PARTY FORMATION — 4 ROWS × 2 COLUMNS",
            new Rect2(new Vector2(PartyLeft, 0.0f), new Vector2(partyWidth, 24.0f)),
            17,
            new Color(0.66f, 0.82f, 1.0f));
        AddLabel(
            "←  front c0   →   rear c1",
            new Rect2(new Vector2(PartyLeft, 24.0f), new Vector2(partyWidth, 24.0f)),
            14,
            new Color(0.76f, 0.82f, 0.94f));

        for (int row = 0; row < BattleFormationRules.RowCount; row++)
        {
            float top = GridTop + (row * CellHeight);
            AddLabel(
                $"r{row}",
                new Rect2(
                    new Vector2(EnemyLeft - 38.0f, top),
                    new Vector2(34.0f, CellHeight)),
                13,
                new Color(0.75f, 0.8f, 0.9f));
            AddLabel(
                $"r{row}",
                new Rect2(
                    new Vector2(PartyLeft + partyWidth + 4.0f, top),
                    new Vector2(34.0f, CellHeight)),
                13,
                new Color(0.75f, 0.8f, 0.9f));
        }
    }

    private void CreatePlacementLabel(FormationPlacement placement)
    {
        Rect2 occupied = GetPlacementRectangle(placement);
        var labelArea = new Rect2(
            occupied.Position + new Vector2(5.0f, 4.0f),
            occupied.Size - new Vector2(10.0f, 8.0f));
        AddLabel(
            GetPlacementLabel(placement),
            labelArea,
            13,
            Colors.White);
    }

    private string GetPlacementLabel(FormationPlacement placement)
    {
        if (_labelByInstanceId.TryGetValue(placement.InstanceId, out string? label))
        {
            return label;
        }

        return ShortDefinitionName(placement.DefinitionId);
    }

    private static IReadOnlyDictionary<string, string> BuildDisplayLabels(
        IReadOnlyList<FormationPlacement> enemyPlacements,
        IReadOnlyList<FormationPlacement> partyPlacements)
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal);
        AddSideLabels(enemyPlacements, labels);
        AddSideLabels(partyPlacements, labels);
        return labels;
    }

    private static void AddSideLabels(
        IReadOnlyList<FormationPlacement> placements,
        Dictionary<string, string> labels)
    {
        Dictionary<string, int> totalByDefinitionId = placements
            .GroupBy(placement => placement.DefinitionId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Count(),
                StringComparer.Ordinal);
        var seenByDefinitionId = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (FormationPlacement placement in placements)
        {
            string shortName = ShortDefinitionName(placement.DefinitionId);
            int seenCount = seenByDefinitionId.GetValueOrDefault(placement.DefinitionId) + 1;
            seenByDefinitionId[placement.DefinitionId] = seenCount;

            // If two slimes use the same enemy definition, show a friendly occurrence number.
            // The core still keeps enemy-0/enemy-1 internally because rules need battle-local
            // identities for overlap diagnostics, targeting later, and deterministic tests.
            labels.Add(
                placement.InstanceId,
                totalByDefinitionId[placement.DefinitionId] > 1
                    ? $"{shortName} #{seenCount}"
                    : shortName);
        }
    }

    private static string ShortDefinitionName(string definitionId)
    {
        int start = definitionId.LastIndexOf('.') + 1;
        string shortName = start <= 0 || start >= definitionId.Length
            ? definitionId
            : definitionId[start..];

        // Localization is not part of this gray-box milestone. Turn the stable final ID segment
        // into readable placeholder text without treating that text as identity. When a real
        // localization presenter arrives, it can replace this one display-only conversion.
        return string.Join(
            " ",
            shortName.Split('-', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }

    private void AddLabel(string text, Rect2 rectangle, int fontSize, Color color)
    {
        var label = new Label
        {
            Text = text,
            Position = rectangle.Position,
            Size = rectangle.Size,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            ClipText = true,
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        AddChild(label);
    }
}
