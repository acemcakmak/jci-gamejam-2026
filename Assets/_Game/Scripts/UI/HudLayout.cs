using System;
using UnityEngine;

namespace Komur
{
    // Mirrors Assets/_Game/UI/hud-layout.json (shared with docs/ui-design/hud-spec.html).
    [Serializable]
    public class HudLayout
    {
        [Serializable]
        public class Element
        {
            public float[] anchor = { 0.5f, 0.5f };
            public float[] pos = { 0f, 0f };
            public float[] size = { 100f, 100f };
            public float fontSize = 32f;
            public float iconSize = 24f;
            public float padding = 0f;

            public Vector2 Anchor => new Vector2(anchor[0], anchor[1]);
            public Vector2 Pos => new Vector2(pos[0], pos[1]);
            public Vector2 Size => new Vector2(size[0], size[1]);
        }

        [Serializable]
        public class IndicatorSpec { public float size = 44f; public float edgePadding = 40f; }

        [Serializable]
        public class BattleTextSpec { public float pixelSize = 0.018f; public float height = 1f; }

        [Serializable]
        public class CameraSpec { public float gameplaySize = 5f; public float titleSize = 3f; }

        public float referenceWidth = 1920f;
        public float referenceHeight = 1080f;
        public float panelSliceScale = 3f;
        public string inkColor = "#5C3A28";
        public string creamColor = "#FFF2D1";

        public Element wavePanel, townPanel, minimap, banner, hint;
        public IndicatorSpec indicator;
        public Element title, subtitle, storyPanel, controlsPanel, playButton, endPanel, endTitle, againButton;
        public BattleTextSpec battleText;
        public CameraSpec camera;

        public Color Ink => Parse(inkColor);
        public Color Cream => Parse(creamColor);

        private static Color Parse(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;
        }

        public static HudLayout FromJson(string json)
        {
            return JsonUtility.FromJson<HudLayout>(json);
        }
    }
}
