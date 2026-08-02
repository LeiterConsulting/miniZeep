using System.Collections.Generic;
using UnityEngine;
using ZeepCast.Core;
using ZeepkistClient;

namespace ZeepCast.Rendering
{
    /// <summary>
    /// Zeepkist calls all remote network representations "ghosts", including
    /// racers who are actively driving. FadeGhostModel may replace every
    /// cosmetic material with one translucent tint. This controller suppresses
    /// only that local presentation behavior while ZeepCast is active.
    /// </summary>
    internal sealed class SolidRacerPresentation
    {
        private sealed class RendererState
        {
            public Renderer Renderer = null!;
            public Material[] Materials = null!;
            public bool Enabled;
        }

        private sealed class GhostState
        {
            public NetworkedZeepkistGhost Ghost = null!;
            public FadeGhostModel Fader = null!;
            public bool FaderEnabled;
            public readonly Dictionary<int, RendererState> Renderers = new();
        }

        private sealed class SettingsState
        {
            public GameSettingsScriptableObject Settings = null!;
            public bool DrawOthers;
            public bool DrawZeepkists;
        }

        private readonly bool _enabled;
        private readonly Dictionary<ulong, GhostState> _ghosts = new();
        private readonly Dictionary<int, SettingsState> _settings = new();
        private bool _active;

        public SolidRacerPresentation(bool enabled)
        {
            _enabled = enabled;
        }

        public void Begin()
        {
            _active = _enabled;
        }

        public void Apply(IReadOnlyList<RacerSnapshot> racers)
        {
            if (!_active)
            {
                return;
            }

            var present = new HashSet<ulong>();
            var localSteamId = ZeepkistNetwork.LocalPlayer != null
                ? ZeepkistNetwork.LocalPlayer.SteamID
                : 0;
            foreach (var racer in racers)
            {
                if (racer == null ||
                    racer.Ghost == null ||
                    (localSteamId != 0 && racer.SteamId == localSteamId))
                {
                    continue;
                }

                present.Add(racer.SteamId);
                ForceSolid(racer);
            }

            var disconnected = new List<ulong>();
            foreach (var pair in _ghosts)
            {
                if (!present.Contains(pair.Key) || pair.Value.Ghost == null)
                {
                    RestoreGhost(pair.Value);
                    disconnected.Add(pair.Key);
                }
            }

            foreach (var steamId in disconnected)
            {
                _ghosts.Remove(steamId);
            }
        }

        public void Restore()
        {
            foreach (var state in _ghosts.Values)
            {
                RestoreGhost(state);
            }

            foreach (var state in _settings.Values)
            {
                if (state.Settings == null)
                {
                    continue;
                }

                state.Settings.online_drawOthers = state.DrawOthers;
                state.Settings.online_drawZeepkists = state.DrawZeepkists;
            }

            _ghosts.Clear();
            _settings.Clear();
            _active = false;
        }

        private void ForceSolid(RacerSnapshot racer)
        {
            var ghost = racer.Ghost;
            var fader = ghost.ghostFader;
            CaptureSettings(ghost.GlobalSettings);

            // Keep Zeepkist's visibility state machine enabled for every live
            // remote racer. This is local-only and restored on exit.
            if (ghost.GlobalSettings != null)
            {
                ghost.GlobalSettings.online_drawOthers = true;
                ghost.GlobalSettings.online_drawZeepkists = true;
            }

            if (fader == null)
            {
                // Zeepkist's own per-ghost visibility loop observes the temporary
                // draw settings. Never force a model object active directly: the
                // local network ghost does not run that repair loop and can
                // otherwise remain as a duplicate kart after leaving photo mode.
                return;
            }

            if (!_ghosts.TryGetValue(racer.SteamId, out var state))
            {
                state = new GhostState
                {
                    Ghost = ghost,
                    Fader = fader,
                    FaderEnabled = fader.enabled
                };
                _ghosts.Add(racer.SteamId, state);
            }

            CaptureRenderers(state);
            fader.enabled = false;

            var count = Mathf.Min(fader.modelRenderers.Count, fader.modelMaterials.Count);
            for (var i = 0; i < count; i++)
            {
                var renderer = fader.modelRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                // modelMaterials is FadeGhostModel's own cache of the original
                // cosmetic material arrays populated immediately after setup.
                renderer.sharedMaterials = fader.modelMaterials[i];
                renderer.enabled = true;
            }
        }

        private void CaptureSettings(GameSettingsScriptableObject settings)
        {
            if (settings == null)
            {
                return;
            }

            var id = settings.GetInstanceID();
            if (_settings.ContainsKey(id))
            {
                return;
            }

            _settings.Add(id, new SettingsState
            {
                Settings = settings,
                DrawOthers = settings.online_drawOthers,
                DrawZeepkists = settings.online_drawZeepkists
            });
        }

        private static void CaptureRenderers(GhostState state)
        {
            var fader = state.Fader;
            foreach (var renderer in fader.modelRenderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                var id = renderer.GetInstanceID();
                if (state.Renderers.ContainsKey(id))
                {
                    continue;
                }

                state.Renderers.Add(id, new RendererState
                {
                    Renderer = renderer,
                    Materials = renderer.sharedMaterials,
                    Enabled = renderer.enabled
                });
            }
        }

        private static void RestoreGhost(GhostState state)
        {
            foreach (var rendererState in state.Renderers.Values)
            {
                if (rendererState.Renderer == null)
                {
                    continue;
                }

                rendererState.Renderer.sharedMaterials = rendererState.Materials;
                rendererState.Renderer.enabled = rendererState.Enabled;
            }

            if (state.Fader != null)
            {
                state.Fader.enabled = state.FaderEnabled;
            }
        }
    }
}
