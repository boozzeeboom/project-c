# T-CULL01 / Stage 8 — brief: L0 door culling under GRD (H8)

**Дата:** 2026-11-01 · **Проект:** `C:\Projects\ProjectC_client` · **База:** `6690d7f1`
**Scope:** вернуть дистанционный culling **дверей на L0 (<3 км)** под GRD. Stage 7/L1/L2 не ломать;
корабли/medium/light/NPC не трогать; B2/Stage5 не делать.
**Play Mode:** нужен для замеров; только с разрешением пользователя. Сцену не сохранять, push не делать.

**Прочитать:**
- `docs/dev/T-CULL01_STAGE7_REPORT.md` §2.2, §9
- `docs/dev/T-CULL01_STAGE6_REPORT.md` (ownership map)
- `docs/world/primum_city/10_CULLING_RESEARCH/10_EXECUTION_PLAN.md` §9.1
- `docs/dev/T-CULL01_COMPLEX_CODE_HANDOFF.md` §9 (Stage 8)

---

## 0. Проблема

Stage 2 вынес двери на слой 10 и поставил `Camera.layerCullDistances[10] = 150 м`.
Stage 4 включил GRD; его `InstanceCuller` смотрит **только `cullingLayerMask`** и **игнорирует
`layerCullDistances`** (`InstanceCuller.cs:359–363`). Поэтому:

- на L0 (<3 км) двери рисуются **на любой дистанции**: ≈25 000 инстансов, ≈**137 DC**, ≈1 млн тр;
- Stage 7 скрывает двери только на L1/L2 (вместе с блоком), но не на L0;
- визуально это не всегда заметно (двери мелкие), но по DC/инстансам это заметный остаток.

Текущий честный baseline: **P1 2 341 DC** (HLOD on = off), L1 46–48 / L2 2.

**Цель:** вернуть дистанционный culling дверей на L0, не меняя `DoorController`, коллайдеры,
меши и логику открытия/закрытия.

---

## 1. Варианты (проверить по порядку)

### A. Политика директора/бюджета для дверей (предпочтительно)

- В `VisibilityDirector` уже есть группы дверей и `forceRenderingOff`; расширить политику:
  для L0-группы дверей гасить рендереры дальше **150 м** (гистерезис 10–15 %, 2–4 Гц).
- Владелец — **один**: либо `VisibilityDirector`, либо `DoorVisibilityBudget` (не оба).
  `DoorController`/коллайдеры/`shadowCastingMode`/`layerCullDistances` не трогать.
- Проверить, что гашение группы не ломает открытие/закрытие двери и `DoorController` корутины.

### B. Opt-out дверей из GRD

- Исключить дверные рендереры из GRD (если API позволяет), чтобы `layerCullDistances[10]` снова
  работал; сравнить DC/инстансы с A.
- Риск: двери могут выпасть из hybrid-батчинга и дать больше DC — измерить.

### C. Гибрид

- Оставить двери в GRD, но гасить **самые дальние** группы/инстансы через директор; порог подобрать
  по DC и визуалу.

**Рекомендация:** A, если API директора позволяет без двойного toggle; B — как контроль.

---

## 2. Замеры

- Play Mode Host, Midday, far 30 000, fov 60; корабли можно выключить для city-точек.
- P1/площадь, C2/улица, C3/крыша; L1 10 км, L2 20 км — проверить, что Stage 7 не сломан.
- Метрики: DC/SetPass/tris/shadowCasters/hybridDC/hybridInstances; двери в кадре/вне 150 м.
- **Gate:** P1 **≤2 341** (лучше ниже); двери за 150 м не рисуются на L0; L1 46–48 / L2 2 не хуже;
  F8/F9 PASS; 0 errors; сцена чистая.
- Визуал: двери вблизи открываются/закрываются, нет pop-in на 140–160 м; ночь — двери/тени.

---

## 3. Приёмка

- [ ] L0-двери за 150 м не рисуются (count/instances подтверждают);
- [ ] `DoorController` open/close работает; 2 630 дверей / 5 493 рендереров не сломаны;
- [ ] P1 ≤2 341; L1 46–48 / L2 2 не хуже; GRD Hybrid Batch Group на месте;
- [ ] F8/F9 PASS; 0 shader errors; сцена не сохранена; prefab/FBX/корабли 0 diff;
- [ ] отчёт `docs/dev/T-CULL01_STAGE8_REPORT.md`, CSV `V_stage8_<date>.csv`, commit `T-CULL01/H8`;
- [ ] после Stage 8 — финальная приёмка T-CULL01 (закрытие).

**Kill/stop:** двери перестают открываться/закрываться → откат; DC хуже 2 341 → откат;
pop-in заметен → подобрать порог/гистерезис, не ломать визуал; L1/L2 сломаны → откат.

---

## 4. Абсолютные входы

- `C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\VisibilityDirector.cs`
- `C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\DoorVisibilityBudget.cs`
- `C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\CityShadowBudget.cs`
- `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Ship\DoorController.cs`
- `C:\Projects\ProjectC_client\Assets\_Project\Editor\Culling\FinalValidationBench.cs`
- `C:\Projects\ProjectC_client\docs\world\primum_city\10_CULLING_RESEARCH\08_DATA\U_stage7_*`
- `C:\Projects\ProjectC_client\Assets\_Project\Scenes\World\WorldScene_0_0.unity` — без сохранения без разрешения.

---

## 5. Жёсткие правила

1. Только L0-двери; Stage 7/L1/L2, B2, корабли/medium/light/NPC не трогать.
2. Не менять prefab/FBX/сцену; не сохранять сцену; не push.
3. Один владелец `forceRenderingOff`; не дублировать `DoorVisibilityBudget`/`VisibilityDirector`.
4. `DoorController`/коллайдеры/меши/`layerCullDistances` не менять; FO-safe.
5. Световой коммит/систему света не трогать (отдельная тема).
