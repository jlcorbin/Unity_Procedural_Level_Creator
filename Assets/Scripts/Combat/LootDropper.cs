// LootDropper.cs — spawns loot pickups when the enemy dies (M-Loot).
//
// Subscribes to CharacterStatsRuntime.OnDied, rolls its LootTable, and spawns a
// placeholder pickup per drop at the corpse. Placeholders are runtime-tinted
// primitives (cube = gear, sphere = material, colour = rarity) carrying a
// WorldItem — so real art is a one-line swap later (spawn item.WorldPrefab
// instead of the primitive).

using UnityEngine;
using LevelGen.Items;
using LevelGen.Interaction;

namespace LevelGen.Combat
{
    /// <summary>
    /// Rolls a <see cref="LootTable"/> on death and drops placeholder pickups.
    /// </summary>
    [RequireComponent(typeof(CharacterStatsRuntime))]
    [DisallowMultipleComponent]
    public class LootDropper : MonoBehaviour
    {
        [Header("Loot")]
        [Tooltip("Drops rolled when this enemy dies.")]
        [SerializeField] private LootTable _lootTable;

        [Header("Placeholder drop (blocks)")]
        [Tooltip("Horizontal scatter radius for dropped pickups.")]
        [SerializeField] private float _scatterRadius = 0.6f;

        [Tooltip("Height above the corpse the pickups float at.")]
        [SerializeField] private float _dropHeight = 0.6f;

        [Tooltip("Uniform scale of the placeholder block.")]
        [SerializeField] private float _blockScale = 0.3f;

        private CharacterStatsRuntime _stats;
        private bool _dropped;

        private void Awake()
        {
            _stats = GetComponent<CharacterStatsRuntime>();
        }

        private void OnEnable()
        {
            if (_stats != null) _stats.OnDied += HandleDied;
        }

        private void OnDisable()
        {
            if (_stats != null) _stats.OnDied -= HandleDied;
        }

        private void HandleDied(CharacterStatsRuntime _)
        {
            if (_dropped) return;           // single-fire
            _dropped = true;

            if (_lootTable == null) return;
            foreach (var drop in _lootTable.Roll())
                SpawnPickup(drop.item, drop.count);
        }

        private void SpawnPickup(ItemData item, int count)
        {
            if (item == null) return;

            // Placeholder block: cube = gear, sphere = material.
            var shape = item.IsMaterial ? PrimitiveType.Sphere : PrimitiveType.Cube;
            var go = GameObject.CreatePrimitive(shape);
            go.name = $"Pickup_{item.name}";

            // Drop the solid collider CreatePrimitive adds — the WorldItem's
            // trigger SphereCollider handles interaction; we don't want it to
            // block the player. DestroyImmediate (not Destroy) so it's gone
            // BEFORE WorldItem's RequireComponent runs — otherwise a still-pending
            // SphereCollider (sphere primitive) would satisfy RequireComponent and
            // then get destroyed, leaving WorldItem with no trigger.
            var solid = go.GetComponent<Collider>();
            if (solid != null) DestroyImmediate(solid);

            // Position: scatter around the corpse, floating at drop height.
            Vector2 off = Random.insideUnitCircle * _scatterRadius;
            go.transform.position = transform.position + new Vector3(off.x, _dropHeight, off.y);
            go.transform.localScale = Vector3.one * _blockScale;

            // Tint by rarity with a URP material (runtime primitives are magenta
            // in URP without one).
            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader != null)
                {
                    var mat = new Material(shader) { color = RarityColors.For(item.Rarity) };
                    rend.sharedMaterial = mat;
                }
                else
                {
                    rend.material.color = RarityColors.For(item.Rarity);
                }
            }

            // Configure the pickup while INACTIVE so WorldItem.Awake runs after
            // Initialize sets the item (Awake enriches the prompt from it).
            go.SetActive(false);
            var wi = go.AddComponent<WorldItem>();   // auto-adds the required SphereCollider
            wi.Initialize(item, count);
            go.SetActive(true);
        }
    }
}
