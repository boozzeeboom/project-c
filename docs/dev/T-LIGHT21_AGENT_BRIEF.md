# T-LIGHT21 — brief: fade в ALIC (L6)

**Дата:** 2026-11-01 · **Проект:** `C:\Projects\ProjectC_client` · **HEAD:** `3454171f`
**Scope:** только плавность появления/исчезновения света: fade-множитель **в ALIC** (владелец `Light.intensity`),
контракт с пулом (владелец `Light.enabled`). Логика фокуса L3, веера L4 и значения L5 — не меняются.
**Play Mode:** только с разрешения пользователя (сейчас **не** разрешён). Сцену не сохранять, push не делать.

**Прочитать:**
- `docs/world/lights/T-LIGHT20_REPORT.md` §4, §8 (финал = L4; «остался только fade»)
- `docs/world/lights/T-LIGHT19_REPORT.md` §8 (pop-in гасится только `pre 5°`, плавности нет)
- `docs/world/lights/LIGHT_CULLING_PLAN.md` §L6, §1 п.4 (fade нельзя писать в `Light.intensity` из пула)
- `Assets/_Project/Scripts/Core/Lighting/AdditionalLightIntensityController.cs`:
  `Update` (455–498), `ApplyIntensityCore` (607–633), `ApplyBaseSweep` (715–756),
  `ApplyActiveCore` (762–791), `SyncActiveSet` (943–968), `OnPoolLightActiveChanged` (984–996),
  `ApplyImmediate` (1002–1022), `EmitEpsilon` (639–646)
- `Assets/_Project/Scripts/World/PrimumLightPool.cs`: `CheckCore` (600–830), `dayGate` (`emitting`),
  `ActiveLightChanged`, `SetVersion`
- `Assets/_Project/Editor/Lighting/TLight16BaselineBench.cs`: `RunMode.L5`, ALIC-счётчики

---

## 0. Факт (после L5)

- Сейчас включение источника — **мгновенная запись**: пул пишет `Light.enabled = true` → событие
  `ActiveLightChanged` → ALIC `ApplyImmediate` ставит полную яркость в том же кадре. Гашение — тоже
  мгновенное: пул пишет `enabled = false`, ALIC убирает источник из `_activeLights`.
- Поэтому единственная защита от «щелчка» — угловой запас `pre 5°` (на 100 м ≈8.7 м упреждения).
  Плавности нет: при развороте/беге лампа может включиться рывком, а на границе веера — выключиться рывком.
- Политика владения жёсткая: `Light.enabled` пишет **только** пул, `Light.intensity` — **только** ALIC
  (flicker пишет каждый кадр). Fade обязан жить в ALIC.
- ALIC уже имеет всё нужное: активный набор `_activeLights`, покадровый проход `ApplyActiveCore`,
  `state.smoothedIntensity`, flicker поверх яркости, `EmitEpsilon` (берёт `DayGateEpsilon` пула).
- **Опасное место (обязательно закрыть):** `dayGate` пула считает источник «светящим» по
  `l.intensity > dayGateEpsilon`. В первых кадрах fade-in интенсивность ≈0 → пул сочтёт источник погашенным
  и **выключит его**, ALIC включит снова → мигание ровно там, где мы его убираем. Нужен грейс-период в пуле.

## 1. Задачи

1. **Fade-in (обязательно), 0.15 с по умолчанию.** В ALIC добавить в `LightState` вес `fadeWeight` (0…1) и
   целевое состояние `fadeTarget` (0/1). На включение (`OnPoolLightActiveChanged(active:true)` и первый
   реальный вход в `_activeLights`) вес стартует с 0 (если источник был полностью погашен) и линейно/экспоненциально
   (одинаковая кривая для всех ламп) доходит до 1 за `fadeInSeconds = 0.15`. Итоговая запись:
   `value = smoothedIntensity * flicker * fadeWeight` — flicker остаётся поверх, day/night-кривая не меняется.
2. **Fade-out (условно), 0.30 с.** Реализовать **только** если удаётся сохранить владение `enabled` у пула:
   пул перед записью `enabled=false` спрашивает ALIC «держать ли ещё» (рекомендуется `System.Func<Light,bool>
   HoldEnabled` на пуле, который ALIC вешает при привязке; альтернатива — события `FadeOutCompleted` +
   список в пуле). Гаснущий источник временно остаётся в наборе:
   - удержание ≤ `fadeOutSeconds + 0.05` (жёсткий дедлайн, чтобы не залипнуть при потере события);
   - его `intensity` идёт к 0, запись только при реальном изменении (как сейчас `WriteIntensity`);
   - **cap может быть превышен только на число гаснущих** (обычно <10; 256-лимит камеры далеко) — это
     измерить и записать; если превышение больше 16 или растёт `switches` — fade-out отклонить;
   - в отчёте явно: fade-out принят или **отклонён** с причиной (тогда L6 PASS на fade-in, а «щелчок»
     на выключение фиксируется как остаточный).
3. **Не ломать существующее:**
   - `SyncActiveSet` на смене версии пула сейчас вызывает `ApplyImmediate` **всем** — при resync нельзя
     перезапускать fade уже горящим лампам (вес хранится в `state`, не сбрасывается при повторном добавлении);
     fade-in применяется только источникам, у которых вес был 0;
   - flicker (провалы) — не должен проходить через вес как «плавный»; амплитуда мерцания сохраняется;
   - скачок часа (`ForcePhaseSilent`): кривая суток применяется как раньше, вес не должен «замораживать»
     переходы (грейс только для `dayGate`-дропа, не для смены времени);
   - `ApplyImmediate` (покадровый немедленный путь) — сохранить как «immediate»-путь для случаев
     `enabled=false`/перезагрузки сцены, чтобы не было тёмного кадра на старте: на первом включении сцены
     fade допустим, но не на `forceApply`/`immediate` путях.
4. **Грейс-период в пуле против thrash от dayGate:** для только что включённых источников не применять
   dayGate-дроп `emitting == false` в течение `gateGraceSeconds = 0.4` (иначе первая же волна чека
   (0.25 с) погасит ещё не разгоревшуюся лампу). Счётчик «сколько раз день/ночь-гейт отбросил источник
   в грейсе» — в лог, должен быть 0 после прогрева.
5. **Диагностика:**
   - ALIC: `FadeEnabled`, `FadeInSeconds`, `FadeOutSeconds`, `FadeActiveCount`, `FadeInCount`, `FadeOutCount`,
     `FadeMaxWeightError`, `LastFadeMs`; счётчики в `ConfigLine`-стиле (`FadeConfigLine`).
   - пул: `GateGraceHeld` (сколько источников держится грейсом), `HeldForFade` (сколько держится под fade-out).
   - бенч: `RunMode.L6`, меню `Project C/Lighting/T-LIGHT21 fade/Run|Abort`, тег `[T-LIGHT21]`,
     CSV-префикс `T-LIGHT21`; проба профиля: после включения выбранной лампы писать кадры
     `FADEPROFILE;frame=N;weight=…;intensity=…` до стабилизации; колонки `fadeEnabled, fadeInSec,
     fadeOutSec, fadeProfileFrames, fadeT90Sec, fadeMaxJumpRatio, fadeHeldForFade, fadeGateGraceHeld,
     alicMs, alicWrites`.
6. **A/B-переключатели в памяти** (сцену не пересохранять): `ApplyFadeConfig(enabled, fadeInSeconds,
   fadeOutSeconds)`; контроль = текущее поведение (мгновенно).
7. Не менять: `dayGate`/`split`/`outMin`/`radius`/`cap`/`hysteresis`/углы веера/фокус, `Light.enabled`
   пишет только пул, `Light.intensity` — только ALIC; сцену/префабы/FBX/URP/`ProjectSettings`/корабли/двери
   и `VisibilityDirector` не трогать.

## 2. Замер и gate

- Play Mode Host; позы P1 / STREET / ADMIN / FARM; часы **12 / 18 / 20** (вечер 17–20 — жалоба).
- Режимы A/B в одном прогоне: **fade off** (контроль = L5) vs **fade on** (0.15 / 0.30).
- Профиль включения: `t90` (время до 90 % яркости) = 0.15 с ±20 %; максимальный скачок яркости за кадр
  ≤ 35 % целевой (против ≈100 % в контроле); профиль монотонный, без overshoot.
- Fade-out: число удержанных источников (`fadeHeldForFade`) ≤ 16, время удержания ≤ 0.35 с, `switches`
  не растёт относительно контроля, `cap` превышение только на гаснущие.
- ALIC CPU: `LastApplyMs`/self-time не хуже базы L5 (0.16 мс/кадр) + 0.05 мс; число записей/кадр не растёт
  (вес меняется только у гаснущих/разгорающихся).
- Никакого thrash: `GateGraceHeld` → 0 после прогрева; `SetVersion` растёт не чаще, чем в контроле;
  `intensityZeroActive = 0` на 12 ч; flicker: амплитуда провалов в интерьере та же, что в контроле
  (визуально — пользователь).
- День/ночь: скачок часа 12→18→20 без «залипания» и без тёмного кадра; `activeOUT` 42 на 18/20 ч,
  0 на 12 ч; `intensityZeroActive=0`.
- F8/F9 PASS; `0 errors`; сцена не сохранена; prefab/FBX/URP/`ProjectSettings`/корабли/двери — 0 diff.
- Артефакты: `T-LIGHT21_REPORT.md` + CSV `T-LIGHT21_<date>.csv`, commit `T-LIGHT21`, план §1.6.
- **Визуальный sign-off пользователя:** нет «щелчков» при развороте и беге вечером; лампы загораются
  плавно; flicker интерьеров не изменился; переходы часа без рывков; интерьеры не потемнели.

## 3. Жёсткие правила

1. `Light.enabled` — только пул; `Light.intensity` — только ALIC; fade — множитель в ALIC, пул отдаёт
   только состояние набора (и, при fade-out, спрашивает разрешение на выключение).
2. Не менять политику и значения L2/L4/L5; не трогать fade в пуле через `intensity` (запрещено).
3. FO-safe: никаких кэшированных мировых `Vector3`; после F8/F9 поведение идемпотентно.
4. Play Mode — только по разрешению; сцену не сохранять; push не делать; `.meta`/`.asmdef` руками не писать.
5. Известные артефакты бенча: `NOTE;farm_cluster` каждый кадр в FARM-позе (секунды нерепрезентативны);
   `gpuMs` шумный (8.63–10.77 мс на одном конфиге) — выводы по структурным метрикам.
6. Если fade-out не удаётся сделать без роста `switches`/превышения cap — **отклонить fade-out** и
   записать это в отчёт (L6 при этом PASS на fade-in), не «дотягивать» ценой стабильности набора.

**Отчёт:** `docs/world/lights/T-LIGHT21_REPORT.md` + CSV `docs/world/lights/T-LIGHT21_<date>.csv`.
