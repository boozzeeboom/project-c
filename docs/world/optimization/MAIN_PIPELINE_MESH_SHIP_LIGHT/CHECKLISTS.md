# CHECKLISTS — быстрые чек-листы контента ×100

Короткие рабочие списки. Полные правила и числа — в [`README.md`](README.md); доказательства — в `_evidence/`.

---

## 0. Перед любым ассетом (общее)

- [ ] Ассет лежит **под активным корнем** `WorldScene_*` (`WorldRoot_0_0/...`), слой правильный (8 — город/свет, 10 — двери), `static = 0`.
- [ ] Нет `NetworkObject`? → только FO-сдвиг. Есть → **FO-каталог**: маркер + observation + entry + хэши + digest + непустой `reviewNote`.
- [ ] Никаких static-флагов/Batching/ContributeGI/Occluder; `.meta`/`.asmdef` руками не правились.
- [ ] Мерж только статики, edit-time, результат в `.asset` (git); runtime `CombineMeshes` нет.
- [ ] Не пишутся чужие свойства: `forceRenderingOff` (директор), `shadowCastingMode` (бюджеты), `enabled` (пул/директор), `intensity` (ALIC).
- [ ] Материалы — из палитры (`M_PC_*`/`PM_*`/`MF_*`), шейдер Shader Graph / URP-Lit / URP-Unlit; без `MaterialPropertyBlock`/`.material`.
- [ ] Сцена `isDirty=false`, изменены только ожидаемые файлы; 0 errors/exceptions/shader errors.

---

## 1. Корабль

**Класс:** S <30 м · M 30–80 м · L 80–200 м · XL >200 м (HeavyII 324.9 м — эталон).

- [ ] `Tools → Project C → Create Ship Preset` (класс) → заменить `MainVisual`, root scale = 1, tag `Ship`.
- [ ] Коллайдеры вынесены из FBX в `SHIP_COLLIDERS` (дети префаба); двери — `DoorController` + `Collider` на узлах створок.
- [ ] `Repair Heavy Prefab (dry-run/apply)` → `Verify (FBX reimport)`: 0 missing, 9 дверей / 250 боксов / 11 слотов целы.
- [ ] `Heavy Visual Merge (dry-run/apply P1+P2)`: greebles `Bolt_/Mess_/UnderHull_` + `Interior_/Contour_/Det_`; Σtris источника = Σtris склейки.
- [ ] LODGroup для M/L/XL: пороги от `worldSize`, **не ближе 0.866×worldSize**; LOD1 — без мелкой greeble; LOD2 — 1 меш ≤5 сабмешей.
- [ ] `ShipShadowBudget` для L/XL: Apply → Verify (параметры, инвентарь, `Off` в префабе = 0, расхождений с FBX = 0).
- [ ] `ContrailVfx.UseShipBounds=false` + `ManualBoundsSize` (после мержа).
- [ ] Бюджет: S ≤30 DC · M ≤75 · L ≤180 · XL ≈1 170 DC; одновременно тяжёлых ≤1.
- [ ] FO-каталог (корабль — корень сцены, `NetworkObject`).
- [ ] Замер: `Stage 3 Ship Bench` 80/200/200-repeat/300/400/1200 м; 200 м ≤1 300, 300 м ≤1 300, повтор ±5 %.
- [ ] Целостность: 9 `DoorController` open/close, 250 `BoxCollider`, 11 `ModuleSlot`, `PilotSeat`, `CargoVisual`, `ContrailVFX`; F8/F9.
- [ ] ❌ Не мержить двери/створки/коллайдеры/интерактив/VFX; ❌ не пытаться LOD0→LOD1 <281.6 м; ❌ не править `ShipPositions.json`.

---

## 2. Дом / ферма

**Масштаб:** 1 дом ≈3.4 DC · 5 домов ≈17 DC · 25 ≈85 DC · 250 ≈850 DC (43 % города). Кластер ≤150–200 DC (45–60 домов).

- [ ] Геометрия: join `(группа × материал)`, cap **60 000 верт./меш**, `mergeSubMeshes=false`, `RecalculateBounds()`; ≤8 материалов/юнит.
- [ ] `Build Settlement Colliders…` (Dry Run → Build → Merge): `minWorldSize 0.25`, `minThickness 0.5`, `mergeGap 0.1`, `shipMode=false`.
  Коллайдеры — боксы в `<Root>_Colliders`, **не на узлах FBX**; `convex=true` на статике запрещён; ≈21 бокс/дом (≤30).
- [ ] Двери — **инстансы 12 префабов**, слой 10 у корня и детей, `DoorController` жив; 1–7 дверей/дом (среднее 4.65), >10 — дубли.
- [ ] `Door Layer → Apply` → проверка: 5 493 рендерера на слое 10, 0 на 8.
- [ ] Двери: `аудит проёмов (CSV)` → `расставить одиночные (префабы)` → `проверить посадку (verify)`; корень `PRIMUM_DOORS/<имя>` = имя HLOD-блока.
- [ ] Тени: новый корень **не попал одновременно** в `CityShadowBudget` (200/180) и `ShadowBudget` (50/60) — иначе мигание в 60–200 м;
  `CityShadowBudget.entries` вырос ровно на число новых рендереров (было 1 301).
- [ ] Свет: только реальные приборы, `PRIMUM_FARMLIGHT_*`; на 250 домов ≤50–80 ламп, остальное emissive/HLOD.
- [ ] HLOD: если контент под `PRIMUM` → `Bake All Blocks + L2` + `Rebake Determinism Check`; **фермам HLOD пока нет** — при >3 км они остаются в L0.
- [ ] Зоны: перед масштабированием — сузить `pollLayerMask` у `OuterCommZone/MarketZone` (пилы 82.93 мс/полл на ферме).
- [ ] Регресс: P1 2 229 ±5 %, L1 46 ±5 %, L2 2, двери >150 м = 0, `hybridDC > 0`, F8/F9.
- [ ] ❌ Не клеить двери/коллайдеры/свет; ❌ не «1 меш на ферму»; ❌ не дробить по дому/ячейкам 10–30 м; ❌ не тащить 7 841 коллайдер без проверки зон.

---

## 3. Свет

- [ ] Лампа ставится **только на реальный прибор** (корпус уже в геометрии); точка = центр `GLOW/BULB/LAMP/LANTERN/HEAD/GLASS/SHADE`,
  подвес — нижний конец `CORD/CHAIN/ROD`; дубликаты <1 м — один прибор.
- [ ] Префаб `PM_LIGHT_*` (Light-only): Point, `range` 3.5–14 м (**никогда >20**), `intensity` 1.0–3.0, `shadows=None`,
  Realtime, `enabled=false`, слой 8, `static=0`.
- [ ] Имя: `PRIMUM_OUTLIGHT_<key>` / `PRIMUM_INLIGHT_<key>` / `PRIMUM_FARMLIGHT_<key>` — иначе catch-all (множитель 1 вместо 20).
- [ ] Группа ALIC: новое семейство — добавить группу **выше** catch-all в `DayNightController` (порядок важен), множитель — одно число, кривая — доля 0…1.
- [ ] Плотность: 4–8 ламп на жилой дом, ≤35 на ADMIN-здание; ферма 5 домов ≤20–30, ферма 250 домов ≤50–80.
- [ ] Мир: **≤4–5 тыс. ламп** без чанк-индекса (5 тыс. → чек 3.4–7.1 мс, спайки); 10 тыс. — только с индексом; 224 тыс. — невозможно.
- [ ] Проверка: `Свет: проверить (verify)` → 0 не-`PM_LIGHT_*`, 0 static, 0 теней, слой 8, 0 дубликатов, emissive peak 2.40.
- [ ] Инварианты: `FocusCamera=ThirdPersonCamera_0`, `intensityZeroActive=0` в 12 ч, `activeOUT` 40–46 вечером, `HeldForFade=0`, `GateGraceHeld=0`, `dc` ±1 %.
- [ ] ❌ Тени на доп. лампах; ❌ light layers/`cullingMask`/per-object 4; ❌ `maxActive` 128+ «чтобы стало видно»; ❌ fade-out; ❌ HDR/bloom от emissive; ❌ лампы на кораблях/NPC «для красоты».

---

## 4. Новый блок города / HLOD

- [ ] Новый квартал = **новый ребёнок `PRIMUM`**, имя `PRIMUM_<TYPE>_S<n>_<ZONE>_P<m>_<NN>H`, совпадает с `PRIMUM_DOORS/<то же имя>`.
- [ ] Футпринт 26–228 × 156 м; ≤80 k тр.; ≤65 535 верт./меш; join «блок × материал»; интерьеры — отдельно; мелочь <0.25 м вырезана.
- [ ] `PRIMUM_DOORS/<блок>` существует, двери на слое 10.
- [ ] `Stage 7 HLOD → Bake All Blocks + L2` → `Rebake Determinism Check` = `DETERMINISTIC; mismatches=0`.
- [ ] Лимиты: блоков ≤200–250 (L1 = 2 DC/блок, гейт ≤500); сцена ≤100 МБ; корней сцены ≤~100.
- [ ] Контент, спрятанный в редакторе (`enabled=false`), в L1 не попадёт — «исчезнет» на 3–12 км: всё, что должно жить в L1, включено.
- [ ] Не переименовывать `PRIMUM`, `Primum_farms`, `Secund/Tertius/Quartus`, `WorldRoot_0_0`, `PRIMUM_DOORS`, `*_DOORS`, `*_LIGHTS`, `PRIMUM_HLOD`.
- [ ] ❌ Не добавлять второй HLOD-корень/манифест/сцену-оверрайды; ❌ не гасить контент через `SetActive`.

---

## 5. Перед коммитом (регресс)

- [ ] Console: 0 errors / 0 exceptions / 0 shader errors.
- [ ] `T-CULL01 Acceptance → Probe`: свет-инварианты + двери (<130 м видны, >150 м = 0).
- [ ] `Stage 8 Doors → Run Bench`: P1 **2 229 ±5 %**, L1 **46 ±5 %**, L2 **2**, двери >150 м = **0**, `hybridDC > 0`, `srpBatcherDC` мал, F8/F9 PASS.
- [ ] Сцена не сохранена (`isDirty=false`), `git status` — только ожидаемые файлы; `git diff` просмотрен.
- [ ] HLOD (если город) перепечён и закоммичен: `Generated/HLOD/**` + `.meta` + манифест.
- [ ] FO-каталог (если `NetworkObject`) пересчитан: Host стартует, `AuditGlobalSceneCatalog` без новых расхождений.
- [ ] Отчёт/коммит с тикетом; **push не делать**.
