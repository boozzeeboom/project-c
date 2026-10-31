# T-CULL01 / Stage 3a (H2) — отчёт: HeavyII visual merge + LOD

**Дата:** 2026-10-31 · **База:** `79f26fdf` (HEAD на старте `4e406c93`) · **Scope:** только `Ship_havyII_base`.
Play Mode агентом не запускался; сцена не сохранялась; push не выполнялся.

## 1. Что сделано

- Новый Editor-тул `Assets/_Project/Editor/Ships/ShipVisualMerger.cs`, меню `ProjectC → Ships → Heavy Visual Merge …`
  (dry-run / apply P1+P2 / verify (FBX reimport) / rollback).
- **P1:** 378 greeble-узлов (`Bolt_/Mess_/UnderHull_` = 219 → ячейка `GrbFine`; `Interior_/Contour_/Det_` = 159 → `GrbDetail`)
  склеены по (ячейка, ассет материала, настройки рендерера) в 10 мешей. С узлов FBX сняты только MeshRenderer+MeshFilter
  (removed-component overrides в prefab-ассете); узлы, трансформы и коллайдеры на месте. Σtris 15 992 → 15 992 (0 %).
- **P2:** `LODGroup` на `SHIP_VISUAL_H2`: LOD0 = 557 рендереров (статика после P1), LOD1 = 10 мешей (без Bolt/Mess/UnderHull),
  LOD2 = 1 меш × 5 материалов (Steel/Bronze/Glass/Lamp_Red/Lamp_Green). Пороги 300 / 900 м (FOV 60, lodBias 1,
  подтверждено `LODUtility.CalculateDistance`), LOD2 без cull — решение пользователя «2500 = LOD2 до конца».
  При текущем Medium (lodBias 0.7) эффективные пороги ≈ 210 / 630 м.
- Вне LODGroup и без мержа: 9 дверей + `Door_AftGate_Stb` + `Ramp_Cargo`, `PilotSeat`, 2× `Slot_Engine`
  (EngineThrusterVisual/ShipPartShake), CargoVisual, ContrailVFX, 11 ModuleSlot, 250 коллайдеров, корневой dummy MR.
- `ShipContrailVfx`: закреплены `UseShipBounds=false`, `ManualBoundsSize=(72.216, 216.360, 9.720)` — это значение компонент
  вычислял сам до мержа (Det_SectionSeams); без закрепления склеенные меши сдвинули бы следы к корме. Согласовано с пользователем.
- `ShipPrefabRepair.cs`: при наличии `SHIP_VISUAL_H2` вместо абсолютной проверки 973 MR вызываются инварианты H2 (H1-R1 не ломается).
- 21 mesh-ассет: `Assets/_Project/Generated/Ships/HeavyII_VIS_*.asset`. Данные: `08_DATA/M_stage3_heavy_2026-10-31.csv`.

## 2. До / после (edit-mode модель; DC = слоты × (1 opaque + 2 каскада))

| | MR | слоты | DC upper |
|---|---:|---:|---:|
| до | 973 | 972 | 2 916 |
| после, LOD0 (≤300 м) | 616 | 604 | 1 812 |
| после, LOD1 (300–900 м) | — | 57 | 171 |
| после, LOD2 (>900 м) | — | 52 | 156 |

47 слотов (двери/движки/кресло) рисуются всегда, поэтому LOD1/LOD2 = 57/52, а не 40/5 из модели брифа.
Gate P2 «≤~1 300 DC» на инстансе у платформы (камера < 300 м → LOD0) этой моделью **не достигается** — нужен замер.

## 3. Проверки

- Σtris P1 и каждого меша LOD1/LOD2 совпадают с источниками; мировой AABB склейки = объединению исходных bounds.
- FBX ForceUpdate reimport → `Verify Heavy Visual Merge`: OK (порог LOD 300/900 м сохранён, 0 битых ссылок).
- H1-R1 (`Verify Heavy Prefab` + `Repair Heavy Prefab (apply)`): OK, 250/240/10 боксов, 9 дверей, 0 missing, файл не переписан.
- Medium / Light / NPC: файлы не менялись (mtime). Сцена: `isDirty=false`, overrides инстанса 14 (как в HEAD).

## 4. Play Mode — выполнено, результаты

Разбор замеров: `docs/dev/T-CULL01_STAGE3_PLAYMODE_2026-10-31.md`; сырые данные:
`08_DATA/M_stage3_heavy_runtime_2026-10-31.csv` (+ `…_run1.txt`, `…_run2.txt`). Кратко:

- `ship_dc` (A − B, Midday, far 30 000, fov 60): **1538** при полном корабле в кадре (200 м, LOD0),
  **1015** при 80 м (около половины корпуса вне фрустума), **235** на 400 м (LOD1), **104** на 1200 м (LOD2).
  Гейт P1 ≤~1800 — **выполнен**; P2 ≤~1300 при полном корабле — **не достигнут** (1538).
- Runtime `lodBias = 1.30` (Medium), то есть эффективные пороги **390/1170 м**, а не 210/630 м из §1.
- Двери/слоты/груз/VFX/250 коллайдеров целы (`Door_Mess`: open `progress=1.00`, `localMove_m=0.15`, close возвращается).
- **R0.2 подтверждён:** падение корабля без пилота/двигателя — ожидаемо (приземляется на `DECK` и стоит), но
  `ShipPositions.json` с сохранённой позицией свободного падения телепортировал корабль на ≈ −41 570 м
  (`run1`/`run2`, `groundDist=-1`, падение до −24.3 м/с). Запись исправлена на позицию покоя; сохранение перекрывает
  авторский hover 28.42 м над `DECK` — решение за пользователем.
- **F8/F9:** API-вызовы отбиты `no_rebase_plan`, 0 транзакций; автоматический ребейз на старте проходит штатно.
- **Frame Debugger:** 217 событий, состав проходов подтверждён; per-mesh имён нет, из bench-цикла FD
  в 6000.5 недоступен — снят вручную.

Осталось за пользователем: визуальная приёмка LOD-переходов, F8/F9 реальным вводом, решение по `ShipPositions.json`.

### Lore / rollback

- Lore: закоммитить `Ship_havyII_base.prefab` и `.meta` для `Generated/Ships/*.asset` и `ShipVisualMerger.cs`
  (`*.meta` и `*.prefab` в `.gitignore`; GUID мешей живут только в `.meta`).
- Rollback: `Rollback Heavy Visual Merge (from backup)` (бэкап `_local_backups/T-CULL01/Stage3/`), затем revert коммита.
