// ForgeMaterialRow.cs — one "materials required" row on the Forge screen.
//
// Layout (design §Materials required): 34×34 rarity tile · "Material · <tier>"
// · have/need count · ✓/✕ status dot. Short rows dim the tile and redden the count.

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LevelGen.Items;

namespace LevelGen.UI
{
    /// <summary>A single material requirement row (have / need + status).</summary>
    public class ForgeMaterialRow : MonoBehaviour
    {
        [SerializeField] private Image tileFrame;
        [SerializeField] private Image tileFill;
        [SerializeField] private TMP_Text tileText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private Image statusDot;
        [SerializeField] private TMP_Text statusGlyph;

        [SerializeField] private Color okColor = RarityPalette.Hex("#43B55F");
        [SerializeField] private Color shortColor = RarityPalette.Hex("#D14343");

        /// <summary>Populates the row from a requirement.</summary>
        /// <param name="displayName">Material name (e.g. "Mat 1").</param>
        /// <param name="rarity">Material tier — drives the tile colours.</param>
        /// <param name="have">How many the player holds.</param>
        /// <param name="need">How many the upgrade costs.</param>
        /// <param name="palette">Rarity colour source.</param>
        public void Bind(string displayName, ItemRarity rarity, int have, int need, RarityPalette palette)
        {
            bool ok = have >= need;
            var c = palette != null ? palette.Get(rarity) : new RarityColorSet();

            if (tileFrame != null) tileFrame.color = Fade(c.main, ok ? 1f : 0.55f);
            if (tileFill != null) tileFill.color = Fade(c.soft, ok ? 1f : 0.55f);
            if (tileText != null)
            {
                tileText.text = string.IsNullOrEmpty(displayName) ? "?" : displayName.Substring(0, 1).ToUpper();
                tileText.color = Fade(c.text, ok ? 1f : 0.55f);
            }

            if (nameText != null) nameText.text = $"{displayName} - {rarity}";
            if (countText != null)
            {
                countText.text = $"{have}/{need}";
                countText.color = ok ? RarityPalette.Hex("#2A2A2E") : shortColor;
            }
            // The coloured dot carries the state; glyph stays ASCII because the
            // default TMP font has no check/cross. Restore ✓/✕ once Fredoka/
            // Nunito TMP font assets are imported.
            if (statusDot != null) statusDot.color = ok ? okColor : shortColor;
            if (statusGlyph != null) statusGlyph.text = ok ? "" : "!";
        }

        private static Color Fade(Color c, float a) { c.a = a; return c; }
    }
}
