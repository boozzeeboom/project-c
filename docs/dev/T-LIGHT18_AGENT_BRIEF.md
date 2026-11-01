# T-LIGHT18 — brief: фокус/камера (L3)

**Дата:** 2026-11-01 · **Проект:** `C:\Projects\ProjectC_client` · **HEAD:** `57bfdb07`
**Scope:** только выбор/кэш фокус-камеры и тайминг реакции; без конуса/fade/LOD.
**Play Mode:** разрешён пользователем. Сцену не сохранять, push не делать.

**Прочитать:**
- `docs/world/lights/T-CULL01_LIGHT_BASELINE_REPORT.md` §4.5, §6
- `docs/world/lights/T-LIGHT17B_REPORT.md` §8
- `docs/world/lights/LIGHT_CULLING_PLAN.md` §L3
- `Assets/_Project/Scripts/World/PrimumLightPool.cs` (TryGetFocusPosition/FindFocusCamera)
- `Assets/_Project/Scripts/World/VisibilityDirector.cs` (WorldCameras/Refresh)

---

## 0. Факт

- Пул использует `Camera.allCameras` и каждую секунду выбирает камеру по ближайшему светильнику
  (O(cams×2 238), ~2.2–2.9 мс; на старте, до спавна игрока, выбрал `AtmosphereCamera@0`).
- **Разворот 180°**: `SetVersion` не менялся; 0 из 96 active в фруструме после разворота — направление
  пул не учитывает (конус — L4/T-LIGHT19). Фокус-камера верна (`ThirdPersonCamera_0`).
- Anchor — камера, **не Transform игрока**: в режиме корабля камера до ~350 м от игрока.

## 1. Задачи

1. **Кэш камер:** пересобирать не чаще раза в секунду и только по инвалидации (null/disabled, sceneLoaded);
   критерии: `CameraType.Game`, активна, `targetTexture==null`, `farClipPlane≥50`, `cullingMask` содержит
   слой мира, камера «дотягивается» до города/ламп (как `VisibilityDirector.WorldCameras`).
   AtmosphereCamera (меню) и парковочная MainCamera — исключить.
2. **Anchor = камера**; `focus`-override остаётся (Transform) и имеет приоритет; локальный игрок —
   только явный override (`LocalClient.PlayerObject`), не по умолчанию.
3. **Адаптивный триггер:** если камера сдвинулась >2 м или повернулась >3° — обновить набор немедленно;
   минимальный интервал 0.1 с; иначе обычный кэш.
4. Лог: `FocusCamera`, `FocusSource`, время поиска/пересборки; счётчики `SetVersion`.
5. Не менять `enabled`/`intensity` политику (dayGate/split/radius); не писать intensity.

## 2. Замер

- Play Mode Host; 12/18 ч; P1/STREET/ADMIN/FARM; разворот 180°, телепорт камеры/игрока; режим корабля (если доступен).
- Метрики: время реакции набора (frames), `SetVersion`, `focusSearchMs`, `FocusSource`, `activeInFrustum`.
- Gate: фокус всегда `ThirdPersonCamera_0` (или явный override); на старте нет Atmosphere; реакция ≤0.15 с;
  F8/F9 PASS; DC/active не хуже baseline; 0 errors; сцена не сохранена; commit `T-LIGHT18`.

## 3. Жёсткие правила

1. Только фокус/камеры; конус/fade/LOD/HLOD не трогать.
2. `Light.enabled` — пул; `intensity` — ALIC; не менять их политики.
3. Play Mode — по разрешению; сцену не сохранять; push нет; FO-safe (без мировых кэшей).
4. Не трогать корабли, двери, URP/ProjectSettings, `VisibilityDirector` кроме чтения/реиспользования API.

**Отчёт:** `T-LIGHT18_REPORT.md` + CSV `T-LIGHT18_<date>.csv`.
