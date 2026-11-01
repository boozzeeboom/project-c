# T-LIGHT17b — brief: ALIC CPU (L2b, обязательный)

**Дата:** 2026-11-01 · **Проект:** `C:\Projects\ProjectC_client` · **HEAD:** `08169bb4`
**Scope:** только `AdditionalLightIntensityController` + контракт с `PrimumLightPool`; без конуса/fade/focus/LOD.
**Play Mode:** разрешён пользователем. Сцену не сохранять, push не делать.

**Прочитать:**
- `docs/world/lights/T-LIGHT17_REPORT.md` §0/§8
- `docs/world/lights/LIGHT_CULLING_PLAN.md` §L2/§4
- `Assets/_Project/Scripts/Core/Lighting/AdditionalLightIntensityController.cs`
- `Assets/_Project/Scripts/World/PrimumLightPool.cs`
- `docs/world/lights/T-LIGHT17_2026-11-01.csv`

---

## 0. Факт

- ALIC пишет `Light.intensity` **всем 2 238** tracked-источникам **каждый кадр** (flicker INLIGHT включён → `_anyFlickerEnabled`, `ApplyIntensity` каждый Update).
- Стоимость **5.4–6.1 мс/кадр**; активных всего 70–96.
- Пул: `Light.enabled` — владелец; `intensity` — ALIC. Flicker/день-ночь/группы/override должны сохраниться.
- L2 PASS: day/night gate, split OUT/IN, radius 100. DC не изменился; CPU-рычаг — ALIC.

**Цель:** убрать ежекадровую запись intensity для неактивных/неизменных ламп, сохранив картинку и flicker.

---

## 1. Задачи

1. **Active-set contract:** пул публично отдаёт активные источники/события (`OnLightEnabled/Disabled` или `CopyActive`), ALIC применяет:
   - базовую intensity всем при смене часа/группы/override (как сейчас, но только на изменении, не каждый кадр);
   - flicker/переходы — только активным;
   - при включении источника — немедленно применить актуальную base intensity, чтобы не было тёмного/яркого кадра.
2. **Разделить base и flicker:** `smoothedIntensity` для всех обновлять только при time/group change; flicker отдельно по active.
3. **Не менять:** кривые, группы, override, flicker-алгоритм, `Light.intensity` ownership; не писать `enabled` из ALIC.
4. **A/B:** baseline (текущий ALIC) vs active-only; измерить `ApplyIntensity` ms, `ApplyCalls`, tracked/applied, active, flicker visual; P1/STREET/ADMIN/FARM; 12/18/20 ч.
5. **Gate:** ALIC ≤ **1.0 мс/кадр** (или ≥70 % ниже baseline), без визуальных изменений; flicker/день-ночь корректны; пул/DC не хуже; F8/F9; 0 errors; сцена чистая.
6. **Отчёт:** `T-LIGHT17B_REPORT.md`, CSV `T-LIGHT17B_<date>.csv`; commit `T-LIGHT17b`.

**Stop:** flicker/день-ночь ломаются; при включении лампа тёмная/яркая кадр; нет выигрыша; ALIC ownership конфликт → отчёт, не «дожимать».

---

## 2. Абсолютные входы

- `Assets/_Project/Scripts/Core/Lighting/AdditionalLightIntensityController.cs`
- `Assets/_Project/Scripts/World/PrimumLightPool.cs`
- `Assets/_Project/Editor/Lighting/TLight16BaselineBench.cs` (A/B-режим можно расширить)
- `docs/world/lights/T-LIGHT17_2026-11-01.csv`, `T-LIGHT16_BASELINE_2026-11-01.csv`
- `docs/world/lights/LIGHT_CULLING_PLAN.md`
- `Assets/_Project/Scenes/World/WorldScene_0_0.unity` — не сохранять.

---

## 3. Жёсткие правила

1. Только ALIC + контракт с пулом; не трогать пул-политику (dayGate/split/radius), конус/fade/focus/LOD/HLOD/URP/ProjectSettings.
2. `intensity` остаётся за ALIC; `enabled` — за пулом; никаких двух писателей.
3. Play Mode — по разрешению; сцену не сохранять; push нет; FO-safe.
4. Не трогать корабли, двери, `VisibilityDirector`, световые префабы/сцену.
