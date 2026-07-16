# Milestone 4.9 - Basic equipment menu

## Ownership

`GameMenuPanel` and `EquipmentPanel` are disposable exploration UI beneath the existing
`CanvasLayer`. They receive injected `IContentCatalog` and `IGameSession`; neither panel locates
`GameRoot`, stores campaign state, or mutates Godot nodes as a replacement for an authoritative
state update.

`EquipmentMenuProjectionResolver` is a read-only core query. It lists the currently supported
slot (`slot.weapon.main-hand`), its equipped item, and owned compatible equipment in stable item
ID order. Non-equipment and wrong-slot inventory items do not appear. The panel calls
`EquipmentService.EquipItem` and `UnequipItem`; session events rebuild its display.

## Player flow

In exploration, press the existing Menu / Cancel action to open Menu, choose Equipment, select
Weapon, then choose an owned compatible item, Unequip, or Back. Menu / Cancel returns from
choices to slots and from slots to exploration. The panel shows Strength, Defense, Weapon Attack,
Max HP, and Max MP. Weapon Attack is deliberately separate from Strength; it is not a damage
preview and does not repeat combat math.

## Current limits

Only the active party actor and main-hand weapon slot are presented. The temporary bootstrap
starter Iron Sword is available so the flow can be manually tested. Deferred: shops, full
inventory UI, comparison polish, multi-actor management, per-instance uniqueness, special
effects, armor/accessory advanced behavior, dual wielding, two-handed weapons, ATB, status
effects, and hybrid classes.
