// RarityPalette.cs — rarity → colour lookup for the Candy Cloud UI.
//
// Holds BOTH approved palettes from the design handoff:
//   • Candy Warm    (CHOSEN for the final Candy Cloud build)
//   • Classic Bright (fallback — matches the exported reference PNGs)
// Swap with the `activeSet` dropdown; every UI view reads through Get().
//
// Design source: Documentation/Asset and inventory UI design/
//                design_handoff_candy_cloud/README.md → "Design Tokens".

using UnityEngine;
using LevelGen.Items;

namespace LevelGen.UI
{
    /// <summary>Per-rarity colour triple: frame border, soft background, text.</summary>
    [System.Serializable]
    public class RarityColorSet
    {
        [Tooltip("2px frame / border colour.")]
        public Color main = Color.grey;
        [Tooltip("Soft cell background tint.")]
        public Color soft = Color.white;
        [Tooltip("Rarity label text colour.")]
        public Color text = Color.black;
    }

    /// <summary>
    /// Rarity colour lookup shared by every inventory / forge view. One asset;
    /// assign it on the screens. Order is Common, Uncommon, Rare, Legendary.
    /// </summary>
    [CreateAssetMenu(fileName = "RarityPalette", menuName = "Hub & Hollow/Rarity Palette")]
    public class RarityPalette : ScriptableObject
    {
        /// <summary>Which approved palette is live.</summary>
        public enum PaletteSet
        {
            /// <summary>Chosen for the final Candy Cloud build.</summary>
            CandyWarm,
            /// <summary>Fallback; matches the exported reference PNGs.</summary>
            ClassicBright,
        }

        [Tooltip("Which palette the UI uses. Candy Warm is the design's chosen final.")]
        [SerializeField] private PaletteSet activeSet = PaletteSet.CandyWarm;

        [Tooltip("Candy Warm — Common, Uncommon, Rare, Legendary.")]
        [SerializeField] private RarityColorSet[] candyWarm = new RarityColorSet[4];

        [Tooltip("Classic Bright — Common, Uncommon, Rare, Legendary.")]
        [SerializeField] private RarityColorSet[] classicBright = new RarityColorSet[4];

        [Header("Empty slot (no item)")]
        public Color emptyMain = Hex("#DBD5C9");
        public Color emptySoft = Hex("#F1EDE4");

        [Header("Locked / future slot")]
        public Color lockedMain = Hex("#D9D3C6");
        public Color lockedSoft = Hex("#F3EFE7");

        /// <summary>Colours for a rarity tier from the active palette.</summary>
        public RarityColorSet Get(ItemRarity rarity)
        {
            var set = activeSet == PaletteSet.CandyWarm ? candyWarm : classicBright;
            int i = Mathf.Clamp((int)rarity, 0, 3);
            if (set == null || set.Length < 4 || set[i] == null) return new RarityColorSet();
            return set[i];
        }

        private void Reset() => ResetToDesignDefaults();

        /// <summary>Refills both palettes with the design-handoff hex values.</summary>
        [ContextMenu("Reset To Design Defaults")]
        public void ResetToDesignDefaults()
        {
            candyWarm = new[]
            {
                Set("#A99B86", "#F3EFE8", "#7C7060"), // Common
                Set("#54C97E", "#E6F8ED", "#2F9455"), // Uncommon
                Set("#9B6BE6", "#F0E9FB", "#6E42C0"), // Rare
                Set("#FF9A3D", "#FFEEDC", "#D26A0E"), // Legendary
            };
            classicBright = new[]
            {
                Set("#7C8698", "#EEF1F5", "#5A6474"), // Common
                Set("#43B55F", "#E7F6EC", "#2E7D46"), // Uncommon
                Set("#2E90E0", "#E4F1FC", "#1E6FB8"), // Rare
                Set("#F0A32E", "#FDF1DC", "#B9740A"), // Legendary
            };
            emptyMain = Hex("#DBD5C9"); emptySoft = Hex("#F1EDE4");
            lockedMain = Hex("#D9D3C6"); lockedSoft = Hex("#F3EFE7");
        }

        private static RarityColorSet Set(string main, string soft, string text)
            => new RarityColorSet { main = Hex(main), soft = Hex(soft), text = Hex(text) };

        /// <summary>Parses a #RRGGBB string into a Color (opaque white on failure).</summary>
        public static Color Hex(string hex)
            => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;
    }
}
