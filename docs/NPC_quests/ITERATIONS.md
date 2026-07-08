# Итерации разработки — NPC Quests

## Итерация от 2026-07-09

**Задача:** DialogWindow: текст NPC всегда виден сверху, кнопки квестов прокручиваются (scroll)
**Коммит:** `aa2a1ec` — T-UI04: фикс DialogWindow — текст NPC всегда виден, кнопки квестов прокручиваются

**Изменения:**
- `Assets/_Project/Quests/Resources/UI/DialogWindow.uxml` — options обёрнут в `<ui:ScrollView name="options-scroll">`
- `Assets/_Project/Quests/Resources/UI/DialogWindow.uss` — panel: `min-height:400px` + `max-height:85vh`; text-scroll: `min-height:80px`; options-scroll: `max-height:220px`
