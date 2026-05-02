using UnityEngine;

namespace LevelGen.Combat
{
    /// <summary>
    /// Reusable character data template. Holds max HP / max Stamina /
    /// display name / free-form description. Created as an asset and
    /// duplicated per character type — the master asset is never assigned
    /// directly, only duplicated and tweaked.
    /// </summary>
    /// <remarks>
    /// Pure config asset — no runtime mutation. Spawned characters reference
    /// the SO via <see cref="CharacterStatsRuntime"/>, which copies the max
    /// values into runtime fields at Awake. Multiple characters can safely
    /// share one asset.
    /// </remarks>
    [CreateAssetMenu(
        menuName = "LevelGen/Combat/Character Stats",
        fileName = "CharacterStats_New",
        order    = 100)]
    public class CharacterStats : ScriptableObject
    {
        [Tooltip("Inspector label / future UI source.")]
        public string displayName = "Character";

        [Tooltip("Max health. Current health lives on the runtime component, not here.")]
        public int maxHP = 100;

        [Tooltip("Max stamina. Data-only for now — not consumed by gameplay yet.")]
        public int maxStamina = 100;

        [TextArea(3, 6)]
        [Tooltip("Free-form notes for the author.")]
        public string description = "";

#if UNITY_EDITOR
        void OnValidate()
        {
            if (maxHP < 1)
            {
                Debug.LogWarning($"[CharacterStats] '{name}' maxHP was {maxHP}; clamped to 1.");
                maxHP = 1;
            }
            if (maxStamina < 1)
            {
                Debug.LogWarning($"[CharacterStats] '{name}' maxStamina was {maxStamina}; clamped to 1.");
                maxStamina = 1;
            }
        }
#endif
    }
}
