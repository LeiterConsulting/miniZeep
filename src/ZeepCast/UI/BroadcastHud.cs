using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using ZeepCast.Core;

namespace ZeepCast.UI
{
    internal sealed class BroadcastHud : MonoBehaviour
    {
        private const float RefreshInterval = 0.1f;

        private sealed class RacerTile
        {
            public RectTransform Root = null!;
            public Image Background = null!;
            public Image ColorBar = null!;
            public Text Position = null!;
            public Text Name = null!;
            public Text Telemetry = null!;
            public Text Status = null!;
        }

        private sealed class RacerMarker
        {
            public RectTransform Root = null!;
            public Image Background = null!;
            public Text Label = null!;
        }

        private BroadcastDirector _director = null!;
        private RectTransform _canvas = null!;
        private RectTransform _markerLayer = null!;
        private RectTransform _rosterContent = null!;
        private RectTransform _rosterViewport = null!;
        private ScrollRect _rosterScroll = null!;
        private Text _eventTitle = null!;
        private Text _levelTitle = null!;
        private Text _clock = null!;
        private Text _mode = null!;
        private Text _rosterHeader = null!;
        private Text _selectedName = null!;
        private Text _selectedStats = null!;
        private Text _selectedStatus = null!;
        private float _nextRefresh;
        private bool _initialized;
        private bool _visible;

        private readonly Dictionary<ulong, RacerTile> _tiles = new();
        private readonly Dictionary<ulong, RacerMarker> _markers = new();

        public bool IsPointerOverRoster =>
            _visible &&
            _rosterViewport != null &&
            RectTransformUtility.RectangleContainsScreenPoint(
                _rosterViewport,
                Input.mousePosition);

        public void Initialize(BroadcastDirector director)
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            _director = director;
            Build();
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (_canvas != null)
            {
                Destroy(_canvas.gameObject);
            }
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (visible)
            {
                // GameScene normally supplies this. The fallback is deliberately
                // delayed until activation so it cannot compete with menu UI setup.
                UiFactory.EnsureEventSystem();
            }

            if (_canvas != null)
            {
                _canvas.gameObject.SetActive(visible);
            }
        }

        private void Update()
        {
            if (!_initialized || !_visible || !_director.IsActive)
            {
                return;
            }

            if (Time.unscaledTime >= _nextRefresh)
            {
                RefreshChrome();
                ReconcileRacers();
                _nextRefresh = Time.unscaledTime + RefreshInterval;
            }

            HandleRosterScrolling();
            UpdateMarkerPositions();
        }

        private void Build()
        {
            _canvas = UiFactory.CreateCanvas("[ZeepCast] Broadcast Canvas", 6500);
            DontDestroyOnLoad(_canvas.gameObject);

            BuildMarkerLayer();
            BuildTopBar();
            BuildRoster();
            BuildSelectedCard();
            BuildHelp();
        }

        private void BuildMarkerLayer()
        {
            var go = new GameObject("WorldMarkers", typeof(RectTransform));
            go.transform.SetParent(_canvas, false);
            _markerLayer = go.GetComponent<RectTransform>();
            UiFactory.Stretch(_markerLayer, 0f, 0f, 0f, 0f);
        }

        private void BuildTopBar()
        {
            var bar = UiFactory.CreatePanel(_canvas, "BroadcastHeader", UiFactory.Panel, false);
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.sizeDelta = new Vector2(0f, 76f);
            bar.anchoredPosition = Vector2.zero;

            var accent = UiFactory.CreatePanel(bar, "Accent", UiFactory.Accent, false);
            accent.anchorMin = new Vector2(0f, 0f);
            accent.anchorMax = new Vector2(1f, 0f);
            accent.pivot = new Vector2(0.5f, 0f);
            accent.sizeDelta = new Vector2(0f, 4f);
            accent.anchoredPosition = Vector2.zero;

            _eventTitle = UiFactory.CreateText(
                bar, "EventTitle", "ZEEPKIST LIVE", 24, FontStyle.Bold,
                TextAnchor.MiddleLeft, UiFactory.Ink);
            UiFactory.Anchor(
                _eventTitle.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(380f, 58f),
                new Vector2(24f, 3f));

            _levelTitle = UiFactory.CreateText(
                bar, "LevelTitle", string.Empty, 17, FontStyle.Normal,
                TextAnchor.MiddleCenter, UiFactory.MutedInk);
            _levelTitle.rectTransform.anchorMin = new Vector2(0.25f, 0f);
            _levelTitle.rectTransform.anchorMax = new Vector2(0.75f, 1f);
            _levelTitle.rectTransform.offsetMin = new Vector2(0f, 4f);
            _levelTitle.rectTransform.offsetMax = Vector2.zero;

            _clock = UiFactory.CreateText(
                bar, "Clock", "--:--", 28, FontStyle.Bold,
                TextAnchor.MiddleRight, UiFactory.Ink);
            UiFactory.Anchor(
                _clock.rectTransform,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(180f, 58f),
                new Vector2(-24f, 3f));

            _mode = UiFactory.CreateText(
                bar, "Mode", "OVERVIEW", 12, FontStyle.Bold,
                TextAnchor.LowerRight, UiFactory.Accent);
            UiFactory.Anchor(
                _mode.rectTransform,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(180f, 58f),
                new Vector2(-24f, 3f));
        }

        private void BuildRoster()
        {
            var panel = UiFactory.CreatePanel(_canvas, "RacerRoster", UiFactory.Panel, true);
            panel.anchorMin = new Vector2(0f, 0f);
            panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 0.5f);
            panel.sizeDelta = new Vector2(366f, -112f);
            panel.anchoredPosition = new Vector2(18f, -18f);

            _rosterHeader = UiFactory.CreateText(
                panel, "RosterHeader", "RACERS", 18, FontStyle.Bold,
                TextAnchor.MiddleLeft, UiFactory.Ink);
            _rosterHeader.rectTransform.anchorMin = new Vector2(0f, 1f);
            _rosterHeader.rectTransform.anchorMax = new Vector2(1f, 1f);
            _rosterHeader.rectTransform.pivot = new Vector2(0.5f, 1f);
            _rosterHeader.rectTransform.sizeDelta = new Vector2(-24f, 54f);
            _rosterHeader.rectTransform.anchoredPosition = new Vector2(0f, -8f);

            _rosterViewport = UiFactory.CreatePanel(panel, "Viewport", Color.clear, true);
            _rosterViewport.gameObject.AddComponent<RectMask2D>();
            _rosterViewport.anchorMin = Vector2.zero;
            _rosterViewport.anchorMax = Vector2.one;
            _rosterViewport.offsetMin = new Vector2(12f, 12f);
            _rosterViewport.offsetMax = new Vector2(-24f, -62f);

            var contentObject = new GameObject(
                "Content",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            contentObject.transform.SetParent(_rosterViewport, false);
            _rosterContent = contentObject.GetComponent<RectTransform>();
            _rosterContent.anchorMin = new Vector2(0f, 1f);
            _rosterContent.anchorMax = new Vector2(1f, 1f);
            _rosterContent.pivot = new Vector2(0.5f, 1f);
            _rosterContent.anchoredPosition = Vector2.zero;
            _rosterContent.sizeDelta = Vector2.zero;

            var layout = contentObject.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 7f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = contentObject.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollbarTrack = UiFactory.CreatePanel(
                panel,
                "Scrollbar",
                new Color(0.12f, 0.15f, 0.22f, 0.85f),
                true);
            scrollbarTrack.anchorMin = new Vector2(1f, 0f);
            scrollbarTrack.anchorMax = new Vector2(1f, 1f);
            scrollbarTrack.pivot = new Vector2(1f, 0.5f);
            scrollbarTrack.sizeDelta = new Vector2(7f, -86f);
            scrollbarTrack.anchoredPosition = new Vector2(-10f, -25f);

            var scrollbarHandle = UiFactory.CreatePanel(
                scrollbarTrack,
                "Handle",
                UiFactory.Accent,
                true);
            UiFactory.Stretch(scrollbarHandle, 0f, 0f, 0f, 0f);

            var scrollbar = scrollbarTrack.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = scrollbarHandle;
            scrollbar.targetGraphic = scrollbarHandle.GetComponent<Image>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            _rosterScroll = panel.gameObject.AddComponent<ScrollRect>();
            _rosterScroll.viewport = _rosterViewport;
            _rosterScroll.content = _rosterContent;
            _rosterScroll.horizontal = false;
            _rosterScroll.vertical = true;
            _rosterScroll.movementType = ScrollRect.MovementType.Clamped;
            // ZeepCast handles the wheel explicitly so it cannot also zoom the
            // broadcast camera while the pointer is over the roster.
            _rosterScroll.scrollSensitivity = 0f;
            _rosterScroll.verticalScrollbar = scrollbar;
            _rosterScroll.verticalScrollbarVisibility =
                ScrollRect.ScrollbarVisibility.Permanent;
        }

        private void HandleRosterScrolling()
        {
            if (!IsPointerOverRoster || _rosterScroll == null)
            {
                return;
            }

            var wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) <= 0.01f)
            {
                return;
            }

            _rosterScroll.StopMovement();
            _rosterScroll.verticalNormalizedPosition = Mathf.Clamp01(
                _rosterScroll.verticalNormalizedPosition + wheel * 0.12f);
        }

        private void BuildSelectedCard()
        {
            var panel = UiFactory.CreatePanel(_canvas, "SelectedRacer", UiFactory.Panel, false);
            UiFactory.Anchor(
                panel,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(620f, 92f),
                new Vector2(120f, 22f));

            var accent = UiFactory.CreatePanel(panel, "Accent", UiFactory.Accent, false);
            accent.anchorMin = new Vector2(0f, 0f);
            accent.anchorMax = new Vector2(0f, 1f);
            accent.pivot = new Vector2(0f, 0.5f);
            accent.sizeDelta = new Vector2(6f, 0f);
            accent.anchoredPosition = Vector2.zero;

            _selectedName = UiFactory.CreateText(
                panel, "Name", "NO RACER SELECTED", 23, FontStyle.Bold,
                TextAnchor.MiddleLeft, UiFactory.Ink);
            UiFactory.Anchor(
                _selectedName.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(350f, 42f),
                new Vector2(22f, -10f));

            _selectedStats = UiFactory.CreateText(
                panel, "Stats", string.Empty, 15, FontStyle.Normal,
                TextAnchor.MiddleLeft, UiFactory.MutedInk);
            UiFactory.Anchor(
                _selectedStats.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(500f, 38f),
                new Vector2(22f, 10f));

            _selectedStatus = UiFactory.CreateText(
                panel, "Status", string.Empty, 14, FontStyle.Bold,
                TextAnchor.MiddleRight, UiFactory.Accent);
            UiFactory.Anchor(
                _selectedStatus.rectTransform,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(190f, 54f),
                new Vector2(-20f, 0f));
        }

        private void BuildHelp()
        {
            var background = UiFactory.CreatePanel(
                _canvas,
                "HelpBackground",
                new Color(0.015f, 0.025f, 0.05f, 0.58f),
                false);
            UiFactory.Anchor(
                background,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(930f, 28f),
                new Vector2(-12f, 3f));

            var help = UiFactory.CreateText(
                background,
                "Help",
                "F8 EXIT  •  F9 UI  •  TAB VIEW  •  [ ] RACER  •  WASD / MMB PAN  •  RMB ORBIT  •  WHEEL ZOOM  •  R RESET",
                11,
                FontStyle.Bold,
                TextAnchor.MiddleRight,
                new Color(0.84f, 0.88f, 0.96f, 0.92f));
            UiFactory.Stretch(help.rectTransform, 12f, 2f, 12f, 2f);
        }

        private void RefreshChrome()
        {
            _eventTitle.text = _director.EventTitle.ToUpperInvariant();
            _levelTitle.text = _director.LevelTitle;
            _clock.text = _director.LobbyClock;
            _mode.text = _director.CameraMode == BroadcastCameraMode.Overview
                ? "OVERVIEW"
                : "FOLLOW";
            _rosterHeader.text = $"RACERS  {_director.Racers.Count}";

            if (_director.TryGetSelected(out var selected))
            {
                _selectedName.text = $"P{selected.Position}  {selected.Name}";
                _selectedName.color = selected.Color;

                var points = selected.Player.ChampionshipPoints.x;
                var time = selected.Finished ? selected.FinishTime : selected.Runtime;
                _selectedStats.text =
                    $"{selected.Speed} km/h   •   {FormatTime(time)}   •   CP {selected.Checkpoints}   •   {points} pts";
                _selectedStatus.text = selected.Status;
            }
            else
            {
                _selectedName.text = "NO RACER SELECTED";
                _selectedName.color = UiFactory.Ink;
                _selectedStats.text = "Waiting for remote racers…";
                _selectedStatus.text = string.Empty;
            }
        }

        private void ReconcileRacers()
        {
            var activeIds = new HashSet<ulong>(_director.Racers.Select(racer => racer.SteamId));

            foreach (var staleId in _tiles.Keys.Where(id => !activeIds.Contains(id)).ToArray())
            {
                Destroy(_tiles[staleId].Root.gameObject);
                _tiles.Remove(staleId);
            }

            foreach (var staleId in _markers.Keys.Where(id => !activeIds.Contains(id)).ToArray())
            {
                Destroy(_markers[staleId].Root.gameObject);
                _markers.Remove(staleId);
            }

            for (var index = 0; index < _director.Racers.Count; index++)
            {
                var racer = _director.Racers[index];
                if (!_tiles.TryGetValue(racer.SteamId, out var tile))
                {
                    tile = CreateRacerTile(racer.SteamId);
                    _tiles.Add(racer.SteamId, tile);
                }

                if (!_markers.TryGetValue(racer.SteamId, out var marker))
                {
                    marker = CreateRacerMarker(racer.SteamId);
                    _markers.Add(racer.SteamId, marker);
                }

                UpdateTile(tile, racer);
                UpdateMarker(marker, racer);
                tile.Root.SetSiblingIndex(index);
            }
        }

        private RacerTile CreateRacerTile(ulong steamId)
        {
            var buttonObject = new GameObject(
                $"Racer_{steamId}",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));
            buttonObject.transform.SetParent(_rosterContent, false);

            var root = buttonObject.GetComponent<RectTransform>();
            buttonObject.GetComponent<LayoutElement>().preferredHeight = 72f;
            var background = buttonObject.GetComponent<Image>();
            background.color = UiFactory.PanelLight;

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(() => _director.SelectRacer(steamId));

            var colorBar = UiFactory.CreatePanel(root, "Color", Color.white, false).GetComponent<Image>();
            UiFactory.Anchor(
                colorBar.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(5f, 72f),
                Vector2.zero);

            var position = UiFactory.CreateText(
                root, "Position", "P1", 17, FontStyle.Bold,
                TextAnchor.MiddleCenter, UiFactory.Ink);
            UiFactory.Anchor(
                position.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(48f, 72f),
                new Vector2(7f, 0f));

            var name = UiFactory.CreateText(
                root, "Name", string.Empty, 16, FontStyle.Bold,
                TextAnchor.MiddleLeft, UiFactory.Ink);
            UiFactory.Anchor(
                name.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(180f, 36f),
                new Vector2(58f, -4f));

            var telemetry = UiFactory.CreateText(
                root, "Telemetry", string.Empty, 12, FontStyle.Normal,
                TextAnchor.MiddleLeft, UiFactory.MutedInk);
            UiFactory.Anchor(
                telemetry.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(210f, 32f),
                new Vector2(58f, 4f));

            var status = UiFactory.CreateText(
                root, "Status", string.Empty, 10, FontStyle.Bold,
                TextAnchor.MiddleRight, UiFactory.Accent);
            UiFactory.Anchor(
                status.rectTransform,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(108f, 56f),
                new Vector2(-10f, 0f));

            return new RacerTile
            {
                Root = root,
                Background = background,
                ColorBar = colorBar,
                Position = position,
                Name = name,
                Telemetry = telemetry,
                Status = status
            };
        }

        private RacerMarker CreateRacerMarker(ulong steamId)
        {
            var buttonObject = new GameObject(
                $"Marker_{steamId}",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(_markerLayer, false);

            var root = buttonObject.GetComponent<RectTransform>();
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(142f, 34f);

            var background = buttonObject.GetComponent<Image>();
            background.color = UiFactory.Panel;

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(() => _director.SelectRacer(steamId));

            var label = UiFactory.CreateText(
                root, "Label", string.Empty, 12, FontStyle.Bold,
                TextAnchor.MiddleCenter, UiFactory.Ink);
            UiFactory.Stretch(label.rectTransform, 5f, 2f, 5f, 2f);

            return new RacerMarker
            {
                Root = root,
                Background = background,
                Label = label
            };
        }

        private void UpdateTile(RacerTile tile, RacerSnapshot racer)
        {
            var selected = racer.SteamId == _director.SelectedSteamId;
            tile.ColorBar.color = racer.Color;
            tile.Position.text = $"P{racer.Position}";
            tile.Name.text = racer.Name;
            tile.Name.color = selected ? racer.Color : UiFactory.Ink;
            tile.Telemetry.text =
                $"{racer.Speed} km/h   {FormatTime(racer.Finished ? racer.FinishTime : racer.Runtime)}   CP {racer.Checkpoints}";
            tile.Status.text = racer.Status;
            tile.Background.color = selected
                ? new Color(racer.Color.r * 0.18f, racer.Color.g * 0.18f, racer.Color.b * 0.18f, 0.97f)
                : UiFactory.PanelLight;
        }

        private void UpdateMarker(RacerMarker marker, RacerSnapshot racer)
        {
            var selected = racer.SteamId == _director.SelectedSteamId;
            marker.Label.text = selected
                ? $"▼  P{racer.Position}  {racer.Name}"
                : $"●  P{racer.Position}  {racer.Name}";
            marker.Label.color = racer.Color;
            marker.Background.color = selected
                ? new Color(0.02f, 0.03f, 0.06f, 0.98f)
                : new Color(0.02f, 0.03f, 0.06f, 0.72f);
            marker.Root.sizeDelta = selected
                ? new Vector2(174f, 40f)
                : new Vector2(142f, 32f);
        }

        private void UpdateMarkerPositions()
        {
            if (!_director.IsVisualizationActive)
            {
                foreach (var marker in _markers.Values)
                {
                    marker.Root.gameObject.SetActive(false);
                }

                return;
            }

            var camera = _director.BroadcastCamera;
            if (camera == null)
            {
                return;
            }

            foreach (var racer in _director.Racers)
            {
                if (!_markers.TryGetValue(racer.SteamId, out var marker) || racer.Transform == null)
                {
                    continue;
                }

                var screenPoint = camera.WorldToScreenPoint(racer.Transform.position + Vector3.up * 2.5f);
                if (screenPoint.z <= 0f)
                {
                    marker.Root.gameObject.SetActive(false);
                    continue;
                }

                marker.Root.gameObject.SetActive(true);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvas,
                    screenPoint,
                    null,
                    out var localPoint);

                var rect = _canvas.rect;
                localPoint.x = Mathf.Clamp(localPoint.x, rect.xMin + 90f, rect.xMax - 90f);
                localPoint.y = Mathf.Clamp(localPoint.y, rect.yMin + 50f, rect.yMax - 115f);
                marker.Root.anchoredPosition = localPoint;
            }
        }

        private static string FormatTime(float seconds)
        {
            if (seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds))
            {
                return "--:--.---";
            }

            var span = TimeSpan.FromSeconds(seconds);
            return span.TotalHours >= 1d
                ? $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}.{span.Milliseconds:000}"
                : $"{(int)span.TotalMinutes}:{span.Seconds:00}.{span.Milliseconds:000}";
        }
    }
}
