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

            var L = LoadLayout();
            var ink = L.Ink;
            var cream = L.Cream;
            var tan = TanPanelSprite();
            var playSprite = SpriteFrom($"{SproutRoot}/Sprite sheets/UI Big Play Button.png", "UI Big Play Button_2");
            var playPressed = SpriteFrom($"{SproutRoot}/Sprite sheets/UI Big Play Button.png", "UI Big Play Button_3");
            var blankButton = SpriteFrom($"{SproutRoot}/Sprite sheets/UI Big Play Button.png", "UI Big Play Button_0");
            var blankPressed = SpriteFrom($"{SproutRoot}/Sprite sheets/UI Big Play Button.png", "UI Big Play Button_1");
            var basicSheet = $"{SproutRoot}/Sprite sheets/Sprite sheet for Basic Pack.png";
            var iconSheet = $"{SproutRoot}/Sprite sheets/Icons/All Icons.png";
            sliceScale = L.panelSliceScale;
            scaler.referenceResolution = new Vector2(L.referenceWidth, L.referenceHeight);

            // --- Gameplay HUD (faded out on the title screen) ---
            var gameplay = Stretch(NewRect("Gameplay", root));
            hud.gameplayGroup = gameplay.gameObject.AddComponent<CanvasGroup>();
            hud.gameplayGroup.blocksRaycasts = false;

            // Arrows sit under the panels so they never cover them, and they route around them.
            var arrowsRect = Stretch(NewRect("Offscreen Indicators", gameplay));
            var arrows = arrowsRect.gameObject.AddComponent<OffscreenIndicators>();
            arrows.container = arrowsRect;
            arrows.arrowSprite = SpriteFrom(basicSheet, "Sprite sheet for Basic Pack_5");
            arrows.sickIcon = SpriteFrom(iconSheet, "All Icons_42");
            arrows.apothecaryIcon = SpriteFrom(iconSheet, "All Icons_44");
            arrows.backgroundSprite = tan;
            arrows.size = L.indicator.size;
            arrows.edgePadding = L.indicator.edgePadding;

            // Wave panel
            var wave = Panel(gameplay, "Wave Panel", tan, L.wavePanel);
            hud.waveText = FitText(wave, "Wave", "WAVE 1/5", L.wavePanel.fontSize, TextAlignmentOptions.Center, ink, Inset(L.wavePanel));

            // Town panel: villagers / sick / lost, each with an icon you can swap
            var town = Panel(gameplay, "Town Panel", tan, L.townPanel);
            float icon = L.townPanel.iconSize, pad = Inset(L.townPanel), fs = L.townPanel.fontSize;
            hud.aliveIcon = CounterIcon(town, 0, SpriteFrom($"{CainosRoot}/Texture/TX Player.png", "TX Player F"), Color.white, icon, pad);
            hud.aliveText = CounterText(town, 0, ink, icon, fs);
            hud.sickIcon = CounterIcon(town, 1, SpriteFrom(iconSheet, "All Icons_42"), new Color(0.3f, 0.75f, 0.25f), icon, pad);
            hud.sickText = CounterText(town, 1, new Color(0.25f, 0.55f, 0.2f), icon, fs);
            hud.lostIcon = CounterIcon(town, 2, SpriteFrom(iconSheet, "All Icons_2"), new Color(0.6f, 0.25f, 0.25f), icon, pad);
            hud.lostText = CounterText(town, 2, new Color(0.6f, 0.2f, 0.2f), icon, fs);

            // Minimap: parchment frame; the map sits in an inset area so the border stays visible
            var mm = Panel(gameplay, "Minimap", tan, L.minimap);
            var mapArea = Stretch(NewRect("Map Area", mm), Inset(L.minimap));
            var mapRect = Stretch(NewRect("Map", mapArea));
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
            minimap.villagerSize = 8f;
            minimap.ratSize = 6f;
            minimap.iconSize = 14f;
            arrows.avoid = new[] { wave, town, mm };

            // Hint
            hud.hintText = UIText(gameplay, "Hint", "", L.hint.fontSize, TextAlignmentOptions.Bottom, L.hint.Anchor, L.hint.Pos, L.hint.Size, cream);
            Outline(hud.hintText);

            // Banner A: short parchment strip
            var banner = Panel(root, "Banner", tan, L.banner);
            hud.bannerRoot = banner.gameObject;
            hud.bannerText = FitText(banner, "Text", "", L.banner.fontSize, TextAlignmentOptions.Center, ink, Inset(L.banner));

            // --- Title screen (Classic) ---
            var title = Stretch(NewRect("Title Screen", root));
            hud.introPanel = title.gameObject;
            var titleText = UIText(title, "Title", "KOMUR", L.title.fontSize, TextAlignmentOptions.Center, L.title.Anchor, L.title.Pos, L.title.Size, cream);
            Outline(titleText, 0.3f);
            var subtitle = UIText(title, "Subtitle", "AND THE PLAGUE RATS", L.subtitle.fontSize, TextAlignmentOptions.Center, L.subtitle.Anchor, L.subtitle.Pos, L.subtitle.Size, new Color(0.66f, 0.95f, 0.5f));
            Outline(subtitle);
            var story = Panel(title, "Story Panel", tan, L.storyPanel);
            var storyText = FitText(story, "Story",
                "THE TOWN SAYS BLACK CATS BRING BAD LUCK.\nBUT THE PLAGUE RATS ARE COMING - ONLY KOMUR CAN STOP THEM.",
                L.storyPanel.fontSize, TextAlignmentOptions.Center, ink, Inset(L.storyPanel));
            storyText.fontSizeMin = L.storyPanel.fontSize * 0.7f;
            ControlsStrip(title, L.controlsPanel);
            hud.playButton = UIButton(title, "Play Button", playSprite, playPressed, L.playButton.Anchor, L.playButton.Pos, L.playButton.Size, null, ink);
            AssignHelpButton(hud);

            // --- End screen: laid out like the title screen (the ending scene plays behind it) ---
            var end = Stretch(NewRect("End Screen", root));
            hud.endTitle = UIText(end, "Title", "", L.title.fontSize, TextAlignmentOptions.Center, L.title.Anchor, L.title.Pos, L.title.Size, cream);
            hud.endTitle.enableAutoSizing = true;
            hud.endTitle.fontSizeMin = L.title.fontSize * 0.45f;
            hud.endTitle.fontSizeMax = L.title.fontSize;
            hud.endTitle.textWrappingMode = TextWrappingModes.NoWrap;
            Outline(hud.endTitle, 0.3f);
            hud.endSubtitle = UIText(end, "Subtitle", "", L.subtitle.fontSize, TextAlignmentOptions.Center, L.subtitle.Anchor, L.subtitle.Pos, L.subtitle.Size, new Color(0.66f, 0.95f, 0.5f));
            Outline(hud.endSubtitle);
            var endPanel = Panel(end, "Story Panel", tan, L.storyPanel);
            hud.endBody = FitText(endPanel, "Body", "", L.storyPanel.fontSize, TextAlignmentOptions.Center, ink, Inset(L.storyPanel));
            hud.endBody.fontSizeMin = L.storyPanel.fontSize * 0.6f;
            hud.againButton = UIButton(end, "Menu Button", blankButton, blankPressed, L.playButton.Anchor, L.playButton.Pos, L.againButton.Size, "MAIN MENU", ink);
            hud.endPanel = end.gameObject;
            end.gameObject.SetActive(false);

            ApplyCameraLayout(L);
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
            SetBattleText(feedbacks, FeedbackId.MedicinePicked, "", "premium", 1f);     // the orbiting potion says it
            SetBattleText(feedbacks, FeedbackId.VillagerScared, "EEK!", "spooky", 1.6f);
            SetBattleText(feedbacks, FeedbackId.Won, "SAVED!", "lvlUp", 1.2f);
            SetBattleText(feedbacks, FeedbackId.HoleOpened, "RAT HOLE!", "venom", 1f);
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

        // ---------- Title screen controls: pixel keyboard keys ----------

        private const string KeysRoot = "Assets/ThirdPartyAssets/Controllers and Keyboard";
        private const int KeyCell = 16;

        // Dark see-through strip above the story panel, white labels: a W/ASD cluster laid out like the
        // keyboard + MOVE, [SPACE] POUNCE, [P] PAUSE. Keys tap themselves (W-A-S-D in sequence, SPACE on a beat).
        private static void ControlsStrip(Transform parent, HudLayout.Element e)
        {
            if (e == null) return;
            string lettersPath = KeysRoot + "/kb_light_alphanumeric.png", symbolsPath = KeysRoot + "/kb_light_symbols.png";
            MakePixelArt(lettersPath);
            MakePixelArt(symbolsPath);
            var letters = AssetDatabase.LoadAssetAtPath<Texture2D>(lettersPath);
            var symbols = AssetDatabase.LoadAssetAtPath<Texture2D>(symbolsPath);

            var strip = UIImage(parent, "Controls", null, e.Anchor, e.Pos, e.Size);
            strip.GetComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.5f);

            var keys = NewRect("Keys", strip);
            keys.anchorMin = Vector2.zero;
            keys.anchorMax = Vector2.one;
            keys.offsetMin = new Vector2(e.padding, e.padding);
            keys.offsetMax = new Vector2(-e.padding, -e.padding);
            var row = keys.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter;
            row.spacing = 8f;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;

            float fs = e.fontSize;
            // Key art is 9x11 px inside each 16x16 cell (the spacebar 14x11): crop to it so keys sit close.
            var keyArt = new RectInt(3, 2, 9, 11);
            var spaceArt = new RectInt(1, 2, 14, 11);
            float kh = Mathf.Min(e.iconSize, (e.Size.y - e.padding * 2f - 3f) / 2f);
            if (letters != null)
                KeyCluster(keys, "WASD", letters, letters, 'W' - 'A', 'A' - 'A', 'S' - 'A', 'D' - 'A', keyArt, kh, 0f);
            if (symbols != null)
            {
                KeyLabel(keys, "OR", fs * 0.8f);
                KeyCluster(keys, "Arrows", symbols, symbols, 12, 13, 14, 15, keyArt, kh, 0.9f, column: 4);
            }
            KeyLabel(keys, "MOVE", fs);
            KeyGap(keys, 56f);
            if (symbols != null) Key(keys, symbols, 0, 8, spaceArt, kh, 0.3f, 1.1f);          // spacebar
            KeyLabel(keys, "POUNCE", fs);
            KeyGap(keys, 56f);
            if (letters != null) Key(keys, letters, 0, 'P' - 'A', keyArt, kh, 0.9f, 2.4f);
            KeyLabel(keys, "PAUSE", fs);
        }

        // Up key on top, left / down / right underneath, packed like on a keyboard.
        private static void KeyCluster(Transform row, string name, Texture2D sheetUp, Texture2D sheet, int up, int left, int down, int right,
            RectInt art, float height, float phase, int column = 0)
        {
            const float gap = 3f;
            float width = height * art.width / art.height;
            var cluster = NewRect(name, row);
            var le = cluster.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            le.preferredWidth = width * 3f + gap * 2f;
            le.preferredHeight = height * 2f + gap;
            foreach (var (sheetRow, i, col, line) in new[] { (up, 0, 1, 0), (left, 1, 0, 1), (down, 2, 1, 1), (right, 3, 2, 1) })
            {
                var key = Key(cluster, line == 0 ? sheetUp : sheet, column, sheetRow, art, height, phase + i * 0.22f, 1.6f);
                key.anchorMin = key.anchorMax = key.pivot = new Vector2(0f, 1f);
                key.anchoredPosition = new Vector2(col * (width + gap), -line * (height + gap));
                key.sizeDelta = new Vector2(width, height);
            }
        }

        // One key: the art cropped out of its cell (normal = this column, pressed = two columns right).
        private static RectTransform Key(Transform row, Texture2D sheet, int column, int sheetRow, RectInt art, float height, float phase, float period)
        {
            var rt = NewRect("Key", row);
            var raw = rt.gameObject.AddComponent<UnityEngine.UI.RawImage>();
            raw.texture = sheet;
            raw.raycastTarget = false;
            Rect Uv(int col) => new Rect((col * KeyCell + art.x) / (float)sheet.width,
                1f - (sheetRow * KeyCell + art.y + art.height) / (float)sheet.height,
                art.width / (float)sheet.width, art.height / (float)sheet.height);
            var press = rt.gameObject.AddComponent<KeyPress>();
            press.normalUv = Uv(column);
            press.pressedUv = Uv(column + 2);
            press.phase = phase;
            press.period = period;
            raw.uvRect = press.normalUv;
            var le = rt.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            le.preferredHeight = height;
            le.preferredWidth = height * art.width / art.height;
            return rt;
        }

        private static void KeyLabel(Transform row, string text, float size)
        {
            var label = UIText(row, text, text, size, TextAlignmentOptions.Left, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Color.white);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.margin = new Vector4(6f, 0f, 0f, 0f);
        }

        private static void KeyGap(Transform row, float width)
        {
            var le = NewRect("Gap", row).gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            le.preferredWidth = width;
        }

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
            Vector2 anchor, Vector2 position, Vector2 size, string label, Color labelColor)
        {
            var rt = Place(NewRect(name, parent), anchor, position, size);
            var image = rt.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = normal;
            var button = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.transition = UnityEngine.UI.Selectable.Transition.SpriteSwap;
            button.spriteState = new UnityEngine.UI.SpriteState { pressedSprite = pressed, highlightedSprite = pressed, selectedSprite = normal };
            if (!string.IsNullOrEmpty(label))
                UIText(rt, "Label", label, size.y * 0.6f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, 4), size, labelColor);
            return button;
        }

        private static float sliceScale = 3f;
        private const string LayoutPath = GameRoot + "/UI/hud-layout.json";

        private static HudLayout LoadLayout()
        {
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(LayoutPath);
            if (json == null) throw new System.Exception("[Kömür] Missing " + LayoutPath);
            return HudLayout.FromJson(json.text);
        }

        // Content inset of a panel: the 9-slice border plus the element's extra padding (same rule as hud-spec.html).
        private static float Inset(HudLayout.Element e) => 7f * sliceScale + e.padding;

        private static void ApplyCameraLayout(HudLayout L)
        {
            var follow = Object.FindFirstObjectByType<CameraFollow2D>();
            if (follow != null) { follow.gameplaySize = L.camera.gameplaySize; EditorUtility.SetDirty(follow); }
            var gm = Object.FindFirstObjectByType<GameManager>();
            if (gm != null) { gm.titleCameraSize = L.camera.titleSize; EditorUtility.SetDirty(gm); }
            var feedbacks = Object.FindFirstObjectByType<GameFeedbacks>();
            if (feedbacks != null) { feedbacks.battleTextPixelSize = L.battleText.pixelSize; EditorUtility.SetDirty(feedbacks); }
        }

        // Sprout Lands tan panel, 9-sliced with chunky pixel borders (panelSliceScale x the source pixels).
        private static RectTransform Panel(Transform parent, string name, Sprite sprite, HudLayout.Element e)
        {
            var rt = UIImage(parent, name, sprite, e.Anchor, e.Pos, e.Size);
            var image = rt.GetComponent<UnityEngine.UI.Image>();
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f / sliceScale;
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
        private static UnityEngine.UI.Image CounterIcon(RectTransform panel, int column, Sprite sprite, Color color, float iconSize, float padding)
        {
            var cell = NewRect("Counter " + column, panel);
            cell.anchorMin = new Vector2(column / 3f, 0f);
            cell.anchorMax = new Vector2((column + 1) / 3f, 1f);
            float border = 7f * sliceScale;
            cell.offsetMin = new Vector2(column == 0 ? padding : 2f, border);
            cell.offsetMax = new Vector2(column == 2 ? -padding : -2f, -border);
            var icon = Place(NewRect("Icon", cell), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(iconSize, iconSize));
            var image = icon.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI CounterText(RectTransform panel, int column, Color color, float iconSize, float fontSize)
        {
            var cell = (RectTransform)panel.Find("Counter " + column);
            var tmp = UIText(cell, "Count", "0", fontSize, TextAlignmentOptions.Left, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero, color);
            var rt = tmp.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(iconSize + 4f, 0f);
            rt.offsetMax = Vector2.zero;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 10f;
            tmp.fontSizeMax = fontSize;
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
