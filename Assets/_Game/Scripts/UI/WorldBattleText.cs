using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Komur
{
    // Plays a PixelBattleText TextAnimation preset in world space, anchored to a character
    // (or to a fixed point when the character is gone, e.g. a killed rat). One "pixel" of the
    // preset maps to pixelSize world units, so the look matches the PixelBattleText demo.
    public class WorldBattleText : MonoBehaviour
    {
        private const string SortingLayer = "Layer 3";
        private const int SortingOrder = 200;

        private PixelBattleText.TextAnimation anim;
        private Transform follow;
        private float height;
        private float pixelSize;
        private float startTime;
        private readonly List<TextMeshPro> fills = new List<TextMeshPro>();
        private readonly List<TextMeshPro[]> borders = new List<TextMeshPro[]>();
        private static readonly Vector2[] BorderOffsets = { new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(0, -1) };

        public static void Spawn(string word, PixelBattleText.TextAnimation animation, Vector3 position, Transform follow, float height, float pixelSize)
        {
            if (animation == null || string.IsNullOrEmpty(word)) return;
            // Not parented to the character: its Sorting Group would draw the text in the character's
            // slot, under any wall in front of it. Instead it follows the character's position.
            var go = new GameObject("Battle Text " + word);
            go.transform.position = (follow != null ? follow.position : position) + Vector3.up * height;
            var text = go.AddComponent<WorldBattleText>();
            text.follow = follow;
            text.height = height;
            text.Init(word, animation, pixelSize);
        }

        private void Init(string word, PixelBattleText.TextAnimation animation, float size)
        {
            anim = animation;
            pixelSize = size;
            startTime = Time.time;
            for (int i = 0; i < word.Length; i++)
            {
                string letter = word[i].ToString();
                var letterBorders = new TextMeshPro[anim.haveBorder ? BorderOffsets.Length : 0];
                for (int b = 0; b < letterBorders.Length; b++) letterBorders[b] = NewLetter(letter, SortingOrder);
                borders.Add(letterBorders);
                fills.Add(NewLetter(letter, SortingOrder + 1));
            }
            Update();
        }

        private TextMeshPro NewLetter(string letter, int order)
        {
            var go = new GameObject(letter);
            go.transform.SetParent(transform, false);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = anim.font;
            tmp.text = letter;
            // TMP world font size 10 is roughly one unit tall.
            tmp.fontSize = anim.textSize * pixelSize * 10f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = new Vector2(2f, 2f);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sortingLayerName = SortingLayer;
            mr.sortingOrder = order;
            return tmp;
        }

        private void Update()
        {
            if (follow != null) transform.position = follow.position + Vector3.up * height;
            float elapsed = Time.time - startTime;
            int n = fills.Count;
            bool done = true;

            // Letters are laid out left to right with animated spacing, centred on the anchor.
            float[] spacing = new float[n];
            float[] t = new float[n];
            float total = 0f;
            for (int i = 0; i < n; i++)
            {
                int order = anim.invertAnimationOrder ? n - 1 - i : i;
                t[i] = Mathf.Clamp01((elapsed - order * anim.perLetterDelay) / Mathf.Max(0.01f, anim.transitionDuration));
                if (t[i] < 1f) done = false;
                spacing[i] = Mathf.LerpUnclamped(anim.initialSpacing, anim.endSpacing, anim.spacingCurve.Evaluate(t[i]));
                if (i > 0) total += spacing[i];
            }

            float x = -total * 0.5f;
            for (int i = 0; i < n; i++)
            {
                if (i > 0) x += spacing[i];
                Vector2 offset = new Vector2(
                    Mathf.LerpUnclamped(anim.initialOffset.x, anim.endOffset.x, anim.offsetCurveX.Evaluate(t[i])),
                    Mathf.LerpUnclamped(anim.initialOffset.y, anim.endOffset.y, anim.offsetCurveY.Evaluate(t[i])));
                Vector3 pos = new Vector3(x + offset.x, offset.y, 0f) * pixelSize;

                fills[i].transform.localPosition = pos;
                fills[i].color = anim.fillColorInTime != null ? anim.fillColorInTime.Evaluate(t[i]) : Color.white;

                var letterBorders = borders[i];
                Color borderColor = anim.borderColorInTime != null ? anim.borderColorInTime.Evaluate(t[i]) : Color.black;
                for (int b = 0; b < letterBorders.Length; b++)
                {
                    letterBorders[b].transform.localPosition = pos + (Vector3)(BorderOffsets[b] * pixelSize);
                    letterBorders[b].color = borderColor;
                }
            }

            if (done) Destroy(gameObject);
        }
    }
}
