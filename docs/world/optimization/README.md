# Optimization — docs/world/optimization

Папка про оптимизацию отрисовки и производительности мира.

## Файлы

| Файл | Что внутри |
|---|---|
| `LOD_VIEW_DISTANCE_DESIGN.md` | Дизайн 4-уровневой системы дальности прорисовки: ближнее / среднее / дальнее (фон) / ультрадальнее (резерв). Принцип «дальний террейн — фон, а не off». Таблицы чисел пресетов. |
| `IMPLEMENTATION_PLAN.md` | Пошаговый план реализации: `SettingsManager.ViewDistance` → `ViewDistanceApplier` → ESC‑меню «Видео → Дальность прорисовки» → локализация → верификация. |

## Контекст (факты, на которых стоит дизайн)

- Террейн `WorldScene_0_0/WorldRoot_0_0/Terrain_0_0`: 80×80 км, `TerrainData` 1025, `heightmapPixelError=200`, `basemapDistance=20000`, `drawInstanced=true`, `TerrainCollider` включён. Ребёнок `WorldRoot_0_0` — по Floating Origin едет бесплатно (🟢). См. `docs/world/terrain/README.md`.
- Руины низин `RuinsValleys`: 496 инстансов, 2 инстансинг‑материала (~5–7 draw calls), тени выкл, коллайдеров нет, `isStatic=false`.
- Камеры сейчас: `SpringArmCamera.Awake` — `farClipPlane=1000000`, `near=0.1`; `WorldCamera` — `far=1000000`, `near=0.5`. Far 1M — главный кандидат на снижение (глубина, overdraw, z‑fighting; см. заметку про шаг глубины в `docs/world/distantfocus/DESIGN_farfocus.md`).
- Настройки: `ProjectC.Core.SettingsManager` (static, PlayerPrefs `Settings.*`, `ApplyAll()` при старте через `NetworkManagerController`, события `OnXChanged`). UI: `GraphicsSettingsSection` + `SettingsWidgets` (Dropdown/Slider/Toggle, `Loc.Bind` по ключам `ui.*`). Локализация — `UI_Table` (`Assets/_Project/Localization/Export/UI_Table.csv` + `Settings/Localization/UI_Table_*.asset`).
- Стриминг: `WorldStreamingManager` (`loadRadius=2`, `unloadRadius=3`), сцены 6×4 `SCENE_SIZE=79999f`, фокус `WorldScene_0_0`. Соседних игровых сцен пока нет.
- Пост‑эффект дали: `DistantFocusRenderFeature` (`FarStart=800`, `FarEnd=4000`, `AfterRenderingOpaques`) — работает в среднем плане, с новой системой не конфликтует, пороги согласовать.

## Статус

- [x] Дизайн утверждён к планированию (этот раздел)
- [ ] Phase 0 — замеры базы
- [ ] Phase 1–4 — реализация (см. `IMPLEMENTATION_PLAN.md`)
- [ ] Phase 5 (Ultra, межсценовый фон) — отложена до появления 2+ игровых сцен
