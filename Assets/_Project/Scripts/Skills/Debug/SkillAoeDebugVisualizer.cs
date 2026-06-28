// Project C: Skills/Battle — T-INP-06
// SkillAoeDebugVisualizer: 3D wireframe визуализация AOE-зоны навыка при активации.
// Auto-spawn singleton MonoBehaviour. SkillNodeConfig.debugVisualizeAoe = true → при TryActivate
// показывает временный 3D wireframe (LineRenderer-based) на debugVisualizeDuration секунд с fade-out.
//
// Что рисуем по AoeFormula:
//   - SingleTarget: маленькая сфера-маркер
//   - Cone:         wire-cone (base circle + 8 spokes + apex sphere)
//   - Sphere:       3 great circles (XY/XZ/YZ)
//   - Line:         wire-box (length × width × 0.5м высота)
//   - Box:          wire-cube (length × width × width)
//
// Только в Editor/Development build (`#if UNITY_EDITOR || DEVELOPMENT_BUILD`). В release — no-op.
//
// Design: docs/dev/INP06_AOE_DEBUG_VISUALIZATION.md

using System.Collections.Generic;
using UnityEngine;
using ProjectC.Skills;

namespace ProjectC.Skills.DebugVisualization
{
    /// <summary>
    /// T-INP-06: AOE Debug Visualizer singleton. Создаёт временные LineRenderer-based
    /// wireframe-объекты на время каста. Editor-only — в release build все вызовы no-op.
    /// </summary>
    public class SkillAoeDebugVisualizer : MonoBehaviour
    {
        public static SkillAoeDebugVisualizer Instance { get; private set; }

        [Header("Defaults")]
        [Tooltip("Цвет wireframe по умолчанию.")]
        [SerializeField] private Color _defaultColor = new Color(1f, 0.85f, 0.2f, 0.9f);

        [Tooltip("Стандартная длительность показа, если в SkillNodeConfig не задана.")]
        [SerializeField] private float _defaultDuration = 0.6f;

        [Tooltip("Толщина линий (world units).")]
        [SerializeField] private float _lineWidth = 0.03f;

        // Список активных wireframe-объектов (для авто-удаления).
        private readonly List<AoeWireframe> _active = new List<AoeWireframe>();

        // === Lifecycle ===

        /// <summary>
        /// Auto-create singleton если Instance==null (для удобства тестирования).
        /// Безопасно вызывать из любого места.
        /// </summary>
        public static SkillAoeDebugVisualizer EnsureExists()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("[SkillAoeDebugVisualizer]");
            DontDestroyOnLoad(go);
            return go.AddComponent<SkillAoeDebugVisualizer>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            // Fade out + cleanup.
            float dt = Time.deltaTime;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var w = _active[i];
                if (w == null || w.root == null)
                {
                    _active.RemoveAt(i);
                    continue;
                }
                w.elapsed += dt;
                float t = w.elapsed / w.duration;
                if (t >= 1f)
                {
                    if (Application.isPlaying) Destroy(w.root);
                    else DestroyImmediate(w.root);
                    _active.RemoveAt(i);
                    continue;
                }
                // Fade alpha (1 → 0) на каждом LineRenderer ребёнке root'а.
                float alpha = 1f - t;
                var lrs = w.root.GetComponentsInChildren<LineRenderer>();
                foreach (var lr in lrs)
                {
                    if (lr.sharedMaterial == null) continue;
                    // Не редактируем sharedMaterial, чтобы не пачкать asset'ы. Используем instance mat.
                    var mat = lr.material; // creates instance per-renderer
                    if (mat == null) continue;
                    var c = w.startColor;
                    c.a = w.startColor.a * alpha;
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
                }
            }
        }

        // === Public API ===

        /// <summary>
        /// Показать wireframe AOE-зоны в указанной позиции. No-op если debugVisualizeAoe=false
        /// или если config==null, или в release build.
        /// </summary>
        public void ShowAoe(Vector3 origin, Vector3 forward, SkillNodeConfig config, float durationOverride = -1f)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (config == null) return;
            if (!config.debugVisualizeAoe) return;

            float duration = durationOverride > 0f
                ? durationOverride
                : (config.debugVisualizeDuration > 0f ? config.debugVisualizeDuration : _defaultDuration);

            var color = _defaultColor;
            var root = new GameObject($"AoeDebug_{config.skillId}");
            root.transform.position = origin;
            if (forward.sqrMagnitude > 0.0001f)
                root.transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            else
                root.transform.rotation = Quaternion.identity;

            BuildWireframe(root, config, color);

            var w = new AoeWireframe
            {
                root = root,
                elapsed = 0f,
                duration = duration,
                startColor = color
            };
            _active.Add(w);
#endif
        }

        /// <summary>
        /// Хук для SkillInputService.TryActivate. Показывает AOE только если debugVisualizeAoe=true.
        /// </summary>
        public void OnSkillActivated(SkillNodeConfig config, Vector3 origin, Vector3 forward)
        {
            ShowAoe(origin, forward, config);
        }

        // === Wireframe builders ===

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void BuildWireframe(GameObject root, SkillNodeConfig cfg, Color color)
        {
            switch (cfg.aoeFormula)
            {
                case AoeFormula.SingleTarget:
                    BuildSphereWire(root, 0.3f, color, "SingleTarget");
                    break;
                case AoeFormula.Cone:
                    BuildConeWire(root, cfg.aoeSize, cfg.aoeConeAngleDeg, color);
                    break;
                case AoeFormula.Sphere:
                    BuildSphereWire(root, cfg.aoeSize, color, "Sphere");
                    break;
                case AoeFormula.Line:
                    BuildLineWire(root, cfg.aoeSize, cfg.aoeWidth, color);
                    break;
                case AoeFormula.Box:
                    BuildBoxWire(root, cfg.aoeSize, cfg.aoeWidth, color);
                    break;
            }
        }

        // URP-friendly unlit transparent material.
        private Material MakeMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            // URP transparent surface mode.
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return mat;
        }

        // Attach a LineRenderer as child of root with local-space positions.
        private void AttachLineToRoot(GameObject root, string name, Vector3[] positions, Color color, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = positions.Length;
            lr.SetPositions(positions);
            lr.startWidth = _lineWidth;
            lr.endWidth = _lineWidth;
            lr.loop = loop;
            lr.numCapVertices = 2;
            lr.alignment = LineAlignment.View;
            lr.material = MakeMaterial(color);
            lr.startColor = color;
            lr.endColor = color;
        }

        // === Cone: apex at origin, base at (0,0,length), angle = coneAngleDeg ===
        private void BuildConeWire(GameObject root, float length, float angleDeg, Color color)
        {
            if (length <= 0f) return;
            float halfRad = angleDeg * 0.5f * Mathf.Deg2Rad;
            float baseRadius = length * Mathf.Tan(halfRad);
            int segments = 16;

            // Apex marker.
            BuildSphereWire(root, 0.15f, color, "ConeApex");

            // Base circle
            var baseCircle = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float a = (float)i / segments * Mathf.PI * 2f;
                baseCircle[i] = new Vector3(Mathf.Cos(a) * baseRadius, Mathf.Sin(a) * baseRadius, length);
            }
            AttachLineToRoot(root, "ConeBase", baseCircle, color, loop: true);

            // 8 spokes от apex к base
            int spokes = 8;
            for (int i = 0; i < spokes; i++)
            {
                float a = (float)i / spokes * Mathf.PI * 2f;
                var spoke = new Vector3[]
                {
                    Vector3.zero,
                    new Vector3(Mathf.Cos(a) * baseRadius, Mathf.Sin(a) * baseRadius, length)
                };
                AttachLineToRoot(root, "ConeSpoke" + i, spoke, color, loop: false);
            }
        }

        // === Sphere: 3 great circles ===
        private void BuildSphereWire(GameObject root, float radius, Color color, string label)
        {
            if (radius <= 0f) radius = 0.3f;
            int segments = 24;

            // XY plane (z = 0)
            var xy = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float a = (float)i / segments * Mathf.PI * 2f;
                xy[i] = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
            }
            AttachLineToRoot(root, label + "_XY", xy, color, loop: true);

            // XZ plane (y = 0)
            var xz = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float a = (float)i / segments * Mathf.PI * 2f;
                xz[i] = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            }
            AttachLineToRoot(root, label + "_XZ", xz, color, loop: true);

            // YZ plane (x = 0)
            var yz = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float a = (float)i / segments * Mathf.PI * 2f;
                yz[i] = new Vector3(0f, Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
            }
            AttachLineToRoot(root, label + "_YZ", yz, color, loop: true);
        }

        // === Line: rectangular box (length × width × 0.5м высота) ===
        private void BuildLineWire(GameObject root, float length, float width, Color color)
        {
            if (length <= 0f) return;
            float w = Mathf.Max(0.3f, width);
            float h = 0.5f;
            float z = length;

            var c = new Vector3[8];
            c[0] = new Vector3(-w * 0.5f, -h * 0.5f, 0f);
            c[1] = new Vector3( w * 0.5f, -h * 0.5f, 0f);
            c[2] = new Vector3( w * 0.5f,  h * 0.5f, 0f);
            c[3] = new Vector3(-w * 0.5f,  h * 0.5f, 0f);
            c[4] = new Vector3(-w * 0.5f, -h * 0.5f, z);
            c[5] = new Vector3( w * 0.5f, -h * 0.5f, z);
            c[6] = new Vector3( w * 0.5f,  h * 0.5f, z);
            c[7] = new Vector3(-w * 0.5f,  h * 0.5f, z);

            int[,] edges = new int[,]
            {
                {0,1},{1,2},{2,3},{3,0},
                {4,5},{5,6},{6,7},{7,4},
                {0,4},{1,5},{2,6},{3,7}
            };
            for (int i = 0; i < edges.GetLength(0); i++)
            {
                var line = new Vector3[] { c[edges[i, 0]], c[edges[i, 1]] };
                AttachLineToRoot(root, "LineEdge" + i, line, color, loop: false);
            }
        }

        // === Box: wire-cube (length × width × width) ===
        private void BuildBoxWire(GameObject root, float length, float width, Color color)
        {
            if (length <= 0f) return;
            float w = Mathf.Max(0.3f, width);
            float z = length;

            var c = new Vector3[8];
            c[0] = new Vector3(-w * 0.5f, -w * 0.5f, 0f);
            c[1] = new Vector3( w * 0.5f, -w * 0.5f, 0f);
            c[2] = new Vector3( w * 0.5f,  w * 0.5f, 0f);
            c[3] = new Vector3(-w * 0.5f,  w * 0.5f, 0f);
            c[4] = new Vector3(-w * 0.5f, -w * 0.5f, z);
            c[5] = new Vector3( w * 0.5f, -w * 0.5f, z);
            c[6] = new Vector3( w * 0.5f,  w * 0.5f, z);
            c[7] = new Vector3(-w * 0.5f,  w * 0.5f, z);

            int[,] edges = new int[,]
            {
                {0,1},{1,2},{2,3},{3,0},
                {4,5},{5,6},{6,7},{7,4},
                {0,4},{1,5},{2,6},{3,7}
            };
            for (int i = 0; i < edges.GetLength(0); i++)
            {
                var line = new Vector3[] { c[edges[i, 0]], c[edges[i, 1]] };
                AttachLineToRoot(root, "BoxEdge" + i, line, color, loop: false);
            }
        }
#endif

        // === Internal struct ===
        private class AoeWireframe
        {
            public GameObject root;
            public float elapsed;
            public float duration;
            public Color startColor;
        }
    }
}
