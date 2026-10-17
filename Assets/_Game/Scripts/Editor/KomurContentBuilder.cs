using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Komur.EditorTools
{
    // Content packs wired in by the builder: Minifolk villagers, magic potions and background music.
    public static partial class KomurSceneBuilder
    {
        private const string MinifolkRoot = "Assets/ThirdPartyAssets/MinifolksVillagers2";
        private const string PotionDir = "Assets/ThirdPartyAssets/48 Free Magic Potions Pixel Art Icons/PNG/Transperent";
        private const string MusicDir = MinifolkRoot + "/Cozy Tunes/Audio/ogg";
        private const string DataDir = GameRoot + "/Data";
        private static readonly string[] DefaultMusic = { "Wanderer's Tale", "Sunlight Through Leaves", "Whispering Woods" };
        private const string EtfxSound = "Assets/ThirdPartyAssets/Epic Toon FX/Sound/";
        private const string CozySound = "Assets/ThirdPartyAssets/Cozy FX/Audio/Sound Effects/";

        // First guesses by file name (Cozy FX is only numbered, so victory/defeat are picks to audition).
        // Only empty slots are filled: anything you assign yourself is kept.
        private static readonly (FeedbackId id, string clip, float volume)[] DefaultSounds =
        {
            (FeedbackId.Pounce, EtfxSound + "etfx_explosion_poof.wav", 0.5f),
            (FeedbackId.RatHit, EtfxSound + "etfx_target_hit.wav", 0.6f),
            (FeedbackId.RatKilled, EtfxSound + "etfx_explosion_liquid.wav", 0.6f),
            (FeedbackId.Bite, EtfxSound + "etfx_explosion_poisoncloud.wav", 0.6f),
            (FeedbackId.MedicinePicked, EtfxSound + "etfx_explosion_sparkle2.wav", 0.7f),
            (FeedbackId.Cured, EtfxSound + "etfx_explosion_magic2.wav", 0.7f),
            (FeedbackId.VillagerDied, EtfxSound + "etfx_explosion_soul.wav", 0.7f),
            (FeedbackId.VillagerScared, EtfxSound + "etfx_shoot_snowball.wav", 0.5f),
            (FeedbackId.Stunned, EtfxSound + "etfx_stun01.wav", 0.6f),
            (FeedbackId.WaveStart, EtfxSound + "etfx_spawn.wav", 0.6f),
            (FeedbackId.HoleOpened, EtfxSound + "etfx_explosion_dark01.wav", 0.6f),
            (FeedbackId.BossArrived, EtfxSound + "etfx_explosion_slime.wav", 0.9f),
            (FeedbackId.BossKilled, EtfxSound + "etfx_explosion_slime2.wav", 0.9f),
            (FeedbackId.Won, CozySound + "cozy_fx_36.ogg", 0.9f),
            (FeedbackId.Lost, CozySound + "cozy_fx_12.ogg", 0.9f),
        };

        private static void AssignDefaultSounds(GameFeedbacks feedbacks)
        {
            feedbacks.EnsureAllEntries();
            foreach (var (id, clipPath, volume) in DefaultSounds)
            {
                var entry = feedbacks.entries.FirstOrDefault(e => e.id == id);
                if (entry == null || entry.sfx != null) continue;
                entry.sfx = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
                entry.volume = volume;
                if (entry.sfx == null) Debug.LogWarning($"[Kömür] Sound not found for {id}: {clipPath}");
            }
            EditorUtility.SetDirty(feedbacks);
        }

        // Called from scene setup and "Update Prefabs and Settings".
        private static void ConfigureSceneContent()
        {
            var potions = PotionSprites();
            var glow = PotionGlowMaterial();
            foreach (var apothecary in Object.FindObjectsByType<Apothecary>(FindObjectsSortMode.None))
            {
                apothecary.potionSprites = potions;
                apothecary.potionGlowMaterial = glow;
                apothecary.potionOffset = Vector2.zero;
                EnsurePotionPlaceholder(apothecary, potions.Length > 0 ? potions[0] : null);
                EditorUtility.SetDirty(apothecary);
            }

            var feedbacks = Object.FindFirstObjectByType<GameFeedbacks>();
            if (feedbacks != null)
            {
                AssignDefaultSounds(feedbacks);
                // The stun already shows OUCH! and the cat flashing; the star burst was redundant.
                var stunned = feedbacks.entries.FirstOrDefault(e => e.id == FeedbackId.Stunned);
                if (stunned != null) stunned.vfxPrefab = null;
            }

            var music = Object.FindFirstObjectByType<MusicPlayer>();
            if (music == null)
            {
                var go = new GameObject("Music");
                go.AddComponent<AudioSource>();
                music = go.AddComponent<MusicPlayer>();
            }
            if (music.tracks == null || music.tracks.Length == 0 || music.tracks.All(t => t == null))
                music.tracks = DefaultMusic.Select(n => AssetDatabase.LoadAssetAtPath<AudioClip>($"{MusicDir}/{n}.ogg")).Where(c => c != null).ToArray();
            var source = music.GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            EditorUtility.SetDirty(music);
        }

        // ---------- Spirits ----------

        // All In 1 Sprite Shader "Ghost" (see-through, brightened) with a pale cyan glowing rim.
        private static Material GhostMaterial()
        {
            var shader = Shader.Find("AllIn1SpriteShader/AllIn1SpriteShader");
            if (shader == null) return null;
            string dir = GameRoot + "/Art/Materials";
            Directory.CreateDirectory(dir);
            string path = dir + "/Spirit.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            foreach (var k in new[] { "GHOST_ON", "OUTBASE_ON", "OUTBASEPIXELPERF_ON" }) mat.EnableKeyword(k);
            mat.SetFloat("_GhostColorBoost", 1.6f);
            mat.SetFloat("_GhostTransparency", 0.15f);
            mat.SetFloat("_GhostBlend", 1f);
            mat.SetColor("_OutlineColor", new Color(0.6f, 0.95f, 1f, 1f));
            mat.SetFloat("_OutlineAlpha", 0.7f);
            mat.SetFloat("_OutlineGlow", 2f);
            mat.SetInt("_OutlinePixelWidth", 1);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------- Potions ----------

        // A sprite under the Apothecary to place and size the potion by hand (hidden in play). Created once,
        // a little above the middle of the altar; after that your edits are kept.
        private static void EnsurePotionPlaceholder(Apothecary apothecary, Sprite sprite)
        {
            if (apothecary.potionPlaceholder != null) return;
            var existing = apothecary.transform.Find("Potion Placeholder");
            var sr = existing != null ? existing.GetComponent<SpriteRenderer>() : null;
            if (sr == null)
            {
                Bounds altar = new Bounds(apothecary.transform.position, Vector3.zero);
                bool any = false;
                int layer = 0, order = 0;
                foreach (var r in apothecary.GetComponentsInChildren<SpriteRenderer>())
                {
                    if (r.sprite == null || r.name.Contains("Shadow")) continue;
                    if (!any) { altar = r.bounds; any = true; } else altar.Encapsulate(r.bounds);
                    layer = r.sortingLayerID;
                    order = Mathf.Max(order, r.sortingOrder);
                }
                var go = new GameObject("Potion Placeholder");
                go.transform.SetParent(apothecary.transform, false);
                go.transform.position = altar.center + Vector3.up * 0.45f;
                sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingLayerID = layer;
                sr.sortingOrder = order + 1;
                sr.color = new Color(1f, 1f, 1f, 0.8f);
                float h = sprite != null ? Mathf.Max(0.01f, sprite.bounds.size.y) : 1f;
                go.transform.localScale = Vector3.one * (0.6f / h / Mathf.Max(0.01f, apothecary.transform.lossyScale.y));
            }
            apothecary.potionPlaceholder = sr;
            EditorUtility.SetDirty(apothecary);
        }

        // All In 1 Sprite Shader: pixel-perfect golden rim (8 directions) plus a shine sweep; the Potion
        // script animates the shine and the rim's glow.
        private static Material PotionGlowMaterial()
        {
            var shader = Shader.Find("AllIn1SpriteShader/AllIn1SpriteShader");
            if (shader == null) { Debug.LogWarning("[Kömür] All In 1 Sprite Shader not found; potions use the soft halo."); return null; }
            string dir = GameRoot + "/Art/Materials";
            Directory.CreateDirectory(dir);
            string path = dir + "/Potion Glow.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            foreach (var k in new[] { "OUTBASE_ON", "OUTBASEPIXELPERF_ON", "OUTBASE8DIR_ON", "SHINE_ON" }) mat.EnableKeyword(k);
            mat.SetColor("_OutlineColor", new Color(1f, 0.82f, 0.35f, 1f));
            mat.SetFloat("_OutlineAlpha", 1f);
            mat.SetFloat("_OutlineGlow", 3f);
            mat.SetInt("_OutlinePixelWidth", 1);
            mat.SetColor("_ShineColor", new Color(1f, 1f, 0.9f, 1f));
            mat.SetFloat("_ShineWidth", 0.12f);
            mat.SetFloat("_ShineGlow", 2.5f);
            mat.SetFloat("_ShineRotate", 0.6f);
            mat.SetFloat("_ShineLocation", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Sprite[] PotionSprites()
        {
            if (!Directory.Exists(PotionDir)) return new Sprite[0];
            var sprites = new List<Sprite>();
            foreach (var file in Directory.GetFiles(PotionDir, "*.png").OrderBy(f => f.Length).ThenBy(f => f))
            {
                string path = file.Replace('\\', '/');
                MakePixelArt(path);
                var sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
                if (sprite != null) sprites.Add(sprite);
            }
            return sprites.ToArray();
        }

        // ---------- Minifolk villagers ----------

        // One look per sheet in Original/ and Blue/ (outlined versions). Rows are read from the importer's
        // auto-slices: row 0 idle, walk = the 6-frame row right before a 3-frame row, hurt = the 2-frame row,
        // death = the last row.
        private static VillagerLooks VillagerLooksAsset()
        {
            Directory.CreateDirectory(DataDir);
            string assetPath = DataDir + "/VillagerLooks.asset";
            var asset = AssetDatabase.LoadAssetAtPath<VillagerLooks>(assetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<VillagerLooks>();
                AssetDatabase.CreateAsset(asset, assetPath);
            }
            asset.looks.Clear();
            foreach (var palette in new[] { "Original", "Blue" })
            {
                string dir = $"{MinifolkRoot}/{palette}/Outline";
                if (!Directory.Exists(dir)) continue;
                foreach (var file in Directory.GetFiles(dir, "*.png").OrderBy(f => f))
                {
                    string path = file.Replace('\\', '/');
                    var look = ReadLook(path);
                    if (look != null) asset.looks.Add(look);
                }
            }
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Kömür] Villager looks: {asset.looks.Count} ({string.Join(", ", asset.looks.Select(l => l.sheet.name).Distinct())})");
            return asset;
        }

        private static VillagerLooks.Look ReadLook(string path)
        {
            MakePixelArt(path);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var slices = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
            const int cell = 32;
            if (tex == null || tex.height < cell * 4 || slices.Length < 10) return null;   // e.g. the anvil prop

            // Frames per row = highest occupied cell + 1 (the importer sometimes splits one frame into
            // several slices, so slices can't be counted directly).
            int rows = tex.height / cell, columns = tex.width / cell;
            var counts = new int[rows];
            Sprite firstIdle = null;
            foreach (var s in slices)
            {
                int row = (tex.height - Mathf.FloorToInt(s.rect.y) - 1) / cell;
                int col = Mathf.FloorToInt(s.rect.center.x / cell);
                if (row < 0 || row >= rows || col < 0 || col >= columns) continue;
                counts[row] = Mathf.Max(counts[row], col + 1);
                if (row == 0 && (firstIdle == null || s.rect.x < firstIdle.rect.x)) firstIdle = s;
            }
            int last = rows - 1;
            while (last > 0 && counts[last] == 0) last--;

            int walk = -1;
            for (int r = 1; r < last; r++) if (counts[r] == 6 && counts[r + 1] == 3) { walk = r; break; }
            if (walk < 0) for (int r = 1; r < last; r++) if (counts[r] == 6) { walk = r; break; }
            if (walk < 0) walk = 1;
            int hurt = -1;
            for (int r = last - 1; r > 0; r--) if (counts[r] == 2) { hurt = r; break; }

            string name = Path.GetFileNameWithoutExtension(path);
            var look = new VillagerLooks.Look
            {
                character = name.EndsWith("B") ? name.Substring(0, name.Length - 1) : name,
                sheet = tex,
                cell = cell,
                idleRow = 0, idleFrames = Mathf.Max(1, counts[0]),
                walkRow = walk, walkFrames = Mathf.Max(1, counts[walk]),
                hurtRow = hurt, hurtFrames = hurt >= 0 ? counts[hurt] : 0,
                deathRow = last, deathFrames = counts[last],
            };
            if (firstIdle != null)
            {
                // Feet: centre of the idle frame horizontally, its lowest pixel vertically.
                float cx = firstIdle.rect.x + firstIdle.rect.width * 0.5f;
                float bottom = firstIdle.rect.y - (tex.height - cell);
                look.pivot = new Vector2(Mathf.Round(cx) / cell, Mathf.Clamp01(bottom / cell));
            }
            return look;
        }

        // Crisp pixels: point filtering, no compression.
        private static void MakePixelArt(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return;
            if (importer.filterMode == FilterMode.Point && importer.textureCompression == TextureImporterCompression.Uncompressed) return;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
    }
}
