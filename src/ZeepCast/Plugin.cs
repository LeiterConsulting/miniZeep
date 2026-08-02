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
        public const string PluginVersion = "0.2.0";

        internal static ManualLogSource Log { get; private set; } = null!;

        private BroadcastDirector? _director;

        private void Awake()
        {
            Log = Logger;
            DontDestroyOnLoad(gameObject);

            var settings = new BroadcastSettings(
                Config.Bind("Controls", "ToggleDirector", new KeyboardShortcut(KeyCode.F6),
                    "Enter or leave the ZeepCast director."),
                Config.Bind("Controls", "ToggleInterface", new KeyboardShortcut(KeyCode.F9),
                    "Show or hide the broadcast interface and racer markers."),
                Config.Bind("Controls", "ToggleDirectorConsole", new KeyboardShortcut(KeyCode.BackQuote),
                    "Show or hide operator-only controls while keeping program graphics visible."),
                Config.Bind("Controls", "ToggleMarkers", new KeyboardShortcut(KeyCode.M),
                    "Show or hide on-track racer labels."),
                Config.Bind("Controls", "ToggleCameraMode", new KeyboardShortcut(KeyCode.V),
                    "Cycle overview, live-field, and followed-racer camera shots."),
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
                Config.Bind("Camera", "FieldPadding", 1.3f,
                    "Extra space around the live racer group in field mode."),
                Config.Bind("Camera", "PositionSmoothing", 6f,
                    "How quickly the follow camera catches its racer."),
                Config.Bind("Camera", "AutoEnterPhotoMode", true,
                    "Use Zeepkist's native photo-mode transition when ZeepCast is activated."),
                Config.Bind("Rendering", "ForceSolidRacers", true,
                    "Render remote live racers with their original cosmetic materials instead of Zeepkist's translucent ghost material."),
                Config.Bind("Rendering", "HighlightSelectedRacer", true,
                    "Draw a subtle local silhouette where the selected racer is hidden by track geometry."),
                Config.Bind("Broadcast", "StartWithDirectorConsole", true,
                    "Show operator controls when ZeepCast starts. Press the console key for a clean feed.")
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
            ConfigEntry<KeyboardShortcut> toggleDirectorConsole,
            ConfigEntry<KeyboardShortcut> toggleMarkers,
            ConfigEntry<KeyboardShortcut> toggleCameraMode,
            ConfigEntry<KeyboardShortcut> previousRacer,
            ConfigEntry<KeyboardShortcut> nextRacer,
            ConfigEntry<string> eventTitle,
            ConfigEntry<float> pitch,
            ConfigEntry<float> yaw,
            ConfigEntry<float> followSize,
            ConfigEntry<float> overviewPadding,
            ConfigEntry<float> fieldPadding,
            ConfigEntry<float> positionSmoothing,
            ConfigEntry<bool> autoEnterPhotoMode,
            ConfigEntry<bool> forceSolidRacers,
            ConfigEntry<bool> highlightSelectedRacer,
            ConfigEntry<bool> startWithDirectorConsole)
        {
            ToggleDirector = toggleDirector;
            ToggleInterface = toggleInterface;
            ToggleDirectorConsole = toggleDirectorConsole;
            ToggleMarkers = toggleMarkers;
            ToggleCameraMode = toggleCameraMode;
            PreviousRacer = previousRacer;
            NextRacer = nextRacer;
            EventTitle = eventTitle;
            Pitch = pitch;
            Yaw = yaw;
            FollowSize = followSize;
            OverviewPadding = overviewPadding;
            FieldPadding = fieldPadding;
            PositionSmoothing = positionSmoothing;
            AutoEnterPhotoMode = autoEnterPhotoMode;
            ForceSolidRacers = forceSolidRacers;
            HighlightSelectedRacer = highlightSelectedRacer;
            StartWithDirectorConsole = startWithDirectorConsole;
        }

        public ConfigEntry<KeyboardShortcut> ToggleDirector { get; }
        public ConfigEntry<KeyboardShortcut> ToggleInterface { get; }
        public ConfigEntry<KeyboardShortcut> ToggleDirectorConsole { get; }
        public ConfigEntry<KeyboardShortcut> ToggleMarkers { get; }
        public ConfigEntry<KeyboardShortcut> ToggleCameraMode { get; }
        public ConfigEntry<KeyboardShortcut> PreviousRacer { get; }
        public ConfigEntry<KeyboardShortcut> NextRacer { get; }
        public ConfigEntry<string> EventTitle { get; }
        public ConfigEntry<float> Pitch { get; }
        public ConfigEntry<float> Yaw { get; }
        public ConfigEntry<float> FollowSize { get; }
        public ConfigEntry<float> OverviewPadding { get; }
        public ConfigEntry<float> FieldPadding { get; }
        public ConfigEntry<float> PositionSmoothing { get; }
        public ConfigEntry<bool> AutoEnterPhotoMode { get; }
        public ConfigEntry<bool> ForceSolidRacers { get; }
        public ConfigEntry<bool> HighlightSelectedRacer { get; }
        public ConfigEntry<bool> StartWithDirectorConsole { get; }
    }
}
