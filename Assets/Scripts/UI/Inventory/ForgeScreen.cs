// ForgeScreen.cs — Candy Cloud Weapon Forge screen. **STUBBED DATA.**
//
// The layout, both states (enough / not enough), the rarity ladder and the
// before→after cards are all real UI. The DATA is stubbed: `stubEnough` /
// `stubShort` are authored view-models shown for layout verification.
//
// ── WIRING REAL DATA (next step) ─────────────────────────────────────────────
// Everything renders through a single entry point:  Show(ForgeViewModel vm)
// To go live, build a ForgeViewModel from the real weapon instance + cost table
// and call Show(). Nothing else in this file needs to change. That requires the
// per-weapon INSTANCE model (level / baseDamage / damagePerLevel / derived
// rarity) which does not exist yet — see Session_Handoff.md.
//
// Design: Documentation/Asset and inventory UI design/design_handoff_candy_cloud/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LevelGen.Items;

namespace LevelGen.UI
{
    /// <summary>Weapon Forge screen. Renders a <see cref="ForgeViewModel"/>.</summary>
    [DisallowMultipleComponent]
    public class ForgeScreen : MonoBehaviour
    {
        /// <summary>One material requirement line.</summary>
        [System.Serializable]
        public class MaterialReq
        {
            public string displayName = "Mat 1";
            public ItemRarity rarity = ItemRarity.Common;
            public int have = 0;
            public int need = 1;
        }

        /// <summary>
        /// Everything the Forge needs to render one upgrade step. Build this from
        /// real weapon-instance data to go live.
        /// </summary>
        [System.Serializable]
        public class ForgeViewModel
        {
            public string weaponName = "Ember Sword";
            public int level = 2;
            public ItemRarity rarity = ItemRarity.Rare;
            public int currentDamage = 37;

            public int nextLevel = 3;
            public ItemRarity nextRarity = ItemRarity.Legendary;
            public int nextDamage = 45;

            public List<MaterialReq> materials = new List<MaterialReq>();

            /// <summary>True when every material requirement is satisfied.</summary>
            public bool CanUpgrade
            {
                get
                {
                    if (materials == null) return false;
                    foreach (var m in materials) if (m == null || m.have < m.need) return false;
                    return true;
                }
            }

            /// <summary>Damage gained by this upgrade.</summary>
            public int Delta => nextDamage - currentDamage;
        }

        [Header("Root / data")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private RarityPalette palette;

        [Header("STUB DATA (remove once real data is wired)")]
        [Tooltip("Shown by default — all materials satisfied.")]
        [SerializeField] private ForgeViewModel stubEnough = new ForgeViewModel();
        [Tooltip("Preview of the 'not enough materials' state.")]
        [SerializeField] private ForgeViewModel stubShort = new ForgeViewModel();

        [Header("Header")]
        [SerializeField] private TMP_Text headerText;
        [SerializeField] private Image statePill;
        [SerializeField] private TMP_Text statePillText;

        [Header("Current card")]
        [SerializeField] private Image curFrame, curFill, curIconFrame;
        [SerializeField] private TMP_Text curLevelBadge, curIconText, curName, curRarityPill, curDamage;

        [Header("Next card")]
        [SerializeField] private Image nextFrame, nextFill, nextIconFrame;
        [SerializeField] private TMP_Text nextLevelBadge, nextIconText, nextName, nextRarityPill, nextDamage, nextDelta;

        [Header("Rarity ladder (Common→Legendary)")]
        [SerializeField] private Image[] ladderPills = new Image[4];
        [SerializeField] private TMP_Text[] ladderLabels = new TMP_Text[4];

        [Header("Materials")]
        [SerializeField] private Transform materialsContainer;
        [SerializeField] private ForgeMaterialRow materialRowPrefab;

        [Header("Upgrade button")]
        [SerializeField] private Button upgradeButton;
        [SerializeField] private Image upgradeButtonImage;
        [SerializeField] private TMP_Text upgradeLabel;
        [SerializeField] private TMP_Text upgradeSubNote;
        [Tooltip("Anchor reserved for the success burst/glow VFX (added later).")]
        [SerializeField] private RectTransform vfxAnchor;

        [Header("Button colours (design tokens)")]
        [SerializeField] private Color enabledBg = RarityPalette.Hex("#38A85E");
        [SerializeField] private Color disabledBg = RarityPalette.Hex("#E7E3DB");
        [SerializeField] private Color disabledFg = RarityPalette.Hex("#A9A192");
        [SerializeField] private Color okPillBg = RarityPalette.Hex("#E7F6EC");
        [SerializeField] private Color okPillFg = RarityPalette.Hex("#2E7D46");
        [SerializeField] private Color badPillBg = RarityPalette.Hex("#FBE7E7");
        [SerializeField] private Color badPillFg = RarityPalette.Hex("#D14343");

        private readonly List<ForgeMaterialRow> _rows = new List<ForgeMaterialRow>();
        private ForgeViewModel _current;

        /// <summary>True while the forge panel is open.</summary>
        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

        private void Awake()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            if (upgradeButton != null) upgradeButton.onClick.AddListener(OnUpgradeClicked);
        }

        /// <summary>Opens the forge (frees the cursor) showing the "enough materials" stub.</summary>
        public void Open()
        {
            if (panelRoot == null || IsOpen) return;   // guard double-open (balanced cursor request)
            panelRoot.SetActive(true);
            LevelGen.Input.MouseLook.RequestUiCursor();
            Show(stubEnough);
        }

        /// <summary>Closes the forge and releases the cursor.</summary>
        public void Close()
        {
            if (panelRoot == null || !IsOpen) return;
            panelRoot.SetActive(false);
            LevelGen.Input.MouseLook.ReleaseUiCursor();
        }

        /// <summary>DEV: preview the "not enough materials" state.</summary>
        [ContextMenu("Preview: Not Enough Materials")]
        public void PreviewShort()
        {
            Open();
            Show(stubShort);
        }

        private void OnDestroy()
        {
            if (IsOpen) LevelGen.Input.MouseLook.ReleaseUiCursor();
        }

        /// <summary>
        /// THE ENTRY POINT — renders a view-model. Wire real weapon-instance data
        /// here and the whole screen goes live with no other changes.
        /// </summary>
        public void Show(ForgeViewModel vm)
        {
            if (vm == null) return;
            _current = vm;

            var cur = palette != null ? palette.Get(vm.rarity) : new RarityColorSet();
            var nxt = palette != null ? palette.Get(vm.nextRarity) : new RarityColorSet();

            if (headerText != null) headerText.text = "Weapon Forge";

            // Current card
            if (curFrame != null) curFrame.color = cur.main;
            if (curFill != null) curFill.color = cur.soft;
            if (curIconFrame != null) curIconFrame.color = cur.main;
            if (curLevelBadge != null) curLevelBadge.text = $"+{vm.level}";
            if (curIconText != null) curIconText.text = Initial(vm.weaponName);
            if (curName != null) curName.text = vm.weaponName;
            if (curRarityPill != null) { curRarityPill.text = vm.rarity.ToString(); curRarityPill.color = cur.text; }
            if (curDamage != null) curDamage.text = vm.currentDamage.ToString();

            // Next card
            if (nextFrame != null) nextFrame.color = nxt.main;
            if (nextFill != null) nextFill.color = nxt.soft;
            if (nextIconFrame != null) nextIconFrame.color = nxt.main;
            if (nextLevelBadge != null) nextLevelBadge.text = $"+{vm.nextLevel}";
            if (nextIconText != null) nextIconText.text = Initial(vm.weaponName);
            if (nextName != null) nextName.text = vm.weaponName;
            if (nextRarityPill != null)
            {
                // A rarity jump is conveyed by bold + colour rather than a ▲ glyph
                // (absent from the default TMP font).
                bool jumped = vm.nextRarity != vm.rarity;
                nextRarityPill.text = vm.nextRarity.ToString();
                nextRarityPill.fontStyle = jumped ? FontStyles.Bold : FontStyles.Normal;
                nextRarityPill.color = nxt.text;
            }
            if (nextDamage != null) nextDamage.text = vm.nextDamage.ToString();
            if (nextDelta != null) nextDelta.text = vm.Delta > 0 ? $"+{vm.Delta}" : vm.Delta.ToString();

            RefreshLadder(vm);
            RefreshMaterials(vm);
            RefreshButton(vm);
        }

        private void RefreshLadder(ForgeViewModel vm)
        {
            for (int i = 0; i < ladderPills.Length && i < 4; i++)
            {
                var tier = (ItemRarity)i;
                var c = palette != null ? palette.Get(tier) : new RarityColorSet();
                bool isCur = tier == vm.rarity;
                bool isNext = tier == vm.nextRarity;
                float alpha = (isCur || isNext) ? 1f : 0.4f;

                if (ladderPills[i] != null)
                {
                    var col = c.soft; col.a = alpha; ladderPills[i].color = col;
                }
                if (i < ladderLabels.Length && ladderLabels[i] != null)
                {
                    ladderLabels[i].text = tier.ToString();
                    var col = c.text; col.a = alpha; ladderLabels[i].color = col;
                    ladderLabels[i].fontStyle = isNext ? FontStyles.Bold : FontStyles.Normal;
                }
            }
        }

        private void RefreshMaterials(ForgeViewModel vm)
        {
            if (materialsContainer == null || materialRowPrefab == null) return;

            int need = vm.materials != null ? vm.materials.Count : 0;
            while (_rows.Count < need) _rows.Add(Instantiate(materialRowPrefab, materialsContainer));

            for (int i = 0; i < _rows.Count; i++)
            {
                bool show = i < need;
                _rows[i].gameObject.SetActive(show);
                if (!show) continue;
                var m = vm.materials[i];
                _rows[i].Bind(m.displayName, m.rarity, m.have, m.need, palette);
            }
        }

        private void RefreshButton(ForgeViewModel vm)
        {
            bool can = vm.CanUpgrade;

            if (statePill != null) statePill.color = can ? okPillBg : badPillBg;
            if (statePillText != null)
            {
                // ASCII only — default TMP font lacks ✓/✕ (restore once Fredoka/
                // Nunito TMP font assets are imported).
                statePillText.text = can ? "Ready to forge" : "Not enough";
                statePillText.color = can ? okPillFg : badPillFg;
            }

            if (upgradeButton != null) upgradeButton.interactable = can;
            if (upgradeButtonImage != null) upgradeButtonImage.color = can ? enabledBg : disabledBg;
            if (upgradeLabel != null)
            {
                upgradeLabel.text = can ? $"UPGRADE  +{vm.Delta} DMG" : "NEED MORE MATERIALS";
                upgradeLabel.color = can ? Color.white : disabledFg;
            }
            if (upgradeSubNote != null)
            {
                if (can)
                {
                    upgradeSubNote.text = "celebration plays here on success";
                    upgradeSubNote.color = okPillFg;
                }
                else
                {
                    upgradeSubNote.text = MissingSummary(vm);
                    upgradeSubNote.color = badPillFg;
                }
            }
        }

        private static string MissingSummary(ForgeViewModel vm)
        {
            if (vm.materials == null) return "Missing materials";
            foreach (var m in vm.materials)
                if (m != null && m.have < m.need)
                    return $"Missing {m.need - m.have}x {m.displayName} - defeat harder enemies to collect";
            return "Missing materials";
        }

        private void OnUpgradeClicked()
        {
            // STUB: real upgrade (spend materials, level+1, recompute damage/rarity)
            // lands with the weapon-instance model. The success VFX anchors at
            // `vfxAnchor`.
            if (_current == null || !_current.CanUpgrade) return;
            Debug.Log($"[ForgeScreen] STUB upgrade: {_current.weaponName} +{_current.level} → " +
                      $"+{_current.nextLevel} ({_current.nextRarity}), {_current.currentDamage} → " +
                      $"{_current.nextDamage}. Real data/spend not wired yet.", this);
        }

        private static string Initial(string s)
            => string.IsNullOrEmpty(s) ? "?" : s.Substring(0, 1).ToUpper();
    }
}
