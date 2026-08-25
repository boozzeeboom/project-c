# Q001 — Отчёт 01: статический аудит диалогов

> **План:** `001_DIALOGUE_LOCALIZATION_REPAIR_PLAN.md`
> **Этап:** 1 — статический аудит Q001
> **Дата:** 2026-08-25
> **Статус:** PASS
> **Режим:** только чтение; ассеты и таблицы не изменялись

## Объём

Проверены шесть Q001 DialogTree:

- `dlg_bram_q001`;
- `dlg_kael_q001`;
- `dlg_lyra_q001`;
- `dlg_noll_q001`;
- `dlg_sela_q001`;
- `dlg_veska_q001`.

Найдено **102 уникальных ссылочных ключа** из `displayName`, `node.text` и `edge.label`.

## Классификация ключей

| Группа | Количество | Действие |
|---|---:|---|
| Q001-specific `dialog.dlg_*` | 97 | Нормализовать только в Q001 assets в `dialogue.dlg_*` |
| Общие `dialog.option.common.*` | 5 | Не менять автоматически; сопоставить с существующими ключами |

## Общие ключи

| Текущий ключ | Нормализованный кандидат | Кандидат уже существует |
|---|---|---:|
| `dialog.option.common.goodbye` | `dialogue.option.common.goodbye` | да |
| `dialog.option.common.understood` | `dialogue.option.common.understood` | да |
| `dialog.option.common.not_now` | `dialogue.option.common.not_now` | нет |
| `dialog.option.common.cancel` | `dialogue.option.common.cancel` | нет |
| `dialog.option.common.thanks` | `dialogue.option.common.thanks` | да |

Для трёх общих ключей уже существует правильный `dialogue.*` вариант. Для `not_now` и `cancel` требуется отдельное решение по значениям; автоматически заменять их в Q001 нельзя до проверки всех потребителей.

## Q001-specific ключи

Все 97 Q001-specific ссылок имеют формат `dialog.dlg_<treeId>...` и после безопасной нормализации должны получить формат:

```text
dialogue.dlg_<treeId>...
```

Все 97 нормализованных ключей отсутствуют в текущем `Dialogue_Table Shared Data` и требуют аддитивного добавления после изменения ссылок.

## Состояние таблицы до изменений

- `Dialogue_Table SharedData`: 41 запись;
- `Dialogue_Table_ru`: 41 запись;
- `Dialogue_Table_en`: 37 записей.

Другие таблицы на этом этапе не изменялись.

## Решение

Этап 1 завершён успешно.

Разрешённый следующий шаг:

1. заменить только 97 Q001-specific ключей `dialog.dlg_*` на `dialogue.dlg_*`;
2. не изменять пять общих `dialog.option.common.*` ключей до отдельного решения;
3. после замены повторно проверить ссылки и только затем готовить аддитивное добавление переводов.
