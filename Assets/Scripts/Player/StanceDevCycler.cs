// StanceDevCycler.cs — M22 DEV-ONLY stance cycler.
//
// ┌─────────────────────────────────────────────────────────────────────────┐
// │ DEV-ONLY. This whole file is a development affordance and is intended to  │
// │ be DELETED before ship (Jason's decision: "the Q cycle is strictly for   │
// │ dev testing purposes; in the end it will be removed").                   │
// │                                                                          │
// │ Deleting this file must NOT break the build. Nothing depends on it:      │
// │ StanceController + the inventory equip→stance bridge are the permanent   │
// │ path. This class only forwards the SwitchStance (Q) input to             │
// │ StanceController.CycleStance() so all 8 stances can be eyeballed without  │
// │ owning items.                                                            │
// └─────────────────────────────────────────────────────────────────────────┘

using UnityEngine;

namespace LevelGen.Player
{
    /// <summary>
    /// DEV-ONLY. Cycles the player's stance on the SwitchStance (Q) input for
    /// testing. Remove before ship — see the file header.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInputReader))]
    [RequireComponent(typeof(StanceController))]
    public class StanceDevCycler : MonoBehaviour
    {
        private PlayerInputReader _input;
        private StanceController _stance;

        private void Awake()
        {
            _input = GetComponent<PlayerInputReader>();
            _stance = GetComponent<StanceController>();
        }

        private void OnEnable()
        {
            if (_input != null) _input.SwitchStancePressed += OnSwitchStance;
        }

        private void OnDisable()
        {
            if (_input != null) _input.SwitchStancePressed -= OnSwitchStance;
        }

        private void OnSwitchStance()
        {
            if (_stance == null) return;
            _stance.CycleStance();
            Debug.Log($"[StanceDevCycler] DEV cycle → {_stance.CurrentStance}");
        }
    }
}
