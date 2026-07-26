using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;
using ZeepCast.Core;

namespace ZeepCast
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class ZeepCastPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.chris.zeepcast";
        public const string PluginName = "ZeepCast";
        public const string PluginVersion = "0.1.0";

        internal static ManualLogSource Log { get; private set; } = null!;

        private BroadcastDirector? _director;

        private void Awake()
        {
            Log = Logger;
            DontDestroyOnLoad(gameObject);

            var settings = new BroadcastSettings(
                Config.Bind("Controls", "ToggleDirector", new KeyboardShortcut(KeyCode.F8),
                    "Enter or leave the ZeepCast director."),
                Config.Bind("Controls", "ToggleInterface", new KeyboardShortcut(KeyCode.F9),
                    "Show or hide the broadcast interface and racer markers."),
                Config.Bind("Controls", "ToggleCameraMode", new KeyboardShortcut(KeyCode.Tab),
                    "Switch between whole-level overview and followed-racer camera modes."),
                Config.Bind("Controls", "PreviousRacer", new KeyboardShortcut(KeyCode.LeftBracket),
                    "Follow the previous racer."),
                Config.Bind("Controls", "NextRacer", new KeyboardShortcut(KeyCode.RightBracket),
                    "Follow the next racer."),
                Config.Bind("Broadcast", "EventTitle", "ZEEPKIST LIVE",
                    "Title displayed in the broadcast header."),
                Config.Bind("Camera", "Pitch", 48f,
                    "Isometric camera pitch in degrees."),
                Config.Bind("Camera", "Yaw", 45f,
                    "Initial isometric camera yaw in degrees."),
                Config.Bind("Camera", "FollowSize", 45f,
                    "Orthographic size used while following a racer."),
                Config.Bind("Camera", "OverviewPadding", 1.18f,
                    "Extra space around the level in overview mode."),
                Config.Bind("Camera", "PositionSmoothing", 6f,
                    "How quickly the follow camera catches its racer."),
                Config.Bind("Camera", "AutoEnterPhotoMode", true,
                    "Use Zeepkist's native photo-mode transition when ZeepCast is activated."),
                Config.Bind("Rendering", "ForceSolidRacers", true,
                    "Render remote live racers with their original cosmetic materials instead of Zeepkist's translucent ghost material.")
            );

            _director = gameObject.AddComponent<BroadcastDirector>();
            _director.Initialize(settings);

            Logger.LogInfo($"{PluginName} v{PluginVersion} loaded. Press {settings.ToggleDirector.Value} in GameScene.");
        }

        private void OnDestroy()
        {
            if (_director != null)
            {
                Destroy(_director);
            }
        }
    }

    internal sealed class BroadcastSettings
    {
        public BroadcastSettings(
            ConfigEntry<KeyboardShortcut> toggleDirector,
            ConfigEntry<KeyboardShortcut> toggleInterface,
            ConfigEntry<KeyboardShortcut> toggleCameraMode,
            ConfigEntry<KeyboardShortcut> previousRacer,
            ConfigEntry<KeyboardShortcut> nextRacer,
            ConfigEntry<string> eventTitle,
            ConfigEntry<float> pitch,
            ConfigEntry<float> yaw,
            ConfigEntry<float> followSize,
            ConfigEntry<float> overviewPadding,
            ConfigEntry<float> positionSmoothing,
            ConfigEntry<bool> autoEnterPhotoMode,
            ConfigEntry<bool> forceSolidRacers)
        {
            ToggleDirector = toggleDirector;
            ToggleInterface = toggleInterface;
            ToggleCameraMode = toggleCameraMode;
            PreviousRacer = previousRacer;
            NextRacer = nextRacer;
            EventTitle = eventTitle;
            Pitch = pitch;
            Yaw = yaw;
            FollowSize = followSize;
            OverviewPadding = overviewPadding;
            PositionSmoothing = positionSmoothing;
            AutoEnterPhotoMode = autoEnterPhotoMode;
            ForceSolidRacers = forceSolidRacers;
        }

        public ConfigEntry<KeyboardShortcut> ToggleDirector { get; }
        public ConfigEntry<KeyboardShortcut> ToggleInterface { get; }
        public ConfigEntry<KeyboardShortcut> ToggleCameraMode { get; }
        public ConfigEntry<KeyboardShortcut> PreviousRacer { get; }
        public ConfigEntry<KeyboardShortcut> NextRacer { get; }
        public ConfigEntry<string> EventTitle { get; }
        public ConfigEntry<float> Pitch { get; }
        public ConfigEntry<float> Yaw { get; }
        public ConfigEntry<float> FollowSize { get; }
        public ConfigEntry<float> OverviewPadding { get; }
        public ConfigEntry<float> PositionSmoothing { get; }
        public ConfigEntry<bool> AutoEnterPhotoMode { get; }
        public ConfigEntry<bool> ForceSolidRacers { get; }
    }
}
