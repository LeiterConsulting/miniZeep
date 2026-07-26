using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ZeepCast.UI
{
    internal static class UiFactory
    {
        private static Font? _font;
        public static Font Font => _font ??= Resources.GetBuiltinResource<Font>("Arial.ttf");

        public static readonly Color Ink = new Color(0.96f, 0.98f, 1f, 1f);
        public static readonly Color MutedInk = new Color(0.68f, 0.73f, 0.82f, 1f);
        public static readonly Color Panel = new Color(0.025f, 0.035f, 0.065f, 0.92f);
        public static readonly Color PanelLight = new Color(0.075f, 0.095f, 0.145f, 0.94f);
        public static readonly Color Accent = new Color(0.2f, 0.82f, 1f, 1f);

        public static RectTransform CreateCanvas(string name, int sortingOrder)
        {
            var root = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            return root.GetComponent<RectTransform>();
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var eventSystem = new GameObject(
                "[ZeepCast] EventSystem Fallback",
                typeof(EventSystem),
                typeof(StandaloneInputModule));
            Object.DontDestroyOnLoad(eventSystem);
        }

        public static RectTransform CreatePanel(
            Transform parent,
            string name,
            Color color,
            bool blocksRaycasts = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = blocksRaycasts;
            return go.GetComponent<RectTransform>();
        }

        public static Text CreateText(
            Transform parent,
            string name,
            string value,
            int fontSize,
            FontStyle style,
            TextAnchor alignment,
            Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.font = Font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.text = value;
            text.supportRichText = true;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        public static void Stretch(RectTransform transform, float left, float bottom, float right, float top)
        {
            transform.anchorMin = Vector2.zero;
            transform.anchorMax = Vector2.one;
            transform.offsetMin = new Vector2(left, bottom);
            transform.offsetMax = new Vector2(-right, -top);
        }

        public static void Anchor(
            RectTransform transform,
            Vector2 anchor,
            Vector2 pivot,
            Vector2 size,
            Vector2 position)
        {
            transform.anchorMin = anchor;
            transform.anchorMax = anchor;
            transform.pivot = pivot;
            transform.sizeDelta = size;
            transform.anchoredPosition = position;
        }

    }
}
