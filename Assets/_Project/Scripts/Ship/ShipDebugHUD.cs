using UnityEngine;
using ProjectC.Player;

namespace ProjectC.Ship
{
    /// <summary>
    /// Debug HUD для отладки состояния корабля.
    /// Включается по F3. Показывает fuel, thrust, roll, meziy state.
    /// Сессия 5_2: Добавлен для диагностики.
    /// </summary>
    [RequireComponent(typeof(ShipController))]
    public class ShipDebugHUD : MonoBehaviour
    {
        [Header("Настройки")]
        [Tooltip("Включить HUD при старте")]
        [SerializeField] private bool enabledByDefault = false;

        [Tooltip("Размер шрифта")]
        [SerializeField] private int fontSize = 14;

        [Tooltip("Позиция HUD (0=top-left, 1=top-right, 2=bottom-left, 3=bottom-right)")]
        [SerializeField] private int position = 0;

        private ShipController _ship;
        private ShipFuelSystem _fuelSystem;
        private bool _visible;

        // GUI
        private GUIStyle _style;
        private Rect _rect;

        private void Awake()
        {
            _ship = GetComponent<ShipController>();
            _fuelSystem = GetComponent<ShipFuelSystem>();
            _visible = enabledByDefault;
        }

        private void Update()
        {
            // F3 toggle
            if (Input.GetKeyDown(KeyCode.F3))
            {
                _visible = !_visible;
                Debug.Log($"[ShipDebugHUD] Visible: {_visible}");
            }
        }

        private void OnGUI()
        {
            if (!_visible) return;

            SetupStyle();

            string text = BuildDebugText();
            GUILayout.BeginArea(_rect);
            GUILayout.Label(text, _style);
            GUILayout.EndArea();
        }

        private void SetupStyle()
        {
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label);
                _style.fontSize = fontSize;
                _style.normal.textColor = Color.cyan;
                _style.fontStyle = FontStyle.Bold;
            }

            int w = 320, h = 340;
            int x = position switch
            {
                1 => Screen.width - w - 10,
                2 => 10,
                3 => Screen.width - w - 10,
                _ => 10
            };
            int y = position switch
            {
                2 or 3 => Screen.height - h - 10,
                _ => 10
            };

            _rect = new Rect(x, y, w, h);
        }

        private string BuildDebugText()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<b>═══ SHIP DEBUG ═══</b>");

            // Fuel
            if (_fuelSystem != null)
            {
                sb.AppendLine($"Fuel: {_fuelSystem.CurrentFuel:F1}/{_fuelSystem.MaxFuel:F0} ({_fuelSystem.FuelPercent * 100:F0}%)");
                sb.AppendLine($"Refueling: {_fuelSystem.isRefueling}");
            }
            else
            {
                sb.AppendLine("Fuel: N/A");
            }

            // Thrust & Speed
            sb.AppendLine($"Speed: {_ship.CurrentSpeed:F1} m/s");

            // Module state
            sb.AppendLine($"Roll Unlocked: {IsRollUnlocked()}");

            // Meziy state
            var activator = GetMeziyActivator();
            if (activator != null)
            {
                var effects = activator.GetActiveEffects();
                sb.AppendLine($"Meziy Active: {effects.Count}");
                foreach (var kvp in effects)
                {
                    if (kvp.Value.isActive)
                        sb.AppendLine($"  → {kvp.Key}");
                }
            }
            else
            {
                sb.AppendLine("Meziy Activator: N/A");
            }

            sb.AppendLine($"[F3] Toggle HUD");

            return sb.ToString();
        }

        private bool IsRollUnlocked()
        {
            var moduleManager = GetModuleManager();
            if (moduleManager == null) return false;

            foreach (var slot in moduleManager.slots)
            {
                if (slot != null && slot.isOccupied && slot.installedModule.moduleId == "MODULE_ROLL")
                    return true;
            }
            return false;
        }

        private MeziyModuleActivator GetMeziyActivator()
        {
            // Попробуем получить через рефлексию (meziyActivator — private поле)
            // Или найдём на сцене
            var activators = FindObjectsByType<MeziyModuleActivator>(FindObjectsSortMode.None);
            return activators.Length > 0 ? activators[0] : null;
        }

        private ShipModuleManager GetModuleManager()
        {
            var managers = FindObjectsByType<ShipModuleManager>(FindObjectsSortMode.None);
            return managers.Length > 0 ? managers[0] : null;
        }
    }
}
