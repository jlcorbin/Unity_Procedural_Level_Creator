using UnityEngine;

namespace LevelGen.Player
{
    /// <summary>
    /// Per-stance configuration asset (one per <see cref="Stance"/>) — the Unity
    /// equivalent of the UE5 <c>StanceRightMeshes</c> / <c>StanceLeftMeshes</c> /
    /// <c>StanceRightRotations</c> / <c>StanceLeftRotations</c> / <c>StanceIsRanged</c>
    /// arrays (spec §6). Authored in the editor; consumed by the stance controller's
    /// <c>ApplyStance</c> routine to swap hand meshes, apply per-hand attach
    /// rotations, and toggle ranged UI.
    ///
    /// The weapon prefabs referenced here are the project's
    /// <c>Assets/Prefabs/Weapons/WeaponPrefab_*</c> assets (M20c). The bow (stance 7)
    /// is a skinned rig — set <see cref="isBowRig"/> so the controller mounts it to
    /// the left hand and enables the nocked arrow + flex-idle controller.
    /// </summary>
    [CreateAssetMenu(fileName = "Stance_", menuName = "Hub & Hollow/Stance Definition")]
    public class StanceDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Which stance this asset configures. Must be unique across the 8 assets.")]
        [SerializeField] private Stance stance = Stance.NoWeapon;

        [Tooltip("Human-readable label (e.g. \"Sword & Shield\").")]
        [SerializeField] private string displayName = "";

        [Header("Weapon meshes (WeaponPrefab_* assets)")]
        [Tooltip("Prefab parented under the right-hand socket. Null = empty hand.")]
        [SerializeField] private GameObject rightHandPrefab;

        [Tooltip("Prefab parented under the left-hand socket. Null = empty hand.")]
        [SerializeField] private GameObject leftHandPrefab;

        [Header("Per-hand attach rotations (local euler, spec §6)")]
        [Tooltip("Right-hand local euler. Spear = (0,0,10); others (0,0,0).")]
        [SerializeField] private Vector3 rightHandEuler = Vector3.zero;

        [Tooltip("Left-hand local euler. Shield=(0,-180,0); DoubleSword=(0,-180,-90); Bow=(0,170,0).")]
        [SerializeField] private Vector3 leftHandEuler = Vector3.zero;

        [Header("Behavior flags")]
        [Tooltip("True for MagicWand (6) and BowAndArrow (7). Toggles the ranged crosshair and charge/release attack path.")]
        [SerializeField] private bool isRanged;

        [Tooltip("True only for BowAndArrow (7): the left-hand prefab is a skinned flex rig; the controller adds an extra +90 yaw and shows the nocked arrow.")]
        [SerializeField] private bool isBowRig;

        /// <summary>Which stance this asset configures.</summary>
        public Stance Stance => stance;
        /// <summary>Human-readable label.</summary>
        public string DisplayName => displayName;
        /// <summary>Right-hand weapon prefab (null = empty).</summary>
        public GameObject RightHandPrefab => rightHandPrefab;
        /// <summary>Left-hand weapon prefab (null = empty).</summary>
        public GameObject LeftHandPrefab => leftHandPrefab;
        /// <summary>Right-hand local attach euler.</summary>
        public Vector3 RightHandEuler => rightHandEuler;
        /// <summary>Left-hand local attach euler.</summary>
        public Vector3 LeftHandEuler => leftHandEuler;
        /// <summary>Ranged stance flag (wand/bow).</summary>
        public bool IsRanged => isRanged;
        /// <summary>Skinned bow rig flag (stance 7 only).</summary>
        public bool IsBowRig => isBowRig;

        /// <summary>The Animator <c>WeaponType</c> int value for this stance (= stance index 0–7).</summary>
        public int AnimatorStanceIndex => (int)stance;

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Keep the ranged flag honest against the canonical stance definition
            // so an authoring mistake can't route a melee stance through the
            // charge/release path (or vice-versa).
            isRanged = stance.IsRanged();
            isBowRig = stance == Stance.BowAndArrow;
            if (string.IsNullOrEmpty(displayName))
                displayName = stance.ToString();
        }
#endif
    }
}
