# T-LIGHT17 — brief: ownership + дешёвые победы (L2)

**Дата:** 2026-11-01 · **Проект:** `C:\Projects\ProjectC_client` · **HEAD:** `622c7f2f`
**Scope:** поведение пула без конуса/fade/focus; ALIC performance — отдельный T-LIGHT17b.
**Play Mode:** разрешён пользователем. Сцену не сохранять, push не делать.

**Прочитать:**
- `docs/world/lights/T-CULL01_LIGHT_BASELINE_REPORT.md` (T-LIGHT16)
- `docs/world/lights/LIGHT_CULLING_PLAN.md` §L2
- `Assets/_Project/Scripts/World/PrimumLightPool.cs`
- `Assets/_Project/Scripts/Core/Lighting/AdditionalLightIntensityController.cs`
- `docs/world/lights/T-LIGHT16_BASELINE_2026-11-01.csv`

---

## 0. Что уже измерено (L1 PASS)

- Под `PRIMUM_LIGHTS` **2 238** (753 OUT + 1 334 IN + 151 ферм); registered=2238, active=96.
- **cap 96 связывает**: P1 174/174, STREET 177/177, ADMIN 540/540 отсечено; набор не пересобирается.
- **Днём 46/96 active имеют intensity≈0** (все OUT); 753 OUT нулевые.
- Интерьеры: P1/STREET 50 IN + 46 OUT; ADMIN **96/96 IN**.
- Фермы: 151 registered; **43** active у ферм; отдельной квоты нет.
- **Разворот 180°**: SetVersion=1, 0/96 active в фруструме после разворота — реакции на направление нет.
- **ALIC 5.4–7.1 мс/кадр** (2 238 intensity каждый кадр), пул 0.22–0.36 мс/кадр; пик Check 9 мс.
- Фокус: ThirdPersonCamera_0; на старте (до спавна) AtmosphereCamera@0.
- F8/F9 PASS; DC P1 2254 vs Stage8 2229 (+1.12%, шум).

**Вывод:** сначала day/night-гейт, split OUT/IN, radius config; конус — третий, после.

---

## 1. Задачи

### 1.1. Day/night-гейт пула

- Не включать/не держать источник, если его текущая `Light.intensity ≈ 0` (ALIC уже выставил 0).
- Убрать 46 нулевых OUT из бюджета P1 днём; освободившиеся слоты отдать видимым лампам.
- При переходе 17→21 ч свет должен включаться **вовремя**; не допустить «погасло и не вернулось».
- Не писать `intensity` из пула; только читать/`enabled`.

### 1.2. Раздельный/приоритетный бюджет OUT/IN

- Сейчас IN занимают 50/96 на площади и 96/96 в ADMIN; ввести квоты/score:
  - минимум OUT-слотов на улице (например ≥40), IN — остальное;
  - в ADMIN/интерьере — приоритет IN, но не «всё 96», если рядом улица;
  - параметры вынести в сериализованные поля; A/B-флаг.
- Точные значения — по замеру, не хардкод; визуал улицы/интерьера проверить.

### 1.3. `activeRadius` 140 → 100/70

- Вынести в конфиг/A-B; замерить P1/STREET/ADMIN/FARM; range ламп 3.5–14 м.
- Не увеличивать `maxActive` 96; 128 — только после GPU-гейта (T-LIGHT20).

### 1.4. Ownership/report

- Зафиксировать карту: `enabled` — пул; `intensity` — ALIC; director не пишет.
- A/B-флаг и строка отчёта (`pool`/`dayGate`/`split`/`radius`).
- Не менять `focus`, конус, fade, LOD; не трогать ALIC логику (только читать intensity).

### 1.5. Замер

- Play Mode Host; Midday + 12/18/20 ч; P1/STREET/ADMIN/FARM; 180°.
- Метрики: activeOut/In/Farm, zeroActive, capSaturated, candidates, checkMs, alicMs, DC/FPS/GPU.
- Gate: **day zeroActive = 0**; cap saturation ниже; визуал не хуже; ALIC не дороже baseline;
  F8/F9 PASS; сцена чистая; commit T-LIGHT17.

---

## 2. Приёмка

- [ ] day/night-гейт работает, 46 нулевых слотов освобождены; 17–21 ч переходы корректны;
- [ ] split OUT/IN уменьшает cap saturation, интерьеры не занимают всё;
- [ ] radius 100/70 A/B измерен; выбран режим;
- [ ] P1/STREET/ADMIN/FARM без визуального регресса; ALIC не дороже baseline;
- [ ] F8/F9 PASS; 0 errors; сцена не сохранена; prefab/FBX/URP/корабли 0 diff;
- [ ] отчёт `T-LIGHT17_REPORT.md` + CSV `T-LIGHT17_<date>.csv`; commit `T-LIGHT17`.

**Stop:** визуальный регресс/мигание → откат; ALIC дороже baseline → не трогать; сцена dirty → не сохранять.

---

## 3. Жёсткие правила

1. Только `PrimumLightPool` + сериализованные параметры/A-B; ALIC — только чтение (или отдельный T-LIGHT17b).
2. Не менять конус/fade/focus/day-night emissive/LOD/HLOD/URP/ProjectSettings.
3. Не трогать корабли, двери, HLOD, `VisibilityDirector` (кроме чтения API).
4. Play Mode — по разрешению; сцену не сохранять; push нет; FO-safe (без мировых кэшей).
