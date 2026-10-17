using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TextCore.LowLevel;

namespace Komur.EditorTools
{
    // Builds the HUD: title screen, status panel, minimap, off-screen arrows, banner, end screen and the
    // PixelBattleText layer. Art: Sprout Lands UI (panels, buttons, icons, font) + Complete UI Essential (frame, banner).
    public static partial class KomurSceneBuilder
    {
        private const string SproutRoot = "Assets/ThirdPartyAssets/Sprout Lands - UI Pack - Basic pack";
        private const string FlatRoot = "Assets/ThirdPartyAssets/Complete_UI_Essential_Pack_Free/01_Flat_Theme/Sprites";
        private const string BattleTextRoot = "Assets/ThirdPartyAssets/PixelBattleText/Animation Presets";
        private const string GeneratedRoot = "Assets/ThirdPartyAssets/_Generated";

        private static readonly Color UiInk = new Color(0.36f, 0.22f, 0.16f);   // Sprout Lands brown
        private static readonly Color UiCream = new Color(1f, 0.95f, 0.82f);

        [MenuItem("Kömür/Rebuild HUD (keeps scene)", priority = 4)]
        public static void RebuildHud()
        {
            foreach (var old in Object.FindObjectsByType<Hud>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(old.gameObject);
            foreach (var old in Object.FindObjectsByType<PixelBattleText.PixelBattleTextController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (old != null) Object.DestroyImmediate(old.GetComponentInParent<Canvas>().gameObject);
            var hud = BuildHud();

            var gm = Object.FindFirstObjectByType<GameManager>();
            if (gm != null)
            {
                gm.hud = hud;
                EditorUtility.SetDirty(gm);
            }
            var feedbacks = Object.FindFirstObjectByType<GameFeedbacks>();
            if (feedbacks != null)
            {
                feedbacks.EnsureAllEntries();
                ConfigureBattleTexts(feedbacks);
                EditorUtility.SetDirty(feedbacks);
            }

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeObject = hud.gameObject;
            Debug.Log("[Kömür] HUD rebuilt.");
        }

        private static Hud BuildHud()
        {
            var canvasGo = new GameObject("HUD", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            var hud = canvasGo.AddComponent<Hud>();
            var root = (RectTransform)canvasGo.transform;

            var tan = TanPanelSprite();
            var playSprite = SpriteFrom($"{SproutRoot}/Sprite sheets/UI Big Play Button.png", "UI Big Play Button_2");
            var playPressed = SpriteFrom($"{SproutRoot}/Sprite sheets/UI Big Play Button.png", "UI Big Play Button_3");
            var blankButton = SpriteFrom($"{SproutRoot}/Sprite sheets/UI Big Play Button.png", "UI Big Play Button_0");
            var blankPressed = SpriteFrom($"{SproutRoot}/Sprite sheets/UI Big Play Button.png", "UI Big Play Button_1");
            var basicSheet = $"{SproutRoot}/Sprite sheets/Sprite sheet for Basic Pack.png";
            var iconSheet = $"{SproutRoot}/Sprite sheets/Icons/All Icons.png";

            // --- Gameplay HUD (faded out on the title screen) ---
            var gameplay = Stretch(NewRect("Gameplay", root));
            hud.gameplayGroup = gameplay.gameObject.AddComponent<CanvasGroup>();
            hud.gameplayGroup.blocksRaycasts = false;

            var arrowsRect = Stretch(NewRect("Offscreen Indicators", gameplay));
            var arrows = arrowsRect.gameObject.AddComponent<OffscreenIndicators>();
            arrows.container = arrowsRect;
            arrows.arrowSprite = SpriteFrom(basicSheet, "Sprite sheet for Basic Pack_5");
            arrows.sickIcon = SpriteFrom(iconSheet, "All Icons_42");
            arrows.apothecaryIcon = SpriteFrom(iconSheet, "All Icons_44");
            arrows.backgroundSprite = tan;

            // Wave panel (top-left)
            var wave = Panel(gameplay, "Wave Panel", tan, new Vector2(0, 1), new Vector2(28, -24), new Vector2(300, 96));
            hud.waveText = FitText(wave, "Wave", "WAVE 1/5", 56, TextAlignmentOptions.Center, UiInk, 20f);

            // Town panel (under the wave panel): villagers / sick / lost, each with an icon you can swap
            var town = Panel(gameplay, "Town Panel", tan, new Vector2(0, 1), new Vector2(28, -128), new Vector2(300, 84));
            hud.aliveIcon = CounterIcon(town, 0, SpriteFrom($"{CainosRoot}/Texture/TX Player.png", "TX Player F"), Color.white);
            hud.aliveText = CounterText(town, 0, UiInk);
            hud.sickIcon = CounterIcon(town, 1, SpriteFrom(iconSheet, "All Icons_42"), new Color(0.3f, 0.75f, 0.25f));
            hud.sickText = CounterText(town, 1, new Color(0.25f, 0.55f, 0.2f));
            hud.lostIcon = CounterIcon(town, 2, SpriteFrom(iconSheet, "All Icons_2"), new Color(0.6f, 0.25f, 0.25f));
            hud.lostText = CounterText(town, 2, new Color(0.6f, 0.2f, 0.2f));

            // Minimap (top-right): parchment frame around a map painted from the tiles
            var mm = Panel(gameplay, "Minimap", tan, new Vector2(1, 1), new Vector2(-28, -24), new Vector2(432, 300));
            var mapRect = Stretch(NewRect("Map", mm), 24f);
            var map = mapRect.gameObject.AddComponent<UnityEngine.UI.RawImage>();
            map.raycastTarget = false;
            var fitter = mapRect.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
            fitter.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 1.5f;
            var markers = Stretch(NewRect("Markers", mapRect));
            var minimap = mm.gameObject.AddComponent<Minimap>();
            minimap.mapImage = map;
            minimap.markerLayer = markers;
            minimap.dotSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderDir + "/circle.png");
            minimap.komurSprite = CatFrames("Idle").FirstOrDefault();
            minimap.sickSprite = SpriteFrom(iconSheet, "All Icons_42");
            minimap.deadSprite = SpriteFrom(iconSheet, "All Icons_2");
            minimap.apothecarySprite = SpriteFrom(iconSheet, "All Icons_44");

            // Hint (bottom-centre)
            hud.hintText = UIText(gameplay, "Hint", "", 34, TextAlignmentOptions.Bottom, new Vector2(0.5f, 0), new Vector2(0, 36), new Vector2(1500, 60), UiCream);
            Outline(hud.hintText);

            // Banner A: parchment strip
            var banner = Panel(root, "Banner", tan, new Vector2(0.5f, 0.5f), new Vector2(0, 250), new Vector2(900, 150));
            hud.bannerRoot = banner.gameObject;
            hud.bannerText = FitText(banner, "Text", "", 64, TextAlignmentOptions.Center, UiInk, 26f);

            // --- Title screen (Classic): title on top, Kömür sitting in the middle, story strip, Play ---
            var title = Stretch(NewRect("Title Screen", root));
            hud.introPanel = title.gameObject;
            var titleText = UIText(title, "Title", "KOMUR", 160, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(1400, 190), UiCream);
            Outline(titleText, 0.3f);
            var subtitle = UIText(title, "Subtitle", "AND THE PLAGUE RATS", 56, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0, -230), new Vector2(1400, 80), new Color(0.66f, 0.95f, 0.5f));
            Outline(subtitle);
            var story = Panel(title, "Story Panel", tan, new Vector2(0.5f, 0), new Vector2(0, 170), new Vector2(1180, 170));
            var storyText = FitText(story, "Story",
                "THE TOWN SAYS BLACK CATS BRING BAD LUCK.\nBUT THE PLAGUE RATS ARE COMING - ONLY KOMUR CAN STOP THEM.",
                34, TextAlignmentOptions.Center, UiInk, 24f);
            SetAnchors(storyText.rectTransform, new Vector2(0, 0.42f), Vector2.one, 24f);
            var controls = FitText(story, "Controls",
                "WASD MOVE   SPACE POUNCE   BRING MEDICINE TO THE SICK   STAY OUT OF THE VILLAGERS' SIGHT",
                24, TextAlignmentOptions.Center, UiInk * 0.8f, 24f);
            SetAnchors(controls.rectTransform, Vector2.zero, new Vector2(1, 0.42f), 24f);
            hud.playButton = UIButton(title, "Play Button", playSprite, playPressed, new Vector2(0.5f, 0), new Vector2(0, 46), new Vector2(92, 29) * 3.4f, null);

            // --- End screen ---
            var end = Stretch(NewRect("End Screen", root));
            var dim = end.gameObject.AddComponent<UnityEngine.UI.Image>();
            dim.color = new Color(0.05f, 0.04f, 0.08f, 0.6f);
            hud.endTitle = UIText(end, "Title", "", 96, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, 250), new Vector2(1600, 130), UiCream);
            Outline(hud.endTitle, 0.3f);
            var endPanel = Panel(end, "Panel", tan, new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(1100, 300));
            hud.endBody = FitText(endPanel, "Body", "", 40, TextAlignmentOptions.Center, UiInk, 30f);
            hud.againButton = UIButton(end, "Again Button", blankButton, blankPressed, new Vector2(0.5f, 0.5f), new Vector2(0, -210), new Vector2(92, 29) * 3.4f, "AGAIN");
            hud.endPanel = end.gameObject;
            end.gameObject.SetActive(false);

            BuildBattleTextCanvas();
            EnsureEventSystem();
            return hud;
        }

        // ---------- Battle texts ----------

        // PixelBattleText is designed for a low-resolution canvas: 480x270 makes its 16px letters crisp and readable.
        private static void BuildBattleTextCanvas()
        {
            var go = new GameObject("Battle Text Canvas", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;
            var scaler = go.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(480f, 270f);
            scaler.matchWidthOrHeight = 0.5f;
            var layer = Stretch(NewRect("Battle Text Layer", go.transform));
            var controller = layer.gameObject.AddComponent<PixelBattleText.PixelBattleTextController>();
            controller.canvas = layer;
            controller.snapToPixelGrid = true;
        }

        private static void ConfigureBattleTexts(GameFeedbacks feedbacks)
        {
            SetBattleText(feedbacks, FeedbackId.Bite, "BITE!", "venom", 1.2f);
            SetBattleText(feedbacks, FeedbackId.RatKilled, "SQUEAK!", "crit", 0.6f);
            SetBattleText(feedbacks, FeedbackId.Cured, "CURED!", "healing", 1.4f);
            SetBattleText(feedbacks, FeedbackId.VillagerDied, "R.I.P.", "KO", 1.2f);
            SetBattleText(feedbacks, FeedbackId.Stunned, "OUCH!", "shock", 1f);
            SetBattleText(feedbacks, FeedbackId.MedicinePicked, "MEDICINE", "premium", 1f);
            SetBattleText(feedbacks, FeedbackId.VillagerScared, "EEK!", "spooky", 1.6f);
            SetBattleText(feedbacks, FeedbackId.Won, "SAVED!", "lvlUp", 1.2f);
        }

        private static void SetBattleText(GameFeedbacks feedbacks, FeedbackId id, string text, string preset, float height)
        {
            var entry = feedbacks.entries.FirstOrDefault(e => e.id == id);
            if (entry == null) return;
            entry.battleText = text;
            entry.battleTextAnimation = AssetDatabase.LoadAssetAtPath<PixelBattleText.TextAnimation>($"{BattleTextRoot}/textAnim_{preset}.asset");
            entry.battleTextHeight = height;
        }

        // ---------- UI helpers ----------

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static RectTransform Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        // Anchor and pivot at the same point; position is relative to it.
        private static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        private static RectTransform UIImage(Transform parent, string name, Sprite sprite, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rt = Place(NewRect(name, parent), anchor, position, size);
            var image = rt.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            if (sprite == null) image.color = new Color(0.2f, 0.15f, 0.1f, 0.85f);
            return rt;
        }

        private static TextMeshProUGUI UIText(Transform parent, string name, string text, float size, TextAlignmentOptions align,
            Vector2 anchor, Vector2 position, Vector2 box, Color color)
        {
            var rt = Place(NewRect(name, parent), anchor, position, box);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = UiFont();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = color;
            tmp.raycastTarget = false;
            tmp.richText = true;
            return tmp;
        }

        private static UnityEngine.UI.Button UIButton(Transform parent, string name, Sprite normal, Sprite pressed,
            Vector2 anchor, Vector2 position, Vector2 size, string label)
        {
            var rt = Place(NewRect(name, parent), anchor, position, size);
            var image = rt.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = normal;
            var button = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.transition = UnityEngine.UI.Selectable.Transition.SpriteSwap;
            button.spriteState = new UnityEngine.UI.SpriteState { pressedSprite = pressed, highlightedSprite = pressed, selectedSprite = normal };
            if (!string.IsNullOrEmpty(label))
                UIText(rt, "Label", label, 52, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, 4), size, UiInk);
            return button;
        }

        // Sprout Lands tan panel, 9-sliced with chunky 3x pixel borders.
        private static RectTransform Panel(Transform parent, string name, Sprite sprite, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rt = UIImage(parent, name, sprite, anchor, position, size);
            var image = rt.GetComponent<UnityEngine.UI.Image>();
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f / 3f;
            return rt;
        }

        // Text that fills its parent (minus padding) and shrinks to fit.
        private static TextMeshProUGUI FitText(Transform parent, string name, string text, float maxSize, TextAlignmentOptions align, Color color, float padding)
        {
            var tmp = UIText(parent, name, text, maxSize, align, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, color);
            SetAnchors(tmp.rectTransform, Vector2.zero, Vector2.one, padding);
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 12f;
            tmp.fontSizeMax = maxSize;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            return tmp;
        }

        private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max, float padding)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
        }

        // One of three equal columns in the town panel: icon on the left, number on the right.
        private static UnityEngine.UI.Image CounterIcon(RectTransform panel, int column, Sprite sprite, Color color)
        {
            var cell = NewRect("Counter " + column, panel);
            cell.anchorMin = new Vector2(column / 3f, 0f);
            cell.anchorMax = new Vector2((column + 1) / 3f, 1f);
            cell.offsetMin = new Vector2(column == 0 ? 20f : 4f, 18f);
            cell.offsetMax = new Vector2(column == 2 ? -20f : -4f, -18f);
            var icon = Place(NewRect("Icon", cell), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(40f, 40f));
            var image = icon.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI CounterText(RectTransform panel, int column, Color color)
        {
            var cell = (RectTransform)panel.Find("Counter " + column);
            var tmp = UIText(cell, "Count", "0", 36, TextAlignmentOptions.Left, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero, color);
            var rt = tmp.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(46f, 0f);
            rt.offsetMax = Vector2.zero;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 12f;
            tmp.fontSizeMax = 36f;
            return tmp;
        }

        private static Sprite TanPanelSprite()
        {
            string sheet = $"{SproutRoot}/Sprite sheets/Sprite sheet for Basic Pack.png";
            const string name = "Sprite sheet for Basic Pack_53";
            EnsureSpriteBorder(sheet, name, new Vector4(7, 7, 7, 7));
            return SpriteFrom(sheet, name);
        }

        // 9-slice borders live in the pack's import settings (git-ignored), so the builder sets them itself.
        private static void EnsureSpriteBorder(string texturePath, string spriteName, Vector4 border)
        {
            var factory = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
            factory.Init();
            var importer = AssetImporter.GetAtPath(texturePath);
            if (importer == null) return;
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var rects = provider.GetSpriteRects();
            bool changed = false;
            foreach (var r in rects)
            {
                if (r.name != spriteName || r.border == border) continue;
                r.border = border;
                changed = true;
            }
            if (!changed) return;
            provider.SetSpriteRects(rects);
            provider.Apply();
            importer.SaveAndReimport();
        }

        private static void Outline(TextMeshProUGUI tmp, float width = 0.2f)
        {
            tmp.outlineWidth = width;
            tmp.outlineColor = new Color32(40, 25, 20, 255);
        }

        private static Sprite SpriteFrom(string path, string name = null)
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
            if (sprites.Length == 0)
            {
                Debug.LogWarning("[Kömür] No sprites in " + path);
                return null;
            }
            return name == null ? sprites[0] : sprites.FirstOrDefault(s => s.name == name) ?? sprites[0];
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static TMP_FontAsset cachedUiFont;

        // Dynamic TMP font from the Sprout Lands pixel font, with the TMP default font as fallback (for Ö, Ü...).
        // Generated into the git-ignored third-party folder.
        private static TMP_FontAsset UiFont()
        {
            if (cachedUiFont != null) return cachedUiFont;
            string path = GeneratedRoot + "/SproutLands UI.asset";
            cachedUiFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (cachedUiFont != null) return cachedUiFont;

            var ttf = AssetDatabase.LoadAssetAtPath<Font>($"{SproutRoot}/fonts/pixelFont-7-8x14-sproutLands.ttf");
            if (ttf == null) return cachedUiFont = TMP_Settings.defaultFontAsset;

            Directory.CreateDirectory(GeneratedRoot);
            var font = TMP_FontAsset.CreateFontAsset(ttf, 48, 6, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            font.name = "SproutLands UI";
            if (TMP_Settings.defaultFontAsset != null)
                font.fallbackFontAssetTable = new List<TMP_FontAsset> { TMP_Settings.defaultFontAsset };
            AssetDatabase.CreateAsset(font, path);
            font.atlasTextures[0].name = "SproutLands UI Atlas";
            AssetDatabase.AddObjectToAsset(font.atlasTextures[0], font);
            font.material.name = "SproutLands UI Material";
            AssetDatabase.AddObjectToAsset(font.material, font);
            AssetDatabase.SaveAssets();
            return cachedUiFont = font;
        }
    }
}
