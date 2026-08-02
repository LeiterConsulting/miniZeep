using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using ZeepCast.Rendering;
using ZeepCast.UI;
using ZeepkistClient;

namespace ZeepCast.Core
{
    internal enum BroadcastCameraMode
    {
        Overview,
        Field,
        Follow
    }

    internal enum FollowShotStyle
    {
        Isometric,
        Chase,
        Lead,
        Trackside
    }

    internal sealed class BroadcastDirector : MonoBehaviour
    {
        private const string GameSceneName = "GameScene";
        private const float RosterRefreshInterval = 0.2f;
        private const float MinimumViewSize = 1.5f;

        private sealed class RacerAttemptState
        {
            public bool Initialized;
            public bool HadResult;
            public bool Finished;
            public float ResultTime;
            public int ResultCheckpoints;
            public float FinishTime;
            public float LastDrivingRuntime;
        }

        private BroadcastSettings _settings = null!;
        private BroadcastHud? _hud;
        private SolidRacerPresentation? _solidRacerPresentation;
        private SelectedRacerVisibility? _selectedRacerVisibility;
        private readonly NativeSpectatorGraphics _nativeSpectatorGraphics = new();
        private readonly List<RacerSnapshot> _racers = new();
        private readonly Dictionary<ulong, RacerAttemptState> _attemptStates = new();

        private GameMaster? _master;
        private FlyingCameraScript? _flyingCamera;
        private Camera? _camera;
        private SpectatorCameraUI? _spectatorUi;
        private PauseMenuUI? _pauseMenuUi;

        private bool _initialized;
        private bool _active;
        private bool _uiVisible = true;
        private bool _directorConsoleVisible = true;
        private bool _markersVisible = true;
        private bool _flyingCameraWasEnabled;
        private bool _spectatorUiWasOpen;
        private bool _enteredPhotoMode;
        private bool _visualizationWasActive;
        private bool _hudAutoHiddenOutsideVisualization;
        private bool _cameraOwned;
        private bool _helpVisible;

        private bool _cameraWasOrthographic;
        private float _cameraOrthographicSize;
        private float _cameraFieldOfView;
        private float _cameraNearClip;
        private float _cameraFarClip;
        private bool _cameraLayerCullSpherical;
        private float[] _cameraLayerCullDistances = Array.Empty<float>();
        private Vector3 _cameraPosition;
        private Quaternion _cameraRotation;
        private CursorLockMode _cursorLockMode;
        private bool _cursorVisible;

        private Bounds _levelBounds;
        private bool _hasLevelBounds;
        private Vector3 _overviewPivot;
        private Vector3 _fieldPivot;
        private Vector3 _currentPivot;
        private Vector3 _panOffset;
        private float _overviewSize = 80f;
        private float _fieldSize = 45f;
        private float _currentSize = 45f;
        private float _zoomScale = 1f;
        private float _yaw;
        private float _pitch;
        private float _nextRosterRefresh;
        private ulong _selectedSteamId;
        private string _levelTitle = "Unknown level";
        private BroadcastFieldSummary _fieldSummary;
        private ulong _motionSteamId;
        private Vector3 _lastMotionPosition;
        private Vector3 _smoothedMotionDirection = Vector3.forward;
        private bool _hasMotionSample;
        private Vector3 _tracksideCameraPosition;
        private bool _hasTracksideCameraPosition;

        // External HUD integrations use IsActive to decide whether ZeepCast owns
        // the program view. A session can remain armed after returning to the race
        // so F9 still works, but it must not suppress the game's race chrome.
        public bool IsActive => _active && IsVisualizationActive;
        internal bool IsSessionActive => _active;
        public bool IsUiVisible => _uiVisible;
        public bool IsDirectorConsoleVisible => _directorConsoleVisible;
        public bool AreMarkersVisible => _markersVisible;
        public bool IsHelpVisible => _helpVisible;
        public bool IsVisualizationActive =>
            _active &&
            _master != null &&
            _master.flyingCamera != null &&
            _master.flyingCamera.isPhotoMode &&
            _camera != null &&
            _camera.gameObject.activeInHierarchy &&
            (_pauseMenuUi == null || !_pauseMenuUi.IsOpen);
        public BroadcastCameraMode CameraMode { get; private set; } = BroadcastCameraMode.Overview;
        public FollowShotStyle FollowStyle { get; private set; } = FollowShotStyle.Isometric;
        public IReadOnlyList<RacerSnapshot> Racers => _racers;
        public ulong SelectedSteamId => _selectedSteamId;
        public Camera? BroadcastCamera => _camera;
        public string EventTitle => _settings.EventTitle.Value;
        public string LevelTitle => _levelTitle;
        public BroadcastFieldSummary FieldSummary => _fieldSummary;
        public string ShotLabel => CameraMode == BroadcastCameraMode.Follow
            ? $"FOLLOW / {FollowStyle.ToString().ToUpperInvariant()}"
            : CameraMode.ToString().ToUpperInvariant();

        public string LobbyClock
        {
            get
            {
                if (!ZeepkistNetwork.IsConnected || ZeepkistNetwork.CurrentLobby == null)
                {
                    return "--:--";
                }

                return string.IsNullOrWhiteSpace(ZeepkistNetwork.CurrentLobby.timeLeftString)
                    ? "--:--"
                    : ZeepkistNetwork.CurrentLobby.timeLeftString;
            }
        }

        public void Initialize(BroadcastSettings settings)
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            _settings = settings;
            _solidRacerPresentation = new SolidRacerPresentation(settings.ForceSolidRacers.Value);
            _selectedRacerVisibility = new SelectedRacerVisibility(settings.HighlightSelectedRacer.Value);
            _yaw = settings.Yaw.Value;
            _pitch = settings.Pitch.Value;
            _currentSize = Mathf.Max(MinimumViewSize, settings.FollowSize.Value);

            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;

            var hudObject = new GameObject("[ZeepCast] Broadcast HUD");
            DontDestroyOnLoad(hudObject);
            _hud = hudObject.AddComponent<BroadcastHud>();
            _hud.Initialize(this);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            Deactivate(restoreCamera: true);

            if (_hud != null)
            {
                Destroy(_hud.gameObject);
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _racers.Clear();
            _attemptStates.Clear();
            _selectedSteamId = 0;
            _hasLevelBounds = false;
            _levelTitle = "Unknown level";
            _hud?.SetVisible(false);
        }

        private void OnSceneUnloaded(Scene scene)
        {
            if (scene.name == GameSceneName)
            {
                Deactivate(restoreCamera: false);
            }
        }

        private void Update()
        {
            if (!_initialized)
            {
                return;
            }

            if (IsPressed(_settings.ToggleDirector))
            {
                if (_active)
                {
                    Deactivate(restoreCamera: true);
                }
                else
                {
                    TryActivate();
                }
            }

            if (!_active)
            {
                return;
            }

            UpdateVisualizationState();

            if (!_active)
            {
                return;
            }

            // Photo mode can reopen its own spectator UI one frame after the
            // camera transition. ZeepCast keeps only that presentation surface
            // closed; it does not alter lobby or network state.
            if (IsVisualizationActive && _spectatorUi != null && _spectatorUi.IsOpen)
            {
                _spectatorUi.Close(announce: false);
            }

            if (IsPressed(_settings.ToggleInterface))
            {
                _hudAutoHiddenOutsideVisualization = false;
                SetInterfaceVisible(!_uiVisible);
            }

            if (IsPressed(_settings.ToggleDirectorConsole))
            {
                SetDirectorConsoleVisible(!_directorConsoleVisible);
            }

            if (IsPressed(_settings.ToggleMarkers))
            {
                SetMarkersVisible(!_markersVisible);
            }

            if (Time.unscaledTime >= _nextRosterRefresh)
            {
                RefreshRacers();
                _nextRosterRefresh = Time.unscaledTime + RosterRefreshInterval;
            }

            if (!IsVisualizationActive)
            {
                return;
            }

            if (IsPressed(_settings.ToggleHelp))
            {
                SetHelpVisible(!_helpVisible);
            }

            if (IsPressed(_settings.FollowIsometric))
            {
                SelectFollowStyle(FollowShotStyle.Isometric);
            }
            else if (IsPressed(_settings.FollowChase))
            {
                SelectFollowStyle(FollowShotStyle.Chase);
            }
            else if (IsPressed(_settings.FollowLead))
            {
                SelectFollowStyle(FollowShotStyle.Lead);
            }
            else if (IsPressed(_settings.FollowTrackside))
            {
                SelectFollowStyle(FollowShotStyle.Trackside);
            }

            if (IsPressed(_settings.ToggleCameraMode))
            {
                ToggleCameraMode();
            }

            if (IsPressed(_settings.PreviousRacer))
            {
                SelectRelative(-1);
            }

            if (IsPressed(_settings.NextRacer))
            {
                SelectRelative(1);
            }

            HandleCameraInput();
        }

        private void LateUpdate()
        {
            if (!_active)
            {
                return;
            }

            if (!IsVisualizationActive)
            {
                return;
            }

            _nativeSpectatorGraphics.Apply(_spectatorUi);

            _solidRacerPresentation?.Apply(_racers);

            if (TryGetSelected(out var highlighted))
            {
                _selectedRacerVisibility?.Apply(_camera!, highlighted);
            }
            else
            {
                _selectedRacerVisibility?.Clear();
            }

            if (_camera == null)
            {
                return;
            }

            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 desiredPivot;
            float desiredSize;

            if (_hasLevelBounds)
            {
                _overviewSize = CalculateViewSize(
                    _levelBounds,
                    rotation,
                    Mathf.Max(1f, _settings.OverviewPadding.Value),
                    20f);
            }

            UpdateFieldFraming(rotation);

            if (CameraMode == BroadcastCameraMode.Follow &&
                FollowStyle != FollowShotStyle.Isometric &&
                TryGetSelected(out var dynamicRacer))
            {
                ApplyDynamicFollowShot(dynamicRacer);
                return;
            }

            if (CameraMode == BroadcastCameraMode.Follow && TryGetSelected(out var selected))
            {
                desiredPivot = selected.Transform.position + _panOffset;
                desiredSize = GetBaseViewSize() * _zoomScale;
            }
            else if (CameraMode == BroadcastCameraMode.Field)
            {
                desiredPivot = _fieldPivot + _panOffset;
                desiredSize = GetBaseViewSize() * _zoomScale;
            }
            else
            {
                desiredPivot = _overviewPivot + _panOffset;
                desiredSize = GetBaseViewSize() * _zoomScale;
            }

            desiredSize = Mathf.Max(MinimumViewSize, desiredSize);
            _currentSize = desiredSize;

            var smoothing = Mathf.Max(0.1f, _settings.PositionSmoothing.Value);
            var blend = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
            _currentPivot = Vector3.Lerp(_currentPivot, desiredPivot, blend);

            var distance = Mathf.Max(200f,
                (_hasLevelBounds ? _levelBounds.extents.magnitude : desiredSize) * 3f + desiredSize * 2f);

            _camera.orthographic = true;
            _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, desiredSize, blend);
            _camera.nearClipPlane = 0.1f;
            var levelRadius = _hasLevelBounds ? _levelBounds.extents.magnitude : desiredSize;
            var requiredDrawDistance = Mathf.Max(2000f, distance + levelRadius * 2f + 500f);
            _camera.farClipPlane = requiredDrawDistance;
            ApplyRacerLayerCullDistances(requiredDrawDistance);
            _camera.transform.rotation = rotation;
            _camera.transform.position = _currentPivot - rotation * Vector3.forward * distance;
        }

        private void ApplyDynamicFollowShot(RacerSnapshot racer)
        {
            if (_camera == null || racer.Transform == null)
            {
                return;
            }

            UpdateMotionDirection(racer);

            var direction = _smoothedMotionDirection;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
            {
                direction = Vector3.forward;
            }
            direction.Normalize();

            var right = Vector3.Cross(Vector3.up, direction).normalized;
            var scale = Mathf.Clamp(_zoomScale, 0.4f, 2.5f);
            var focus = racer.Transform.position + Vector3.up * 1.4f + _panOffset;
            Vector3 desiredPosition;
            Vector3 lookAt;
            float desiredFieldOfView;

            switch (FollowStyle)
            {
                case FollowShotStyle.Lead:
                    desiredPosition =
                        focus + direction * (16f * scale) + right * (4f * scale) +
                        Vector3.up * (7f * scale);
                    lookAt = focus - direction * 2f;
                    desiredFieldOfView = 48f;
                    break;
                case FollowShotStyle.Trackside:
                    if (!_hasTracksideCameraPosition ||
                        Vector3.Distance(_tracksideCameraPosition, focus) > 34f * scale)
                    {
                        _tracksideCameraPosition =
                            focus + right * (15f * scale) + direction * (5f * scale) +
                            Vector3.up * (7f * scale);
                        _hasTracksideCameraPosition = true;
                    }

                    desiredPosition = _tracksideCameraPosition;
                    lookAt = focus + direction * 2f;
                    desiredFieldOfView = 44f;
                    break;
                default:
                    desiredPosition =
                        focus - direction * (12f * scale) + Vector3.up * (5f * scale);
                    lookAt = focus + direction * 5f;
                    desiredFieldOfView = 55f;
                    break;
            }

            var smoothing = Mathf.Max(0.1f, _settings.PositionSmoothing.Value);
            var blend = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
            var desiredRotation = Quaternion.LookRotation(lookAt - desiredPosition, Vector3.up);

            _currentPivot = Vector3.Lerp(_currentPivot, focus, blend);
            _camera.orthographic = false;
            _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, desiredFieldOfView, blend);
            _camera.nearClipPlane = 0.1f;
            var requiredDrawDistance = Mathf.Max(
                2000f,
                (_hasLevelBounds ? _levelBounds.extents.magnitude * 2f : 500f) + 500f);
            _camera.farClipPlane = requiredDrawDistance;
            ApplyRacerLayerCullDistances(requiredDrawDistance);
            _camera.transform.position = Vector3.Lerp(
                _camera.transform.position,
                desiredPosition,
                blend);
            _camera.transform.rotation = Quaternion.Slerp(
                _camera.transform.rotation,
                desiredRotation,
                blend);
        }

        private void UpdateMotionDirection(RacerSnapshot racer)
        {
            var position = racer.Transform.position;
            if (_motionSteamId != racer.SteamId)
            {
                _motionSteamId = racer.SteamId;
                _lastMotionPosition = position;
                _hasMotionSample = false;
                var initialDirection = Vector3.ProjectOnPlane(racer.Transform.forward, Vector3.up);
                _smoothedMotionDirection = initialDirection.sqrMagnitude > 0.001f
                    ? initialDirection.normalized
                    : Vector3.forward;
                _hasTracksideCameraPosition = false;
                return;
            }

            var displacement = Vector3.ProjectOnPlane(position - _lastMotionPosition, Vector3.up);
            _lastMotionPosition = position;
            if (displacement.sqrMagnitude < 0.0001f || displacement.sqrMagnitude > 2500f)
            {
                return;
            }

            var measuredDirection = displacement.normalized;
            var directionBlend = _hasMotionSample
                ? 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime)
                : 1f;
            _smoothedMotionDirection = Vector3.Slerp(
                _smoothedMotionDirection,
                measuredDirection,
                directionBlend).normalized;
            _hasMotionSample = true;
        }

        public void SelectRacer(ulong steamId)
        {
            var racer = _racers.FirstOrDefault(item => item.SteamId == steamId);
            if (racer == null)
            {
                return;
            }

            _selectedSteamId = steamId;
            _panOffset = Vector3.zero;
            CameraMode = BroadcastCameraMode.Follow;
            _zoomScale = 1f;
            _currentSize = GetBaseViewSize();
            ResetFollowTracking();

            if (_flyingCamera != null)
            {
                _flyingCamera.currentTarget = new SpectatorZeepkistTarget
                {
                    name = racer.Name,
                    transform = racer.Transform,
                    ghost = racer.Ghost
                };
            }
        }

        public bool TryGetSelected(out RacerSnapshot racer)
        {
            racer = _racers.FirstOrDefault(item => item.SteamId == _selectedSteamId)!;
            return racer != null;
        }

        private void TryActivate()
        {
            if (SceneManager.GetActiveScene().name != GameSceneName)
            {
                Notify("ZeepCast is available while a level is running.");
                return;
            }

            _master = FindObjectOfType<GameMaster>();
            if (_master == null || _master.flyingCamera == null)
            {
                Notify("ZeepCast could not find Zeepkist's spectator camera.");
                return;
            }

            _enteredPhotoMode = false;
            if (!_master.flyingCamera.isPhotoMode)
            {
                if (!_settings.AutoEnterPhotoMode.Value)
                {
                    Notify("Enter Zeepkist photo mode first, then press the ZeepCast hotkey.");
                    return;
                }

                if (!_master.flyingCamera.CanEnablePhotoMode())
                {
                    Notify("This lobby currently prevents entering photo mode.");
                    return;
                }

                _master.flyingCamera.ToggleFlyingCamera();
                _enteredPhotoMode = true;
            }

            _flyingCamera = FindSceneFlyingCamera();
            _camera = _flyingCamera != null ? _flyingCamera.GetTheCamera() : null;
            if (_flyingCamera == null || _camera == null)
            {
                if (_enteredPhotoMode && _master.flyingCamera.isPhotoMode)
                {
                    _master.flyingCamera.ToggleFlyingCamera();
                    _enteredPhotoMode = false;
                }

                Notify("ZeepCast could not acquire the active flying camera.");
                return;
            }

            if (_flyingCamera.currentTarget != null &&
                _flyingCamera.currentTarget.ghost != null)
            {
                _selectedSteamId = _flyingCamera.currentTarget.ghost.player.SteamID;
            }

            AcquireCameraOwnership();

            _spectatorUi = _master.OnlineGameplayUI != null
                ? _master.OnlineGameplayUI.spectatorUI
                : null;
            _pauseMenuUi = FindScenePauseMenu();
            _spectatorUiWasOpen = _spectatorUi != null && _spectatorUi.IsOpen;
            if (_spectatorUiWasOpen)
            {
                _spectatorUi!.Close(announce: false);
            }

            _cursorLockMode = Cursor.lockState;
            _cursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            _pitch = Mathf.Clamp(_settings.Pitch.Value, 12f, 88f);
            _yaw = _settings.Yaw.Value;
            _panOffset = Vector3.zero;
            CameraMode = BroadcastCameraMode.Overview;
            _zoomScale = 1f;
            _directorConsoleVisible = _settings.StartWithDirectorConsole.Value;
            _markersVisible = true;
            _helpVisible = false;

            ReadLevelTitle();
            CalculateLevelBounds();
            RefreshRacers();

            _currentPivot = _overviewPivot;
            _currentSize = _overviewSize;
            _active = true;
            _visualizationWasActive = true;
            _hudAutoHiddenOutsideVisualization = false;
            _solidRacerPresentation?.Begin();
            _selectedRacerVisibility?.Begin(_camera);
            SetInterfaceVisible(true);
            SetDirectorConsoleVisible(_directorConsoleVisible);
            SetMarkersVisible(_markersVisible);
            SetHelpVisible(false);

            ZeepCastPlugin.Log.LogInfo(
                $"Director activated with {_racers.Count} racers. Bounds: center={_levelBounds.center}, size={_levelBounds.size}");
            Notify("ZeepCast spectator active — ` clean feed, F9 graphics, V changes shot.");
        }

        private void Deactivate(bool restoreCamera)
        {
            if (!_active && _camera == null)
            {
                return;
            }

            var shouldExitOwnedPhotoMode =
                restoreCamera &&
                _enteredPhotoMode &&
                _master != null &&
                _master.flyingCamera != null &&
                _master.flyingCamera.isPhotoMode;
            var photoModeStillActive =
                _master != null &&
                _master.flyingCamera != null &&
                _master.flyingCamera.isPhotoMode;

            _active = false;
            _hud?.SetVisible(false);
            _solidRacerPresentation?.Restore();
            _selectedRacerVisibility?.Restore();
            _nativeSpectatorGraphics.Restore();
            ReleaseCameraOwnership(restoreCamera);

            // Restore the stock spectator surface only when ZeepCast borrowed
            // an already-active photo-mode session. Reopening it after Back to
            // Race produces a stranded free-camera overlay over race chrome.
            if (_spectatorUi != null &&
                _spectatorUiWasOpen &&
                photoModeStillActive &&
                !shouldExitOwnedPhotoMode &&
                !_spectatorUi.IsOpen)
            {
                _spectatorUi.Open(announce: false);
            }

            Cursor.lockState = _cursorLockMode;
            Cursor.visible = _cursorVisible;

            if (shouldExitOwnedPhotoMode)
            {
                try
                {
                    _master!.flyingCamera!.ToggleFlyingCamera();
                    Notify("ZeepCast returned control to the race.");
                }
                catch (Exception exception)
                {
                    ZeepCastPlugin.Log.LogWarning(
                        $"Could not leave Zeepkist photo mode cleanly: {exception.Message}");
                }
            }

            _camera = null;
            _flyingCamera = null;
            _spectatorUi = null;
            _pauseMenuUi = null;
            _master = null;
            _enteredPhotoMode = false;
            _visualizationWasActive = false;
            _hudAutoHiddenOutsideVisualization = false;
            _helpVisible = false;
            ResetFollowTracking();
        }

        private void UpdateVisualizationState()
        {
            var visualizationActive = IsVisualizationActive;
            var photoModeActive =
                _master != null &&
                _master.flyingCamera != null &&
                _master.flyingCamera.isPhotoMode;

            // Once Zeepkist has completed its own Back to Race transition,
            // ZeepCast no longer owns that photo-mode session. Keeping the
            // director armed still permits an explicit F9 graphics overlay.
            if (!photoModeActive)
            {
                _enteredPhotoMode = false;
            }

            if (visualizationActive == _visualizationWasActive)
            {
                return;
            }

            _visualizationWasActive = visualizationActive;
            if (!visualizationActive)
            {
                _solidRacerPresentation?.Restore();
                _selectedRacerVisibility?.Restore();
                _nativeSpectatorGraphics.Restore();
                ReleaseCameraOwnership(restoreCamera: photoModeActive);
                if (_uiVisible)
                {
                    _hudAutoHiddenOutsideVisualization = true;
                    SetInterfaceVisible(false);
                }

                return;
            }

            AcquireCameraOwnership();
            _solidRacerPresentation?.Begin();
            if (_camera != null)
            {
                _selectedRacerVisibility?.Begin(_camera);
            }
            if (_hudAutoHiddenOutsideVisualization)
            {
                _hudAutoHiddenOutsideVisualization = false;
                SetInterfaceVisible(true);
            }
        }

        private void AcquireCameraOwnership()
        {
            if (_cameraOwned || _camera == null || _flyingCamera == null)
            {
                return;
            }

            CaptureCameraState();
            _flyingCameraWasEnabled = _flyingCamera.enabled;
            _flyingCamera.enabled = false;
            _cameraOwned = true;
        }

        private void ReleaseCameraOwnership(bool restoreCamera)
        {
            if (!_cameraOwned)
            {
                return;
            }

            if (restoreCamera)
            {
                RestoreCameraState();
            }

            if (_flyingCamera != null)
            {
                _flyingCamera.enabled = _flyingCameraWasEnabled;
            }

            _cameraOwned = false;
        }

        private void SetInterfaceVisible(bool visible)
        {
            _uiVisible = visible;
            _hud?.SetVisible(visible);

            if (visible && IsVisualizationActive)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = _directorConsoleVisible;
            }
            else if (!IsVisualizationActive)
            {
                Cursor.lockState = _cursorLockMode;
                Cursor.visible = _cursorVisible;
            }
            else
            {
                Cursor.visible = false;
            }
        }

        private void SetDirectorConsoleVisible(bool visible)
        {
            _directorConsoleVisible = visible;
            _hud?.SetDirectorConsoleVisible(visible);

            if (IsVisualizationActive && _uiVisible)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = visible;
            }
        }

        private void SetMarkersVisible(bool visible)
        {
            _markersVisible = visible;
            _hud?.SetMarkersVisible(visible);
        }

        private void SetHelpVisible(bool visible)
        {
            _helpVisible = visible;
            _hud?.SetHelpVisible(visible);
        }

        private void CaptureCameraState()
        {
            if (_camera == null)
            {
                return;
            }

            _cameraWasOrthographic = _camera.orthographic;
            _cameraOrthographicSize = _camera.orthographicSize;
            _cameraFieldOfView = _camera.fieldOfView;
            _cameraNearClip = _camera.nearClipPlane;
            _cameraFarClip = _camera.farClipPlane;
            _cameraLayerCullSpherical = _camera.layerCullSpherical;
            _cameraLayerCullDistances = (float[])_camera.layerCullDistances.Clone();
            _cameraPosition = _camera.transform.position;
            _cameraRotation = _camera.transform.rotation;
        }

        private void RestoreCameraState()
        {
            if (_camera == null)
            {
                return;
            }

            _camera.orthographic = _cameraWasOrthographic;
            _camera.orthographicSize = _cameraOrthographicSize;
            _camera.fieldOfView = _cameraFieldOfView;
            _camera.nearClipPlane = _cameraNearClip;
            _camera.farClipPlane = _cameraFarClip;
            _camera.layerCullSpherical = _cameraLayerCullSpherical;
            _camera.layerCullDistances = _cameraLayerCullDistances;
            _camera.transform.position = _cameraPosition;
            _camera.transform.rotation = _cameraRotation;
        }

        private void ApplyRacerLayerCullDistances(float requiredDrawDistance)
        {
            if (_camera == null)
            {
                return;
            }

            var distances = _cameraLayerCullDistances.Length == 32
                ? (float[])_cameraLayerCullDistances.Clone()
                : new float[32];

            foreach (var racer in _racers)
            {
                if (racer.Ghost == null)
                {
                    continue;
                }

                RaiseCullDistanceForHierarchy(racer.Ghost.ghostModel, distances, requiredDrawDistance);
                RaiseCullDistanceForHierarchy(racer.Ghost.cameraManModel, distances, requiredDrawDistance);
            }

            // CameraCullSphere uses planar layer distances, so retain that
            // behavior while extending only layers occupied by racer models.
            _camera.layerCullSpherical = false;
            _camera.layerCullDistances = distances;
        }

        private static void RaiseCullDistanceForHierarchy(
            Component root,
            float[] distances,
            float requiredDrawDistance)
        {
            if (root == null)
            {
                return;
            }

            var renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
            foreach (var renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                var layer = renderer.gameObject.layer;
                if (layer < 0 || layer >= distances.Length)
                {
                    continue;
                }

                distances[layer] = Mathf.Max(distances[layer], requiredDrawDistance);
            }
        }

        private FlyingCameraScript? FindSceneFlyingCamera()
        {
            var activeScene = SceneManager.GetActiveScene();
            foreach (var candidate in Resources.FindObjectsOfTypeAll<FlyingCameraScript>())
            {
                if (candidate != null &&
                    candidate.gameObject.scene.IsValid() &&
                    candidate.gameObject.scene == activeScene)
                {
                    return candidate;
                }
            }

            return null;
        }

        private PauseMenuUI? FindScenePauseMenu()
        {
            var activeScene = SceneManager.GetActiveScene();
            return Resources.FindObjectsOfTypeAll<PauseMenuUI>()
                .FirstOrDefault(candidate =>
                    candidate != null &&
                    candidate.gameObject.scene.IsValid() &&
                    candidate.gameObject.scene == activeScene);
        }

        private void RefreshRacers()
        {
            var previousSelection = _selectedSteamId;
            var previousSelectionIndex = _racers.FindIndex(item => item.SteamId == previousSelection);
            _racers.Clear();

            if (!ZeepkistNetwork.IsConnected)
            {
                _selectedSteamId = 0;
                _fieldSummary = default;
                _selectedRacerVisibility?.Clear();
                return;
            }

            foreach (var player in ZeepkistNetwork.PlayerList)
            {
                var ghost = player.Zeepkist;
                if (ghost == null || ghost.ghostModel == null)
                {
                    continue;
                }

                var currentResult = player.CurrentResult;
                var attempt = UpdateAttemptState(player, ghost);
                var statusKind = ReadStatus(ghost, attempt.Finished, out var status);
                var name = player.GetUserNameNoTag();
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = player.GetTaggedUsername();
                }

                _racers.Add(new RacerSnapshot(
                    player.SteamID,
                    0,
                    string.IsNullOrWhiteSpace(name) ? $"Racer {player.UID}" : name,
                    ColorForPlayer(player.SteamID),
                    player,
                    ghost,
                    ghost.ghostModel.transform,
                    attempt.Finished ? currentResult?.Checkpoints ?? 0 : 0,
                    Mathf.Max(0f, ghost.displayRuntime),
                    Math.Abs((int)ghost.displayVelocity),
                    player.ChampionshipPoints.x,
                    attempt.Finished,
                    attempt.Finished ? attempt.FinishTime : 0f,
                    statusKind,
                    status));
            }

            var activeIds = new HashSet<ulong>(_racers.Select(racer => racer.SteamId));
            foreach (var staleId in _attemptStates.Keys.Where(id => !activeIds.Contains(id)).ToArray())
            {
                _attemptStates.Remove(staleId);
            }

            _racers.Sort(CompareRacers);
            for (var i = 0; i < _racers.Count; i++)
            {
                _racers[i] = _racers[i].WithPosition(i + 1);
            }

            if (_racers.Any(item => item.SteamId == previousSelection))
            {
                _selectedSteamId = previousSelection;
            }
            else if (_racers.Count > 0)
            {
                var replacementIndex = previousSelectionIndex < 0
                    ? 0
                    : Mathf.Clamp(previousSelectionIndex, 0, _racers.Count - 1);
                _selectedSteamId = _racers[replacementIndex].SteamId;
            }
            else
            {
                _selectedSteamId = 0;
            }

            _fieldSummary = BuildFieldSummary(_racers);
            SyncFlyingCameraTarget();
        }

        private RacerAttemptState UpdateAttemptState(
            ZeepkistNetworkPlayer player,
            NetworkedZeepkistGhost ghost)
        {
            if (!_attemptStates.TryGetValue(player.SteamID, out var state))
            {
                state = new RacerAttemptState();
                _attemptStates.Add(player.SteamID, state);
            }

            var result = player.CurrentResult;
            var hasResult = result != null && result.Time > 0f;
            var resultTime = hasResult ? result!.Time : 0f;
            var resultCheckpoints = hasResult ? result!.Checkpoints : 0;
            var runtime = Mathf.Max(0f, ghost.displayRuntime);
            var activelyDriving = !ghost.isDead && !ghost.isPhotoMode && runtime > 0.05f;

            if (!state.Initialized)
            {
                // CurrentResult survives resets. It describes the present attempt
                // only while the network runtime is still at that result, or the
                // player has entered the post-finish photo mode.
                state.Finished = hasResult &&
                    (ghost.isPhotoMode || Mathf.Abs(runtime - resultTime) <= 0.75f);
                state.FinishTime = state.Finished ? resultTime : 0f;
                state.LastDrivingRuntime = activelyDriving ? runtime : resultTime;
                state.Initialized = true;
            }
            else
            {
                var resultChanged = hasResult &&
                    (!state.HadResult ||
                     Mathf.Abs(resultTime - state.ResultTime) > 0.001f ||
                     resultCheckpoints != state.ResultCheckpoints);

                if (resultChanged)
                {
                    state.Finished = true;
                    state.FinishTime = resultTime;
                }

                // A result remains attached to the network player after resetting.
                // A restarted running clock is the reliable signal that a fresh
                // attempt has begun, including on very short tracks.
                var clockRestarted =
                    state.LastDrivingRuntime > 0.5f &&
                    runtime + 0.5f < state.LastDrivingRuntime;
                if (state.Finished &&
                    activelyDriving &&
                    (clockRestarted || runtime < state.FinishTime - 0.25f))
                {
                    state.Finished = false;
                    state.FinishTime = 0f;
                }

                if (!hasResult)
                {
                    state.Finished = false;
                    state.FinishTime = 0f;
                }

                if (activelyDriving)
                {
                    state.LastDrivingRuntime = runtime;
                }
            }

            state.HadResult = hasResult;
            state.ResultTime = resultTime;
            state.ResultCheckpoints = resultCheckpoints;
            return state;
        }

        private void SyncFlyingCameraTarget()
        {
            if (_flyingCamera == null)
            {
                return;
            }

            var racer = _racers.FirstOrDefault(item => item.SteamId == _selectedSteamId);
            if (racer == null ||
                (_flyingCamera.currentTarget != null &&
                 _flyingCamera.currentTarget.ghost == racer.Ghost))
            {
                return;
            }

            _flyingCamera.currentTarget = new SpectatorZeepkistTarget
            {
                name = racer.Name,
                transform = racer.Transform,
                ghost = racer.Ghost
            };
        }

        private static int CompareRacers(RacerSnapshot left, RacerSnapshot right)
        {
            if (left.Finished != right.Finished)
            {
                return left.Finished ? -1 : 1;
            }

            if (left.Finished)
            {
                return left.FinishTime.CompareTo(right.FinishTime);
            }

            var checkpointOrder = right.Checkpoints.CompareTo(left.Checkpoints);
            if (checkpointOrder != 0)
            {
                return checkpointOrder;
            }

            return left.Runtime.CompareTo(right.Runtime);
        }

        private static RacerStatusKind ReadStatus(
            NetworkedZeepkistGhost ghost,
            bool finished,
            out string label)
        {
            if (finished)
            {
                label = "FINISHED";
                return RacerStatusKind.Finished;
            }

            if (ghost.isDead)
            {
                label = "CRASHED";
                return RacerStatusKind.Crashed;
            }

            if (ghost.isPhotoMode)
            {
                label = "SPECTATING";
                return RacerStatusKind.Spectating;
            }

            var damagedWheels =
                (ghost.frontLeftDead ? 1 : 0) +
                (ghost.frontRightDead ? 1 : 0) +
                (ghost.rearLeftDead ? 1 : 0) +
                (ghost.rearRightDead ? 1 : 0);

            if (damagedWheels > 0)
            {
                label = $"{damagedWheels} WHEEL{(damagedWheels == 1 ? string.Empty : "S")} LOST";
                return RacerStatusKind.Damaged;
            }

            if (ghost.isBoosting)
            {
                label = "BOOST";
                return RacerStatusKind.Boosting;
            }

            if (ghost.isFanning)
            {
                label = "FAN";
                return RacerStatusKind.Fanning;
            }

            if (ghost.brake)
            {
                label = "BRAKE";
                return RacerStatusKind.Braking;
            }

            label = "RACING";
            return RacerStatusKind.Racing;
        }

        private static BroadcastFieldSummary BuildFieldSummary(IReadOnlyList<RacerSnapshot> racers)
        {
            var racing = 0;
            var finished = 0;
            var spectating = 0;
            var incidents = 0;

            foreach (var racer in racers)
            {
                switch (racer.StatusKind)
                {
                    case RacerStatusKind.Finished:
                        finished++;
                        break;
                    case RacerStatusKind.Spectating:
                        spectating++;
                        break;
                    case RacerStatusKind.Crashed:
                    case RacerStatusKind.Damaged:
                        incidents++;
                        break;
                    default:
                        racing++;
                        break;
                }
            }

            return new BroadcastFieldSummary(
                racers.Count,
                racing,
                finished,
                spectating,
                incidents);
        }

        private void ToggleCameraMode()
        {
            _panOffset = Vector3.zero;
            _zoomScale = 1f;
            switch (CameraMode)
            {
                case BroadcastCameraMode.Overview:
                    CameraMode = BroadcastCameraMode.Field;
                    break;
                case BroadcastCameraMode.Field:
                    CameraMode = TryGetSelected(out _)
                        ? BroadcastCameraMode.Follow
                        : BroadcastCameraMode.Overview;
                    break;
                default:
                    CameraMode = BroadcastCameraMode.Overview;
                    break;
            }

            _currentSize = GetBaseViewSize();
            ResetFollowTracking();
        }

        private void SelectFollowStyle(FollowShotStyle style)
        {
            if (!TryGetSelected(out _))
            {
                Notify("ZeepCast is waiting for a racer to follow.");
                return;
            }

            FollowStyle = style;
            CameraMode = BroadcastCameraMode.Follow;
            _panOffset = Vector3.zero;
            _zoomScale = 1f;
            _currentSize = GetBaseViewSize();
            ResetFollowTracking();
        }

        private void ResetFollowTracking()
        {
            _motionSteamId = 0;
            _hasMotionSample = false;
            _smoothedMotionDirection = Vector3.forward;
            _hasTracksideCameraPosition = false;
        }

        private void SelectRelative(int direction)
        {
            if (_racers.Count == 0)
            {
                return;
            }

            var index = _racers.FindIndex(item => item.SteamId == _selectedSteamId);
            if (index < 0)
            {
                index = 0;
            }

            index = (index + direction + _racers.Count) % _racers.Count;
            SelectRacer(_racers[index].SteamId);
        }

        private void HandleCameraInput()
        {
            if (_camera == null)
            {
                return;
            }

            var pointerOverUi =
                (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) ||
                (_hud != null && _hud.IsPointerOverRoster);
            var precision = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            var fast = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            var inputScale = precision ? 0.3f : fast ? 2f : 1f;

            if (!pointerOverUi)
            {
                var scroll = Input.mouseScrollDelta.y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    ZoomAtCursor(scroll * inputScale);
                }

                if (Input.GetMouseButton(1))
                {
                    _yaw += Input.GetAxis("Mouse X") * 2.5f * inputScale;
                    _pitch = Mathf.Clamp(
                        _pitch - Input.GetAxis("Mouse Y") * 2f * inputScale,
                        12f,
                        88f);
                }

                if (Input.GetMouseButton(2))
                {
                    PanWithMouse(inputScale);
                }
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                ResetCameraFraming();
            }

            var move = Vector2.zero;
            if (Input.GetKey(KeyCode.A)) move.x -= 1f;
            if (Input.GetKey(KeyCode.D)) move.x += 1f;
            if (Input.GetKey(KeyCode.S)) move.y -= 1f;
            if (Input.GetKey(KeyCode.W)) move.y += 1f;

            if (move.sqrMagnitude > 0.01f)
            {
                var rotation = Quaternion.Euler(0f, _yaw, 0f);
                var speed =
                    Mathf.Max(0.75f, _currentSize * 0.9f) *
                    inputScale *
                    Time.unscaledDeltaTime;
                _panOffset +=
                    (rotation * Vector3.right * move.x +
                     rotation * Vector3.forward * move.y) *
                    speed;
            }
        }

        private void ZoomAtCursor(float wheelDelta)
        {
            if (_camera == null)
            {
                return;
            }

            if (!_camera.orthographic)
            {
                _zoomScale = Mathf.Clamp(
                    _zoomScale * Mathf.Pow(0.86f, wheelDelta),
                    0.4f,
                    2.5f);
                _hasTracksideCameraPosition = false;
                return;
            }

            var previousSize = _currentSize;
            var nextSize = Mathf.Clamp(
                previousSize * Mathf.Pow(0.8f, wheelDelta),
                MinimumViewSize,
                Mathf.Max(200f, _overviewSize * 6f));
            if (Mathf.Approximately(previousSize, nextSize))
            {
                return;
            }

            // For an orthographic camera, scaling the cursor's offset from the
            // pivot produces cursor-anchored zoom without an extra raycast after
            // the camera's smoothed size catches up.
            var pivotPlane = new Plane(Vector3.up, _currentPivot);
            var ray = _camera.ScreenPointToRay(Input.mousePosition);
            if (pivotPlane.Raycast(ray, out var distance))
            {
                var cursorWorld = ray.GetPoint(distance);
                var ratio = nextSize / Mathf.Max(MinimumViewSize, previousSize);
                var pivotShift = (cursorWorld - _currentPivot) * (1f - ratio);
                pivotShift.y = 0f;
                _panOffset += pivotShift;
            }

            _zoomScale = nextSize / Mathf.Max(MinimumViewSize, GetBaseViewSize());
            _currentSize = nextSize;
        }

        private void PanWithMouse(float inputScale)
        {
            if (_camera == null || Screen.height <= 0)
            {
                return;
            }

            var delta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
            if (delta.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var right = Vector3.ProjectOnPlane(_camera.transform.right, Vector3.up).normalized;
            var screenUp = Vector3.ProjectOnPlane(_camera.transform.up, Vector3.up).normalized;
            var verticalSpan = _camera.orthographic
                ? 2f * Mathf.Max(MinimumViewSize, _camera.orthographicSize)
                : 2f * Mathf.Max(1f, Vector3.Distance(_camera.transform.position, _currentPivot)) *
                  Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var worldPerPixel = verticalSpan / Screen.height;
            _panOffset -=
                (right * delta.x + screenUp * delta.y) *
                worldPerPixel *
                inputScale;
        }

        private void ResetCameraFraming()
        {
            _panOffset = Vector3.zero;
            _yaw = _settings.Yaw.Value;
            _pitch = Mathf.Clamp(_settings.Pitch.Value, 12f, 88f);
            _zoomScale = 1f;
            _currentSize = GetBaseViewSize();
            ResetFollowTracking();
        }

        private static bool IsPressed(BepInEx.Configuration.ConfigEntry<KeyCode> setting)
        {
            return setting.Value != KeyCode.None && Input.GetKeyDown(setting.Value);
        }

        private float GetBaseViewSize()
        {
            switch (CameraMode)
            {
                case BroadcastCameraMode.Field:
                    return Mathf.Max(MinimumViewSize, _fieldSize);
                case BroadcastCameraMode.Follow:
                    return Mathf.Max(MinimumViewSize, _settings.FollowSize.Value);
                default:
                    return Mathf.Max(MinimumViewSize, _overviewSize);
            }
        }

        private void UpdateFieldFraming(Quaternion rotation)
        {
            var hasField = false;
            var bounds = new Bounds();

            foreach (var racer in _racers)
            {
                if (racer.Transform == null ||
                    racer.StatusKind == RacerStatusKind.Spectating ||
                    racer.StatusKind == RacerStatusKind.Finished ||
                    racer.StatusKind == RacerStatusKind.Crashed)
                {
                    continue;
                }

                if (!hasField)
                {
                    bounds = new Bounds(racer.Transform.position, Vector3.zero);
                    hasField = true;
                }
                else
                {
                    bounds.Encapsulate(racer.Transform.position);
                }
            }

            if (!hasField)
            {
                foreach (var racer in _racers)
                {
                    if (racer.Transform == null)
                    {
                        continue;
                    }

                    if (!hasField)
                    {
                        bounds = new Bounds(racer.Transform.position, Vector3.zero);
                        hasField = true;
                    }
                    else
                    {
                        bounds.Encapsulate(racer.Transform.position);
                    }
                }
            }

            if (!hasField)
            {
                _fieldPivot = _overviewPivot;
                _fieldSize = _overviewSize;
                return;
            }

            bounds.Expand(new Vector3(12f, 10f, 12f));
            _fieldPivot = bounds.center;
            _fieldSize = CalculateViewSize(
                bounds,
                rotation,
                Mathf.Max(1f, _settings.FieldPadding.Value),
                8f);
        }

        private void CalculateLevelBounds()
        {
            _hasLevelBounds = false;
            _levelBounds = new Bounds(Vector3.zero, Vector3.zero);

            if (_master != null)
            {
                foreach (var block in _master.allBlockPropertiesInThisLevel)
                {
                    if (block == null)
                    {
                        continue;
                    }

                    var half = block.boundingBoxSize * 0.5f;
                    for (var x = -1; x <= 1; x += 2)
                    {
                        for (var y = -1; y <= 1; y += 2)
                        {
                            for (var z = -1; z <= 1; z += 2)
                            {
                                var local = block.boundingBoxOffset +
                                            Vector3.Scale(half, new Vector3(x, y, z));
                                Encapsulate(block.transform.TransformPoint(local));
                            }
                        }
                    }
                }
            }

            if (!_hasLevelBounds)
            {
                foreach (var racer in _racers)
                {
                    Encapsulate(racer.Transform.position);
                }
            }

            if (!_hasLevelBounds)
            {
                _levelBounds = new Bounds(Vector3.zero, new Vector3(100f, 30f, 100f));
                _hasLevelBounds = true;
            }

            _overviewPivot = _levelBounds.center;
            _overviewSize = CalculateViewSize(
                _levelBounds,
                Quaternion.Euler(_pitch, _yaw, 0f),
                Mathf.Max(1f, _settings.OverviewPadding.Value),
                20f);
            _fieldPivot = _overviewPivot;
            _fieldSize = Mathf.Min(_overviewSize, Mathf.Max(8f, _settings.FollowSize.Value * 1.5f));
        }

        private void Encapsulate(Vector3 point)
        {
            if (!IsFinite(point))
            {
                return;
            }

            if (!_hasLevelBounds)
            {
                _levelBounds = new Bounds(point, Vector3.zero);
                _hasLevelBounds = true;
            }
            else
            {
                _levelBounds.Encapsulate(point);
            }
        }

        private float CalculateViewSize(
            Bounds bounds,
            Quaternion rotation,
            float padding,
            float minimum)
        {
            var inverse = Quaternion.Inverse(rotation);
            var maxX = 0f;
            var maxY = 0f;

            for (var x = -1; x <= 1; x += 2)
            {
                for (var y = -1; y <= 1; y += 2)
                {
                    for (var z = -1; z <= 1; z += 2)
                    {
                        var corner = bounds.center + Vector3.Scale(
                            bounds.extents,
                            new Vector3(x, y, z));
                        var cameraSpace = inverse * (corner - bounds.center);
                        maxX = Mathf.Max(maxX, Mathf.Abs(cameraSpace.x));
                        maxY = Mathf.Max(maxY, Mathf.Abs(cameraSpace.y));
                    }
                }
            }

            var aspect = _camera != null && _camera.aspect > 0.01f
                ? _camera.aspect
                : 16f / 9f;
            var size = Mathf.Max(maxY, maxX / aspect);
            return Mathf.Max(minimum, size * padding);
        }

        private void ReadLevelTitle()
        {
            var setup = FindObjectOfType<SetupGame>();
            if (setup != null && setup.GlobalLevel != null)
            {
                _levelTitle = SanitizeLegacyText(setup.GlobalLevel.GetLevelByAuthorString());
            }
        }

        private static string SanitizeLegacyText(string value)
        {
            // LevelScriptableObject formats names for TextMeshPro. ZeepCast's
            // lightweight overlay uses legacy uGUI Text, where noparse is not a
            // recognized tag and would be printed literally.
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("<noparse>", string.Empty)
                    .Replace("</noparse>", string.Empty);
        }

        private static Color ColorForPlayer(ulong steamId)
        {
            var hash = unchecked((uint)(steamId ^ (steamId >> 32)));
            var hue = (hash % 997u) / 997f;
            return Color.HSVToRGB(hue, 0.68f, 1f);
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private static void Notify(string message)
        {
            ZeepCastPlugin.Log.LogInfo(message);
            try
            {
                if (PlayerManager.Instance != null && PlayerManager.Instance.messenger != null)
                {
                    PlayerManager.Instance.messenger.Log(message, 5f);
                }
            }
            catch (Exception exception)
            {
                ZeepCastPlugin.Log.LogDebug($"Could not display in-game notice: {exception.Message}");
            }
        }
    }
}
