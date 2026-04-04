using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectC.Items
{
    /// <summary>
    /// Круговой инвентарь (GTA-стиль).
    /// 8 секторов = 8 типов предметов.
    /// Tab — открыть/закрыть.
    /// Наведение мыши подсвечивает сектор.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        [Header("Настройки колеса")]
        [Tooltip("Радиус круга")]
        [SerializeField] private float wheelRadius = 210f; // Увеличен на 40% (было 150)

        [Tooltip("Радиус центрального отверстия")]
        [SerializeField] private float innerRadius = 70f;

        [Tooltip("Позиция центра на экране (0,0 = центр экрана)")]
        [SerializeField] private Vector2 screenCenter = new Vector2(0.5f, 0.5f);

        [Header("Цвета")]
        [SerializeField] private Color emptyColor = new Color(0.2f, 0.2f, 0.2f, 0.7f);
        [SerializeField] private Color hasItemsColor = new Color(0.3f, 0.5f, 0.3f, 0.8f);
        [SerializeField] private Color hoverColor = new Color(0.9f, 0.8f, 0.2f, 0.9f);

        [Header("Текст")]
        [SerializeField] private Color textColor = Color.white;
        [SerializeField] private float fontSize = 14f;

        private bool _isOpen = false;
        private int _hoveredSector = -1;

        // Material для GL
        private static Material _lineMaterial;

        // Ввод
        private InputAction _toggleAction;
        private InputAction _mousePosAction;

        private void Awake()
        {
            _toggleAction = new InputAction("ToggleInventory", binding: "<Keyboard>/tab", expectedControlType: "Button");
            _mousePosAction = new InputAction("MousePosition", binding: "<Mouse>/position");
        }

        private void OnEnable()
        {
            _toggleAction.Enable();
            _toggleAction.performed += ctx => ToggleInventory();
            _mousePosAction.Enable();
        }

        private void OnDisable()
        {
            _toggleAction.Disable();
            _toggleAction.performed -= ctx => ToggleInventory();
            _mousePosAction.Disable();
        }

        private void Update()
        {
            if (_isOpen)
            {
                UpdateHover();
            }
        }

        private void ToggleInventory()
        {
            _isOpen = !_isOpen;
            Debug.Log($"[InventoryUI] Инвентарь: {(_isOpen ? "открыт" : "закрыт")}");
        }

        private void UpdateHover()
        {
            Vector2 mousePos = _mousePosAction.ReadValue<Vector2>();
            Vector2 center = new Vector2(Screen.width * screenCenter.x, Screen.height * screenCenter.y);
            Vector2 dir = mousePos - center;
            float dist = dir.magnitude;

            if (dist >= innerRadius && dist <= wheelRadius)
            {
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

                // Сектора по часовой от верха: 0=верх(90°), 1=верх-право(45°), 2=право(0°)...
                // Корректируем: верх = 0, по часовой = уменьшение индекса
                float adjustedAngle = (90 - angle + 360) % 360;
                _hoveredSector = Mathf.FloorToInt(adjustedAngle / 45f) % 8;
            }
            else
            {
                _hoveredSector = -1;
            }
        }

        private void OnGUI()
        {
            if (!_isOpen) return;

            Vector2 center = new Vector2(Screen.width * screenCenter.x, Screen.height * screenCenter.y);

            // Сначала рисуем все GL секторы
            for (int i = 0; i < 8; i++)
            {
                ItemType type = (ItemType)i;
                bool hasItems = Inventory.Instance != null && Inventory.Instance.HasItemsInType(type);
                bool isHovered = (i == _hoveredSector);

                Color sectorColor = hasItems ? hasItemsColor : emptyColor;
                if (isHovered) sectorColor = hoverColor;

                DrawSectorFill(center, i, sectorColor);
            }

            // Сбрасываем цвет после GL операций
            GUI.color = Color.white;

            // Потом рисуем текстовые метки
            for (int i = 0; i < 8; i++)
            {
                // Отображаемый номер типа должен соответствовать типу для подсчёта
                int displayNum = ((4 - i + 8) % 8) + 1;
                ItemType type = (ItemType)(displayNum - 1);
                bool hasItems = Inventory.Instance != null && Inventory.Instance.HasItemsInType(type);

                DrawSectorText(center, i, type, hasItems);
            }

            // Центральный текст
            GUI.color = Color.white;
            GUIStyle centerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = (int)fontSize,
                alignment = TextAnchor.MiddleCenter,
                normal = new GUIStyleState { textColor = Color.white }
            };
            GUI.Label(new Rect(center.x - 40, center.y - 10, 80, 20), "Инвентарь", centerStyle);
        }

        /// <summary>
        /// Рисует заполненный сектор (GL операции)
        /// </summary>
        private void DrawSectorFill(Vector2 center, int index, Color color)
        {
            float startAngle = 67.5f - index * 45f;
            float endAngle = 112.5f - index * 45f;

            int segments = 16;
            Vector3[] vertices = new Vector3[segments + 2];
            vertices[0] = new Vector3(center.x, center.y, 0);

            for (int i = 0; i <= segments; i++)
            {
                float angle = startAngle + (endAngle - startAngle) * (i / (float)segments);
                float rad = angle * Mathf.Deg2Rad;
                vertices[i + 1] = new Vector3(
                    center.x + Mathf.Cos(rad) * wheelRadius,
                    center.y + Mathf.Sin(rad) * wheelRadius,
                    0
                );
            }

            DrawFilledFan(vertices, color);
            DrawOutline(vertices);
        }

        /// <summary>
        /// Рисует текстовую метку сектора (GUI операции)
        /// </summary>
        private void DrawSectorText(Vector2 center, int index, ItemType type, bool hasItems)
        {
            // Обводка текста (для читаемости на тёмном фоне)
            float startAngle = 67.5f - index * 45f;
            float endAngle = 112.5f - index * 45f;
            float midAngle = (startAngle + endAngle) / 2 * Mathf.Deg2Rad;
            Vector2 textPos = center + new Vector2(Mathf.Cos(midAngle), Mathf.Sin(midAngle)) * (wheelRadius * 0.65f);

            int displayNum = ((4 - index + 8) % 8) + 1;
            string label = hasItems ? $"Тип {displayNum}\n[{Inventory.Instance.GetCountByType(type)}]" : $"Тип {displayNum}";

            GUIStyle style = new GUIStyle();
            style.alignment = TextAnchor.MiddleCenter;
            style.fontSize = (int)fontSize;
            style.normal.textColor = Color.white;

            // Явно белый с полной непрозрачностью
            GUI.color = new Color(1f, 1f, 1f, 1f);
            GUI.Label(new Rect(textPos.x - 30, textPos.y - 15, 60, 30), label, style);
        }

        private void DrawFilledFan(Vector3[] vertices, Color color)
        {
            if (_lineMaterial == null)
            {
                _lineMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
                _lineMaterial.hideFlags = HideFlags.HideAndDontSave;
                _lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                _lineMaterial.SetInt("_ZWrite", 0);
            }

            _lineMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix();

            // Рисуем треугольники от центра (GL.TRIANGLES — рисует каждый triplet как отдельный треугольник)
            GL.Begin(GL.TRIANGLES);
            GL.Color(color);
            for (int i = 1; i < vertices.Length - 1; i++)
            {
                GL.Vertex(vertices[0]);       // центр
                GL.Vertex(vertices[i]);       // точка i
                GL.Vertex(vertices[i + 1]);   // точка i+1
            }
            GL.End();

            GL.PopMatrix();
        }

        private void DrawOutline(Vector3[] vertices)
        {
            if (_lineMaterial == null)
            {
                _lineMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
                _lineMaterial.hideFlags = HideFlags.HideAndDontSave;
            }

            _lineMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix();

            // Рисуем контур линиями
            GL.Begin(GL.LINES);
            GL.Color(Color.white);
            for (int i = 1; i < vertices.Length; i++)
            {
                GL.Vertex(vertices[i]);
                GL.Vertex(vertices[i + 1 < vertices.Length ? i + 1 : 1]);
            }
            GL.End();

            GL.PopMatrix();
        }

        private void DrawLine(Vector2 start, Vector2 end, float thickness)
        {
            Vector2 dir = end - start;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            GUIUtility.RotateAroundPivot(angle, start);
            GUI.DrawTexture(new Rect(start.x, start.y, dir.magnitude, thickness), Texture2D.whiteTexture);
            GUIUtility.RotateAroundPivot(-angle, start);
        }
    }
}
