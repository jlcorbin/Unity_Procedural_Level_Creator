// LockIndicator.cs — world-space triangle billboard displayed above a
// locked target. The indicator bobs vertically and always faces the camera.
//
// Setup: instantiated at runtime by TargetLock via AddComponent<LockIndicator>
// on a fresh GameObject; TargetLock calls Init(enemyRoot) immediately after.
// Never place this in a prefab slot — TargetLock manages the lifecycle.
//
// IL2CPP / mobile note: The URP/Unlit shader is resolved via Shader.Find at
// runtime. On IL2CPP mobile builds Unity's shader stripping can remove
// shaders that are not referenced by materials in the build. If the triangle
// disappears on device, add "Universal Render Pipeline/Unlit" to
// Project Settings ▶ Graphics ▶ Always Included Shaders, or hold a
// Resources/-based fallback material in a Resources/ folder so the shader
// is preserved. See class summary XML doc for the canonical note.

using UnityEngine;

namespace LevelGen
{
    /// <summary>
    /// Procedural equilateral-triangle billboard that bobs above a locked
    /// enemy. Instantiated by <see cref="LevelGen.Player.TargetLock"/> via
    /// <c>AddComponent&lt;LockIndicator&gt;</c> on a fresh GameObject.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The triangle mesh is built once in Awake using a URP Unlit material
    /// with a yellow <c>_BaseColor</c>. Vertices wind counter-clockwise
    /// (top-left, top-right, bottom-center) when viewed from the camera,
    /// matching Unity's default front-face convention.
    /// </para>
    /// <para>
    /// <strong>IL2CPP / mobile shader stripping:</strong> This component
    /// resolves "Universal Render Pipeline/Unlit" via <c>Shader.Find</c>
    /// at runtime. On ARM64 IL2CPP builds, Unity may strip the shader
    /// during build if no material in the build already references it.
    /// If the indicator becomes invisible on device, add
    /// "Universal Render Pipeline/Unlit" to
    /// <em>Project Settings ▶ Graphics ▶ Always Included Shaders</em>,
    /// or place a fallback URP/Unlit material in a <c>Resources/</c>
    /// folder and load it with <c>Resources.Load&lt;Material&gt;</c>
    /// instead of the programmatic <c>new Material(shader)</c> call.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [DisallowMultipleComponent]
    public class LockIndicator : MonoBehaviour
    {
        // ── Triangle geometry constants ──────────────────────────────────────
        // Equilateral triangle side ≈ 0.4 units, pointing downward (tip at
        // the bottom center so it reads as "arrow pointing at the enemy").
        private const float TriangleSide   = 0.4f;
        private const float TriangleHeight = TriangleSide * 0.866f; // sqrt(3)/2

        // ── SerializeFields ──────────────────────────────────────────────────

        [SerializeField]
        [Tooltip("Height above the enemy root pivot where the indicator floats.")]
        private float _yOffset = 2.5f;

        [SerializeField]
        [Tooltip("Amplitude of the vertical sinusoidal bob, in world units.")]
        private float _bobAmplitude = 0.15f;

        [SerializeField]
        [Tooltip("Number of complete bob cycles per second.")]
        private float _bobSpeed = 2f;

        // ── Private state ────────────────────────────────────────────────────
        private Vector3 _basePosition;
        private Camera _cam;

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            BuildMesh();
        }

        private void Update()
        {
            // Lazy camera cache — matches EnemyHealthBar.Update pattern.
            // Retries each null frame so a late-binding CinemachineAutoBind
            // camera is picked up automatically.
            if (_cam == null)
            {
                _cam = Camera.main;
                if (_cam == null) return;
            }

            // Bob: sinusoidal vertical displacement around the base position.
            float bob = Mathf.Sin(Time.time * _bobSpeed * Mathf.PI * 2f) * _bobAmplitude;
            transform.localPosition = _basePosition + Vector3.up * bob;

            // Billboard: align this transform's rotation to the camera so the
            // triangle always faces the viewer. Using the camera's own rotation
            // avoids the LookAt mirror-flip and matches EnemyHealthBar's pattern.
            transform.rotation = Quaternion.LookRotation(-_cam.transform.forward, _cam.transform.up);
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Initialises the indicator's base position and parents it to the
        /// enemy root so it follows the enemy when it moves.
        /// Call immediately after instantiation, before the first Update.
        /// </summary>
        /// <param name="enemyRoot">The enemy's root transform.</param>
        public void Init(Transform enemyRoot)
        {
            _basePosition = Vector3.up * _yOffset;
            transform.SetParent(enemyRoot, worldPositionStays: false);
            transform.localPosition = _basePosition;
        }

        // ── Private helpers ──────────────────────────────────────────────────

        /// <summary>
        /// Builds a procedural equilateral triangle mesh (downward-pointing)
        /// and assigns a URP Unlit yellow material. Called once in Awake.
        /// </summary>
        private void BuildMesh()
        {
            // Vertices: top-left (0), top-right (1), bottom-center (2).
            // The triangle points downward so it reads as "arrow toward enemy."
            float halfWidth = TriangleSide * 0.5f;
            Vector3[] vertices = new Vector3[]
            {
                new Vector3(-halfWidth,  TriangleHeight * 0.5f, 0f),   // top-left
                new Vector3( halfWidth,  TriangleHeight * 0.5f, 0f),   // top-right
                new Vector3(      0f,  -TriangleHeight * 0.5f, 0f),    // bottom-center
            };

            // CCW winding when viewed from camera (toward -Z / toward viewer
            // in Unity's left-handed coordinate system). Per unity-specialist
            // guidance: {0, 2, 1} winds CCW viewed from camera.
            int[] triangles = new int[] { 0, 2, 1 };

            // Normals all point toward the camera (Vector3.back = -Z in local space).
            Vector3[] normals = new Vector3[]
            {
                Vector3.back,
                Vector3.back,
                Vector3.back,
            };

            // UVs unused (unlit solid color), all at zero.
            Vector2[] uvs = new Vector2[]
            {
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
            };

            var mesh = new Mesh();
            mesh.name = "LockIndicatorTriangle";
            mesh.vertices  = vertices;
            mesh.triangles = triangles;
            mesh.normals   = normals;
            mesh.uv        = uvs;
            mesh.RecalculateBounds();

            var meshFilter = GetComponent<MeshFilter>();
            meshFilter.mesh = mesh;

            // URP Unlit material — yellow tint.
            // _BaseColor is the URP/Unlit tint property (NOT _Color).
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            Material mat;
            if (shader != null)
            {
                mat = new Material(shader);
                mat.SetColor("_BaseColor", Color.yellow);
            }
            else
            {
                // Fallback: pink default material signals a missing shader
                // so it's immediately visible in the editor.
                Debug.LogWarning("[LockIndicator] Could not find shader " +
                                 "'Universal Render Pipeline/Unlit'. " +
                                 "Check Always Included Shaders or add a " +
                                 "fallback material in a Resources/ folder.", this);
                mat = new Material(Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse"));
                if (mat.shader != null)
                    mat.color = Color.yellow;
            }

            var meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.material = mat;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }
    }
}
