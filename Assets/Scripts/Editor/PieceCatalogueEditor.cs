using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace LevelGen
{
    /// <summary>
    /// Custom inspector for <see cref="PieceCatalogue"/>.
    ///
    /// Provides:
    ///   • Auto-Populate from Folder  — scans the Fantastic Dungeon Pack MODULAR folder
    ///   • Auto-Populate from Second Pack — same scan starting from Assets/ root
    ///   • Clear All Pieces            — wipes the list with a confirmation dialog
    ///   • Read-only breakdown         — piece counts per type + total
    ///
    /// Subfolder → PieceType mapping (applied to the full relative sub-path):
    ///   WallCover          → Ceiling   (checked BEFORE "Trim" to capture Trim/WallCover)
    ///   OneSided           → skip
    ///   Railing            → skip
    ///   Trim               → skip      (WallTrim etc., but NOT WallCover — caught above)
    ///   Props / PROPS      → skip
    ///   Floor              → Floor
    ///   Gateway            → Doorway
    ///   Column             → Column
    ///   Wall + Middle      → Wall      (matches PivotMiddle subfolders)
    ///   Stair              → Stair
    ///   (no match)         → skip
    /// </summary>
    [CustomEditor(typeof(PieceCatalogue))]
    public class PieceCatalogueEditor : Editor
    {
        // Default starting folder for the primary auto-populate picker
        private const string FdpDefaultFolder =
            "Assets/Fantastic Dungeon Pack/prefabs/MODULAR";

        private ReorderableList _piecesList;

        // ── Filter state ──────────────────────────────────────────────────────
        private int      _filterTypeIndex    = 0;   // 0 = All; 1..N = specific PieceType
        private int      _filterNameIndex    = 0;   // 0 = All; 1..N = specific prefab name
        private string[] _nameOptions;              // rebuilt when type filter or piece count changes
        private int      _lastPieceCount     = -1;  // detect catalogue edits that require a rebuild
        private int      _pendingDeleteIndex = -1;  // deletion deferred out of the ReorderableList callback

        private void OnEnable()
        {
            var piecesProp = serializedObject.FindProperty("pieces");
            _piecesList = new ReorderableList(
                serializedObject, piecesProp,
                draggable: true, displayHeader: true,
                displayAddButton: true, displayRemoveButton: true);

            _piecesList.drawHeaderCallback = rect =>
                EditorGUI.LabelField(rect, "Pieces");

            _piecesList.elementHeightCallback = index =>
            {
                var el = _piecesList.serializedProperty.GetArrayElementAtIndex(index);
                float lh  = EditorGUIUtility.singleLineHeight;
                float pad = 2f;
                // +1 for the header row (label + ✕ button); Doorway adds isExit row
                int rows  = el.FindPropertyRelative("pieceType").enumValueIndex ==
                            (int)PieceCatalogue.PieceType.Doorway ? 5 : 4;
                return rows * (lh + pad) + pad;
            };

            _piecesList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                var el  = _piecesList.serializedProperty.GetArrayElementAtIndex(index);
                float lh  = EditorGUIUtility.singleLineHeight;
                float pad = 2f;
                float rowH = lh + pad;
                float btnW = 24f;

                // ── Header row: label + ✕ delete button ──────────────────────
                float headerY = rect.y + pad;
                EditorGUI.LabelField(
                    new Rect(rect.x, headerY, rect.width - btnW - pad, lh),
                    $"Element {index}", EditorStyles.boldLabel);
                GUI.backgroundColor = new Color(1f, 0.35f, 0.35f);
                if (GUI.Button(new Rect(rect.x + rect.width - btnW, headerY, btnW, lh), "✕"))
                    _pendingDeleteIndex = index;
                GUI.backgroundColor = Color.white;

                // ── Fields (shifted down one row) ─────────────────────────────
                EditorGUI.PropertyField(
                    new Rect(rect.x, rect.y + pad + rowH, rect.width, lh),
                    el.FindPropertyRelative("prefab"));
                EditorGUI.PropertyField(
                    new Rect(rect.x, rect.y + pad + rowH * 2, rect.width, lh),
                    el.FindPropertyRelative("pieceType"));
                EditorGUI.PropertyField(
                    new Rect(rect.x, rect.y + pad + rowH * 3, rect.width, lh),
                    el.FindPropertyRelative("subFolder"));

                if (el.FindPropertyRelative("pieceType").enumValueIndex ==
                    (int)PieceCatalogue.PieceType.Doorway)
                    EditorGUI.PropertyField(
                        new Rect(rect.x, rect.y + pad + rowH * 4, rect.width, lh),
                        el.FindPropertyRelative("isExit"));
            };
        }

        public override void OnInspectorGUI()
        {
            var cat = (PieceCatalogue)target;

            // ── Theme field ───────────────────────────────────────────────────
            serializedObject.UpdateIfRequiredOrScript();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("theme"));
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(4);

            // ── Auto-populate buttons ─────────────────────────────────────────
            EditorGUILayout.LabelField("Auto-Populate", EditorStyles.boldLabel);

            // Capture clicks as booleans INSIDE the layout group, then call DoAutoPopulate
            // OUTSIDE it. OpenFolderPanel calls GUIUtility.ExitGUI() which throws
            // ExitGUIException — if that happens while a BeginHorizontal is still open,
            // Unity tears down its internal layout stack before the finally block runs,
            // causing "EndLayoutGroup: BeginLayoutGroup must be called first" at line 64.
            // Deferring the call to after EndHorizontal eliminates any open group.
            bool clickedFolder     = false;
            bool clickedSecondPack = false;

            EditorGUILayout.BeginHorizontal();
            try
            {
                clickedFolder     = GUILayout.Button("Auto-Populate from Folder",      GUILayout.Height(26));
                clickedSecondPack = GUILayout.Button("Auto-Populate from Second Pack", GUILayout.Height(26));
            }
            finally
            {
                EditorGUILayout.EndHorizontal();
            }

            // DoAutoPopulate (and its OpenFolderPanel call) runs here — outside any layout group.
            if (clickedFolder)     DoAutoPopulate(cat, FdpDefaultFolder);
            if (clickedSecondPack) DoAutoPopulate(cat, "Assets");

            EditorGUILayout.Space(4);

            // ── Clear button ──────────────────────────────────────────────────
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("Clear All Pieces", GUILayout.Height(24)))
            {
                if (EditorUtility.DisplayDialog(
                    "Clear All Pieces",
                    $"Remove all {cat.pieces.Count} entries from '{cat.name}'?\nThis cannot be undone.",
                    "Clear", "Cancel"))
                {
                    Undo.RecordObject(cat, "Clear PieceCatalogue");
                    cat.pieces.Clear();
                    EditorUtility.SetDirty(cat);
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(4);

            // ── Debug: log all entries ────────────────────────────────────────
            if (GUILayout.Button("Log All Pieces", GUILayout.Height(22)))
            {
                Debug.Log($"[PieceCatalogue] '{cat.name}' — {cat.pieces.Count} entries:");
                foreach (var entry in cat.pieces)
                {
                    string prefabName = entry.prefab != null ? entry.prefab.name : "NULL";
                    Debug.Log($"  {entry.pieceType}: {prefabName}  from '{entry.subFolder}'");
                }
            }

            EditorGUILayout.Space(6);

            // ── Breakdown ─────────────────────────────────────────────────────
            EditorGUILayout.LabelField("Catalogue Breakdown", EditorStyles.boldLabel);

            int total = 0;
            foreach (PieceCatalogue.PieceType pt in System.Enum.GetValues(typeof(PieceCatalogue.PieceType)))
            {
                int n = cat.CountOfType(pt);
                total += n;
                EditorGUILayout.BeginHorizontal();
                try
                {
                    EditorGUILayout.LabelField($"  {pt}", GUILayout.Width(90));
                    EditorGUILayout.LabelField($"{n} piece(s)", EditorStyles.miniLabel);
                }
                finally
                {
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.BeginHorizontal();
            try
            {
                EditorGUILayout.LabelField("  Total", GUILayout.Width(90));
                EditorGUILayout.LabelField($"{total}", EditorStyles.boldLabel);
            }
            finally
            {
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(6);

            // ── Filter ────────────────────────────────────────────────────────
            EditorGUILayout.LabelField("Filter", EditorStyles.boldLabel);

            // PieceType filter — "All" + each enum value
            var allTypes = (PieceCatalogue.PieceType[])System.Enum.GetValues(
                               typeof(PieceCatalogue.PieceType));
            var typeOptions = new string[allTypes.Length + 1];
            typeOptions[0] = "All";
            for (int i = 0; i < allTypes.Length; i++)
                typeOptions[i + 1] = allTypes[i].ToString();

            int newTypeIndex = EditorGUILayout.Popup("Piece Type", _filterTypeIndex, typeOptions);
            bool typeChanged = newTypeIndex != _filterTypeIndex;
            if (typeChanged)
            {
                _filterTypeIndex = newTypeIndex;
                _filterNameIndex = 0;   // reset name selection when type filter changes
            }

            // Rebuild name option list when type changes or catalogue contents change
            if (_nameOptions == null || typeChanged || cat.pieces.Count != _lastPieceCount)
            {
                RebuildNameOptions(cat, allTypes);
                _lastPieceCount = cat.pieces.Count;
            }

            // Clamp in case the list shrank since last frame
            _filterNameIndex = Mathf.Clamp(_filterNameIndex, 0, _nameOptions.Length - 1);
            _filterNameIndex = EditorGUILayout.Popup("Prefab", _filterNameIndex, _nameOptions);

            EditorGUILayout.Space(4);

            // ── Pieces list ───────────────────────────────────────────────────
            bool anyFilter = _filterTypeIndex > 0 || _filterNameIndex > 0;

            if (anyFilter)
            {
                // Resolve active filters
                PieceCatalogue.PieceType? typeFilter = _filterTypeIndex > 0
                    ? allTypes[_filterTypeIndex - 1]
                    : (PieceCatalogue.PieceType?)null;
                string nameFilter = _filterNameIndex > 0
                    ? _nameOptions[_filterNameIndex]
                    : null;

                // Collect real indices of matching entries
                var filtered = new List<int>();
                for (int i = 0; i < cat.pieces.Count; i++)
                {
                    var e = cat.pieces[i];
                    if (typeFilter.HasValue && e.pieceType != typeFilter.Value) continue;
                    if (nameFilter != null && (e.prefab == null || e.prefab.name != nameFilter)) continue;
                    filtered.Add(i);
                }

                EditorGUILayout.LabelField(
                    $"Showing {filtered.Count} / {cat.pieces.Count} pieces",
                    EditorStyles.miniLabel);
                EditorGUILayout.HelpBox(
                    "Filters active — +/- disabled. Clear filters to add or remove entries.",
                    MessageType.Info);

                serializedObject.UpdateIfRequiredOrScript();
                var piecesProp = serializedObject.FindProperty("pieces");
                foreach (int idx in filtered)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                    // Header row: real index label + ✕ delete button
                    bool doDelete = false;
                    EditorGUILayout.BeginHorizontal();
                    try
                    {
                        EditorGUILayout.LabelField($"Element {idx}", EditorStyles.boldLabel);
                        GUI.backgroundColor = new Color(1f, 0.35f, 0.35f);
                        doDelete = GUILayout.Button("✕", GUILayout.Width(20), GUILayout.Height(18));
                        GUI.backgroundColor = Color.white;
                    }
                    finally
                    {
                        EditorGUILayout.EndHorizontal();
                    }

                    DrawPieceEntryLayout(piecesProp.GetArrayElementAtIndex(idx));
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(2);

                    if (doDelete)
                    {
                        piecesProp.DeleteArrayElementAtIndex(idx);
                        serializedObject.ApplyModifiedProperties();
                        // Reset name filter in case it pointed at the deleted entry
                        _filterNameIndex = 0;
                        _nameOptions = null;
                        return;
                    }
                }
                serializedObject.ApplyModifiedProperties();
            }
            else
            {
                EditorGUILayout.LabelField(
                    $"Showing {cat.pieces.Count} / {cat.pieces.Count} pieces",
                    EditorStyles.miniLabel);
                serializedObject.UpdateIfRequiredOrScript();
                _piecesList.DoLayoutList();
                serializedObject.ApplyModifiedProperties();

                // Process deferred ✕ deletion (cannot delete inside the ReorderableList callback)
                if (_pendingDeleteIndex >= 0)
                {
                    int toDelete = _pendingDeleteIndex;
                    _pendingDeleteIndex = -1;
                    serializedObject.FindProperty("pieces").DeleteArrayElementAtIndex(toDelete);
                    serializedObject.ApplyModifiedProperties();
                    _filterNameIndex = 0;
                    _nameOptions = null;
                    return;
                }
            }
        }

        // ── Auto-populate logic ───────────────────────────────────────────────

        /// <summary>
        /// Opens a folder picker starting at <paramref name="startFolder"/>, then
        /// recursively scans the chosen folder for prefabs, maps them to PieceType
        /// via subfolder name rules, and appends new entries (skipping duplicates).
        /// Shows a summary dialog when done.
        /// </summary>
        private static void DoAutoPopulate(PieceCatalogue cat, string startFolder)
        {
            // ── Folder picker ─────────────────────────────────────────────────
            string absStart = Path.GetFullPath(Path.Combine(Application.dataPath, "..", startFolder))
                                  .Replace('\\', '/');

            string chosen = EditorUtility.OpenFolderPanel(
                "Select Asset Pack MODULAR Folder", absStart, "");

            if (string.IsNullOrEmpty(chosen)) return;

            // Convert absolute path → project-relative
            string projectRoot = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..")).Replace('\\', '/');

            if (!chosen.Replace('\\', '/').StartsWith(projectRoot))
            {
                EditorUtility.DisplayDialog("Error",
                    "Selected folder must be inside the project's Assets folder.", "OK");
                return;
            }

            string folderRelative = chosen.Replace('\\', '/')
                                          .Substring(projectRoot.Length)
                                          .TrimStart('/');

            // Must start with "Assets/"
            if (!folderRelative.StartsWith("Assets"))
            {
                EditorUtility.DisplayDialog("Error",
                    "Could not resolve a project-relative path from the selection.", "OK");
                return;
            }

            // ── Scan ──────────────────────────────────────────────────────────
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folderRelative });

            // Build a map of already-catalogued entries by asset path for duplicate detection
            // and isExit preservation (so re-running auto-populate doesn't reset user-set values).
            var existingPaths = new HashSet<string>();
            var existingIsExit = new Dictionary<string, bool>();
            foreach (var entry in cat.pieces)
            {
                if (entry.prefab == null) continue;
                string ap = AssetDatabase.GetAssetPath(entry.prefab);
                existingPaths.Add(ap);
                existingIsExit[ap] = entry.isExit;
            }

            int added   = 0;
            int skipped = 0;

            Undo.RecordObject(cat, "Auto-Populate PieceCatalogue");

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);

                // Duplicate check
                if (existingPaths.Contains(assetPath))
                {
                    skipped++;
                    continue;
                }

                // Build the sub-path relative to the chosen folder
                string subPath = assetPath;
                if (assetPath.Replace('\\', '/').StartsWith(folderRelative))
                    subPath = assetPath.Substring(folderRelative.Length).TrimStart('/', '\\');

                // Map subfolder to PieceType; null = skip
                if (!TryMapSubPath(subPath, out PieceCatalogue.PieceType pieceType))
                {
                    skipped++;
                    continue;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab == null)
                {
                    skipped++;
                    continue;
                }

                cat.pieces.Add(new PieceCatalogue.PieceEntry
                {
                    prefab    = prefab,
                    pieceType = pieceType,
                    subFolder = Path.GetDirectoryName(subPath)?.Replace('\\', '/') ?? "",
                    isExit    = existingIsExit.TryGetValue(assetPath, out bool savedIsExit) && savedIsExit,
                });

                existingPaths.Add(assetPath);
                added++;
            }

            EditorUtility.SetDirty(cat);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog(
                "Auto-Populate Complete",
                $"Added   : {added}\nSkipped : {skipped}\n\nTotal in catalogue: {cat.pieces.Count}",
                "OK");
        }

        // ── Filter helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Rebuilds <see cref="_nameOptions"/> from the entries that pass the current
        /// PieceType filter, sorted alphabetically. Index 0 is always "All".
        /// </summary>
        private void RebuildNameOptions(PieceCatalogue cat, PieceCatalogue.PieceType[] allTypes)
        {
            PieceCatalogue.PieceType? typeFilter = _filterTypeIndex > 0
                ? allTypes[_filterTypeIndex - 1]
                : (PieceCatalogue.PieceType?)null;

            var names = new SortedSet<string>();
            foreach (var e in cat.pieces)
            {
                if (e.prefab == null) continue;
                if (typeFilter.HasValue && e.pieceType != typeFilter.Value) continue;
                names.Add(e.prefab.name);
            }

            _nameOptions = new string[names.Count + 1];
            _nameOptions[0] = "All";
            int i = 1;
            foreach (var n in names)
                _nameOptions[i++] = n;
        }

        /// <summary>
        /// Draws the editable fields for a single <see cref="PieceCatalogue.PieceEntry"/>
        /// using <c>EditorGUILayout</c> (used in the filtered read-only view).
        /// <c>isExit</c> is only drawn when <c>pieceType == Doorway</c>.
        /// </summary>
        private static void DrawPieceEntryLayout(SerializedProperty el)
        {
            EditorGUILayout.PropertyField(el.FindPropertyRelative("prefab"));
            EditorGUILayout.PropertyField(el.FindPropertyRelative("pieceType"));
            EditorGUILayout.PropertyField(el.FindPropertyRelative("subFolder"));
            if (el.FindPropertyRelative("pieceType").enumValueIndex ==
                (int)PieceCatalogue.PieceType.Doorway)
                EditorGUILayout.PropertyField(el.FindPropertyRelative("isExit"));
        }

        // ── Subfolder mapping ─────────────────────────────────────────────────

        /// <summary>
        /// Maps the sub-path of a prefab (relative to the scanned root) to a PieceType.
        /// Returns false if the prefab should be skipped entirely.
        /// </summary>
        private static bool TryMapSubPath(string subPath, out PieceCatalogue.PieceType pieceType)
        {
            // Normalise separators so all checks work on forward slashes
            subPath = subPath.Replace('\\', '/');

            // ── WallCover → Ceiling (checked BEFORE Trim to capture Trim/WallCover) ──
            if (subPath.Contains("WallCover"))
            {
                pieceType = PieceCatalogue.PieceType.Ceiling;
                return true;
            }

            // ── Hard skip rules ───────────────────────────────────────────────
            if (subPath.Contains("OneSided")  ||
                subPath.Contains("Railing")   ||
                subPath.Contains("Trim")      ||
                subPath.Contains("Props")     ||
                subPath.Contains("PROPS")     ||
                subPath.Contains("PivotEdge"))
            {
                pieceType = default;
                return false;
            }

            // ── Positive mappings ─────────────────────────────────────────────
            if (subPath.Contains("Floor"))
            {
                pieceType = PieceCatalogue.PieceType.Floor;
                return true;
            }

            if (subPath.Contains("Gateway"))
            {
                pieceType = PieceCatalogue.PieceType.Doorway;
                return true;
            }

            if (subPath.Contains("Column"))
            {
                pieceType = PieceCatalogue.PieceType.Column;
                return true;
            }

            // Wall must also contain "Middle" to avoid PivotEdge / OneSided.
            // Further split by shape keyword in the asset filename:
            //   straight              → Wall  (standard wall run pieces)
            //   corner / angle / concave → Corner (junction pieces)
            if (subPath.Contains("Wall") && subPath.Contains("Middle"))
            {
                string lower = subPath.ToLower();
                if (lower.Contains("corner") || lower.Contains("angle") || lower.Contains("concave"))
                {
                    pieceType = PieceCatalogue.PieceType.Corner;
                    return true;
                }
                pieceType = PieceCatalogue.PieceType.Wall;
                return true;
            }

            if (subPath.Contains("Stair"))
            {
                pieceType = PieceCatalogue.PieceType.Stair;
                return true;
            }

            // No rule matched — skip
            pieceType = default;
            return false;
        }

    }
}
