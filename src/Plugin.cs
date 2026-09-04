using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace LethalDoors
{
    /// <summary>
    /// BepInEx entry point. Binds config, applies Harmony patches and exposes
    /// a shared logger + config that the rest of the mod uses.
    ///
    /// Because BaseUnityPlugin is itself a MonoBehaviour, <see cref="Instance"/>
    /// doubles as our coroutine host (used by the door / trap managers).
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "Solon.TerminalThatCanKill";
        public const string PluginName = "Terminal that can kill";
        public const string PluginVersion = "1.3.3";

        public static Plugin Instance { get; private set; }
        public static ManualLogSource Log { get; private set; }

        // 'new' silences CS0108: this static config intentionally shadows the
        // inherited BaseUnityPlugin.Config instance property for convenient access.
        internal static new ModConfig Config { get; private set; }

        private readonly Harmony _harmony = new Harmony(PluginGuid);

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            Config = new ModConfig(base.Config);

            try
            {
                _harmony.PatchAll(typeof(Plugin).Assembly);
                Log.LogInfo($"{PluginName} v{PluginVersion} loaded — Harmony patches applied.");
            }
            catch (Exception e)
            {
                Log.LogError($"Failed to apply Harmony patches: {e}");
            }
        }
    }
}
