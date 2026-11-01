# T-CULL01 / Final Acceptance — brief для отдельного агента (закрытие тикета)

**Дата:** 2026-11-01 · **Проект:** `C:\Projects\ProjectC_client` · **База:** `3454171f` (обновить на HEAD при запуске)
**Scope:** **только финальная приёмка и закрытие T-CULL01.** Никакой новой оптимизации, Stage 6/7/8
не переделывать, корабли/свет/B2 не трогать.
**Play Mode:** нужен; только с разрешением пользователя. Сцену не сохранять, push не делать.

> **Актуализация 2026-11-01 (после закрытия светового трека L1–L6).**
> Световой трек (`T-LIGHT16…T-LIGHT21`) закрыт и принят пользователем: `PrimumLightPool` теперь
> day/night-гейт + split OUT/IN + radius 100 + фокус-камера L3 + веер L4 (`halfH/halfV` из FOV,
> `R_rear 28`, `score = d + 1.2·угол`) + fade в ALIC. Это меняет только **набор включённых**
> источников и CPU, **DC не меняется** (веер: DC совпал во всех парах L4/L5), поэтому критерии
> приёмки ниже остаются прежними. Не откатывать свет-код и не перезаписывать сцену; если свет
> мешает замеру — только A/B-конфиг в памяти пула (`ApplyTuneConfig`/`ApplyConeConfig`), без правок сцены.

**Прочитать:**
- `docs/dev/T-CULL01_STAGE8_REPORT.md` (последний этап)
- `docs/dev/T-CULL01_STAGE7_REPORT.md`
- `docs/world/lights/LIGHT_CULLING_PLAN.md` §0–§3 (итоговое состояние света)
- `docs/world/lights/T-LIGHT21_REPORT.md` (последний этап света, fade)
- `docs/world/primum_city/10_CULLING_RESEARCH/10_EXECUTION_PLAN.md` §9.1–9.2
- `docs/dev/T-CULL01_COMPLEX_CODE_HANDOFF.md` §8.5

---

## 0. Финальное состояние

| Метрика | Значение |
|---|---:|
| L0 P1 (реальная игра, меню скрыто) | **2 229 DC** |
| Двери L0 дальше 150 м | **0** (контроль 2 633) |
| L1 город | **46 DC** |
| L2 город | **2 DC** |
| culling | ≈0.8–1.0 мс (≤2) |
| директор, тик | ≈0.9 мс средний, пик ~4–6.7 мс на телепорте |
| F8 / F9 | PASS (`PASS_rollback_restore`) |
| Ошибки | 0 / 0 shader |
| Визуал | принят пользователем (Stage 7/8) |
| Свет (L1–L6) | **закрыт и принят**: ALIC 6.81 → 0.16 мс/кадр, веер (разворот меняет набор 6/6, «за спиной» → 0–1), fade-in 0.15 с; **DC не менялся** ни на одном этапе |

**Цель:** подтвердить, что это состояние воспроизводится в обычной игре, компоненты включены
по умолчанию, документы/ранбук закрыты, T-CULL01 можно закрыть.

**Свет на момент приёмки (не переделывать, только проверить, что дефолты на месте):**
`dayGate=on (eps 0.002)`, `split=on (outMin 40)`, `radius 100`, `cap 96`, `hysteresis 0.10`,
`checkInterval 0.25`, фокус — камера с фильтрами (`FocusCamera=ThirdPersonCamera_0`, `FocusSource=camera`),
веер из FOV + `R_rear 28` + `score = d + 1.2·угол`, `margin 6°` / `pre 5°`, `fadeInSeconds 0.15`,
`fadeOutSeconds 0` (fade-out отклонён по гейту удержаний), грейс `dayGate` 0.4 с.
Световые бенчи (`T-LIGHT16…21`) **не перезапускать** — они уже приняты; в приёмке достаточно
структурных инвариантов ниже.

---

## 1. Задачи

### 1.1. Runtime acceptance

- Запуск игры **обычным путём** (главное меню → «Одиночная игра»), не через старый legacy-путь.
- Проверить:
  - P1 **2 229 ±5 %**; повтор; L1 10 км **46 ±5 %**; L2 20 км **2**;
  - `doorsDrawnBeyond150 = 0`; `doorsForcedOff` корректен; двери <130 м видны;
  - GRD `hybridDC > 0`, `srpBatcherDC` мал; L1/L2 при переключении 3/12 км;
  - F8/F9 реальным вводом; `restore=True`; объекты/двери/HLOD на месте.
  - свет (инварианты, без световых бенчей): `FocusCamera=ThirdPersonCamera_0`, `FocusSource=camera`;
    `intensityZeroActive = 0` днём (12 ч), `activeOUT` 40–46 вечером; `active` в пределах 30–72 (поза зависит);
    `HeldForFade = 0` и `GateGraceHeld = 0` в установившемся режиме; появление лампы плавное (fade-in 0.15 с);
    `dc` не изменился относительно базы Stage 8 (свет DC не трогал).
- 0 errors/exceptions/shader errors; сцена `isDirty=false`; prefab/FBX/корабли 0 diff.
- Визуально: двери вблизи, pop-in 130–150 м, L1/L2 силуэт, ночь — пользователь уже принял;
  при повторной проверке зафиксировать.

### 1.2. Defaults / bootstrap

- Убедиться, что без ручных действий в Play Mode поднимаются:
  - GRD (`BrgStripping=2`, `GPUResidentDrawerMode=1`, occlusion=1);
  - `CityShadowBudget` (200/180 м, 1 301 entry);
  - `VisibilityDirector` (1 000 групп, двери 150 м, HLOD L1/L2);
  - HLOD-манифест/ассеты (L1 23 блока, L2).
- Записать в отчёт: какие компоненты auto-bootstrap, какие требуют инструментов/ассетов.
- Не менять настройки; только чтение/логи.

### 1.3. Runbook для чистого клона/нового контента

Обязательно зафиксировать:
- `TagManager` слой 10 `PRIMUM_DOORS` (в git);
- дверные префабы на слое 10 — **вне git (Lore)**, инструмент `Project C → Culling → Door Layer → Apply`;
- `Ship_havyII_base.prefab` (коллайдеры/двери/HLOD) — **вне git (Lore)**, инструменты ремонта/бейка;
- HLOD `.asset`/материалы/шейдеры — в git; manifest `Resources/HLOD/PRIMUM_HLOD`;
- GRD/URP/GraphicsSettings в git; бюджет теней/директор auto-bootstrap;
- быстрый regression-замер: `FinalValidationBench` Stage 8 (P1, L1 10 км, L2 20 км, F8/F9).
- Что делать при росте P1 >2 341 / двери >150 м: не отключать системы, сначала профилировать;
  порог/сценарии — в отчёте.

### 1.4. Закрытие документации

- `docs/dev/T-CULL01_FINAL_ACCEPTANCE_REPORT.md`: числа, логи, runbook, открытые пункты.
- `10_EXECUTION_PLAN.md`: Stage 8 ✅; **T-CULL01 = CLOSED**; финальные бюджеты.
- `T-CULL01_COMPLEX_CODE_HANDOFF.md`: статус CLOSED, ссылки на отчёты; открытые отдельные тикеты:
  - F9 input flakiness (не куллинг);
  - PVS не нужен (L0 2 229 >1k);
  - pre-existing двойное владение тенями ферм;
  - свет закрыт отдельным треком: остаточные темы — fade-out (лимит удерживаемых источников),
    интерьерный бюджет/окклюзия ADMIN, `range` INLIGHT 48.3 м (правка сцены требует одобрения),
    пространственный индекс для `FindFocusCamera`, L7 (multi-camera/emissive) отложен.
- README/02: финальные числа 2 229 / 46 / 2.
- Commit `T-CULL01/final: acceptance + runbook`; без push.

---

## 2. Приёмка

- [ ] обычный запуск игры: P1 2 229 ±5 %, двери >150 м = 0, L1 46 / L2 2;
- [ ] GRD/бюджеты/директор/HLOD включены по умолчанию; F8/F9 PASS; 0 errors;
- [ ] свет-инварианты: `FocusCamera=ThirdPersonCamera_0`, `intensityZeroActive=0` днём,
      `HeldForFade=0`, `GateGraceHeld=0`, fade-in виден, `dc` не изменился;
- [ ] сцена не сохранена, prefab/FBX/корабли 0 diff;
- [ ] runbook для clean clone/нового контента записан;
- [ ] T-CULL01 закрыт в плане/тикете; финальный отчёт и коммит;
- [ ] открытые пункты вынесены отдельно (F9 input, PVS not needed, farm shadow ownership, light out of scope).

**Stop:** числа не сходятся → не закрывать, отчёт с diff; визуальный регресс → пользователь;
не менять код «по пути» без согласования.

---

## 3. Жёсткие правила

1. Только приёмка/документация; никакой новой оптимизации; Stage 6/7/8 не переделывать.
2. Корабли/medium/light/NPC и B2 — не трогать; **свет-код не откатывать и не переписывать**
   (владение: `Light.enabled` — пул, `Light.intensity` — ALIC), световые бенчи `T-LIGHT16…21` не запускать.
3. Не менять prefab/FBX/сцену; сцену не сохранять; не push.
4. Не менять URP/GRD/ProjectSettings.
5. F9-флакиность — отдельный тикет, не блокирует закрытие, если run 2 PASS и restore подтверждён.
