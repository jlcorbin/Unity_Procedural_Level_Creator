// PlayerEquipmentVisuals.cs — M20: Visual Weapon Mesh Swap.
//
// Subscribes to PlayerInventory.OnWeaponEquipped and swaps the visible
// weapon mesh under the weapon_r bone socket whenever the equipped Melee
// item changes. Empty hand (no mesh) is the correct unarmed state when
// null is received.
//
// Single-direction dependency: PlayerInventory fires OnWeaponEquipped;
// this component is a pure consumer — it never reaches back into
// PlayerInventory beyond subscribing to the event.
//
// _weaponSocket must be wired manually in the Inspector by dragging the
// weapon_r bone from the MaleCharacterPBR hierarchy. It is NOT resolved
// at runtime via GetBoneTransform because weapon_r is an extra bone
// outside the standard Humanoid skeleton and is not exposed by
// Animator.GetBoneTransform.

using UnityEngine;
using LevelGen.Combat;
using LevelGen.Items;

namespace LevelGen.Player
{
    /// <summary>
    /// Subscribes to <see cref="PlayerInventory.OnWeaponEquipped"/> and
    /// instantiates the equipped item's <see cref="ItemData.WorldPrefab"/>
    /// as a child of the <c>weapon_r</c> bone socket. Destroying any
    /// previously-equipped mesh child first. When null is received the
    /// socket is left empty (correct unarmed state — no fallback mesh).
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerEquipmentVisuals : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────

        [Tooltip("The weapon_r bone Transform in the MaleCharacterPBR hierarchy. " +
                 "Drag the bone here manually — it cannot be found via " +
                 "Animator.GetBoneTransform because it is outside the Humanoid skeleton.")]
        [SerializeField] private Transform _weaponSocket;

[Tooltip("The weapon_l bone Transform in the MaleCharacterPBR hierarchy. " +
         "Drag the bone here manually for off-hand items (shields).")]
[SerializeField] private Transform _offHandSocket;
        // ── Lifecycle ──────────────────────────────────────────────────────

        private void OnEnable()
        {
            if (PlayerInventory.Instance == null)
            {
                Debug.LogWarning("[PlayerEquipmentVisuals] PlayerInventory.Instance is null in OnEnable. " +
                                 "Subscription skipped — ensure PlayerInventory is present in the scene.");
                return;
            }
            PlayerInventory.Instance.OnWeaponEquipped += HandleWeaponEquipped;
        }

        private void OnDisable()
        {
            if (PlayerInventory.Instance == null) return;
            PlayerInventory.Instance.OnWeaponEquipped -= HandleWeaponEquipped;
        }

        // ── Private ────────────────────────────────────────────────────────

        /// <summary>
        /// Responds to <see cref="PlayerInventory.OnWeaponEquipped"/>.
        /// Destroys existing children of the weapon socket then instantiates
        /// the new item's world prefab as a child. When <paramref name="item"/>
        /// or its <see cref="ItemData.WorldPrefab"/> is null the socket is
        /// left empty.
        /// </summary>
        /// <param name="item">
        /// The newly equipped item, or null when the slot was unequipped.
        /// </param>
        private void HandleWeaponEquipped(ItemData item)
{
    // Pick the correct socket based on slot
    Transform socket = (item != null && item.Slot == EquipSlot.OffHand)
        ? _offHandSocket
        : _weaponSocket;

    if (socket == null)
    {
        Debug.LogError($"[PlayerEquipmentVisuals] Socket is null for slot " +
                       $"{(item != null ? item.Slot.ToString() : "null")}. " +
                       "Drag the correct bone into the Inspector field.");
        return;
    }

    // Destroy existing children of this socket only
    for (int i = socket.childCount - 1; i >= 0; i--)
        Object.Destroy(socket.GetChild(i).gameObject);

    if (item == null || item.WorldPrefab == null)
    {
        // Only clear the hitbox reference when it's the melee slot
        if (item == null || item.Slot == EquipSlot.Melee)
        {
            var pcUnequip = GetComponentInParent<PlayerCombat>();
            if (pcUnequip != null) pcUnequip.Hitbox = null;
        }
        return;
    }

    var instance = Instantiate(item.WorldPrefab, socket);

    // Only wire hitbox relay for melee weapons — shields have no hitbox
    if (item.Slot == EquipSlot.Melee)
    {
        var relay = instance.GetComponent<HitboxRelay>();
        var col   = instance.GetComponent<Collider>();
        var pc    = GetComponentInParent<PlayerCombat>();
        if (pc != null)
            pc.Hitbox = col;
        if (relay != null && pc != null)
            relay.Combat = pc;
    }

    Debug.Log($"[PlayerEquipmentVisuals] Equipped '{item.DisplayName}' mesh under {socket.name}.");
}
    }
}
