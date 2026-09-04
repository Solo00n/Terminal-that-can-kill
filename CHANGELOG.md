# Changelog

## 1.3.1
- **Fixed turrets flipping on and off forever.** The hijack was riding the turret power toggle,
  which the facility power system (and power mods such as DefendFacility, which re-applies trap
  power several times a second) also uses — so the two fought each other. The hijack now rides a
  sentinel value tagged onto the berserk broadcast, which nothing else in the game or in other
  mods ever sends.
- **Compatible with power mods.** The mod no longer forces `turretActive` at all. Whether a trap
  has power is left entirely to the game and to power mods; this mod only ever changes *who* a
  trap targets. With DefendFacility that means a blackout arms the traps, and any you have
  hijacked defend you while the rest hunt the crew.
- **A blackout can no longer be mistaken for a hack.** Mine hijacks are signalled by a deliberate
  off-then-on blink instead of a plain shutdown, so a power cut no longer turns a quarter of the
  map's mines friendly.

## 1.3.0
- **Hijacking a turret no longer sends it berserk.** The hijack used the berserk broadcast as
  its sync carrier, so the turret really did spin out for a moment. It now travels on the
  turret's "powered down" broadcast instead — which every client receives, needs no custom
  networking, and doubles as the sound of a hack.
- **Audible hack confirmation** (`AlliedHackSound`): the trap powers down and a beat later comes
  back online on your side, built from the trap's own clips.
- **An allied turret now does exactly the damage a normal one does.** A vanilla turret kills on
  a 0.21s tick in two hits; an allied turret matches that tick and scales its force to the
  monster's health, so it is neither weaker nor stronger than a normal turret.
  `AlliedTurretDamage` and `AlliedTurretHitInterval` are gone — there is nothing left to tune
  because there is no longer any difference.

## 1.2.2
- **Allied turrets now actually shoot.** They were being forced into Berserk, which is the
  malfunction spin — no muzzle flash, no firing sound. They now run the completely normal
  vanilla sequence instead (scan -> warning beep -> track -> fire): the turret is simply handed
  a monster as its target, via a `SetTargetToPlayerBody` patch that swaps `targetTransform` for
  an aim marker on the monster. An allied turret behaves exactly like a normal one, it just
  cannot see players.
- Allied turrets also respect their own firing arc when picking a monster, so they never lock
  onto something they cannot physically face.
- Turret damage is now applied only while the turret is genuinely in its Firing state, so hits
  line up with the muzzle flash and sound.
- **A hijack is permanent now.** `AlliedDuration` defaults to `0` — once a trap is yours it
  stays yours for the rest of the round. Set a number of seconds if you want it to wear off.

## 1.2.1
- **Hijacked traps now look hijacked.** An allied turret's laser and an allied mine's blinking
  indicator turn green instead of red (`AlliedTint`). Neither is a field on the script, so the
  lights/line renderers and the dominantly-red indicator materials are found in the prefab at
  runtime and restored when the hijack ends.
- **Allied turrets idle properly.** They stay in their normal scanning state (green laser
  sweeping) and only switch to firing once a monster is actually in sight, instead of spinning
  and firing non-stop.
- **Fixed the hijack not reaching other players.** The terminal only runs the code on the client
  that typed it, so remote clients still saw a hostile trap. The hijack now travels on a vanilla
  synced RPC (turret berserk broadcast / mine deactivate broadcast) and every client re-derives
  the same deterministic roll from it.

## 1.2.0
- **New: hijacked (allied) traps.** A terminal trap command can now flip the trap to your side
  instead of detonating it. An allied turret stops seeing players entirely and hunts monsters;
  an allied mine can no longer be set off by the team and waits for a monster to walk up to it.
  Controlled by `AlliedChance`, `AlliedDuration` and the rest of the `[AlliedTraps]` section.
- The hijack roll is **deterministic per trap** (level seed + NetworkObjectId), so every client
  independently computes the same result. No custom networking, and re-entering the code cannot
  re-roll a trap.
- Turret player-immunity is a single postfix on `Turret.CheckForPlayersInLineOfSight`, which the
  game uses both to pick a target and as the condition for damaging the local player.

## 1.1.0
- **Fixed the turret "error chance" behaving as if it were always on.** A rampage re-issued
  `EnterBerserkModeServerRpc` every 1.5s; because that replays the berserk entry on remote
  clients, a single command looked like the turret entered berserk ~5 times. The rampage now
  only keeps the firing countdown alive and re-triggers solely if the turret genuinely
  dropped out of berserk.
- **Fixed rampage stretching the spin-up instead of the rampage.** `berserkTimer` means the
  1.3s wind-up while `enteringBerserkMode`, and the firing countdown afterwards. Topping it
  up during the wind-up made the turret whine and spin without shooting; it is now only
  extended during the firing phase.
- **New `TurretAlwaysBerserk` option.** `true` (default, unchanged behaviour) = every turret
  code sends it berserk and `TurretErrorChance` only adds an extended rampage. `false` = a
  turret code disables the turret as in vanilla and only an error roll sends it berserk,
  which makes the configured chance directly observable.
- Mine and turret commands now log the actual roll (`roll 0.412 >= 0.150`) so the chance can
  be verified from the BepInEx log.

## 1.0.9
- **Renamed to "Terminal that can kill" (by Solon).** New GUID `Solon.TerminalThatCanKill`
  and DLL name; delete the old plugin/config when updating.
- **Config cleanup.** Ship-door zone coordinates are now hard-coded (the ship is always at a
  fixed world position) instead of exposed as settings. Door-zone size (`DoorwayThickness/
  Width/Height`) moved to the advanced section; the `[LethalDoors]` sections are now `[Doors]`.
- Removed the legacy `[LethalDoors]` prefix from log lines (the log source already names the mod).

## 1.0.8
- **New: turret head flips 180° after berserk.** When a turret leaves berserk it remembers
  its pre-berserk facing and smoothly turns its head 180°, resting that way until the next
  berserk (still turns to fire at detected players). Configurable via
  `TurretFlipOnBerserkExit` and `TurretFlipSmoothDuration`. Patched as a postfix on
  `Turret.Update`, per-turret state kept in a dictionary.
- `TerminalDoorCloseSeconds` default set to 0.4 (field-tuned sweet spot).

## 1.0.7
- **Door death now matches the Barber (ClaySurgeonAI).** Players killed by a door use the
  exact Barber death: launched up (`Vector3.up * 14`), cause `Snipping`, ragdoll index 7
  (`PlayerDeathAnimationId` default 7).
- **Faster facility-door trigger.** `TerminalDoorCloseSeconds` default dropped 1.2 → 0.25 so
  the crush fires as the door slams instead of ~1s later, and quick open/close spam can no
  longer out-run it.

## 1.0.6
- **Facility doors: reworked to frame-driven detection.** The previous coroutine cancelled
  itself whenever a door was reopened within its ~1.2s close window, so rapid toggling meant
  the crush (and even the diagnostic log) never ran. Closing doors are now tracked and checked
  every frame instead.
- Added a `Terminal door '<name>' closing — tracking for crush` log the moment a close is
  detected, so it's clear the hook fires.

## 1.0.5
- **Fixed facility (terminal) doors never firing.** The crush was hooked on
  `SetDoorLocalClient`, but the vanilla terminal-code path goes through
  `SetDoorToggleLocalClient` → `SetDoorOpen`. Re-hooked on `SetDoorOpen` (the one method
  every client runs on a state change) and it now detects a real open→closed transition.
- The zone is built from the door's own transform (the `TerminalAccessibleObject` lives on
  the door GameObject). The diagnostic log prints the player's local offset so the geometry
  can be tuned like the ship door was.

## 1.0.4
- **Ship door now connects.** Field testing showed players stand ~0.65m off the door
  plane (a doorway has depth — you can't stand inside a closing door), just outside the
  old 0.5m half-thickness. Bumped the default `DoorwayThickness` to 2.0 so the slab covers
  the doorway depth. Orientation and centre were already correct.

## 1.0.3
- **Precise doorway zone.** Replaced the spherical `CheckRadius` with a thin oriented slab
  in the doorway opening (`DoorwayThickness` / `DoorwayWidth` / `DoorwayHeight`). Only
  someone standing *in* the opening is crushed, not merely near the door. Applies to both
  the ship door and terminal (facility) doors.
- **Fixed the hanging corpse.** `PlayerDeathAnimationId` now defaults to `0` — the normal
  physics ragdoll that falls. Other indices point to special/animated bodies that can
  freeze in mid-air.
- Dropped the custom crunch sound: the game already plays `playerCrushDeath` for
  `CauseOfDeath.Crushing`.

## 1.0.2
- **Fix: ship door now actually crushes.** The kill zone was centred on the wrong point
  (`outsideDoorPoint`), so the player was never inside it. The ship sits at a fixed world
  position, so the doorway zone is now a world coordinate (default `-5.72, 0.305, -14.1`,
  configurable) — the same reference proven to work by other door mods.
- Detection rewritten to patch `HangarShipDoor.Update`, using `doorPower < 1` plus the
  animator's `"ShipDoorClose"` state (with a time fallback) to know when the door is shut.

## 1.0.1
- **Fix: ship door now kills.** Vanilla never calls `PlayDoorAnimation(true)` on close,
  so detection now watches the authoritative `StartOfRound.hangarDoorsClosed` flag
  (rising edge, on the surface only).
- **Fix: turret now goes berserk.** Mirrors vanilla `Hit()` exactly — a local
  `SwitchTurretMode(3)` plus `EnterBerserkModeServerRpc(...)` (the ClientRpc deliberately
  skips the triggering client, so the local call is required).
- **Split error chance** into `MineErrorChance` and `TurretErrorChance` for independent tuning.
- Added a diagnostic log of the local player's distance to the door zone centre to help
  tune `CheckRadius`.

## 1.0.0
- Initial release.
- **Lethal Doors**: ship hangar door and big terminal-controlled facility doors kill
  players and/or monsters caught in the doorway when they finish closing.
  - Configurable affected doors, safe period after landing, kill radius, and
    InstantKill / DamageOverTime modes.
  - Ghost-Girl style head-pop death animation + best-effort crunch sound.
  - Monster exclusion list for enemies that cannot enter the ship.
- **Remote Trap Control**: terminal codes detonate mines and send turrets berserk
  instead of disabling them, with a configurable critical-error chance (mine chain
  reaction / sustained turret rampage).
