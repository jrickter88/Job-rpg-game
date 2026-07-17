using Godot;
using RpgGame.Core.Combat.Formation;
using RpgGame.Core.Content;
using RpgGame.Core.Content.Definitions;
using RpgGame.Core.Loot;

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
    private IReadOnlyDictionary<string, Texture2D> _partyTextureByDefinitionId =
        new Dictionary<string, Texture2D>(StringComparer.Ordinal);
    private readonly List<Label> _layoutLabels = [];
    private IReadOnlySet<string> _defeatedCombatantIds =
        new HashSet<string>(StringComparer.Ordinal);
    private IReadOnlyList<LootAward> _victoryRewardItems = [];
    private string? _targetedCombatantId;
    private string? _highlightedCombatantId;
    private double _targetPulseTime;
    private bool _gridVisible = true;
    private bool _initialized;

    public override void _Ready() => Resized += OnResized;

    public override void _Process(double delta)
    {
        if (string.IsNullOrWhiteSpace(_highlightedCombatantId))
        {
            return;
        }

        _targetPulseTime += delta;
        QueueRedraw();
    }

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
        _partyTextureByDefinitionId = LoadPartyTextures(_partyPlacements);
        _labelByInstanceId = BuildDisplayLabels(_enemyPlacements, _partyPlacements);
        _initialized = true;

        RefreshLayoutLabels();
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_gridVisible)
        {
            DrawGrid(BattleSide.Enemy);
            DrawGrid(BattleSide.Party);
        }

        if (!_initialized)
        {
            return;
        }

        foreach (FormationPlacement placement in _enemyPlacements)
        {
            if (_defeatedCombatantIds.Contains(placement.InstanceId))
            {
                continue;
            }

            if (!_textureByDefinitionId.TryGetValue(placement.DefinitionId, out Texture2D? texture))
            {
                DrawPlacement(placement, new Color(0.70f, 0.23f, 0.29f));
                continue;
            }

            DrawEnemyTexture(placement, texture);
        }

        foreach (FormationPlacement placement in _partyPlacements)
        {
            if (_defeatedCombatantIds.Contains(placement.InstanceId))
            {
                continue;
            }

            if (_partyTextureByDefinitionId.TryGetValue(
                    placement.DefinitionId,
                    out Texture2D? texture))
            {
                DrawPartyTexture(placement, texture);
            }
            else
            {
                DrawPlacement(placement, new Color(0.18f, 0.45f, 0.78f));
            }
        }

        DrawVictoryRewardItems();

        DrawTargetCursor();
    }

    /// <summary>Highlights the currently selected target without changing the logical grid.</summary>
    public void SetTargetedCombatant(string? instanceId)
    {
        _targetedCombatantId = instanceId;
        if (_initialized)
        {
            QueueRedraw();
        }
    }

    /// <summary>Applies the active-turn pulse without showing a target-selection cursor.</summary>
    public void SetHighlightedCombatant(string? instanceId)
    {
        _highlightedCombatantId = instanceId;
        if (_initialized)
        {
            QueueRedraw();
        }
    }

    /// <summary>Shows or hides the logical formation grid for presentation debugging.</summary>
    public void SetGridVisible(bool visible)
    {
        _gridVisible = visible;
        if (_initialized)
        {
            RefreshLayoutLabels();
        }

        QueueRedraw();
    }

    /// <summary>Updates the presentation-only list of combatants removed from the field.</summary>
    public void SetDefeatedCombatants(IEnumerable<string> instanceIds)
    {
        ArgumentNullException.ThrowIfNull(instanceIds);
        _defeatedCombatantIds = new HashSet<string>(instanceIds, StringComparer.Ordinal);
        if (!_initialized)
        {
            return;
        }

        RefreshLayoutLabels();
        QueueRedraw();
    }

    public void SetVictoryRewardItems(IReadOnlyList<LootAward> awards)
    {
        ArgumentNullException.ThrowIfNull(awards);
        _victoryRewardItems = awards.ToArray();
        QueueRedraw();
    }

    private void DrawVictoryRewardItems()
    {
        if (_victoryRewardItems.Count == 0)
        {
            return;
        }

        var placementUseCount = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (LootAward award in _victoryRewardItems)
        {
            FormationPlacement[] matches = _enemyPlacements
                .Where(placement => string.Equals(
                    placement.DefinitionId,
                    award.EnemyDefinitionId,
                    StringComparison.Ordinal))
                .ToArray();
            if (matches.Length == 0)
            {
                continue;
            }

            int used = placementUseCount.GetValueOrDefault(award.EnemyDefinitionId);
            FormationPlacement placement = matches[Math.Min(used, matches.Length - 1)];
            placementUseCount[award.EnemyDefinitionId] = used + 1;
            string assetName = award.ItemId[(award.ItemId.LastIndexOf('.') + 1)..];
            Texture2D? texture = ResourceLoader.Load<Texture2D>(
                $"res://game/assets/items/{assetName}.png");
            if (texture is null)
            {
                continue;
            }

            Rect2 cell = GetPlacementRectangle(placement);
            float scale = Mathf.Min(cell.Size.X / texture.GetWidth(), cell.Size.Y / texture.GetHeight());
            Vector2 size = texture.GetSize() * scale;
            DrawTextureRect(
                texture,
                new Rect2(cell.Position + ((cell.Size - size) * 0.5f), size),
                false);
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
        float cellWidth = cell.Side == BattleSide.Enemy
            ? layout.EnemyCellWidth
            : layout.PartyCellWidth;
        return new Rect2(
            new Vector2(
                left + (visualColumn * cellWidth),
                layout.GridTop + (cell.Row * layout.CellHeight)),
            new Vector2(cellWidth, layout.CellHeight));
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
        // Give enemy art strong visual presence beyond its logical cell while keeping the
        // authored footprint authoritative. This follows the classic JRPG composition where
        // enemies read as larger silhouettes than the party sprites on the opposite side.
        float scale = Mathf.Min(
            (occupied.Size.X * 1.50f) / texture.GetWidth(),
            (occupied.Size.Y * 1.50f) / texture.GetHeight());
        Vector2 size = new(texture.GetWidth() * scale, texture.GetHeight() * scale);
        Rect2 destination = new(
            occupied.Position + ((occupied.Size - size) / 2.0f),
            size);
        DrawTextureRect(texture, destination, false, new Color(1.0f, 1.0f, 1.0f, GetHighlightAlpha(placement)));
    }

    private void DrawTargetCursor()
    {
        if (string.IsNullOrWhiteSpace(_targetedCombatantId))
        {
            return;
        }

        FormationPlacement? placement = _enemyPlacements
            .Concat(_partyPlacements)
            .FirstOrDefault(candidate => string.Equals(
                candidate.InstanceId,
                _targetedCombatantId,
                StringComparison.Ordinal));
        if (placement is null)
        {
            return;
        }

        Rect2 target = GetPlacementRectangle(placement);
        bool pointsLeft = placement.Anchor.Side == BattleSide.Enemy;
        float x = pointsLeft
            ? target.End.X + 24.0f
            : target.Position.X - 24.0f;
        float y = target.Position.Y + (target.Size.Y / 2.0f);
        Vector2[] points =
        [
            new(x + (pointsLeft ? -12.0f : 12.0f), y),
            new(x, y - 8.0f),
            new(x, y + 8.0f),
        ];
        DrawColoredPolygon(points, new Color(1.0f, 0.84f, 0.25f));
        DrawPolyline([points[0], points[1], points[2], points[0]], new Color(1.0f, 0.96f, 0.65f), 2.0f);
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

    private static IReadOnlyDictionary<string, Texture2D> LoadPartyTextures(
        IReadOnlyList<FormationPlacement> placements)
    {
        var textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        foreach (string definitionId in placements
                     .Select(placement => placement.DefinitionId)
                     .Distinct(StringComparer.Ordinal))
        {
            string assetName = definitionId[(definitionId.LastIndexOf('.') + 1)..];
            string path = $"res://game/assets/party/{assetName}/battle.png";
            if (ResourceLoader.Load<Texture2D>(path) is Texture2D texture)
            {
                textures[definitionId] = texture;
            }
            else
            {
                GD.PushWarning($"Party presentation '{definitionId}' could not load '{path}'.");
            }
        }

        return textures;
    }

    private void DrawPartyTexture(FormationPlacement placement, Texture2D texture)
    {
        Rect2 occupied = GetPlacementRectangle(placement);
        float scale = Mathf.Min(
            (occupied.Size.X * 0.9f) / texture.GetWidth(),
            (occupied.Size.Y * 0.9f) / texture.GetHeight());
        Vector2 size = new(texture.GetWidth() * scale, texture.GetHeight() * scale);
        DrawTextureRect(
            texture,
            new Rect2(occupied.Position + ((occupied.Size - size) / 2.0f), size),
            false,
            new Color(1.0f, 1.0f, 1.0f, GetHighlightAlpha(placement)));
    }

    private float GetHighlightAlpha(FormationPlacement placement) =>
        string.Equals(placement.InstanceId, _highlightedCombatantId, StringComparison.Ordinal)
            ? 0.68f + (0.32f * ((Mathf.Sin((float)_targetPulseTime * 3.0f) + 1.0f) / 2.0f))
            : 1.0f;

    private void RefreshLayoutLabels()
    {
        foreach (Label label in _layoutLabels)
        {
            RemoveChild(label);
            label.QueueFree();
        }

        _layoutLabels.Clear();
        if (_gridVisible)
        {
            CreateGridLabels();
        }

        foreach (FormationPlacement placement in _enemyPlacements.Concat(_partyPlacements))
        {
            if (_defeatedCombatantIds.Contains(placement.InstanceId))
            {
                continue;
            }

            CreatePlacementLabel(placement);
        }
    }

    private void CreateGridLabels()
    {
        FormationLayout layout = GetLayout();
        float enemyWidth = BattleFormationRules.EnemyColumnCount * layout.EnemyCellWidth;
        float partyWidth = BattleFormationRules.PartyColumnCount * layout.PartyCellWidth;
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
        if (placement.Anchor.Side == BattleSide.Enemy
            && _textureByDefinitionId.ContainsKey(placement.DefinitionId))
        {
            return;
        }

        if (placement.Anchor.Side == BattleSide.Party
            && _partyTextureByDefinitionId.ContainsKey(placement.DefinitionId))
        {
            return;
        }

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
        foreach (FormationPlacement placement in placements)
        {
            string shortName = ShortDefinitionName(placement.DefinitionId);
            // Duplicate presentation names are intentional. Combat still uses the unique
            // battle-local instance ID for targeting, ordering, and event application.
            labels.Add(placement.InstanceId, shortName);
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
        const float enemyCellWidthRatio = 1.25f;
        float layoutUnits = (BattleFormationRules.EnemyColumnCount * enemyCellWidthRatio)
            + BattleFormationRules.PartyColumnCount;
        float partyCellWidth = availableWidth / layoutUnits;
        float enemyCellWidth = partyCellWidth * enemyCellWidthRatio;
        float gridHeight = Mathf.Min(220.0f, Mathf.Max(64.0f, Size.Y - 24.0f));
        float gridTop = Mathf.Max(20.0f, Size.Y - gridHeight - 4.0f);
        float cellHeight = gridHeight / BattleFormationRules.RowCount;
        float partyLeft = outerMargin + (BattleFormationRules.EnemyColumnCount * enemyCellWidth) + gridGap;
        return new FormationLayout(
            enemyCellWidth,
            partyCellWidth,
            cellHeight,
            gridTop,
            outerMargin,
            partyLeft);
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
        float EnemyCellWidth,
        float PartyCellWidth,
        float CellHeight,
        float GridTop,
        float EnemyLeft,
        float PartyLeft);
}
