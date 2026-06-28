# Phase 3 — Summary (сессия #7, 2026-06-28)

> **Статус:** ✅ Phase 3 DONE
> **Что:** BindSlot UI в SkillTreeWindow — назначение выученных навыков на слоты Primary/Secondary/Slot1-4

---

## Мотивация

Phase 1-2 сделали **переназначение клавиш** (InputBindingsConfig → KeybindingsWindow).
Но сами навыки были привязаны к слотам (Primary/Secondary/Slot1-4) только через код.
Игрок не мог в UI сказать «повесь навык `combat_basic_strike` на слот Primary».

Phase 3 добавляет **кнопки назначения** прямо в SkillTreeWindow.

## Что сделано

### Файлы

| Файл | Изменения |
|------|-----------|
| `Resources/UI/SkillTreeWindow.uxml` | 6 новых Label `btn-bind-primary/secondary/slot1-4` с классами `stw-btn stw-btn-bind` |
| `Resources/UI/SkillTreeWindow.uss` | `stw-btn-bind` стиль + layout (`flex-wrap`, `flex-basis: 45%`/`22%`) |
| `Scripts/Skills/UI/SkillTreeWindow.cs` | 6 полей `_btnBindPrimary`, … + Q загрузка + `RegisterCallback` + `OnBindSlotClicked()` |
| `Scripts/Skills/SkillInputService.cs` | +`GetAllSkillIds()`, +`GetSkillForSlot()`, +`GetAllBindings()`, +`SetKnownSkills()` |

### Архитектура

```
SkillTreeWindow (выученный навык)
  └── detail панель → секция действий
       ├── [Забыть]              (свой ряд, 100%)
       ├── [→ Prim] [→ Sec]      (ряд, 45%+45%)
       └── [→ 1] [→ 2] [→ 3] [→ 4] (ряд, 22%×4)
```

Клик на `→ Prim` → `SkillInputService.BindSlot(Primary, skillId)` → навык привязан к ЛКМ.
Клик на `→ 1` → `BindSlot(Slot1, skillId)` → навык привязан к цифре `1`.

### SkillInputService API (Phase 3)

| Метод | Назначение |
|-------|------------|
| `BindSlot(SkillInputSlot, string skillId)` | Привязать навык к слоту (был, доработан) |
| `GetSkillForSlot(SkillInputSlot)` → string | Какой навык сейчас в слоте |
| `GetAllSkillIds()` → IReadOnlyList | Список всех известных skillId |
| `GetAllBindings()` → Dictionary | Все привязки slot → skillId |
| `SetKnownSkills(IEnumerable<string>)` | Задать список (вызывается из ClientState) |

### Как работает

1. Игрок открывает **SkillTreeWindow** (CharacterWindow → вкладка Skills → [SkillTree])
2. Кликает на **выученный навык** (зелёный ✓)
3. В правой detail панели появляются кнопки:
   - `→ Prim` — назначить на ЛКМ
   - `→ Sec` — назначить на ПКМ
   - `→ 1` — назначить на клавишу 1
   - `→ 2` / `→ 3` / `→ 4` — на 2/3/4
4. Клик → `SkillInputService.BindSlot()` → навык активируется через комбинацию клавиш

### Условия
- Bind-кнопки видны **только для выученных навыков** (isLearned=true)
- Если навык уже привязан к другому слоту — отвязывается автоматически (переносится)
- Bind-кнопки в `stw-detail-actions` — секция действий (Изучить/Забыть/Bind)

---

## Взаимодействие с InputBindingsConfig

```
InputBindingsConfig.asset (31 бинд)
  └── combatSkills (10 биндов: ЛКМ/ПКМ/Ctrl+.../1-4)
        └── SkillInputSlot.Primary → проверяет
              └── SkillInputService.GetSkillForSlot(Primary)
                    └── если есть — активирует навык при нажатии ЛКМ
```

Таким образом, `BindSlot` в **SkillTreeWindow** определяет **какой навык** активируется
при нажатии клавиши, а **KeybindingsWindow** определяет **какую клавишу** нажать.

---

## Что НЕ сделано

- Drag-and-drop (pick-and-bind через клик — достаточный MVP)
- PlayerPrefs persistence для slot→skillId (только runtime)
- Визуальный индикатор «какой слот куда назначен» в дереве навыков
