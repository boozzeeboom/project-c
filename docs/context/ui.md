# UI Context — Project C

**Теги:** `UI`, `HUD`, `Inventory`, `Trade`, `Canvas`, `TextMeshPro`

---

## 🎨 UI Система (Спринты 1-3 завершены)

### Архитектура

```
UIManager (централизованный)
├── TradeUI (торговля)
├── ContractBoardUI (контракты)
├── InventoryUI (круговое колесо)
├── NetworkUI (Disconnect/Reconnect)
├── AltitudeUI (HUD высоты)
└── ControlHintsUI (подсказки)
```

### Ключевые Файлы

| Файл | Назначение | Статус |
|------|------------|--------|
| `UIManager.cs` | Приоритеты, z-ordering, input | ✅ Новый |
| `UIFactory.cs` | Фабрика компонентов (8 методов) | ✅ Новый |
| `UITheme.cs` | ScriptableObject темы (51+ цвет) | ✅ Новый |
| `TradeUI.cs` | Интерфейс торговли | 🟡 Мигрирован |
| `ContractBoardUI.cs` | Доска контрактов | 🟡 Мигрирован |
| `InventoryUI.cs` | Круговое колесо (8 секторов) | 🟡 Спринт 1 fixes |

---

## 🏭 UIFactory — Фабрика компонентов

```csharp
// 8 методов, 0 дублирования
public static TextMeshProUGUI CreateLabel(Transform parent, string text, Vector2 anchoredPos);
public static Image CreateIcon(Transform parent, Sprite sprite, Vector2 pos);
public static Button CreateButton(Transform parent, string text, Vector2 pos, Action onClick);
public static Slider CreateSlider(Transform parent, float min, float max, Vector2 pos);
public static TMP_InputField CreateInput(Transform parent, string placeholder, Vector2 pos);
public static VerticalLayoutGroup CreateLayoutGroup(Transform parent);
public static RectTransform CreatePanel(Transform parent, Vector2 size, Vector2 pos);
public static void ApplyTheme(TextMeshProUGUI text);
```

---

## 🎨 UITheme — ScriptableObject тема

```csharp
[CreateAssetMenu(fileName = "UITheme", menuName = "ProjectC/UI/Theme")]
public class UITheme : ScriptableObject
{
    public Color primaryColor = new Color(0.1f, 0.4f, 0.8f);
    public Color backgroundColor = new Color(0.05f, 0.05f, 0.1f, 0.95f);
    public Color textColor = Color.white;
    // ... 51+ цветов
}
```

**Доступные темы:** `UITheme.Default` (авто-создание)

---

## 📦 Inventory — Круговое колесо

### 8 Типов предметов

```csharp
public enum ItemType
{
    Resource = 1,    // Ресурсы
    Equipment = 2,   // Оборудование
    Food = 3,        // Еда
    Fuel = 4,        // Топливо
    Antigrav = 5,    // Антигравий
    Meziy = 6,      // Мезий (энергия корабля)
    Medical = 7,     // Медицина
    Tech = 8         // Технологии
}
```

### Взаимодействия
- **E** — подбор предмета / открытие сундука
- **Tab** — открыть круговое колесо
- **ChestContainer** — сундук с LootTable

---

## 🔴 Известные Проблемы

| Приоритет | Проблема | Файл | Статус |
|-----------|----------|------|--------|
| P0 | AltitudeUI HUD не отображается | AltitudeUI.cs | Требует @unity-ui-specialist |
| UI | InventoryUI остаётся на OnGUI | InventoryUI.cs | Canvas-based rewrite |
| UI | TradeUI 1200 строк | TradeUI.cs | MVC разделение |
| UI | Контракты не сдаются с грузом | ContractBoardUI.cs | Спринт 3.3 |

---

## 📖 Подробнее

- `docs/QWEN-UI-AGENTIC-SUMMARY.md` — полный отчёт UI системы
- `docs/INVENTORY_SYSTEM.md` — система инвентаря
- `docs/TRADE_SYSTEM_RAG.md` — RAG торговой системы

---

**Обновлено:** 2026-04-15
