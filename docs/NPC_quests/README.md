# NPC_quests — индекс документации

> Актуально на 2026-11-26. Исторические документы вынесены в
> `docs/archive/NPC_quests_iterations/` и `docs/archive/NPC_quests_design_legacy/`.

## Что читать первым

| Документ | Когда нужен |
|---|---|
| `00_NAMED_NPC_PIPELINE.md` | Создать именного NPC: definition, диалог, постановка в сцену, память игрока. |
| `Quests/00_UNIVERSAL_QUEST_GUIDE.md` | Создать/починить квест, stage, objective, награду, turn-in. |
| `Quests/001/001_ASH_UNDER_GLASS_DRAFT.md` | Текущий квест Q001: сюжет, ID, ветки, ассеты. |
| `audits/DEEP_AUDIT_2026-08-13.md` | Последний глубокий аудит квестовой подсистемы (снимок, не статус). |
| `Faction/FACTION_ID_DATA_DRIVEN_PLAN.md` | План перевода фракций на data-driven `FactionDefinition`. |

Короткое правило: **квесты — universal guide, именные NPC — named NPC pipeline**.

## Структура

```text
docs/NPC_quests/
├── README.md
├── 00_NAMED_NPC_PIPELINE.md      ← как делаем именных NPC
├── audits/
│   └── DEEP_AUDIT_2026-08-13.md
├── Faction/
│   └── FACTION_ID_DATA_DRIVEN_PLAN.md
└── Quests/
    ├── 00_UNIVERSAL_QUEST_GUIDE.md
    └── 001/                      ← активный Q001
```

## Что переехало в архив

Всё, что было журналом итераций, отчётом о разовом фиксе или планом уже
завершённого этапа, лежит в `docs/archive/NPC_quests_iterations/`.

| Папка | Содержимое |
|---|---|
| `roadmap_and_logs/` | `08_ROADMAP.md`, `ITERATIONS.md` |
| `editor_tools/` | `NPC_EDITOR_v2`, `DIALOGTREE_EDITOR_v2`, `CUSTOM_EDITOR_PLAN`, `UNIFIED_QUEST_GRAPH_PLAN`, `T-U01…T-U10` |
| `audits/` | `DEEP_AUDIT_2026-07-09`, `DEEP_AUDIT_2026-07-13` |
| `pipeline/` | `M19_CSV_PIPELINE_v2`, `M19_T7_DIALOG_CSV`, `T_Q19`, `ANALYSIS_QuestRewardItem_refactor`, `T-NPC-S23_activity_anchors` |
| `fixes/` | `T-Q22`, `T-Q28`, `T-QC7`, `T-QS2` |

Старая проектная документация v2 (навигация, архитектура, UI, input,
open questions) лежит в `docs/archive/NPC_quests_design_legacy/`.

## Текущее состояние

- Рабочая мировая сцена одна: `Assets/_Project/Scenes/World/WorldScene_0_0.unity`.
- Канонический именной NPC-префаб: `Assets/_Project/Prefabs/NPC/[Mira] - DON`T DELETE DEFOULT.prefab`. Не изменять.
- В `WorldScene_0_0` стоят 8 именных NPC (Mira, Onboarding alfa + шесть Q001) как экземпляры этого префаба.
- Q001 — активный квест-пакет: ассеты и связи готовы, финальная расстановка NPC/зон ещё ручная.
- Известный хвост: `mira_default.asset` и `MiraDefault.asset` делят `treeId: mira_default`; новые NPC так не делать.
- `merchants/` — пустой задел под будущие документы торговцев.

## Ссылки извне

`docs/gdd/` — read-only для агента. Часть GDD-ссылок ведёт на перемещённые
файлы (`08_ROADMAP.md`, `02_V2_ARCHITECTURE.md`, старые аудиты). Их нужно
поправить отдельным проходом с разрешения владельца GDD.
