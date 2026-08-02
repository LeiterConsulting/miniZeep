using UnityEngine;
using UnityEngine.Rendering;
using ZeepCast.Core;

namespace ZeepCast.Rendering
{
    /// <summary>
    /// Adds a local-only, depth-tested silhouette for the selected racer. The
    /// material uses Greater depth testing, so only portions hidden behind
    /// course geometry are tinted. No track renderer or shared racer material
    /// is changed.
    /// </summary>
    internal sealed class SelectedRacerVisibility
    {
        private const string BufferName = "ZeepCast Selected Racer Visibility";

        private readonly bool _enabled;
        private Camera? _camera;
        private CommandBuffer? _buffer;
        private Material? _material;
        private ulong _selectedSteamId;
        private bool _unsupportedLogged;

        public SelectedRacerVisibility(bool enabled)
        {
            _enabled = enabled;
        }

        public void Begin(Camera camera)
        {
            if (!_enabled || camera == null)
            {
                return;
            }

            if (_camera == camera && _buffer != null && _material != null)
            {
                return;
            }

            Restore();

            var shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
            {
                if (!_unsupportedLogged)
                {
                    _unsupportedLogged = true;
                    ZeepCastPlugin.Log.LogWarning(
                        "Selected-racer visibility shader is unavailable; continuing without the silhouette.");
                }

                return;
            }

            _material = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            _material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            _material.SetInt("_Cull", (int)CullMode.Off);
            _material.SetInt("_ZWrite", 0);
            _material.SetInt("_ZTest", (int)CompareFunction.Greater);
            _material.SetColor("_Color", new Color(0.25f, 0.92f, 1f, 0.42f));

            _buffer = new CommandBuffer { name = BufferName };
            _camera = camera;
            _camera.AddCommandBuffer(CameraEvent.AfterForwardAlpha, _buffer);
        }

        public void Apply(Camera camera, RacerSnapshot racer)
        {
            if (!_enabled || camera == null || racer == null)
            {
                Clear();
                return;
            }

            Begin(camera);
            if (_buffer == null || _material == null)
            {
                return;
            }

            if (_selectedSteamId == racer.SteamId)
            {
                return;
            }

            _selectedSteamId = racer.SteamId;
            _buffer.Clear();

            if (racer.Ghost == null || racer.Ghost.ghostModel == null)
            {
                return;
            }

            foreach (var renderer in racer.Ghost.ghostModel.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                {
                    continue;
                }

                var submeshCount = Mathf.Max(1, renderer.sharedMaterials.Length);
                for (var submesh = 0; submesh < submeshCount; submesh++)
                {
                    _buffer.DrawRenderer(renderer, _material, submesh);
                }
            }
        }

        public void Clear()
        {
            _selectedSteamId = 0;
            _buffer?.Clear();
        }

        public void Restore()
        {
            if (_camera != null && _buffer != null)
            {
                _camera.RemoveCommandBuffer(CameraEvent.AfterForwardAlpha, _buffer);
            }

            _buffer?.Release();
            _buffer = null;

            if (_material != null)
            {
                Object.Destroy(_material);
            }

            _material = null;
            _camera = null;
            _selectedSteamId = 0;
        }
    }
}
