using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace Komur.EditorTools
{
    // One-click setup: copies the Cainos demo map into Assets/_Game/Scenes/Game.unity and
    // adds Kömür, villagers, rat holes, the apothecary, HUD and feedback hooks.
    public static partial class KomurSceneBuilder
    {
        private const string GameRoot = "Assets/_Game";
        private const string ScenePath = GameRoot + "/Scenes/Game.unity";
        private const string PrefabDir = GameRoot + "/Prefabs";
        private const string PlaceholderDir = GameRoot + "/Art/Placeholder";
        private const string CainosRoot = "Assets/ThirdPartyAssets/Cainos/Pixel Art Top Down - Basic";
        private const string CatRoot = "Assets/ThirdPartyAssets/Pet Cats pack/Sprites/Cat-2";
        private const string EtfxRoot = "Assets/ThirdPartyAssets/Epic Toon FX/Prefabs";
        private const string CharacterSortingLayer = "Layer 1";
        private const int VillagerCount = 6;
        private const int RatHoleCount = 4;

        [MenuItem("Kömür/Build Game Scene (from Cainos demo)", priority = 0)]
        public static void BuildGameScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Kömür", "Game.unity already exists. Rebuild it from scratch?", "Rebuild", "Cancel"))
                return;

            var prefabs = BuildPrefabsAndSettings();

            if (File.Exists(ScenePath)) AssetDatabase.DeleteAsset(ScenePath);
            AssetDatabase.CopyAsset(CainosRoot + "/Scene/SC Demo.unity", ScenePath);
            var scene = EditorSceneManager.OpenScene(ScenePath);

            Vector3 start = RetireDemoPlayer() ?? Vector3.zero;
            Physics2D.SyncTransforms();
            var apothecary = SetupScene(prefabs, start);

            var used = new List<Vector3> { start, apothecary.transform.position };
            for (int i = 0; i < VillagerCount; i++)
                PlaceVillager(prefabs.villager, FindSpot(start, 2.5f, 7f, used, 1.5f), 1);
            for (int i = 0; i < RatHoleCount; i++)
                PlaceRatHole(prefabs.ratHole, FindSpot(start, 8f, 12f, used, 4f), 1, i + 1);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AddSceneToBuildSettings(ScenePath);
            Selection.activeObject = apothecary;
            Debug.Log("[Kömür] Game scene built. Move villagers, rat holes and the apothecary to taste, then press Play.");
        }

        [MenuItem("Kömür/Setup Open Scene (your own map)", priority = 1)]
        public static void SetupOpenScene()
        {
            var prefabs = BuildPrefabsAndSettings();
            var existing = Object.FindFirstObjectByType<KomurController>();
            Vector3 start = existing != null ? existing.transform.position : RetireDemoPlayer() ?? PlacePosition();
            SetupScene(prefabs, start);

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            if (EditorSceneManager.SaveScene(scene) && !string.IsNullOrEmpty(scene.path)) AddSceneToBuildSettings(scene.path);
            Debug.Log("[Kömür] Scene set up. Place villagers and rat holes with Kömür > Place, " +
                      "and set each rat hole's 'Active From Wave'.");
        }

        [MenuItem("Kömür/Update Prefabs and Settings (keeps scene)", priority = 2)]
        public static void UpdatePrefabsAndSettings()
        {
            var prefabs = BuildPrefabsAndSettings();

            var gm = Object.FindFirstObjectByType<GameManager>();
            if (gm != null)
            {
                gm.ratPrefab = prefabs.rat;
                gm.bossPrefab = prefabs.boss;
                gm.waves = GameManager.DefaultWaves();
                EnsureNavGrids(gm);
                foreach (var tmp in Object.FindObjectsByType<TextMeshPro>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    tmp.font = WorldFont();
                    tmp.text = tmp.text.ToUpperInvariant();
                    EditorUtility.SetDirty(tmp);
                }
                EditorUtility.SetDirty(gm);
                EditorSceneManager.MarkSceneDirty(gm.gameObject.scene);
                EditorSceneManager.SaveScene(gm.gameObject.scene);
            }
            Debug.Log("[Kömür] Prefabs and project settings updated.");
        }

        [MenuItem("Kömür/Place/Villager - Ground Floor", priority = 20)]
        private static void PlaceVillagerGround() => PlaceFromMenu(villager: true, floor: 1);

        [MenuItem("Kömür/Place/Villager - Upper Floor", priority = 21)]
        private static void PlaceVillagerUpper() => PlaceFromMenu(villager: true, floor: 2);

        [MenuItem("Kömür/Place/Rat Hole - Ground Floor", priority = 40)]
        private static void PlaceRatHoleGround() => PlaceFromMenu(villager: false, floor: 1);

        [MenuItem("Kömür/Place/Rat Hole - Upper Floor", priority = 41)]
        private static void PlaceRatHoleUpper() => PlaceFromMenu(villager: false, floor: 2);

        private static void PlaceFromMenu(bool villager, int floor)
        {
            var villagerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Villager.prefab");
            var holePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Rat Hole.prefab");
            if (villagerPrefab == null || holePrefab == null)
            {
                var prefabs = BuildPrefabsAndSettings();
                villagerPrefab = prefabs.villager;
                holePrefab = prefabs.ratHole;
            }

            GameObject placed;
            if (villager)
            {
                placed = PlaceVillager(villagerPrefab, PlacePosition(), floor);
            }
            else
            {
                int wave = Mathf.Min(Object.FindObjectsByType<RatHole>(FindObjectsSortMode.None).Length + 1, 5);
                placed = PlaceRatHole(holePrefab, PlacePosition(), floor, wave);
            }
            Undo.RegisterCreatedObjectUndo(placed, "Place " + placed.name);
            Selection.activeObject = placed;
            EditorSceneManager.MarkSceneDirty(placed.scene);
        }

        private static GameObject PlaceVillager(GameObject prefab, Vector3 position, int floor)
        {
            var root = FindOrCreateRoot("Villagers");
            var v = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            v.transform.position = position;
            v.name = $"Villager {root.childCount} (F{floor})";
            v.GetComponent<FloorAgent>().SetFloor(floor);
            return v;
        }

        private static GameObject PlaceRatHole(GameObject prefab, Vector3 position, int floor, int activeFromWave)
        {
            var root = FindOrCreateRoot("Rat Holes");
            var h = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            h.transform.position = position;
            h.name = $"Rat Hole (wave {activeFromWave}, F{floor})";
            h.GetComponent<FloorAgent>().SetFloor(floor);
            h.GetComponent<RatHole>().activeFromWave = activeFromWave;
            return h;
        }

        // Only looks at scene roots: the HUD has a text object called "Villagers" too.
        private static Transform FindOrCreateRoot(string name)
        {
            var scene = EditorSceneManager.GetActiveScene();
            var go = scene.GetRootGameObjects().FirstOrDefault(r => r.name == name && r.GetComponent<RectTransform>() == null);
            return go != null ? go.transform : new GameObject(name).transform;
        }

        private static Vector3 PlacePosition()
        {
            var view = SceneView.lastActiveSceneView;
            Vector3 p = view != null ? view.pivot : Vector3.zero;
            return new Vector3(p.x, p.y, 0f);
        }

        private static Vector3? RetireDemoPlayer()
        {
            var demoPlayer = FindBehaviour("TopDownCharacterController");
            if (demoPlayer == null) return null;
            demoPlayer.gameObject.SetActive(false);
            return demoPlayer.transform.position;
        }

        // Adds whatever is missing: Kömür, camera follow, apothecary, HUD, feedbacks, game manager, nav grids.
        private static GameObject SetupScene(PrefabSet prefabs, Vector3 start)
        {
            var komurController = Object.FindFirstObjectByType<KomurController>();
            if (komurController == null)
            {
                var komurGo = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.komur);
                komurGo.transform.position = start;
                komurController = komurGo.GetComponent<KomurController>();
            }

            EnsureCamera();
            var cam = Camera.main;
            if (cam != null)
            {
                foreach (var mb in cam.GetComponents<MonoBehaviour>())
                    if (mb.GetType().Name == "CameraFollow") mb.enabled = false;
                var follow = cam.GetComponent<CameraFollow2D>();
                if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow2D>();
                follow.target = komurController.transform;
                cam.transform.position = new Vector3(start.x, start.y, cam.transform.position.z);
            }

            var apothecary = Object.FindFirstObjectByType<Apothecary>();
            var apothecaryGo = apothecary != null ? apothecary.gameObject : BuildApothecary(start + Vector3.right * 3f);

            var hud = Object.FindFirstObjectByType<Hud>();
            if (hud == null) hud = BuildHud();

            if (Object.FindFirstObjectByType<GameFeedbacks>() == null)
            {
                var feedbacks = new GameObject("Game Feedbacks").AddComponent<GameFeedbacks>();
                ConfigureDefaultFeedbacks(feedbacks);
            }

            var gm = Object.FindFirstObjectByType<GameManager>();
            if (gm == null)
            {
                gm = new GameObject("Game Manager").AddComponent<GameManager>();
                gm.waves = GameManager.DefaultWaves();
            }
            gm.ratPrefab = prefabs.rat;
            gm.bossPrefab = prefabs.boss;
            gm.komur = komurController;
            gm.hud = hud;
            EnsureNavGrids(gm);
            EditorUtility.SetDirty(gm);
            return apothecaryGo;
        }

        private static void EnsureNavGrids(GameManager gm)
        {
            var grids = Object.FindObjectsByType<NavGrid>(FindObjectsSortMode.None);
            for (int f = 1; f <= 3; f++)
            {
                if (grids.Any(g => g.floor == f)) continue;
                var go = new GameObject($"NavGrid Floor {f}");
                go.transform.SetParent(gm.transform, false);
                go.AddComponent<NavGrid>().floor = f;
            }
        }

        private struct PrefabSet
        {
            public Rat rat;
            public Rat boss;
            public GameObject villager;
            public GameObject komur;
            public GameObject ratHole;
        }

        private static PrefabSet BuildPrefabsAndSettings()
        {
            EnsureFolders();
            int charactersLayer = EnsureLayer("Characters");
            // Cainos floors use physics layers 20-22; name them so StairsLayerTrigger can find them.
            EnsureLayerAt(20, "Layer 1");
            EnsureLayerAt(21, "Layer 2");
            EnsureLayerAt(22, "Layer 3");
            GameManager.ConfigurePhysicsLayers();
            ConfigureProjectSettings();

            var square = EnsurePlaceholderSprite("square", false);
            var circle = EnsurePlaceholderSprite("circle", true);
            var stonePrefab = BuildStonePrefab(circle);
            return new PrefabSet
            {
                rat = BuildRatPrefab(circle, charactersLayer),
                boss = BuildBossPrefab(charactersLayer),
                villager = BuildVillagerPrefab(square, stonePrefab, charactersLayer),
                komur = BuildKomurPrefab(LayerMask.NameToLayer("Layer 1"), charactersLayer),
                ratHole = BuildRatHolePrefab(circle),
            };
        }

        private static GameObject BuildRatHolePrefab(Sprite circle)
        {
            var go = new GameObject("Rat Hole");
            go.transform.localScale = new Vector3(0.7f, 0.4f, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = circle;
            sr.color = new Color(0.08f, 0.06f, 0.05f, 0.9f);
            sr.sortingLayerName = CharacterSortingLayer;
            sr.sortingOrder = 1;
            go.AddComponent<FloorAgent>();
            go.AddComponent<RatHole>().visual = sr;
            return SavePrefab(go, "Rat Hole");
        }

        // ---------- Prefabs ----------

        private static ThrownStone BuildStonePrefab(Sprite circle)
        {
            var go = new GameObject("Stone");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = circle;
            sr.color = new Color(0.55f, 0.55f, 0.6f);
            sr.sortingLayerName = CharacterSortingLayer;
            sr.sortingOrder = 20;
            go.transform.localScale = Vector3.one * 0.2f;
            go.AddComponent<ThrownStone>();
            return SavePrefab(go, "Stone").GetComponent<ThrownStone>();
        }

        private const string MonstersRoot = "Assets/ThirdPartyAssets/Monsters Creatures Fantasy 2/Sprites";

        private static Rat BuildRatPrefab(Sprite circle, int layer)
        {
            var run = SheetFrames(MonstersRoot + "/Rat/run.png");
            var go = BuildEnemyBase("Rat", layer, 0.16f);
            var visual = go.transform.Find("Visual");
            var sr = visual.GetComponent<SpriteRenderer>();

            if (run.Length > 0)
            {
                // Monsters Creatures Fantasy 2 rat: 70px frames at 100 PPU, faces right.
                visual.localPosition = new Vector3(0f, 0.23f, 0f);
                visual.localScale = Vector3.one * 2f;
                var flipbook = visual.gameObject.AddComponent<SpriteFlipbook>();
                flipbook.defaultClip = "Run";
                flipbook.clips = new[]
                {
                    new SpriteFlipbook.Clip { name = "Run", frames = run, fps = 14f },
                    new SpriteFlipbook.Clip { name = "Idle", frames = SheetFrames(MonstersRoot + "/Rat/idle.png"), fps = 10f },
                    new SpriteFlipbook.Clip { name = "Bite", frames = SheetFrames(MonstersRoot + "/Rat/attack_bite.png"), fps = 24f, loop = false },
                };
                sr.sprite = run[0];
            }
            else
            {
                visual.localScale = new Vector3(0.45f, 0.28f, 1f);
                sr.sprite = circle;
                sr.color = new Color(0.36f, 0.27f, 0.22f);
            }

            var rat = go.AddComponent<Rat>();
            rat.visual = sr;
            rat.flipbook = visual.GetComponent<SpriteFlipbook>();
            return SavePrefab(go, "Rat").GetComponent<Rat>();
        }

        // Final-wave boss: the Plague Slime from Monsters Creatures Fantasy 2.
        private static Rat BuildBossPrefab(int layer)
        {
            var walk = SheetFrames(MonstersRoot + "/Slime/walk.png");
            if (walk.Length == 0) return null;
            var go = BuildEnemyBase("Plague Slime", layer, 0.5f);
            var visual = go.transform.Find("Visual");
            visual.localPosition = new Vector3(0f, 0.9f, 0f);
            visual.localScale = Vector3.one * 6f;
            var sr = visual.GetComponent<SpriteRenderer>();
            sr.sprite = walk[0];
            var flipbook = visual.gameObject.AddComponent<SpriteFlipbook>();
            flipbook.defaultClip = "Run";
            flipbook.clips = new[]
            {
                new SpriteFlipbook.Clip { name = "Run", frames = walk, fps = 8f },
                new SpriteFlipbook.Clip { name = "Idle", frames = SheetFrames(MonstersRoot + "/Slime/idle.png"), fps = 10f },
                new SpriteFlipbook.Clip { name = "Bite", frames = SheetFrames(MonstersRoot + "/Slime/attack.png"), fps = 24f, loop = false },
            };
            var boss = go.AddComponent<Rat>();
            boss.visual = sr;
            boss.flipbook = flipbook;
            boss.isBoss = true;
            return SavePrefab(go, "Plague Slime").GetComponent<Rat>();
        }

        private static GameObject BuildEnemyBase(string name, int layer, float radius)
        {
            var go = new GameObject(name) { layer = layer };
            var body = AddBody(go, radius);
            body.mass = 0.2f;
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<PathMover>();
            go.AddComponent<FloorAgent>();
            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = CharacterSortingLayer;
            sr.sortingOrder = 2;
            return go;
        }

        private static Sprite[] SheetFrames(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<Sprite>()
                .OrderBy(s => int.TryParse(s.name.Substring(s.name.LastIndexOf('_') + 1), out int n) ? n : 0)
                .ToArray();
        }

        private static GameObject BuildVillagerPrefab(Sprite square, ThrownStone stone, int layer)
        {
            var go = new GameObject("Villager") { layer = layer };
            AddBody(go, 0.2f);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<PathMover>();
            go.AddComponent<FloorAgent>();

            var pfPlayer = AssetDatabase.LoadAssetAtPath<GameObject>(CainosRoot + "/Prefab/Player/PF Player.prefab");
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(pfPlayer);
            PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            visual.name = "Visual";
            visual.transform.SetParent(go.transform, false);
            visual.transform.localPosition = Vector3.zero;
            foreach (var mb in visual.GetComponentsInChildren<MonoBehaviour>())
                if (mb.GetType().Name == "TopDownCharacterController") Object.DestroyImmediate(mb);
            foreach (var c in visual.GetComponentsInChildren<Collider2D>()) Object.DestroyImmediate(c);
            foreach (var rb in visual.GetComponentsInChildren<Rigidbody2D>()) Object.DestroyImmediate(rb);

            // Sickness timer bar.
            var barGo = new GameObject("Sick Bar");
            barGo.transform.SetParent(go.transform, false);
            barGo.transform.localPosition = new Vector3(0f, 1.7f, 0f);
            var bg = AddSprite(barGo.transform, "Background", square, new Color(0f, 0f, 0f, 0.7f), new Vector3(0.86f, 0.14f, 1f), 30);
            var fill = AddSprite(barGo.transform, "Fill", square, Color.green, new Vector3(0.8f, 0.08f, 1f), 31);
            var bar = barGo.AddComponent<WorldBar>();
            bar.background = bg;
            bar.fill = fill;
            bar.width = 0.8f;
            bar.colors = new Gradient();
            bar.colors.SetKeys(
                new[] { new GradientColorKey(Color.red, 0f), new GradientColorKey(Color.yellow, 0.5f), new GradientColorKey(Color.green, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });

            var icon = AddWorldText(go.transform, "Icon", "", 5f, new Vector3(0f, 2.1f, 0f), Color.white);

            var villager = go.AddComponent<Villager>();
            villager.animator = visual.GetComponent<Animator>();
            villager.sickBar = bar;
            villager.icon = icon;
            villager.visualRoot = visual;
            villager.stonePrefab = stone;
            villager.gravestonePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CainosRoot + "/Prefab/Props/PF Props - Gravestone 01.prefab");
            return SavePrefab(go, "Villager");
        }

        private static GameObject BuildKomurPrefab(int layer, int charactersLayer)
        {
            var go = new GameObject("Komur") { layer = Mathf.Max(0, layer) };
            AddBody(go, 0.28f);
            go.GetComponent<Collider2D>().excludeLayers = 1 << charactersLayer;
            go.AddComponent<SpriteRenderer>(); // Cainos StairsLayerTrigger needs one on the root

            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            visual.transform.localScale = Vector3.one * 5f;
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = CharacterSortingLayer;
            sr.sortingOrder = 2;
            var flipbook = visual.AddComponent<SpriteFlipbook>();
            flipbook.clips = new[]
            {
                new SpriteFlipbook.Clip { name = "Idle", frames = CatFrames("Idle"), fps = 8f },
                new SpriteFlipbook.Clip { name = "Run", frames = CatFrames("Run"), fps = 14f },
                new SpriteFlipbook.Clip { name = "Pounce", frames = CatFrames("Run"), fps = 28f },
                new SpriteFlipbook.Clip { name = "Stunned", frames = CatFrames("Sitting"), fps = 6f },
                new SpriteFlipbook.Clip { name = "Sit", frames = CatFrames("Sitting"), fps = 5f },
            };
            sr.sprite = flipbook.clips[0].frames.FirstOrDefault();

            var medicine = AddWorldText(go.transform, "Medicine Icon", "+", 7f, new Vector3(0f, 1.6f, 0f), new Color(0.3f, 1f, 0.4f));

            var komur = go.AddComponent<KomurController>();
            komur.flipbook = flipbook;
            komur.visual = sr;
            komur.medicineIcon = medicine.gameObject;
            return SavePrefab(go, "Komur");
        }

        private static GameObject BuildApothecary(Vector3 position)
        {
            var go = new GameObject("Apothecary");
            go.transform.position = position;
            go.AddComponent<Apothecary>();
            var altar = AssetDatabase.LoadAssetAtPath<GameObject>(CainosRoot + "/Prefab/Props/PF Props - Altar 01.prefab");
            if (altar != null)
            {
                var a = (GameObject)PrefabUtility.InstantiatePrefab(altar, go.transform);
                a.transform.localPosition = Vector3.zero;
            }
            AddWorldText(go.transform, "Label", "APOTHECARY", 3.5f, new Vector3(0f, 1.6f, 0f), Color.white);
            AddWorldText(go.transform, "Cross", "+", 8f, new Vector3(0f, 2.3f, 0f), new Color(0.3f, 1f, 0.4f));
            return go;
        }

        // ---------- Feedback defaults (Epic Toon FX) ----------

        private static void ConfigureDefaultFeedbacks(GameFeedbacks feedbacks)
        {
            feedbacks.EnsureAllEntries();
            SetFeedback(feedbacks, FeedbackId.Pounce, "Environment/Dust/DustDirtyPoofSoft.prefab", 0.3f, 0f);
            SetFeedback(feedbacks, FeedbackId.RatKilled, "Combat/Explosions (Misc)/HitDustExplosion.prefab", 0.35f, 0.08f);
            SetFeedback(feedbacks, FeedbackId.Bite, "Environment/Smoke/StinkyCloud.prefab", 0.3f, 0.05f);
            SetFeedback(feedbacks, FeedbackId.Cured, "Interactive/Hearts/HeartPoof.prefab", 0.4f, 0f);
            SetFeedback(feedbacks, FeedbackId.VillagerDied, "Environment/Smoke/Dark/SmokeBurstDark.prefab", 0.5f, 0.15f);
            SetFeedback(feedbacks, FeedbackId.Stunned, "Interactive/Stars/StarPoof.prefab", 0.35f, 0.1f);
            SetFeedback(feedbacks, FeedbackId.MedicinePicked, "Interactive/Sparkle/SparkleSolo/SparkleSoloGreen.prefab", 0.5f, 0f);
            ConfigureBattleTexts(feedbacks);
        }

        private static void SetFeedback(GameFeedbacks feedbacks, FeedbackId id, string etfxPath, float scale, float shake)
        {
            var entry = feedbacks.entries.First(e => e.id == id);
            entry.vfxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EtfxRoot + "/" + etfxPath);
            entry.vfxScale = scale;
            entry.cameraShake = shake;
            if (entry.vfxPrefab == null) Debug.LogWarning($"[Kömür] Epic Toon FX prefab not found: {etfxPath}");
        }

        // ---------- Helpers ----------

        private static Rigidbody2D AddBody(GameObject go, float radius)
        {
            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = radius;
            return body;
        }

        private static SpriteRenderer AddSprite(Transform parent, string name, Sprite sprite, Color color, Vector3 scale, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingLayerName = CharacterSortingLayer;
            sr.sortingOrder = order;
            return sr;
        }

        private static TextMeshPro AddWorldText(Transform parent, string name, string text, float size, Vector3 localPos, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = WorldFont();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.rectTransform.sizeDelta = new Vector2(4f, 1f);
            tmp.outlineWidth = 0.2f;
            tmp.outlineColor = Color.black;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sortingLayerName = CharacterSortingLayer;
            mr.sortingOrder = 40;
            return tmp;
        }

        // World-space labels and icons use the PixelBattleText pixel font so they match the battle texts.
        private static TMP_FontAsset WorldFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/ThirdPartyAssets/PixelBattleText/Fonts/Alphapix.asset");
            return font != null ? font : TMP_Settings.defaultFontAsset;
        }

        private static Sprite[] CatFrames(string anim)
        {
            string path = $"{CatRoot}/Cat-2-{anim}.png";
            return AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<Sprite>()
                .OrderBy(s => int.TryParse(s.name.Substring(s.name.LastIndexOf('_') + 1), out int n) ? n : 0)
                .ToArray();
        }

        private static GameObject SavePrefab(GameObject go, string name)
        {
            string path = $"{PrefabDir}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static MonoBehaviour FindBehaviour(string typeName)
        {
            return Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .FirstOrDefault(m => m != null && m.GetType().Name == typeName);
        }

        private static Vector3 FindSpot(Vector3 center, float minRadius, float maxRadius, List<Vector3> used, float separation)
        {
            var groundMaps = Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None)
                .Where(tm => tm.GetComponent<TilemapRenderer>() is TilemapRenderer r && r.sortingLayerName == CharacterSortingLayer)
                .ToArray();

            for (int attempt = 0; attempt < 400; attempt++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                Vector3 p = center + (Vector3)(dir * Random.Range(minRadius, maxRadius));
                if (used.Any(u => (u - p).sqrMagnitude < separation * separation)) continue;
                if (groundMaps.Length > 0 && !groundMaps.Any(tm => tm.HasTile(tm.WorldToCell(p)))) continue;
                if (Physics2D.OverlapCircleAll(p, 0.6f).Any(c => !c.isTrigger)) continue;
                used.Add(p);
                return p;
            }

            Vector3 fallback = center + (Vector3)(Random.insideUnitCircle.normalized * minRadius);
            used.Add(fallback);
            return fallback;
        }

        private static void EnsureFolders()
        {
            foreach (var dir in new[] { GameRoot + "/Scenes", PrefabDir, PlaceholderDir })
                Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();
        }

        private static Sprite EnsurePlaceholderSprite(string name, bool circle)
        {
            string path = $"{PlaceholderDir}/{name}.png";
            if (!File.Exists(path))
            {
                const int size = 32;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var center = new Vector2(size / 2f - 0.5f, size / 2f - 0.5f);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool inside = !circle || Vector2.Distance(new Vector2(x, y), center) <= size / 2f - 0.5f;
                    tex.SetPixel(x, y, inside ? Color.white : Color.clear);
                }
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureType != TextureImporterType.Sprite || importer.spritePixelsPerUnit != 32f)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 32f;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static int EnsureLayer(string layerName)
        {
            int existing = LayerMask.NameToLayer(layerName);
            if (existing >= 0) return existing;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                var layer = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(layer.stringValue)) continue;
                layer.stringValue = layerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                return i;
            }
            Debug.LogError("[Kömür] No free layer slot for " + layerName);
            return 0;
        }

        private static void EnsureLayerAt(int index, string layerName)
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layer = tagManager.FindProperty("layers").GetArrayElementAtIndex(index);
            if (layer.stringValue == layerName) return;
            if (!string.IsNullOrEmpty(layer.stringValue))
            {
                Debug.LogWarning($"[Kömür] Layer {index} is '{layer.stringValue}', expected '{layerName}'. Leaving it.");
                return;
            }
            layer.stringValue = layerName;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureProjectSettings()
        {
            // Y-sort sprites so characters walk behind/in front of props correctly.
            var renderer2D = AssetDatabase.LoadMainAssetAtPath("Assets/Settings/Renderer2D.asset");
            if (renderer2D != null)
            {
                var so = new SerializedObject(renderer2D);
                var mode = so.FindProperty("m_TransparencySortMode");
                var axis = so.FindProperty("m_TransparencySortAxis");
                if (mode != null) mode.intValue = (int)TransparencySortMode.CustomAxis;
                if (axis != null) axis.vector3Value = new Vector3(0f, 1f, 0f);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // WebGL that runs on itch.io without server configuration.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            AssetDatabase.SaveAssets();
        }

        private static void AddSceneToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != path).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
