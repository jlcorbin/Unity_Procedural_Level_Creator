using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace LevelGen
{
    /// <summary>
    /// Custom inspector for <see cref="PieceCatalogue"/>.
    ///
    /// Renders one foldout section per PieceType (Floor → Stair) each backed by its own
    /// ReorderableList, followed by a distinct Skipped section for PieceType.None entries.
    /// The underlying storage (PieceCatalogue.pieces as a single List&lt;PieceEntry&gt;) is unchanged.
    ///
    /// Subfolder → PieceType mapping (auto-populate):
    ///   WallCover        → Ceiling   (checked before Trim to capture Trim/WallCover)
    ///   OneSided         → None      (staged — user promotes via Move)
    ///   Railing          → None
    ///   Trim             → None
    ///   Props / PROPS    → None
    ///   PivotEdge        → None
    ///   Floor            → Floor
    ///   Gateway          → Doorway
    ///   Column           → Column
    ///   Wall + Middle    → Wall or Corner (by filename keyword)
    ///   Stair            → Stair
    ///   (no match)       → None
    /// </summary>
    [CustomEditor(typeof(PieceCatalogue))]
    public class PieceCatalogueEditor : Editor
    {
        private const string FdpDefaultFolder =
            "Assets/Fantastic Dungeon Pack/prefabs/MODULAR";

        // All enum values in ascending order (includes None = 99 at the end).
        private static readonly PieceCatalogue.PieceType[] AllTypes =
            (PieceCatalogue.PieceType[])System.Enum.GetValues(typeof(PieceCatalogue.PieceType));

        // Generator-visible types only (excludes None).
        private static readonly PieceCatalogue.PieceType[] NormalTypes =
            AllTypes.Where(t => t != PieceCatalogue.PieceType.None).ToArray();

        // ── Section state ─────────────────────────────────────────────────────

        private class SectionState
        {
            public PieceCatalogue.PieceType type;
            public List<int>       realIndices            = new List<int>();
            public ReorderableList list;
            public int             pendingDeleteRealIndex = -1;
            public int             pendingMoveRealIndex   = -1; // Skipped section only
            public string          foldoutPrefKey;
            public bool            foldoutOpen            = true;
        }

        private Dictionary<PieceCatalogue.PieceType, SectionState> _sections;

        // Pending destination type per real-index in the Skipped section.
        // Keys are real indices into cat.pieces; cleared on every RebuildAllSections.
        private readonly Dictionary<int, PieceCatalogue.PieceType> _pendingMoveTypes =
            new Dictionary<int, PieceCatalogue.PieceType>();

        // One-frame message shown after a successful Move.
        private string _moveMessage;

        // ── Filter state ──────────────────────────────────────────────────────

        private int      _filterTypeIndex = 0;
        private int      _filterNameIndex = 0;
        private string[] _nameOptions;
        private int      _lastPieceCount  = -1;

        // ── OnEnable ──────────────────────────────────────────────────────────

        private void OnEnable()
        {
            var    cat       = (PieceCatalogue)target;
            string assetPath = AssetDatabase.GetAssetPath(cat);
            string guid      = AssetDatabase.AssetPathToGUID(assetPath);

            _sections = new Dictionary<PieceCatalogue.PieceType, SectionState>();
            _pendingMoveTypes.Clear();

            foreach (var pt in AllTypes)
            {
                var s = new SectionState { type = pt };
                s.foldoutPrefKey = $"PieceCatalogueEditor_{guid}_{pt}";
                // Skipped (None) section defaults to collapsed; real types default to open.
                bool defaultOpen = pt != PieceCatalogue.PieceType.None;
                s.foldoutOpen    = EditorPrefs.GetBool(s.foldoutPrefKey, defaultOpen);

                s.list = new ReorderableList(
                    s.realIndices, typeof(int),
                    draggable: true, displayHeader: false,
                    displayAddButton: true, displayRemoveButton: false);
                s.list.headerHeight = 0f;

                if (pt == PieceCatalogue.PieceType.None)
                    SetupSkippedListCallbacks(s, cat);
                else
                    SetupNormalListCallbacks(s, cat);

                _sections[pt] = s;
            }

            RebuildAllSections(cat);
        }

        // ── List callback wiring — normal sections ────────────────────────────

        private void SetupNormalListCallbacks(SectionState s, PieceCatalogue cat)
        {
            s.list.elementHeightCallback = viewIdx =>
            {
                if (viewIdx < 0 || viewIdx >= s.realIndices.Count)
                    return EditorGUIUtility.singleLineHeight + 4f;
                int ri = s.realIndices[viewIdx];
                if (ri >= cat.pieces.Count)
                    return EditorGUIUtility.singleLineHeight + 4f;
                float lh  = EditorGUIUtility.singleLineHeight;
                float pad = 2f;
                int   rows = cat.pieces[ri].pieceType == PieceCatalogue.PieceType.Doorway ? 5 : 4;
                return rows * (lh + pad) + pad;
            };

            s.list.drawElementCallback = (rect, viewIdx, isActive, isFocused) =>
            {
                if (viewIdx < 0 || viewIdx >= s.realIndices.Count) return;
                int ri = s.realIndices[viewIdx];
                if (ri >= cat.pieces.Count) return;

                var piecesProp = serializedObject.FindProperty("pieces");
                var el   = piecesProp.GetArrayElementAtIndex(ri);
                float lh   = EditorGUIUtility.singleLineHeight;
                float pad  = 2f;
                float rowH = lh + pad;
                float btnW = 24f;

                float headerY = rect.y + pad;
                EditorGUI.LabelField(
                    new Rect(rect.x, headerY, rect.width - btnW - pad, lh),
                    $"Element {ri}", EditorStyles.boldLabel);
                GUI.backgroundColor = new Color(1f, 0.35f, 0.35f);
                if (GUI.Button(new Rect(rect.x + rect.width - btnW, headerY, btnW, lh), "✕"))
                    s.pendingDeleteRealIndex = ri;
                GUI.backgroundColor = Color.white;

                EditorGUI.PropertyField(
                    new Rect(rect.x, rect.y + pad + rowH,     rect.width, lh),
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

            s.list.drawNoneElementCallback = rect =>
                EditorGUI.LabelField(rect, "No pieces.", EditorStyles.centeredGreyMiniLabel);

            s.list.onAddCallback = _ => OnSectionAdd(s, cat);
            s.list.onReorderCallbackWithDetails = (_, oi, ni) => OnSectionReorder(s, cat);
        }

        // ── List callback wiring — Skipped (None) section ────────────────────

        private void SetupSkippedListCallbacks(SectionState s, PieceCatalogue cat)
        {
            // Destination popup labels: index 0 = keep in Skipped; 1..N = NormalTypes.
            string[] destLabels = new string[NormalTypes.Length + 1];
            destLabels[0] = "— keep in Skipped";
            for (int i = 0; i < NormalTypes.Length; i++)
                destLabels[i + 1] = NormalTypes[i].ToString();

            s.list.elementHeightCallback = viewIdx =>
            {
                if (viewIdx < 0 || viewIdx >= s.realIndices.Count)
                    return EditorGUIUtility.singleLineHeight + 4f;
                int ri = s.realIndices[viewIdx];
                float lh  = EditorGUIUtility.singleLineHeight;
                float pad = 2f;
                bool hasPending = _pendingMoveTypes.TryGetValue(ri, out var pendingType);
                // rows: header(0) + prefab(1) + destination(2) + subFolder(3) + moveRow(4)
                // +1 for isExit when pending type is Doorway
                int rows = 5;
                if (hasPending && pendingType == PieceCatalogue.PieceType.Doorway) rows++;
                return rows * (lh + pad) + pad;
            };

            s.list.drawElementCallback = (rect, viewIdx, isActive, isFocused) =>
            {
                if (viewIdx < 0 || viewIdx >= s.realIndices.Count) return;
                int ri = s.realIndices[viewIdx];
                if (ri >= cat.pieces.Count) return;

                var piecesProp = serializedObject.FindProperty("pieces");
                var el   = piecesProp.GetArrayElementAtIndex(ri);
                float lh   = EditorGUIUtility.singleLineHeight;
                float pad  = 2f;
                float rowH = lh + pad;
                float btnW = 24f;

                // Header row + ✕
                float headerY = rect.y + pad;
                EditorGUI.LabelField(
                    new Rect(rect.x, headerY, rect.width - btnW - pad, lh),
                    $"Element {ri}", EditorStyles.boldLabel);
                GUI.backgroundColor = new Color(1f, 0.35f, 0.35f);
                if (GUI.Button(new Rect(rect.x + rect.width - btnW, headerY, btnW, lh), "✕"))
                    s.pendingDeleteRealIndex = ri;
                GUI.backgroundColor = Color.white;

                // Prefab
                EditorGUI.PropertyField(
                    new Rect(rect.x, rect.y + pad + rowH, rect.width, lh),
                    el.FindPropertyRelative("prefab"));

                // Destination type popup (controls _pendingMoveTypes, not serialized pieceType)
                bool hasPending = _pendingMoveTypes.TryGetValue(ri, out var pendingType);
                int  pendingIdx = 0;
                if (hasPending)
                {
                    int found = System.Array.IndexOf(NormalTypes, pendingType);
                    if (found >= 0) pendingIdx = found + 1;
                }

                int newPendingIdx = EditorGUI.Popup(
                    new Rect(rect.x, rect.y + pad + rowH * 2, rect.width, lh),
                    "Destination", pendingIdx, destLabels);

                if (newPendingIdx != pendingIdx)
                {
                    if (newPendingIdx == 0)
                        _pendingMoveTypes.Remove(ri);
                    else
                        _pendingMoveTypes[ri] = NormalTypes[newPendingIdx - 1];
                    hasPending = _pendingMoveTypes.TryGetValue(ri, out pendingType);
                }

                // subFolder
                EditorGUI.PropertyField(
                    new Rect(rect.x, rect.y + pad + rowH * 3, rect.width, lh),
                    el.FindPropertyRelative("subFolder"));

                // isExit row — shown only when pending type is Doorway
                int extraRows = 0;
                if (hasPending && pendingType == PieceCatalogue.PieceType.Doorway)
                {
                    EditorGUI.PropertyField(
                        new Rect(rect.x, rect.y + pad + rowH * 4, rect.width, lh),
                        el.FindPropertyRelative("isExit"));
                    extraRows = 1;
                }

                // Move row: "Will move to: X" label + Move button
                float moveY  = rect.y + pad + rowH * (4 + extraRows);
                bool  canMove = hasPending;
                float moveW  = 56f;
                float labelW = rect.width - moveW - pad;

                EditorGUI.LabelField(
                    new Rect(rect.x, moveY, labelW, lh),
                    canMove ? $"Will move to: {pendingType}" : "",
                    EditorStyles.miniLabel);

                GUI.enabled = canMove;
                if (GUI.Button(new Rect(rect.x + labelW + pad, moveY, moveW, lh), "Move"))
                    s.pendingMoveRealIndex = ri;
                GUI.enabled = true;
            };

            s.list.drawNoneElementCallback = rect =>
                EditorGUI.LabelField(rect, "No staged pieces.", EditorStyles.centeredGreyMiniLabel);

            s.list.onAddCallback = _ => OnSectionAdd(s, cat);
            s.list.onReorderCallbackWithDetails = (_, oi, ni) => OnSectionReorder(s, cat);
        }

        // ── Section mutations ─────────────────────────────────────────────────

        private void RebuildAllSections(PieceCatalogue cat)
        {
            foreach (var s in _sections.Values)
                RebuildSection(s, cat);
            _pendingMoveTypes.Clear(); // real indices may have shifted after any mutation
            _lastPieceCount = cat.pieces.Count;
        }

        private static void RebuildSection(SectionState s, PieceCatalogue cat)
        {
            s.realIndices.Clear();
            for (int i = 0; i < cat.pieces.Count; i++)
                if (cat.pieces[i].pieceType == s.type)
                    s.realIndices.Add(i);
        }

        private void OnSectionAdd(SectionState s, PieceCatalogue cat)
        {
            serializedObject.Update();
            var piecesProp = serializedObject.FindProperty("pieces");
            piecesProp.InsertArrayElementAtIndex(piecesProp.arraySize);
            var newEl = piecesProp.GetArrayElementAtIndex(piecesProp.arraySize - 1);
            newEl.FindPropertyRelative("pieceType").enumValueIndex    = (int)s.type;
            newEl.FindPropertyRelative("isExit").boolValue            = false;
            newEl.FindPropertyRelative("prefab").objectReferenceValue = null;
            newEl.FindPropertyRelative("subFolder").stringValue       = "";
            serializedObject.ApplyModifiedProperties();
            RebuildAllSections(cat);
        }

        /// <summary>
        /// Called after ReorderableList has already shuffled s.realIndices.
        /// Applies the new view order back into cat.pieces via serializedObject.
        /// </summary>
        private void OnSectionReorder(SectionState s, PieceCatalogue cat)
        {
            var sortedSlots = new List<int>(s.realIndices);
            sortedSlots.Sort();

            // Capture piece data in new desired order before serializedObject.Update() runs.
            var reordered = s.realIndices
                .Select(ri => new {
                    prefab    = cat.pieces[ri].prefab,
                    pieceType = cat.pieces[ri].pieceType,
                    subFolder = cat.pieces[ri].subFolder,
                    isExit    = cat.pieces[ri].isExit,
                })
                .ToList();

            serializedObject.Update();
            var piecesProp = serializedObject.FindProperty("pieces");
            for (int i = 0; i < sortedSlots.Count; i++)
            {
                var el = piecesProp.GetArrayElementAtIndex(sortedSlots[i]);
                el.FindPropertyRelative("prefab").objectReferenceValue = reordered[i].prefab;
                el.FindPropertyRelative("pieceType").enumValueIndex    = (int)reordered[i].pieceType;
                el.FindPropertyRelative("subFolder").stringValue       = reordered[i].subFolder;
                el.FindPropertyRelative("isExit").boolValue            = reordered[i].isExit;
            }
            serializedObject.ApplyModifiedProperties();
            RebuildAllSections(cat);
        }

        // ── Inspector GUI ─────────────────────────────────────────────────────

        public override void OnInspectorGUI()
        {
            var cat = (PieceCatalogue)target;

            // ── Visual theme ──────────────────────────────────────────────────
            serializedObject.UpdateIfRequiredOrScript();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("theme"));
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.Space(4);

            // ── V2 Themes (prefab bundles) ─────────────────────────────────────
            serializedObject.UpdateIfRequiredOrScript();
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("themes"),
                new GUIContent("V2 Themes", "Named prefab bundles. Pick one by name from a RoomBuilder."),
                includeChildren: true);
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.Space(4);

            // ── Auto-populate ─────────────────────────────────────────────────
            EditorGUILayout.LabelField("Auto-Populate", EditorStyles.boldLabel);
            bool clickedFolder = false, clickedSecondPack = false;
            EditorGUILayout.BeginHorizontal();
            try
            {
                clickedFolder     = GUILayout.Button("Auto-Populate from Folder",      GUILayout.Height(26));
                clickedSecondPack = GUILayout.Button("Auto-Populate from Second Pack", GUILayout.Height(26));
            }
            finally { EditorGUILayout.EndHorizontal(); }

            // OpenFolderPanel calls GUIUtility.ExitGUI — must run outside any layout group.
            if (clickedFolder)     { DoAutoPopulate(cat, FdpDefaultFolder); RebuildAllSections(cat); }
            if (clickedSecondPack) { DoAutoPopulate(cat, "Assets");         RebuildAllSections(cat); }
            EditorGUILayout.Space(4);

            // ── Clear ─────────────────────────────────────────────────────────
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
                    RebuildAllSections(cat);
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.Space(4);

            // ── Log ───────────────────────────────────────────────────────────
            if (GUILayout.Button("Log All Pieces", GUILayout.Height(22)))
            {
                Debug.Log($"[PieceCatalogue] '{cat.name}' — {cat.pieces.Count} entries:");
                foreach (var entry in cat.pieces)
                    Debug.Log($"  {entry.pieceType}: " +
                              $"{(entry.prefab != null ? entry.prefab.name : "NULL")}  " +
                              $"from '{entry.subFolder}'");
            }
            EditorGUILayout.Space(6);

            // ── Breakdown ─────────────────────────────────────────────────────
            EditorGUILayout.LabelField("Catalogue Breakdown", EditorStyles.boldLabel);
            int liveTotal = 0;
            foreach (var pt in NormalTypes)
            {
                int n = cat.CountOfType(pt);
                liveTotal += n;
                EditorGUILayout.BeginHorizontal();
                try
                {
                    EditorGUILayout.LabelField($"  {pt}", GUILayout.Width(90));
                    EditorGUILayout.LabelField($"{n} piece(s)", EditorStyles.miniLabel);
                }
                finally { EditorGUILayout.EndHorizontal(); }
            }

            // Thin divider before Skipped + totals
            EditorGUILayout.Space(2);
            var divRect = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(divRect, Color.gray * 0.5f);
            EditorGUILayout.Space(2);

            int skippedCount = cat.CountOfType(PieceCatalogue.PieceType.None);
            EditorGUILayout.BeginHorizontal();
            try
            {
                EditorGUILayout.LabelField("  Skipped", GUILayout.Width(90));
                EditorGUILayout.LabelField($"{skippedCount} piece(s)", EditorStyles.miniLabel);
            }
            finally { EditorGUILayout.EndHorizontal(); }

            EditorGUILayout.BeginHorizontal();
            try
            {
                EditorGUILayout.LabelField("  Total (live)", GUILayout.Width(90));
                EditorGUILayout.LabelField($"{liveTotal}", EditorStyles.boldLabel);
            }
            finally { EditorGUILayout.EndHorizontal(); }

            EditorGUILayout.BeginHorizontal();
            try
            {
                EditorGUILayout.LabelField("  Total", GUILayout.Width(90));
                EditorGUILayout.LabelField($"{liveTotal + skippedCount}", EditorStyles.boldLabel);
            }
            finally { EditorGUILayout.EndHorizontal(); }
            EditorGUILayout.Space(6);

            // ── Filter ────────────────────────────────────────────────────────
            EditorGUILayout.LabelField("Filter", EditorStyles.boldLabel);

            // typeOptions: All / Floor..Stair / Skipped
            var typeOptions = new string[NormalTypes.Length + 2];
            typeOptions[0] = "All";
            for (int i = 0; i < NormalTypes.Length; i++)
                typeOptions[i + 1] = NormalTypes[i].ToString();
            typeOptions[NormalTypes.Length + 1] = "Skipped";

            int  newTypeIndex = EditorGUILayout.Popup("Piece Type", _filterTypeIndex, typeOptions);
            bool typeChanged  = newTypeIndex != _filterTypeIndex;
            if (typeChanged) { _filterTypeIndex = newTypeIndex; _filterNameIndex = 0; }

            if (_nameOptions == null || typeChanged || cat.pieces.Count != _lastPieceCount)
            {
                RebuildNameOptions(cat);
                if (cat.pieces.Count != _lastPieceCount)
                    RebuildAllSections(cat);
                _lastPieceCount = cat.pieces.Count;
            }

            _filterNameIndex = Mathf.Clamp(_filterNameIndex, 0, _nameOptions.Length - 1);
            _filterNameIndex = EditorGUILayout.Popup("Prefab", _filterNameIndex, _nameOptions);
            EditorGUILayout.Space(4);

            // ── Expand / Collapse All ─────────────────────────────────────────
            EditorGUILayout.BeginHorizontal();
            try
            {
                if (GUILayout.Button("Expand All",   GUILayout.Height(20))) SetAllFoldouts(true);
                if (GUILayout.Button("Collapse All", GUILayout.Height(20))) SetAllFoldouts(false);
            }
            finally { EditorGUILayout.EndHorizontal(); }
            EditorGUILayout.Space(6);

            // ── Resolve active filters ────────────────────────────────────────
            PieceCatalogue.PieceType? typeFilter = null;
            if (_filterTypeIndex > 0 && _filterTypeIndex <= NormalTypes.Length)
                typeFilter = NormalTypes[_filterTypeIndex - 1];
            else if (_filterTypeIndex == NormalTypes.Length + 1)
                typeFilter = PieceCatalogue.PieceType.None;

            string nameFilter      = _filterNameIndex > 0 ? _nameOptions[_filterNameIndex] : null;
            bool   nameFilterActive = nameFilter != null;

            // ── Normal type sections ──────────────────────────────────────────
            serializedObject.UpdateIfRequiredOrScript();
            var piecesProp = serializedObject.FindProperty("pieces");

            foreach (var pt in NormalTypes)
            {
                if (typeFilter.HasValue && typeFilter.Value != pt) continue;

                var s = _sections[pt];

                if (typeFilter.HasValue && !s.foldoutOpen)
                {
                    s.foldoutOpen = true;
                    EditorPrefs.SetBool(s.foldoutPrefKey, true);
                }

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                bool earlyReturn = false;

                bool newOpen = EditorGUILayout.Foldout(
                    s.foldoutOpen, $"{pt} ({s.realIndices.Count})", true, EditorStyles.foldoutHeader);
                if (newOpen != s.foldoutOpen)
                {
                    s.foldoutOpen = newOpen;
                    EditorPrefs.SetBool(s.foldoutPrefKey, newOpen);
                }

                if (s.foldoutOpen)
                {
                    if (nameFilterActive)
                    {
                        var visibleVIs = new List<int>();
                        for (int vi = 0; vi < s.realIndices.Count; vi++)
                        {
                            var e = cat.pieces[s.realIndices[vi]];
                            if (e.prefab != null && e.prefab.name == nameFilter)
                                visibleVIs.Add(vi);
                        }

                        EditorGUILayout.HelpBox(
                            "Prefab name filter active — clear it to add new entries to this section.",
                            MessageType.Info);

                        if (visibleVIs.Count == 0)
                        {
                            EditorGUILayout.LabelField("No matching entries.",
                                EditorStyles.centeredGreyMiniLabel);
                        }
                        else
                        {
                            foreach (int vi in visibleVIs)
                            {
                                int  realIdx = s.realIndices[vi];
                                var  el      = piecesProp.GetArrayElementAtIndex(realIdx);
                                bool doDelete;

                                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                                EditorGUILayout.BeginHorizontal();
                                try
                                {
                                    EditorGUILayout.LabelField($"Element {realIdx}",
                                        EditorStyles.boldLabel);
                                    GUI.backgroundColor = new Color(1f, 0.35f, 0.35f);
                                    doDelete = GUILayout.Button("✕",
                                        GUILayout.Width(20), GUILayout.Height(18));
                                    GUI.backgroundColor = Color.white;
                                }
                                finally { EditorGUILayout.EndHorizontal(); }

                                DrawPieceEntryLayout(el);
                                EditorGUILayout.EndVertical();
                                EditorGUILayout.Space(2);

                                if (doDelete)
                                {
                                    serializedObject.ApplyModifiedProperties();
                                    piecesProp.DeleteArrayElementAtIndex(realIdx);
                                    serializedObject.ApplyModifiedProperties();
                                    _filterNameIndex = 0;
                                    _nameOptions     = null;
                                    RebuildAllSections(cat);
                                    earlyReturn = true;
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        s.list.DoLayoutList();

                        if (s.pendingDeleteRealIndex >= 0)
                        {
                            int toDelete              = s.pendingDeleteRealIndex;
                            s.pendingDeleteRealIndex  = -1;
                            serializedObject.ApplyModifiedProperties();
                            piecesProp.DeleteArrayElementAtIndex(toDelete);
                            serializedObject.ApplyModifiedProperties();
                            _filterNameIndex = 0;
                            _nameOptions     = null;
                            RebuildAllSections(cat);
                            earlyReturn = true;
                        }
                    }
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);

                if (earlyReturn) return;
            }

            // ── Skipped (None) section ────────────────────────────────────────
            if (!typeFilter.HasValue || typeFilter.Value == PieceCatalogue.PieceType.None)
            {
                var s = _sections[PieceCatalogue.PieceType.None];

                if (typeFilter.HasValue && !s.foldoutOpen)
                {
                    s.foldoutOpen = true;
                    EditorPrefs.SetBool(s.foldoutPrefKey, true);
                }

                // Distinct background tint for the staging section
                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.95f, 0.72f);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUI.backgroundColor = prevBg;

                bool earlyReturn = false;

                bool newOpen = EditorGUILayout.Foldout(
                    s.foldoutOpen,
                    $"Skipped ({s.realIndices.Count})",
                    true, EditorStyles.foldoutHeader);
                if (newOpen != s.foldoutOpen)
                {
                    s.foldoutOpen = newOpen;
                    EditorPrefs.SetBool(s.foldoutPrefKey, newOpen);
                }

                if (s.foldoutOpen)
                {
                    EditorGUILayout.LabelField(
                        "staging — not used by generator",
                        EditorStyles.centeredGreyMiniLabel);

                    // One-frame feedback after a Move
                    if (_moveMessage != null)
                    {
                        EditorGUILayout.HelpBox(_moveMessage, MessageType.Info);
                        _moveMessage = null;
                    }

                    if (nameFilterActive)
                    {
                        var visibleVIs = new List<int>();
                        for (int vi = 0; vi < s.realIndices.Count; vi++)
                        {
                            var e = cat.pieces[s.realIndices[vi]];
                            if (e.prefab != null && e.prefab.name == nameFilter)
                                visibleVIs.Add(vi);
                        }

                        EditorGUILayout.HelpBox(
                            "Prefab name filter active — clear it to add new entries to this section.",
                            MessageType.Info);

                        if (visibleVIs.Count == 0)
                        {
                            EditorGUILayout.LabelField("No matching entries.",
                                EditorStyles.centeredGreyMiniLabel);
                        }
                        else
                        {
                            foreach (int vi in visibleVIs)
                            {
                                int  realIdx = s.realIndices[vi];
                                var  el      = piecesProp.GetArrayElementAtIndex(realIdx);
                                bool doDelete;

                                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                                EditorGUILayout.BeginHorizontal();
                                try
                                {
                                    EditorGUILayout.LabelField($"Element {realIdx}",
                                        EditorStyles.boldLabel);
                                    GUI.backgroundColor = new Color(1f, 0.35f, 0.35f);
                                    doDelete = GUILayout.Button("✕",
                                        GUILayout.Width(20), GUILayout.Height(18));
                                    GUI.backgroundColor = Color.white;
                                }
                                finally { EditorGUILayout.EndHorizontal(); }

                                DrawSkippedEntryLayout(el, realIdx, cat);
                                EditorGUILayout.EndVertical();
                                EditorGUILayout.Space(2);

                                if (doDelete)
                                {
                                    serializedObject.ApplyModifiedProperties();
                                    piecesProp.DeleteArrayElementAtIndex(realIdx);
                                    serializedObject.ApplyModifiedProperties();
                                    _filterNameIndex = 0;
                                    _nameOptions     = null;
                                    RebuildAllSections(cat);
                                    earlyReturn = true;
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        s.list.DoLayoutList();

                        // Deferred delete
                        if (s.pendingDeleteRealIndex >= 0)
                        {
                            int toDelete             = s.pendingDeleteRealIndex;
                            s.pendingDeleteRealIndex = -1;
                            serializedObject.ApplyModifiedProperties();
                            piecesProp.DeleteArrayElementAtIndex(toDelete);
                            serializedObject.ApplyModifiedProperties();
                            _filterNameIndex = 0;
                            _nameOptions     = null;
                            RebuildAllSections(cat);
                            earlyReturn = true;
                        }

                        // Deferred Move (apply pending type to the real entry)
                        if (!earlyReturn && s.pendingMoveRealIndex >= 0)
                        {
                            int ri               = s.pendingMoveRealIndex;
                            s.pendingMoveRealIndex = -1;
                            if (_pendingMoveTypes.TryGetValue(ri, out var destType))
                            {
                                string prefabName = cat.pieces[ri].prefab != null
                                    ? cat.pieces[ri].prefab.name : "?";
                                serializedObject.ApplyModifiedProperties();
                                serializedObject.Update();
                                var el = piecesProp.GetArrayElementAtIndex(ri);
                                el.FindPropertyRelative("pieceType").enumValueIndex = (int)destType;
                                serializedObject.ApplyModifiedProperties();
                                _moveMessage = $"Moved '{prefabName}' to {destType} section.";
                                RebuildAllSections(cat); // also clears _pendingMoveTypes
                                earlyReturn = true;
                            }
                        }
                    }
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);

                if (earlyReturn) return;
            }

            serializedObject.ApplyModifiedProperties();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void SetAllFoldouts(bool open)
        {
            foreach (var s in _sections.Values)
            {
                s.foldoutOpen = open;
                EditorPrefs.SetBool(s.foldoutPrefKey, open);
            }
        }

        private void RebuildNameOptions(PieceCatalogue cat)
        {
            // Resolve what type the current filter index maps to
            PieceCatalogue.PieceType? typeFilter = null;
            if (_filterTypeIndex > 0 && _filterTypeIndex <= NormalTypes.Length)
                typeFilter = NormalTypes[_filterTypeIndex - 1];
            else if (_filterTypeIndex == NormalTypes.Length + 1)
                typeFilter = PieceCatalogue.PieceType.None;

            var names = new SortedSet<string>();
            foreach (var e in cat.pieces)
            {
                if (e.prefab == null) continue;
                if (typeFilter.HasValue && e.pieceType != typeFilter.Value) continue;
                names.Add(e.prefab.name);
            }

            _nameOptions    = new string[names.Count + 1];
            _nameOptions[0] = "All";
            int i = 1;
            foreach (var n in names)
                _nameOptions[i++] = n;
        }

        /// <summary>
        /// Draws editable fields for a normal PieceEntry row (EditorGUILayout variant used in
        /// the name-filter path). isExit shown only for Doorway.
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

        /// <summary>
        /// Draws the editable fields for a Skipped row in the name-filter path.
        /// Includes the Destination popup and Move button.
        /// </summary>
        private void DrawSkippedEntryLayout(SerializedProperty el, int realIdx, PieceCatalogue cat)
        {
            string[] destLabels = new string[NormalTypes.Length + 1];
            destLabels[0] = "— keep in Skipped";
            for (int i = 0; i < NormalTypes.Length; i++)
                destLabels[i + 1] = NormalTypes[i].ToString();

            EditorGUILayout.PropertyField(el.FindPropertyRelative("prefab"));

            bool hasPending = _pendingMoveTypes.TryGetValue(realIdx, out var pendingType);
            int  pendingIdx = 0;
            if (hasPending)
            {
                int found = System.Array.IndexOf(NormalTypes, pendingType);
                if (found >= 0) pendingIdx = found + 1;
            }

            int newPendingIdx = EditorGUILayout.Popup("Destination", pendingIdx, destLabels);
            if (newPendingIdx != pendingIdx)
            {
                if (newPendingIdx == 0) _pendingMoveTypes.Remove(realIdx);
                else _pendingMoveTypes[realIdx] = NormalTypes[newPendingIdx - 1];
                hasPending = _pendingMoveTypes.TryGetValue(realIdx, out pendingType);
            }

            EditorGUILayout.PropertyField(el.FindPropertyRelative("subFolder"));

            if (hasPending && pendingType == PieceCatalogue.PieceType.Doorway)
                EditorGUILayout.PropertyField(el.FindPropertyRelative("isExit"));

            EditorGUILayout.BeginHorizontal();
            try
            {
                EditorGUILayout.LabelField(
                    hasPending ? $"Will move to: {pendingType}" : "",
                    EditorStyles.miniLabel);
                GUI.enabled = hasPending;
                if (GUILayout.Button("Move", GUILayout.Width(56)))
                {
                    string prefabName = cat.pieces[realIdx].prefab != null
                        ? cat.pieces[realIdx].prefab.name : "?";
                    el.FindPropertyRelative("pieceType").enumValueIndex = (int)pendingType;
                    serializedObject.ApplyModifiedProperties();
                    _moveMessage = $"Moved '{prefabName}' to {pendingType} section.";
                    _filterNameIndex = 0;
                    _nameOptions     = null;
                    RebuildAllSections(cat);
                }
                GUI.enabled = true;
            }
            finally { EditorGUILayout.EndHorizontal(); }
        }

        // ── Auto-populate ─────────────────────────────────────────────────────

        /// <summary>
        /// Opens a folder picker, recursively scans for prefabs, maps them to PieceType
        /// via subfolder rules, and appends new entries.
        ///
        /// Unmapped prefabs (Trim, Railing, OneSided, etc.) are added as PieceType.None
        /// so the user can review and promote them in the Skipped section.
        /// On re-populate, existing pieceType values are preserved (matched by prefab reference).
        /// </summary>
        private static void DoAutoPopulate(PieceCatalogue cat, string startFolder)
        {
            string absStart = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", startFolder)).Replace('\\', '/');

            string chosen = EditorUtility.OpenFolderPanel(
                "Select Asset Pack MODULAR Folder", absStart, "");
            if (string.IsNullOrEmpty(chosen)) return;

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
            if (!folderRelative.StartsWith("Assets"))
            {
                EditorUtility.DisplayDialog("Error",
                    "Could not resolve a project-relative path from the selection.", "OK");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folderRelative });

            // Build lookup from existing entries: path → pieceType (preserve user promotions).
            var existingPaths  = new HashSet<string>();
            var existingTypes  = new Dictionary<string, PieceCatalogue.PieceType>();
            var existingIsExit = new Dictionary<string, bool>();
            foreach (var entry in cat.pieces)
            {
                if (entry.prefab == null) continue;
                string ap = AssetDatabase.GetAssetPath(entry.prefab);
                existingPaths.Add(ap);
                existingTypes[ap]  = entry.pieceType;
                existingIsExit[ap] = entry.isExit;
            }

            int addedReal    = 0;
            int addedSkipped = 0;
            int trulySkipped = 0;

            Undo.RecordObject(cat, "Auto-Populate PieceCatalogue");

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);

                if (existingPaths.Contains(assetPath))
                {
                    trulySkipped++;
                    continue;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab == null)
                {
                    trulySkipped++;
                    continue;
                }

                string subPath = assetPath;
                if (assetPath.Replace('\\', '/').StartsWith(folderRelative))
                    subPath = assetPath.Substring(folderRelative.Length).TrimStart('/', '\\');

                bool mapped = TryMapSubPath(subPath, out PieceCatalogue.PieceType pieceType);
                if (!mapped) pieceType = PieceCatalogue.PieceType.None;

                // Honour any type the user previously set for this prefab.
                if (existingTypes.TryGetValue(assetPath, out var savedType))
                    pieceType = savedType;

                cat.pieces.Add(new PieceCatalogue.PieceEntry
                {
                    prefab    = prefab,
                    pieceType = pieceType,
                    subFolder = Path.GetDirectoryName(subPath)?.Replace('\\', '/') ?? "",
                    isExit    = existingIsExit.TryGetValue(assetPath, out bool saved) && saved,
                });
                existingPaths.Add(assetPath);

                if (pieceType == PieceCatalogue.PieceType.None) addedSkipped++;
                else                                             addedReal++;
            }

            EditorUtility.SetDirty(cat);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog(
                "Auto-Populate Complete",
                $"Added   : {addedReal}\n" +
                $"Staged  : {addedSkipped}\n" +
                $"Skipped : {trulySkipped}\n\n" +
                $"Total in catalogue: {cat.pieces.Count}",
                "OK");
        }

        // ── Subfolder → PieceType mapping ─────────────────────────────────────

        /// <summary>
        /// Maps the sub-path of a prefab (relative to the scanned root) to a real PieceType.
        /// Returns false when the subfolder rule says "stage as None" — caller adds as None.
        /// </summary>
        private static bool TryMapSubPath(string subPath, out PieceCatalogue.PieceType pieceType)
        {
            subPath = subPath.Replace('\\', '/');

            // WallCover → Ceiling (checked before Trim to capture Trim/WallCover)
            if (subPath.Contains("WallCover"))
            {
                pieceType = PieceCatalogue.PieceType.Ceiling;
                return true;
            }

            // These subfolder patterns → stage as None (caller adds with PieceType.None)
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

            if (subPath.Contains("Floor"))   { pieceType = PieceCatalogue.PieceType.Floor;   return true; }
            if (subPath.Contains("Gateway")) { pieceType = PieceCatalogue.PieceType.Doorway; return true; }
            if (subPath.Contains("Column"))  { pieceType = PieceCatalogue.PieceType.Column;  return true; }

            if (subPath.Contains("Wall") && subPath.Contains("Middle"))
            {
                string lower = subPath.ToLower();
                pieceType = (lower.Contains("corner") || lower.Contains("angle") || lower.Contains("concave"))
                    ? PieceCatalogue.PieceType.Corner
                    : PieceCatalogue.PieceType.Wall;
                return true;
            }

            if (subPath.Contains("Stair")) { pieceType = PieceCatalogue.PieceType.Stair; return true; }

            // No rule matched → stage as None
            pieceType = default;
            return false;
        }
    }
}
