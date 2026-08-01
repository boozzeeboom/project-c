# SocialSkillTreeWindow — окно графа социальных навыков

> **Дата:** 2026-07-16 (v2: фикс вёрстки — реюз ресурсов SkillTreeWindow)
> **Ticket:** T-SOC-01

## Задача

Добавить в колонку «Социальные навыки» CharacterWindow кнопку «ИЗУЧИТЬ НАВЫК», открывающую новое окно `SocialSkillTreeWindow` — упрощённую версию `SkillTreeWindow` без слотов биндов (все социальные навыки пассивные).

## Реализация

### Подход: реюз ресурсов SkillTreeWindow
**v1 была сломана** — создание отдельных UXML/USS привело к расхождению вёрстки.
**v2:** `SocialSkillTreeWindow` использует **те же** `SkillTreeWindow.uxml` и `SkillTreeWindow.uss`. Ненужные элементы скрываются через `style.display = DisplayStyle.None` в `EnsureBuilt()`:
- `.stw-slot-overview-col` — левая панель слотов
- `.stw-chip-row` — чипы фильтрации по дисциплинам
- `btn-bind-primary/secondary/slot1-4` — кнопки бинда на слоты

Заголовок меняется на «Социальные навыки» через `Q<Label>(className: "stw-title").text`.

### Файлы

| Файл | Действие |
|---|---|
| `CharacterWindow.uxml` | + кнопка `open-social-skill-tree-btn` |
| `CharacterWindow.cs` | + `InitOpenSocialSkillTreeButton()` |
| `SocialSkillTreeWindow.cs` | Создан — реюз SkillTreeWindow UXML/USS |
| `NetworkManagerController.cs` | + `CreateSocialSkillTreeWindow()` auto-spawn |
| `UIManager.cs` | + Esc-закрытие |

### Социальные навыки (4 шт.)

| skillId | displayName | tier | prereqs |
|---|---|---|---|
| `social_basic_talk` | Базовый разговор | INT 0 | — |
| `social_barter` | Торговля | INT 2 | BasicTalk |
| `social_persuasion` | Убеждение | INT 2 | BasicTalk |
| `social_leadership` | Лидерство | INT 4 | Barter, Persuasion |

## Верификация

- [ ] P→ПЕРСОНАЖ: кнопка «ИЗУЧИТЬ НАВЫК» в колонке «Социальные навыки»
- [ ] Клик → `SocialSkillTreeWindow` overlay (граф из 4 навыков)
- [ ] Detail-панель: название, описание, эффекты, стоимость, требования, prereq/deps
- [ ] Кнопки «Изучить»/«Забыть» работают
- [ ] Нет панели слотов, нет кнопок бинда, нет чипов фильтра
- [ ] Esc закрывает окно
