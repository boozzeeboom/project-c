# 04 — Ассеты, данные, сборка

## 1. Пути (все — создаются генератором, руками не править)

| Ассет | Путь |
|---|---|
| Конфиг толпы | `Assets/_Project/Data/NPC/PrimumCrowdConfig.asset` |
| Аниматор-контроллеры | `Assets/_Project/Animation/NPC/PrimumCrowd_Female.controller`, `..._Male.controller` |
| Шаблон NPC | `Assets/_Project/Prefabs/NPC/PrimumCrowd/PrimumCrowdNpc_Template.prefab` |
| `NpcDefinition` резидентов | `Assets/_Project/Quests/Data/Npcs/PrimumCrowd/primum_crowd_0001.asset` … `primum_crowd_0100.asset` |
| Корень в сцене | `WorldScene_0_0 / WorldRoot_0_0 / PRIMUM_NPC_CROWD` (сцена одна, дублей нет) |

Скрипты: `Assets/_Project/Scripts/World/Npc/Primum/` (runtime), `Assets/_Project/Editor/Npc/` (сборка + аудит).

## 2. Генератор

Меню: **Tools/Project C/NPC/Primum Crowd**

| Пункт | Что делает |
|---|---|
| `0) Audit readiness (no Play Mode)` | без сборки и без Play Mode меряет готовность: дома/навмеш/модели/база/корень (см. `01_AUDIT §9`) |
| `1) Build T1 (100)` | полная сборка ассетов под тир 100 |
| `2) Rebuild All` | то же, но количество берёт из текущего конфига |
| `3) Place root in WorldScene_0_0` | идемпотентно ставит/обновляет `PRIMUM_NPC_CROWD` (открывает сцену, сохраняет, возвращается назад) |

Что делает `Build`:

1. создаёт **два** контроллера `PrimumCrowd_Female/Male` (1D blend: `Speed` 0→`Idle01`, 1→`Walk01_Forward`,
   клипы — саб-ассеты FBX Kevin Iglesias, humanoid-ретаргет);
2. собирает шаблон-префаб (Agent `enabled=false`, trigger-capsule, `NpcController`, `PrimumCrowdNpc`, TMP-плейт);
3. генерирует N `NpcDefinition` с детерминированными именами/репликами (seed = индекс);
4. создаёт/обновляет конфиг: каталог 106 моделей + шаблон + контроллеры + пулы + лимиты;
5. вызывает `QuestDatabaseAutoDiscover.Rescan()` (иначе база не увидит новые `NpcDefinition`);
6. **не трогает сцену** — корень ставится пунктом `3`.

## 3. Данные личности

Детерминированная генерация (одинаковый результат при повторной сборке):

- `npcId`: `primum_crowd_{index:D4}`
- `displayName`: `Имя Фамилия` из двух пулов (40×40 → 1600 комбинаций, без повторов внутри тира)
- роль: `City`, `Engineers`, `Medics`, `Guilds`, `Explorers`, `CreationChem`, `Pilots`, `Archetypes`
  (папка модели определяет роль → реплики роли)
- `greetingText`: 3 фразы — приветствие + наблюдение о городе/работе + личная деталь; шаблоны
  выбираются по (index, роль) без повторов подряд
- `faction = Neutral`, `interactionRadius = 3`, `services = None`

## 4. Каталог моделей

- В конфиг попадают все 106 FBX из `Assets/Generated_Models_Rigged_NPC` (отсортированы по пути).
- Модель на NPC: `index % 106` — при 100 NPC все модели разные, при 1000 — по кругу.
- Модель **не** префаб-ребёнок шаблона, а инстанс FBX в рантайме (`Instantiate`), потому что аватар у
  каждой модели свой, а контроллер — общий. Это единственная причина, почему шаблон один.

## 5. Масштабирование данных (T3+)

1000 `NpcDefinition` и 1000 инстансов моделей — уже перебор для авто-скана базы и памяти. План перехода:

- `NpcDefinition` остаются только у «горячих» NPC (сколько игрок реально может встретить),
  остальным идентичность считается из `PrimumCrowdConfig` (пулы) и создаётся в момент приближения
  (ленивая регистрация в `NpcController.Registry`).
- Модели — пул по 2–3 экземпляра на роль вместо инстанса на NPC.
- Материалы/текстуры — атлас по роли (сейчас 106 уникальных `URP/Lit`).

## 6. Идемпотентность

- Повторный запуск генератора **перезаписывает** контроллеры/шаблон/конфиг и **пересоздаёт** только
  файлы `primum_crowd_*`; чужие `NpcDefinition` (`mira_01` и др.) не трогаются.
  Пересоздание идёт через загрузку существующего ассета по `npcId` (GUID сохраняется), а не через
  `DeleteAsset` — иначе ссылки из конфига обнулились бы.
- Сцена не пересохраняется автоматически: корень ставится один раз и переиспользуется по имени.
- Аниматор-контроллеры создаются через `DeleteAsset` + `CreateAnimatorControllerAtPath` — на них
  никто, кроме конфига, не ссылается, поэтому GUID-смена безопасна (конфиг перезаписывается в том же прогоне).
