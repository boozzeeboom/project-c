# T-CULL01 / Stage 1 — brief для отдельного агента (H1: ShipPrefabRepair)

**Дата:** 2026-10-31 · **Проект:** `C:\Projects\ProjectC_client` · **Базовый коммит:** `b507a2a7`
**Статус:** Stage 0 принят с оговорками; Stage 1 — H1, можно начинать.
**Play Mode:** для H1 **не требуется**; запускать только с отдельным разрешением пользователя.
Скриншоты — с отдельным разрешением. Не сохранять сцену, не делать push.

**Прочитать до работы:**
- `C:\Projects\ProjectC_client\docs\dev\T-CULL01_COMPLEX_CODE_HANDOFF.md` — §2 (H1) и правила §0.
- `C:\Projects\ProjectC_client\docs\world\primum_city\10_CULLING_RESEARCH\10_EXECUTION_PLAN.md` — Stage 0 review и Stage 1.
- `C:\Projects\ProjectC_client\docs\world\primum_city\10_CULLING_RESEARCH\08_DATA\K_stage0_report.md` — контекст и R0.2.

---

## 0. Задача H1

Вынести **250 BoxCollider** и **9 `DoorController`** из **инстанса тяжёлого FBX** внутри префаба
`Ship_havyII_base.prefab` в обычные дети prefab-корня, чтобы **реимпорт FBX их не сносил**.

**Визуал не склеивать. Сцену не менять. Source FBX не менять.**

Почему: сейчас 238 из 250 боксов и все 9 дверей лежат внутри nested FBX-инстанса. При reimport модели
они пропадут/сломаются. Это не оптимизация DC, а устранение хрупкости пайплайна.

**Ожидаемое время:** 0.5–1 день. Если выясняется, что нужен unpack nested FBX / ручная пересборка
визуала — **остановиться и согласовать** (это уже H2-риск).

---

## 1. Абсолютные входы

- Префаб:
  `C:\Projects\ProjectC_client\Assets\_Project\Prefabs\Ships\Ship_havyII_base.prefab`
- FBX:
  `C:\Projects\ProjectC_client\Assets\_Project\Art\Models\Ships\HeavyII_base\project-c_ship_heavyII_1_cline_deepseek_1_glm-53_1_aura_1_game-ready_1.fbx`
- Скрипты:
  - `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Ship\DoorController.cs` — класс 32, `[RequireComponent]` 30.
  - `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Ship\ShipRootReference.cs`
  - `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Ship\ShipComponentLocator.cs`
  - `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Ship\ShipDeckNav.cs` — 26–30, 62, 67.
  - `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Player\ShipController.cs` — 39–41, 100–106, 461–462.
- Тулы-референсы:
  - `C:\Projects\ProjectC_client\Assets\_Project\Editor\BuildSettlementColliders.cs` — shipMode 34–35, 89, 160, 268–269, 1438.
  - `C:\Projects\ProjectC_client\Assets\_Project\Editor\ShipPresetCreator.cs` — 40, 43, 181, 526, 578–589.
  - `C:\Projects\ProjectC_client\Assets\_Project\Editor\CombineMeshesToCollider.cs` — **не использовать как есть** (`convex=false` :172, один меш :114).
- Выход скрипта:
  `C:\Projects\ProjectC_client\Assets\_Project\Editor\Ships\ShipPrefabRepair.cs` (папка новая; `.meta` создаст Unity).
- Отчёт/CSV:
  `C:\Projects\ProjectC_client\docs\world\primum_city\10_CULLING_RESEARCH\08_DATA\H1_prefab_repair.csv`

**Git-важно:** `Ship_havyII_base.prefab` и FBX **не в git** (`*.prefab`, `*.fbx` — `.gitignore:153–154`).
Поэтому правки префаба **не попадут в git-коммит**; в коммит идут только Editor-скрипт и CSV/отчёт.
Сделать локальный бэкап префаба вне Assets:
`C:\Projects\ProjectC_client\_local_backups\T-CULL01\H1\Ship_havyII_base.prefab.bak`
(папку не коммитить).

---

## 2. Текущее состояние (проверено)

- Тяжёлый префаб: **973 MeshRenderer / 973 slot / 250 Collider / 1 452 Transform**;
  FBX heavy = 936 Model / 935 Mesh / 37 128 тр. / 10 материалов.
- **238 из 250** BoxCollider — внутри nested FBX-инстанса; 10 — под корнем FBX; 2 — под корнем префаба.
- **9 `DoorController`** висят на узлах nested FBX: `Door_AftGate_Port`, `Door_Bridge_1`, `Door_Bridge_2`,
  `Door_Guest`, `Door_Landing`, `Door_Medbay`, `Door_Mess`, `Door_Nav`, `Ramp_Cargo`.
- 2 `MeshCollider` (тестовые/двигатели) — **не трогать**.
- В сцене `Ship_havyII_base` может падать при Play Mode (R0.2) — это **не задача H1**; не исправлять здесь.

---

## 3. Что сделать

1. **Dry-run-отчёт:** обойти префаб, найти nested prefab instance(s), собрать:
   путь, тип, локальную/мировую позу, parent, компоненты для 250 коллайдеров и 9 дверей.
   Сохранить в `H1_prefab_repair.csv` (до изменений).
2. Создать `ShipPrefabRepair.cs` с меню:
   `ProjectC/Ships/Repair Heavy Prefab (dry-run)` и `... (apply)`.
3. **Apply:**
   - создать под prefab-корнем `SHIP_COLLIDERS` и `Doors`;
   - перенести 250 `BoxCollider` в `SHIP_COLLIDERS` (сохранить center/size/rotation, слой, isTrigger);
   - перенести 9 дверей в `Doors`, сохранив имена, `DoorController`-настройки, `ShipRootReference`,
     визуальные ссылки (если меш двери — саб-ассет FBX, использовать ссылку, не копировать геометрию);
   - удалить/отключить оригиналы внутри nested FBX-инстанса **без unpack визуала**; если Unity не позволяет
     это сделать без unpack — **остановиться** и отчитаться (не делать unpack без согласования);
   - не дублировать: в префабе должно быть ровно 250 боксов и 9 `DoorController`;
   - префаб сохранить через `PrefabUtility.SavePrefabAsset`; сцену не сохранять.
4. **Проверка reimport:** `AssetDatabase.ImportAsset(FBX, ForceUpdate)`; повторно проверить:
   250 боксов, 9 дверей, 0 missing refs, `DoorController` на месте; визуал 973 MR не изменился.
5. **Проверка prefab/scene:** открыть/перечитать `Ship_havyII_base.prefab`; сцена `WorldScene_0_0`
   не должна получить diff (если сцена автоматически обновилась — **не сохранять**, отчитаться).
6. **Rollback:** `PrefabUtility.RevertPrefabInstance`/замена из backup; описать в отчёте.

---

## 4. Приёмка H1

- [ ] в префабе **250 BoxCollider** и **9 DoorController**, все — **вне** nested FBX-инстанса;
- [ ] нет дублей и missing references; `ShipRootReference`/`ModuleSlot`/`PilotSeat`/`CargoVisual`/VFX целы;
- [ ] `DoorController` открывается/закрывается (проверка в Edit Mode или Play Mode — по разрешению);
- [ ] reimport FBX **не теряет** коллайдеры/двери;
- [ ] визуал: **973 MR** до/после, 0 изменений мешей/материалов;
- [ ] сцена `WorldScene_0_0` **не сохранена**, `git diff` по сцене пуст;
- [ ] CSV `H1_prefab_repair.csv` заполнен до/после;
- [ ] коммит (если разрешён): `T-CULL01/H1: ship prefab repair` — **только** Editor-скрипт + CSV/отчёт;
      prefab не коммитится (Lore); push не делать.

**Kill / stop conditions:**
- без unpack nested FBX перенос невозможен → stop, отчёт Lead;
- ломаются `DoorController`/`ShipRootReference`/визуал → rollback;
- reimport теряет результаты → не считать H1 выполненной;
- сцена получает diff >0 строк → не сохранять, отчёт.

---

## 5. Правила

1. Только H1; не начинать H2 (мерж визуала), не трогать город/двери/URP/ProjectSettings.
2. Не сохранять сцену; не запускать Play Mode без разрешения; не делать скриншоты без разрешения.
3. Не менять source FBX; не делать unpack визуала без согласования; не удалять 2 `MeshCollider`.
4. Не коммитить prefab/FBX (их нет в git); backup — вне Assets.
5. В отчёте — absolute paths, `git diff --stat`, CSV, результаты reimport, rollback-план.

---

## 6. Что сдать Lead

Короткий отчёт:
- commit hash (если был);
- до/после: коллайдеры внутри FBX / вне; 9 дверей; 973 MR; missing refs;
- reimport-результат + путь к CSV;
- `git status` (сцена чистая, prefab вне git),
- что осталось непроверенным;
- подтверждение, что сцена не сохранена.
