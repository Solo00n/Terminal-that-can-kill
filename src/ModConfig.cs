using BepInEx.Configuration;

namespace LethalDoors
{
    /// <summary>Which doors the "deadly door" mechanic applies to.</summary>
    public enum AffectedDoors
    {
        ShipDoor,       // Only the ship hangar door (ramp).
        TerminalDoors,  // Only the big powered facility doors controlled by terminal codes.
        Both
    }

    /// <summary>How a caught entity is harmed when a door closes on it.</summary>
    public enum DamageMode
    {
        InstantKill,
        DamageOverTime
    }

    /// <summary>
    /// Central strongly-typed wrapper around the BepInEx config file.
    /// All tunables live here so the rest of the code never touches ConfigEntry directly.
    /// </summary>
    internal sealed class ModConfig
    {
        // ---- [Doors] core -----------------------------------------------------
        public readonly ConfigEntry<bool> Enabled;
        public readonly ConfigEntry<bool> KillPlayers;
        public readonly ConfigEntry<bool> KillMonsters;
        public readonly ConfigEntry<AffectedDoors> AffectedDoors;
        public readonly ConfigEntry<float> SafeZoneSeconds;
        public readonly ConfigEntry<DamageMode> DamageMode;
        public readonly ConfigEntry<int> DamageAmount;

        // ---- [Doors.Advanced] -------------------------------------------------
        public readonly ConfigEntry<float> ShipDoorCloseSeconds;
        public readonly ConfigEntry<float> TerminalDoorCloseSeconds;
        public readonly ConfigEntry<float> DoorwayThickness;
        public readonly ConfigEntry<float> DoorwayWidth;
        public readonly ConfigEntry<float> DoorwayHeight;
        public readonly ConfigEntry<int> PlayerDeathAnimationId;
        public readonly ConfigEntry<string> ExcludedEnemies;

        // ---- [RemoteControl] -------------------------------------------------
        public readonly ConfigEntry<bool> EnableRemoteControl;
        public readonly ConfigEntry<float> MineErrorChance;
        public readonly ConfigEntry<float> TurretErrorChance;
        public readonly ConfigEntry<float> MineErrorDamageMultiplier;
        public readonly ConfigEntry<float> TurretRampageDuration;
        public readonly ConfigEntry<bool> TurretFlipOnBerserkExit;
        public readonly ConfigEntry<float> TurretFlipSmoothDuration;

        // ---- [RemoteControl.Advanced] ---------------------------------------
        public readonly ConfigEntry<float> MineErrorChainRadius;
        public readonly ConfigEntry<bool> MineWarnBeforeDetonate;

        public ModConfig(ConfigFile cfg)
        {
            // ---------------------------------------------------------------- Doors (core)
            Enabled = cfg.Bind("Doors", "Enabled", true,
                "Master switch for the deadly-doors mechanic.");

            KillPlayers = cfg.Bind("Doors", "KillPlayers", true,
                "Kill players caught in a closing door's threshold.");

            KillMonsters = cfg.Bind("Doors", "KillMonsters", false,
                "Kill monsters caught in a closing door's threshold (only enemies that can enter the ship).");

            AffectedDoors = cfg.Bind("Doors", "AffectedDoors", LethalDoors.AffectedDoors.Both,
                "Which doors are deadly: ShipDoor, TerminalDoors or Both.");

            SafeZoneSeconds = cfg.Bind("Doors", "SafeZoneSeconds", 5.0f,
                new ConfigDescription("Grace period after the ship lands during which doors do NOT kill. 0 disables the grace period.",
                    new AcceptableValueRange<float>(0f, 120f)));

            DamageMode = cfg.Bind("Doors", "DamageMode", LethalDoors.DamageMode.InstantKill,
                "InstantKill = kill immediately when the door finishes closing. DamageOverTime = damage players while the door stays shut on them.");

            DamageAmount = cfg.Bind("Doors", "DamageAmount", 100,
                new ConfigDescription("Damage-per-second applied to players in DamageOverTime mode (ignored for InstantKill).",
                    new AcceptableValueRange<int>(1, 100)));

            // ---------------------------------------------------------------- Doors.Advanced
            ShipDoorCloseSeconds = cfg.Bind("Doors.Advanced", "ShipDoorCloseSeconds", 1.6f,
                "Approximate duration of the ship-door close animation. The kill check fires this long after the door starts closing.");

            TerminalDoorCloseSeconds = cfg.Bind("Doors.Advanced", "TerminalDoorCloseSeconds", 0.4f,
                "Delay (sec) between a facility door starting to close and the kill check firing. " +
                "Big doors slam shut fast, so keep this small — larger values make the crush feel late " +
                "and let quick open/close spam dodge it.");

            // Kill-zone shape. The zone is a thin oriented slab sitting in the doorway opening,
            // NOT a sphere — only someone standing IN the opening is caught, not merely near the
            // door. These stay configurable because door sizes differ (ship ramp vs facility
            // doors vs modded doors) and the facility-door facing can't be verified from the
            // game files, so tuning is occasionally needed.
            DoorwayThickness = cfg.Bind("Doors.Advanced", "DoorwayThickness", 2.0f,
                new ConfigDescription("Depth (metres) of the kill slab THROUGH the doorway — the 'thin line'. " +
                    "A doorway is ~1.3m deep (you stand just inside/outside a closing door), so ~2.0 reliably " +
                    "catches someone in the opening while staying far tighter than a proximity sphere.",
                    new AcceptableValueRange<float>(0.2f, 6f)));

            DoorwayWidth = cfg.Bind("Doors.Advanced", "DoorwayWidth", 3.0f,
                new ConfigDescription("Width (metres) of the kill slab across the doorway opening.",
                    new AcceptableValueRange<float>(0.5f, 8f)));

            DoorwayHeight = cfg.Bind("Doors.Advanced", "DoorwayHeight", 5.0f,
                new ConfigDescription("Height (metres) of the kill slab. Kept generous so it never misses vertically.",
                    new AcceptableValueRange<float>(1f, 12f)));

            PlayerDeathAnimationId = cfg.Bind("Doors.Advanced", "PlayerDeathAnimationId", 7,
                "Index into StartOfRound.playerRagdolls picking which corpse spawns. " +
                "7 = the Barber (ClaySurgeonAI) 'Snipping' death — body is launched up and ragdolls (default). " +
                "0 = plain physics ragdoll that just falls. -1 = no body.");

            ExcludedEnemies = cfg.Bind("Doors.Advanced", "ExcludedEnemies",
                "Girl,RadMech,Manticoil,Docile,Roaming Locust,Red Locust Bees,Locust,Earth Leviathan,Forest Giant",
                "Comma-separated list of enemy internal names that can NEVER be crushed (matched case-insensitively as substrings). " +
                "Defaults exclude enemies that cannot physically enter the ship (Ghost Girl, Old Bird, Manticoil, Locusts, etc.).");

            // ---------------------------------------------------------------- RemoteControl
            EnableRemoteControl = cfg.Bind("RemoteControl", "EnableRemoteControl", true,
                "Replace terminal 'disable mine/turret' with detonate/rampage. False = vanilla behaviour (disable).");

            MineErrorChance = cfg.Bind("RemoteControl", "MineErrorChance", 0.15f,
                new ConfigDescription("Chance (0..1) that a remote MINE command critically malfunctions (chain detonation).",
                    new AcceptableValueRange<float>(0f, 1f)));

            TurretErrorChance = cfg.Bind("RemoteControl", "TurretErrorChance", 0.15f,
                new ConfigDescription("Chance (0..1) that a remote TURRET command critically malfunctions (sustained rampage).",
                    new AcceptableValueRange<float>(0f, 1f)));

            MineErrorDamageMultiplier = cfg.Bind("RemoteControl", "MineErrorDamageMultiplier", 2.0f,
                new ConfigDescription("On a mine error the blast is amplified: nearby mines within MineErrorChainRadius * this factor also detonate.",
                    new AcceptableValueRange<float>(1f, 5f)));

            TurretRampageDuration = cfg.Bind("RemoteControl", "TurretRampageDuration", 5.0f,
                new ConfigDescription("Seconds a turret keeps re-triggering its berserk state on an error (chaotic spin-fire).",
                    new AcceptableValueRange<float>(0f, 30f)));

            TurretFlipOnBerserkExit = cfg.Bind("RemoteControl", "TurretFlipOnBerserkExit", true,
                "When a turret leaves berserk, turn its head 180° from the angle it had before berserk and " +
                "keep it there as the new resting facing until the next berserk (it can still turn to fire at " +
                "detected players).");

            TurretFlipSmoothDuration = cfg.Bind("RemoteControl", "TurretFlipSmoothDuration", 0.5f,
                new ConfigDescription("Seconds the 180° head turn takes on berserk exit. 0 = instant snap.",
                    new AcceptableValueRange<float>(0f, 5f)));

            // ---------------------------------------------------------------- RemoteControl.Advanced
            MineErrorChainRadius = cfg.Bind("RemoteControl.Advanced", "MineErrorChainRadius", 8.0f,
                "Base radius (metres) used to find neighbouring mines for the chain reaction on a mine error.");

            MineWarnBeforeDetonate = cfg.Bind("RemoteControl.Advanced", "MineWarnBeforeDetonate", false,
                "If true, a NORMAL (non-error) mine command plays the warning beep before exploding. Errors always detonate instantly.");
        }
    }
}
