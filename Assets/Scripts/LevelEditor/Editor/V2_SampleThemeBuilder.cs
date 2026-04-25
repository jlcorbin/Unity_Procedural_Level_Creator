#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using LevelGen;

namespace LevelEditor
{
    /// <summary>
    /// Editor smoke-test for the V2 theme resolution path.
    /// Themes are user-authored in the catalogue Inspector — this utility only
    /// exercises whichever theme is already selected on the scene RoomBuilder.
    ///
    /// <para>Menu: <c>Tools &gt; V2 Tests &gt; Theme: Build with Selected Theme</c></para>
    /// </summary>
    public static class V2_SampleThemeBuilder
    {
        [MenuItem("Tools/V2 Tests/Theme: Build with Selected Theme")]
        private static void RunThemeTest()
        {
            var builder = Object.FindFirstObjectByType<RoomBuilder>();
            if (builder == null)
            {
                Debug.LogWarning("[V2_ThemeTest] No RoomBuilder found in the active scene. " +
                                 "Add a RoomBuilder component to a scene object first.");
                return;
            }

            PieceCatalogue cat   = builder.catalogue;
            string         theme = builder.themeName;

            bool hasTheme = cat != null
                         && !string.IsNullOrEmpty(theme)
                         && cat.GetTheme(theme) != null;

            if (!hasTheme)
            {
                string reason = cat == null           ? "catalogue is null"
                              : string.IsNullOrEmpty(theme) ? "themeName is empty"
                              : $"theme '{theme}' not found in catalogue";
                Debug.LogWarning($"[V2_ThemeTest] No theme selected ({reason}); " +
                                 "falling back to direct slots.");
            }

            // ── Pass 1: build with inspector-assigned theme (or fallback) ─────
            Debug.Log($"[V2_ThemeTest] Pass 1 — theme='{theme}' (hasTheme={hasTheme}):");
            LogResolve(builder, PieceCatalogue.PieceType.Floor);
            LogResolve(builder, PieceCatalogue.PieceType.Wall);
            LogResolve(builder, PieceCatalogue.PieceType.Corner);
            LogResolve(builder, PieceCatalogue.PieceType.Column);
            LogResolve(builder, PieceCatalogue.PieceType.Doorway);
            builder.Build();

            // ── Pass 2: clear theme — must fall back to direct slots ──────────
            string saved = builder.themeName;
            builder.themeName = "";
            Debug.Log("[V2_ThemeTest] Pass 2 — no theme (direct-slot fallback):");
            LogResolve(builder, PieceCatalogue.PieceType.Floor);
            LogResolve(builder, PieceCatalogue.PieceType.Wall);
            LogResolve(builder, PieceCatalogue.PieceType.Corner);
            builder.Build();

            // Restore original theme selection.
            builder.themeName = saved;
            Debug.Log($"[V2_ThemeTest] Done. themeName restored to '{saved}'.");
        }

        private static void LogResolve(RoomBuilder builder, PieceCatalogue.PieceType type)
        {
            GameObject resolved = builder.ResolvePrefab(type);
            Debug.Log($"[V2_ThemeTest]   {type,-8} → {(resolved != null ? resolved.name : "(null)")}");
        }

        // ── Task 6: categorized save folder roundtrip ─────────────────────────

        [MenuItem("Tools/V2 Tests/Save: Categorized RoomPiece Roundtrip")]
        private static void RunSaveRoundtripTest()
        {
            var builder = Object.FindFirstObjectByType<RoomBuilder>();
            if (builder == null)
            {
                Debug.LogWarning("[SaveRoundtrip] No RoomBuilder in the active scene.");
                return;
            }

            // ── Check 1: Room / Starter folder ────────────────────────────────
            builder.pieceType    = PieceType.Room;
            builder.roomCategory = RoomCategory.Starter;

            string roomFolder = builder.ResolveSaveFolder();
            const string expectedRoom = "Assets/Prefabs/Rooms/Starter";
            LogCheck(roomFolder == expectedRoom,
                $"ResolveSaveFolder() = '{roomFolder}'  expected '{expectedRoom}'");

            builder.EnsureFolderExists(roomFolder);
            bool roomExists = AssetDatabase.IsValidFolder(roomFolder);
            LogCheck(roomExists, $"Folder exists after EnsureFolderExists: {roomFolder}");

            // ── Check 2: Hall / Special folder ────────────────────────────────
            builder.pieceType    = PieceType.Hall;
            builder.hallCategory = HallCategory.Special;

            string hallFolder = builder.ResolveSaveFolder();
            const string expectedHall = "Assets/Prefabs/Halls/Special";
            LogCheck(hallFolder == expectedHall,
                $"ResolveSaveFolder() = '{hallFolder}'  expected '{expectedHall}'");

            builder.EnsureFolderExists(hallFolder);
            bool hallExists = AssetDatabase.IsValidFolder(hallFolder);
            LogCheck(hallExists, $"Folder exists after EnsureFolderExists: {hallFolder}");

            Debug.Log("[SaveRoundtrip] Done. No prefab saved — folder structure only.");
        }

        private static void LogCheck(bool pass, string detail)
        {
            if (pass) Debug.Log($"[SaveRoundtrip] PASS — {detail}");
            else       Debug.LogWarning($"[SaveRoundtrip] FAIL — {detail}");
        }
    }
}
#endif
