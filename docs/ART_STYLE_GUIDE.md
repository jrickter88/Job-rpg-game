# ART-001: Project Visual Style Guide

This document is the visual source of truth for original artwork in the JRPG. It applies to
human-made assets, commissioned work, and AI-assisted drafts.

The project takes inspiration from the design principles of early SNES JRPGs: readable shapes,
compact pixel clusters, expressive silhouettes, reusable environments, and clear tactical
communication. It does not copy Final Fantasy or any other game. Commercial sprites, tiles,
portraits, palettes, screenshots, or distinctive compositions must never be traced or reproduced.
The goal is an original world with the same kind of modularity and visual discipline, not a clone.

## 1. Overall Visual Philosophy

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

## 2. Overworld and Dungeon Map Standards

### Coordinate and tile standards

The current exploration placeholder uses a 48 x 48 logical tile at a 1280 x 720 logical viewport.
That is the project integration target for map assets until a later rendering milestone explicitly
changes it. Keep source tiles on a clean pixel grid and make each tile's canvas exactly 48 x 48
pixels unless a documented tileset exception is approved.

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

## 3. Exploration Character Standards

Use a 24 x 32 pixel native canvas for an ordinary exploration character, with a transparent canvas
and a consistent ground anchor at the bottom-center. The character's feet should land on the tile's
logical center; do not center by the sprite's visual bounding box.

- Four directions are required: north, south, east, and west.
- Begin with one idle frame per direction and three walk frames per direction.
- Keep the body readable at native resolution; do not rely on runtime enlargement to reveal detail.
- Use a consistent upper-left light source and keep feet/contact shadows subtle.
- The head, torso, and carried item should remain distinct at a normal gameplay zoom.
- Render characters above floor decorations and below foreground overlays or roofs.
- A character may extend above its tile, but the ground anchor and collision tile never move.

Exploration characters should feel like they belong to the map kit: their outline, shadow weight,
pixel density, and lighting must agree with nearby terrain.

## 4. Combat Character Standards

Use a 64 x 64 pixel native canvas for a standard party battle sprite. Preserve transparent padding
so the silhouette does not touch the canvas edge. A normal character should occupy roughly 44 to
56 pixels vertically, leaving room for weapons, casting poses, and animation motion.

Combat sprites are placed by the battle presentation using formation data. They must not be
arbitrarily scaled per action or per frame. Keep the feet or base aligned to a stable anchor, keep
weapon hands consistent between idle and attack, and preserve the same apparent pixel density as
exploration sprites.

Animation should make the character's action clear without becoming fluid HD animation. Attacks may
use a stronger silhouette break, but the idle pose should remain recognizable before and after it.

## 5. Enemy Standards

Enemy art and logical occupancy are related but not identical. The formation system currently uses
a 4 x 4 enemy grid and rectangular logical footprints. The footprint controls targeting, overlap,
and formation placement; the sprite controls visual presence.

| Logical footprint | Typical visual use | Art guidance |
| --- | --- | --- |
| 1 x 1 | slime, bat, small soldier | Keep the silhouette inside its cell or with a modest overhang. |
| 1 x 2 | tall knight, serpent, large flying enemy | Use the long axis to communicate height or reach. |
| 2 x 2 | ogre, golem, large beast | Establish one clear center of mass and readable attack side. |
| 3 x 2 | giant creature, machine, elite monster | Allow visual overlap while keeping the authored rectangle obvious. |
| 4 x 4 | boss or battlefield-scale entity | Fill the formation space impressively without changing logical occupancy. |

An enemy may visually overlap nearby cells, especially for wings, tails, weapons, smoke, or a boss
silhouette. It must never imply a different tactical footprint than the content definition. Do not
use art size as a substitute for formation data.

## 6. Portrait Standards

Use a 96 x 96 pixel source canvas for a standard dialogue portrait, with a transparent or controlled
background treatment chosen by the owning dialogue UI. Keep eyes and the primary expression inside
the central 72 x 72 safe area so portrait framing can crop consistently.

- Maintain the same head proportions, eye placement, and lighting direction across expressions.
- Use a simple flat, transparent, or softly framed background; never a noisy painted scene.
- Crop at a deliberate shoulder or chest line and keep the face large enough to read at 1280 x 720.
- Expression changes should be economical: eyes, brows, mouth, and a few silhouette cues carry most
  of the emotion.
- Portraits should share palette ramps and pixel density with combat and exploration sprites.

## 7. Pixel Art Standards

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

## 8. Lighting Standards

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

## 9. Palette Standards

This guide does not prescribe a fixed palette. Artists should use shared ramps and coordinate new
colors with existing work before adding them. Palette consistency is what makes assets from
different artists read as one world.

Maintain reusable ramps for:

- Skin: light, base, shadow, and a selective deepest shadow.
- Metals: highlight, light, base, shadow, and reflected dark.
- Grass: sunlit, base, shadow, and deep separation green.
- Stone: light, base, cool shadow, and deep crevice.
- Wood: highlight, warm base, grain shadow, and deep edge.

Avoid near-identical colors with different names. Prefer reusing a ramp wherever the material and
lighting agree. New thematic regions may shift hue, but they should retain comparable value steps,
outline weight, and highlight discipline.

## 10. Animation Standards

These are starting targets, not a reason to add motion where a still pose communicates better.

| State | Starting frames | Notes |
| --- | ---: | --- |
| Idle | 1-2 | Optional one-pixel breathing or cloth shift. |
| Walk | 3 | Clear foot alternation; keep the cycle readable and subtle. |
| Attack | 4-6 | Anticipation, action, impact, recovery. |
| Cast | 4-6 | Pose, channel, release, settle; effects may animate separately. |
| Hurt | 2-3 | Brief recoil, then return to idle. |
| Death | 4-6 | A readable fall or defeat pose; avoid excessive fluidity. |
| Victory | 3-5 | Small celebratory gesture that preserves the silhouette. |

Keep frame canvases and anchors identical within an animation. Do not move a character's logical
position to create a visual bob. Animation remains SNES-inspired: a few strong poses beat many
subtle in-between frames.

## 11. UI Standards

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

## 12. Asset Naming Standards

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

## 13. Export Standards

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

## 14. Future Expansion

The standards should scale to hundreds or thousands of assets. New tilesets should reuse existing
edge logic and material ramps. New character classes should share exploration and battle canvas
standards. Bosses and enemy families should declare logical formation footprints independently from
art dimensions. Portrait sets should reuse framing and expression conventions. Environmental themes
can add hue families while preserving value structure, pixel density, lighting, and modular joins.

Spell effects should remain readable against both dungeon and battle backgrounds. New assets for
mods should use namespaced content IDs, stable filenames, and the same review checklist. A mod may
add a visual theme, but it must not require gameplay code or a scene-specific asset contract merely
to load an ordinary map or enemy.

## 15. AI-Assisted Art Standards

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

## 16. Asset Review Checklist

Review every new visual asset against this list:

- [ ] Correct category, filename, dimensions, frame size, and transparent padding.
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
