#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LevelGen.V2
{
    /// <summary>
    /// Editor-only helper that enumerates room and hall prefabs from
    /// <c>Assets/Prefabs/Rooms/{Category}/</c> and <c>Assets/Prefabs/Halls/{Size}/</c>,
    /// filtered to those with a <see cref="RoomPiece"/> component on the root.
    /// </summary>
    public static class V2PrefabSource
    {
        /// <summary>Returns all room prefabs under <c>Assets/Prefabs/Rooms/{category}/</c>.</summary>
        public static List<GameObject> GetRoomPrefabs(RoomCategory category) =>
            GetPrefabsAt($"Assets/Prefabs/Rooms/{category}");

        /// <summary>Returns all hall prefabs under <c>Assets/Prefabs/Halls/{size}/</c>.</summary>
        public static List<GameObject> GetHallPrefabs(HallCategory size) =>
            GetPrefabsAt($"Assets/Prefabs/Halls/{size}");

        static List<GameObject> GetPrefabsAt(string folderPath)
        {
            var result = new List<GameObject>();
            if (!AssetDatabase.IsValidFolder(folderPath)) return result;

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                if (prefab.GetComponent<RoomPiece>() == null)
                {
                    Debug.LogWarning($"[V2PrefabSource] Skipping {path}: no RoomPiece on root.");
                    continue;
                }
                result.Add(prefab);
            }
            return result;
        }
    }
}
#endif
