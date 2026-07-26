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
        Follow
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
        private bool _flyingCameraWasEnabled;
        private bool _spectatorUiWasOpen;
        private bool _enteredPhotoMode;
        private bool _visualizationWasActive;
        private bool _hudAutoHiddenOutsideVisualization;

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
        private Vector3 _currentPivot;
        private Vector3 _panOffset;
        private float _overviewSize = 80f;
        private float _currentSize = 45f;
        private float _yaw;
        private float _pitch;
        private float _nextRosterRefresh;
        private ulong _selectedSteamId;
        private string _levelTitle = "Unknown level";

        public bool IsActive => _active;
        public bool IsUiVisible => _uiVisible;
        public bool IsVisualizationActive =>
            _active &&
            _master != null &&
            _master.flyingCamera != null &&
            _master.flyingCamera.isPhotoMode &&
            _camera != null &&
            _camera.gameObject.activeInHierarchy &&
            (_pauseMenuUi == null || !_pauseMenuUi.IsOpen);
        public BroadcastCameraMode CameraMode { get; private set; } = BroadcastCameraMode.Overview;
        public IReadOnlyList<RacerSnapshot> Racers => _racers;
        public ulong SelectedSteamId => _selectedSteamId;
        public Camera? BroadcastCamera => _camera;
        public string EventTitle => _settings.EventTitle.Value;
        public string LevelTitle => _levelTitle;

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

            if (_settings.ToggleDirector.Value.IsDown())
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

            if (_settings.ToggleInterface.Value.IsDown())
            {
                _hudAutoHiddenOutsideVisualization = false;
                SetInterfaceVisible(!_uiVisible);
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

            if (_settings.ToggleCameraMode.Value.IsDown())
            {
                ToggleCameraMode();
            }

            if (_settings.PreviousRacer.Value.IsDown())
            {
                SelectRelative(-1);
            }

            if (_settings.NextRacer.Value.IsDown())
            {
                SelectRelative(1);
            }

            HandleCameraInput();
        }

        private void LateUpdate()
        {
            if (!_active || !IsVisualizationActive)
            {
                return;
            }

            _solidRacerPresentation?.Apply(_racers);

            if (_camera == null)
            {
                return;
            }

            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 desiredPivot;
            float desiredSize;

            if (CameraMode == BroadcastCameraMode.Follow && TryGetSelected(out var selected))
            {
                desiredPivot = selected.Transform.position + _panOffset;
                desiredSize = Mathf.Max(MinimumViewSize, _currentSize);
            }
            else
            {
                desiredPivot = _overviewPivot + _panOffset;
                desiredSize = Mathf.Max(MinimumViewSize, _currentSize);
            }

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
            _currentSize = Mathf.Max(MinimumViewSize, _settings.FollowSize.Value);

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
                Notify("ZeepCast could not acquire the active flying camera.");
                return;
            }

            CaptureCameraState();
            _flyingCameraWasEnabled = _flyingCamera.enabled;
            if (_flyingCamera.currentTarget != null &&
                _flyingCamera.currentTarget.ghost != null)
            {
                _selectedSteamId = _flyingCamera.currentTarget.ghost.player.SteamID;
            }

            _flyingCamera.enabled = false;

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

            ReadLevelTitle();
            CalculateLevelBounds();
            RefreshRacers();

            _currentPivot = _overviewPivot;
            _currentSize = _overviewSize;
            _active = true;
            _visualizationWasActive = true;
            _hudAutoHiddenOutsideVisualization = false;
            _solidRacerPresentation?.Begin();
            SetInterfaceVisible(true);

            ZeepCastPlugin.Log.LogInfo(
                $"Director activated with {_racers.Count} racers. Bounds: center={_levelBounds.center}, size={_levelBounds.size}");
            Notify("ZeepCast director active — F9 hides the interface, Tab changes camera mode.");
        }

        private void Deactivate(bool restoreCamera)
        {
            if (!_active && _camera == null)
            {
                return;
            }

            _active = false;
            _hud?.SetVisible(false);
            _solidRacerPresentation?.Restore();

            if (restoreCamera && _camera != null)
            {
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

            if (_flyingCamera != null)
            {
                _flyingCamera.enabled = _flyingCameraWasEnabled;
            }

            if (_spectatorUi != null && _spectatorUiWasOpen && !_spectatorUi.IsOpen)
            {
                _spectatorUi.Open(announce: false);
            }

            Cursor.lockState = _cursorLockMode;
            Cursor.visible = _cursorVisible;

            if (_enteredPhotoMode)
            {
                Notify("ZeepCast released the camera; Zeepkist photo mode remains active.");
            }

            _camera = null;
            _flyingCamera = null;
            _spectatorUi = null;
            _pauseMenuUi = null;
            _master = null;
            _enteredPhotoMode = false;
            _visualizationWasActive = false;
            _hudAutoHiddenOutsideVisualization = false;
        }

        private void UpdateVisualizationState()
        {
            var visualizationActive = IsVisualizationActive;
            if (visualizationActive == _visualizationWasActive)
            {
                return;
            }

            _visualizationWasActive = visualizationActive;
            if (!visualizationActive)
            {
                _solidRacerPresentation?.Restore();
                if (_uiVisible)
                {
                    _hudAutoHiddenOutsideVisualization = true;
                    SetInterfaceVisible(false);
                }

                return;
            }

            _solidRacerPresentation?.Begin();
            if (_hudAutoHiddenOutsideVisualization)
            {
                _hudAutoHiddenOutsideVisualization = false;
                SetInterfaceVisible(true);
            }
        }

        private void SetInterfaceVisible(bool visible)
        {
            _uiVisible = visible;
            _hud?.SetVisible(visible);

            if (visible)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
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
            _racers.Clear();

            if (!ZeepkistNetwork.IsConnected)
            {
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
                var name = player.GetUserNameNoTag();
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = player.GetTaggedUsername();
                }

                _racers.Add(new RacerSnapshot
                {
                    SteamId = player.SteamID,
                    Name = string.IsNullOrWhiteSpace(name) ? $"Racer {player.UID}" : name,
                    Color = ColorForPlayer(player.SteamID),
                    Player = player,
                    Ghost = ghost,
                    Transform = ghost.ghostModel.transform,
                    Checkpoints = attempt.Finished ? currentResult?.Checkpoints ?? 0 : 0,
                    Runtime = Mathf.Max(0f, ghost.displayRuntime),
                    Speed = Math.Abs((int)ghost.displayVelocity),
                    Finished = attempt.Finished,
                    FinishTime = attempt.Finished ? attempt.FinishTime : 0f,
                    Status = BuildStatus(ghost, attempt.Finished)
                });
            }

            var activeIds = new HashSet<ulong>(_racers.Select(racer => racer.SteamId));
            foreach (var staleId in _attemptStates.Keys.Where(id => !activeIds.Contains(id)).ToArray())
            {
                _attemptStates.Remove(staleId);
            }

            _racers.Sort(CompareRacers);
            for (var i = 0; i < _racers.Count; i++)
            {
                _racers[i].Position = i + 1;
            }

            if (_racers.Any(item => item.SteamId == previousSelection))
            {
                _selectedSteamId = previousSelection;
            }
            else if (_racers.Count > 0)
            {
                _selectedSteamId = _racers[0].SteamId;
            }
            else
            {
                _selectedSteamId = 0;
            }

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

        private static string BuildStatus(NetworkedZeepkistGhost ghost, bool finished)
        {
            if (finished)
            {
                return "FINISHED";
            }

            if (ghost.isDead)
            {
                return "CRASHED";
            }

            if (ghost.isPhotoMode)
            {
                return "SPECTATING";
            }

            var damagedWheels =
                (ghost.frontLeftDead ? 1 : 0) +
                (ghost.frontRightDead ? 1 : 0) +
                (ghost.rearLeftDead ? 1 : 0) +
                (ghost.rearRightDead ? 1 : 0);

            if (damagedWheels > 0)
            {
                return $"{damagedWheels} WHEEL{(damagedWheels == 1 ? string.Empty : "S")} LOST";
            }

            if (ghost.isBoosting)
            {
                return "BOOST";
            }

            if (ghost.isFanning)
            {
                return "FAN";
            }

            if (ghost.brake)
            {
                return "BRAKE";
            }

            return "RACING";
        }

        private void ToggleCameraMode()
        {
            _panOffset = Vector3.zero;
            if (CameraMode == BroadcastCameraMode.Overview)
            {
                CameraMode = BroadcastCameraMode.Follow;
                _currentSize = Mathf.Max(MinimumViewSize, _settings.FollowSize.Value);
            }
            else
            {
                CameraMode = BroadcastCameraMode.Overview;
                _currentSize = _overviewSize;
            }
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
            var worldPerPixel = 2f * Mathf.Max(MinimumViewSize, _camera.orthographicSize) /
                                Screen.height;
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
            _currentSize = CameraMode == BroadcastCameraMode.Overview
                ? _overviewSize
                : Mathf.Max(MinimumViewSize, _settings.FollowSize.Value);
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
            _overviewSize = CalculateOverviewSize(_levelBounds, Quaternion.Euler(_pitch, _yaw, 0f));
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

        private float CalculateOverviewSize(Bounds bounds, Quaternion rotation)
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
            return Mathf.Max(20f, size * Mathf.Max(1f, _settings.OverviewPadding.Value));
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
