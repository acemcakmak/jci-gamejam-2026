using System;
using System.Collections.Generic;
using UnityEngine;

namespace Komur
{
    // Minifolk villager sheets (32x32 grid, one animation per row). Frames are cut on the grid at runtime
    // so every frame shares the same pivot - the importer's tight auto-slices would make them jitter.
    // Looks are dealt from a shuffled deck so no two villagers look alike (until the deck runs out).
    [CreateAssetMenu(menuName = "Kömür/Villager Looks")]
    public class VillagerLooks : ScriptableObject
    {
        [Serializable]
        public class Look
        {
            public string character;       // e.g. "MiniNun"; palette variants share it
            public Texture2D sheet;
            public int cell = 32;
            [Tooltip("Pivot within a cell (0-1): centre of the character's feet.")]
            public Vector2 pivot = new Vector2(0.5f, 0f);
            public int idleRow, idleFrames = 4;
            public int walkRow = 1, walkFrames = 6;
            public int hurtRow = -1, hurtFrames;
            public int deathRow = -1, deathFrames;
        }

        public List<Look> looks = new List<Look>();
        [Tooltip("Pixels per world unit: 16 makes a ~15 px villager about one tile tall.")]
        public float pixelsPerUnit = 16f;

        private static readonly List<Look> deck = new List<Look>();
        private static readonly Dictionary<(Texture2D, int, int), Sprite[]> cache = new Dictionary<(Texture2D, int, int), Sprite[]>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            deck.Clear();
            cache.Clear();
        }

        // Next look from the deck: every character once (random palette) before any repeats.
        public Look Draw()
        {
            if (looks.Count == 0) return null;
            if (deck.Count == 0) Refill();
            var look = deck[deck.Count - 1];
            deck.RemoveAt(deck.Count - 1);
            return look;
        }

        private void Refill()
        {
            var byCharacter = new Dictionary<string, List<Look>>();
            foreach (var l in looks)
            {
                if (l == null || l.sheet == null) continue;
                if (!byCharacter.TryGetValue(l.character, out var list)) byCharacter[l.character] = list = new List<Look>();
                list.Add(l);
            }
            var first = new List<Look>();
            var rest = new List<Look>();
            foreach (var list in byCharacter.Values)
            {
                Shuffle(list);
                first.Add(list[0]);
                for (int i = 1; i < list.Count; i++) rest.Add(list[i]);
            }
            Shuffle(first);
            Shuffle(rest);
            // Drawn from the end: the distinct characters come out first.
            deck.AddRange(rest);
            deck.AddRange(first);
        }

        private static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        public Sprite[] Frames(Look look, int row, int count)
        {
            if (look == null || look.sheet == null || row < 0 || count <= 0) return null;
            count = Mathf.Min(count, look.sheet.width / look.cell);
            if (count <= 0 || (row + 1) * look.cell > look.sheet.height) return null;
            var key = (look.sheet, row, count);
            if (cache.TryGetValue(key, out var frames)) return frames;
            frames = new Sprite[count];
            int c = look.cell;
            float y = look.sheet.height - (row + 1) * c;
            // Trim one texel off the left, right and top of the cell: the outline shader samples one texel
            // past the sprite's edge, which would otherwise outline the neighbouring frames (e.g. the feet
            // of the row above showed as a red strip over angry villagers). Feet sit on the bottom row.
            var pivot = new Vector2((look.pivot.x * c - 1f) / (c - 2f), look.pivot.y * c / (c - 1f));
            for (int i = 0; i < count; i++)
            {
                var rect = new Rect(i * c + 1, y, c - 2, c - 1);
                frames[i] = Sprite.Create(look.sheet, rect, pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
                frames[i].name = $"{look.sheet.name}_{row}_{i}";
            }
            cache[key] = frames;
            return frames;
        }

        public SpriteFlipbook.Clip[] Clips(Look look)
        {
            var idle = Frames(look, look.idleRow, look.idleFrames);
            var walk = Frames(look, look.walkRow, look.walkFrames) ?? idle;
            var clips = new List<SpriteFlipbook.Clip>
            {
                new SpriteFlipbook.Clip { name = "Idle", frames = idle, fps = 6f },
                new SpriteFlipbook.Clip { name = "Walk", frames = walk, fps = 10f },
            };
            var hurt = Frames(look, look.hurtRow, look.hurtFrames);
            if (hurt != null) clips.Add(new SpriteFlipbook.Clip { name = "Hurt", frames = hurt, fps = 8f, loop = false });
            var death = Frames(look, look.deathRow, look.deathFrames);
            if (death != null) clips.Add(new SpriteFlipbook.Clip { name = "Death", frames = death, fps = 8f, loop = false });
            return clips.ToArray();
        }
    }
}
