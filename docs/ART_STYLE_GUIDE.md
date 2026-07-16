# ART-001: Project Visual Style Guide

This document is the visual source of truth for original artwork in the JRPG. It applies to
human-made assets, commissioned work, and AI-assisted drafts.

The project takes inspiration from the design principles of early SNES JRPGs: readable shapes,
compact pixel clusters, expressive silhouettes, reusable environments, and clear tactical
communication. It does not copy Final Fantasy or any other game. Commercial sprites, tiles,
portraits, palettes, screenshots, or distinctive compositions must never be traced or reproduced.
The goal is an original world with the same kind of modularity and visual discipline, not a clone.

## 1. Historical Reference and Project Decision

The original SNES Final Fantasy IV is used only as a technical reference for compact construction
and readable presentation. It is not an art source. The following confidence labels distinguish
historical evidence from project decisions:

- **Confirmed:** SNES graphics are built from fundamental 8 x 8 pixel tiles; composed 16 x 16 map
  tiles use four such source tiles. The SNES native rendering family is commonly documented around
  256 x 224 pixels, with hardware-specific display modes and borders varying by context.
- **Common observed pattern:** FFIV field characters use compact 16 x 16 frames assembled from four
  8 x 8 components, with multiple directions and movement poses.
- **Measured from extracted artwork:** observed enemy visible bounds include approximately 13 x 15,
  31 x 32, 32 x 32, 32 x 48, 48 x 31, 48 x 32, 48 x 45, 48 x 48, 48 x 64, 62 x 48, 64 x 48,
  64 x 61, 64 x 64, and 96 x 61 pixels. These are visible crops, not proof of allocated canvases.
- **Common observed pattern:** FFIV party battle sprites are approximately 16 x 24 pixels and
  are substantially smaller than many enemies.
- **Not reliably verified:** the original SNES FFIV dialogue portrait canvas. Do not substitute GBA,
  PSP, Pixel Remaster, fan-expanded, or tightly cropped portrait measurements.

Likely tile-aligned enemy canvas families can be inferred from those observations, but they must not
be presented as universal FFIV allocations:

```text
Visible bound up to 16x16 -> likely 16x16 family
Visible bound up to 32x32 -> likely 32x32 family
Visible bound up to 48x32 -> likely 48x32 family
Visible bound up to 48x48 -> likely 48x48 family
Visible bound up to 64x48 -> likely 64x48 family
Visible bound up to 48x64 -> likely 48x64 family
Visible bound up to 64x64 -> likely 64x64 family
Visible bound up to 96x64 -> likely 96x64 family
```

The project standards below are deliberate original-game decisions. They favor FFIV-informed
pixel density while preserving the current 1280 x 720 authored viewport, 48 x 48 displayed
exploration cells, logical map data, and tactical formation rules.

## 2. Consolidated Project Dimensions

Historical construction diagrams:

```text
FFIV-style field frame: 16 x 16
+--------+--------+
|  8x8   |  8x8   |
+--------+--------+
|  8x8   |  8x8   |
+--------+--------+

FFIV-style party battle frame: 16 x 24
+----------------+
|      8 px      |
+----------------+
|      8 px      |
+----------------+
|      8 px      |
+----------------+
```

| Category | Native standard | Displayed/logical relationship |
| --- | --- | --- |
| Hardware construction tile | 8 x 8 | Construction unit only |
| Base map tile | 16 x 16 | One logical map cell |
| Displayed exploration tile | 48 x 48 | Exact 3x nearest-neighbor presentation |
| Exploration character | 16 x 16 | One logical map cell |
| Optional tall exploration character | 16 x 24 | Feet remain in one 16 x 16 cell |
| Party battle character | 16 x 24 | Presented inside a larger 48 x 48 tactical cell |
| Optional extended battle pose | 24 x 32 | Same body scale, larger canvas only when needed |
| Dialogue portrait | 32 x 32 | Project decision; historical SNES value unverified |
| Item/ability/status icon | 16 x 16 | UI-native size |
| Small enemy | 32 x 32 | Usually 1 x 1 logical footprint |
| Medium enemy | 48 x 48 | Footprint remains authored separately |
| Large enemy | 64 x 64 | Often 2 x 2 |
| Wide large enemy | 96 x 64 | Often 3 x 2 |
| Standard boss | 96 x 96 | Usually 3 x 3 |
| Major boss | 128 x 128 | Usually 4 x 4 |
| Exceptional boss maximum guideline | 192 x 192 | Explicit design review required |
| Standard spell impact | 48 x 48 | Effect anchor is explicit |
| Large spell impact | 64 x 64 | May cover multiple logical cells |

Native artwork dimensions, displayed dimensions, logical occupancy, and tactical footprint are
separate concepts. Artwork never creates collision or targeting cells.

## 3. Fundamental Pixel Grid and Map Construction

The fundamental construction unit is 8 x 8 pixels. The base map tile is 16 x 16 pixels, composed
from four 8 x 8 quadrants when useful:

```text
One 16 x 16 map tile
+--------+--------+
|  8x8   |  8x8   |
+--------+--------+
|  8x8   |  8x8   |
+--------+--------+
```

All ordinary sprite canvases should align to multiples of 8. Map artwork should primarily align to
multiples of 16. Nonstandard dimensions require a documented reason. Maps use reusable terrain,
cliffs, roads, buildings, trees, water, and decorations; no hand-painted full-map backgrounds.
Collision remains independent from graphics, and decorative objects may extend beyond their tile
footprint.

The current placeholder renderer displays a native 16 x 16 tile at exactly 3x:

```text
Native tile       Integer scale       Displayed tile
+--------+             x3             +------------------------+
| 16x16  |  ----------------------->  |         48x48          |
+--------+                             +------------------------+
                         Occupancy remains one logical cell.
```

The following map categories use the same 16 x 16 base system:

- Overworld: modular grass, coasts, mountains, forests, roads, rivers, settlements, and landmarks.
- Town: modular roads, walls, roofs, doors, windows, and buildings assembled in 16-pixel increments.
- Dungeon: modular floors, walls, corners, elevation transitions, doors, hazards, and mechanisms.
- Interior: modular floors, furniture, shelves, beds, counters, rugs, stairs, and props.

Recommended native object sizes include 16 x 16 flowers, windows, signs, chests, and hazards;
16 x 32 doors, torches, and cabinets; 32 x 32 beds and tables; 32 x 48 trees or statues; and
48 x 48 fountains or large mechanisms. These are art standards, not collision rules.

## 4. Overall Visual Philosophy

The visual identity is original, early-SNES-inspired pixel art with charm and restraint. Prefer a
strong silhouette and a small number of intentional shapes over realism or dense rendering.

- Readability comes before texture, realism, and spectacle.
- Enemies should be large enough to understand at a glance, especially in tactical combat.
- Environments are built from reusable pieces so new maps remain affordable and coherent.
- Pixel-perfect rendering is required: crisp edges, deliberate clusters, and stable integer scale.
- Every visual choice must support exploration, targeting, formation occupancy, or story emotion.
- Consistency between assets matters more than any single asset looking impressive in isolation.

Current presentation is a gray-box placeholder layer. Future art should preserve the same logical
separation: map passability, encounter markers, transitions, and combat footprints remain data and
core rules; art is a presentation layer that can be replaced without changing those facts.

## 5. Overworld and Dungeon Map Standards

### Coordinate and tile standards

The art tile system uses a 16 x 16 pixel native tile. Keep source tiles on a clean pixel grid and
make each base terrain tile exactly 16 x 16 pixels unless a documented asset exception is approved.
The current exploration debug renderer may display logical cells at a larger presentation scale;
that does not change the native art standard.

Maps are authored from ASCII passability rows and map-owned markers. Artwork must fit that logical
grid but must not redefine collision. A decorative tree may overhang neighboring tiles; its walkable
footprint must still agree with the map's data.

### Modular construction

Build map art from reusable tiles, edges, corners, transitions, and decoration pieces. Do not paint
one-off full-map backgrounds. A new town or dungeon should be assemblable from a kit with enough
variation to avoid obvious repetition.

Recommended modular pieces include:

- Overworld: grass, dirt, roads, bridges, water edges, trees, cliffs, signs, flowers, and rocks.
- Towns: floor, wall, roof, door, window, awning, street, fence, planter, and prop tiles.
- Dungeons: stone, wood, cave, stairs, ledges, water, lava, pillars, doors, and debris.
- Interiors: wall/floor joins, corners, furniture footprints, shelves, lamps, rugs, and exits.

Use edge and corner variants to make roads, buildings, trees, cliffs, and water connect cleanly.
Keep decoration optional: the same terrain kit should support an empty test room and a finished
scene without changing collision data.

## 6. Exploration Character Standards

Use a 16 x 16 pixel native canvas for an ordinary exploration character, with a transparent canvas
and a consistent ground anchor at the bottom-center. The character's feet should land on the tile's
logical center; do not center by the sprite's visual bounding box.

- Four directions are required: north, south, east, and west.
- Use two idle frames and three walk frames per direction. A preferred walk sequence is 1 -> 2 -> 3 -> 2.
- Keep the body readable at native resolution; do not rely on runtime enlargement to reveal detail.
- Use a consistent upper-left light source and keep feet/contact shadows subtle.
- The head, torso, and carried item should remain distinct at a normal gameplay zoom.
- Render characters above floor decorations and below foreground overlays or roofs.
- A character may extend above its tile, but the ground anchor and collision tile never move.

Exploration characters should feel like they belong to the map kit: their outline, shadow weight,
pixel density, and lighting must agree with nearby terrain.

## 7. Combat Character Standards

Use a 16 x 24 pixel native canvas for a standard party battle sprite, matching the observed compact
FFIV-style reference. Preserve transparent padding and use an 8 x 8 construction grid. Present it
inside the larger 48 x 48 tactical cell without scaling the artwork. A 24 x 32 canvas is an approved
extended pose exception for weapons, capes, bows, staffs, or class silhouettes that genuinely need it.

Combat sprites are placed by the battle presentation using formation data. They must not be
arbitrarily scaled per action or per frame. Keep the feet or base aligned to a stable anchor, keep
weapon hands consistent between idle and attack, and preserve the same apparent pixel density as
exploration sprites.

Animation should make the character's action clear without becoming fluid HD animation. Attacks may
use a stronger silhouette break, but the idle pose should remain recognizable before and after it.

## 8. Enemy Standards

Enemy art and logical occupancy are related but not identical. The formation system currently uses
a 4 x 4 enemy grid and rectangular logical footprints. The footprint controls targeting, overlap,
and formation placement; the sprite controls visual presence.

| Example | Logical footprint | Native sprite canvas |
| --- | --- | --- |
| Slime | 1 x 1 | 32 x 32 |
| Wolf | 1 x 2 | 48 x 48 |
| Ogre | 2 x 2 | 64 x 64 |
| Dragon | 3 x 2 | 96 x 64 |
| Final Boss | 4 x 4 | 128 x 128 |

An enemy may visually overlap nearby cells, especially for wings, tails, weapons, smoke, or a boss
silhouette. Targeting is based on occupied logical grid cells, not transparent pixels. Artwork is
never stretched to fit a footprint, and sprite size never replaces formation data.

## 9. Portrait Standards

Use a 32 x 32 pixel source canvas for a standard dialogue portrait, with a transparent or controlled
background treatment chosen by the owning dialogue UI. Keep eyes and the primary expression inside
the central 72 x 72 safe area so portrait framing can crop consistently.

- Maintain the same head proportions, eye placement, and lighting direction across expressions.
- Use a simple flat, transparent, or softly framed background; never a noisy painted scene.
- Crop at a deliberate shoulder or chest line and keep the face large enough to read at 1280 x 720.
- Expression changes should be economical: eyes, brows, mouth, and a few silhouette cues carry most
  of the emotion.
- Portraits should share palette ramps and pixel density with combat and exploration sprites.

```text
32 x 32 portrait crop
+--------------------------------+
|                                |
|       fixed eye line           |
|         head and face          |
|                                |
|       fixed shoulder line      |
+--------------------------------+
```

## 10. Pixel Art Standards

Pixel art is authored at native resolution and displayed with nearest-neighbor filtering. Use large
intentional clusters, clear silhouette steps, and limited detail. A pixel should belong to a shape,
shadow, highlight, or deliberate texture pattern.

Required practices:

- Use hard pixel edges and opaque color clusters.
- Keep color counts economical; use ramps instead of many near-duplicate colors.
- Use selective outlining. A full black outline is not mandatory when a dark local color reads better.
- Use dithering rarely and only as a purposeful texture or transition.
- Keep diagonal steps deliberate and consistent within an asset family.

Prohibited or discouraged styles:

- Anti-aliased edges, blur, soft brushes, painterly noise, or photorealistic rendering.
- Gradients used as a substitute for pixel ramps.
- HD-2D bloom, depth-of-field, lens effects, or arbitrary real-time smoothing.
- Uncontrolled dithering, tiny isolated pixels, and detail that disappears at native scale.
- Upscaling a small asset with a blurry filter or shrinking a large asset until its pixels break.

## 11. Lighting Standards

The global light comes from the upper-left. Every asset should make that direction legible through
its brightest planes and its cast/contact shadows.

```text
        light
          ↘
      [ upper-left planes ]
      [      object       ] ── darker right/lower planes
             ▾
        contact shadow
```

Apply the same rule to characters, enemies, buildings, trees, cliffs, water edges, portraits,
spell effects, and UI illustrations. A roof plane facing upper-left may be brighter; the underside
and lower-right edge should carry the darker ramp. Effects can be luminous, but their glow should
still have a readable source and should not erase silhouettes.

## 12. Palette Standards

The project uses one global palette. Artists should use its shared ramps and coordinate any proposed
addition before adding colors. Palette consistency is what makes assets from different artists read
as one world.

Maintain reusable ramps for:

- Skin: light, base, shadow, and a selective deepest shadow.
- Metals: highlight, light, base, shadow, and reflected dark.
- Grass: sunlit, base, shadow, and deep separation green.
- Stone: light, base, cool shadow, and deep crevice.
- Wood: highlight, warm base, grain shadow, and deep edge.

Avoid near-identical colors with different names. Prefer reusing a ramp wherever the material and
lighting agree. New thematic regions may shift hue, but they should retain comparable value steps,
outline weight, and highlight discipline.

## 13. Animation Standards

These are starting targets, not a reason to add motion where a still pose communicates better.

| State | Starting frames | Notes |
| --- | ---: | --- |
| Idle | 2 | A still readable pose with only a subtle shift. |
| Walk | 3 | Clear foot alternation; keep the cycle readable and subtle. |
| Attack | 4-6 | Anticipation, action, impact, recovery. |
| Cast | 4-6 | Pose, channel, release, settle; effects may animate separately. |
| Hurt | 2-3 | Brief recoil, then return to idle. |
| Death | 4-6 | A readable fall or defeat pose; avoid excessive fluidity. |
| Victory | 3-5 | Small celebratory gesture that preserves the silhouette. |

Keep frame canvases and anchors identical within an animation. Do not move a character's logical
position to create a visual bob. Animation remains SNES-inspired: a few strong poses beat many
subtle in-between frames.

## 14. UI Standards

The UI should feel like the same pixel world while remaining readable on the project's logical
1280 x 720 viewport and smaller CRT-safe presentations.

- Use opaque or controlled-color windows with crisp borders and a restrained number of tones.
- Keep border thickness consistent within a screen and use corners that tile cleanly.
- Use simple, high-contrast icons with a clear active/inactive state.
- Selection cursors should be unmistakable without obscuring item or ability text.
- Dialogue boxes need clear speaker, body, and confirm/cancel regions.
- Target markers must remain visible over the battle formation and should not imply a different
  logical target than the core command.
- Avoid decorative UI noise, gradients, and oversized effects that compete with combat information.

UI artwork should respect nearest-neighbor rendering, stable anchors, and the same upper-left light
direction when it depicts physical objects.

## 15. Asset Naming Standards

Use lowercase kebab-case filenames and stable, descriptive IDs. Keep category and role in the path
or name rather than relying on a display name.

```text
characters/james/walk-south-00.png
enemies/green-slime/idle-00.png
portraits/james/neutral.png
tilesets/dungeon-stone/floor-center.png
animations/fire-cast-00.png
spell-effects/fire-impact-00.png
icons/abilities/black-magic-fire.png
ui/cursor-target.png
```

Use `idle`, `walk`, `attack`, `cast`, `hurt`, `death`, and `victory` for animation state names.
Use direction names `north`, `south`, `east`, and `west`. Do not use spaces, uppercase letters,
temporary names, artist initials, or version suffixes in production asset filenames. Content IDs
remain stable string IDs and are not replaced by file paths.

## 16. Export Standards

- Export source art as PNG with transparency where needed.
- Preserve native dimensions and consistent frame sizes.
- Use nearest-neighbor filtering for import and runtime display.
- Prefer integer scaling factors; do not introduce fractional pixel scaling.
- Keep movement and sprite anchors on integer coordinates.
- Do not use subpixel movement for pixel-art characters or map decorations.
- Verify assets at native size, at the logical 1280 x 720 viewport, and at the project's CRT-safe
  4:3 presentations before approval.
- Keep source files layered and editable outside the repository when the production workflow needs
  them, but commit only approved runtime assets in the documented game asset locations.

Sprite sheets use fixed frame grids with no automatic trimming:

```text
Exploration walk: 3 frames x 16x16 = 48x16 per direction
+------+------ +------+
| 16x16| 16x16 | 16x16 |
+------+-------+------+

Party attack: 4 frames x 16x24 = 64x24
+--------+--------+--------+--------+
| 16x24  | 16x24  | 16x24  | 16x24  |
+--------+--------+--------+--------+
```

## 17. Placement, Footprints, and Asset Families

Exploration characters use a bottom-center anchor. Their feet sit on the center of the logical
16 x 16 cell while the body may occupy the upper portion:

```text
      16 x 16 logical cell
+----------------+
|     head       |
|     body       |
|      feet      |  <- bottom-center anchor
+--------+-------+
         ^ cell center
```

Enemy canvas families are 16 x 16, 32 x 32, 48 x 32, 48 x 48, 64 x 48, 48 x 64, 64 x 64,
96 x 64, 64 x 96, 96 x 96, 128 x 96, and 128 x 128. A reviewed exceptional boss may use up to
192 x 192. These canvas families do not automatically choose a tactical footprint.

The existing formation code describes footprint width and height in its own row/column terms.
Artists should write the asset specification as width-in-cells x height-in-cells without reversing
those fields. Common examples are 1x1, 1x2, 2x1, 2x2, 3x2, and 4x4.

```text
Logical 2x2 footprint       Visual overhang is allowed
+--------+--------+         /----------------------\
|   X    |   X    |         |    wings / weapon    |
+--------+--------+         +--------+--------+----+
|   X    |   X    |         |   X    |   X    |    |
+--------+--------+         +--------+--------+----+
                              |   X    |   X    |
                              +--------+--------+
```

Targeting uses only the X cells. Transparent pixels do not remove occupancy, and visible pixels do
not create occupancy. Overhang must not hide another combatant or target marker.

```text
1x1 footprint        3x2 footprint
+--------+           +--------+--------+--------+
|   X    |           |   X    |   X    |   X    |
+--------+           +--------+--------+--------+
                       |   X    |   X    |   X    |
                       +--------+--------+--------+
```

Icons use 16 x 16 canvases for items, abilities, statuses, equipment slots, and cursors; small UI
arrows use 8 x 8. Spell effects use 16 x 16 status sparkles, 24 x 24 or 32 x 32 small hits,
48 x 48 standard impacts, 64 x 64 large impacts, and 96 x 64 multi-cell effects. Effects use
stable anchors and fixed frame dimensions.

## 18. Asset Specification Template

```text
Asset ID:
Asset category:
Native canvas:
Visible bounds:
Frame count:
Sheet layout:
Anchor:
Logical tile occupancy:
Combat footprint:
Facing:
Lighting direction:
Palette family:
Visual overhang:
Animation timing:
Export path:
Notes:
```

Use `N/A` when a field does not apply. Every enemy and boss specification must state both native
canvas and logical footprint.

## 19. Future Expansion

The standards should scale to hundreds or thousands of assets. New tilesets should reuse existing
edge logic and material ramps. New character classes should share exploration and battle canvas
standards. Bosses and enemy families should declare logical formation footprints independently from
art dimensions. Portrait sets should reuse framing and expression conventions. Environmental themes
can add hue families while preserving value structure, pixel density, lighting, and modular joins.

Spell effects should remain readable against both dungeon and battle backgrounds. New assets for
mods should use namespaced content IDs, stable filenames, and the same review checklist. A mod may
add a visual theme, but it must not require gameplay code or a scene-specific asset contract merely
to load an ordinary map or enemy.

## 20. AI-Assisted Art Standards

AI-generated artwork is always a draft, never an automatic production asset. It must follow every
standard in this guide: original design, correct dimensions, palette discipline, upper-left lighting,
native pixel density, strong silhouette, and the correct animation or portrait framing.

AI output must never imitate, trace, reconstruct, or closely reproduce copyrighted sprites, tiles,
portraits, palettes, logos, or recognizable compositions. Prompts and references should describe
high-level original properties rather than naming a specific copyrighted asset as a target.

Before production use, an artist or maintainer must inspect the output at native size, remove
anti-aliasing and stray pixels, correct palette and lighting, normalize anchors and frame sizes,
confirm originality, and verify that the asset fits the relevant map or formation footprint. Treat
the result like an untrusted sketch that needs human art direction and cleanup.

## 21. Asset Review Checklist

Review every new visual asset against this list:

- [ ] Correct category, filename, dimensions, frame size, and transparent padding.
- [ ] Dimensions align to the 8 x 8 construction grid or 16 x 16 map grid.
- [ ] Strong silhouette and readable at native size.
- [ ] Pixel clusters are intentional; no anti-aliasing, blur, or painterly noise.
- [ ] Palette uses existing ramps or documents an approved new ramp.
- [ ] Upper-left lighting agrees with characters, enemies, terrain, portraits, and effects.
- [ ] Animation uses the expected frame count and stable anchors.
- [ ] Exploration character uses the correct direction, ground anchor, and layer order.
- [ ] Enemy artwork agrees with its logical formation footprint without redefining it.
- [ ] UI artwork remains legible at 1280 x 720 and CRT-safe output sizes.
- [ ] Asset is modular where appropriate and does not require a one-off painted background.
- [ ] Naming uses lowercase kebab-case and stable category terminology.
- [ ] Export uses PNG, nearest-neighbor filtering, integer scale, and no subpixel assumptions.
- [ ] Design is original and does not trace or reproduce copyrighted artwork.
- [ ] AI-assisted drafts received human cleanup and review before production use.
