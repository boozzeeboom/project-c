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
        private Texture2D _bgTex;

        private void Awake()
        {
            _ship = GetComponent<ShipController>();
            _fuelSystem = GetComponent<ShipFuelSystem>();
            _visible = enabledByDefault;
        }

        private void Update()
        {
            // F3 toggle -- поддержка и Input System и Old Input Manager
            bool f3Pressed = false;
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null)
                f3Pressed = UnityEngine.InputSystem.Keyboard.current.f3Key.wasPressedThisFrame;
#else
            f3Pressed = Input.GetKeyDown(KeyCode.F3);
#endif
            if (f3Pressed)
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

            // Фон -- полупрозрачный чёрный прямоугольник
            var bgRect = _rect;
            bgRect.width = 380;
            bgRect.height = _style.CalcHeight(new GUIContent(text), 380) + 16;

            // Рисуем фон
            GUI.DrawTexture(bgRect, MakeTex(2, 2, new Color(0, 0, 0, 0.8f)));
            // Рамка
            GUI.DrawTexture(new Rect(bgRect.x, bgRect.y, bgRect.width, 2), MakeTex(2, 2, Color.green));
            GUI.DrawTexture(new Rect(bgRect.x, bgRect.yMax - 2, bgRect.width, 2), MakeTex(2, 2, Color.green));
            GUI.DrawTexture(new Rect(bgRect.x, bgRect.y, 2, bgRect.height), MakeTex(2, 2, Color.green));
            GUI.DrawTexture(new Rect(bgRect.xMax - 2, bgRect.y, 2, bgRect.height), MakeTex(2, 2, Color.green));

            // Текст со сдвигом внутрь
            var textRect = _rect;
            textRect.x += 10;
            textRect.y += 6;
            textRect.width -= 20;
            GUI.Label(textRect, text, _style);
        }

        private Texture2D MakeTex(int w, int h, Color col)
        {
            if (_bgTex == null)
            {
                _bgTex = new Texture2D(w, h);
                _bgTex.hideFlags = HideFlags.HideAndDontSave;
            }
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    _bgTex.SetPixel(x, y, col);
            _bgTex.Apply();
            return _bgTex;
        }

        private void SetupStyle()
        {
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label);
                _style.fontSize = fontSize;
                _style.normal.textColor = Color.green;
                _style.fontStyle = FontStyle.Bold;
                _style.richText = true;
            }

            int w = 350;
            int x = position switch
            {
                1 => Screen.width - w - 10,
                2 => 10,
                3 => Screen.width - w - 10,
                _ => 10
            };
            int y = position switch
            {
                2 or 3 => Screen.height - 360 - 10,
                _ => 10
            };

            _rect = new Rect(x, y, w, 360);
        }

        private string BuildDebugText()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<b>=== SHIP DEBUG ===</b>");

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

            // Meziy state (continuous mode)
            var activator = GetMeziyActivator();
            if (activator != null)
            {
                var states = activator.GetActiveStates();
                sb.AppendLine($"Meziy Active: {activator.GetActiveCount()}");
                foreach (var kvp in states)
                {
                    var state = kvp.Value;
                    string status = state.isActive ? "ACTIVE" : (state.isOnCooldown ? $"COOL({state.cooldownRemaining:F1}s)" : "READY");
                    sb.AppendLine($"  {kvp.Key}: {status}");
                    if (state.isActive)
                    {
                        sb.AppendLine($"    time: {state.continuousActiveTime:F1}/{state.overheatThreshold:F0}s");
                    }
                }
            }
            else
            {
                sb.AppendLine("Meziy Activator: N/A");
            }

            sb.AppendLine("[F3] Toggle HUD");

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
            var activators = FindObjectsByType<MeziyModuleActivator>(FindObjectsInactive.Exclude);
            return activators.Length > 0 ? activators[0] : null;
        }

        private ShipModuleManager GetModuleManager()
        {
            var managers = FindObjectsByType<ShipModuleManager>(FindObjectsInactive.Exclude);
            return managers.Length > 0 ? managers[0] : null;
        }
    }
}
