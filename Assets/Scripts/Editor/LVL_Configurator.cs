#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LevelGen
{
    /// <summary>
    /// EditorWindow that processes LVL_ prefabs into generator-ready prefabs by
    /// parsing their names and stamping on <see cref="RoomPiece"/> +
    /// <see cref="ExitPoint"/> components, then saving to the correct output folder
    /// with an <c>_LG</c> suffix.
    ///
    /// Open via: <b>LevelGen ▶ LVL Configurator</b>
    ///
    /// Name-parsing rules:
    /// <list type="bullet">
    ///   <item><b>Size</b> — <c>_large_</c> → halfExtent=3 | <c>_med_</c> → 2
    ///         | <c>_small_</c> → 1 | <c>_tiny_</c> → 0.5</item>
    ///   <item><b>Exits</b> — last underscore-delimited token made entirely of
    ///         direction letters (N/S/E/W/U/D) is the exit set.</item>
    ///   <item><b>Type</b> — "stair"/"hall" in name, or N+S-only / S-only exits
    ///         → saves to Halls folder; otherwise → Rooms folder.</item>
    /// </list>
    /// </summary>
    public class LVL_Configurator : EditorWindow
    {
        // ── Paths ─────────────────────────────────────────────────────────────

        private const string DefaultStartFolder =
            "Assets/Fantastic Dungeon Pack/prefabs/MODULAR";

        private const string RoomsFolder = "Assets/Prefabs/Rooms";
        private const string HallsFolder = "Assets/Prefabs/Halls";

        // ── Window state ──────────────────────────────────────────────────────

        private GameObject       _singlePrefab;
        private Vector2          _scrollPos;
        private readonly List<string> _log = new List<string>();

        // ── Menu ──────────────────────────────────────────────────────────────

        /// <summary>Opens the LVL Configurator window.</summary>
        [MenuItem("LevelGen/LVL Configurator")]
        public static void Open() => GetWindow<LVL_Configurator>("LVL Configurator");

        // ── GUI ───────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            EditorGUILayout.LabelField("LVL_ Prefab Configurator", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // ── Single prefab ─────────────────────────────────────────────────
            EditorGUILayout.LabelField("Single Prefab", EditorStyles.boldLabel);

            _singlePrefab = (GameObject)EditorGUILayout.ObjectField(
                "LVL_ Prefab", _singlePrefab, typeof(GameObject), false);

            GUI.enabled = _singlePrefab != null;
            if (GUILayout.Button("Configure Prefab", GUILayout.Height(26)))
                ConfigureSingle(_singlePrefab);
            GUI.enabled = true;

            EditorGUILayout.Space(8);

            // ── Batch ─────────────────────────────────────────────────────────
            EditorGUILayout.LabelField("Batch", EditorStyles.boldLabel);

            if (GUILayout.Button("Batch Configure Folder", GUILayout.Height(26)))
                RunBatch();

            EditorGUILayout.Space(8);

            // ── Log ───────────────────────────────────────────────────────────
            EditorGUILayout.LabelField("Output Log", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear Log", GUILayout.Width(80)))
                _log.Clear();
            EditorGUILayout.EndHorizontal();

            _scrollPos = EditorGUILayout.BeginScrollView(
                _scrollPos, GUILayout.ExpandHeight(true));

            foreach (string line in _log)
                EditorGUILayout.LabelField(line, EditorStyles.wordWrappedLabel);

            EditorGUILayout.EndScrollView();
        }

        // ── Single-prefab flow ────────────────────────────────────────────────

        /// <summary>
        /// Configures one LVL_ prefab: parses its name, instantiates and fully
        /// unpacks it in the scene, stamps <see cref="RoomPiece"/> and
        /// <see cref="ExitPoint"/> children, saves the result to the correct output
        /// folder with an <c>_LG</c> suffix, then destroys the scene instance.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if the prefab was successfully saved;
        /// <see langword="false"/> if it was skipped or failed.
        /// </returns>
        private bool ConfigureSingle(GameObject prefab)
        {
            if (prefab == null) return false;

            string assetPath  = AssetDatabase.GetAssetPath(prefab);
            string prefabName = Path.GetFileNameWithoutExtension(assetPath);

            // ── STEP 1 — Parse name ───────────────────────────────────────────
            if (!TryParseName(prefabName, out ParsedName parsed))
            {
                LogLine($"SKIP  {prefabName} — no recognised size suffix (_large_/_med_/_small_/_tiny_)");
                return false;
            }

            // ── Debug — classification trace (remove once classification is verified) ──
            {
                int hCount = 0;
                bool hasN = false, hasS = false, hasE = false, hasW = false;
                foreach (ExitPoint.Direction d in parsed.exits)
                {
                    switch (d)
                    {
                        case ExitPoint.Direction.North: hasN = true; hCount++; break;
                        case ExitPoint.Direction.South: hasS = true; hCount++; break;
                        case ExitPoint.Direction.East:  hasE = true; hCount++; break;
                        case ExitPoint.Direction.West:  hasW = true; hCount++; break;
                    }
                }
                Debug.Log($"[LVL_Configurator] Classifying {prefabName}: " +
                          $"horizontalExits={hCount} " +
                          $"exitSet=N:{hasN} S:{hasS} E:{hasE} W:{hasW} " +
                          $"isHall={parsed.isHall}");
            }

            // ── STEP 2 — Instantiate and fully unpack ─────────────────────────
            // HideAndDontSave prevents SaveAsPrefabAsset from seeing child objects,
            // so we instantiate normally and unpack the prefab connection instead.
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(
                instance,
                PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);

            try
            {
                // ── STEP 3 — Add / configure RoomPiece ───────────────────────
                RoomPiece rp = instance.GetComponent<RoomPiece>()
                            ?? instance.AddComponent<RoomPiece>();

                rp.pieceType    = parsed.isHall
                                    ? RoomPiece.PieceType.Hall
                                    : RoomPiece.PieceType.Room;
                rp.boundsSize   = new Vector3(parsed.halfExtent, 3f, parsed.halfExtent);
                rp.boundsOffset = new Vector3(0f, 3f, 0f);

                // ── STEP 4 — (Re-)create ExitPoint children ───────────────────
                // Remove any that may already exist (defensive — raw packs won't have them)
                foreach (ExitPoint ep in instance.GetComponentsInChildren<ExitPoint>())
                    DestroyImmediate(ep.gameObject);

                foreach (ExitPoint.Direction dir in parsed.exits)
                    CreateExitChild(instance, dir, parsed.halfExtent);

                // ── STEP 5 — Save to correct folder with _LG suffix ───────────
                string saveFolder = parsed.isHall ? HallsFolder : RoomsFolder;
                EnsureDirectory(saveFolder);
                string savePath = $"{saveFolder}/{prefabName}_LG.prefab";

                PrefabUtility.SaveAsPrefabAsset(instance, savePath, out bool success);

                // ── STEP 7 — Log result ───────────────────────────────────────
                if (success)
                {
                    AssetDatabase.ImportAsset(savePath);
                    string exitList = parsed.exits.Count > 0
                        ? string.Join("+", parsed.exits)
                        : "none";
                    LogLine($"OK    {prefabName}_LG  " +
                            $"[{(parsed.isHall ? "Hall" : "Room")}  " +
                            $"halfExtent={parsed.halfExtent}  exits={exitList}]");
                    return true;
                }
                else
                {
                    LogLine($"FAIL  {prefabName} — SaveAsPrefabAsset returned false");
                    return false;
                }
            }
            finally
            {
                // ── STEP 6 — Destroy temp instance (always runs) ──────────────
                DestroyImmediate(instance);
            }
        }

        // ── Batch flow ────────────────────────────────────────────────────────

        /// <summary>
        /// Opens a folder picker, scans recursively for prefabs, skips any that
        /// already carry a <see cref="RoomPiece"/> component, then runs
        /// <see cref="ConfigureSingle"/> on the rest. Shows a summary dialog when done.
        /// </summary>
        private void RunBatch()
        {
            // Folder picker
            string absStart = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", DefaultStartFolder))
                .Replace('\\', '/');

            string chosen = EditorUtility.OpenFolderPanel(
                "Select Folder Containing LVL_ Prefabs", absStart, "");

            if (string.IsNullOrEmpty(chosen)) return;

            // Convert to project-relative path
            string projectRoot = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..")).Replace('\\', '/');

            string relFolder = chosen.Replace('\\', '/')
                                     .Replace(projectRoot, "")
                                     .TrimStart('/');

            if (!relFolder.StartsWith("Assets"))
            {
                EditorUtility.DisplayDialog("Error",
                    "Selected folder must be inside the project's Assets folder.", "OK");
                return;
            }

            // Pre-create output folders so EnsureDirectory isn't called per-prefab
            EnsureDirectory(RoomsFolder);
            EnsureDirectory(HallsFolder);

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { relFolder });

            int configured = 0;
            int skipped    = 0;

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var    prefab    = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

                if (prefab == null) { skipped++; continue; }

                // Skip prefabs that already have RoomPiece
                if (prefab.GetComponent<RoomPiece>() != null)
                {
                    LogLine($"SKIP  {prefab.name} — already has RoomPiece");
                    skipped++;
                    continue;
                }

                if (ConfigureSingle(prefab)) configured++;
                else                         skipped++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Repaint();

            EditorUtility.DisplayDialog(
                "Batch Complete",
                $"Configured : {configured}\nSkipped    : {skipped}\n\nTotal found: {guids.Length}",
                "OK");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a child <see cref="GameObject"/> with an <see cref="ExitPoint"/>
        /// component at the correct local position for the given direction.
        /// </summary>
        private static void CreateExitChild(
            GameObject          parent,
            ExitPoint.Direction dir,
            float               halfExtent)
        {
            var child = new GameObject($"Exit_{dir}");
            child.transform.SetParent(parent.transform, false);
            child.transform.localPosition = ExitLocalPos(dir, halfExtent);

            var ep = child.AddComponent<ExitPoint>();
            ep.exitDirection = dir;
        }

        /// <summary>
        /// Returns the local-space position of the exit socket for the given
        /// direction, as documented in CLAUDE.md.
        /// </summary>
        private static Vector3 ExitLocalPos(ExitPoint.Direction dir, float halfExtent)
        {
            return dir switch
            {
                ExitPoint.Direction.North => new Vector3( 0f,  0f,  halfExtent),
                ExitPoint.Direction.South => new Vector3( 0f,  0f, -halfExtent),
                ExitPoint.Direction.East  => new Vector3( halfExtent, 0f, 0f),
                ExitPoint.Direction.West  => new Vector3(-halfExtent, 0f, 0f),
                ExitPoint.Direction.Up    => new Vector3( 0f,  6f,  0f),
                ExitPoint.Direction.Down  => new Vector3( 0f,  0f,  0f),
                _                        => Vector3.zero
            };
        }

        /// <summary>
        /// Parses a LVL_ prefab name to extract half-extent, exit directions, and
        /// whether the piece should be classified as a hall/stair or a room.
        /// Returns <see langword="false"/> if no size suffix is recognised.
        /// </summary>
        private static bool TryParseName(string name, out ParsedName result)
        {
            result = default;
            string lower = name.ToLowerInvariant();

            // ── Size ──────────────────────────────────────────────────────────
            float halfExtent;
            if      (lower.Contains("_large_")) halfExtent = 3f;
            else if (lower.Contains("_med_"))   halfExtent = 2f;
            else if (lower.Contains("_small_")) halfExtent = 1f;
            else if (lower.Contains("_tiny_"))  halfExtent = 0.5f;
            else { return false; }

            // ── Exits — last direction-only token wins ────────────────────────
            // e.g. "LVL_01_M_wall_large_NS" → last token "NS" → North + South
            var    exits  = new List<ExitPoint.Direction>();
            string[] toks = name.Split('_');

            for (int i = toks.Length - 1; i >= 0; i--)
            {
                if (string.IsNullOrEmpty(toks[i])) continue;

                // Only the rightmost non-empty token is a candidate for the exit set.
                // If it consists entirely of direction letters, decode it; otherwise no exits.
                if (IsDirectionToken(toks[i]))
                {
                    foreach (char c in toks[i].ToUpperInvariant())
                    {
                        ExitPoint.Direction? dir = CharToDirection(c);
                        if (dir.HasValue && !exits.Contains(dir.Value))
                            exits.Add(dir.Value);
                    }
                }
                break;  // stop after inspecting the last non-empty token
            }

            // ── PieceType — classify by horizontal exit count ─────────────────────
            // 1 exit  = Hall (dead end)
            // 2 exits = Hall (straight or corner)
            // 3 exits = Hall (T-junction, e.g. SEW)
            // 4 exits = Room (crossroads / open space)
            // stair / hall in name always overrides to Hall save-path
            bool hasNorth = exits.Contains(ExitPoint.Direction.North);
            bool hasSouth = exits.Contains(ExitPoint.Direction.South);
            bool hasEast  = exits.Contains(ExitPoint.Direction.East);
            bool hasWest  = exits.Contains(ExitPoint.Direction.West);

            int hCount = 0;
            if (hasNorth) hCount++;
            if (hasSouth) hCount++;
            if (hasEast)  hCount++;
            if (hasWest)  hCount++;

            bool isHall = lower.Contains("stair")
                       || lower.Contains("hall")
                       || hCount <= 3;

            result = new ParsedName
            {
                halfExtent = halfExtent,
                exits      = exits,
                isHall     = isHall,
            };
            return true;
        }

        /// <summary>
        /// Returns <see langword="true"/> if every character in <paramref name="token"/>
        /// is a valid direction letter (N / S / E / W / U / D).
        /// </summary>
        private static bool IsDirectionToken(string token)
        {
            foreach (char c in token.ToUpperInvariant())
            {
                if (c != 'N' && c != 'S' && c != 'E' &&
                    c != 'W' && c != 'U' && c != 'D')
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Maps a single character to an <see cref="ExitPoint.Direction"/>, or
        /// <see langword="null"/> if not recognised.
        /// </summary>
        private static ExitPoint.Direction? CharToDirection(char c)
        {
            return c switch
            {
                'N' => ExitPoint.Direction.North,
                'S' => ExitPoint.Direction.South,
                'E' => ExitPoint.Direction.East,
                'W' => ExitPoint.Direction.West,
                'U' => ExitPoint.Direction.Up,
                'D' => ExitPoint.Direction.Down,
                _   => (ExitPoint.Direction?)null
            };
        }

        /// <summary>
        /// Creates a folder (and its parent chain) if it does not already exist.
        /// </summary>
        private static void EnsureDirectory(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "Assets";
            string folder = Path.GetFileName(path);

            if (!AssetDatabase.IsValidFolder(parent))
                EnsureDirectory(parent);

            AssetDatabase.CreateFolder(parent, folder);
        }

        /// <summary>Appends a line to the log and requests a window repaint.</summary>
        private void LogLine(string line)
        {
            _log.Add(line);
            Repaint();
        }

        // ── Data ──────────────────────────────────────────────────────────────

        /// <summary>Data extracted from a LVL_ prefab name by <see cref="TryParseName"/>.</summary>
        private struct ParsedName
        {
            /// <summary>Half-extent of the piece's bounding box (X and Z).</summary>
            public float halfExtent;

            /// <summary>Exit directions decoded from the name suffix.</summary>
            public List<ExitPoint.Direction> exits;

            /// <summary>True → save to Halls folder and set PieceType.Hall.</summary>
            public bool isHall;
        }
    }
}
#endif
