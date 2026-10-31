# T-CULL01 / Stage 4c — brief: фактический gate `CityShadowBudget` + F9

**Дата:** 2026-11-01 · **Проект:** `C:\Projects\ProjectC_client` · **База:** `f9dc332a`
**Scope:** починить/доказать **реальный** P1 ≤2 500 с `CityShadowBudget` и корректный F9. Никаких
Stage 6/7, кораблей, B2, контента.
**Play Mode:** нужен; только с разрешением пользователя. Сцену не сохранять, push не делать.

**Прочитать:**
- `docs/dev/T-CULL01_FINAL_VALIDATION_REPORT.md`
- `docs/dev/T-CULL01_STAGE4B_REPORT.md`
- `docs/world/primum_city/10_CULLING_RESEARCH/10_EXECUTION_PLAN.md` §6.1–6.2
- `docs/dev/T-CULL01_FINAL_VALIDATION_AGENT_BRIEF.md` (что уже проверялось)

---

## 0. Факт: final validation не прошла

| | Бенч Stage 4b | Реальная игра (V) |
|---|---:|---:|
| P1 DC | 2 423 | **2 811** |
| entries | 1 482 | **1 301** |
| shadowsOff | 921 | **776** |

`P1 2 811 > 2 500` (+311, +12.4 %). Компонент собрал на **181** рендерер меньше, чем полигон бенча.
F8 — PASS (256 м, 0 errors). F9 — bench ждёт frameGen, а rollback прошёл
(`restore=True`, `forced_user_validation_failure`) → **критерий в bench неверный**, продукт-путь
нужно проверить корректно.

**Нельзя** закрывать T-CULL01 и запускать Stage 6/7 на неверном baseline. Сначала 4c.

---

## 1. Задачи

### 1.1. Candidate parity (главное)

- Сравнить наборы:
  - компонент `CityShadowBudget._entries` (приватное поле, reflection);
  - полигон `Stage4bGapBench._cityShadowCandidates` (или его текущий аналог).
- Вывести diff: путь, layer, `enabled`, `shadowCastingMode`, bounds.center, distance, камера.
- Проверить гипотезы:
  1. `Build()` пропускает `shadowCastingMode == Off` (181 шт.) — нужно ли включать их в счётчик и
     не терять on-кастеры;
  2. `Build()` происходит до активации части мира; нужен **rebuild после загрузки/активации**
     (или периодический rebuild, пока count не стабилизируется);
  3. `RefreshCameras` включает `AtmosphereCamera|MainCamera` — сравнить с одним геймплейным `_cam`;
     union должен быть только по реальным геймплейным камерам, menu/static — отсечь.
- Исправить **минимально**, без сцены/prefab: компонент должен собирать тот же набор, что бенч,
  и давать тот же P1 (2 423 ±5 %) в реальной игре.

### 1.2. Фактический замер

- Реальная игра (auto-bootstrap), Play Mode Host, Midday, far 30 000.
- P1 с кораблями выключенными и с кораблями (как в V), повтор ±5 %.
- `CityShadowBudget` log: entries ≈1 482 (не 1 301); budget off/on; C2/C3.
- **Гейт: P1 ≤2 500**; если 200 м не хватает — 150 м fallback **только с разрешения пользователя**
  (бенч 2 302; фактический компонент нужно перемерить).

### 1.3. F9

- Исправить критерий в `FinalValidationBench`: F9 — это rollback; успех = `restore=True` и
  возврат позиций/кумулятива, а не смена frameGen.
- Проверить F8 → F9 реальным вводом:
  - F8: 256 м, 0 errors;
  - F9: rollback/restore без потери объектов, 0 errors, двери/корабли/бюджет на месте.
- Если продукт-путь реально отказывает (`apply_refused`) — приложить trace, не «симулировать» успех.

### 1.4. Чистота и отчёт

- Сцена не сохранена; `sceneDirtyNow=False`; `git status` чистый (кроме отчётов/CSV).
- prefab/FBX/корабли не менялись.
- Отчёт `docs/dev/T-CULL01_STAGE4C_REPORT.md`; CSV `R_stage4c_<date>.csv`.
- Коммит `T-CULL01/H5c: actual CityShadowBudget gate + F9`; без push.
- После зелёного 4c — **Stage 6 (approved)**, затем Stage 7.

---

## 2. Приёмка

- [ ] компонент entries паритетны бенчу (≈1 482), причина 1 301 найдена;
- [ ] **реальный P1 ≤2 500** (ожидается ~2 423), повтор ±5 %;
- [ ] C2/C3 без регрессии; двери/корабли не тронуты;
- [ ] F8 PASS; F9 rollback/restore PASS (критерий bench исправлен);
- [ ] сцена не сохранена, `git status` чистый, 0 shader errors;
- [ ] отчёт + коммит H5c.

**Stop:** P1 >2 500 после фикса — отчёт, не менять визуал/URP без согласования; 150 м только по разрешению.

---

## 3. Жёсткие правила

1. Только `CityShadowBudget`/`FinalValidationBench`; не Stage 6/7, не B2, не корабли/контент.
2. Не менять prefab/FBX/сцену; не сохранять сцену; не push.
3. URP/ProjectSettings — не менять (валидация текущего состояния).
4. FO-safe, без `Camera.main`, без scene-overrides.
