// StanceController.cs — M22 stance system (spec §6).
//
// The permanent, canonical owner of the player's CurrentStance. It is the SOLE
// writer of the Animator stance index (the "WeaponType" int, 0–7 via
// PlayerAnimator.SetStanceIndex) and the source of truth for "is the active
// stance ranged". It toggles the ranged crosshair on stance change and fires
// OnStanceChanged so combat / ranged systems can react.
//
// COEXIST model (Jason's decision): the inventory Equip path stays the "real"
// game route — PlayerEquipmentVisuals still mounts weapon meshes + wires the
// melee hitbox from equipped ItemData. This controller does NOT swap weapon
// meshes in the equip path (that would double-mount). It only drives the
// animator stance + crosshair. The equip→stance BRIDGE keeps the animator's
// stance in sync with whatever is equipped.
//
// The DEV-ONLY Q cycle lives in the separate, removable StanceDevCycler; that
// file (and only that file) drives the stance-definition mesh swap for visual
// testing of all 8 stances without owning items. Deleting StanceDevCycler must
// not break this controller or the build.

using System;
using UnityEngine;
using LevelGen.Items;
using LevelGen.Combat;

namespace LevelGen.Player
{
    /// <summary>
    /// Canonical owner of the player's <see cref="Stance"/>. Writes the Animator
    /// stance index, toggles ranged UI, and bridges inventory equips to stance.
    /// </summary>
    [DisallowMultipleComponent]
    public class StanceController : MonoBehaviour
    {
        [Header("Stance definitions (author 8 — one per Stance)")]
        [Tooltip("StanceDefinition assets, one per stance. Order/index is not relied on — lookups match by StanceDefinition.Stance.")]
        [SerializeField] private StanceDefinition[] _stanceDefinitions = new StanceDefinition[0];

        [Tooltip("Stance applied once on spawn (spec default = SingleSword). The equip bridge overrides this if an item is already equipped.")]
        [SerializeField] private Stance _startingStance = Stance.SingleSword;

        [Header("Ranged UI")]
        [Tooltip("Center reticle GameObject shown only in ranged stances (wand/bow). Optional — null-safe.")]
        [SerializeField] private GameObject _rangedCrosshair;

        [Header("Hand sockets (used only by the DEV mesh swap)")]
        [Tooltip("Right-hand bone (weapon_r). Only used when a stance is applied with mesh-swapping (dev cycler). Leave null in pure inventory play.")]
        [SerializeField] private Transform _rightHandSocket;

        [Tooltip("Left-hand bone (off-hand). Only used when a stance is applied with mesh-swapping (dev cycler).")]
        [SerializeField] private Transform _leftHandSocket;

        // ── Runtime state ───────────────────────────────────────────────────
        private PlayerAnimator _anim;
        private PlayerCombat _combat;   // optional — used to wire the dev-cycle melee hitbox

        /// <summary>The player's currently active stance.</summary>
        public Stance CurrentStance { get; private set; }

        /// <summary>True while the active stance is ranged (wand / bow).</summary>
        public bool IsRanged => CurrentStance.IsRanged();

        /// <summary>
        /// Fired after the stance changes (and once on Start). Payload is the new
        /// stance. Consumed by ranged combat (to arm/disarm the charge-release
        /// path) and any UI that mirrors the active stance.
        /// </summary>
        public event Action<Stance> OnStanceChanged;

        // ── Lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            _anim = GetComponent<PlayerAnimator>();
            _combat = GetComponent<PlayerCombat>();   // optional — dev-cycle hitbox wiring
            if (_anim == null)
                Debug.LogWarning($"[StanceController] No PlayerAnimator on '{name}'. Stance index will not reach the Animator.", this);
            CurrentStance = _startingStance;
        }

        private void OnEnable()
        {
            if (PlayerInventory.Instance != null)
                PlayerInventory.Instance.OnWeaponEquipped += OnEquipChanged;
        }

        private void OnDisable()
        {
            if (PlayerInventory.Instance != null)
                PlayerInventory.Instance.OnWeaponEquipped -= OnEquipChanged;
        }

        private void Start()
        {
            // Apply once on spawn (spec §6). If inventory already has something
            // equipped, sync to that; otherwise apply the starting stance.
            Stance initial = PlayerInventory.Instance != null
                ? ResolveStanceFromInventory(fallback: _startingStance)
                : _startingStance;
            SetStance(initial, swapMeshes: false);
        }

        // ── Public API ──────────────────────────────────────────────────────

        /// <summary>
        /// Sets the active stance: writes the Animator stance index, toggles the
        /// ranged crosshair, optionally swaps the stance-definition hand meshes
        /// (dev path only), and fires <see cref="OnStanceChanged"/>.
        /// </summary>
        /// <param name="stance">The stance to activate.</param>
        /// <param name="swapMeshes">
        /// DEV path: true to mount this stance's <see cref="StanceDefinition"/>
        /// weapon meshes into the hand sockets. Real inventory play passes false
        /// (PlayerEquipmentVisuals owns weapon meshes).
        /// </param>
        public void SetStance(Stance stance, bool swapMeshes)
        {
            CurrentStance = stance;

            _anim?.SetStanceIndex((int)stance);

            if (_rangedCrosshair != null)
                _rangedCrosshair.SetActive(stance.IsRanged());

            if (swapMeshes)
                SwapHandMeshes(GetDefinition(stance));

            OnStanceChanged?.Invoke(stance);
        }

        /// <summary>
        /// DEV-ONLY: advance to the next stance with wraparound
        /// (<c>(stance + 1) % 8</c>) and swap in that stance's meshes for visual
        /// testing. Called by <c>StanceDevCycler</c>.
        /// </summary>
        public void CycleStance()
        {
            SetStance(CurrentStance.Next(), swapMeshes: true);
        }

        /// <summary>
        /// Returns the authored <see cref="StanceDefinition"/> for a stance, or
        /// null if none is assigned in <see cref="_stanceDefinitions"/>.
        /// </summary>
        public StanceDefinition GetDefinition(Stance stance)
        {
            if (_stanceDefinitions == null) return null;
            foreach (var def in _stanceDefinitions)
                if (def != null && def.Stance == stance)
                    return def;
            return null;
        }

        // ── Equip → stance bridge ───────────────────────────────────────────

        /// <summary>
        /// Inventory bridge: on any equip / unequip, re-derive the stance from
        /// the currently equipped set and apply it WITHOUT swapping meshes
        /// (PlayerEquipmentVisuals already mounted the item mesh).
        /// </summary>
        private void OnEquipChanged(ItemData _)
        {
            SetStance(ResolveStanceFromInventory(fallback: CurrentStance), swapMeshes: false);
        }

        /// <summary>
        /// Maps the currently equipped items to a <see cref="Stance"/>. Ranged
        /// slot takes priority over melee (documented coexist rule). Falls back
        /// to <paramref name="fallback"/> only when nothing recognizable is
        /// equipped and no melee/ranged item is present.
        /// </summary>
        private Stance ResolveStanceFromInventory(Stance fallback)
        {
            var inv = PlayerInventory.Instance;
            if (inv == null) return fallback;

            // Ranged wins if present (wand/bow).
            var ranged = inv.GetEquipped(EquipSlot.Ranged);
            if (ranged != null)
            {
                switch (Classify(ranged))
                {
                    case WeaponCategory.Bow:  return Stance.BowAndArrow;
                    case WeaponCategory.Wand: return Stance.MagicWand;
                }
            }

            var melee = inv.GetEquipped(EquipSlot.Melee);
            if (melee == null)
                return Stance.NoWeapon;

            switch (Classify(melee))
            {
                case WeaponCategory.TwoHand: return Stance.TwoHandsSword;
                case WeaponCategory.Spear:   return Stance.Spear;
                case WeaponCategory.OneHand:
                {
                    var off = inv.GetEquipped(EquipSlot.OffHand);
                    if (off != null)
                    {
                        var oc = Classify(off);
                        if (oc == WeaponCategory.Shield)  return Stance.SwordAndShield;
                        if (oc == WeaponCategory.OneHand) return Stance.DoubleSword;
                    }
                    return Stance.SingleSword;
                }
                default: return Stance.NoWeapon;
            }
        }

        private enum WeaponCategory { Unknown, OneHand, TwoHand, Spear, Shield, Bow, Wand }

        /// <summary>
        /// Classifies an item by its WorldPrefab name prefix (mirrors
        /// WeaponTypeResolver's approach, extended to the 8-stance vocabulary).
        /// Prefabs are <c>WeaponPrefab_&lt;name&gt;</c>: OHS*, THS*, Spear*,
        /// Shield*, Wand*, Bows.
        /// </summary>
        private static WeaponCategory Classify(ItemData item)
        {
            if (item == null || item.WorldPrefab == null) return WeaponCategory.Unknown;
            string n = item.WorldPrefab.name;
            const string prefix = "WeaponPrefab_";
            if (n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                n = n.Substring(prefix.Length);

            if (n.StartsWith("Bow",    StringComparison.OrdinalIgnoreCase)) return WeaponCategory.Bow;
            if (n.StartsWith("Wand",   StringComparison.OrdinalIgnoreCase)) return WeaponCategory.Wand;
            if (n.StartsWith("Shield", StringComparison.OrdinalIgnoreCase)) return WeaponCategory.Shield;
            if (n.StartsWith("THS",    StringComparison.OrdinalIgnoreCase)) return WeaponCategory.TwoHand;
            if (n.StartsWith("Spear",  StringComparison.OrdinalIgnoreCase)) return WeaponCategory.Spear;
            if (n.StartsWith("OHS",    StringComparison.OrdinalIgnoreCase)) return WeaponCategory.OneHand;
            return WeaponCategory.Unknown;
        }

        // ── DEV mesh swap ───────────────────────────────────────────────────

        /// <summary>
        /// DEV path only: clears the hand sockets and mounts the stance's
        /// definition meshes. Weapons are instantiated preserving their AUTHORED
        /// local transform (exactly like the inventory/equip path in
        /// PlayerEquipmentVisuals) so orientation is correct without per-hand
        /// eulers — the UE5 spec §6 rotations don't map to Unity's bones, so the
        /// StanceDefinition eulers are no longer applied here. For non-ranged
        /// stances the right-hand weapon's collider is wired to PlayerCombat so
        /// dev-cycled weapons deal damage. Never called in inventory play, so it
        /// doesn't fight PlayerEquipmentVisuals.
        /// </summary>
        private void SwapHandMeshes(StanceDefinition def)
        {
            // Drop the melee hitbox ref before destroying the old weapon so
            // PlayerCombat never holds a collider that's about to be destroyed.
            if (_combat != null) _combat.Hitbox = null;

            ClearSocket(_rightHandSocket);
            ClearSocket(_leftHandSocket);
            if (def == null) return;

            // Right hand: authored transform only (correct as-is). Left hand: the
            // same prefabs sit wrong on the mirrored off-hand bone, so apply the
            // definition's LeftHandEuler as a corrective offset (tune per stance).
            var right = MountInto(_rightHandSocket, def.RightHandPrefab, Vector3.zero);
            MountInto(_leftHandSocket, def.LeftHandPrefab, def.LeftHandEuler);

            // Wire the right-hand melee weapon's hitbox (matches PlayerEquipmentVisuals).
            if (!def.IsRanged && right != null && _combat != null)
            {
                var col = right.GetComponentInChildren<Collider>();
                var relay = right.GetComponentInChildren<HitboxRelay>();
                if (col != null) _combat.Hitbox = col;
                if (relay != null) relay.Combat = _combat;
            }
        }

        private static void ClearSocket(Transform socket)
        {
            if (socket == null) return;
            for (int i = socket.childCount - 1; i >= 0; i--)
                Destroy(socket.GetChild(i).gameObject);
        }

        private static GameObject MountInto(Transform socket, GameObject prefab, Vector3 eulerOffset)
        {
            if (socket == null || prefab == null) return null;
            // instantiateInWorldSpace:false → keep the prefab's authored local
            // transform relative to the socket bone (correct orientation).
            var go = Instantiate(prefab, socket, false);
            // Optional corrective offset (used for the off-hand). Applied in socket
            // space on top of the authored rotation. Zero = no change.
            if (eulerOffset != Vector3.zero)
                go.transform.localRotation = Quaternion.Euler(eulerOffset) * go.transform.localRotation;
            return go;
        }
    }
}
