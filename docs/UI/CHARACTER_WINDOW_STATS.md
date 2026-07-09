# CharacterWindow — блок характеристик (STATS)

## Обзор

Блок «Характеристики» в CharacterWindow отображает три стата: **STR** (сила), **DEX** (ловкость), **INT** (интеллект). Каждая строка содержит:

- **Лейбл** (STR/DEX/INT) — фикс. 24px, подкрашен под цвет стата
- **Полоска прогресса** — absolute-позиционирована на всю ширину строки, позади текста
- **Текст значения** — `{flat-стат} {xp}/{порог} T{тир} (+бонус)`, на тёмной подложке для читаемости

## Структура (UXML)

```xml
<ui:VisualElement class="stat-row-compact">          <!-- position:relative — якорь для бара -->
    <ui:VisualElement class="stat-bar-bg">            <!-- position:absolute; left/right/top/bottom:0 — полный фон -->
        <ui:VisualElement name="stat-str-bar-fill" class="stat-bar-fill stat-bar-fill-str" />
    </ui:VisualElement>
    <ui:Label text="STR" class="stat-label-compact stat-label-str" />
    <ui:Label name="stat-str-value" text="—" class="stat-value-compact" />
</ui:VisualElement>
```

Ключевой момент: **bar-bg — первый ребёнок** в DOM-порядке, поэтому отрисовывается позади лейбла и значения.

## Цветовая схема

| Стат | Цвет лейбла | Цвет fill-бара |
|---|---|---|
| STR | `rgb(255, 130, 110)` — ярко-красный | `rgb(210, 65, 60)` — красный |
| DEX | `rgb(130, 230, 130)` — ярко-зелёный | `rgb(55, 175, 65)` — зелёный |
| INT | `rgb(130, 170, 255)` — ярко-синий | `rgb(60, 100, 210)` — синий |

Цвета заданы **только в USS** (классы `stat-bar-fill-str/dex/int`, `stat-label-str/dex/int`). C# не переопределяет цвета — только управляет шириной fill и tier-классами строки.

## Тиры и рамки (ApplyTierClass)

`ApplyTierClass` добавляет на **строку** (не на бар) один из CSS-классов, меняющих цвет рамки:

| Тиры | Класс | Цвет рамки |
|---|---|---|
| T0–T2 | `stat-row-tier-low` | серый `rgba(120,120,120,0.4)` |
| T3–T5 | `stat-row-tier-mid` | голубой `rgba(150,180,220,0.5)` |
| T6–T9 | `stat-row-tier-high` | фиолетовый `rgba(200,150,255,0.5)` |
| T10+ | `stat-row-tier-master` | золотой `rgba(255,215,60,0.7)` + фон |

Цвет fill-бара **никогда не перетирается** тиром — всегда per-stat (красный/зелёный/синий).

## Формула бара (C#)

```csharp
// Правильно: strength = текущий XP в тире (числитель)
float pct = Mathf.Clamp01(snap.strength / snap.strengthXpForNextTier) * 100f;
_statStrBarFill.style.width = new StyleLength(new Length(pct, LengthUnit.Percent));
```

**Баг, который был исправлен:** использовалось `snap.effectiveStrength` (flat-боевой стат) вместо `snap.strength` (XP в тире). При росте тиров `effectiveStrength` рос линейно, а `XpForNextTier` — экспоненциально, поэтому полоска **сужалась** с прогрессом.

## Формат текста (C#)

```
{effectiveStrength:F1} {strength:F0}/{strengthXpForNextTier:F0} T{strengthTier}{bonusStr}
```

Пример: `11.0 45/100 T0 (+2)` — flat-стат, XP-прогресс, тир, бонус от экипировки.

## Ключевые решения

1. **Бар absolute, а не во flex-потоке** — иначе ширина бара скакала при смене цифр (текст и бар «боролись» за место в ряду)
2. **Текст на тёмной подложке** (`rgba(0,0,0,0.45)`) — читаем поверх любого цвета fill-бара
3. **Цвета только в USS** — `ApplyTierClass` теперь меняет только рамку строки, не трогая fill
4. **Строки растянуты по высоте** — `stats-grid: flex-grow:1; justify-content:space-evenly` + `stat-row-compact: flex-grow:1`

## Связанные файлы

| Файл | Что |
|---|---|
| `Assets/_Project/UI/Resources/UI/CharacterWindow.uxml` | Структура (строки 63–85) |
| `Assets/_Project/UI/Resources/UI/CharacterWindow.uss` | Стили (секция «Compact stat bars») |
| `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` | Логика: `RefreshStatsDisplay`, `ApplyTierClass` |

## История изменений

| Коммит | Что |
|---|---|
| `354e3d2` | Фикс формулы бара, цвета в USS, горизонтальный layout |
| `d506df4` | Колонка 22→28%, бары растянуты |
| `8742613` | Строки по высоте блока, фикс.ширина текста |
| `ba26a29` | Бар absolute позади текста — ширина 100% строки |
| `c34899a` | Цифры крупнее (8→11px) |
