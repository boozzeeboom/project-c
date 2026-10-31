# T-CULL01 / Stage 0 — baseline и фиксы (2026-10-31)

**Коммит:** `T-CULL01/0: Stage 0 baseline + fixes` · **Базовый коммит:** `831fd759` · **Проект:** `C:\Projects\ProjectC_client`

Связанные файлы:
- `docs/world/primum_city/10_CULLING_RESEARCH/08_DATA/K_baseline_2026-10-31.csv` — baseline (сводка);
- `docs/world/primum_city/10_CULLING_RESEARCH/08_DATA/K_baseline_2026-10-31_raw.txt` — сырые строки дневного прогона;
- `docs/world/primum_city/10_CULLING_RESEARCH/08_DATA/K_baseline_2026-10-31_night_raw.txt` — сырой ночной прогон (контрольный, baseline не является).

---

## 1. Методика

- Unity `6000.5.2f1`, URP 17.5, NVIDIA RTX 3090, Direct3D12, i7-8700, Game View `1491x847`, quality `Medium(2)`.
- URP-ассет `Assets/_Project/Settings/ProjectC_URP.asset`: `m_ShadowDistance=1000`, `m_ShadowCascadeCount=2`,
  `m_AdditionalLightShadowsSupported=0`, `m_SoftShadowsSupported=0`. В Stage 0 ассет не менялся.
- Play Mode → Host; замер идёт по геймплейной камере `ThirdPersonCamera_0` (не `BootstrapScene/MainCamera`).
- Камера: `far = 30 000` (пресет **Medium**; у активного `Far` — 120 000, поэтому значение выставлялось принудительно),
  `fov = 60`, `near = 0.1`. `SpringArmCamera` на время замера отключён (иначе переписывает transform в LateUpdate).
- **Условие фазы суток: Midday forced.** `DayNightController.ApplySunLighting()` переводится в фазу `Midday (Polden)`
  (`sunIntensity 1.4`, `castShadows 1`), компонент отключается в рантайме. Сцена не сохраняется. Без этого условия
  ориентир 26 981 не воспроизводится (см. §2).
- Точки заданы авторскими координатами + живая дельта `cityRoot.position`, поэтому сдвиги Floating Origin поглощаются.
- На точку: 40 кадров успокоения, затем 3 замера с шагом 8 кадров через `WaitForEndOfFrame`; берётся медиана.
  Прогрев — 1500 кадров. Метрики — `UnityStats` (`drawCalls`, `setPassCalls`, `triangles`, `vertices`, `shadowCasters`).
- A/B дверей: корни `PRIMUM_DOORS` (4949 рендереров), `Primum_farms_DOORS` (270), `ADMIN_DOOR_PREFABS` (266).

Замер выполнен временным probe-скриптом (`Assets/T_CULL01_TEMP/BaselineProbe.cs`), после прогона удалён;
в рабочем дереве его нет.

---

## 2. Главная находка: 17 020 DC — это кадр без теневого прохода

| Проверка (live, до принудительного Midday) | Значение |
|---|---|
| `DayNightController.ServerTimeOfDay` | `21.78` → фаза `Night (Noch)` |
| Фаза в `NightPhase.asset` | `castShadows: 0`, `sunIntensity: 0` |
| `DayNightController.cs:434,438` | `sunLight.shadows = None`, `sunLight.enabled = false` |
| `RenderSettings.sun` | `Sun`, `enabled=False`, `shadows=None` |
| `UnityStats.shadowCasters` | **0** → прохода `Directional Shadow Map` нет |
| `MeshRenderer` в сцене | 17 751 / enabled 17 229 / с `shadowCastingMode != Off` — 16 422 |

То есть кастеров в сцене достаточно, но у главного света ночью нет теней. Тот же кадр после принудительного Midday:

| Состояние | DC | SetPass | Tris | shadowCasters |
|---|---|---|---|---|
| ночь (как в первом прогоне) | 17 020 | 287 | 8 772 252 | 0 |
| **день (Midday)** | **27 131** | 316 | 15 981 018 | **10 115** |

`26 981 → 27 131` = **+0.56 %**, треугольники `15.98 млн` попадают в референсный диапазон `15.86–16.57 млн`.
Вывод: 17 020 — цветовой проход без теней; ориентир 26 981 включает теневой проход (≈ +10 тыс. DC).

**Baseline теперь определён как «Midday + тени URP 1000/2».** Любой замер в другой фазе суток даёт −37 % и с
ориентиром не сравним.

---

## 3. Результаты baseline

| Точка | DC | SetPass | Tris | Shadow casters | Комментарий |
|---|---|---|---|---|---|
| P1 центр площади | **27 131** | 316 | 15 981 018 | 10 115 | основной кадр |
| P1 повтор | 27 131 | 315 | 15 981 018 | 10 115 | расхождение **0.00 %** |
| P2 узкая улица | 907 | 169 | 2 248 472 | 304 | точка перевыбрана: свободная линия взгляда 45+ м и стены с двух сторон |
| P3 крыша | 2 220 | 242 | 3 605 300 | 219 | крыша +24 м, наклон −18° |
| P4 порт | 941 | 152 | 1 091 584 | 10 | корабли в порту отсутствуют (см. §6) |
| P1 без городских дверей | 4 607 | 281 | 15 066 522 | 1 729 | отключён только `PRIMUM_DOORS` → ориентир 4 954: **−7.0 %** |
| P1 без всех дверных корней | 2 730 | 270 | 14 984 274 | 1 094 | городские + фермы + admin |

Сверка с ориентирами: `26 981 → 27 131` (+0.56 %) и `4 954 → 4 607` (−7.0 %) — оба внутри допуска ±5–10 %.
Повтор на P1 — 0.00 %.

Прежняя точка «улица» (`(-256, -64.73, 184)`, 260 DC) и «порт» (941 DC) не годились: первая стоит на мостовой
внутри AABB пяти рендереров и смотрит в стену, вторая вообще без кораблей. Это зафиксировано в §6.

---

## 4. Фиксы (2 файла + 1 поле конфига)

### 4.1. `Assets/_Project/Scripts/World/DetailDistanceCuller.cs`

- `Camera.main` (строка 87 старой версии) заменён на `ResolveFocusCamera()` / `FindFocusCamera()`.
- Выбор камеры: активная камера, у которой ближайшая группа деталей ближе всех остальных (тот же приём, что
  `PrimumLightPool.FindFocusCamera`), с фильтрами: `targetTexture == null` и `cullingMask` содержит слой деталей.
  Второй фильтр обязателен: меню-камера `AtmosphereCamera` (`cullingMask = 512` = слой `PCMM_Atmosphere`)
  не рендерит руины и не должна задавать их дальность.
- Поиск не чаще раза в секунду; позиции читаются живьём, мировых `Vector3` между кадрами нет (правило FO).
  `checkInterval`, гистерезис 10 % и `Renderer.enabled`-логика не менялись.

### 4.2. `Assets/_Project/Scripts/World/ViewDistanceApplier.cs`

- Удалено `QualitySettings.shadowDistance = p.shadowDistance;` — URP читает `UniversalRenderPipelineAsset.shadowDistance`
  (1000/2), поэтому присваивание было no-op. Добавлен комментарий на русском.
- Лог пресета больше не печатает `shadow=`, вместо этого `shadows=URP-ассет(1000/2)`.
- URP-ассет и `ProjectSettings` не изменялись.

### 4.3. `Assets/_Project/Scripts/World/ViewDistanceConfig.cs`

- Поле `shadowDistance` помечено как УСТАРЕВШЕЕ в `Tooltip` (значения пресетов не менялись, ассеты не мигрировались).

---

## 5. Проверка в Play Mode

**Куллинг руин.**
- До фикса: `Camera.main` = `BootstrapScene/MainCamera` в `(239997, 3000, 159998)` (240 км от города),
  `RuinsValleys` — `enabled 0 / 496` при игроке в городе; при спавне вблизи руин выбранной камерой становилась
  `AtmosphereCamera`.
- После фикса: `_focusCamera = ThirdPersonCamera_0` при `Camera.main` в 240 км; `AtmosphereCamera` отфильтрована по маске.
- Функциональный переворот: камера в центре хэмлетов (в радиусе 12 км — 27 групп) → `21 / 496` рендереров включены;
  камера уведена на 300 км → `0 / 496`. Куллинг считается от выбранной игровой камеры.

**Floating Origin F8/F9 (до фиксов, код FO не менялся).** `RequestControlledRebase(false)` → `runtimeRebase.Completed`,
участников 78, `rootMoved=0.0`, `relDrift=0.00`; `RequestControlledRebase(true)` → `Rollback`, `rootMovedFromF8=0.0`,
`relDrift=0.00`, 0 ошибок. Сдвиг был нулевым (фокус уже в кванте), поэтому перенос/возврат мира этой проверкой
не покрыты — см. §6.

**Console.** Во время замеров ошибок нет. На старте Play логируется 11 строк уровня Error
`[PadStateSync] UpdatePadState called on client — ignored` (существующий код, к этапу не относится);
при выходе из предыдущей Play-сессии — 2 `NullReferenceException` в NGO-teardown (`NetworkSpawnManager.DespawnAndDestroyNetworkObjects`).
Постоянные warning'и: `"Failed to create agent because there is no valid NavMesh"`, `[EngineThrusterVisual] ...`,
`[ShipCargoVisual] ... _boxPrefabs array is empty`.

**Сцена не сохранялась**, `ProjectSettings`/`Packages`/URP-ассеты/`PRIMUM_CITY.prefab`/`PRIMUM_DOORS` не менялись.

---

## 6. Открытые вопросы

1. **SetPass 316 против 232 (+36 %)** при совпавших DC — не объяснено.
2. **Ориентир «без дверей» 4 954** относится к отключению только городских дверей: полное отключение всех трёх
   корней даёт 2 730. Разница ≈ остаток фермерских + admin дверей. −7.0 % по городским — в пределах допуска,
   но происхождение референсных 4 954 (измерение или модель) по документам не восстановимо.
3. **Корабли не у порта.** `Ship_havyII_base` (976 рендереров) — root `(79953, -34884, 79367)`,
   `lossyScale (6, 0.9, 12)`, bounds `199×296×102`; между двумя обращениями Y менялся на ≈17 км, то есть объект
   движется вниз. В авторской портовой точке `(40064, 2512, 39449)` кораблей нет → строка `P4` описывает порт без
   кораблей, а не «DC корабля». Отдельная задача, к Stage 0 не относится.
4. **F8/F9** выполнены без фактического сдвига мира. Нужен прогон с фокусом, выведенным за порог `256 м`
   от кванта (или с явным переносом player root).
5. **Baseline снят до фикса куллера**, то есть руины в нём выключены целиком (`Camera.main` смотрел с 240 км).
   После фикса числа на участках рядом с руинами изменятся — сравнение Stage 2 обязано делаться на пост-фикс
   конфигурации, а P1-центр как контрольная точка сохраняется.
6. **Группы деталей собираются с `includeInactive: false`** — рендереры, включившиеся позже (наблюдалось 16),
   куллер не отслеживает. Предсуществующая особенность, в Stage 0 не менялась.
