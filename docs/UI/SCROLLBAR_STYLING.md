# Scrollbar Styling — тонкие скролбары для всех окон

> **Дата:** 2026-07-02
> **Тикет:** T-CARGO-UI-SCROLL
> **Статус:** ✅ Добавлено во все 13 USS файлов

---

## 1. Проблема

В Unity 6 UI Toolkit скролбары (`ScrollView`, `ListView`) отображаются с дефолтной темой — толстые (12-16px), серые, с кнопками +/-, «виндовс 98» стиль. USS-стилизация не работала через `.unity-scrollbar` (неправильные классы для Unity 6).

## 2. Решение

Через `unity_reflect` и `execute_code` инспектирована runtime-структура `Scroller` (scrollbar VisualElement). Обнаружены актуальные классы Unity 6:

```
Scroller [unity-scroller, unity-scroller--vertical]
├── RepeatButton [unity-scroller__low-button]     ← кнопка ▲
├── RepeatButton [unity-scroller__high-button]    ← кнопка ▼
└── ScrollerSlider [unity-scroller__slider]       ← слайдер
    └── VisualElement [unity-slider__input]
        └── VisualElement [unity-base-slider__drag-container]
            ├── VisualElement [unity-base-slider__tracker]          ← ! серая полоса
            ├── VisualElement [unity-base-slider__dragger-border]    ← ! бордер
            └── VisualElement [unity-base-slider__dragger]           ← ! ползунок
```

**Ключевые классы для стилизации:**

| Класс | Что делает |
|---|---|
| `.unity-scroller` | Контейнер скролбара (ширина, фон) |
| `.unity-scroller__low-button` / `__high-button` | Кнопки +/– (скрываем) |
| `.unity-scroller__slider` | Трек слайдера |
| `.unity-slider__input` | Внутренний контейнер слайдера |
| `.unity-base-slider__tracker` | **Серая полоса** (делаем прозрачной) |
| `.unity-base-slider__dragger-border` | Бордер вокруг ползунка (скрываем) |
| `.unity-base-slider__dragger` | Ползунок (4px, голубой) |

**Важно:** Все селекторы используют родительский `.unity-scroll-view` для повышения специфичности (иначе runtime-тема перебивает).

## 3. Стили

```css
.unity-scroll-view .unity-scroller {
    width: 6px !important;
    max-width: 6px !important;
    background-color: transparent !important;
    padding: 0 !important;
    margin: 0 !important;
}
.unity-scroll-view .unity-scroller--vertical {
    width: 6px !important;
    max-width: 6px !important;
}
.unity-scroll-view .unity-scroller--horizontal {
    height: 6px !important;
    max-height: 6px !important;
}
.unity-scroll-view .unity-scroller__low-button,
.unity-scroll-view .unity-scroller__high-button {
    display: none !important;
}
.unity-scroll-view .unity-scroller__slider,
.unity-scroll-view .unity-slider__input,
.unity-scroll-view .unity-base-slider__drag-container,
.unity-scroll-view .unity-base-slider__tracker {
    background-color: transparent !important;
}
.unity-scroll-view .unity-base-slider__dragger-border {
    display: none !important;
}
.unity-scroll-view .unity-base-slider__dragger {
    background-color: rgba(100, 160, 220, 0.4) !important;
    border-radius: 3px !important;
    width: 4px !important;
    max-width: 4px !important;
    min-width: 4px !important;
    min-height: 24px !important;
    border-width: 0 !important;
}
.unity-scroll-view .unity-base-slider__dragger:hover {
    background-color: rgba(120, 180, 240, 0.6) !important;
}
```

## 4. Покрытие (13 файлов)

- `CharacterWindow.uss`, `MarketWindow.uss` — основные окна
- `CommPanel.uss` — стыковочная панель
- `DialogWindow.uss` — диалоги NPC
- `QuestTracker.uss` — трекер квестов
- `CustomisationWindow.uss` — кастомизация персонажа
- `EscMenuStyles.uss` — меню паузы
- `KeybindingsWindow.uss` — настройка клавиш
- `RebindPromptStyles.uss` — промпт ребандинга
- `SkillBindingWindow.uss` — биндинг скиллов
- `SkillTreeWindow.uss` — дерево навыков
- `CraftingWindow.uss` — крафт
- `InventoryWheel.uss` — колесо инвентаря

## 5. Трудности (pitfalls)

1. **Unity 6 сменил классы** — `.unity-scrollbar` не существует. Актуальный класс — `.unity-scroller`.
2. **Tracker элемент** — `.unity-base-slider__tracker` — это серая полоса, которая остаётся видимой даже при прозрачном `.unity-scroller`. Нужно отдельно.
3. **Dragger-border** — `.unity-base-slider__dragger-border` — бордер вокруг ползунка, тоже требует явного скрытия.
4. **Специфичность** — `.unity-scroll-view` обязателен, иначе runtime тема Unity перебивает `!important`.
