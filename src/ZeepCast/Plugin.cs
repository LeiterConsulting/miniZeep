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
        public const string PluginVersion = "0.2.1";

        internal static ManualLogSource Log { get; private set; } = null!;

        private BroadcastDirector? _director;

        private void Awake()
        {
            Log = Logger;
            DontDestroyOnLoad(gameObject);

            var settings = new BroadcastSettings(
                Config.Bind("Controls", "ToggleDirector", KeyCode.F6,
                    "Enter or leave the ZeepCast director."),
                Config.Bind("Controls", "ToggleInterface", KeyCode.F9,
                    "Show or hide the broadcast interface and racer markers."),
                Config.Bind("Controls", "ToggleDirectorConsole", KeyCode.BackQuote,
                    "Show or hide operator-only controls while keeping program graphics visible."),
                Config.Bind("Controls", "ToggleMarkers", KeyCode.M,
                    "Show or hide on-track racer labels."),
                Config.Bind("Controls", "ToggleCameraMode", KeyCode.V,
                    "Cycle overview, live-field, and followed-racer camera shots."),
                Config.Bind("Controls", "PreviousRacer", KeyCode.LeftBracket,
                    "Follow the previous racer."),
                Config.Bind("Controls", "NextRacer", KeyCode.RightBracket,
                    "Follow the next racer."),
                Config.Bind("Controls", "ToggleHelp", KeyCode.H,
                    "Show or hide the complete operator control reference."),
                Config.Bind("Controls", "FollowIsometric", KeyCode.Alpha1,
                    "Select the isometric racer-following shot."),
                Config.Bind("Controls", "FollowChase", KeyCode.Alpha2,
                    "Select the chase racer-following shot."),
                Config.Bind("Controls", "FollowLead", KeyCode.Alpha3,
                    "Select the lead racer-following shot."),
                Config.Bind("Controls", "FollowTrackside", KeyCode.Alpha4,
                    "Select the trackside racer-following shot."),
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
            ConfigEntry<KeyCode> toggleDirector,
            ConfigEntry<KeyCode> toggleInterface,
            ConfigEntry<KeyCode> toggleDirectorConsole,
            ConfigEntry<KeyCode> toggleMarkers,
            ConfigEntry<KeyCode> toggleCameraMode,
            ConfigEntry<KeyCode> previousRacer,
            ConfigEntry<KeyCode> nextRacer,
            ConfigEntry<KeyCode> toggleHelp,
            ConfigEntry<KeyCode> followIsometric,
            ConfigEntry<KeyCode> followChase,
            ConfigEntry<KeyCode> followLead,
            ConfigEntry<KeyCode> followTrackside,
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
            ToggleHelp = toggleHelp;
            FollowIsometric = followIsometric;
            FollowChase = followChase;
            FollowLead = followLead;
            FollowTrackside = followTrackside;
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

        public ConfigEntry<KeyCode> ToggleDirector { get; }
        public ConfigEntry<KeyCode> ToggleInterface { get; }
        public ConfigEntry<KeyCode> ToggleDirectorConsole { get; }
        public ConfigEntry<KeyCode> ToggleMarkers { get; }
        public ConfigEntry<KeyCode> ToggleCameraMode { get; }
        public ConfigEntry<KeyCode> PreviousRacer { get; }
        public ConfigEntry<KeyCode> NextRacer { get; }
        public ConfigEntry<KeyCode> ToggleHelp { get; }
        public ConfigEntry<KeyCode> FollowIsometric { get; }
        public ConfigEntry<KeyCode> FollowChase { get; }
        public ConfigEntry<KeyCode> FollowLead { get; }
        public ConfigEntry<KeyCode> FollowTrackside { get; }
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
