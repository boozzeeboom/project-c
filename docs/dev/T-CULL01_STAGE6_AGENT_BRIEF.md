# T-CULL01 / Stage 6 — brief для отдельного агента (`VisibilityDirector`, H6)

**Дата:** 2026-11-01 · **Проект:** `C:\Projects\ProjectC_client` · **База:** `09f88ec5`
**Статус:** Stage 6 **утверждён пользователем как фундамент**, не опциональный. Текущий реальный
baseline: **P1 2 394 DC** (корабли 2 406), F8/F9 PASS, визуально принято.
**Scope:** VisibilityDirector как оркестратор видимости/бюджетов. Stage 7/HLOD и B2 — **не начинать**.
Корабли не оптимизировать; при помехах — runtime-выключение рендереров, без prefab/сцены.
**Play Mode:** нужен для замеров; только с разрешением пользователя. Сцену не сохранять, push не делать.

**Прочитать:**
- `docs/world/primum_city/10_CULLING_RESEARCH/10_EXECUTION_PLAN.md` §8
- `docs/dev/T-CULL01_COMPLEX_CODE_HANDOFF.md` §7
- `docs/dev/T-CULL01_STAGE4C_REPORT.md` (текущий фактический baseline)
- `docs/dev/T-CULL01_STAGE2_REPORT.md` / `T-CULL01_STAGE3C_REPORT.md` (как устроены бюджеты)

---

## 0. Цель Stage 6

Сделать **фундамент** управляемой видимости на будущее:
- единый слой поверх GRD/существующих бюджетов: группы/ячейки, мультикамерный union, политики по классам;
- не дублировать `CityShadowBudget`, `DoorVisibilityBudget`, `ShipShadowBudget`, `DetailDistanceCuller`,
  а **оркестрировать** их (реестр/провайдеры/ownership), чтобы не было двойных `Renderer.enabled`;
- FO-safe, 2–4 Гц, гистерезис 10–15 %, culling ≤2 мс;
- **DC не хуже 2 394**; измеримый выигрыш — хорошо, но «−60…−80 %» — stretch-цель, не ломать визуал.

---

## 1. Что уже есть (не ломать)

| Компонент | Что делает | Владелец |
|---|---|---|
| `CityShadowBudget.cs` | тени города off >200 м, 1 301 entry, камеры только «видящие мир» | Stage 4b/4c |
| `DoorVisibilityBudget.cs` | `layerCullDistances[10]=150`, тени дверей | Stage 2 |
| `ShipShadowBudget.cs` | тени heavy-корабля off >150 м | Stage 3c |
| `DetailDistanceCuller.cs` | дистанции руин/деталей | Stage 0 |
| `PrimumLightPool.cs` | пул ламп | ранее |
| GRD (`BrgStripping=2`, mode=1) | hybrid batching | Stage 4 |

**Требование:** перед Stage 6 составить **ownership map** (кто чем владеет); Stage 6 не должен
включать/выключать то же, что уже делает существующий бюджет, без явной передачи владения.

---

## 2. Что сделать

### 2.1. VisibilityDirector (MVP)

Новый `C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\VisibilityDirector.cs`:
- иерархия групп: мир → чанк → улица/дом → двери; отдельные группы для кораблей/интерьеров/ламп/руин;
- **мультикамерный union:** только реальные камеры, которые рисуют мир:
  - включать `ThirdPersonCamera_0` + `WorldCamera` (far 1e6, если активна);
  - **исключать** `AtmosphereCamera` (mask только атмосферы) и парковочную `MainCamera` (344 км);
  - проверка: `cullingMask & worldLayer` и `ReachesCity(cam)` (как в Stage 4c);
- политики по группам: дистанция, `Renderer.enabled`/`forceRenderingOff`, `shadowCastingMode`
  (только если группа ещё не под бюджетом); гистерезис 10–15 %, 2–4 Гц;
- FO: живые Transform/bounds; после F8/F9 (**success и rollback**) — сброс кэшей/пересчёт;
- не трогать `DoorController`, коллайдеры, `PrimumLightPool`, сеть/физику/VFX; не `SetActive`.

### 2.2. Интеграция бюджетов

- Реестр провайдеров: `CityShadowBudget`, `DoorVisibilityBudget`, `ShipShadowBudget`,
  `DetailDistanceCuller` регистрируются в директоре; директор только координирует камеры/группы,
  не переписывает их политику.
- Если владение пересекается — **остановиться и согласовать**; не делать двойной toggle.
- `VisibilityDirector` должен уметь работать при отключённых провайдерах (fallback — текущее поведение).

### 2.3. Замеры

- Play Mode Host, Midday, far 30 000, fov 60, текущие настройки; корабли можно выключать для city-точек.
- C1/площадь, C2/улица, C3/крыша, PILOT чанк; P1 repeat.
- Метрики: DC/SetPass/tris/shadowCasters/hybridDC; **culling ms** (Profiler/`ProfilerRecorder`);
  группы/видимые/выключенные; камеры в union.
- F8/F9 реальным вводом: 0 errors, объекты/бюджеты на месте.
- Сцена не сохранена; prefab/FBX/корабли 0 diff; 0 shader errors.

---

## 3. Приёмка Stage 6

- [ ] VisibilityDirector работает: группы/union/гистерезис/FO; камеры корректны (Atmosphere исключена);
- [ ] существующие бюджеты не дублируются, ownership map в отчёте;
- [ ] **culling ≤2 мс** (MVP ≤0.7 мс — цель);
- [ ] **DC не хуже 2 394**; измеримый выигрыш зафиксирован (если есть);
- [ ] визуал без артефактов; двери/корабли/тени целы; F8/F9 PASS;
- [ ] сцена не сохранена; `git status` чистый; commit `T-CULL01/H6`; отчёт `T-CULL01_STAGE6_REPORT.md`.

**Kill/stop:** артефакты/двойные toggle → урезать гранулярность (двери/руины/интерьеры); culling >2 мс →
упростить; DC хуже 2 394 → откат Stage6-влияния, оставить фундамент в отчёте.

---

## 4. Абсолютные входы

- `C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\CityShadowBudget.cs`
- `C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\DoorVisibilityBudget.cs`
- `C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\ShadowBudget.cs`
- `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Ship\ShipShadowBudget.cs`
- `C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\DetailDistanceCuller.cs`
- `C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\PrimumLightPool.cs`
- `C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\FloatingOrigin\Network\GlobalMotionControlledRebaseSlice.cs`
  (265–418, 326, 1345, 1349–1354), `GlobalMotionActorContract.cs` (10–15)
- `C:\Projects\ProjectC_client\Assets\_Project\Editor\Culling\FinalValidationBench.cs` (переиспользовать подход)
- Сцена: `Assets/_Project/Scenes/World/WorldScene_0_0.unity`; URP/GRD не менять.
- Отчёт/CSV: `docs/dev/T-CULL01_STAGE6_REPORT.md`, `08_DATA/S_stage6_<date>.csv`.

---

## 5. Жёсткие правила

1. Только Stage 6; Stage 7/HLOD/B2 не начинать; корабли не оптимизировать.
2. Не менять prefab/FBX/сцену; сцену не сохранять; не push.
3. Не менять `ProjectSettings`/URP-ассет; GRD/тени как после Stage 4c.
4. Не `SetActive`, не `Camera.main`, не scene-overrides; FO-safe.
5. Если нужен рефакторинг существующих бюджетов — сначала ownership map и согласование, не «по пути».
