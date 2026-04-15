using UnityEngine;
using UnityEngine.InputSystem;
using ProjectC.UI;

namespace ProjectC.Items
{
    /// <summary>
    /// Круговой инвентарь (GTA-стиль).
    /// 8 секторов = 8 типов предметов (Ресурсы, Оборудование, Еда, Топливо, Антигравий, Мезий, Медикаменты, Техника).
    /// Tab — открыть/закрыть.
    /// Наведение мыши подсвечивает сектор.
    /// При >1 предмете в секторе — подсписок.
    /// Каждый экземпляр привязан к своему Inventory (на NetworkPlayer).
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        [Header("Ссылки")]
        [SerializeField] private Inventory inventory;

        /// <summary>
        /// REFACTORED (R3-001): Установка инвентаря без reflection.
        /// Заменяет reflection-код в NetworkPlayer.SpawnInventory().
        /// </summary>
        public void SetInventory(Inventory inv)
        {
            inventory = inv;
        }

        [Header("Настройки колеса")]
        [SerializeField] private float wheelRadius = 210f;

        [SerializeField] private float innerRadius = 70f;

        [Header("Цвета")]
        [SerializeField] private Color emptyColor = new Color(0.2f, 0.2f, 0.2f, 0.7f);
        [SerializeField] private Color hasItemsColor = new Color(0.3f, 0.5f, 0.3f, 0.8f);
        [SerializeField] private Color hoverColor = new Color(0.9f, 0.8f, 0.2f, 0.9f);

        private bool _isOpen = false;
        private int _hoveredSector = -1;

        private static Material _glMaterial;

        private InputAction _toggleAction;
        private InputAction _mousePosAction;
        private System.Action<InputAction.CallbackContext> _onTogglePerformed;

        // Анимация получения предметов (вспышка секторов)
        private float _flashTimer = 0f;
        private const float _flashDuration = 0.6f;
        private bool[] _flashingSectors = new bool[8];

        // Углы секторов: index 0 = верх, по часовой стрелке (стандартная математика)
        private static readonly float[] _sectorMidAngles = new float[]
        {
            90f,    // 0: верх     — Ресурсы
            45f,    // 1: верх-право  — Оборудование
            0f,     // 2: право    — Еда
            -45f,   // 3: низ-право  — Топливо
            -90f,   // 4: низ      — Антигравий
            -135f,  // 5: низ-лево  — Мезий
            180f,   // 6: лево     — Медикаменты
            135f,   // 7: верх-лево  — Техника
        };

        private void Awake()
        {
            _toggleAction = new InputAction("ToggleInventory", binding: "<Keyboard>/tab", expectedControlType: "Button");
            _mousePosAction = new InputAction("MousePosition", binding: "<Mouse>/position");
            _onTogglePerformed = _ => ToggleInventory();

            // REFACTORED: Pre-allocate GL material once in Awake instead of lazy creation in Draw methods
            // This eliminates allocations in hot path (OnGUI)
            if (_glMaterial == null)
            {
                _glMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
                _glMaterial.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        private void OnEnable()
        {
            _toggleAction.Enable();
            _toggleAction.performed += _onTogglePerformed;
            _mousePosAction.Enable();
        }

        private void OnDisable()
        {
            _toggleAction.Disable();
            _toggleAction.performed -= _onTogglePerformed;
            _mousePosAction.Disable();
        }

        private void OnDestroy()
        {
            // Cleanup InputActions
            _toggleAction.Disable();
            _toggleAction.performed -= _onTogglePerformed;
            _toggleAction.Dispose();
            _mousePosAction.Dispose();

            // Cleanup GL material (prevent leak)
            if (_glMaterial != null)
            {
                Destroy(_glMaterial);
                _glMaterial = null;
            }
        }

        private void Update()
        {
            if (_isOpen)
                UpdateHover();

            // Обновляем анимацию вспышки
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                if (_flashTimer <= 0f)
                {
                    _flashTimer = 0f;
                    for (int i = 0; i < 8; i++)
                        _flashingSectors[i] = false;
                }
            }
        }

        private void ToggleInventory()
        {
            _isOpen = !_isOpen;

            if (_isOpen)
            {
                UIManager.EnsureExists().OpenPanel("InventoryUI", 400, OnInventoryPanelClosed, gameObject);
            }
            else
            {
                UIManager.Instance?.ClosePanel("InventoryUI");
            }
        }

        /// <summary>
        /// Callback при закрытии панели инвентаря (вызывается из UIManager)
        /// </summary>
        private void OnInventoryPanelClosed()
        {
            _isOpen = false;
            _hoveredSector = -1;
            Debug.Log("[InventoryUI] Панель инвентаря закрыта через UIManager");
        }

        /// <summary>
        /// Запустить анимацию вспышки секторов при получении предметов.
        /// Вызывается после открытия сундука.
        /// </summary>
        public void TriggerSectorFlash()
        {
            _flashTimer = _flashDuration;

            // Помечаем все непустые секторы
            if (inventory != null)
            {
                for (int i = 0; i < 8; i++)
                {
                    ItemType type = (ItemType)i;
                    _flashingSectors[i] = inventory.HasItemsInType(type);
                }
            }
        }

        private void UpdateHover()
        {
            Vector2 mousePos = _mousePosAction.ReadValue<Vector2>();
            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 dir = mousePos - center;
            float dist = dir.magnitude;

            if (dist >= innerRadius && dist <= wheelRadius)
            {
                // Угол мыши (0° = вправо, 90° = вверх в стандартной математике)
                // Но в экранных координатах Y вниз, поэтому инвертируем
                float angle = Mathf.Atan2(-dir.y, dir.x) * Mathf.Rad2Deg;

                // Сектор i охватывает углы от (midAngle - 22.5) до (midAngle + 22.5)
                float bestDiff = float.MaxValue;
                _hoveredSector = -1;

                for (int i = 0; i < 8; i++)
                {
                    float diff = Mathf.DeltaAngle(angle, _sectorMidAngles[i]);
                    if (Mathf.Abs(diff) < 22.5f && Mathf.Abs(diff) < bestDiff)
                    {
                        bestDiff = Mathf.Abs(diff);
                        _hoveredSector = i;
                    }
                }
            }
            else
            {
                _hoveredSector = -1;
            }
        }

        private void OnGUI()
        {
            if (!_isOpen) return;

            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            // 1. Рисуем все секторы (GL)
            for (int i = 0; i < 8; i++)
            {
                ItemType type = (ItemType)i;
                bool hasItems = inventory != null && inventory.HasItemsInType(type);
                bool isHovered = (i == _hoveredSector);

                Color sectorColor = hasItems ? hasItemsColor : emptyColor;
                if (isHovered) sectorColor = hoverColor;

                DrawSector(center, i, sectorColor);
            }

            // 2. Рисуем текст и подсписки (GUI)
            GUI.color = Color.white;

            for (int i = 0; i < 8; i++)
            {
                ItemType type = (ItemType)i;
                bool hasItems = inventory != null && inventory.HasItemsInType(type);
                bool isHovered = (i == _hoveredSector);

                // Текст сектора
                DrawSectorLabel(center, i, type, hasItems);

                // Подсписок при наведении
                if (isHovered && inventory != null && inventory.GetCountByType(type) > 1)
                {
                    DrawSublist(center, i, type);
                }
            }

            // Центр
            GUI.color = Color.white;
            GUIStyle centerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter,
                normal = new GUIStyleState { textColor = Color.white }
            };
            GUI.Label(new Rect(center.x - 40, center.y - 10, 80, 20), "Инвентарь", centerStyle);
        }

        private void DrawSector(Vector2 center, int index, Color color)
        {
            float midAngle = _sectorMidAngles[index];
            // Инвертируем угол для рендера (экранная Y направлена вниз)
            float startAngle = -midAngle + 22.5f;
            float endAngle = -midAngle - 22.5f;

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

            // Если сектор мерцает — усиливаем цвет
            Color finalColor = color;
            if (_flashTimer > 0f && _flashingSectors[index])
            {
                float t = _flashTimer / _flashDuration; // 1→0
                Color flashColor = Color.Lerp(hasItemsColor, new Color(0.5f, 0.9f, 0.5f, 0.95f), t);
                finalColor = Color.Lerp(color, flashColor, t);
            }

            DrawFilledFan(vertices, finalColor);
            DrawOutline(vertices);
        }

        private void DrawSectorLabel(Vector2 center, int index, ItemType type, bool hasItems)
        {
            float midAngle = _sectorMidAngles[index];
            float rad = midAngle * Mathf.Deg2Rad;
            Vector2 textPos = center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * (wheelRadius * 0.65f);

            string typeName = ItemTypeNames.GetDisplayName(type);
            string label = hasItems ? $"{typeName}\n[{inventory.GetCountByType(type)}]" : typeName;

            GUIStyle style = new GUIStyle();
            style.alignment = TextAnchor.MiddleCenter;
            style.fontSize = 14;
            style.normal.textColor = Color.white;

            GUI.color = new Color(1f, 1f, 1f, 1f);
            GUI.Label(new Rect(textPos.x - 50, textPos.y - 15, 100, 30), label, style);
        }

        private void DrawSublist(Vector2 center, int index, ItemType type)
        {
            var items = inventory.GetItemsByType(type);
            if (items.Count <= 1) return;

            float midAngle = _sectorMidAngles[index];
            float rad = midAngle * Mathf.Deg2Rad;
            Vector2 listPos = center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * (wheelRadius + 40f);

            float boxWidth = 130f;
            float boxHeight = items.Count * 18f + 10f;

            // Фон
            GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.92f);
            GUI.DrawTexture(new Rect(listPos.x - boxWidth / 2, listPos.y - boxHeight / 2, boxWidth, boxHeight), Texture2D.whiteTexture);

            // Обводка
            GUI.color = new Color(0.9f, 0.8f, 0.2f, 0.9f);
            GUI.DrawTexture(new Rect(listPos.x - boxWidth / 2, listPos.y - boxHeight / 2, boxWidth, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(listPos.x - boxWidth / 2, listPos.y + boxHeight / 2 - 1f, boxWidth, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(listPos.x - boxWidth / 2, listPos.y - boxHeight / 2, 1f, boxHeight), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(listPos.x + boxWidth / 2 - 1f, listPos.y - boxHeight / 2, 1f, boxHeight), Texture2D.whiteTexture);

            // Предметы
            GUI.color = Color.white;
            GUIStyle itemStyle = new GUIStyle();
            itemStyle.fontSize = 11;
            itemStyle.alignment = TextAnchor.MiddleLeft;
            itemStyle.normal.textColor = Color.white;

            for (int i = 0; i < items.Count; i++)
            {
                float y = listPos.y - boxHeight / 2 + 6f + i * 18f;
                string itemName = items[i] != null ? items[i].itemName : "(пусто)";
                GUI.Label(new Rect(listPos.x - boxWidth / 2 + 5f, y, boxWidth - 10f, 18f), $"• {itemName}", itemStyle);
            }
        }

        private void DrawFilledFan(Vector3[] vertices, Color color)
        {
            // REFACTORED: Material is pre-allocated in Awake, no null check needed
            _glMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix();

            GL.Begin(GL.TRIANGLES);
            GL.Color(color);
            for (int i = 1; i < vertices.Length - 1; i++)
            {
                GL.Vertex(vertices[0]);
                GL.Vertex(vertices[i]);
                GL.Vertex(vertices[i + 1]);
            }
            GL.End();

            GL.PopMatrix();
        }

        private void DrawOutline(Vector3[] vertices)
        {
            // REFACTORED: Material is pre-allocated in Awake, no null check needed
            _glMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix();

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
    }
}
