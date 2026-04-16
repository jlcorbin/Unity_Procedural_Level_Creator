#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LevelGen
{
    /// <summary>
    /// Room Workshop — editor window for building custom dungeon rooms from
    /// modular asset-pack pieces and saving them as generator-ready prefabs.
    ///
    /// Mirrors how the Fantastic Dungeon Pack boss room was constructed:
    /// floors from P_MOD_Floor_ tiles (4-unit grid, corner pivot),
    /// walls from COMP_Wall_ pieces (6-unit step, edge pivot),
    /// exits derived from COMP_Door_ pieces placed in a doors/ child group.
    ///
    /// Workflow:
    ///   ① Define     — room size in units (multiples of 12 = LCM of floor-snap 4 and wall-snap 6)
    ///   ② Catalogue  — assign PieceCatalogue (floor / wall / doorway pieces)
    ///   ③ Build      — auto-fill floor grid + wall perimeter
    ///   ④ Doors      — add or detect COMP_Door_ pieces that define exits
    ///   ⑤ Components — stamp RoomPiece + ExitPoints from door positions
    ///   ⑥ Save       — write prefab to Curated/ + RoomPreset asset
    ///
    /// Open via: LevelGen ▶ Room Workshop
    /// </summary>
    public class RoomWorkshopWindow : EditorWindow
    {
        // ── Constants ─────────────────────────────────────────────────────────

        /// <summary>Floor tile snap unit — P_MOD_Floor _med corner-pivot pieces.</summary>
        private const float FloorStep = 4f;

        /// <summary>Wall comp snap unit — COMP_Wall large edge-pivot pieces.</summary>
        private const float WallStep = 6f;

        /// <summary>Wall tier height — one stacked wall layer.</summary>
        private const float WallTier = 6f;

        private const string ModRootName = "MOD";
        private const string GroupFloors = "floors";
        private const string GroupWalls = "walls";
        private const string GroupCeiling = "ceiling";
        private const string GroupDoors = "doors";
        private const string CuratedFolder = "Assets/Prefabs/Rooms/Curated";
        private const string PresetsFolder = "Assets/RoomPresets";

        private static readonly System.Random _rng = new();

        // ── Enums ─────────────────────────────────────────────────────────────

        private enum SizePreset { Small, Medium, Large, Custom }
        private enum WallSide   { North, South, East, West }
        private enum WallSize   { Large, Med, Small, None }

        // ── Serialised state (survives domain reload) ─────────────────────────

        [SerializeField] private SizePreset _sizePreset = SizePreset.Small;
        [SerializeField] private int _customWidth  = 12;
        [SerializeField] private int _customDepth  = 12;
        [SerializeField] private int _customHeight =  6;
        // Wall mode toggle
        [SerializeField] private bool _directionalWalls = false;
        // Shared mode: per-side enabled toggle (false = skip / None)
        [SerializeField] private bool _wallNEnabled = true;
        [SerializeField] private bool _wallSEnabled = true;
        [SerializeField] private bool _wallEEnabled = true;
        [SerializeField] private bool _wallWEnabled = true;
        // Directional mode: per-side prefab index (0 = None, 1+ = GetAllStraightWallPrefabs index + 1)
        [SerializeField] private int _wallNDirIdx = 0;
        [SerializeField] private int _wallSDirIdx = 0;
        [SerializeField] private int _wallEDirIdx = 0;
        [SerializeField] private int _wallWDirIdx = 0;
        // Corner mode toggle
        [SerializeField] private bool _directionalCorners = false;
        // Shared mode: 0=None, 1=Auto, 2+=specific corner prefab
        [SerializeField] private int _cornerPrefabIndex = 1; // default Auto
        // Directional mode: per-corner prefab index (0=None, 1+=specific)
        [SerializeField] private int _cornerNWDirIdx = 0;
        [SerializeField] private int _cornerNEDirIdx = 0;
        [SerializeField] private int _cornerSWDirIdx = 0;
        [SerializeField] private int _cornerSEDirIdx = 0;
        [SerializeField] private PieceCatalogue _pieceCat;
        [SerializeField] private GameObject _modRoot;
        [SerializeField] private string _saveName = "Room_New";
        [SerializeField] private WallSide _addDoorWall = WallSide.North;
        [SerializeField] private bool _doubleDoor = false;
        [SerializeField] private int  _openingTier = 0;
        // Pinned wall prefab index (0 = auto / first match per size)
        [SerializeField] private int _wallPrefabIndex = 0;
        // Seed generator
        [SerializeField] private int _seed = 0;

        // ── Non-serialised transient state ────────────────────────────────────

        private string[] _wallPrefabOptions;
        private int      _lastWallCatCount = -1;
        private string[] _wallDirOptions;       // "None" + all straight walls (directional mode)
        private int      _lastWallDirCount = -1;
        private string[] _cornerPrefabOptions; // None, Auto, specific corner prefabs
        private int      _lastCornerCatCount = -1;
        private string[] _cornerDirOptions;    // "None" + all corner prefabs (directional mode)
        private int      _lastCornerDirCount = -1;

        private Vector2 _scroll;
        private bool _showSeed = true;
        private bool _show1 = true, _show2 = true, _show3 = true;
        private bool _show4 = true, _show5 = true, _show6 = true;
        private bool _showDebug = false;

        // ── Computed properties ───────────────────────────────────────────────

        private float FullWidth => _sizePreset switch
        {
            SizePreset.Small => 12f,
            SizePreset.Medium => 24f,
            SizePreset.Large => 36f,
            _ => SnapTo12(_customWidth),
        };

        private float FullDepth => _sizePreset switch
        {
            SizePreset.Small => 12f,
            SizePreset.Medium => 24f,
            SizePreset.Large => 36f,
            _ => SnapTo12(_customDepth),
        };

        private float FullHeight => _sizePreset == SizePreset.Custom
            ? SnapToStep(_customHeight, (int)WallTier)
            : WallTier;

        private float HalfWidth  => FullWidth  * 0.5f;
        private float HalfDepth  => FullDepth  * 0.5f;
        private float HalfHeight => FullHeight * 0.5f;

        /// <summary>Snaps v to the nearest multiple of 12 (LCM of floor-snap 4 and wall-snap 6).</summary>
        private static float SnapTo12(int v) =>
            Mathf.Max(12f, Mathf.Round(Mathf.Max(1, v) / 12f) * 12f);

        /// <summary>Snaps v to the nearest multiple of <paramref name="step"/>.</summary>
        private static int SnapToStep(int v, int step) =>
            Mathf.Max(step, Mathf.RoundToInt(Mathf.Max(1, v) / (float)step) * step);

        // ── Menu ──────────────────────────────────────────────────────────────

        /// <summary>Opens the Room Workshop window.</summary>
        [MenuItem("LevelGen/Room Workshop")]
        public static void Open()
        {
            var win = GetWindow<RoomWorkshopWindow>("Room Workshop");
            win.minSize = new Vector2(320, 600);
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void OnEnable() => SceneView.duringSceneGui += OnSceneGUI;
        private void OnDisable() => SceneView.duringSceneGui -= OnSceneGUI;

        // ── GUI ───────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawSeedGenerator();
            DrawDivider();
            DrawStep1_Define();
            DrawDivider();
            DrawStep2_Catalogue();
            DrawDivider();
            DrawStep3_Build();
            DrawDivider();
            DrawStep4_Doors();
            DrawDivider();
            DrawStep5_Components();
            DrawDivider();
            DrawStep6_Save();
            DrawDivider();
            DrawDebugFoldout();

            EditorGUILayout.EndScrollView();
        }

        // ── Seed Generator ────────────────────────────────────────────────────

        private void DrawSeedGenerator()
        {
            if (!DrawFoldout(ref _showSeed, "⓪ Seed Generator")) return;
            EditorGUI.indentLevel++;

            EditorGUILayout.HelpBox(
                "Generates a complete room from a seed number.\n" +
                "Same seed + same catalogue = identical result every time.",
                MessageType.None);

            EditorGUILayout.Space(2);

            EditorGUILayout.BeginHorizontal();
            _seed = EditorGUILayout.IntField("Seed", _seed);
            if (GUILayout.Button("Random", GUILayout.Width(64)))
                _seed = new System.Random().Next();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            GUI.enabled = _pieceCat != null;
            GUI.backgroundColor = new Color(0.85f, 0.55f, 1f);
            if (GUILayout.Button("Generate Room", GUILayout.Height(30)))
                DoGenerateFromSeed();
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;

            if (_pieceCat == null)
                EditorGUILayout.HelpBox("Assign a Piece Catalogue first (step ②).", MessageType.None);

            EditorGUI.indentLevel--;
        }

        private void DoGenerateFromSeed()
        {
            if (_pieceCat == null) return;

            DoClearRoom();

            var rng = new System.Random(_seed);

            // ── Size ──────────────────────────────────────────────────────────
            var presets = new[] { SizePreset.Small, SizePreset.Medium, SizePreset.Large };
            _sizePreset = presets[rng.Next(presets.Length)];

            // ── Walls (shared mode) ───────────────────────────────────────────
            _directionalWalls = false;
            var walls = GetAllStraightWallPrefabs();
            // index layout: 0=None, 1=Auto, 2+=specific — always pick a specific prefab
            _wallPrefabIndex = walls.Count > 0 ? rng.Next(walls.Count) + 2 : 1;
            _lastWallCatCount = -1; // force dropdown rebuild on next repaint

            // ── Corners (shared mode) ─────────────────────────────────────────
            _directionalCorners = false;
            var corners = GetAllCornerPrefabs();
            _cornerPrefabIndex = corners.Count > 0 ? rng.Next(corners.Count) + 2 : 1;
            _lastCornerCatCount = -1;

            // ── Build room ────────────────────────────────────────────────────
            DoBuildRoom();

            // ── Openings (1–4, on randomly chosen walls, always at least 1) ──
            var sides = new List<WallSide>
                { WallSide.North, WallSide.South, WallSide.East, WallSide.West };
            // Fisher-Yates shuffle
            for (int i = sides.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (sides[i], sides[j]) = (sides[j], sides[i]);
            }
            int numOpenings = 1 + rng.Next(4); // 1–4
            _doubleDoor  = false;
            _openingTier = 0;
            for (int i = 0; i < Mathf.Min(numOpenings, sides.Count); i++)
                DoAddDoor(sides[i]);

            string wallName   = walls.Count   > 0 && _wallPrefabIndex   >= 2 ? walls[_wallPrefabIndex   - 2].prefab?.name : "auto";
            string cornerName = corners.Count > 0 && _cornerPrefabIndex >= 2 ? corners[_cornerPrefabIndex - 2].prefab?.name : "auto";
            Debug.Log($"[RoomWorkshop] Seed {_seed} → {_sizePreset}  wall:{wallName}  corner:{cornerName}  openings:{numOpenings}");

            Repaint();
        }

        // ── Step 1: Define ────────────────────────────────────────────────────

        private void DrawStep1_Define()
        {
            if (!DrawFoldout(ref _show1, "① Define Room")) return;
            EditorGUI.indentLevel++;

            _sizePreset = (SizePreset)EditorGUILayout.EnumPopup(
                new GUIContent("Size Preset",
                    "Small=12×12  Medium=24×24  Large=36×36  (multiples of 12 — fits both floor-snap 4 and wall-snap 6)"),
                _sizePreset);

            if (_sizePreset == SizePreset.Custom)
            {
                _customWidth = DrawSliderWithIntField(
                    new GUIContent("Width (units)", "Must be a multiple of 12"),
                    _customWidth, 12, 120, snap: 12);
                _customDepth = DrawSliderWithIntField(
                    new GUIContent("Depth (units)", "Must be a multiple of 12"),
                    _customDepth, 12, 120, snap: 12);
                _customHeight = DrawSliderWithIntField(
                    new GUIContent("Height (units)", "Wall tiers: 6 = 1 tier, 12 = 2 tiers, 18 = 3 tiers"),
                    _customHeight, 6, 18, snap: 6);
            }

            int tX = Mathf.RoundToInt(FullWidth  / FloorStep);
            int tZ = Mathf.RoundToInt(FullDepth  / FloorStep);
            int wX = Mathf.RoundToInt(FullWidth  / WallStep);
            int wZ = Mathf.RoundToInt(FullDepth  / WallStep);
            int tiers = Mathf.RoundToInt(FullHeight / WallTier);
            EditorGUILayout.LabelField(
                $"World: {FullWidth} × {FullDepth}  h:{FullHeight} units  |  " +
                $"Floor: {tX}×{tZ}  |  Walls: {wX}+{wZ}/side  |  Tiers: {tiers}",
                EditorStyles.miniLabel);

            EditorGUI.indentLevel--;
        }

        // ── Step 2: Catalogue ─────────────────────────────────────────────────

        private void DrawStep2_Catalogue()
        {
            if (!DrawFoldout(ref _show2, "② Piece Catalogue")) return;
            EditorGUI.indentLevel++;

            _pieceCat = (PieceCatalogue)EditorGUILayout.ObjectField(
                "Piece Catalogue", _pieceCat, typeof(PieceCatalogue), false);

            if (_pieceCat != null)
            {
                int floors = _pieceCat.CountOfType(PieceCatalogue.PieceType.Floor);
                int walls = _pieceCat.CountOfType(PieceCatalogue.PieceType.Wall);
                int doorways = _pieceCat.CountOfType(PieceCatalogue.PieceType.Doorway);

                EditorGUILayout.LabelField(
                    $"  Floor: {floors}   Wall: {walls}   Doorway: {doorways}",
                    EditorStyles.miniLabel);

                if (floors == 0)
                    EditorGUILayout.HelpBox(
                        "No Floor pieces. Auto-populate the catalogue from the folder that " +
                        "contains P_MOD_Floor_ prefabs.",
                        MessageType.Warning);

                if (walls == 0)
                    EditorGUILayout.HelpBox(
                        "No Wall pieces. Auto-populate from the folder that contains " +
                        "COMP_Wall_ prefabs.",
                        MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Assign a PieceCatalogue to enable building.", MessageType.None);
            }

            EditorGUI.indentLevel--;
        }

        // ── Step 3: Build ─────────────────────────────────────────────────────

        private void DrawStep3_Build()
        {
            if (!DrawFoldout(ref _show3, "③ Build Room")) return;
            EditorGUI.indentLevel++;

            bool hasCat = _pieceCat != null;

            // ── Wall Prefab picker (shared mode only) ────────────────────────
            if (!_directionalWalls)
            {
                if (hasCat)
                {
                    var allWalls = GetAllStraightWallPrefabs();
                    if (_lastWallCatCount != allWalls.Count)
                    {
                        _lastWallCatCount = allWalls.Count;
                        // index 0 = None (no walls), 1 = Auto, 2+ = specific prefabs
                        _wallPrefabOptions = new string[allWalls.Count + 2];
                        _wallPrefabOptions[0] = "None";
                        _wallPrefabOptions[1] = "Auto (first match)";
                        for (int i = 0; i < allWalls.Count; i++)
                            _wallPrefabOptions[i + 2] = allWalls[i].prefab != null ? allWalls[i].prefab.name : "(null)";
                        _wallPrefabIndex = Mathf.Clamp(_wallPrefabIndex, 0, allWalls.Count + 1);
                    }
                    _wallPrefabIndex = EditorGUILayout.Popup(
                        new GUIContent("Wall Prefab", "None = corners only. Auto = first catalogue match. Or pick a specific prefab."),
                        _wallPrefabIndex,
                        _wallPrefabOptions);
                }
                else
                {
                    EditorGUILayout.LabelField("Wall Prefab", "(assign catalogue first)");
                }
            }

            _directionalWalls = EditorGUILayout.Toggle(
                new GUIContent("Directional Wall Setup?",
                    "When on, choose a specific wall prefab per side. When off, all enabled sides share the Wall Prefab above."),
                _directionalWalls);

            EditorGUILayout.Space(4);

            // ── Per-side Walls (directional mode only) ───────────────────────
            const float wLbl = 50f;
            string[] wLabels = { "North", "South", "East", "West" };

            if (_directionalWalls)
            {
                EditorGUILayout.LabelField("Walls", EditorStyles.boldLabel);
                // Directional mode: per-side prefab dropdown (None + all straight walls)
                if (hasCat)
                {
                    var allWalls = GetAllStraightWallPrefabs();
                    if (_lastWallDirCount != allWalls.Count)
                    {
                        _lastWallDirCount = allWalls.Count;
                        _wallDirOptions = new string[allWalls.Count + 1];
                        _wallDirOptions[0] = "None";
                        for (int i = 0; i < allWalls.Count; i++)
                            _wallDirOptions[i + 1] = allWalls[i].prefab != null ? allWalls[i].prefab.name : "(null)";
                        _wallNDirIdx = Mathf.Clamp(_wallNDirIdx, 0, allWalls.Count);
                        _wallSDirIdx = Mathf.Clamp(_wallSDirIdx, 0, allWalls.Count);
                        _wallEDirIdx = Mathf.Clamp(_wallEDirIdx, 0, allWalls.Count);
                        _wallWDirIdx = Mathf.Clamp(_wallWDirIdx, 0, allWalls.Count);
                    }
                    int[] wDirIdx = { _wallNDirIdx, _wallSDirIdx, _wallEDirIdx, _wallWDirIdx };
                    for (int i = 0; i < 4; i++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(wLabels[i], GUILayout.Width(wLbl));
                        wDirIdx[i] = EditorGUILayout.Popup(wDirIdx[i], _wallDirOptions);
                        EditorGUILayout.EndHorizontal();
                    }
                    _wallNDirIdx = wDirIdx[0]; _wallSDirIdx = wDirIdx[1];
                    _wallEDirIdx = wDirIdx[2]; _wallWDirIdx = wDirIdx[3];
                }
                else
                {
                    EditorGUILayout.LabelField("(assign catalogue first)");
                }
            }

            EditorGUILayout.Space(4);

            // ── Corners ───────────────────────────────────────────────────────
            const float cLbl = 30f, cDrp = 70f, chk = 60f;
            // ── Corner Prefab picker (shared mode only) ──────────────────────
            if (!_directionalCorners)
            {
                if (hasCat)
                {
                    var allCorners = GetAllCornerPrefabs();
                    if (_lastCornerCatCount != allCorners.Count)
                    {
                        _lastCornerCatCount = allCorners.Count;
                        _cornerPrefabOptions = new string[allCorners.Count + 2];
                        _cornerPrefabOptions[0] = "None";
                        _cornerPrefabOptions[1] = "Auto (first match)";
                        for (int i = 0; i < allCorners.Count; i++)
                            _cornerPrefabOptions[i + 2] = allCorners[i].prefab != null ? allCorners[i].prefab.name : "(null)";
                        _cornerPrefabIndex = Mathf.Clamp(_cornerPrefabIndex, 0, allCorners.Count + 1);
                    }
                    _cornerPrefabIndex = EditorGUILayout.Popup(
                        new GUIContent("Corner Prefab", "None = no corners. Auto = first catalogue match. Or pick a specific prefab."),
                        _cornerPrefabIndex,
                        _cornerPrefabOptions);
                }
                else
                {
                    EditorGUILayout.LabelField("Corner Prefab", "(assign catalogue first)");
                }
            }

            _directionalCorners = EditorGUILayout.Toggle(
                new GUIContent("Directional Corner Setup?",
                    "When on, choose a specific corner prefab per corner. When off, all corners share the Corner Prefab above."),
                _directionalCorners);

            // ── Per-corner (directional mode only) ───────────────────────────
            if (_directionalCorners)
            {
                EditorGUILayout.LabelField("Corners", EditorStyles.boldLabel);
                if (hasCat)
                {
                    var allCorners = GetAllCornerPrefabs();
                    if (_lastCornerDirCount != allCorners.Count)
                    {
                        _lastCornerDirCount = allCorners.Count;
                        _cornerDirOptions = new string[allCorners.Count + 1];
                        _cornerDirOptions[0] = "None";
                        for (int i = 0; i < allCorners.Count; i++)
                            _cornerDirOptions[i + 1] = allCorners[i].prefab != null ? allCorners[i].prefab.name : "(null)";
                        _cornerNWDirIdx = Mathf.Clamp(_cornerNWDirIdx, 0, allCorners.Count);
                        _cornerNEDirIdx = Mathf.Clamp(_cornerNEDirIdx, 0, allCorners.Count);
                        _cornerSWDirIdx = Mathf.Clamp(_cornerSWDirIdx, 0, allCorners.Count);
                        _cornerSEDirIdx = Mathf.Clamp(_cornerSEDirIdx, 0, allCorners.Count);
                    }
                    string[] cLabels = { "NW", "NE", "SW", "SE" };
                    int[] cDirIdx    = { _cornerNWDirIdx, _cornerNEDirIdx, _cornerSWDirIdx, _cornerSEDirIdx };
                    for (int i = 0; i < 4; i++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(cLabels[i], GUILayout.Width(cLbl));
                        cDirIdx[i] = EditorGUILayout.Popup(cDirIdx[i], _cornerDirOptions);
                        EditorGUILayout.EndHorizontal();
                    }
                    _cornerNWDirIdx = cDirIdx[0]; _cornerNEDirIdx = cDirIdx[1];
                    _cornerSWDirIdx = cDirIdx[2]; _cornerSEDirIdx = cDirIdx[3];
                }
                else
                {
                    EditorGUILayout.LabelField("(assign catalogue first)");
                }
            }

            EditorGUILayout.Space(4);

            // Full build
            GUI.enabled = hasCat;
            GUI.backgroundColor = new Color(0.4f, 0.9f, 0.4f);
            if (GUILayout.Button("Build Room", GUILayout.Height(28)))
                DoBuildRoom();
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;

            EditorGUILayout.Space(2);

            // Partial rebuilds
            EditorGUILayout.BeginHorizontal();
            GUI.enabled = hasCat && ModExists();
            GUI.backgroundColor = new Color(1f, 0.85f, 0.3f);
            if (GUILayout.Button("Rebuild Floor", GUILayout.Height(24)))
                DoRebuildFloor();
            if (GUILayout.Button("Rebuild Walls", GUILayout.Height(24)))
                DoRebuildWalls();
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);

            // Clear
            GUI.enabled = ModExists();
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("Clear Room", GUILayout.Height(24)))
                DoClearRoom();
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;

            EditorGUILayout.Space(2);
            if (ModExists())
                EditorGUILayout.LabelField(
                    $"MOD has {_modRoot.transform.childCount} child group(s)",
                    EditorStyles.miniLabel);

            EditorGUI.indentLevel--;
        }

        // ── Step 4: Entry / Exit ──────────────────────────────────────────────

        private void DrawStep4_Doors()
        {
            if (!DrawFoldout(ref _show4, "④ Entry / Exit")) return;
            EditorGUI.indentLevel++;

            EditorGUILayout.HelpBox(
                "Removes a wall piece to create an opening. ExitPoint is stamped at the opening " +
                "for the Level Builder to use as a connection point. Doors are placed later.\n" +
                "Rooms ≤ 60 units: 1 opening per side.  Rooms > 60 units (custom): 2 per side.",
                MessageType.None);

            EditorGUILayout.Space(2);

            EditorGUILayout.BeginHorizontal();
            _addDoorWall = (WallSide)EditorGUILayout.EnumPopup("Wall Side", _addDoorWall, GUILayout.Width(220));
            GUI.enabled = ModExists();
            GUI.backgroundColor = new Color(0.5f, 0.8f, 1f);
            if (GUILayout.Button("Add Opening", GUILayout.Height(22)))
                DoAddDoor(_addDoorWall);
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            int totalTiers = Mathf.RoundToInt(FullHeight / WallTier);
            if (totalTiers > 1)
            {
                _openingTier = Mathf.Clamp(_openingTier, 0, totalTiers - 1);
                var tierLabels = new string[totalTiers];
                tierLabels[0] = "Tier 0  (Ground)";
                for (int t = 1; t < totalTiers; t++) tierLabels[t] = $"Tier {t}";
                _openingTier = EditorGUILayout.Popup(
                    new GUIContent("Opening Tier", "Which wall tier to cut the opening into."),
                    _openingTier, tierLabels);
            }
            else
            {
                _openingTier = 0;
            }

            bool canDouble = _sizePreset != SizePreset.Small;
            EditorGUI.BeginDisabledGroup(!canDouble);
            _doubleDoor = EditorGUILayout.Toggle(
                new GUIContent("Double Door?",
                    "Removes 2 adjacent wall pieces for a wider opening. Not available for Small preset."),
                canDouble && _doubleDoor);
            EditorGUI.EndDisabledGroup();

            if (!ModExists())
                EditorGUILayout.HelpBox("Build the room first (③).", MessageType.None);

            EditorGUI.indentLevel--;
        }

        // ── Step 5: Components ────────────────────────────────────────────────

        private void DrawStep5_Components()
        {
            if (!DrawFoldout(ref _show5, "⑤ Components")) return;
            EditorGUI.indentLevel++;

            if (!ModExists())
            {
                EditorGUILayout.HelpBox("Build the room first (③).", MessageType.None);
                EditorGUI.indentLevel--;
                return;
            }

            GUI.backgroundColor = new Color(0.5f, 0.85f, 0.5f);
            if (GUILayout.Button("Apply Components", GUILayout.Height(28)))
                DoApplyComponents();
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(2);

            var rp = _modRoot.GetComponent<RoomPiece>();
            if (rp != null)
            {
                EditorGUILayout.LabelField(
                    $"RoomPiece  size({rp.boundsSize.x:0.#}, {rp.boundsSize.y:0.#}, {rp.boundsSize.z:0.#})" +
                    $"  offset({rp.boundsOffset.x:0.#}, {rp.boundsOffset.y:0.#}, {rp.boundsOffset.z:0.#})",
                    EditorStyles.miniLabel);

                var exits = _modRoot.GetComponentsInChildren<ExitPoint>();
                EditorGUILayout.LabelField($"{exits.Length} ExitPoint(s):",
                    EditorStyles.miniLabel);
                foreach (var ep in exits)
                {
                    Vector3 p = ep.transform.localPosition;
                    EditorGUILayout.LabelField(
                        $"    {ep.exitDirection,-6}  ({p.x:0.#}, {p.y:0.#}, {p.z:0.#})",
                        EditorStyles.miniLabel);
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "No RoomPiece yet. Click Apply Components.", MessageType.None);
            }

            EditorGUI.indentLevel--;
        }

        // ── Step 6: Save ──────────────────────────────────────────────────────

        private void DrawStep6_Save()
        {
            if (!DrawFoldout(ref _show6, "⑥ Save")) return;
            EditorGUI.indentLevel++;

            if (!ModExists())
            {
                EditorGUILayout.HelpBox("Build the room first (③).", MessageType.None);
                EditorGUI.indentLevel--;
                return;
            }

            _saveName = EditorGUILayout.TextField("Save Name", _saveName);
            EditorGUILayout.LabelField(
                $"Prefab → {CuratedFolder}/{_saveName}.prefab", EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                $"Preset → {PresetsFolder}/{_saveName}.asset", EditorStyles.miniLabel);

            EditorGUILayout.Space(2);
            GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
            if (GUILayout.Button("Save Prefab + Preset", GUILayout.Height(30)))
                DoSave();
            GUI.backgroundColor = Color.white;

            EditorGUI.indentLevel--;
        }

        // ── Debug foldout ─────────────────────────────────────────────────────

        private void DrawDebugFoldout()
        {
            if (!DrawFoldout(ref _showDebug, "Debug Info")) return;
            EditorGUI.indentLevel++;

            EditorGUILayout.LabelField(
                $"FullWidth={FullWidth}  FullDepth={FullDepth}  " +
                $"halfW={HalfWidth}  halfD={HalfDepth}",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                $"Floor tiles: {Mathf.RoundToInt(FullWidth / FloorStep)} × " +
                $"{Mathf.RoundToInt(FullDepth / FloorStep)}",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                $"Wall pieces N/S: {Mathf.RoundToInt(FullWidth / WallStep)}  " +
                $"E/W: {Mathf.RoundToInt(FullDepth / WallStep)}",
                EditorStyles.miniLabel);

            if (ModExists())
            {
                EditorGUILayout.Space(2);
                Transform dg = GetDoorsGroup();
                int doorCount = dg != null ? dg.childCount : 0;
                var exits = _modRoot.GetComponentsInChildren<ExitPoint>();
                EditorGUILayout.LabelField(
                    $"Doors in hierarchy: {doorCount}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField(
                    $"ExitPoints: {exits.Length}", EditorStyles.miniLabel);
                foreach (var ep in exits)
                {
                    Vector3 lp = ep.transform.localPosition;
                    EditorGUILayout.LabelField(
                        $"  {ep.exitDirection,-6}  local({lp.x:0.##}, {lp.y:0.##}, {lp.z:0.##})",
                        EditorStyles.miniLabel);
                }
            }

            EditorGUI.indentLevel--;
        }

        // ── Logic: Build ──────────────────────────────────────────────────────

        /// <summary>
        /// Full build: creates MOD root + four child groups, then fills
        /// floors and walls. The ceiling/ and doors/ groups are created empty
        /// for manual use — they are never cleared by this call.
        /// </summary>
        private void DoBuildRoom()
        {
            EnsureMod();
            Undo.RegisterFullObjectHierarchyUndo(_modRoot, "Build Room");

            Transform floorsGrp = GetOrCreateGroup(GroupFloors);
            Transform wallsGrp = GetOrCreateGroup(GroupWalls);
            GetOrCreateGroup(GroupCeiling);
            GetOrCreateGroup(GroupDoors);

            BuildFloors(floorsGrp);
            BuildWalls(wallsGrp);

            EditorUtility.SetDirty(_modRoot);
            SceneView.RepaintAll();
            Debug.Log($"[RoomWorkshop] Built {FullWidth}×{FullDepth} unit room — " +
                      $"floors:{floorsGrp.childCount}  walls:{wallsGrp.childCount}");
        }

        /// <summary>Clears and rebuilds only the floors/ group.</summary>
        private void DoRebuildFloor()
        {
            if (!ModExists()) return;
            Undo.RegisterFullObjectHierarchyUndo(_modRoot, "Rebuild Floor");
            BuildFloors(GetOrCreateGroup(GroupFloors));
            EditorUtility.SetDirty(_modRoot);
            SceneView.RepaintAll();
        }

        /// <summary>Clears and rebuilds only the walls/ group.</summary>
        private void DoRebuildWalls()
        {
            if (!ModExists()) return;
            Undo.RegisterFullObjectHierarchyUndo(_modRoot, "Rebuild Walls");
            BuildWalls(GetOrCreateGroup(GroupWalls));
            EditorUtility.SetDirty(_modRoot);
            SceneView.RepaintAll();
        }

        /// <summary>Destroys the MOD root and all its children from the scene.</summary>
        private void DoClearRoom()
        {
            if (!ModExists()) return;
            Undo.DestroyObjectImmediate(_modRoot);
            _modRoot = null;
            SceneView.RepaintAll();
        }

        // ── Logic: Floor grid ─────────────────────────────────────────────────

        /// <summary>
        /// Fills <paramref name="floorsGrp"/> with a grid of Floor pieces.
        /// Floor tiles are <c>_O_</c> corner-pivot pieces.
        /// startX = −halfWidth, startZ = −halfDepth, step = 4 units.
        /// </summary>
        private void BuildFloors(Transform floorsGrp)
        {
            var all = GetEntriesByType(PieceCatalogue.PieceType.Floor);
            if (all.Count == 0)
            {
                Debug.LogWarning("[RoomWorkshop] No Floor pieces in catalogue — skipping floor fill.");
                return;
            }

            // ── Tier separation ───────────────────────────────────────────────
            // Base  : straight tiles  — fills the grid
            // Bonus : bonepile tiles  — 15 % random replacements
            // Corner: convex / angle  — placed under matching corner type
            // Skip  : trapdoor tiles  — manual placement only
            var baseTiles   = all.FindAll(e => e.prefab != null &&
                                               e.prefab.name.Contains("straight",   System.StringComparison.OrdinalIgnoreCase));
            var bonepiles    = all.FindAll(e => e.prefab != null &&
                                                e.prefab.name.Contains("bonepile",  System.StringComparison.OrdinalIgnoreCase));
            var angleTiles   = all.FindAll(e => e.prefab != null &&
                                                e.prefab.name.Contains("angle",     System.StringComparison.OrdinalIgnoreCase));
            var concaveTiles = all.FindAll(e => e.prefab != null &&
                                                e.prefab.name.Contains("concave",   System.StringComparison.OrdinalIgnoreCase));
            var convexTiles  = all.FindAll(e => e.prefab != null &&
                                                e.prefab.name.Contains("convex",    System.StringComparison.OrdinalIgnoreCase));

            PieceCatalogue.PieceEntry baseTile;
            if (baseTiles.Count > 0)
            {
                baseTile = baseTiles[0];
            }
            else
            {
                Debug.LogWarning(
                    "[RoomWorkshop] WARNING: No straight floor tile in catalogue. " +
                    "Add P_MOD_Floor_01_O_straight_med from 01_PARTS. " +
                    "Falling back to first Floor entry.");
                baseTile = all[0];
            }

            // ── Summary logs ──────────────────────────────────────────────────
            int countX = Mathf.RoundToInt(FullWidth / FloorStep);
            int countZ = Mathf.RoundToInt(FullDepth / FloorStep);

            Debug.Log($"[RoomWorkshop] Base floor tile: {baseTile.prefab.name}");
            Debug.Log($"[RoomWorkshop] Floor variants available: {bonepiles.Count} bonepile  " +
                      $"{angleTiles.Count} angle  {concaveTiles.Count} concave  {convexTiles.Count} convex");
            Debug.Log($"[RoomWorkshop] Floor grid: {countX}×{countZ} = {countX * countZ} tiles");

            // ── Corner tile lookup ────────────────────────────────────────────
            // angle          corner → angle   floor tile (P_MOD_Floor_01_O_angle_med)
            // concave        corner → convex  floor tile (geometry inverts: concave wall needs convex floor)
            // concave_small  corner → convex  floor tile, prefers "_3" suffix (P_MOD_Floor_01_O_convex_med_3)
            // convex         corner → concave floor tile (geometry inverts: convex wall needs concave floor)
            PieceCatalogue.PieceEntry CornerTile(int dirIdx)
            {
                var entries = GetCornerEntriesForSlot(dirIdx);
                if (entries.Count == 0 || entries[0].prefab == null) return null;
                string n = entries[0].prefab.name.ToLower();
                bool isAngle   = n.Contains("angle");
                bool isConcave = n.Contains("concave");
                bool isConvex  = n.Contains("convex");

                if (isAngle && angleTiles.Count > 0)
                {
                    // angle_[size]_2 (name ends "_2") → pivot-corrected _3 floor tile
                    // angle_[size]   (straight)        → base floor tile (must not be _3)
                    if (n.EndsWith("_2"))
                    {
                        var t3 = angleTiles.Find(e => e.prefab != null &&
                            e.prefab.name.EndsWith("_3", System.StringComparison.OrdinalIgnoreCase));
                        if (t3 != null) return t3;
                    }
                    var tBase = angleTiles.Find(e => e.prefab != null &&
                        !e.prefab.name.EndsWith("_3", System.StringComparison.OrdinalIgnoreCase));
                    return tBase ?? angleTiles[0];
                }
                if (isConcave && convexTiles.Count > 0)
                {
                    // concave_[size]_2 (name ends "_2") → pivot-corrected _3 floor tile
                    // concave_[size]   (straight)        → base floor tile (must not be _3)
                    if (n.EndsWith("_2"))
                    {
                        var t3 = convexTiles.Find(e => e.prefab != null &&
                            e.prefab.name.EndsWith("_3", System.StringComparison.OrdinalIgnoreCase));
                        if (t3 != null) return t3;
                    }
                    var tBase = convexTiles.Find(e => e.prefab != null &&
                        !e.prefab.name.EndsWith("_3", System.StringComparison.OrdinalIgnoreCase));
                    return tBase ?? convexTiles[0];
                }
                if (isConvex && concaveTiles.Count > 0) return concaveTiles[0];
                return null; // standard corner — use base tile
            }

            // ── Build ─────────────────────────────────────────────────────────
            ClearGroup(floorsGrp);

            float startX = -HalfWidth + FloorStep;
            float startZ = -HalfDepth;

            for (int ix = 0; ix < countX; ix++)
                for (int iz = 0; iz < countZ; iz++)
                {
                    float x = startX + ix * FloorStep;
                    float z = startZ + iz * FloorStep;

                    // Check if this tile sits under a room corner
                    bool isSW = ix == 0           && iz == 0;
                    bool isSE = ix == countX - 1  && iz == 0;
                    bool isNW = ix == 0           && iz == countZ - 1;
                    bool isNE = ix == countX - 1  && iz == countZ - 1;

                    PieceCatalogue.PieceEntry cornerOverride = null;
                    Quaternion tileRot = Quaternion.identity;
                    Vector3    tileOff = Vector3.zero;

                    if      (isSW) { cornerOverride = CornerTile(_cornerSWDirIdx); tileRot = Quaternion.Euler(0f,  -90f, 0f); tileOff = new Vector3(0f,        0f, FloorStep); }
                    else if (isSE) { cornerOverride = CornerTile(_cornerSEDirIdx); tileRot = Quaternion.Euler(0f,  180f, 0f); tileOff = new Vector3(-FloorStep, 0f, FloorStep); }
                    else if (isNW) { cornerOverride = CornerTile(_cornerNWDirIdx); tileRot = Quaternion.identity; }
                    else if (isNE) { cornerOverride = CornerTile(_cornerNEDirIdx); tileRot = Quaternion.Euler(0f,   90f, 0f); tileOff = new Vector3(-FloorStep, 0f, 0f); }

                    PieceCatalogue.PieceEntry chosen;
                    Quaternion rot;
                    Vector3    pos = new(x, 0f, z);
                    if (cornerOverride != null)
                    {
                        chosen = cornerOverride;
                        rot    = tileRot;
                        pos   += tileOff;
                    }
                    else
                    {
                        chosen = (bonepiles.Count > 0 && _rng.NextDouble() > 0.85)
                            ? bonepiles[_rng.Next(bonepiles.Count)]
                            : baseTile;
                        rot = Quaternion.identity;
                    }

                    PlaceInGroup(floorsGrp,
                        new List<PieceCatalogue.PieceEntry> { chosen },
                        pos,
                        rot,
                        $"Floor_{ix}_{iz}");
                }
        }

        // ── Logic: Wall perimeter ─────────────────────────────────────────────

        /// <summary>
        /// Places COMP_Wall_01_M_straight_large pieces around the four perimeter faces,
        /// then fills each corner with a COMP_Column_01_large piece.
        /// Wall face = local +Z; face is ~0.5 units ahead of the center pivot.
        /// fixedCoord = ±halfSize so the face lands flush at the room edge.
        /// Wall rotations face inward: N=180° S=0° E=270° W=90°.
        /// Step = 6 units; startX/Z = -halfSize + wallStep/2 (center-pivot grid).
        /// Room sizes must be multiples of 12 (LCM of floorStep 4 and wallStep 6).
        /// </summary>
        private void BuildWalls(Transform wallsGrp)
        {
            ClearGroup(wallsGrp);

            // Wall pieces have an edge pivot: each piece extends one FloorStep in the
            // direction determined by its rotation.  Count excludes the two corner slots.
            int perSideX = Mathf.RoundToInt(FullWidth  / FloorStep) - 1;
            int perSideZ = Mathf.RoundToInt(FullDepth  / FloorStep) - 1;

            // Fixed coords — flush at room boundary
            float northZ =  HalfDepth;
            float southZ = -HalfDepth;
            float eastX  =  HalfWidth;
            float westX  = -HalfWidth;

            // Start positions account for edge-pivot direction per wall
            float startN = -HalfWidth + FloorStep * 1.5f;
            float startS = -HalfWidth + FloorStep * 0.5f;
            float startE = -HalfDepth + FloorStep * 0.5f;
            float startW = -HalfDepth + FloorStep * 1.5f;

            // A corner with angle or concave variant requires the adjacent wall
            // piece to use the half-length variant (e.g. COMP_Wall_01_M_straight_large_half_R)
            // so the geometry fits correctly at the junction.
            bool nwHalf = CornerNeedsHalfWall(_cornerNWDirIdx);
            bool neHalf = CornerNeedsHalfWall(_cornerNEDirIdx);
            bool swHalf = CornerNeedsHalfWall(_cornerSWDirIdx);
            bool seHalf = CornerNeedsHalfWall(_cornerSEDirIdx);

            int tiers = Mathf.RoundToInt(FullHeight / WallTier);
            for (int tier = 0; tier < tiers; tier++)
            {
                float yOff = tier * WallTier;

                // North: N_0=NW no flip/no shift; N_last=NE flip+shift toward centre
                {
                    var p = GetWallEntriesForSide(_wallNEnabled, _wallNDirIdx);
                    if (p.Count > 0)
                    {
                        var half = GetHalfWallPrefabs(InferWallSize(p));
                        if (nwHalf && neHalf && perSideX == 2)
                            PlaceInGroup(wallsGrp, p, new Vector3(startN + FloorStep * 0.5f, yOff, northZ), Quaternion.identity, TierName("Wall_N_0", tier));
                        else
                            PlaceWallRun(wallsGrp, p, perSideX, startN, northZ, true,
                                Quaternion.identity, "N",
                                startOverride:   nwHalf ? half : null,
                                endOverride:     neHalf ? half : null,
                                flipEndRotation: neHalf,
                                shiftEndToward:  neHalf,
                                yOffset: yOff, tier: tier);
                    }
                }
                // South: S_0=SW flip+shift toward centre; S_last=SE no flip/no shift
                {
                    var p = GetWallEntriesForSide(_wallSEnabled, _wallSDirIdx);
                    if (p.Count > 0)
                    {
                        var half = GetHalfWallPrefabs(InferWallSize(p));
                        if (swHalf && seHalf && perSideX == 2)
                            PlaceInGroup(wallsGrp, p, new Vector3(startS + FloorStep * 0.5f, yOff, southZ), Quaternion.Euler(0f, 180f, 0f), TierName("Wall_S_0", tier));
                        else
                            PlaceWallRun(wallsGrp, p, perSideX, startS, southZ, true,
                                Quaternion.Euler(0f, 180f, 0f), "S",
                                startOverride:     swHalf ? half : null,
                                endOverride:       seHalf ? half : null,
                                flipStartRotation: swHalf,
                                shiftStartToward:  swHalf,
                                yOffset: yOff, tier: tier);
                    }
                }
                // East: E_0=SE flip+shift toward centre; E_last=NE no flip/no shift
                {
                    var p = GetWallEntriesForSide(_wallEEnabled, _wallEDirIdx);
                    if (p.Count > 0)
                    {
                        var half = GetHalfWallPrefabs(InferWallSize(p));
                        if (seHalf && neHalf && perSideZ == 2)
                            PlaceInGroup(wallsGrp, p, new Vector3(eastX, yOff, startE + FloorStep * 0.5f), Quaternion.Euler(0f, 90f, 0f), TierName("Wall_E_0", tier));
                        else
                            PlaceWallRun(wallsGrp, p, perSideZ, startE, eastX, false,
                                Quaternion.Euler(0f, 90f, 0f), "E",
                                startOverride:     seHalf ? half : null,
                                endOverride:       neHalf ? half : null,
                                flipStartRotation: seHalf,
                                shiftStartToward:  seHalf,
                                yOffset: yOff, tier: tier);
                    }
                }
                // West: W_0=SW no flip/no shift; W_last=NW flip+shift toward centre
                {
                    var p = GetWallEntriesForSide(_wallWEnabled, _wallWDirIdx);
                    if (p.Count > 0)
                    {
                        var half = GetHalfWallPrefabs(InferWallSize(p));
                        if (swHalf && nwHalf && perSideZ == 2)
                            PlaceInGroup(wallsGrp, p, new Vector3(westX, yOff, startW + FloorStep * 0.5f), Quaternion.Euler(0f, -90f, 0f), TierName("Wall_W_0", tier));
                        else
                            PlaceWallRun(wallsGrp, p, perSideZ, startW, westX, false,
                                Quaternion.Euler(0f, -90f, 0f), "W",
                                startOverride:   swHalf ? half : null,
                                endOverride:     nwHalf ? half : null,
                                flipEndRotation: nwHalf,
                                shiftEndToward:  nwHalf,
                                yOffset: yOff, tier: tier);
                    }
                }

                PlaceCorners(wallsGrp, yOff, tier);
            }
        }

        /// <summary>Places one straight run of wall pieces along a single axis.</summary>
        /// <param name="axisIsX">True → vary X, fixed Z. False → vary Z, fixed X.</param>
        /// <param name="startOverride">If non-null and non-empty, used for index 0 instead of <paramref name="prefabs"/>.</param>
        /// <param name="endOverride">If non-null and non-empty, used for the last index instead of <paramref name="prefabs"/>.</param>
        /// <param name="flipStartRotation">
        /// When true, the first piece (start override) is placed at rotation + 180° Y.
        /// Needed on South and East walls where the start piece sits to the LEFT of its
        /// corner — the half_R right side would otherwise point into the corner.
        /// </param>
        /// <param name="flipEndRotation">
        /// When true, the last piece (end override) is placed at rotation + 180° Y.
        /// Needed on North and West walls where the end piece sits to the LEFT of its
        /// corner — the half_R right side would otherwise point into the corner.
        /// </param>
        private void PlaceWallRun(
            Transform group,
            List<PieceCatalogue.PieceEntry> prefabs,
            int count,
            float start,
            float fixedCoord,
            bool axisIsX,
            Quaternion rotation,
            string prefix,
            List<PieceCatalogue.PieceEntry> startOverride    = null,
            List<PieceCatalogue.PieceEntry> endOverride      = null,
            bool flipStartRotation = false,
            bool flipEndRotation   = false,
            bool shiftStartToward  = false,
            bool shiftEndToward    = false,
            float yOffset          = 0f,
            int   tier             = 0)
        {
            Quaternion startRot = flipStartRotation
                ? rotation * Quaternion.Euler(0f, 180f, 0f)
                : rotation;
            Quaternion endRot = flipEndRotation
                ? rotation * Quaternion.Euler(0f, 180f, 0f)
                : rotation;

            for (int i = 0; i < count; i++)
            {
                float vary = start + i * FloorStep;

                bool isStart = i == 0         && startOverride != null && startOverride.Count > 0;
                bool isEnd   = i == count - 1 && endOverride   != null && endOverride.Count   > 0;

                if (isStart && shiftStartToward) vary += FloorStep;
                if (isEnd   && shiftEndToward)   vary -= FloorStep;

                var pos = axisIsX
                    ? new Vector3(vary,       yOffset, fixedCoord)
                    : new Vector3(fixedCoord, yOffset, vary);

                List<PieceCatalogue.PieceEntry> toPlace = prefabs;
                Quaternion rot = rotation;

                if (isStart) { toPlace = startOverride; rot = startRot; }
                if (isEnd)   { toPlace = endOverride;   rot = endRot;   }

                PlaceInGroup(group, toPlace, pos, rot, TierName($"Wall_{prefix}_{i}", tier));
            }
        }

        /// <summary>
        /// Places corner pieces at the four room corners. Each corner has its own
        /// <see cref="WallSize"/> setting — set to None to leave that corner open.
        /// </summary>
        private void PlaceCorners(Transform wallsGrp, float yOffset = 0f, int tier = 0)
        {
            float cx = HalfWidth;
            float cz = HalfDepth;

            var defs = new[]
            {
                (dirIdx: _cornerSWDirIdx, pos: new Vector3(-cx, yOffset, -cz), rot: Quaternion.Euler(0f,  90f, 0f), name: "Corner_SW"),
                (dirIdx: _cornerSEDirIdx, pos: new Vector3( cx, yOffset, -cz), rot: Quaternion.identity,             name: "Corner_SE"),
                (dirIdx: _cornerNEDirIdx, pos: new Vector3( cx, yOffset,  cz), rot: Quaternion.Euler(0f, -90f, 0f), name: "Corner_NE"),
                (dirIdx: _cornerNWDirIdx, pos: new Vector3(-cx, yOffset,  cz), rot: Quaternion.Euler(0f, 180f, 0f), name: "Corner_NW"),
            };

            int placed = 0;
            foreach (var c in defs)
            {
                var prefabs = GetCornerEntriesForSlot(c.dirIdx);
                if (prefabs.Count == 0) continue;
                PlaceInGroup(wallsGrp, prefabs, c.pos, c.rot, TierName(c.name, tier));
                placed++;
            }

            if (placed > 0 && tier == 0) Debug.Log($"[RoomWorkshop] Corners placed: {placed}");
        }

        // ── Wall / corner prefab helpers ──────────────────────────────────────

        private static string WallSizeString(WallSize size) => size switch
        {
            WallSize.Large => "large",
            WallSize.Med   => "med",
            WallSize.Small => "small",
            _              => "",
        };

        /// <summary>
        /// All straight (non-half) wall prefabs in the catalogue, regardless of size.
        /// Used to populate both Wall Prefab picker dropdowns.
        /// </summary>
        private List<PieceCatalogue.PieceEntry> GetAllStraightWallPrefabs()
        {
            if (_pieceCat == null) return new List<PieceCatalogue.PieceEntry>();
            return GetEntriesByType(PieceCatalogue.PieceType.Wall)
                .FindAll(e => e.prefab != null
                    && e.prefab.name.ToLower().Contains("straight")
                    && !e.prefab.name.ToLower().Contains("half"));
        }

        /// <summary>
        /// Returns the prefab entry list for a single wall side based on the current mode.
        /// Returns an empty list when the side is disabled or set to None.
        /// </summary>
        private List<PieceCatalogue.PieceEntry> GetWallEntriesForSide(bool enabled, int dirIdx)
        {
            if (_directionalWalls)
            {
                if (dirIdx == 0) return new List<PieceCatalogue.PieceEntry>();
                var all = GetAllStraightWallPrefabs();
                int i = dirIdx - 1;
                return i < all.Count
                    ? new List<PieceCatalogue.PieceEntry> { all[i] }
                    : new List<PieceCatalogue.PieceEntry>();
            }

            // Shared mode index layout: 0=None, 1=Auto, 2+=specific
            if (_wallPrefabIndex == 0) return new List<PieceCatalogue.PieceEntry>(); // None

            if (_wallPrefabIndex >= 2)
            {
                var all = GetAllStraightWallPrefabs();
                int i = _wallPrefabIndex - 2;
                return i < all.Count
                    ? new List<PieceCatalogue.PieceEntry> { all[i] }
                    : GetAllStraightWallPrefabs();
            }
            // _wallPrefabIndex == 1: Auto
            return GetAllStraightWallPrefabs();
        }

        /// <summary>
        /// Infers WallSize from the prefab name of the first entry in a list.
        /// Used to look up the matching half-wall prefab for angle/concave corners.
        /// </summary>
        private static WallSize InferWallSize(List<PieceCatalogue.PieceEntry> entries)
        {
            if (entries.Count == 0 || entries[0].prefab == null) return WallSize.Large;
            string n = entries[0].prefab.name.ToLower();
            if (n.Contains("large")) return WallSize.Large;
            if (n.Contains("med"))   return WallSize.Med;
            if (n.Contains("small")) return WallSize.Small;
            return WallSize.Large;
        }


        /// <summary>
        /// Returns wall entries that are the half-length variant adjacent to angle/concave corners
        /// (e.g. COMP_Wall_01_M_straight_large_half_R).
        /// Falls back to null (caller uses the full piece) when none are found.
        /// </summary>
        private List<PieceCatalogue.PieceEntry> GetHalfWallPrefabs(WallSize size)
        {
            string s = WallSizeString(size);
            var result = GetEntriesByType(PieceCatalogue.PieceType.Wall)
                .FindAll(e => e.prefab != null
                    && e.prefab.name.ToLower().Contains("straight")
                    && e.prefab.name.ToLower().Contains(s)
                    && e.prefab.name.ToLower().Contains("half"));
            if (result.Count == 0)
                Debug.LogWarning($"[RoomWorkshop] No straight+{s}+half Wall entries — adjacent wall will use full piece.");
            return result;
        }

        /// <summary>All Corner-type entries in the catalogue (all variants: standard, angle, concave, convex).</summary>
        private List<PieceCatalogue.PieceEntry> GetAllCornerPrefabs()
        {
            if (_pieceCat == null) return new List<PieceCatalogue.PieceEntry>();
            return GetEntriesByType(PieceCatalogue.PieceType.Corner)
                .FindAll(e => e.prefab != null);
        }

        /// <summary>
        /// Returns the prefab entry list for a single corner slot based on the current mode.
        /// In shared mode <paramref name="dirIdx"/> is ignored — <see cref="_cornerPrefabIndex"/> governs.
        /// In directional mode 0=None, 1+=specific prefab.
        /// </summary>
        private List<PieceCatalogue.PieceEntry> GetCornerEntriesForSlot(int dirIdx)
        {
            if (_directionalCorners)
            {
                if (dirIdx == 0) return new List<PieceCatalogue.PieceEntry>();
                var all = GetAllCornerPrefabs();
                int i = dirIdx - 1;
                return i < all.Count ? new List<PieceCatalogue.PieceEntry> { all[i] } : new List<PieceCatalogue.PieceEntry>();
            }

            // Shared: 0=None, 1=Auto, 2+=specific
            if (_cornerPrefabIndex == 0) return new List<PieceCatalogue.PieceEntry>();
            if (_cornerPrefabIndex >= 2)
            {
                var all = GetAllCornerPrefabs();
                int i = _cornerPrefabIndex - 2;
                return i < all.Count ? new List<PieceCatalogue.PieceEntry> { all[i] } : GetAllCornerPrefabs();
            }
            return GetAllCornerPrefabs(); // Auto
        }

        /// <summary>True when the selected corner prefab for this slot has "angle" in its name.</summary>
        private bool CornerIsAngle(int dirIdx)
        {
            var e = GetCornerEntriesForSlot(dirIdx);
            return e.Count > 0 && e[0].prefab != null && e[0].prefab.name.ToLower().Contains("angle");
        }

        /// <summary>True when the selected corner prefab for this slot has "concave" in its name.</summary>
        private bool CornerIsConcave(int dirIdx)
        {
            var e = GetCornerEntriesForSlot(dirIdx);
            return e.Count > 0 && e[0].prefab != null && e[0].prefab.name.ToLower().Contains("concave");
        }

        /// <summary>True when the selected corner prefab for this slot has "convex" in its name.</summary>
        private bool CornerIsConvex(int dirIdx)
        {
            var e = GetCornerEntriesForSlot(dirIdx);
            return e.Count > 0 && e[0].prefab != null && e[0].prefab.name.ToLower().Contains("convex");
        }

        /// <summary>True when the corner needs adjacent half-wall pieces (angle, concave, or convex variant).</summary>
        private bool CornerNeedsHalfWall(int dirIdx)
        {
            var e = GetCornerEntriesForSlot(dirIdx);
            if (e.Count == 0 || e[0].prefab == null) return false;
            string n = e[0].prefab.name.ToLower();
            return n.Contains("angle") || n.Contains("concave") || n.Contains("convex");
        }

        // ── Logic: Doors ──────────────────────────────────────────────────────

        /// <summary>
        /// Replaces a randomly chosen wall piece on <paramref name="wall"/> side
        /// with a Doorway piece from the catalogue, parenting the door to doors/.
        /// The door inherits the replaced wall's local position and rotation.
        /// </summary>
        private void DoAddDoor(WallSide wall)
        {
            if (!ModExists()) return;

            ExitPoint.Direction exitDir = wall switch
            {
                WallSide.North => ExitPoint.Direction.North,
                WallSide.South => ExitPoint.Direction.South,
                WallSide.East  => ExitPoint.Direction.East,
                _              => ExitPoint.Direction.West,
            };

            // Rooms > 60 units on the relevant axis get 2 openings per side, others get 1.
            float span = (wall == WallSide.North || wall == WallSide.South) ? FullWidth : FullDepth;
            int maxOpenings = span > 60f ? 2 : 1;

            // Guard: count existing ExitPoints for this direction
            int existingCount = 0;
            foreach (ExitPoint ep in _modRoot.GetComponentsInChildren<ExitPoint>())
                if (ep.exitDirection == exitDir) existingCount++;

            if (existingCount >= maxOpenings)
            {
                Debug.LogWarning($"[RoomWorkshop] {wall} side already has {existingCount} opening(s) — max {maxOpenings} for a {span}-unit span.");
                return;
            }

            // Collect wall pieces on this side, sorted by slot index
            Transform wallsGrp = _modRoot.transform.Find(GroupWalls);
            if (wallsGrp == null)
            {
                Debug.LogWarning("[RoomWorkshop] No walls/ group — Build Room first.");
                return;
            }

            string prefix = wall switch
            {
                WallSide.North => "Wall_N_",
                WallSide.South => "Wall_S_",
                WallSide.East  => "Wall_E_",
                _              => "Wall_W_",
            };

            int selectedTier = Mathf.Clamp(_openingTier, 0, Mathf.RoundToInt(FullHeight / WallTier) - 1);
            string tierSuffix = selectedTier == 0 ? "" : $"_t{selectedTier}";

            var allSideWalls = new List<(Transform t, int idx)>();
            for (int i = 0; i < wallsGrp.childCount; i++)
            {
                Transform child = wallsGrp.GetChild(i);
                if (!child.name.StartsWith(prefix)) continue;
                string suffix = child.name[prefix.Length..];
                int slotIdx;
                if (selectedTier == 0)
                {
                    // T0 pieces: Wall_N_{idx}  (no _t suffix)
                    if (!suffix.Contains("_t") && int.TryParse(suffix, out slotIdx))
                        allSideWalls.Add((child, slotIdx));
                }
                else
                {
                    // Higher-tier pieces: Wall_N_{idx}_t{tier}
                    if (suffix.EndsWith(tierSuffix) &&
                        int.TryParse(suffix[..^tierSuffix.Length], out slotIdx))
                        allSideWalls.Add((child, slotIdx));
                }
            }
            allSideWalls.Sort((a, b) => a.idx.CompareTo(b.idx));

            if (allSideWalls.Count == 0)
            {
                Debug.LogWarning($"[RoomWorkshop] No {wall} wall pieces found — Build Room first.");
                return;
            }

            // Exclude corner-adjacent first/last slots; fall back to all for small rooms
            int minIdx = allSideWalls[0].idx;
            int maxIdx = allSideWalls[^1].idx;
            var candidates = allSideWalls.FindAll(w => w.idx != minIdx && w.idx != maxIdx);
            if (candidates.Count == 0)
                candidates = new List<(Transform t, int idx)>(allSideWalls);

            // Ensure RoomPiece on root
            if (!_modRoot.TryGetComponent(out RoomPiece rp))
                rp = _modRoot.AddComponent<RoomPiece>();
            rp.boundsSize   = new Vector3(HalfWidth, HalfHeight, HalfDepth);
            rp.boundsOffset = new Vector3(0f, HalfHeight, 0f);

            if (_doubleDoor)
            {
                DoDoubleDoorOpen(wall, exitDir, allSideWalls, candidates, wallsGrp);
            }
            else
            {
                // Choose which slots to open: middle for 1, equally spaced for 2
                int toPlace = maxOpenings - existingCount;
                var slots = new List<(Transform t, int idx)>();
                if (toPlace == 1)
                {
                    slots.Add(candidates[candidates.Count / 2]);
                }
                else // toPlace == 2 — place both at once, equally spaced
                {
                    int i1 = candidates.Count / 3;
                    int i2 = 2 * candidates.Count / 3;
                    if (i2 >= candidates.Count) i2 = candidates.Count - 1;
                    if (i1 == i2)               i1 = Mathf.Max(0, i2 - 1);
                    slots.Add(candidates[i1]);
                    slots.Add(candidates[i2]);
                }

                foreach (var (wallPiece, _) in slots)
                {
                    Vector3 localPos = wallPiece.localPosition;
                    string  pieceName = wallPiece.name;
                    Undo.DestroyObjectImmediate(wallPiece.gameObject);
                    StampExitPoint(exitDir, localPos);
                    Debug.Log($"[RoomWorkshop] Removed {pieceName} (tier {selectedTier}) — opening on {wall}, ExitPoint.{exitDir} at {localPos}.");
                }
            }

            EditorUtility.SetDirty(_modRoot);
        }

        /// <summary>Creates an ExitPoint child on MOD root at the given local XZ position.</summary>
        private void StampExitPoint(ExitPoint.Direction dir, Vector3 localPos)
        {
            var go = new GameObject($"Exit_{dir}");
            Undo.RegisterCreatedObjectUndo(go, $"Create ExitPoint {dir}");
            go.transform.SetParent(_modRoot.transform, false);
            go.transform.localPosition = localPos;   // Y carries the tier offset
            go.AddComponent<ExitPoint>().exitDirection = dir;
        }

        /// <summary>Dispatches to the appropriate double-door variant.</summary>
        private void DoDoubleDoorOpen(
            WallSide wall,
            ExitPoint.Direction exitDir,
            List<(Transform t, int idx)> allSideWalls,
            List<(Transform t, int idx)> candidates,
            Transform wallsGrp)
        {
            // candidates.Count == 3 → Medium-like span (24 units, 5-slot run → 3 non-corner slots)
            bool isMediumLike = candidates.Count == 3;

            bool wallHasSpecialCorners = wall switch
            {
                WallSide.North => CornerNeedsHalfWall(_cornerNWDirIdx) || CornerNeedsHalfWall(_cornerNEDirIdx),
                WallSide.South => CornerNeedsHalfWall(_cornerSWDirIdx) || CornerNeedsHalfWall(_cornerSEDirIdx),
                WallSide.East  => CornerNeedsHalfWall(_cornerSEDirIdx) || CornerNeedsHalfWall(_cornerNEDirIdx),
                _              => CornerNeedsHalfWall(_cornerSWDirIdx) || CornerNeedsHalfWall(_cornerNWDirIdx),
            };

            float span = (wall == WallSide.North || wall == WallSide.South) ? FullWidth : FullDepth;

            if (isMediumLike && !wallHasSpecialCorners)
                DoDoubleDoorMediumStandard(wall, exitDir, candidates, wallsGrp);
            else if (isMediumLike && wallHasSpecialCorners)
                DoDoubleDoorMediumSpecial(wall, exitDir, allSideWalls, wallsGrp);
            else
                DoDoubleDoorLargeSlots(wall, exitDir, candidates, twoOpenings: span > 60f);
        }

        /// <summary>
        /// Medium + standard (_corner_) corners: candidates[1] (centre) removed,
        /// candidates[0] and candidates[2] replaced with half walls.
        /// </summary>
        private void DoDoubleDoorMediumStandard(
            WallSide wall,
            ExitPoint.Direction exitDir,
            List<(Transform t, int idx)> candidates,
            Transform wallsGrp)
        {
            var wallEntries = GetWallEntriesForSide(true, GetDirIdxForWall(wall));
            var halfEntries = GetHalfWallPrefabs(InferWallSize(wallEntries));

            // Flank rotation: geometry must face AWAY from the opening.
            // half_R geometry is on local +X; facing away from the opening:
            //   candidates[0] (low side): North/West need flip; South/East do not.
            //   candidates[2] (high side): South/East need flip; North/West do not.
            Quaternion baseRot = WallRotation(wall);
            bool flipLo = wall == WallSide.North || wall == WallSide.West;
            bool flipHi = wall == WallSide.South || wall == WallSide.East;
            Quaternion rotLo = flipLo ? baseRot * Quaternion.Euler(0f, 180f, 0f) : baseRot;
            Quaternion rotHi = flipHi ? baseRot * Quaternion.Euler(0f, 180f, 0f) : baseRot;

            // Capture positions before destruction
            Vector3 pos0 = candidates[0].t.localPosition;
            Vector3 pos1 = candidates[1].t.localPosition;
            Vector3 pos2 = candidates[2].t.localPosition;

            Undo.DestroyObjectImmediate(candidates[0].t.gameObject);
            Undo.DestroyObjectImmediate(candidates[1].t.gameObject);
            Undo.DestroyObjectImmediate(candidates[2].t.gameObject);

            string pfx = WallPrefix(wall);
            // The flipped half-wall has its geometry pointing away from the opening (toward the corner).
            // Its pivot must shift one FloorStep toward that corner so the geometry fills the gap correctly.
            // flipLo walls (North / West): shift the lo flank toward its near corner.
            // flipHi walls (South / East): shift the hi flank toward its near corner.
            Vector3 loShift = wall switch
            {
                WallSide.North => new Vector3(-FloorStep, 0f, 0f),   // toward NW corner (−X)
                WallSide.West  => new Vector3(0f, 0f, -FloorStep),   // toward SW corner (−Z)
                _              => Vector3.zero,
            };
            Vector3 hiShift = wall switch
            {
                WallSide.South => new Vector3( FloorStep, 0f, 0f),   // toward SE corner (+X)
                WallSide.East  => new Vector3(0f, 0f,  FloorStep),   // toward NE corner (+Z)
                _              => Vector3.zero,
            };
            if (halfEntries.Count > 0)
            {
                PlaceInGroup(wallsGrp, halfEntries, pos0 + loShift, rotLo, $"Wall_{pfx}_dbl_lo");
                PlaceInGroup(wallsGrp, halfEntries, pos2 + hiShift, rotHi, $"Wall_{pfx}_dbl_hi");
            }
            else
            {
                Debug.LogWarning("[RoomWorkshop] No half-wall prefab found — double door flanks left open.");
            }

            StampExitPoint(exitDir, pos1);
            Debug.Log($"[RoomWorkshop] Double door (medium/standard) on {wall} — ExitPoint.{exitDir} at {pos1}.");
        }

        /// <summary>
        /// Medium + special (angle/concave/convex) corners: corner-adjacent half walls
        /// replaced with full walls; all interior walls removed.
        /// </summary>
        private void DoDoubleDoorMediumSpecial(
            WallSide wall,
            ExitPoint.Direction exitDir,
            List<(Transform t, int idx)> allSideWalls,
            Transform wallsGrp)
        {
            var wallEntries = GetWallEntriesForSide(true, GetDirIdxForWall(wall));
            Quaternion baseRot = WallRotation(wall);
            string pfx = WallPrefix(wall);

            // Compute un-shifted positions for the two corner-adjacent slots
            // so the replacement full walls land on the correct grid positions.
            int perSide = allSideWalls.Count;
            Vector3 posFirst = WallRunPosition(wall, 0);
            Vector3 posLast  = WallRunPosition(wall, perSide - 1);

            // ExitPoint at the midpoint of the interior opening
            // (between slot 1 and slot perSide-2 which are being removed)
            Vector3 lo = WallRunPosition(wall, 1);
            Vector3 hi = WallRunPosition(wall, perSide - 2);
            Vector3 exitPos = (lo + hi) * 0.5f;

            // Each replacement wall shifts 2 units toward 0 on the run axis
            // (N/S walls run along X; E/W walls run along Z).
            // posFirst sits at the negative end → shift positive toward 0.
            // posLast  sits at the positive end → shift negative toward 0.
            const float kShift = 2f;
            Vector3 firstShift, lastShift;
            if (wall == WallSide.North || wall == WallSide.South)
            {
                firstShift = new Vector3( kShift, 0f, 0f);
                lastShift  = new Vector3(-kShift, 0f, 0f);
            }
            else   // East / West — run along Z
            {
                firstShift = new Vector3(0f, 0f,  kShift);
                lastShift  = new Vector3(0f, 0f, -kShift);
            }

            // Destroy all walls in this run (in reverse order to preserve indices)
            for (int i = allSideWalls.Count - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(allSideWalls[i].t.gameObject);

            if (wallEntries.Count > 0)
            {
                PlaceInGroup(wallsGrp, wallEntries, posFirst + firstShift, baseRot, $"Wall_{pfx}_dbl_lo");
                PlaceInGroup(wallsGrp, wallEntries, posLast  + lastShift,  baseRot, $"Wall_{pfx}_dbl_hi");
            }
            else
            {
                Debug.LogWarning("[RoomWorkshop] No wall prefab found — double door corner replacements skipped.");
            }

            StampExitPoint(exitDir, exitPos);
            Debug.Log($"[RoomWorkshop] Double door (medium/special) on {wall} — ExitPoint.{exitDir} at {exitPos}.");
        }

        /// <summary>
        /// Large+ (and >60-unit custom): removes 2 adjacent centre wall pieces per opening.
        /// </summary>
        private void DoDoubleDoorLargeSlots(
            WallSide wall,
            ExitPoint.Direction exitDir,
            List<(Transform t, int idx)> candidates,
            bool twoOpenings)
        {
            var openings = new List<(int lo, int hi)>();

            if (twoOpenings)
            {
                // Two double openings at 1/3 and 2/3 of the candidate run
                int c1 = candidates.Count / 3;
                int lo1 = Mathf.Clamp(c1 - 1, 0, candidates.Count - 2);
                int hi1 = lo1 + 1;

                int c2 = 2 * candidates.Count / 3;
                int lo2 = Mathf.Clamp(c2, hi1 + 2, candidates.Count - 2);
                int hi2 = lo2 + 1;

                openings.Add((lo1, hi1));
                openings.Add((lo2, hi2));
            }
            else
            {
                // Single double opening at the centre
                int center = candidates.Count / 2;
                int lo = Mathf.Max(0, center - 1);
                int hi = Mathf.Min(candidates.Count - 1, lo + 1);
                if (lo == hi) lo = Mathf.Max(0, hi - 1);
                openings.Add((lo, hi));
            }

            // Destroy and stamp — iterate openings from high to low index to preserve earlier indices
            for (int o = openings.Count - 1; o >= 0; o--)
            {
                var (lo, hi) = openings[o];
                Vector3 posLo = candidates[lo].t.localPosition;
                Vector3 posHi = candidates[hi].t.localPosition;

                Undo.DestroyObjectImmediate(candidates[hi].t.gameObject);
                Undo.DestroyObjectImmediate(candidates[lo].t.gameObject);

                Vector3 exitPos = (posLo + posHi) * 0.5f;
                StampExitPoint(exitDir, exitPos);
                Debug.Log($"[RoomWorkshop] Double door (large) on {wall} — ExitPoint.{exitDir} at {exitPos}.");
            }
        }

        // ── Wall-run positional helpers ───────────────────────────────────────

        /// <summary>Returns the standard (un-shifted) grid position for slot <paramref name="index"/> on <paramref name="wall"/>.</summary>
        private Vector3 WallRunPosition(WallSide wall, int index)
        {
            float vary = WallRunStart(wall) + index * FloorStep;
            float fixedCoord = wall switch
            {
                WallSide.North =>  HalfDepth,
                WallSide.South => -HalfDepth,
                WallSide.East  =>  HalfWidth,
                _              => -HalfWidth,
            };
            return (wall == WallSide.North || wall == WallSide.South)
                ? new Vector3(vary, 0f, fixedCoord)
                : new Vector3(fixedCoord, 0f, vary);
        }

        private float WallRunStart(WallSide wall) => wall switch
        {
            WallSide.North => -HalfWidth + FloorStep * 1.5f,
            WallSide.South => -HalfWidth + FloorStep * 0.5f,
            WallSide.East  => -HalfDepth + FloorStep * 0.5f,
            _              => -HalfDepth + FloorStep * 1.5f,
        };

        private static Quaternion WallRotation(WallSide wall) => wall switch
        {
            WallSide.North => Quaternion.identity,
            WallSide.South => Quaternion.Euler(0f, 180f, 0f),
            WallSide.East  => Quaternion.Euler(0f,  90f, 0f),
            _              => Quaternion.Euler(0f, -90f, 0f),
        };

        /// <summary>Returns <paramref name="baseName"/> for tier 0, or <c>{baseName}_t{tier}</c> for higher tiers.</summary>
        private static string TierName(string baseName, int tier) =>
            tier == 0 ? baseName : $"{baseName}_t{tier}";

        private static string WallPrefix(WallSide wall) => wall switch
        {
            WallSide.North => "N",
            WallSide.South => "S",
            WallSide.East  => "E",
            _              => "W",
        };

        private int GetDirIdxForWall(WallSide wall) => wall switch
        {
            WallSide.North => _wallNDirIdx,
            WallSide.South => _wallSDirIdx,
            WallSide.East  => _wallEDirIdx,
            _              => _wallWDirIdx,
        };

        /// <summary>
        /// Scans the doors/ group and logs the count so the user can verify
        /// before clicking Apply Components.
        /// </summary>
        private void DetectDoors()
        {
            if (!ModExists()) return;
            Transform dg = GetDoorsGroup();
            int count = dg != null ? dg.childCount : 0;
            Debug.Log($"[RoomWorkshop] Detect Doors — doors/ group has {count} object(s).  " +
                      "Click 'Apply Components' to stamp ExitPoints.");
            Repaint();
        }

        // ── Logic: Apply Components ───────────────────────────────────────────

        /// <summary>
        /// Stamps <see cref="RoomPiece"/> on the MOD root and creates one
        /// <see cref="ExitPoint"/> per object in the doors/ group.
        /// Direction is inferred by comparing each door's local XZ position
        /// to the four wall faces (nearest face wins).
        /// ExitPoint Y is always 0 so horizontal connections match LVL_ modules.
        /// </summary>
        private void DoApplyComponents()
        {
            if (!ModExists()) return;
            Undo.RegisterFullObjectHierarchyUndo(_modRoot, "Apply Components");

            // ── RoomPiece ─────────────────────────────────────────────────────
            RoomPiece rp = _modRoot.GetComponent<RoomPiece>()
                        ?? _modRoot.AddComponent<RoomPiece>();

            rp.pieceType = RoomPiece.PieceType.Room;
            rp.boundsSize = new Vector3(HalfWidth, HalfHeight, HalfDepth);
            rp.boundsOffset = new Vector3(0f, HalfHeight, 0f);

            // ── Remove old ExitPoints ─────────────────────────────────────────
            foreach (ExitPoint ep in _modRoot.GetComponentsInChildren<ExitPoint>())
                Undo.DestroyObjectImmediate(ep.gameObject);

            // ── Create ExitPoints from doors/ group ───────────────────────────
            Transform doorsGrp = GetDoorsGroup();
            int created = 0;

            if (doorsGrp != null)
            {
                for (int i = 0; i < doorsGrp.childCount; i++)
                {
                    Transform door = doorsGrp.GetChild(i);
                    Vector3 localPos = _modRoot.transform.InverseTransformPoint(
                                           door.transform.position);

                    ExitPoint.Direction dir = DetectExitDirection(localPos);

                    var go = new GameObject($"Exit_{dir}_{created}");
                    Undo.RegisterCreatedObjectUndo(go, "Create ExitPoint");
                    go.transform.SetParent(_modRoot.transform, false);
                    go.transform.localPosition = new Vector3(localPos.x, 0f, localPos.z);

                    var ep = go.AddComponent<ExitPoint>();
                    ep.exitDirection = dir;
                    created++;
                }
            }

            EditorUtility.SetDirty(_modRoot);
            Debug.Log($"[RoomWorkshop] RoomPiece applied — " +
                      $"boundsSize=({HalfWidth},{HalfHeight},{HalfDepth})  " +
                      $"ExitPoints={created}");
            Repaint();
        }

        // ── Logic: Save ───────────────────────────────────────────────────────

        /// <summary>
        /// Saves the MOD root as a prefab to <c>Assets/Prefabs/Rooms/Curated/</c>
        /// and writes a companion <see cref="RoomPreset"/> asset.
        /// </summary>
        private void DoSave()
        {
            if (!ModExists()) return;

            EnsureDirectory(CuratedFolder);
            EnsureDirectory(PresetsFolder);

            string prefabPath = $"{CuratedFolder}/{_saveName}.prefab";
            string presetPath = $"{PresetsFolder}/{_saveName}.asset";

            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "Overwrite existing prefab?",
                    $"A prefab already exists at:\n{prefabPath}\n\nOverwrite it?",
                    "Overwrite", "Cancel");
                if (!overwrite) return;
            }

            bool success;
            PrefabUtility.SaveAsPrefabAsset(_modRoot, prefabPath, out success);

            if (!success)
            {
                Debug.LogError($"[RoomWorkshop] Failed to save prefab: {prefabPath}");
                return;
            }

            // ── RoomPreset ────────────────────────────────────────────────────
            var preset = AssetDatabase.LoadAssetAtPath<RoomPreset>(presetPath)
                      ?? ScriptableObject.CreateInstance<RoomPreset>();

            preset.roomName = _saveName;
            preset.theme = _pieceCat != null
                                      ? _pieceCat.theme
                                      : PieceCatalogue.Theme.Dungeon;
            preset.roomPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            preset.pieceCatalogue = _pieceCat;

            var rp = _modRoot.GetComponent<RoomPiece>();
            if (rp != null) preset.pieceType = rp.pieceType;

            if (!AssetDatabase.Contains(preset))
                AssetDatabase.CreateAsset(preset, presetPath);
            else
                EditorUtility.SetDirty(preset);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(prefabPath));
            Debug.Log($"[RoomWorkshop] Prefab → {prefabPath}");
            Debug.Log($"[RoomWorkshop] Preset → {presetPath}");
        }

        // ── Shared helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Returns all non-null entries in the catalogue whose
        /// <see cref="PieceCatalogue.PieceEntry.pieceType"/> matches
        /// <paramref name="type"/>. Returns an empty list when no catalogue
        /// is assigned or no matching entries exist.
        /// </summary>
        private List<PieceCatalogue.PieceEntry> GetEntriesByType(
            PieceCatalogue.PieceType type)
        {
            var result = new List<PieceCatalogue.PieceEntry>();
            if (_pieceCat == null) return result;
            foreach (var e in _pieceCat.pieces)
                if (e.pieceType == type && e.prefab != null)
                    result.Add(e);
            return result;
        }

        /// <summary>
        /// Instantiates the first entry's prefab from <paramref name="entries"/>
        /// and parents it to <paramref name="group"/> at the given local transform.
        /// </summary>
        private static void PlaceInGroup(
            Transform group,
            List<PieceCatalogue.PieceEntry> entries,
            Vector3 localPos,
            Quaternion localRot,
            string goName)
        {
            if (entries.Count == 0 || entries[0].prefab == null) return;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(entries[0].prefab);
            Undo.RegisterCreatedObjectUndo(go, $"Place {goName}");
            go.name = goName;
            go.transform.SetParent(group, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale    = Vector3.one;
            Debug.Log($"[DIAG] group world pos: {group.position}");
            Debug.Log($"[DIAG] piece world pos after place: {go.transform.position}  local: {go.transform.localPosition}");
        }

        /// <summary>
        /// Returns the exit direction whose wall face is closest to
        /// <paramref name="localPos"/> in XZ space.
        /// </summary>
        private ExitPoint.Direction DetectExitDirection(Vector3 localPos)
        {
            float dN = Mathf.Abs(localPos.z - HalfDepth);
            float dS = Mathf.Abs(localPos.z - -HalfDepth);
            float dE = Mathf.Abs(localPos.x - HalfWidth);
            float dW = Mathf.Abs(localPos.x - -HalfWidth);
            float min = Mathf.Min(dN, Mathf.Min(dS, Mathf.Min(dE, dW)));

            if (Mathf.Approximately(min, dN)) return ExitPoint.Direction.North;
            if (Mathf.Approximately(min, dS)) return ExitPoint.Direction.South;
            if (Mathf.Approximately(min, dE)) return ExitPoint.Direction.East;
            return ExitPoint.Direction.West;
        }

        /// <summary>Creates the MOD root if it doesn't exist in the scene.</summary>
        private void EnsureMod()
        {
            if (_modRoot != null) return;
            var existing = GameObject.Find(ModRootName);
            if (existing != null) { _modRoot = existing; return; }
            _modRoot = new GameObject(ModRootName);
            Undo.RegisterCreatedObjectUndo(_modRoot, "Create MOD root");
        }

        /// <summary>Returns the doors/ child group, or null if absent.</summary>
        private Transform GetDoorsGroup() =>
            ModExists() ? _modRoot.transform.Find(GroupDoors) : null;

        /// <summary>Finds or creates a named child group under MOD root.</summary>
        private Transform GetOrCreateGroup(string groupName)
        {
            Transform t = _modRoot.transform.Find(groupName);
            if (t != null) return t;
            var go = new GameObject(groupName);
            Undo.RegisterCreatedObjectUndo(go, $"Create {groupName}");
            go.transform.SetParent(_modRoot.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale    = Vector3.one;
            return go.transform;
        }

        /// <summary>Destroys all children of <paramref name="group"/> via Undo.</summary>
        private static void ClearGroup(Transform group)
        {
            for (int i = group.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(group.GetChild(i).gameObject);
        }

        private bool ModExists() => _modRoot != null;

        private static int SnapTo12Int(int v) =>
            Mathf.Max(12, Mathf.RoundToInt(Mathf.Max(1, v) / 12f) * 12);

        /// <summary>
        /// Draws a labelled row: slider + int field, value snapped to multiples of <paramref name="snap"/>.
        /// </summary>
        private static int DrawSliderWithIntField(GUIContent label, int current, int min, int max, int snap = 12)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(label);
            int steps     = (max - min) / snap;
            int curStep   = Mathf.Clamp((current - min) / snap, 0, steps);
            int newStep   = Mathf.RoundToInt(GUILayout.HorizontalSlider(curStep, 0, steps));
            int fromSlider = SnapToStep(min + newStep * snap, snap);
            int typed     = EditorGUILayout.IntField(fromSlider, GUILayout.Width(48));
            EditorGUILayout.EndHorizontal();
            return SnapToStep(Mathf.Clamp(typed, min, max), snap);
        }

        private static void EnsureDirectory(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "Assets";
            string folder = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureDirectory(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }

        /// <summary>
        /// Draws a bold foldout header and returns true if the section is expanded.
        /// Calls BeginFoldoutHeaderGroup + EndFoldoutHeaderGroup internally.
        /// </summary>
        private static bool DrawFoldout(ref bool state, string title)
        {
            var style = new GUIStyle(EditorStyles.foldoutHeader)
            { fontStyle = FontStyle.Bold };
            state = EditorGUILayout.BeginFoldoutHeaderGroup(state, title, style);
            EditorGUILayout.EndFoldoutHeaderGroup();
            return state;
        }

        private static void DrawDivider()
        {
            var rect = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.3f));
            EditorGUILayout.Space(2);
        }

        // ── Scene-view compass gizmo ──────────────────────────────────────────

        /// <summary>
        /// Draws N/S/E/W labels outside the room boundary in the Scene view.
        /// Blue = Z axis (North/South), Red = X axis (East/West).
        /// </summary>
        private void OnSceneGUI(SceneView _)
        {
            if (!ModExists()) return;

            Transform root = _modRoot.transform;
            float halfW = HalfWidth;
            float halfD = HalfDepth;
            float midY = HalfHeight;
            const float OFF = 2f;

            var blueStyle = new GUIStyle(EditorStyles.boldLabel)
            { normal = { textColor = new Color(0.2f, 0.5f, 1f) }, fontSize = 14 };
            var redStyle = new GUIStyle(EditorStyles.boldLabel)
            { normal = { textColor = new Color(1f, 0.25f, 0.25f) }, fontSize = 14 };

            Vector3 nPt = root.TransformPoint(new Vector3(0f, midY, halfD + OFF));
            Vector3 sPt = root.TransformPoint(new Vector3(0f, midY, -halfD - OFF));
            Vector3 ePt = root.TransformPoint(new Vector3(halfW + OFF, midY, 0f));
            Vector3 wPt = root.TransformPoint(new Vector3(-halfW - OFF, midY, 0f));

            Vector3 nEdge = root.TransformPoint(new Vector3(0f, midY, halfD));
            Vector3 sEdge = root.TransformPoint(new Vector3(0f, midY, -halfD));
            Vector3 eEdge = root.TransformPoint(new Vector3(halfW, midY, 0f));
            Vector3 wEdge = root.TransformPoint(new Vector3(-halfW, midY, 0f));

            Handles.color = new Color(0.2f, 0.5f, 1f);
            Handles.DrawLine(nEdge, nPt, 2f);
            Handles.DrawLine(sEdge, sPt, 2f);
            Handles.Label(nPt, "  N  (+Z)", blueStyle);
            Handles.Label(sPt, "  S  (−Z)", blueStyle);

            Handles.color = new Color(1f, 0.25f, 0.25f);
            Handles.DrawLine(eEdge, ePt, 2f);
            Handles.DrawLine(wEdge, wPt, 2f);
            Handles.Label(ePt, "  E  (+X)", redStyle);
            Handles.Label(wPt, "  W  (−X)", redStyle);

            Handles.color = Color.white;
        }
    }
}
#endif
