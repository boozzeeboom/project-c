# 04. Окклюжн-куллинг Umbra: попытка запечь данные и разбор отказов

Дата: **2026-10-30**. База: HEAD `5b41dd7c`.
Статус: **исследование остановлено по решению заказчика**. Данные окклюзии (`OcclusionCullingData`)
за весь прогон **не получены ни в одной конфигурации**. Бейк №6 отменён 30.10 в 20:58, сцена
восстановлена до HEAD `5b41dd7c` и перезагружена в редакторе (§12); рабочее дерево чистое.

Этот документ фиксирует факты, а не план: что мерили, чем мерили, какие конфигурации пробовали,
какой текст ошибки выдал Unity и что из этого следует для Э5/Э6.

---

## 1. Зачем это делалось

В `ROADMAP.md` бюджет draw calls города — **≤ 120** (Э5), а фактический кадр с дверями — десятки тысяч
draw calls (§3). Гипотеза проверки: **запечённый occlusion culling** (Umbra) уберёт из кадра
большую часть дверей и домов и приблизит L0 к бюджету без изменения геометрии, материалов и теней.

Проверка распалась на две части:

1. **измеримая** — сколько именно draw calls/FPS даёт включение окклюзии в данной точке (замер §2–§3);
2. **техническая** — можно ли вообще запечь окклюжн-карту для этой сцены (шесть попыток, §7).

Техническая часть провалилась целиком, поэтому выигрыш от куллинга в цифрах получить не удалось.

---

## 2. Методика замера

| Параметр | Значение |
|---|---|
| Сцена | `Assets/_Project/Scenes/World/WorldScene_0_0.unity` (**63 532 621 байт**), `BootstrapScene` загружена аддитивно второй |
| Точка съёмки | `MainCamera` в центре города `(39593.1, 2509.6, 39833.8)`, yaw **337.5°**, fov 60, far **1000** |
| Выбор точки | перебор 32 позиций по максимуму дверей во фрустуме |
| Исключено из замера | `AtmosphereCamera` (runtime-камера в `(0,0,0)`, far 120 000, fov 50) — `enabled = false` |
| Изоляция редактора | Scene View уведена в небо: pivot `(0, 200000, 0)`, rotation `(-89, 0, 0)`, size 5 |
| Инструмент | `UnityStats.drawCalls` / `UnityStats.setPassCalls` / `UnityStats.triangles`, FPS — по Profiler |

**Важно по методике:** `UnityStats.drawCalls` стабилен (±15), а **FPS в первых ~1 500 кадров врёт**
(в одном прогоне 23.5 → 34.1 без изменений конфигурации). Сравнивать прогоны можно **только на равном
`frameCount`**; все цифры ниже сняты на кадре 2659 / 2685.

---

## 3. Побочный результат: конфигурация теней (откачена)

Первая серия замеров случайно оказалась A/B по теням, а не по окклюзии — двери в кадре стоят дороже
всего, когда кастуют тени и попадают в каскады.

| Конфиг | Двери | draw calls | setPass | Треугольники, млн | FPS (равный кадр) |
|---|---|---|---|---|---|
| **A** 1000 м / 2 каскада | вкл | 26 981–26 996 | 232 | 15.86–16.57 | **27.8** (frame 2659) |
| **B** 250 м / 1 каскад | вкл | 21 118 | 216 | 11.61 | **34.1** (frame 2685) |
| **A** | выкл | 4 954 | 195 | 15.68 | 38.8 |
| **B** | выкл | 3 352 | 182 | 10.88 | 35.7 |

Δ между A и B (двери включены): **draw calls −21.5 %**, **FPS +22.7 %**.
В конфиге B двери дают **17 766 из 21 118** draw calls = **84 %**.

**Решение заказчика:** тени в этом этапе не трогаем («задача — идеальный culling»), поэтому конфиг
**откачен к A (1000 м / 2 каскада)**; `Assets/_Project/Settings/ProjectC_URP.asset` совпадает с HEAD
(`shadowDistance 1000`, `cascades 2`). Цифры оставлены здесь как единственный точный замер цены дверей в кадре.

---

## 4. Инвентаризация мира (что вообще претендовало на роль окклюдера)

| Корень | MeshRenderer | Вершины | Замечание |
|---|---|---|---|
| `WorldRoot_0_0/PRIMUM` | 1 207 | 10 263 972 | 1 133 активных; после Э4 |
| `WorldRoot_0_0/PRIMUM_DOORS` | 4 949 | 1 011 312 | 2 363 `DoorController` |
| `WorldRoot_0_0/Primum_farms` | 147 | 1 269 389 | батчи |
| `WorldRoot_0_0/Primum_farms_DOORS` | 270 | 54 408 | — |
| `WorldRoot_0_0/RuinsValleys` | 496 | 11 409 | разбросаны на **77.7 × 78.2 км** |
| `WorldRoot_0_0/Secund` | 25 | ~7.6–8.8 k | центр (15 733, 61 731) |
| `WorldRoot_0_0/Tertius` | 25 | ~7.6–8.8 k | центр (4 428, 2 931) |
| `WorldRoot_0_0/Quartus` | 25 | ~7.6–8.8 k | центр (76 842, 70 471) |
| `WorldRoot_0_0/Terrain_0_0` | **0 Renderer** | — | рисуется `Terrain`-компонентом, **для Umbra невидим** |

Распределение вершин по 1 207 рендерерам города: медиана **1 100**, p90 **23 982**, p99 **112 554**,
максимум **191 986** (`PRIMUM_ADMIN_S0_ADM_P1_01H_M_M_BRONZE`); **27** рендереров > 65 535 вершин,
**19** > 100 k, **356** с более чем одним сабмешем.

Габариты объединённого AABB окклюдеров в первой (самой широкой) конфигурации:
**78 935 × 4 509 × 78 180 м**. Тянули его `RuinsValleys` (77 672 × 295 × 78 180), `Secund`
(4 681 × 3 745 × 3 065), `Tertius` (7 732 × 4 509 × 2 822), `Quartus` (5 173 × 3 516 × 2 887) и
`Cube` (4 733 × 2 529 × 6 000). Сам `PRIMUM` — всего **1 466 × 73 × 1 059**, `Primum_farms` —
5 808 × 3 528 × 5 427.

---

## 5. Прокси-оценка видимости (числам не доверять)

Чтобы понять, есть ли вообще что выигрывать от окклюзии, видимость оценили по **коллайдерам**
(`Physics.Linecast` от камеры к двери) из центра города:

- фрустум: **2 881** дверь, «видимо» по физике: **10** → **99 % скрыто**;
- город: **758** во фрустуме, «видимо» **70**;
- скан 49 открытых уличных точек, 6 разнесённых × 4 yaw: среднее frus **1 106** / vis **2**,
  плотный вид **4 691** / **16**.

**Оценка не является замером окклюзии.** Коллайдеры дверей — упрощённые боксы, луч в центр двери
упирается в стену, а не в проём. Эти числа годятся только как «порядок величины: во фрустуме
тысячи дверей, физически видно единицы» — но не как доказательство, что Umbra вернёт тот же результат.

---

## 6. Исходное состояние перед экспериментом

- `staticFlags = 0` у **всех** 4 949 рендереров `PRIMUM_DOORS` и **всех** 1 207 рендереров `PRIMUM`;
- `StaticOcclusionCulling.umbraDataSize = 0`; `OcclusionCullingData`-ассетов в проекте — **0**;
- папка `Assets/_Project/Scenes/World/WorldScene_0_0/` пуста;
- в сцене: `m_OcclusionCullingData: {fileID: 0}`,
  `m_OcclusionBakeSettings: {smallestOccluder: 5, smallestHole: 0.25, backfaceThreshold: 100}`;
- `Camera.useOcclusionCulling = True` у `MainCamera`.

`m_SceneGUID` в сцене — нулевой (`00000000000000000000000000000000`), но это **норма для непечённой
сцены**: то же самое во всех 16+ сценах проекта. Как причина отказа не рассматривался.

Статик-флаги дверей — отдельная проблема: `04_COLLIDERS/…`/`README.md` §5 фиксирует правило
«двери `static = false`», потому что мир ездит (floating origin) и статик-батчинг нам запрещён.
Эксперимент это правило сознательно ломал.

---

## 7. Шесть попыток бейка — все неуспешны

Каждая попытка: правка `StaticEditorFlags` → `EditorSceneManager.SaveScene` → `StaticOcclusionCulling.GenerateInBackground()`
→ опрос `Logs/Editor.log` + `umbraDataSize`.

| № | Конфигурация | Результат |
|---|---|---|
| 1 | `PRIMUM` + фермы + руины + горы + `Cube` как Occluder (union 78 935 м) | `Failure in split phase` |
| 2 | только `PRIMUM`, 1 207 окклюдеров | `Failure in split phase` |
| 3 | 1 180 окклюдеров (исключены 27 мешей > 65 535 вершин — 7 093 203 вершины) | `Failure in split phase` |
| 4 | **один** окклюдер: `PRIMUM_BASE_S0_CITY_P1_00H_M_M_GRANITE` (1466 × 34 × 1059, 16 800 вершин) | `Failure in split phase` |
| 5 | мир перенесён в `(-39593, -2510, -39834)` — проверка гипотезы floating origin | `Failure in split phase` |
| 6 | 1 207 окклюдеров + `smallestOccluder = 20`, мир в нуле | старт 20:51:56; на **20:57:24** (через 5.5 мин) всё ещё `isRunning = True`, `umbraDataSize = 0`, ошибки в логе нет; **отменён в 20:58** (`StaticOcclusionCulling.Cancel`) |

После каждой попытки: `umbraDataSize = 0`, папка `Assets/_Project/Scenes/World/WorldScene_0_0`
пуста (0 файлов), `m_OcclusionCullingData` остаётся `{fileID: 0}`.

**Попытка 6 отличается от предыдущих только одним:** она не упала за ~2 минуты, как попытки 1–5
(в логе видно строгую периодичность отказов 20:44:12 → 20:46:18 → 20:48:03 → 20:50:08),
а на 20:57:24 всё ещё считалась. Это **не** успех: данных нет, итог неизвестен, работу остановили.
Если `smallestOccluder = 20` действительно меняет поведение, то «`Failure in split phase`» —
функция от порога мелких окклюдеров, а не от геометрии вообще; проверять это отдельным прогоном
с ожиданием до конца.

---

## 8. Что исключено как причина отказа

| Гипотеза | Чем опровергнута |
|---|---|
| Объём/разлёт окклюдеров (78 × 78 км, горы за десятки км) | попытка 4 — **один** окклюдер 1.4 × 0.03 × 1.0 км, тот же отказ |
| Лимит 65 535 вершин на меш | попытка 3 — сверхлимитные меши убраны, тот же отказ |
| Число окклюдеров (1 207) | попытка 4 — один окклюдер, тот же отказ |
| Большие мировые координаты / floating origin (город на ~39.6 км) | попытка 5 — мир перенесён в отрицательные значения, тот же отказ |
| Грубость `smallestOccluder = 5` | попытка 6 — порог поднят до 20, отказ не повторился в те же 2 минуты (итог неизвестен) |
| Нулевой `m_SceneGUID` | нулевой во всех сценах проекта; стандартное состояние непечённых сцен |
| `m_OcclusionCullingData: {fileID: 0}` | это следствие, а не причина: данные не создаются, потому что бейк падает |

---

## 9. Что осталось подозреваемым

1. **Невалидная геометрия в merged-мешах**: вырожденные треугольники (нулевая площадь), вывернутые
   нормали, незамкнутые оболочки. Геометрия города собрана батчами Blender→FBX и уже проходила
   ремонт нормалей (`02_PIPELINE/02_NORMALS_POSTMORTEM.md`), но Umbra строит **замкнутые окклюдерные
   оболочки** и на «дырявых» мешах падает именно в split-фазе.
2. **Поведение Umbra 3.3.22 внутри Unity 6** при ~19 754 статических объектах сцены: версия оптимизатора
   старая (Umbra 3.3.22), а редактор — 6000.5.2f1; известных обходов нет.
3. **Способ задания геометрии окклюдера**: возможно, единственный рабочий путь — не помечать
   существующие батчи `OccluderStatic`, а дать Umbra **отдельные упрощённые замкнутые объёмы**
   (боксы домов, которые уже есть в `PRIMUM_COLLIDERS`, 10 817 боксов).
4. **Террейн**: `Terrain_0_0` не даёт Renderer вообще, то есть в окклюзию не попадает (ни как
   окклюдер, ни как окклюди). Для города в котловине это означает заведомо неполную карту — даже
   удачный бейк не описал бы рельеф.

---

## 10. Текст ошибки (verbatim из `Logs/Editor.log`)

```
[20:44:12] INFO: Umbra 3 Optimizer version 3.3.22  (build: 58bc48d2405823a0d92fca7d7a5dac9d02e0a94b)
Error occurred in occluder data computation:
0x00007ff801a13bb6 (Unity) OcclusionCullingTask::Complete
0x00007ff801a158af (Unity) OcclusionCullingTask::GenerateTome
0x00007ff800fc58e4 (Unity) StaticOcclusionCulling_CUSTOM_Compute
0x00000234529fc077 (Mono JIT Code) (wrapper managed-to-native) UnityEditor.StaticOcclusionCulling:Compute ()
Failure in split phase
0x00007ff801a13c19 (Unity) OcclusionCullingTask::Complete
0x00007ff801a158af (Unity) OcclusionCullingTask::GenerateTome
0x00007ff800fc58e4 (Unity) StaticOcclusionCulling_CUSTOM_Compute
0x00000234529fc077 (Mono JIT Code) (wrapper managed-to-native) UnityEditor.StaticOcclusionCulling:Compute ()
```

Ключевые строки для поиска: `Umbra 3 Optimizer version`, `Error occurred in occluder data computation:`,
`Failure in split phase`, `INFO: T: 20 O: 5 H: 0.25 B: 100` (T — потоки, O — `smallestOccluder`,
H — `smallestHole`, B — `backfaceThreshold`).

Лог искать **только** в `C:\Projects\ProjectC_client\Logs\Editor.log` — в
`%LOCALAPPDATA%\Unity\Editor\Editor.log` этот сеанс уже не пишется.

---

## 11. API и подводные камни (проверено `unity_reflect` на этом проекте)

- У `UnityEditor.StaticOcclusionCulling` **нет** `doesSceneHaveOcclusionCullingData`. Есть:
  `Compute`, `GenerateInBackground`, `isRunning`, `umbraDataSize`, `Cancel`, `Clear`,
  `RemoveCacheFolder`, `SetDefaultOcclusionBakeSettings`, `smallestOccluder` / `smallestHole` /
  `backfaceThreshold`, `doesSceneHaveManualPortals`.
- `OcclusionCullingSettings` **не** является `UnityEngine.Object` — через
  `Resources.FindObjectsOfTypeAll` не читается (`FindAllObjectsOfType: The type has to be derived from
  UnityEngine.Object`).
- `StaticEditorFlags` **не имеет** значения `Nothing`; «снять всё» = `(StaticEditorFlags)0`.
- `execute_code` уходит в `"Timeout receiving Unity response"` на `EditorSceneManager.SaveScene` сцены
  на 63 МБ и на тяжёлых обходах (`GetComponentsInChildren<MonoBehaviour>` по 19 754 объектам).
  После таймаута операция в редакторе обычно **продолжается** — это таймаут транспорта, не отмена.
- `timeout /t N /nobreak` не работает при перенаправленном stdin (`The parameter is incorrect`);
  ожидание делать через `powershell -NoProfile -Command "Start-Sleep -Seconds N; ..."`.
- При выходе из Play Mode Unity восстанавливает сцену из pre-play бэкапа (даже при отключённом
  Scene Reload), поэтому правки, сделанные **до** входа, сохраняются.

---

## 12. Состояние репозитория и сцены: что было и как восстановлено

На момент остановки `git status` показывал **только** экспериментальную правку сцены:

```
 M Assets/_Project/Scenes/World/WorldScene_0_0.unity
```

Она **не годна ни для коммита, ни для продолжения работы**:

- `WorldRoot_0_0.position = (-39593, -2510, -39834)` — мир сдвинут (попытка 5, проверка FO);
- 1 207 рендереров `PRIMUM` помечены `OccluderStatic | OccludeeStatic`;
- 2 464 `Frame` (город + фермы) помечены `OccludeeStatic`;
- `smallestOccluder = 20`;
- `m_OcclusionCullingData` по-прежнему `{fileID: 0}`, папка окклюзии пуста.

**Восстановление выполнено 30.10 в 21:08** — бейк отменён, файл возвращён на HEAD, сцена
перезагружена в редакторе (`EditorSceneManager.OpenScene` + additive `BootstrapScene`):

```
git checkout -- Assets/_Project/Scenes/World/WorldScene_0_0.unity
```

Проверено после восстановления: `git status --porcelain` пуст; файл сцены **62 869 056** байт
(экспериментальный был 63 532 621); `WorldRoot_0_0.position = (0,0,0)`; `staticFlags = 0` у всех
рендереров `PRIMUM` (1 207), `PRIMUM_DOORS` (4 949), `Primum_farms` (142) и `Primum_farms_DOORS` (270);
`smallestOccluder = 5`, `smallestHole = 0.25`, `backfaceThreshold = 100`; `umbraDataSize = 0`,
`isRunning = False`; `Camera.useOcclusionCulling = True`; сцена не dirty.
Правило `README.md` §5 «двери `static = false`» остаётся в силе — эксперимент его нарушал.

> Замечание: перезагрузка сцены на 63 МБ занимает **несколько минут**, в течение которых мост
> MCP не отвечает (`get_unity_editor_state` → таймаут 180 с). Операция при этом не падает —
> надо дождаться и перепроверить позицию корня.
