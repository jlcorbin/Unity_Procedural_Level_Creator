// InventoryUIBuilder.cs — builds the Candy Cloud Inventory + Forge UI.
//
// Creates (all idempotent — re-run to rebuild):
//   Assets/Data/UI/RarityPalette.asset
//   Assets/Prefabs/UI/Inventory/Cell_Item.prefab
//   Assets/Prefabs/UI/Inventory/Slot_Equipment.prefab
//   Assets/Prefabs/UI/Inventory/Row_ForgeMaterial.prefab
//   Assets/Prefabs/UI/Inventory/InventoryScreen.prefab
//   Assets/Prefabs/UI/Inventory/ForgeScreen.prefab
//
// Structure + colours come from the design handoff. Rounded corners use Unity's
// built-in UISprite (9-sliced). Gradients/soft shadows are approximated with
// flat tints — swap in real sprites during art polish.
//
// FONTS: uses the TMP default font. Import **Fredoka** + **Nunito** as TMP font
// assets and assign them to swap in the design typography.
//
// Menu: LevelGen ▶ UI ▶ Build Inventory + Forge UI

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LevelGen.UI;
using LevelGen.Items;

namespace LevelGen.UI.Editor
{
    public static class InventoryUIBuilder
    {
        private const string UiDataFolder = "Assets/Data/UI";
        private const string PrefabFolder = "Assets/Prefabs/UI/Inventory";
        private const string PalettePath = UiDataFolder + "/RarityPalette.asset";

        // ── Design tokens ───────────────────────────────────────────────────
        private static readonly Color ScreenBg = RarityPalette.Hex("#E4EDFB"); // approximates the sky→lavender gradient
        private static readonly Color PanelBg = Color.white;
        private static readonly Color Indigo = RarityPalette.Hex("#4B57C9");
        private static readonly Color TextStrong = RarityPalette.Hex("#2A2A2E");
        private static readonly Color TextMuted = RarityPalette.Hex("#7A7062");
        private static readonly Color TextFaint = RarityPalette.Hex("#8A8172");
        private static readonly Color BtnGreen = RarityPalette.Hex("#43C07B");
        private static readonly Color BtnGreenDark = RarityPalette.Hex("#2E8B4E");
        private static readonly Color SoftFill = new Color(0f, 0f, 0f, 0.035f);
        private static readonly Color GoldBg = RarityPalette.Hex("#FDF1DC");
        private static readonly Color GoldBorder = RarityPalette.Hex("#F0A32E");
        private static readonly Color GoldText = RarityPalette.Hex("#B9740A");
        private static readonly Color CloseBg = RarityPalette.Hex("#F5E3E3");
        private static readonly Color CloseFg = RarityPalette.Hex("#C05A5A");
        private static readonly Color DmgOrange = RarityPalette.Hex("#E0872E");
        private static readonly Color ArmorBlue = RarityPalette.Hex("#2E90E0");
        private static readonly Color HpRed = RarityPalette.Hex("#E0524E");
        private static readonly Color StamGreen = RarityPalette.Hex("#43B55F");

        private static Sprite Rounded =>
            AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        [MenuItem("LevelGen/UI/Build Inventory + Forge UI")]
        public static void BuildAll()
        {
            EnsureFolder(UiDataFolder);
            EnsureFolder(PrefabFolder);

            var palette = BuildPalette();
            var cell = BuildCellPrefab(palette);
            var slot = BuildSlotPrefab();
            var row = BuildMaterialRowPrefab();
            BuildInventoryScreen(palette, cell, slot);
            BuildForgeScreen(palette, row);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[InventoryUIBuilder] Built RarityPalette + Cell/Slot/Row prefabs + " +
                      "InventoryScreen + ForgeScreen in " + PrefabFolder + ".\n" +
                      "Next: place InventoryScreen in your scene (I key opens it), and import " +
                      "Fredoka/Nunito as TMP fonts to match the design typography.");
        }

        // ── Palette ─────────────────────────────────────────────────────────
        private static RarityPalette BuildPalette()
        {
            var p = AssetDatabase.LoadAssetAtPath<RarityPalette>(PalettePath);
            if (p == null)
            {
                p = ScriptableObject.CreateInstance<RarityPalette>();
                p.ResetToDesignDefaults();
                AssetDatabase.CreateAsset(p, PalettePath);
            }
            return p;
        }

        // ── Reusable prefabs ────────────────────────────────────────────────
        private static InventoryItemCell BuildCellPrefab(RarityPalette palette)
        {
            var root = UI("Cell_Item", null, 64, 64);
            var frame = AddPanel(root, Color.grey);
            var fill = AddChildPanel(root, "Fill", Color.white, 2f);

            var icon = UI("Icon", root.transform, 30, 30).AddComponent<Image>();
            icon.preserveAspect = true; icon.enabled = false;
            Center(icon.rectTransform);

            var iconText = MakeText("IconText", root.transform, "", 16, TextStrong, FontStyles.Bold);
            Stretch(iconText.rectTransform);

            var countRoot = UI("Count", root.transform, 26, 16);
            var countBg = countRoot.AddComponent<Image>();
            countBg.sprite = Rounded; countBg.type = Image.Type.Sliced;
            countBg.color = new Color(30f / 255f, 28f / 255f, 26f / 255f, 0.82f);
            var crt = countRoot.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(1, 0); crt.anchorMax = new Vector2(1, 0);
            crt.pivot = new Vector2(1, 0); crt.anchoredPosition = new Vector2(-3, 3);
            var countText = MakeText("CountText", countRoot.transform, "x1", 11, Color.white, FontStyles.Bold);
            Stretch(countText.rectTransform);

            var ring = UI("SelectRing", root.transform, 0, 0);
            var ringImg = ring.AddComponent<Image>();
            ringImg.sprite = Rounded; ringImg.type = Image.Type.Sliced;
            ringImg.color = TextStrong; ringImg.raycastTarget = false;
            var rrt = ring.GetComponent<RectTransform>();
            Stretch(rrt); rrt.offsetMin = new Vector2(-3, -3); rrt.offsetMax = new Vector2(3, 3);
            ring.transform.SetAsFirstSibling();   // behind the frame → reads as an outline
            ring.SetActive(false);

            var cell = root.AddComponent<InventoryItemCell>();
            Wire(cell, "frame", frame); Wire(cell, "fill", fill);
            Wire(cell, "icon", icon); Wire(cell, "iconText", iconText);
            Wire(cell, "countRoot", countRoot); Wire(cell, "countText", countText);
            Wire(cell, "selectRing", ring);

            return SavePrefab(root, $"{PrefabFolder}/Cell_Item.prefab").GetComponent<InventoryItemCell>();
        }

        private static EquipmentSlotView BuildSlotPrefab()
        {
            var root = UI("Slot_Equipment", null, 58, 58);
            var frame = AddPanel(root, Color.grey);
            var fill = AddChildPanel(root, "Fill", Color.white, 2f);

            var icon = UI("Icon", root.transform, 28, 28).AddComponent<Image>();
            icon.preserveAspect = true; icon.enabled = false;
            Center(icon.rectTransform);

            var iconText = MakeText("IconText", root.transform, "", 18, TextStrong, FontStyles.Bold);
            Stretch(iconText.rectTransform);

            // Label chip overlapping the bottom edge.
            var chip = UI("LabelChip", root.transform, 52, 14);
            var chipBg = chip.AddComponent<Image>();
            chipBg.sprite = Rounded; chipBg.type = Image.Type.Sliced; chipBg.color = Color.white;
            var chrt = chip.GetComponent<RectTransform>();
            chrt.anchorMin = new Vector2(0.5f, 0); chrt.anchorMax = new Vector2(0.5f, 0);
            chrt.pivot = new Vector2(0.5f, 0.5f); chrt.anchoredPosition = new Vector2(0, -2);
            var label = MakeText("Label", chip.transform, "SLOT", 7, TextFaint, FontStyles.Bold);
            Stretch(label.rectTransform);

            var lockBadge = UI("Lock", root.transform, 12, 12);
            var lockText = MakeText("LockGlyph", lockBadge.transform, "L", 9, TextFaint, FontStyles.Bold);
            Stretch(lockText.rectTransform);
            var lrt = lockBadge.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(1, 1); lrt.anchorMax = new Vector2(1, 1);
            lrt.pivot = new Vector2(1, 1); lrt.anchoredPosition = new Vector2(-2, -2);

            var view = root.AddComponent<EquipmentSlotView>();
            Wire(view, "frame", frame); Wire(view, "fill", fill);
            Wire(view, "icon", icon); Wire(view, "iconText", iconText);
            Wire(view, "label", label); Wire(view, "lockBadge", lockBadge);

            return SavePrefab(root, $"{PrefabFolder}/Slot_Equipment.prefab").GetComponent<EquipmentSlotView>();
        }

        private static ForgeMaterialRow BuildMaterialRowPrefab()
        {
            var root = UI("Row_ForgeMaterial", null, 320, 40);
            var h = root.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 9; h.childAlignment = TextAnchor.MiddleLeft;
            h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            h.padding = new RectOffset(4, 4, 2, 2);

            var tile = UI("Tile", root.transform, 34, 34);
            var tileFrame = AddPanel(tile, Color.grey);
            var tileFill = AddChildPanel(tile, "Fill", Color.white, 2f);
            var tileText = MakeText("TileText", tile.transform, "M", 14, TextStrong, FontStyles.Bold);
            Stretch(tileText.rectTransform);
            Fixed(tile, 34, 34);

            var nameText = MakeText("Name", root.transform, "Material - Common", 12, TextStrong, FontStyles.Bold, TextAlignmentOptions.Left);
            Flexible(nameText.gameObject, 1f);

            var countText = MakeText("Count", root.transform, "0/0", 15, TextStrong, FontStyles.Bold, TextAlignmentOptions.Right);
            Fixed(countText.gameObject, 52, 34);

            var dot = UI("StatusDot", root.transform, 20, 20);
            var dotImg = dot.AddComponent<Image>();
            dotImg.sprite = Rounded; dotImg.type = Image.Type.Sliced; dotImg.color = StamGreen;
            var glyph = MakeText("Glyph", dot.transform, "", 11, Color.white, FontStyles.Bold);
            Stretch(glyph.rectTransform);
            Fixed(dot, 20, 20);

            var row = root.AddComponent<ForgeMaterialRow>();
            Wire(row, "tileFrame", tileFrame); Wire(row, "tileFill", tileFill); Wire(row, "tileText", tileText);
            Wire(row, "nameText", nameText); Wire(row, "countText", countText);
            Wire(row, "statusDot", dotImg); Wire(row, "statusGlyph", glyph);

            return SavePrefab(root, $"{PrefabFolder}/Row_ForgeMaterial.prefab").GetComponent<ForgeMaterialRow>();
        }

        // ── Inventory screen ────────────────────────────────────────────────
        private static void BuildInventoryScreen(RarityPalette palette, InventoryItemCell cellPrefab, EquipmentSlotView slotPrefab)
        {
            var root = new GameObject("InventoryScreen", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // Panel root (toggled) — full-screen bg.
            var panelRoot = UI("PanelRoot", root.transform, 0, 0);
            Stretch(panelRoot.GetComponent<RectTransform>());
            var bg = panelRoot.AddComponent<Image>();
            bg.color = ScreenBg;

            var content = UI("Content", panelRoot.transform, 1700, 940);
            Center(content.GetComponent<RectTransform>());
            var vert = content.AddComponent<VerticalLayoutGroup>();
            vert.spacing = 14; vert.padding = new RectOffset(24, 24, 24, 24);
            vert.childForceExpandHeight = false; vert.childControlHeight = true;
            vert.childForceExpandWidth = true; vert.childControlWidth = true;

            // ── Title bar ──
            var bar = UI("TitleBar", content.transform, 0, 70);
            var barImg = bar.AddComponent<Image>();
            barImg.sprite = Rounded; barImg.type = Image.Type.Sliced; barImg.color = PanelBg;
            Fixed(bar, 0, 70);
            var barH = bar.AddComponent<HorizontalLayoutGroup>();
            barH.spacing = 12; barH.padding = new RectOffset(14, 14, 10, 10);
            barH.childAlignment = TextAnchor.MiddleCenter;
            barH.childForceExpandWidth = false; barH.childControlWidth = true;

            var gold = UI("GoldPill", bar.transform, 120, 34);
            var goldImg = gold.AddComponent<Image>();
            goldImg.sprite = Rounded; goldImg.type = Image.Type.Sliced; goldImg.color = GoldBg;
            var goldInner = AddChildPanel(gold, "Inner", GoldBg, 1.5f); goldInner.color = GoldBg;
            goldImg.color = GoldBorder;
            var goldText = MakeText("GoldText", gold.transform, "2,584", 14, GoldText, FontStyles.Bold);
            Stretch(goldText.rectTransform);
            Fixed(gold, 120, 34);

            var title = MakeText("Title", bar.transform, "Knight", 26, Indigo, FontStyles.Normal);
            Flexible(title.gameObject, 1f);

            var close = UI("CloseButton", bar.transform, 40, 40);
            var closeImg = close.AddComponent<Image>();
            closeImg.sprite = Rounded; closeImg.type = Image.Type.Sliced; closeImg.color = CloseBg;
            var closeBtn = close.AddComponent<Button>();
            var closeText = MakeText("X", close.transform, "X", 16, CloseFg, FontStyles.Bold);
            Stretch(closeText.rectTransform);
            Fixed(close, 40, 40);

            // ── Body ──
            var body = UI("Body", content.transform, 0, 0);
            var bodyH = body.AddComponent<HorizontalLayoutGroup>();
            bodyH.spacing = 14; bodyH.childForceExpandHeight = true; bodyH.childControlHeight = true;
            bodyH.childForceExpandWidth = true; bodyH.childControlWidth = true;
            Flexible(body, 1f);

            // Left: character panel
            var left = MakeRoundedPanel("CharacterPanel", body.transform, PanelBg);
            Flexible(left.gameObject, 1.25f);
            var leftV = left.gameObject.AddComponent<VerticalLayoutGroup>();
            leftV.spacing = 16; leftV.padding = new RectOffset(16, 16, 16, 16);
            leftV.childForceExpandHeight = false; leftV.childControlHeight = true;
            leftV.childForceExpandWidth = true; leftV.childControlWidth = true;

            var slotsRow = UI("SlotsRow", left.transform, 0, 0);
            var slotsH = slotsRow.AddComponent<HorizontalLayoutGroup>();
            slotsH.spacing = 12; slotsH.childAlignment = TextAnchor.MiddleCenter;
            slotsH.childForceExpandWidth = false; slotsH.childControlWidth = true;
            slotsH.childForceExpandHeight = true; slotsH.childControlHeight = true;
            Flexible(slotsRow, 1f);

            var slots = new List<EquipmentSlotView>();
            var colL = MakeSlotColumn("SlotsLeft", slotsRow.transform);
            var colR = MakeSlotColumn("SlotsRight", slotsRow.transform);

            // Hero preview placeholder between the columns.
            var hero = MakeRoundedPanel("HeroPreview", slotsRow.transform, new Color(0, 0, 0, 0.04f));
            Flexible(hero.gameObject, 1f);
            hero.transform.SetSiblingIndex(1);
            var heroText = MakeText("Hint", hero.transform, "CHIBI HERO\nrotatable 3D preview", 14, new Color(0, 0, 0, 0.42f));
            Stretch(heroText.rectTransform);

            // Left column: Armor + 3 locked. Right column: Melee, Off-hand, Ranged + locked Charm.
            slots.Add(MakeSlot(slotPrefab, colL, EquipSlot.Armor, "ARMOR", false));
            slots.Add(MakeSlot(slotPrefab, colL, EquipSlot.Armor, "HEAD", true));
            slots.Add(MakeSlot(slotPrefab, colL, EquipSlot.Armor, "CHEST", true));
            slots.Add(MakeSlot(slotPrefab, colL, EquipSlot.Armor, "FEET", true));
            slots.Add(MakeSlot(slotPrefab, colR, EquipSlot.Melee, "MELEE", false));
            slots.Add(MakeSlot(slotPrefab, colR, EquipSlot.OffHand, "OFF-HAND", false));
            slots.Add(MakeSlot(slotPrefab, colR, EquipSlot.Ranged, "RANGED", false));
            slots.Add(MakeSlot(slotPrefab, colR, EquipSlot.Armor, "CHARM", true));

            // Stats row
            var statsRow = UI("StatsRow", left.transform, 0, 90);
            var statsH = statsRow.AddComponent<HorizontalLayoutGroup>();
            statsH.spacing = 8; statsH.childForceExpandWidth = true; statsH.childControlWidth = true;
            statsH.childForceExpandHeight = true; statsH.childControlHeight = true;
            Fixed(statsRow, 0, 90);
            var dmgT = MakeStatCell(statsRow.transform, "DAMAGE", "0", DmgOrange);
            var armT = MakeStatCell(statsRow.transform, "ARMOR", "0", ArmorBlue);
            var hpT = MakeStatCell(statsRow.transform, "HP", "0", HpRed);
            var stamT = MakeStatCell(statsRow.transform, "STAMINA", "0", StamGreen);

            // Skins / Stats buttons
            var btnRow = UI("ButtonRow", left.transform, 0, 56);
            var btnH = btnRow.AddComponent<HorizontalLayoutGroup>();
            btnH.spacing = 8; btnH.childForceExpandWidth = true; btnH.childControlWidth = true;
            btnH.childForceExpandHeight = true; btnH.childControlHeight = true;
            Fixed(btnRow, 0, 56);
            MakeChunkyButton(btnRow.transform, "Skins");
            MakeChunkyButton(btnRow.transform, "Stats");

            // Right: items bag
            var right = MakeRoundedPanel("ItemsBagPanel", body.transform, PanelBg);
            Flexible(right.gameObject, 1f);
            var rightV = right.gameObject.AddComponent<VerticalLayoutGroup>();
            rightV.spacing = 12; rightV.padding = new RectOffset(14, 14, 14, 14);
            rightV.childForceExpandHeight = false; rightV.childControlHeight = true;
            rightV.childForceExpandWidth = true; rightV.childControlWidth = true;

            var bagTitle = MakeText("BagTitle", right.transform, "Items Bag", 20, Indigo, FontStyles.Normal, TextAlignmentOptions.Left);
            Fixed(bagTitle.gameObject, 0, 28);

            // Tabs
            var tabsRow = UI("Tabs", right.transform, 0, 40);
            var tabsH = tabsRow.AddComponent<HorizontalLayoutGroup>();
            tabsH.spacing = 6; tabsH.childForceExpandWidth = false; tabsH.childControlWidth = true;
            tabsH.childForceExpandHeight = true; tabsH.childControlHeight = true;
            Fixed(tabsRow, 0, 40);
            string[] tabNames = { "All", "Weapons", "Armor", "Materials", "Potions" };
            var tabButtons = new Button[5]; var tabLabels = new TMP_Text[5];
            for (int i = 0; i < 5; i++) MakeTab(tabsRow.transform, tabNames[i], i, tabButtons, tabLabels);

            // Grid
            var grid = UI("Grid", right.transform, 0, 420);
            var g = grid.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(84, 84); g.spacing = new Vector2(8, 8);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = 5;
            Fixed(grid, 0, 430);

            // Detail strip
            var detail = UI("DetailStrip", right.transform, 0, 62);
            var detailFrame = AddPanel(detail, Color.grey);
            var detailFill = AddChildPanel(detail, "Fill", Color.white, 2f);
            Fixed(detail, 0, 62);
            var detH = detail.AddComponent<HorizontalLayoutGroup>();
            detH.spacing = 10; detH.padding = new RectOffset(10, 10, 8, 8);
            detH.childAlignment = TextAnchor.MiddleLeft;
            detH.childForceExpandWidth = false; detH.childControlWidth = true;
            detH.childForceExpandHeight = false; detH.childControlHeight = true;

            var dIconTile = UI("IconTile", detail.transform, 44, 44);
            var dIconFrame = AddPanel(dIconTile, Color.grey);
            AddChildPanel(dIconTile, "Fill", Color.white, 2f);
            var dIcon = UI("Icon", dIconTile.transform, 26, 26).AddComponent<Image>();
            dIcon.preserveAspect = true; dIcon.enabled = false; Center(dIcon.rectTransform);
            var dIconText = MakeText("IconText", dIconTile.transform, "", 18, TextStrong, FontStyles.Bold);
            Stretch(dIconText.rectTransform);
            Fixed(dIconTile, 44, 44);

            var dInfo = UI("Info", detail.transform, 0, 0);
            var dInfoV = dInfo.AddComponent<VerticalLayoutGroup>();
            dInfoV.childForceExpandHeight = false; dInfoV.childControlHeight = true;
            dInfoV.childForceExpandWidth = true; dInfoV.childControlWidth = true;
            Flexible(dInfo, 1f);
            var dName = MakeText("Name", dInfo.transform, "-", 15, TextStrong, FontStyles.Bold, TextAlignmentOptions.Left);
            var dRarity = MakeText("Rarity", dInfo.transform, "", 11, TextMuted, FontStyles.Bold, TextAlignmentOptions.Left);

            var dDmgBox = UI("DmgBox", detail.transform, 60, 44);
            var dDmgV = dDmgBox.AddComponent<VerticalLayoutGroup>();
            dDmgV.childForceExpandHeight = false; dDmgV.childControlHeight = true;
            MakeText("DmgLabel", dDmgBox.transform, "DMG", 9, TextFaint, FontStyles.Bold);
            var dDmg = MakeText("DmgValue", dDmgBox.transform, "-", 20, DmgOrange, FontStyles.Normal);
            Fixed(dDmgBox, 60, 44);

            // Footer
            var footer = UI("Footer", right.transform, 0, 40);
            var footImg = footer.AddComponent<Image>();
            footImg.sprite = Rounded; footImg.type = Image.Type.Sliced; footImg.color = SoftFill;
            Fixed(footer, 0, 40);
            var footText = MakeText("FooterText", footer.transform, "ADD 5 SLOTS", 13, TextMuted, FontStyles.Bold);
            Stretch(footText.rectTransform);

            // ── Wire the controller ──
            var screen = root.AddComponent<InventoryScreen>();
            Wire(screen, "panelRoot", panelRoot);
            Wire(screen, "palette", palette);
            Wire(screen, "gridContainer", grid.transform);
            Wire(screen, "cellPrefab", cellPrefab);
            WireArray(screen, "tabButtons", tabButtons);
            WireArray(screen, "tabLabels", tabLabels);
            WireArray(screen, "equipmentSlots", slots.ToArray());
            Wire(screen, "detailFrame", detailFrame); Wire(screen, "detailFill", detailFill);
            Wire(screen, "detailIconFrame", dIconFrame); Wire(screen, "detailIcon", dIcon);
            Wire(screen, "detailIconText", dIconText);
            Wire(screen, "detailName", dName); Wire(screen, "detailRarity", dRarity);
            Wire(screen, "detailDamage", dDmg);
            Wire(screen, "damageText", dmgT); Wire(screen, "armorText", armT);
            Wire(screen, "hpText", hpT); Wire(screen, "staminaText", stamT);
            Wire(screen, "titleText", title); Wire(screen, "closeButton", closeBtn);

            SavePrefab(root, $"{PrefabFolder}/InventoryScreen.prefab");
        }

        // ── Forge screen (stub data, real layout) ───────────────────────────
        private static void BuildForgeScreen(RarityPalette palette, ForgeMaterialRow rowPrefab)
        {
            var root = new GameObject("ForgeScreen", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 61;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var panelRoot = UI("PanelRoot", root.transform, 0, 0);
            Stretch(panelRoot.GetComponent<RectTransform>());
            var dim = panelRoot.AddComponent<Image>();
            dim.color = new Color(0, 0, 0, 0.35f);

            var card = MakeRoundedPanel("ForgeCard", panelRoot.transform, PanelBg);
            var crt = card.rectTransform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(1000, 780);
            var cardV = card.gameObject.AddComponent<VerticalLayoutGroup>();
            cardV.spacing = 12; cardV.padding = new RectOffset(20, 20, 18, 18);
            cardV.childForceExpandHeight = false; cardV.childControlHeight = true;
            cardV.childForceExpandWidth = true; cardV.childControlWidth = true;

            // Header
            var header = UI("Header", card.transform, 0, 36);
            var headH = header.AddComponent<HorizontalLayoutGroup>();
            headH.childForceExpandWidth = false; headH.childControlWidth = true;
            headH.childAlignment = TextAnchor.MiddleLeft;
            Fixed(header, 0, 36);
            var headerText = MakeText("HeaderText", header.transform, "Weapon Forge", 22, Indigo, FontStyles.Normal, TextAlignmentOptions.Left);
            Flexible(headerText.gameObject, 1f);
            var pill = UI("StatePill", header.transform, 170, 30);
            var pillImg = pill.AddComponent<Image>();
            pillImg.sprite = Rounded; pillImg.type = Image.Type.Sliced;
            var pillText = MakeText("PillText", pill.transform, "Ready to forge", 12, TextStrong, FontStyles.Bold);
            Stretch(pillText.rectTransform);
            Fixed(pill, 170, 30);

            // Before → after
            var cmp = UI("Comparison", card.transform, 0, 260);
            var cmpH = cmp.AddComponent<HorizontalLayoutGroup>();
            cmpH.spacing = 10; cmpH.childForceExpandWidth = true; cmpH.childControlWidth = true;
            cmpH.childForceExpandHeight = true; cmpH.childControlHeight = true;
            Fixed(cmp, 0, 260);

            MakeForgeCard(cmp.transform, "Current", "CURRENT DMG", TextMuted,
                out var curFrame, out var curFill, out var curIconFrame, out var curLevel,
                out var curIconText, out var curName, out var curRarity, out var curDmg, out _);

            var arrow = MakeText("Arrow", cmp.transform, ">", 30, GoldBorder);
            Fixed(arrow.gameObject, 60, 0);

            MakeForgeCard(cmp.transform, "Next", "AFTER UPGRADE", StamGreen,
                out var nextFrame, out var nextFill, out var nextIconFrame, out var nextLevel,
                out var nextIconText, out var nextName, out var nextRarity, out var nextDmg, out var nextDelta);

            // Rarity ladder
            var ladder = UI("RarityLadder", card.transform, 0, 34);
            var ladH = ladder.AddComponent<HorizontalLayoutGroup>();
            ladH.spacing = 6; ladH.childAlignment = TextAnchor.MiddleCenter;
            ladH.childForceExpandWidth = false; ladH.childControlWidth = true;
            Fixed(ladder, 0, 34);
            var ladderPills = new Image[4]; var ladderLabels = new TMP_Text[4];
            for (int i = 0; i < 4; i++)
            {
                var p = UI($"Pill{i}", ladder.transform, 110, 28);
                var pi = p.AddComponent<Image>(); pi.sprite = Rounded; pi.type = Image.Type.Sliced;
                var pt = MakeText("Label", p.transform, ((ItemRarity)i).ToString(), 11, TextStrong, FontStyles.Bold);
                Stretch(pt.rectTransform);
                Fixed(p, 110, 28);
                ladderPills[i] = pi; ladderLabels[i] = pt;
            }

            // Materials
            var matsBox = UI("Materials", card.transform, 0, 190);
            var matsImg = matsBox.AddComponent<Image>();
            matsImg.sprite = Rounded; matsImg.type = Image.Type.Sliced; matsImg.color = SoftFill;
            Fixed(matsBox, 0, 190);
            var matsV = matsBox.AddComponent<VerticalLayoutGroup>();
            matsV.spacing = 9; matsV.padding = new RectOffset(12, 12, 10, 10);
            matsV.childForceExpandHeight = false; matsV.childControlHeight = true;
            matsV.childForceExpandWidth = true; matsV.childControlWidth = true;
            MakeText("MatsLabel", matsBox.transform, "MATERIALS REQUIRED", 10, TextFaint, FontStyles.Bold, TextAlignmentOptions.Left);
            var matsList = UI("List", matsBox.transform, 0, 0);
            var listV = matsList.AddComponent<VerticalLayoutGroup>();
            listV.spacing = 9; listV.childForceExpandHeight = false; listV.childControlHeight = true;
            listV.childForceExpandWidth = true; listV.childControlWidth = true;
            Flexible(matsList, 1f);

            // Upgrade button
            var btn = UI("UpgradeButton", card.transform, 0, 64);
            var btnImg = btn.AddComponent<Image>();
            btnImg.sprite = Rounded; btnImg.type = Image.Type.Sliced; btnImg.color = BtnGreen;
            var btnComp = btn.AddComponent<Button>();
            var btnLabel = MakeText("Label", btn.transform, "UPGRADE", 18, Color.white, FontStyles.Bold);
            Stretch(btnLabel.rectTransform);
            Fixed(btn, 0, 64);
            var subNote = MakeText("SubNote", card.transform, "", 11, StamGreen, FontStyles.Normal);
            Fixed(subNote.gameObject, 0, 20);
            var vfx = UI("VfxAnchor", card.transform, 0, 0).GetComponent<RectTransform>();

            var forge = root.AddComponent<ForgeScreen>();
            Wire(forge, "panelRoot", panelRoot); Wire(forge, "palette", palette);
            Wire(forge, "headerText", headerText); Wire(forge, "statePill", pillImg); Wire(forge, "statePillText", pillText);
            Wire(forge, "curFrame", curFrame); Wire(forge, "curFill", curFill); Wire(forge, "curIconFrame", curIconFrame);
            Wire(forge, "curLevelBadge", curLevel); Wire(forge, "curIconText", curIconText);
            Wire(forge, "curName", curName); Wire(forge, "curRarityPill", curRarity); Wire(forge, "curDamage", curDmg);
            Wire(forge, "nextFrame", nextFrame); Wire(forge, "nextFill", nextFill); Wire(forge, "nextIconFrame", nextIconFrame);
            Wire(forge, "nextLevelBadge", nextLevel); Wire(forge, "nextIconText", nextIconText);
            Wire(forge, "nextName", nextName); Wire(forge, "nextRarityPill", nextRarity);
            Wire(forge, "nextDamage", nextDmg); Wire(forge, "nextDelta", nextDelta);
            WireArray(forge, "ladderPills", ladderPills); WireArray(forge, "ladderLabels", ladderLabels);
            Wire(forge, "materialsContainer", matsList.transform); Wire(forge, "materialRowPrefab", rowPrefab);
            Wire(forge, "upgradeButton", btnComp); Wire(forge, "upgradeButtonImage", btnImg);
            Wire(forge, "upgradeLabel", btnLabel); Wire(forge, "upgradeSubNote", subNote);
            Wire(forge, "vfxAnchor", vfx);
            SeedForgeStubs(forge);

            SavePrefab(root, $"{PrefabFolder}/ForgeScreen.prefab");
        }

        /// <summary>Fills the two stub view-models so the Forge renders without real data.</summary>
        private static void SeedForgeStubs(ForgeScreen forge)
        {
            var so = new SerializedObject(forge);
            SeedVm(so.FindProperty("stubEnough"), 8, 4, 3);
            SeedVm(so.FindProperty("stubShort"), 8, 4, 1);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SeedVm(SerializedProperty vm, int have1, int have2, int have3)
        {
            if (vm == null) return;
            vm.FindPropertyRelative("weaponName").stringValue = "Ember Sword";
            vm.FindPropertyRelative("level").intValue = 2;
            vm.FindPropertyRelative("rarity").enumValueIndex = (int)ItemRarity.Rare;
            vm.FindPropertyRelative("currentDamage").intValue = 37;
            vm.FindPropertyRelative("nextLevel").intValue = 3;
            vm.FindPropertyRelative("nextRarity").enumValueIndex = (int)ItemRarity.Legendary;
            vm.FindPropertyRelative("nextDamage").intValue = 45;

            var mats = vm.FindPropertyRelative("materials");
            mats.ClearArray();
            AddMat(mats, "Mat 1", ItemRarity.Common, have1, 5);
            AddMat(mats, "Mat 2", ItemRarity.Rare, have2, 3);
            AddMat(mats, "Mat 3", ItemRarity.Legendary, have3, 2);
        }

        private static void AddMat(SerializedProperty arr, string name, ItemRarity rarity, int have, int need)
        {
            int i = arr.arraySize; arr.InsertArrayElementAtIndex(i);
            var e = arr.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("displayName").stringValue = name;
            e.FindPropertyRelative("rarity").enumValueIndex = (int)rarity;
            e.FindPropertyRelative("have").intValue = have;
            e.FindPropertyRelative("need").intValue = need;
        }

        // ── Small builders ──────────────────────────────────────────────────
        private static Transform MakeSlotColumn(string name, Transform parent)
        {
            var col = UI(name, parent, 70, 0);
            var v = col.AddComponent<VerticalLayoutGroup>();
            v.spacing = 20; v.childAlignment = TextAnchor.MiddleCenter;
            v.childForceExpandWidth = false; v.childControlWidth = true;
            v.childForceExpandHeight = false; v.childControlHeight = false;
            Fixed(col, 70, 0);
            return col.transform;
        }

        private static EquipmentSlotView MakeSlot(EquipmentSlotView prefab, Transform parent, EquipSlot slot, string label, bool locked)
        {
            var inst = (EquipmentSlotView)PrefabUtility.InstantiatePrefab(prefab, parent);
            inst.Configure(slot, label, locked);
            EditorUtility.SetDirty(inst);
            return inst;
        }

        private static TMP_Text MakeStatCell(Transform parent, string label, string value, Color valueColor)
        {
            var cell = UI($"Stat_{label}", parent, 0, 0);
            var img = cell.AddComponent<Image>();
            img.sprite = Rounded; img.type = Image.Type.Sliced; img.color = SoftFill;
            var v = cell.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(6, 6, 9, 9); v.spacing = 3;
            v.childAlignment = TextAnchor.MiddleCenter;
            v.childForceExpandHeight = false; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childControlWidth = true;
            var val = MakeText("Value", cell.transform, value, 24, valueColor, FontStyles.Normal);
            MakeText("Label", cell.transform, label, 10, TextFaint, FontStyles.Bold);
            return val;
        }

        private static void MakeChunkyButton(Transform parent, string label)
        {
            var shadow = UI($"Btn_{label}", parent, 0, 0);
            var sImg = shadow.AddComponent<Image>();
            sImg.sprite = Rounded; sImg.type = Image.Type.Sliced; sImg.color = BtnGreenDark;
            var face = UI("Face", shadow.transform, 0, 0);
            var fImg = face.AddComponent<Image>();
            fImg.sprite = Rounded; fImg.type = Image.Type.Sliced; fImg.color = BtnGreen;
            var frt = face.GetComponent<RectTransform>();
            Stretch(frt); frt.offsetMin = new Vector2(0, 4); frt.offsetMax = new Vector2(0, 0);
            face.AddComponent<Button>();
            var t = MakeText("Label", face.transform, label, 16, Color.white, FontStyles.Bold);
            Stretch(t.rectTransform);
        }

        private static void MakeTab(Transform parent, string name, int index, Button[] buttons, TMP_Text[] labels)
        {
            var tab = UI($"Tab_{name}", parent, 120, 0);
            var img = tab.AddComponent<Image>();
            img.sprite = Rounded; img.type = Image.Type.Sliced; img.color = new Color(0, 0, 0, 0.05f);
            var btn = tab.AddComponent<Button>();
            var txt = MakeText("Label", tab.transform, name, 14, TextMuted, FontStyles.Bold);
            Stretch(txt.rectTransform);
            Fixed(tab, 120, 0);
            buttons[index] = btn; labels[index] = txt;
        }

        private static Image MakeForgeCard(Transform parent, string name, string dmgLabel, Color dmgColor,
            out Image frame, out Image fill, out Image iconFrame, out TMP_Text level, out TMP_Text iconText,
            out TMP_Text nameText, out TMP_Text rarityPill, out TMP_Text damage, out TMP_Text delta)
        {
            var card = UI($"Card_{name}", parent, 0, 0);
            frame = AddPanel(card, Color.grey);
            fill = AddChildPanel(card, "Fill", Color.white, 2f);
            var v = card.AddComponent<VerticalLayoutGroup>();
            v.spacing = 6; v.padding = new RectOffset(12, 12, 12, 12);
            v.childAlignment = TextAnchor.UpperCenter;
            v.childForceExpandHeight = false; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childControlWidth = true;

            level = MakeText("Level", card.transform, "+0", 14, TextStrong, FontStyles.Bold, TextAlignmentOptions.Left);
            var tile = UI("IconTile", card.transform, 60, 60);
            iconFrame = AddPanel(tile, Color.grey);
            AddChildPanel(tile, "Fill", Color.white, 2f);
            iconText = MakeText("IconText", tile.transform, "?", 24, TextStrong, FontStyles.Bold);
            Stretch(iconText.rectTransform);
            Fixed(tile, 60, 60);

            nameText = MakeText("Name", card.transform, "Weapon", 14, TextStrong, FontStyles.Bold);
            rarityPill = MakeText("Rarity", card.transform, "Common", 12, TextMuted, FontStyles.Bold);
            MakeText("DmgLabel", card.transform, dmgLabel, 9, TextFaint, FontStyles.Bold);
            damage = MakeText("Damage", card.transform, "0", 34, dmgColor, FontStyles.Normal);
            delta = MakeText("Delta", card.transform, "", 14, StamGreen, FontStyles.Bold);
            return frame;
        }

        // ── Primitives ──────────────────────────────────────────────────────
        private static GameObject UI(string name, Transform parent, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h);
            return go;
        }

        private static Image AddPanel(GameObject go, Color color)
        {
            var img = go.AddComponent<Image>();
            img.sprite = Rounded; img.type = Image.Type.Sliced; img.color = color;
            return img;
        }

        private static Image AddChildPanel(GameObject parent, string name, Color color, float inset)
        {
            var go = UI(name, parent.transform, 0, 0);
            var img = go.AddComponent<Image>();
            img.sprite = Rounded; img.type = Image.Type.Sliced; img.color = color; img.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            Stretch(rt);
            rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
            return img;
        }

        private static Image MakeRoundedPanel(string name, Transform parent, Color color)
        {
            var go = UI(name, parent, 0, 0);
            return AddPanel(go, color);
        }

        private static TextMeshProUGUI MakeText(string name, Transform parent, string content, float size,
            Color color, FontStyles style = FontStyles.Normal,
            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var go = UI(name, parent, 0, 0);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = content; t.fontSize = size; t.color = color;
            t.fontStyle = style; t.alignment = align;
            t.raycastTarget = false;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static void Center(RectTransform rt)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = Vector2.zero;
        }

        private static void Fixed(GameObject go, float w, float h)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            if (w > 0) { le.preferredWidth = w; le.minWidth = w; le.flexibleWidth = 0; }
            if (h > 0) { le.preferredHeight = h; le.minHeight = h; le.flexibleHeight = 0; }
        }

        private static void Flexible(GameObject go, float weight)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.flexibleWidth = weight; le.flexibleHeight = weight;
        }

        // ── Wiring / assets ─────────────────────────────────────────────────
        private static void Wire(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning($"[InventoryUIBuilder] No field '{field}' on {target.GetType().Name}"); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning($"[InventoryUIBuilder] No array '{field}' on {target.GetType().Name}"); return; }
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif
