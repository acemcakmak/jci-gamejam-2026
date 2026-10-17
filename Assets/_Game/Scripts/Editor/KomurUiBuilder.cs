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

            var panelSprite = SpriteFrom($"{SproutRoot}/Sprite sheets/Dialouge UI/dialog box big.png");
            var bigPanelSprite = SpriteFrom($"{SproutRoot}/Sprite sheets/Dialouge UI/Premade dialog box  big.png");
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

            var battleLayer = Stretch(NewRect("Battle Text Layer", gameplay));
            var battle = battleLayer.gameObject.AddComponent<PixelBattleText.PixelBattleTextController>();
            battle.canvas = battleLayer;

            var arrowsRect = Stretch(NewRect("Offscreen Indicators", gameplay));
            var arrows = arrowsRect.gameObject.AddComponent<OffscreenIndicators>();
            arrows.container = arrowsRect;
            arrows.arrowSprite = SpriteFrom(basicSheet, "Sprite sheet for Basic Pack_5");
            arrows.sickIcon = SpriteFrom(iconSheet, "All Icons_42");
            arrows.apothecaryIcon = SpriteFrom(iconSheet, "All Icons_44");
            arrows.backgroundSprite = SpriteFrom(basicSheet, "Sprite sheet for Basic Pack_53");

            // Status panel (top-left)
            var status = UIImage(gameplay, "Status Panel", panelSprite, new Vector2(0, 1), new Vector2(28, -24), new Vector2(174, 37) * 3f);
            hud.waveText = UIText(status, "Wave", "", 44, TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(34, -14), new Vector2(460, 50), UiInk);
            hud.villagersText = UIText(status, "Villager Counts", "", 28, TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(34, -60), new Vector2(470, 40), UiInk);

            // Minimap (top-right)
            var frame = UIImage(gameplay, "Minimap", SpriteFrom($"{FlatRoot}/UI_Flat_Frame01a.png"), new Vector2(1, 1), new Vector2(-28, -24), new Vector2(96, 64) * 4f);
            var mapRect = Stretch(NewRect("Map", frame), 14f);
            var map = mapRect.gameObject.AddComponent<UnityEngine.UI.RawImage>();
            map.raycastTarget = false;
            var fitter = mapRect.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
            fitter.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 1.5f;
            var markers = Stretch(NewRect("Markers", mapRect));
            var minimap = frame.gameObject.AddComponent<Minimap>();
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

            // Banner (centre)
            var banner = UIImage(root, "Banner", SpriteFrom($"{FlatRoot}/UI_Flat_Banner01a.png"), new Vector2(0.5f, 0.5f), new Vector2(0, 230), new Vector2(64, 20) * 9f);
            hud.bannerRoot = banner.gameObject;
            hud.bannerText = UIText(banner, "Text", "", 64, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, 6), new Vector2(560, 120), UiCream);
            Outline(hud.bannerText);

            // --- Title screen: the map stays visible, Kömür sits in the square under the title ---
            var title = Stretch(NewRect("Title Screen", root));
            hud.introPanel = title.gameObject;
            var titleText = UIText(title, "Title", "KÖMÜR", 150, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(1400, 190), UiCream);
            Outline(titleText, 0.25f);
            var subtitle = UIText(title, "Subtitle", "and the Plague Rats", 54, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0, -235), new Vector2(1400, 80), new Color(0.6f, 1f, 0.5f));
            Outline(subtitle);
            var story = UIImage(title, "Story Panel", bigPanelSprite, new Vector2(0.5f, 0), new Vector2(0, 170), new Vector2(300, 62) * 3.2f);
            UIText(story, "Story",
                "The townsfolk say black cats bring bad luck.\nBut the plague rats are coming - only Kömür can save them.",
                30, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0, -26), new Vector2(880, 90), UiInk);
            UIText(story, "Controls",
                "WASD move    SPACE pounce    Bring medicine to the sick\nVillagers fear you - stay out of their sight!",
                24, TextAlignmentOptions.Center, new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(880, 70), UiInk * 0.85f);
            hud.playButton = UIButton(title, "Play Button", playSprite, playPressed, new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(92, 29) * 3.4f, null);

            // --- End screen ---
            var end = Stretch(NewRect("End Screen", root));
            var dim = end.gameObject.AddComponent<UnityEngine.UI.Image>();
            dim.color = new Color(0.05f, 0.04f, 0.08f, 0.6f);
            var endPanel = UIImage(end, "Panel", bigPanelSprite, new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(300, 62) * 4f);
            hud.endTitle = UIText(end, "Title", "", 96, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, 250), new Vector2(1600, 130), UiCream);
            Outline(hud.endTitle, 0.25f);
            hud.endBody = UIText(endPanel, "Body", "", 34, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100, 210), UiInk);
            hud.againButton = UIButton(end, "Again Button", blankButton, blankPressed, new Vector2(0.5f, 0.5f), new Vector2(0, -200), new Vector2(92, 29) * 3.4f, "AGAIN");
            hud.endPanel = end.gameObject;
            end.gameObject.SetActive(false);

            EnsureEventSystem();
            return hud;
        }

        // ---------- Battle texts ----------

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
