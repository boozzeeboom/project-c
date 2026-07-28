# Ретроспектива версии 0.0.70

**Период:** 22.07.2026 – 28.07.2026  
**Диапазон коммитов:** `6cdba18` → `afe151f` (71 коммит)  
**Дата сборки:** 28.07.2026

---

## Общая сводка

За неделю закрыто **~13 тематических направлений** (~71 коммит). Основной фокус: камера (15 итераций), расследование микротряски персонажа (11 итераций), перформанс (9+ итераций) и документация (аудит ~180 файлов).

---

## 1. CORE: Предупреждения и чистка (4 коммита)

| Коммит | Задача | Суть |
|--------|--------|------|
| `c530371` | T-CORE14 | Fix ~35 compiler warnings (obsolete APIs, unused fields, CS0253, TMP corruption) |
| `a264438` | T-CORE15 | Fix runtime warnings — kinematic velocity, DontDestroyOnLoad, ShipCargoVisual LogError→LogWarning |
| `6de53af` | T-CORE16 | Suppress noisy runtime warnings (NavMesh, PadStateSync, meziy, KeyRod, StationRoot, ResourceNode) |
| `d684547` | T-CORE17 | Wrap verbose init/save/restore logs in `Debug.isDebugBuild` |

**Итог:** ~35 compiler warnings + десятки runtime-спама устранены. Консоль стала читаемой.

---

## 2. GIT: Очистка Assets (1 коммит)

| Коммит | Задача | Суть |
|--------|--------|------|
| `b22461c` | T-GIT01 | Clean tracked Assets/ — migrate scenes, prefabs, shaders, binaries to Lore VCS |

**Итог:** Бинарные ассеты вынесены из git в Lore VCS.

---

## 3. CAMERA: SpringArmCamera — полный цикл (15 итераций, 17 коммитов)

**Старт:** `f2f3fbd` — T-CAM01: ThirdPersonCamera → SpringArmCamera (Phase 1)  
**Финиш:** `f067278` — T-CAM15: Zoom колёсиком мыши

| Фаза | Коммиты | Что сделано |
|------|---------|-------------|
| **Phase 1** | `f2f3fbd` | Collision avoidance + smoothing. Базовый переход на SpringArmCamera |
| **Phase 2** | `b891391`, `c05a514` | Camera Lag + Adaptive Distance |
| **Phase 3** | `8e0412d`, `a593400` | Occlusion Fade (dither shader + detection) |
| **Phase 4** | — | (пропущена/переименована) |
| **Phase 5** | `68d0432`, `167d33a` | FOV Dynamics + Auto-Center (11-step pipeline) |
| **Phase 6** | `12019d3`, `1e5a336` | Over-the-shoulder offset + cleanup ThirdPersonCamera.cs |
| **T-CAM06** | `6f9e337`, `82a1df5` | Fix jitter: stable lag formula + anti-pop + clamp; wire mouseSensitivity/invertY |
| **T-CAM07** | `7519a6f` | Remove double-smoothing, exclude target layer from SphereCast |
| **T-CAM08** | `663585b` | Fix yaw jitter — fast smooth + dead-zone snap + disable adaptive |
| **T-CAM09** | `8f83aca` | Minimal SpringArmCamera — old camera + SphereCast + SmoothDamp only |
| **T-CAM10** | `8b973df` | Восстановление Camera Lag + Anti-Pop + Adaptive Distance + Wall Recovery |
| **T-CAM11** | `568d0f5` | Убрано исключение слоя цели из SphereCast + positionSmoothTime 0.04 |
| **T-CAM12** | `4c2fd05` | Цепной SphereCast + minDist near-clip + smoothTime 0.04→0.06 |
| **T-CAM13** | `297dc99` | Dead-zone 3mm + ускорение vertical lag при падении + smoothTime 0.06→0.08 |
| **T-CAM14** | `8da893d` | Deep Audit: near-clip unified + Adaptive fix + smoothTime 0.04 |
| **T-CAM15** | `f067278` | **Финал:** Zoom колёсиком мыши (SettingsManager + InputBindingsConfig) |
| **Доп.** | `e631c27` | Dead-zone мыши + эксп. decay вместо SmoothDamp |
| | `f56b447` | Near-clip защита на финальной позиции + Animator WriteDefaultValues=0 |

**Итог:** Камера прошла путь от «всё с нуля» до стабильной третьеличностной камеры с:
- Collision avoidance (SphereCast)
- Lag + Anti-Pop
- Adaptive Distance
- Occlusion Fade (dither)
- FOV Dynamics + Auto-Center
- Over-the-shoulder offset
- Mouse dead-zone + экспоненциальный decay
- Zoom колёсиком
- Near-clip защита

---

## 4. JITTER: Расследование микротряски персонажа (11 итераций, 18 коммитов)

**Проблема:** персонаж микротрясётся при standing.

| Итерация | Коммит | Гипотеза / Действие | Результат |
|----------|--------|---------------------|-----------|
| T-JITTER01 | `7d1293d` | Фильтрация стационарных Rigidbody в moving-platform carry | Частично |
| T-JITTER01-v2 | `c32b1e4` | **Корневая причина:** NetworkTransform.Interpolate конфликтует с CharacterController.Move/NavMeshAgent | Найдена |
| T-JITTER01v2 | `72e398a` | NetworkTransform AuthorityMode=Owner | — |
| T-JITTER02 | `b383443` | keep-grounded -2f→-0.5f, MinMoveDistance 0.001→0.005 | — |
| T-JITTER02v2 | `1c5a54e` | keep-grounded -2f восстановлен, гравитация только в воздухе | — |
| T-JITTER03 | `38717da` | Clamp _currentDistance по near-clip + удалён Animator debug-лог | — |
| T-JITTER04 | `a3cb625`, `f65673b` | keep-grounded -2f→-0.5f (H1 fix) | — |
| T-JITTER05 | `89613f8`, `0f4e4b0` | Skip CC.Move when idle on static ground (Вариант C) | — |
| T-JITTER06 | `99e11ef` | stepOffset 0.3→0.01 (H2 diagnostic) | — |
| T-JITTER07 | `ce6f658`, `bc0c538` | NT.Interpolate=false for all clients (H3 diagnostic) | — |
| T-JITTER-SUMMARY | `cd8bd7c` | Все гипотезы исключены, 4 новых направления | Перелом |
| T-JITTER (test cam) | `7332d31`, `c7e1d4f`, `a7c41f3` | Minimal third-person camera для диагностики | Инструмент |
| T-JITTER10 | `83a62ec` | H4/H8 diagnostic — `_diagnosticDisableAnimator` checkbox | — |
| **T-JITTER11** | `5fc5768`, `0de4710` | `skinnedMotionVectors=false` (диагностика Animator motion vectors) | ❓ — не подтверждён |

**Итог:** После 11 итераций все проверенные гипотезы исключены (NetworkTransform.Interpolate, keep-grounded, stepOffset, CharacterController.Move, SkinnedMotionVectors). Проблема остаётся открытой — сформулированы 4 новых направления для отдельного расследования.

---

## 5. PERFORMANCE: Оптимизация (9+ итераций, 16 коммитов)

| Коммит | Задача | Суть |
|--------|--------|------|
| `5931085` | T-PERF01 | Fix 5 узких мест троттлинга: NavMesh спам, disk I/O, Physics.Overlap, FindObjectsByType |
| `fe5600d` | T-PERF02 | NpcBrain NavMesh guard, PadStateSync 32 pads, PanelSettings DPI 96→72 |
| `2f42260`, `9fc8a2c` | T-PERF-01 | ProfilerMarker instrumentation — Phase 0+1 (14 subsystems) |
| `8aff027`, `bbef21e` | T-PERF-07..09 | Phase 2 — Runtime HUD + CPU Budget + NGO Metrics |
| `ac589bb` | T-PERF-opt | SplineWindZone: stagger detection + static ship registry (A+B+C fix) |
| `c8b3070` | T-PERF | Fix log-spam GC pressure — `#if UNITY_EDITOR` в ShipDeckNav, UIManager |
| `e1711db` | T-PERF-NAVMESH | NavMesh re-registration stutter — разброс 0→10s + cooldown 30s |
| `44b35ac` | T-PERF-LOGS | Guard 15 unguarded `Debug.Log` в CombatServer → `_debugLog` |
| `f5a5602` | T-PERF01 | Архитектурный рефакторинг: устранение ритмичных лагов от NavMesh + FindObjectsByType |
| `d9c2244` | T-PERF01-fix | Подавление GC-аллокаций NotifyNavMeshAdded (filterLogType=Exception) |
| `dbb900b` | docs | План исследования UIElementsRepaintPanels (3.1MB/кадр GC alloc) |

**Итог:** 
- Профилирование 14 подсистем (ProfilerMarker)
- Runtime HUD + CPU Budget
- Устранение ритмичных лагов (NavMesh, FindObjectsByType)
- Лог-спам → GC pressure устранён
- SplineWindZone оптимизирован (stagger + static registry + AABB-предфильтр)

---

## 6. WIND SYSTEM: SplineWindZone (3 коммита)

| Коммит | Задача | Суть |
|--------|--------|------|
| `ac589bb` | T-PERF-opt | Stagger detection + static ship registry |
| `0016d74` | T-WIND03 | Централизованный процессинг в WindManager с round-robin и per-zone throttling |
| `2c4ebb7` | T-WIND03 fix | ZoneRuntimeState struct→class (счётчик не сохранялся) |
| `1e21eaf` | T-WIND03 perf | AABB-предфильтр перед GetNearestPoint |
| `7ce2e7e` | T-WIND03 doc | Фикс хеша коммита и документация |

**Итог:** Wind system полностью переведена на централизованный round-robin с троттлингом и AABB-предфильтром.

---

## 7. PERSISTENCE: Deadlock (2 коммита)

| Коммит | Задача | Суть |
|--------|--------|------|
| `054386c` | T-PERSIST-FIX | Deadlock `_restoreCompleted` + ThreadPool persistentDataPath |
| `7205663` | docs | Запись итерации (Ships + Character/respawn) |

**Итог:** Критический deadlock при восстановлении персистентности исправлен.

---

## 8. DOCKING: Спам Pad Occupied (2 коммита)

| Коммит | Задача | Суть |
|--------|--------|------|
| `8d391a9` | T-DOCK15 | Retry-cooldown + авто-регистрация в `_occupiedPads` |
| `7a8dc2c` | T-DOCK15 | Документация итерации |

**Итог:** Спам «Pad Occupied» в FixedUpdate устранён.

---

## 9. DOCS: Аудит документации (3 коммита)

| Коммит | Задача | Суть |
|--------|--------|------|
| `0ea054e` | T-DOCS01 | Структуризация docs/dev — разбор 38 файлов |
| `a86247a` | T-DOCS01 | Аудит и очистка — ~180 файлов в архив, актуализация ссылок |
| `c107b08` | T-DOCS01 | Запись итерации в ITERATIONS.md |

**Итог:** 
- 38 → 21 файл пересортирован по подсистемам
- ~180 устаревших файлов в 23 архивные папки
- Исправлены битые ссылки в GDD
- Переработан Character/00_README.md

---

## 10. TIME SYSTEM: Игровой календарь (2 коммита)

| Коммит | Задача | Суть |
|--------|--------|------|
| `30fc5dc` | T-TIME01 | TimeManager — игровой календарь, персистенция, UI, квестовые триггеры |
| `6f2d13d` | T-TIME01 | CalendarConfig ScriptableObject — имена и правила календаря в Editor |

**Итог:** Новая система игрового времени с календарём, персистентностью, UI и квестовыми триггерами.

---

## 11. WORLD: EarthCurvature Post-Mortem (1 коммит)

| Коммит | Задача | Суть |
|--------|--------|------|
| `afe151f` | T-WORLD03 | Анализ неудачных попыток EarthCurvature |

**Итог:** Задокументированы причины отказа от curvature-шейдера, уроки для будущего.

---

## Статистика

| Метрика | Значение |
|---------|----------|
| Всего коммитов | 71 |
| Дней работы | 7 (22.07 – 28.07) |
| Тематических направлений | 13 |
| Коммитов/день (среднее) | ~10 |
| Самая итеративная система | Камера (15 итераций) |
| Самое глубокое расследование | Jitter (11 итераций к корневой причине) |

## Ключевые достижения

1. **SpringArmCamera** — полный цикл от ThirdPersonCamera до стабильной камеры с 15+ фичами
2. **Jitter investigation** — 11 итераций диагностики, все проверенные гипотезы исключены; проблема микротряски остаётся открытой, требует отдельного подхода
3. **Performance** — профилирование 14 подсистем, устранение ритмичных лагов, HUD метрик
4. **Docs** — аудит ~180 файлов, структуризация в целевые папки
5. **TimeManager** — новая система игрового календаря
6. **Wind System** — централизованный round-robin процессинг
7. **~35 compiler warnings** устранено (впервые за долгое время чистая компиляция)

## Ключевые уроки

1. **Микротряска персонажа** — 11 итераций не хватило; проблема глубже, чем любой из проверенных факторов (NetworkTransform, keep-grounded, stepOffset, CC.Move, SkinnedMotionVectors) — требуется отдельный структурный подход
2. **NavMesh + FindObjectsByType** в апдейте — источники ритмичных лагов; требуют архитектурного решения (кеширование/пулы)
3. **Struct vs Class** для runtime-состояний: struct не сохраняет изменения в словарях (WIND03 fix)
4. **Документация:** дизайн-документы, код которых реализован, должны уходить в архив
