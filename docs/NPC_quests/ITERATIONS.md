# Итерации разработки — NPC Quests

## Итерация от 2026-07-09

**Задача:** DialogWindow: текст NPC всегда виден сверху, кнопки квестов прокручиваются (scroll)
**Коммит:** `aa2a1ec` — T-UI04: фикс DialogWindow — текст NPC всегда виден, кнопки квестов прокручиваются

**Изменения:**
- `Assets/_Project/Quests/Resources/UI/DialogWindow.uxml` — options обёрнут в `<ui:ScrollView name="options-scroll">`
- `Assets/_Project/Quests/Resources/UI/DialogWindow.uss` — panel: `min-height:400px` + `max-height:85vh`; text-scroll: `min-height:80px`; options-scroll: `max-height:220px`

## Итерация от 2026-07-09 (аудит)

**Задача:** Глубокий аудит всей системы квестов — архитектура, стабы, дублирование, интеграции
**Коммит:** `13f3c7f` — T-QAUDIT: Глубокий аудит системы квестов (NPC Quests v2)

**Изменения:**
- `docs/NPC_quests/DEEP_AUDIT_2026-07-09.md` — полный аудит (319 строк)

## Итерация от 2026-07-13 (комбинированный аудит)

**Задача:** Повторный глубокий аудит системы квестов — сравнение с предыдущим, выявление регрессов и незавершённых интеграций
**Коммит:** (pending — пользователь)

**Изменения:**
- `docs/NPC_quests/DEEP_AUDIT_2026-07-13.md` — комбинированный аудит (сопоставлен с предыдущим)
- **Критическое открытие:** квестовые ассеты (FactionDefinition, NpcDefinition, QuestDefinition) утеряны — файлы отсутствуют, GUIDs в QuestDatabase висят в никуда
