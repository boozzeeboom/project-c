using UnityEngine;

namespace ProjectC.World
{
    /// <summary>
    /// Роза ветров — маркер мирового севера. Ставится ОДИН раз в ключевой сцене
    /// (WorldScene_0_0, под корень WorldRoot_0_0): север = горизонтальная
    /// проекция синей стрелки (+Z) этого объекта. Крутите Y в редакторе, чтобы
    /// направить N на нужный город — все компасы (HUD корабля, будущий пеший)
    /// подхватят север автоматически через <see cref="WorldNorth"/>.
    ///
    /// В игре невидима (рендера нет, только гизмо в редакторе).
    /// Другим сценам своя роза НЕ нужна — ориентация сетки WorldScene_* общая.
    ///
    /// 🟢 Floating Origin: север задан ПОВОРОТОМ, а сдвиг мира — чистая
    /// трансляция без поворота. Роза едет со сценой бесплатно, хук сдвига
    /// не нужен, после F8/F9 север тот же.
    /// </summary>
    [DisallowMultipleComponent]
    public class CompassRose : MonoBehaviour
    {
        [Header("Гизмо (только редактор)")]
        [Tooltip("Длина стрелы севера в гизмо, метры.")]
        [SerializeField] private float _gizmoRadius = 30f;

        /// <summary>
        /// Направление на север: проекция +Z на горизонталь, нормировано.
        /// </summary>
        public Vector3 NorthDirection
        {
            get
            {
                Vector3 fwd = transform.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude < 0.0001f) return WorldNorth.DefaultNorth;
                return fwd.normalized;
            }
        }

        private void OnEnable() => WorldNorth.Register(this);

        private void OnDisable() => WorldNorth.Unregister(this);

        private void OnDrawGizmos()
        {
            Vector3 p = transform.position;
            Vector3 n = NorthDirection;
            Vector3 e = Quaternion.Euler(0f, 90f, 0f) * n;
            float r = Mathf.Max(1f, _gizmoRadius);

            // Север — длинная красная стрела
            Gizmos.color = Color.red;
            Gizmos.DrawLine(p, p + n * r);
            DrawArrowHead(p + n * r, n, r * 0.12f, Color.red);

            // E/S/W — короткие белые
            Gizmos.color = Color.white;
            Gizmos.DrawLine(p, p - n * r * 0.6f); // S
            Gizmos.DrawLine(p, p + e * r * 0.6f); // E
            Gizmos.DrawLine(p, p - e * r * 0.6f); // W

            // Кольцо-основание
            Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
            const int seg = 24;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg;
                float a1 = (i + 1) * Mathf.PI * 2f / seg;
                Vector3 d0 = n * Mathf.Cos(a0) + e * Mathf.Sin(a0);
                Vector3 d1 = n * Mathf.Cos(a1) + e * Mathf.Sin(a1);
                Gizmos.DrawLine(p + d0 * r * 0.6f, p + d1 * r * 0.6f);
            }

#if UNITY_EDITOR
            // Подписи румбов в Scene view
            var style = new GUIStyle { normal = { textColor = Color.red } };
            UnityEditor.Handles.Label(p + n * (r + 4f), "N", style);
            style = new GUIStyle { normal = { textColor = Color.white } };
            UnityEditor.Handles.Label(p - n * (r * 0.6f + 4f), "S", style);
            UnityEditor.Handles.Label(p + e * (r * 0.6f + 4f), "E", style);
            UnityEditor.Handles.Label(p - e * (r * 0.6f + 4f), "W", style);
#endif
        }

        private static void DrawArrowHead(Vector3 tip, Vector3 dir, float size, Color c)
        {
            Vector3 up = Vector3.up;
            Vector3 side = Vector3.Cross(dir, up).normalized * size;
            Gizmos.color = c;
            Gizmos.DrawLine(tip, tip - dir * size * 2f + side);
            Gizmos.DrawLine(tip, tip - dir * size * 2f - side);
        }

#if UNITY_EDITOR
        /// <summary>
        /// GameObject → Project C → Compass Rose (Север): создать розу в активной
        /// сцене. После создания перетащить под WorldRoot_0_0 и повернуть Y так,
        /// чтобы красная стрела смотрела на север.
        /// </summary>
        [UnityEditor.MenuItem("GameObject/Project C/Compass Rose (Север)", false, 10)]
        public static void CreateRoseMenu()
        {
            var go = new GameObject("CompassRose_North");
            go.AddComponent<CompassRose>();
            UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create Compass Rose");
            UnityEditor.Selection.activeGameObject = go;
            Debug.Log("[CompassRose] Создана роза севера. Перетащите под WorldRoot_0_0 " +
                      "и поверните Y: красная стрела (+Z) = север.");
        }
#endif
    }
}
