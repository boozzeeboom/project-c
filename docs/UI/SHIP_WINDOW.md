# Ship Window — итоги вёрстки и архитектура (T-CARGO-UI-01)

> **Файл:** `docs/UI/SHIP_WINDOW.md`
> **Дата:** 2026-07-02
> **Назначение:** Документация по вкладке «Корабль» в CharacterWindow — что было сделано, структура UXML/USS, архитектурные решения.

---

## 1. Что показывает вкладка «Корабль»

**Вызов:** `CharacterWindow` → таб «КОРАБЛЬ» (кнопка `#tab-ship`).

**Содержимое (сверху вниз):**

| Элемент | UXML name | Класс | Данные |
|---|---|---|---|
| Dropdown выбора корабля | `ship-selector` | `.ship-selector` | Список из `InventoryWorld.GetMyShips()` (по ключам) |
| Сообщение «нет кораблей» | `ship-empty-label` | `.ship-empty` | Показывается если `_choices.Count == 0` |
| **ship-info** (основной блок) | `ship-info` | `.ship-info` | Flex-column, растягивается на всё место |
| Key-ID | `ship-info-key-id` | `.ship-info-key-id` | `🔑 Key itemId=X, instanceId=Y` |
| Bar топлива (bg+fill) | `ship-fuel-bar-fill` | `.ship-bar-bg` + `.ship-bar-fill-fuel` | `_fuelBarFill.style.width = %` |
| Текст топлива | `ship-fuel-text` | `.ship-info-row-compact` | `Топливо: 40.0% (100 max)` |
| **ship-cols** (2 колонки) | — | `.ship-cols` | Flex-row, 50/50 |
| Колонка Груз | `ship-col-cargo` | `.ship-col` | Содержит bar + ScrollView |
| Bar груза (bg+fill) | `ship-cargo-bar-fill` | `.ship-bar-fill-cargo` | `_cargoBarFill.style.width = %` |
| Текст груза | `ship-cargo-text` | `.ship-info-row-compact` | `Груз: 6/10` |
| ScrollView списка груза | `ship-cargo-scroll` | `.ship-cargo-scroll` | `RenderCargoDetail()` — programmatic rows |
| Колонка Модули | `ship-col-modules` | `.ship-col` | ScrollView с модулями (заглушка) |
| Footer (позиция + состояние) | `ship-footer-row` | `.ship-footer-row` | inline, border-top |

---

## 2. Файлы

| Файл | Назначение |
|---|---|
| `Assets/_Project/UI/Resources/UI/CharacterWindow.uxml` | UXML-шаблон окна, секция `#ship-section` |
| `Assets/_Project/UI/Resources/UI/CharacterWindow.uss` | Стили (секция `/* T-KEY-08: MyShipsTab */`) |
| `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` | Контроллер окна (SwitchTab, build, lifecycle) |
| `Assets/_Project/Scripts/UI/Client/CharacterWindow/MyShipsTab.cs` | Таб «Корабль» — выбор корабля, рендер telemetry + cargo detail |

---

## 3. Поток данных

```
ShipController (server)
  └─ UpdateTelemetryState() — 5 Hz (throttle 200ms)
     ├─ ShipTelemetryState.fuelNormalized / fuelMax
     ├─ ShipTelemetryState.cargoUsed / cargoMax
     └─ ShipTelemetryState.cargoDetail[]
         └─ CargoDetailDto[32]
            ├─ itemId         (string)
            ├─ displayName    (FixedString64Bytes, резолвится на сервере)
            ├─ quantity       (int)
            ├─ unitWeight     (float)
            └─ flags          (byte: bit0=dangerous, bit1=fragile)
                ↓  NetworkVariable<ShipTelemetryState>
ShipTelemetryClientState (client)
  └─ OnShipStateChanged → MyShipsTab.HandleShipStateChanged
     └─ RenderSelectedShip() → UI
```

**Throttle:** `ShipTelemetryStateEqualsApprox` сравнивает все поля + `cargoDetail[]` поэлементно. При delta → `HandleShipStateChanged` → `RenderSelectedShip()`.

**cargoUsed** = `CargoData.ComputeTotalSlots(resolver)` (sum qty*slots per item), **не** `Items.Count` (исправленный баг).

**cargoMax** = `ShipCargoRegistry.GetEffectiveLimits(shipId)?.maxSlots ?? ShipClassLimits.Get(class).maxSlots` (per-instance + module bonuses, исправленный баг «cargoMax=0»).

---

## 4. Кастомные ProgressBar'ы (bg + fill)

Вместо `UnityEngine.UIElements.ProgressBar` используется пара `VisualElement bg + VisualElement fill`. Причина: ProgressBar в Unity 6 runtime имеет жёсткий дефолтный стиль (толстый, цифры поверх), который плохо переопределяется.

**USS:**
```css
.ship-bar-bg {
    flex-grow: 0;
    height: 8px;
    background-color: rgba(40, 55, 85, 0.6);
    border-radius: 4px;
    overflow: hidden;
}
.ship-bar-fill {
    width: 0%;
    height: 100%;
    border-radius: 4px;
    transition-property: width;
    transition-duration: 0.3s;
}
.ship-bar-fill-fuel  { background-color: rgba(120, 220, 150, 0.85); }
.ship-bar-fill-cargo { background-color: rgba(100, 200, 255, 0.85); }
```

**C#:**
```csharp
_fuelBarFill.style.width = new StyleLength(new Length(fuelPct, LengthUnit.Percent));
_cargoBarFill.style.width = new StyleLength(new Length(cargoPct, LengthUnit.Percent));
```

---

## 5. Селектор корабля (DropdownField)

`DropdownField` в Unity 6 runtime использует `GenericDropdownMenu` (не VisualElement), поэтому **выпадающий список не стилизуется USS** — всегда системный серый/белый.

Стилизована только кнопка (видимая часть):

| Свойство | Значение |
|---|---|
| `min-height` | 24px |
| `font-size` | 13px |
| Input bg | `rgba(30, 45, 70, 0.5)` |
| input border-radius | 4px |
| Text color | `rgb(200, 220, 255)` |
| Label | bold, 12px, голубой |

**Решение для полной стилизации:** заменить `DropdownField` на кастомный компонент (см. раздел «Custom Dropdown»).

---

## 6. CustomDropdown (реализовано)

Вместо `DropdownField` используется `CustomDropdown` (`Assets/_Project/Scripts/UI/Client/CharacterWindow/CustomDropdown.cs`) — полностью VisualElement-based компонент:

- **Кнопка** — `custom-dropdown__button` (Label + ▼ стрелка), кликабельная
- **Popup** — создаётся на `panel.visualTree` (overlay, не обрезается `overflow: hidden`)
- **Items** — `custom-dropdown__item`, тёмные, с hover-подсветкой, scroll при >6 items
- **Закрытие** — клик вне popup'а (глобальный PointerDown на панели)
- **Cleanup** — `Cleanup()` при уничтожении окна

**USS-классы:** `.custom-dropdown`, `__button`, `__text`, `__arrow`, `__popup`, `__item` — все стилизуются.

**Связано:** `docs/UI/CUSTOM_DROPDOWN_DESIGN.md`

---

## 7. История изменений

| Дата | Изменение |
|---|---|
| 2026-06-17 | T-CARGO-06: per-instance лимиты трюма + модули |
| 2026-07-02 | T-CARGO-UI-01-1: CargoDetailDto, server-push cargo list, фикс cargoMax |
| 2026-07-02 | T-CARGO-UI-01-2: вёрстка 2 колонки (груз/модули), компактный header |
| 2026-07-02 | T-CARGO-UI-01-3: кастомные бары bg+fill, стилизация DropdownField |
| 2026-07-02 | T-CARGO-UI-01-4: удалён дубликат имени, popup — не стилизуется USS |
| 2026-07-02 | T-CARGO-UI-01-5: `CustomDropdown` VisualElement-based — полная стилизация USS вместо `DropdownField` |
