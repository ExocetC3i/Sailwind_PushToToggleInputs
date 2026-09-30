using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace PushToToggleInputs
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class PushToToggleInputsPlugin : BaseUnityPlugin
    {
        internal const string PluginGuid = "com.sailwind.movementwinchtoggles";
        internal const string PluginName = "Sailwind Push To Toggle Inputs";
        internal const string PluginVersion = "1.0.0";

        private Harmony _harmony;

        internal static ConfigEntry<KeyboardShortcut> AutoForwardShortcut { get; private set; }
        internal static ConfigEntry<KeyboardShortcut> WinchPullInShortcut { get; private set; }
        internal static ConfigEntry<KeyboardShortcut> WinchPayOutShortcut { get; private set; }
        internal static ManualLogSource PluginLog { get; private set; }
        internal static bool AutoForwardEnabled { get; set; }

        private void Awake()
        {
            PluginLog = Logger;

            AutoForwardShortcut = Config.Bind(
                "Movement",
                "Auto-forward toggle",
                new KeyboardShortcut(KeyCode.Z),
                "Press to toggle forward movement. Forward or backward keyboard input cancels auto-forward.");
            WinchPullInShortcut = Config.Bind(
                "Winch",
                "Toggle pull in",
                new KeyboardShortcut(KeyCode.PageUp),
                "Press while operating a winch to toggle pulling the rope in.");
            WinchPayOutShortcut = Config.Bind(
                "Winch",
                "Toggle pay out",
                new KeyboardShortcut(KeyCode.PageDown),
                "Press while operating a winch to toggle paying rope out.");

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(PushToToggleInputsPlugin).Assembly);
            Logger.LogInfo("Movement and winch toggle shortcuts are ready.");
        }

        private void Update()
        {
            if (!GameState.playing)
            {
                AutoForwardEnabled = false;
                WinchToggleState.Clear();
                return;
            }

            if (ShortcutInput.IsDown(AutoForwardShortcut.Value))
            {
                AutoForwardEnabled = !AutoForwardEnabled;
                Logger.LogInfo($"Auto-forward {(AutoForwardEnabled ? "enabled" : "disabled")}.");
            }

            WinchToggleState.ClearIfDestroyed();
        }

        private void OnDestroy()
        {
            AutoForwardEnabled = false;
            WinchToggleState.Clear();
            _harmony?.UnpatchSelf();
        }
    }
}
