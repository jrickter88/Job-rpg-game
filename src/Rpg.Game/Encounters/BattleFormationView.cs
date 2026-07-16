using Godot;
using RpgGame.Core.Combat.Formation;
using RpgGame.Core.Content;
using RpgGame.Core.Content.Definitions;

namespace RpgGame.Encounters;

/// <summary>Converts validated core formation coordinates into battle-screen geometry.</summary>
/// <remarks>
/// Core defines rows, side-relative depth, footprints, bounds, and overlap. This disposable
/// Control only mirrors those logical cells onto the screen and draws them. It does not parse
/// content slots or decide whether placements are legal.
/// </remarks>
public partial class BattleFormationView : Control
{
    private IReadOnlyList<FormationPlacement> _enemyPlacements = [];
    private IReadOnlyList<FormationPlacement> _partyPlacements = [];
    private IReadOnlyDictionary<string, string> _labelByInstanceId =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private IReadOnlyDictionary<string, Texture2D> _textureByDefinitionId =
        new Dictionary<string, Texture2D>(StringComparer.Ordinal);
    private readonly List<Label> _layoutLabels = [];
    private bool _initialized;

    public override void _Ready() => Resized += OnResized;

    /// <summary>Receives placements already built and validated by plain .NET core rules.</summary>
    public void Initialize(
        IReadOnlyList<FormationPlacement> enemyPlacements,
        IReadOnlyList<FormationPlacement> partyPlacements,
        IContentCatalog content)
    {
        ArgumentNullException.ThrowIfNull(enemyPlacements);
        ArgumentNullException.ThrowIfNull(partyPlacements);
        ArgumentNullException.ThrowIfNull(content);
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
        _textureByDefinitionId = LoadEnemyTextures(_enemyPlacements, content);
        _labelByInstanceId = BuildDisplayLabels(_enemyPlacements, _partyPlacements);
        _initialized = true;

        RefreshLayoutLabels();
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
            if (!_textureByDefinitionId.TryGetValue(placement.DefinitionId, out Texture2D? texture))
            {
                DrawPlacement(placement, new Color(0.70f, 0.23f, 0.29f));
                continue;
            }

            DrawEnemyTexture(placement, texture);
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

    private Rect2 GetCellRectangle(FormationCell cell)
    {
        FormationLayout layout = GetLayout();
        int visualColumn = cell.Side == BattleSide.Enemy
            ? BattleFormationRules.EnemyColumnCount - 1 - cell.Column
            : cell.Column;
        float left = cell.Side == BattleSide.Enemy ? layout.EnemyLeft : layout.PartyLeft;
        return new Rect2(
            new Vector2(
                left + (visualColumn * layout.CellWidth),
                layout.GridTop + (cell.Row * layout.CellHeight)),
            new Vector2(layout.CellWidth, layout.CellHeight));
    }

    private Rect2 GetPlacementRectangle(FormationPlacement placement)
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

    private void DrawEnemyTexture(FormationPlacement placement, Texture2D texture)
    {
        Rect2 occupied = GetPlacementRectangle(placement);
        float scale = Mathf.Min(occupied.Size.X / texture.GetWidth(), occupied.Size.Y / texture.GetHeight());
        Vector2 size = new(texture.GetWidth() * scale, texture.GetHeight() * scale);
        Rect2 destination = new(
            occupied.Position + ((occupied.Size - size) / 2.0f),
            size);
        DrawTextureRect(texture, destination, false);
    }

    private static IReadOnlyDictionary<string, Texture2D> LoadEnemyTextures(
        IReadOnlyList<FormationPlacement> placements,
        IContentCatalog content)
    {
        var textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        foreach (string definitionId in placements.Select(placement => placement.DefinitionId).Distinct(StringComparer.Ordinal))
        {
            EnemyDefinition enemy = content.GetRequired<EnemyDefinition>(definitionId);
            if (string.IsNullOrWhiteSpace(enemy.PresentationId))
            {
                continue;
            }

            string assetName = enemy.PresentationId[(enemy.PresentationId.LastIndexOf('.') + 1)..];
            string path = $"res://game/assets/enemies/{assetName}/battle.png";
            if (ResourceLoader.Load<Texture2D>(path) is Texture2D texture)
            {
                textures[definitionId] = texture;
            }
            else
            {
                GD.PushWarning($"Enemy presentation '{enemy.PresentationId}' could not load '{path}'.");
            }
        }

        return textures;
    }

    private void RefreshLayoutLabels()
    {
        foreach (Label label in _layoutLabels)
        {
            RemoveChild(label);
            label.QueueFree();
        }

        _layoutLabels.Clear();
        CreateGridLabels();
        foreach (FormationPlacement placement in _enemyPlacements.Concat(_partyPlacements))
        {
            CreatePlacementLabel(placement);
        }
    }

    private void CreateGridLabels()
    {
        FormationLayout layout = GetLayout();
        float enemyWidth = BattleFormationRules.EnemyColumnCount * layout.CellWidth;
        float partyWidth = BattleFormationRules.PartyColumnCount * layout.CellWidth;
        AddLabel(
            "ENEMIES",
            new Rect2(new Vector2(layout.EnemyLeft, 0.0f), new Vector2(enemyWidth, 16.0f)),
            12,
            new Color(1.0f, 0.72f, 0.72f));
        AddLabel(
            "PARTY",
            new Rect2(new Vector2(layout.PartyLeft, 0.0f), new Vector2(partyWidth, 16.0f)),
            12,
            new Color(0.66f, 0.82f, 1.0f));

        for (int row = 0; row < BattleFormationRules.RowCount; row++)
        {
            float top = layout.GridTop + (row * layout.CellHeight);
            AddLabel(
                $"r{row}",
                new Rect2(
                    new Vector2(0.0f, top),
                    new Vector2(layout.EnemyLeft - 2.0f, layout.CellHeight)),
                10,
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
            11,
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
        _layoutLabels.Add(label);
    }

    private FormationLayout GetLayout()
    {
        const float outerMargin = 18.0f;
        const float gridGap = 24.0f;
        float availableWidth = Mathf.Max(1.0f, Size.X - (outerMargin * 2.0f) - gridGap);
        float cellWidth = availableWidth / (
            BattleFormationRules.EnemyColumnCount + BattleFormationRules.PartyColumnCount);
        float gridTop = 20.0f;
        float cellHeight = Mathf.Max(16.0f, (Mathf.Max(gridTop + 4.0f, Size.Y) - gridTop - 4.0f)
            / BattleFormationRules.RowCount);
        float partyLeft = outerMargin + (BattleFormationRules.EnemyColumnCount * cellWidth) + gridGap;
        return new FormationLayout(cellWidth, cellHeight, gridTop, outerMargin, partyLeft);
    }

    private void OnResized()
    {
        if (_initialized)
        {
            RefreshLayoutLabels();
            QueueRedraw();
        }
    }

    private sealed record FormationLayout(
        float CellWidth,
        float CellHeight,
        float GridTop,
        float EnemyLeft,
        float PartyLeft);
}
