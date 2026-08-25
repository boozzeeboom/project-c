# Q001 — Отчёт 03: аддитивная диалоговая локализация

> **План:** `001_DIALOGUE_LOCALIZATION_REPAIR_PLAN.md`
> **Этапы:** 3 — аддитивное добавление RU/EN; 4 — пост-изменительный статический контроль
> **Дата:** 2026-08-25
> **Статус:** PASS

## Выполненные изменения

После отменённой предыдущей процедуры состояние проверено заново. Сохранённые изменения выполнены узкой идемпотентной процедурой, без `Clear()`, rebuild или удаления SharedData.

Изменены только:

- шесть Q001 DialogTree:
  - `dlg_lyra_q001`;
  - `dlg_bram_q001`;
  - `dlg_veska_q001`;
  - `dlg_noll_q001`;
  - `dlg_kael_q001`;
  - `dlg_sela_q001`;
- `Dialogue_Table Shared Data.asset`;
- `Dialogue_Table_ru.asset`;
- `Dialogue_Table_en.asset`;
- два узких editor-инструмента Stage 3/4.

## Количественные результаты

| Проверка | Результат |
|---|---:|
| Уникальные Q001-specific ключи | 97 |
| Общие нормализованные ключи | 5 |
| Всего Q001 ссылочных ключей | 102 |
| Новые SharedData entries | 99 (97 Q001-specific + `not_now` + `cancel`) |
| SharedData до / после | 41 → 140 |
| RU entries до / после | 41 → 140 |
| EN entries до / после | 37 → 136 |
| Старые `dialog.dlg_*` / `dialog.option.common.*` ссылки | 0 |

Три существующих общих ключа (`goodbye`, `understood`, `thanks`) не перезаписывались и сохранили свои SharedData ID. Для отсутствовавших `not_now` и `cancel` добавлены новые entries через `SharedData.AddKey()`.

## Контроль значений

Для каждого из 102 Q001 ключей подтверждены:

- наличие в `Dialogue_Table Shared Data`;
- наличие RU значения;
- наличие EN значения;
- RU и EN значения не равны literal key и не пусты.

Существующие значения и ID не перезаписывались. Повторный запуск процедуры не добавляет дубликаты.

## Stage 4 — статическая проверка

Результат validator:

```text
PASS: q001Keys=102; nodes=42; oldPrefixes=0; dangling=0; unreachable=0; missingShared=0; missingRU=0; missingEN=0; invalidTranslations=0; dialogueSharedTotal=140; dialogueRuTotal=140; dialogueEnTotal=136
```

Дополнительно подтверждено:

- все 42 Q001 nodes достижимы;
- dangling edge targets отсутствуют;
- все displayName/node.text/edge.label Q001 используют `dialogue.*`;
- `Static_Table`, `UI_Table` и `System_Table` процедурой не затрагивались;
- `check_compile_errors`: `No compile errors`.

## Оставшиеся блокеры

- Play Mode-прогон RU/EN ещё не выполнен; по плану его выполняет пользователь вручную с screenshots.
- Локализация Q001 quest/NPC/item/zone/objective display strings не входит в этот отчёт и требует отдельного аудита.

## Решение

Этапы 3 и 4 завершены успешно. Можно переходить к ручному Этапу 5: Play Mode-прогону шести Q001 NPC и переключению RU → EN → RU.
