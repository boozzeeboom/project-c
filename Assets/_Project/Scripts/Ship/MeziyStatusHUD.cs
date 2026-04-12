using UnityEngine;
using System.Collections.Generic;

namespace ProjectC.Ship
{
    /// <summary>
    /// MeziyStatusHUD — HUD overlay для отображения состояния мезиевых модулей.
    /// Сессия 5_4: UI, Thrust Module, и Полировка.
    ///
    /// Отображает:
    /// - Статус каждого модуля: 🟢 Passive | 🔵 Active | 🔴 Overheated
    /// - Прогресс-бар перегрева (0-10 сек до перегрева)
    /// - Прогресс-бар кулдауна (15 сек → 0)
    /// - Текущий уровень топлива
    ///
    /// Управление: F4 toggle
    /// Позиция: bottom-right (не пересекается с ShipDebugHUD top-left)
    /// </summary>
    public class MeziyStatusHUD : MonoBehaviour
    {
        [Header("Настройки")]
        [Tooltip("Включить HUD при старте")]
        [SerializeField] private bool enabledByDefault = true;

        [Tooltip("Размер шрифта")]
        [SerializeField] private int fontSize = 13;

        [Tooltip("Ссылка на MeziyModuleActivator (автопоиск если null)")]
        [SerializeField] private MeziyModuleActivator meziyActivator;

        [Tooltip("Ссылка на ShipFuelSystem (автопоиск если null)")]
        [SerializeField] private ShipFuelSystem fuelSystem;

        // Состояние
        private bool _visible;
        private GUIStyle _style;
        private Rect _rect;
        private Texture2D _bgTex;
        private Texture2D _greenBar;
        private Texture2D _blueBar;
        private Texture2D _redBar;
        private Texture2D _yellowBar;
        private Texture2D _fuelBar;

        // Модули для отображения (порядок имеет значение)
        private readonly string[] _moduleIds = new[]
        {
            "MODULE_MEZIY_PITCH",
            "MODULE_MEZIY_ROLL",
            "MODULE_MEZIY_YAW",
            "MODULE_MEZIY_THRUST"
        };

        private readonly string[] _moduleNames = new[]
        {
            "PITCH",
            "ROLL",
            "YAW",
            "THRUST"
        };

        private void Awake()
        {
            _visible = enabledByDefault;
        }

        private void Update()
        {
            // F4 toggle
            bool f4Pressed = false;
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null)
                f4Pressed = UnityEngine.InputSystem.Keyboard.current.f4Key.wasPressedThisFrame;
#else
            f4Pressed = Input.GetKeyDown(KeyCode.F4);
#endif
            if (f4Pressed)
            {
                _visible = !_visible;
                Debug.Log($"[MeziyStatusHUD] Visible: {_visible}");
            }
        }

        private void OnGUI()
        {
            if (!_visible) return;

            SetupStyle();
            CacheReferences();

            string text = BuildStatusText();
            if (string.IsNullOrEmpty(text)) return;

            // Рассчитываем высоту
            var content = new GUIContent(text);
            float height = _style.CalcHeight(content, 300) + 20;

            // Позиция: bottom-right
            var bgRect = new Rect(Screen.width - 320, Screen.height - height - 20, 310, height);

            // Фон
            GUI.DrawTexture(bgRect, GetBgTex());

            // Рамка
            DrawBorder(bgRect);

            // Текст
            var textRect = new Rect(bgRect.x + 10, bgRect.y + 6, bgRect.width - 20, bgRect.height - 12);
            GUI.Label(textRect, text, _style);
        }

        private void CacheReferences()
        {
            if (meziyActivator == null)
            {
                var activators = FindObjectsByType<MeziyModuleActivator>(FindObjectsInactive.Exclude);
                if (activators.Length > 0) meziyActivator = activators[0];
            }

            if (fuelSystem == null)
            {
                var fuels = FindObjectsByType<ShipFuelSystem>(FindObjectsInactive.Exclude);
                if (fuels.Length > 0) fuelSystem = fuels[0];
            }
        }

        private string BuildStatusText()
        {
            if (meziyActivator == null) return "<color=red>Meziy Activator: NOT FOUND</color>";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<b>⚡ MEZIY STATUS</b>");

            // Топливо
            if (fuelSystem != null)
            {
                float fuelPercent = fuelSystem.FuelPercent;
                string fuelColor = fuelPercent > 0.5f ? "lime" : (fuelPercent > 0.2f ? "yellow" : "red");
                sb.AppendLine($"<color={fuelColor}>Fuel: {fuelPercent * 100:F0}%</color>");

                // Fuel bar
                string fuelBar = BuildProgressBar(fuelPercent, 20, GetFuelBarTex());
                sb.AppendLine(fuelBar);
            }

            sb.AppendLine("---");

            // Статус каждого модуля
            var states = meziyActivator.GetActiveStates();

            for (int i = 0; i < _moduleIds.Length; i++)
            {
                string moduleId = _moduleIds[i];
                string moduleName = _moduleNames[i];

                if (meziyActivator.IsModuleInstalled(moduleId))
                {
                    var state = meziyActivator.GetState(moduleId);
                    if (state == null) continue;

                    // Статус индикатор
                    string statusIcon;
                    string statusColor;
                    if (state.isOnCooldown)
                    {
                        statusIcon = "🔴";
                        statusColor = "red";
                    }
                    else if (state.isActive)
                    {
                        statusIcon = "🔵";
                        statusColor = "cyan";
                    }
                    else
                    {
                        statusIcon = "🟢";
                        statusColor = "lime";
                    }

                    sb.Append($"{statusIcon} <color={statusColor}>{moduleName}</color>");

                    // Прогресс-бар перегрева (если активен)
                    if (state.isActive)
                    {
                        float overheatProgress = meziyActivator.GetOverheatProgress(moduleId);
                        sb.AppendLine($" ACTIVE {overheatProgress * 100:F0}%");
                        string bar = BuildProgressBar(overheatProgress, 18, GetRedBarTex());
                        sb.AppendLine(bar);
                    }
                    // Прогресс-бар кулдауна (если на cooldown)
                    else if (state.isOnCooldown)
                    {
                        // cooldownRemaining → progress (1.0 = только начал остывать, 0.0 = готов)
                        float cooldownProgress = state.cooldownRemaining / 15f; // 15s = полный кулдаун
                        sb.AppendLine($" COOL {state.cooldownRemaining:F1}s");
                        string bar = BuildProgressBar(cooldownProgress, 18, GetYellowBarTex());
                        sb.AppendLine(bar);
                    }
                    else
                    {
                        sb.AppendLine(" READY");
                    }
                }
                else
                {
                    sb.AppendLine($"⬜ <color=gray>{moduleName}</color> NOT INSTALLED");
                }
            }

            sb.AppendLine("---");
            sb.AppendLine("[F4] Toggle HUD");

            return sb.ToString();
        }

        private string BuildProgressBar(float progress, int width, Texture2D tex)
        {
            progress = Mathf.Clamp01(progress);
            int filledWidth = Mathf.RoundToInt(width * progress);
            int emptyWidth = width - filledWidth;

            // Юникод блоки для прогресс-бара
            string filled = new string('█', Mathf.Max(0, filledWidth / 2));
            string empty = new string('░', Mathf.Max(0, emptyWidth / 2));

            return $"<color=gray>[</color>{filled}{empty}<color=gray>]</color>";
        }

        private void SetupStyle()
        {
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label);
                _style.fontSize = fontSize;
                _style.normal.textColor = Color.white;
                _style.fontStyle = FontStyle.Normal;
                _style.richText = true;
                _style.wordWrap = true;
                _style.alignment = TextAnchor.UpperLeft;
            }
        }

        private Texture2D GetBgTex()
        {
            if (_bgTex == null)
            {
                _bgTex = new Texture2D(2, 2);
                _bgTex.hideFlags = HideFlags.HideAndDontSave;
                Color[] pixels = new Color[4];
                for (int i = 0; i < 4; i++) pixels[i] = new Color(0, 0, 0, 0.85f);
                _bgTex.SetPixels(pixels);
                _bgTex.Apply();
            }
            return _bgTex;
        }

        private Texture2D GetGreenBarTex()
        {
            if (_greenBar == null) _greenBar = CreateColoredTex(Color.green);
            return _greenBar;
        }

        private Texture2D GetBlueBarTex()
        {
            if (_blueBar == null) _blueBar = CreateColoredTex(Color.cyan);
            return _blueBar;
        }

        private Texture2D GetRedBarTex()
        {
            if (_redBar == null) _redBar = CreateColoredTex(Color.red);
            return _redBar;
        }

        private Texture2D GetYellowBarTex()
        {
            if (_yellowBar == null) _yellowBar = CreateColoredTex(Color.yellow);
            return _yellowBar;
        }

        private Texture2D GetFuelBarTex()
        {
            if (_fuelBar == null) _fuelBar = CreateColoredTex(new Color(0.2f, 0.8f, 0.2f));
            return _fuelBar;
        }

        private Texture2D CreateColoredTex(Color col)
        {
            var tex = new Texture2D(2, 2);
            tex.hideFlags = HideFlags.HideAndDontSave;
            Color[] pixels = new Color[4];
            for (int i = 0; i < 4; i++) pixels[i] = col;
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private void DrawBorder(Rect rect)
        {
            Color borderColor = new Color(0, 0.6f, 0, 1f); // зелёная рамка
            var borderTex = CreateColoredTex(borderColor);
            float thickness = 2f;

            // Top
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), borderTex);
            // Bottom
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), borderTex);
            // Left
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), borderTex);
            // Right
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), borderTex);
        }
    }
}
