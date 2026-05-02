// PlayerHUD.cs — passive observer of CharacterStatsRuntime on the Player.
//
// Lives on the Canvas root of PlayerHUD.prefab. Finds Player_MaleHero
// once at Start (with a polling retry coroutine for deferred-spawn
// scenarios), then per-frame mirrors HP / Stamina onto two Filled
// Images plus TMP labels.
//
// Display rules:
//   - target < displayed → snap (damage feedback should be instant)
//   - target > displayed → MoveTowards at lerpSpeedHeal (healing feels
//     more rewarding when you can watch it climb)
//   - labels always show the truth integer; bar lerp is visual only
//
// Pure read-only — never mutates stats. Damage / heal / regen come
// from other systems; this is the view layer.

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LevelGen.Combat;

namespace LevelGen.UI
{
    [DisallowMultipleComponent]
    public class PlayerHUD : MonoBehaviour
    {
        [Header("Wiring (assigned in prefab)")]
        [SerializeField]
        [Tooltip("Filled Image for HP bar. Must be Image Type=Filled, Horizontal, Left.")]
        private Image hpFill;

        [SerializeField]
        [Tooltip("Filled Image for Stamina bar. Must be Image Type=Filled, Horizontal, Left.")]
        private Image staminaFill;

        [SerializeField]
        [Tooltip("TMP label for HP. Format: 'HP {current} / {max}'.")]
        private TMP_Text hpLabel;

        [SerializeField]
        [Tooltip("TMP label for Stamina. Format: 'Stamina {current} / {max}'.")]
        private TMP_Text staminaLabel;

        [Header("Behavior")]
        [SerializeField]
        [Tooltip("Heal lerp rate in fill-fraction units per second. 1.5 ≈ 0.67s for full empty→full.")]
        private float lerpSpeedHeal = 1.5f;

        [SerializeField]
        [Tooltip("Tag used to find the Player. Default 'Player'.")]
        private string playerTag = "Player";

        // Runtime state — not serialized.
        private CharacterStatsRuntime _runtime;
        private float _displayedHPFraction;
        private float _displayedStaminaFraction;
        private bool  _isBound;
        private Coroutine _bindLoop;

        public bool IsBound => _isBound;
        public CharacterStatsRuntime BoundRuntime => _runtime;

        void Awake()
        {
            if (hpFill == null || staminaFill == null || hpLabel == null || staminaLabel == null)
            {
                Debug.LogError("[PlayerHUD] One or more serialized refs are unassigned. " +
                               "Disabling component. Run 'LevelGen ▶ UI ▶ Build PlayerHUD Prefab' " +
                               "to regenerate, or assign by hand.", this);
                enabled = false;
            }
        }

        void Start()
        {
            if (!TryBindToPlayer())
                _bindLoop = StartCoroutine(PollForPlayer());
        }

        void OnDisable()
        {
            if (_bindLoop != null)
            {
                StopCoroutine(_bindLoop);
                _bindLoop = null;
            }
        }

        void Update()
        {
            if (!_isBound) return;

            // Player despawned — drop binding and re-poll.
            if (_runtime == null)
            {
                _isBound = false;
                if (_bindLoop == null)
                    _bindLoop = StartCoroutine(PollForPlayer());
                return;
            }

            int maxHP      = _runtime.MaxHP;
            int maxStamina = _runtime.MaxStamina;

            float targetHP      = maxHP      > 0 ? Mathf.Clamp01((float)_runtime.CurrentHP      / maxHP)      : 0f;
            float targetStamina = maxStamina > 0 ? Mathf.Clamp01((float)_runtime.CurrentStamina / maxStamina) : 0f;

            // Snap-on-damage / lerp-on-heal — same rule, both bars.
            _displayedHPFraction      = StepFraction(_displayedHPFraction,      targetHP);
            _displayedStaminaFraction = StepFraction(_displayedStaminaFraction, targetStamina);

            hpFill.fillAmount      = _displayedHPFraction;
            staminaFill.fillAmount = _displayedStaminaFraction;

            // Labels show truth, never the lerped fraction.
            hpLabel.text      = $"HP {_runtime.CurrentHP} / {maxHP}";
            staminaLabel.text = $"Stamina {_runtime.CurrentStamina} / {maxStamina}";
        }

        private float StepFraction(float displayed, float target)
        {
            if (target < displayed) return target;                                            // snap down
            if (target > displayed) return Mathf.MoveTowards(displayed, target,               // lerp up
                                                              lerpSpeedHeal * Time.deltaTime);
            return displayed;
        }

        private bool TryBindToPlayer()
        {
            // Warn (don't error) if multiple Player-tagged objects exist;
            // FindWithTag returns the first.
            var all = GameObject.FindGameObjectsWithTag(playerTag);
            if (all.Length > 1)
                Debug.LogWarning($"[PlayerHUD] {all.Length} GameObjects tagged '{playerTag}' in scene. " +
                                 "Binding to the first.", this);

            var player = all.Length > 0 ? all[0] : null;
            if (player == null) return false;

            var runtime = player.GetComponent<CharacterStatsRuntime>();
            if (runtime == null)
            {
                Debug.LogWarning($"[PlayerHUD] '{player.name}' has no CharacterStatsRuntime. " +
                                 "Run 'LevelGen ▶ UI ▶ Add CharacterStatsRuntime to Player_MaleHero' " +
                                 "to wire it.", this);
                return false;
            }

            _runtime = runtime;
            _isBound = true;

            // Initialize displayed fractions to current truth so first frame
            // doesn't lerp up from 0.
            int maxHP      = _runtime.MaxHP;
            int maxStamina = _runtime.MaxStamina;
            _displayedHPFraction      = maxHP      > 0 ? Mathf.Clamp01((float)_runtime.CurrentHP      / maxHP)      : 0f;
            _displayedStaminaFraction = maxStamina > 0 ? Mathf.Clamp01((float)_runtime.CurrentStamina / maxStamina) : 0f;

            return true;
        }

        private IEnumerator PollForPlayer()
        {
            var wait = new WaitForSeconds(0.5f);
            while (!_isBound)
            {
                yield return wait;
                if (TryBindToPlayer()) break;
            }
            _bindLoop = null;
        }
    }
}
