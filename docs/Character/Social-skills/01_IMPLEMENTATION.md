# SocialSkillTreeWindow — окно графа социальных навыков

> **Дата:** 2026-07-16
> **Ticket:** T-SOC-01
> **База:** `SkillTreeWindow` (T-INP-09, `docs/Character/Skills/Battle/60_SKILL_TREE_WINDOW_DESIGN.md`)

## Задача

Добавить в колонку «Социальные навыки» CharacterWindow кнопку «ИЗУЧИТЬ НАВЫК», открывающую новое окно `SocialSkillTreeWindow` — упрощённую версию `SkillTreeWindow` без слотов биндов (все социальные навыки пассивные).

## Реализация

### 1. CharacterWindow.uxml
Добавлен `open-social-skill-tree-btn` (Label) после `skills-social-scroll` в колонке социальных навыков. Использует класс `.open-skill-tree-btn` (реюз стилей боевой кнопки).

### 2. CharacterWindow.cs
Добавлен метод `InitOpenSocialSkillTreeButton()` — открывает `SocialSkillTreeWindow.Instance.Show()`. Вызывается в `InitProgressionTab()` после `InitOpenSkillTreeButton()`.

### 3. SocialSkillTreeWindow.uxml
Упрощённый макет (отличия от SkillTreeWindow):
- **Нет** левой панели slot-overview (все навыки пассивные)
- **Нет** bind-кнопок (Primary/Secondary/Slot1-4)
- **Нет** filter chips (для социальных навыков нет discipline-фильтра)
- Только: top (title + search), middle (tree + detail), bottom (close)

### 4. SocialSkillTreeWindow.uss
Копия `SkillTreeWindow.uss` без:
- `.stw-slot-overview-col`, `.stw-slot-overview-container`, `.stw-slot-cell`
- `.stw-chip-row`, `.stw-chip`, `.stw-chip-active`
- `.stw-btn-bind`, `#btn-bind-primary` и т.д.

### 5. SocialSkillTreeWindow.cs
Singleton-окно по паттерну `SkillTreeWindow`. Отличия:
- `LoadAllSkills()` грузит только `SkillCategory.Social`
- Нет slot overview (нет `RebuildSlotOverview`, нет `_slotOverviewContainer`)
- Нет bind-кнопок
- Нет AOE-форматирования (все навыки пассивные)
- Все узлы дерева помечаются `.tree-node-passive` и `[P]`
- Learn/Forget через тот же reflection-RPC (`SkillsServer.RequestLearnSkillRpc` / `RequestForgetSkillRpc`)

### 6. NetworkManagerController.cs
Добавлен `CreateSocialSkillTreeWindow()` — auto-spawn root GameObject с UIDocument + SocialSkillTreeWindow, по тому же шаблону что и `CreateSkillTreeWindow()`. Reuses `SkillTreePanelSettings`.

### 7. UIManager.cs
Добавлена проверка `SocialSkillTreeWindow.Instance.IsOpen()` в `IsAnyExternalWindowOpen()` — Esc закрывает окно.

## Файлы

| Файл | Действие |
|---|---|
| `Assets/_Project/UI/Resources/UI/CharacterWindow.uxml` | Изменён: + кнопка `open-social-skill-tree-btn` |
| `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` | Изменён: + `InitOpenSocialSkillTreeButton()` |
| `Assets/_Project/Resources/UI/SocialSkillTreeWindow.uxml` | Создан |
| `Assets/_Project/Resources/UI/SocialSkillTreeWindow.uss` | Создан |
| `Assets/_Project/Scripts/Skills/UI/SocialSkillTreeWindow.cs` | Создан |
| `Assets/_Project/Scripts/Core/NetworkManagerController.cs` | Изменён: + `CreateSocialSkillTreeWindow()` |
| `Assets/_Project/Scripts/UI/UIManager.cs` | Изменён: + проверка в `IsAnyExternalWindowOpen()` |

## Социальные навыки (4 шт.)

| skillId | displayName | tier | prereqs | treeX | treeY |
|---|---|---|---|---|---|
| `social_basic_talk` | Базовый разговор | INT 0 | — | 100 | 200 |
| `social_barter` | Торговля | INT 2 | BasicTalk | 200 | 250 |
| `social_persuasion` | Убеждение | INT 2 | BasicTalk | 200 | 300 |
| `social_leadership` | Лидерство | INT 4 | Barter, Persuasion | 300 | 350 |

## Верификация

- [ ] P→ПЕРСОНАЖ: в колонке «Социальные навыки» видна кнопка «ИЗУЧИТЬ НАВЫК»
- [ ] Клик → открывается `SocialSkillTreeWindow` overlay с графом из 4 социальных навыков
- [ ] Detail-панель: название, описание, эффекты, стоимость, требования, prereq/deps
- [ ] Кнопки «Изучить»/«Забыть» работают (RPC в консоли)
- [ ] Нет кнопок бинда на слоты
- [ ] Нет левой панели «Слоты»
- [ ] Esc закрывает окно
