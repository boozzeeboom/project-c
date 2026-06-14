# CHANGELOG — Character Progression

> **Что это:** лог изменений дизайн-документации. Каждая запись = одна сессия актуализации (сводка принятых решений пользователя → обновление файлов).
> **Дата первой записи:** 2026-06-14

---

## 2026-06-14 — S1: Первые ответы пользователя на Open Questions

**Сессия:** пользователь записал ответы под каждым вопросом в `09_OPEN_QUESTIONS.md`. Задача — актуализировать всю дизайн-документацию по ответам.

### Решения пользователя (сводка)

| # | Вопрос | Ответ | Действие |
|---|--------|-------|----------|
| Q1.1 | Стартовые значения | **(a) 0/0/0** | OK — current default |
| Q1.2 | Per-stat base XP разные? | **все одинаковые**, главное — **множители за действия в SO** | Подтвердить, никаких изменений tierBaseXp |
| Q1.3 | Глобальный множитель | **без ограничений**, 1 = ориентир на норму | Расширить Range |
| Q1.4 | NPC-spam cooldown | **НЕ cooldown**, а **per unique dialog/нажатие** | **ПЕРЕПИСАТЬ** §3 NPC-spam protection |
| Q1.5 | Walk threshold | **per 1m**, настраиваемо, **зацемпить total walked для ачивок** | Изменить + добавить `totalDistanceWalked` |
| Q1.6 | Pilot XP | только пилотирование (boarding не считается) | OK — current default |
| Q1.7 | Stat-bonus | **(c) Both** additive + multiplicative | OK — current default |
| Q2.1 | Clothing vs Module | **(a) Wearable vs Implant**, **но оба видны** | Уточнить semantic "видны" |
| Q2.2 | Слоты | **(a) 13 слотов** | OK — current default |
| Q2.3 | Required skills | **(c) Both** — hard для уникальных, soft для обычных | **ИЗМЕНИТЬ** TryEquip |
| Q2.4 | Unequip delay | **(a) мгновенно** | OK — current default |
| Q2.5 | Модули | **(a) персонажные импланты** | OK — current default |
| Q3.1 | Навыки в MVP | **8 навыков достаточно** | OK — current default |
| Q3.2 | Стартовые навыки | **(b) никаких** | **ИЗМЕНИТЬ** SkillsConfig.defaultSkills = empty |
| Q3.3 | Skill XP cost | **(a) 0/100/200**, настраиваемо | OK — current default |
| Q3.4 | Забывание навыков | **(c) Free respec без потерь** | **ДОБАВИТЬ** RequestForgetSkillRpc |
| Q3.5 | Prerequisites | **(a) DAG**, **нодовая система → Phase 2** | OK — current default |
| Q3.6 | Skill tree UI | **сразу с Painter2D graph** | **ИЗМЕНИТЬ** — T-P14 + T-P19 объединить |
| Q4.1 | Tab placement | **(a) nested sub-tabs** | OK — current default |
| Q4.2 | Progress bar | **(b) Fill + value, без тиров в UI** | **УБРАТЬ** tier label |
| Q4.3 | Tier color | **(b) per-category + свечение по уровню** | **ИЗМЕНИТЬ** USS-стили (continuous glow) |
| Q4.4 | Tier-up notification | **все 3 комплексно (toast + inline + progress)** | **ДОБАВИТЬ** QuestToast integration |
| Q4.5 | Skill row action | **без панели, только кнопки** | OK — current default |
| Q5.1 | Save format | JSON (a) | OK — current default |
| Q5.2 | Atomic write | общий подход без костылей | OK — current default (tmp + Move) |
| Q5.3 | Save триггеры | (a) + (b) + (c) | OK — current default |
| Q5.4 | Load триггеры | (a) OnClientConnected | OK — current default |
| Q6.1 | Placeholder | M1 = working server сразу | OK — current default |
| Q6.2 | Стартовый StatsConfig | настраиваемо + глобальный мультипликатор | OK — current default |
| Q7.1 | Out-of-scope | всё верно | OK — current default |
| Q8.1 | Приоритет | **всё по порядку, без костылей** | Подтвердить |
| Q8.2 | M4 разделение | по приоритетам только если нужно | OK — current default |
| Q9.1 | Локализация | (a) hardcoded, локализация позже | OK — current default |
| Q9.2 | Тестирование | manual через unity-mcp | Подтвердить |
| Q9.3 | Структура | per-subsystem folders | OK — current default |
| Q10.1 | Формула | (a) classic geometric | OK — current default |
| Q10.2 | Heavy weapons | (a) только mining, **задокументировать вход** | OK + добавить в roadmap |
| Q10.3 | Mining mapping | **hardcoded mining → STR**, не нужна вариативность | **УБРАТЬ** per-stat mapping fields |
| Q10.4 | Глобальный множитель в UI | (a) Скрыт | OK — current default |
| Q10.5 | Когда добавлять events | все 8 в одном, **с debug on/off в инспекторе** | **ДОБАВИТЬ** _debugLogging поле |

### Файлы обновлённые

| Файл | Что изменилось |
|------|----------------|
| `00_README.md` | TL;DR обновлён — убраны "угадайки", заменены на confirmed answers |
| `02_V2_ARCHITECTURE.md` | Убрано per-stat mapping (Q10.3), добавлен debug logging (Q10.5), добавлен total walked tracking (Q1.5) |
| `03_DATA_MODEL.md` | `StatsConfig`: расширен Range globalMultiplier (Q1.3), убраны per-stat mapping (Q10.3), добавлен debug logging toggle (Q10.5), добавлен total walked distance field (Q1.5) |
| `04_STATS_PROGRESSION.md` | §3 NPC-spam полностью переписан (Q1.4), §1.3 уточнён per-1m (Q1.5), добавлен total walked achievement tracking |
| `05_CLOTHING_AND_MODULES.md` | §4.3 TryEquip поддерживает Both (hard+soft) (Q2.3), §1.2 "видны" уточнено (Q2.1) |
| `06_SKILL_TREE.md` | §4.3 Painter2D graph как primary подход (Q3.6), §1.3 default skills = empty (Q3.2), §3.2 RequestForgetSkillRpc (Q3.4) |
| `07_UI_TABS_IN_CHARACTER_WINDOW.md` | §3.2 убран tier label, упрощён stat-row-progress (Q4.2), §3.2 stat-progress-fill — per-category colors + continuous glow (Q4.3), §4.6 tier-up = 3 эффекта (Q4.4) |
| `08_ROADMAP.md` | T-P11 (SkillsConfig) defaultSkills = empty, T-P13 (SkillsServer) добавить forget, T-P14 (Skill UI) **Painter2D graph** = primary, T-P19 помечен как T-P14b (объединён), T-P20 forget skill |
| `09_OPEN_QUESTIONS.md` | Добавлена секция "## 11. Decision Log" — зафиксированы все ответы |

### Не изменилось (подтверждение defaults)

- Q1.1 стартовые 0/0/0
- Q1.6 только пилотирование
- Q1.7 additive+multiplicative bonuses
- Q2.2 13 слотов
- Q2.4 unequip мгновенно
- Q2.5 персонажные импланты
- Q3.1 8 навыков достаточно
- Q3.3 0/100/200 XP cost
- Q3.5 DAG
- Q4.1 nested sub-tabs
- Q4.5 без панели
- Q5.1 JSON
- Q5.2 tmp + Move
- Q5.3 (a)+(b)+(c) save triggers
- Q5.4 (a) load on connect
- Q6.1 M1 = working server
- Q6.2 настраиваемо
- Q7.1 out-of-scope OK
- Q9.1 hardcoded Russian
- Q9.3 per-subsystem folders
- Q10.1 classic geometric
- Q10.4 скрыт

### Открыто осталось (требует доп. решений)

Ничего критичного — все топ-5 вопросов получили ответы. Дизайн зафиксирован, можно начинать T-P01.
