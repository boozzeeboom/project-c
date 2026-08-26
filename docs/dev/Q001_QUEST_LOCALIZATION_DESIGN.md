# Q001 Quest Localization — Design Note

> **Дата:** 2026-08-26
> **Скоуп:** только Q001 (main + 3 ветки, 55 ключей)
> **Статус:** design → implementation

## Проблема

Q001 quest assets (`q_001_ash_under_glass`, `q_001a`, `q_001b`, `q_001c`) используют ключи `quest.q001.*` / `quest.q001a.*` / `quest.q001b.*` / `quest.q001c.*` для `displayName`, `description`, stage/objective `description`.

`Loc.ParseKey()` (`Loc.cs:189`) маршрутизирует **только по префиксу**:
- `static.*` → `Static_Table`
- `ui.*` → `UI_Table`
- `dialogue.*` → `Dialogue_Table`
- `sys.*` → `System_Table`
- **всё остальное (включая голый `quest.*`) → `UI_Table`** (default, строка 202)

Значит, ключи `quest.q001.*` обязаны быть в **`UI_Table`**, чтобы `Loc.Get` их нашёл.

**Текущее состояние:**
- `UI_Table Shared Data` — 424 ключей, **0** с префиксом `quest.`
- `Static_Table Shared Data` — 6 мёртвых ключей `quest.q001*.objective.reach_*` (строки 495–515) — добавлены в неправильную таблицу, не резолвятся
- На рунтайме все 55 ключей Q001 показываются как голые ключи (fallback = key itself)

**Рендер-путь:**
1. `QuestServer.BuildQuestSnapshot` (`QuestServer.cs:843`) кладёт в DTO **голый ключ** из ассета: `desc = st.objectives[k].description`
2. Ключ приходит в клиент в `ObjectiveProgressDto.description`
3. Рендер-слой:
   - `QuestTracker.cs:309` → `Loc.Get(o.description, o.description)` ✅ резолвит
   - `CharacterWindow.cs:2910` → `new Label { text = o.description }` ❌ **не резолвит** (голый ключ)

## Решение

### 1. Добавить 55 ключей в `UI_Table`

Через Editor-скрипт `AddQ001QuestKeys.cs` (безопасный паттерн из skill `project-c-localization`):

```csharp
var sharedEntry = collection.SharedData.AddKey(key);
table_ru.AddEntry(sharedEntry.Id, ruValue);
table_en.AddEntry(sharedEntry.Id, enValue);
EditorUtility.SetDirty(table_ru);
EditorUtility.SetDirty(table_en);
EditorUtility.SetDirty(collection.SharedData);
EditorUtility.SetDirty(collection);
AssetDatabase.SaveAssets();
```

**Не трогать:**
- `Static_Table` (6 мёртвых `reach_*` — не переносить, не удалять)
- `ui.character.quests_progress` (статус — по решению пользователя)
- Любые существующие 424 ключа `UI_Table`

### 2. Починить `CharacterWindow.cs:2910`

Заменить:
```csharp
new Label { text = o.description }
```
на:
```csharp
new Label { text = Loc.Get(o.description, o.description) }
```

Это починит рендер целей в окне персонажа. `QuestTracker.cs:309` уже резолвит — не трогаем.

### 3. RU/EN-тексты

RU — из draft-дока `001_ASH_UNDER_GLASS_DRAFT.md` §7. EN — перевод.

## Инвентарь (55 ключей)

| Квест | Ключей |
|---|---|
| `q_001_ash_under_glass` (main) | 22 |
| `q_001a_signal_reconstruction` | 11 |
| `q_001b_blackbox_salvage` | 12 |
| `q_001c_silent_exchange` | 10 |
| **Итого** | **55** |

## Верификация

1. **Compile:** Unity Editor → Console → 0 errors
2. **Static:** `UI_Table Shared Data` содержит 55 новых ключей `quest.q001*.*`
3. **Runtime:** Play Mode → открыть окно персонажа → вкладка «Квесты» → цели Q001 показывают RU-текст, не голые ключи
4. **Locale switch:** RU → EN → RU → тексты переключаются
5. **No regression:** существующие 424 ключа `UI_Table` не изменились
