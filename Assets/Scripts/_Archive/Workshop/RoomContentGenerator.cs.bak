using UnityEngine;

namespace LevelGen
{
    /// <summary>
    /// Static utility that dresses a built room by filling <see cref="SpawnPoint"/>
    /// children with props drawn from a <see cref="PropCatalogue"/>.
    /// </summary>
    public static class RoomContentGenerator
    {
        /// <summary>
        /// Finds all <see cref="SpawnPoint"/> children of <paramref name="roomRoot"/>
        /// and instantiates a prop at each unoccupied point.
        ///
        /// Prop selection is filtered by the SpawnPoint's type and size, then weighted
        /// by <see cref="PropCatalogue"/> entries. Pass a non-empty theme string to
        /// further restrict which props are eligible.
        /// </summary>
        /// <param name="roomRoot">Root transform of the built room.</param>
        /// <param name="catalogue">Prop catalogue to sample from.</param>
        /// <param name="theme">Optional theme filter (empty = any theme).</param>
        /// <param name="rng">Seeded RNG for deterministic placement.</param>
        /// <returns>Number of props successfully placed.</returns>
        public static int DressRoom(Transform roomRoot, PropCatalogue catalogue,
                                     string theme, System.Random rng)
        {
            if (roomRoot   == null) { Debug.LogWarning("[RoomContentGenerator] roomRoot is null."); return 0; }
            if (catalogue  == null) { Debug.LogWarning("[RoomContentGenerator] catalogue is null."); return 0; }

            var spawnPoints = roomRoot.GetComponentsInChildren<SpawnPoint>();
            int placed      = 0;

            foreach (var sp in spawnPoints)
            {
                if (sp.isOccupied) continue;

                var prefab = catalogue.GetRandom(sp.spawnType, sp.spawnSize, theme, rng);
                if (prefab == null) continue;

                InstantiateProp(prefab, sp, rng);
                sp.isOccupied = true;
                placed++;
            }

            return placed;
        }

        /// <summary>
        /// Removes all props placed under SpawnPoint children and resets their
        /// <see cref="SpawnPoint.isOccupied"/> flags.
        /// </summary>
        /// <param name="roomRoot">Root transform of the room to clear.</param>
        public static void ClearDressing(Transform roomRoot)
        {
            if (roomRoot == null) return;

            var spawnPoints = roomRoot.GetComponentsInChildren<SpawnPoint>();
            foreach (var sp in spawnPoints)
            {
                for (int i = sp.transform.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(sp.transform.GetChild(i).gameObject);
                sp.isOccupied = false;
            }
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private static void InstantiateProp(GameObject prefab, SpawnPoint sp, System.Random rng)
        {
            GameObject go;
#if UNITY_EDITOR
            go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, sp.transform);
#else
            go = Object.Instantiate(prefab, sp.transform);
#endif
            go.transform.localPosition = Vector3.zero;
            // Randomise Y rotation in 90-degree steps for natural variety
            go.transform.localRotation = Quaternion.Euler(0f, rng.Next(4) * 90f, 0f);
        }
    }
}
