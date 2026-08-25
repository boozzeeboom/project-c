# Q001 — Отчёт 02: нормализация ключей

> **План:** `001_DIALOGUE_LOCALIZATION_REPAIR_PLAN.md`
> **Этап:** 2 — нормализация ключей только в Q001
> **Дата:** 2026-08-25
> **Статус:** PASS

## Изменения

Изменены только шесть Q001 DialogTree:

- `dlg_lyra_q001`;
- `dlg_bram_q001`;
- `dlg_veska_q001`;
- `dlg_noll_q001`;
- `dlg_kael_q001`;
- `dlg_sela_q001`.

Заменены только Q001-specific ссылки с префиксом:

```text
dialog.dlg_...
```

на:

```text
dialogue.dlg_...
```

Результат:

- **97 уникальных Q001-specific ключей** нормализовано;
- **98 ссылочных вхождений** изменено в DialogTree;
- старых Q001-specific ключей `dialog.dlg_*`: `0`;
- новых Q001-specific вхождений `dialogue.dlg_*`: `98`.

Разница между 97 ключами и 98 вхождениями вызвана повторным использованием одного ключа в нескольких местах диалогов.

## Общие ключи

Ключи `dialog.option.common.*` автоматически не изменялись.

Не затронуты:

- `dialog.option.common.goodbye`;
- `dialog.option.common.understood`;
- `dialog.option.common.not_now`;
- `dialog.option.common.cancel`;
- `dialog.option.common.thanks`.

## Безопасность

- таблицы локализации не изменялись;
- `SharedData` не изменялся;
- `Static_Table`, `UI_Table`, `Dialogue_Table`, `System_Table` не пересобирались;
- `Clear()` и удаление ключей не выполнялись;
- `LocalizationTableRepair.cs` и `AddDialogueEntries.cs` не запускались.

## Решение

Этап 2 завершён успешно. Ключи Q001 теперь маршрутизируются через `Loc.ParseKey()` в `Dialogue_Table`.

Следующий шаг — Этап 3: аддитивно добавить отсутствующие RU/EN значения только для нормализованных Q001 ключей.
