# Итерации реализации Cloud System 3.0

---

## Итерация от 2026-08-04 (Storm Cloud Shader — Phase 2.4)

**Задача:** Добавить реальный визуал штормовых облаков (тёмные грозовые кластеры) в существующий volumetric raymarch.

**Коммит:** `bcd64790` — T-CLOUD35: analytic storm density в VolumetricClouds шейдере

**Изменения:**
- `Assets/_Project/Shaders/Clouds/VolumetricClouds.shader` — +StormDensity(), +uniforms, инъекция в CloudDensity()
- `Assets/_Project/Scripts/World/Clouds/StormCellDirector.cs` — +PushStormCellsToShader(), +инспектор-параметры

**Архитектура:**
- CPU: StormCellDirector пакует до 8 ячеек в Vector4[8]×2, пушит через Shader.SetGlobal*
- GPU: StormDensity() проверяет distanceXZ + vertical profile → density + dark color
- Цвет: lerp(StormColorLight, StormColorDark) по плотности → тёмные грозовые массы
- Совместимость: добавляется поверх существующих 4 слоёв Ghibli-облаков

**Инспектор-параметры (Storm Cloud Shader):**
- StormDensityMultiplier (0.1-10) — общая плотность шторма
- StormColorDark / StormColorLight — цвет ядра и края
- StormEdgeSoftness (0.01-0.5) — мягкость края
- StormVerticalPeak (0.1-0.9) — пик плотности по вертикали
- MaxStormCellsInShader (1-8) — макс. ячеек в шейдер

**Статус:** 🟢 Готово к тестированию. Включить StormDirector → клетки дадут тёмные облака в VolumetricClouds.

---
=======
## Итерация от 2026-08-04 (Debug Positioning — завершено)

## Итерация от 2026-08-04 (Debug Positioning — завершено)

**Задача:** Разобраться почему штормовые ячейки не видны. Настроить дебаг-визуализацию.

**Коммиты:**
- `ea7b07f` — T-CLOUD28: `Camera.main` → `FindGameObjectWithTag("Player")`, задержка 2→15 сек
- `243d8a8` — T-CLOUD29: маркеры-столбы 200×4200×200 вместо кубов
- `a46513b` — T-CLOUD30: все параметры маркеров/ветра в инспектор
- `00892c7` — T-CLOUD31: CellRadius до 50000, MarkerWidth=0→авто
- `e139e49` — T-CLOUD32: scale маркеров live-update
- `b28e40c` — T-CLOUD32b: сброс сериализованного MarkerWidth в сцене
- `8a38ea7` — T-CLOUD33: фиксированный MarkerWidth=500, без авто-привязки

**Результат:**
- Ячейки видны (розовые столбы в Game View, цилиндры/гизмо в Scene View)
- Все параметры управляются из инспектора
- Документация: `DEBUG_POSITIONING_INVESTIGATION.md`

**Статус:** ✅ Дебаг-позиционирование завершено.  
**Next:** Реальная визуализация грозовых облаков (сейчас при выключенных маркерах — пусто).

---
=======
## Итерация от 2026-08-04 (Phase 2.4 — начало)

## Итерация от 2026-08-04 (Phase 2.4 — начало)

**Задача:** Phase 2.4 — VFX молнии в грозовых ячейках. Проектирование с нуля (старая шторм-система нерабочая).

**Коммит:** `b67c3ce6` — T-CLOUD12: Phase 2.4 — StormCellDirector + переписан StormLightningVfx

**Изменения:**
- `Assets/_Project/Scripts/World/Clouds/StormCellDirector.cs` — новый. Управление штормовыми ячейками.
- `Assets/_Project/Scripts/World/Clouds/StormLightningVfx.cs` — переписан. Отвязан от StormController.
- `docs/world/CLOUD_system/3.0/PHASE_2_4_STORM_LIGHTNING_PLAN.md` — план реализации.

**Статус:** 🟡 C# код готов, сцена собрана. Осталось:
1. ⚠️ Создать `LightningBolt.vfx` вручную (VFX Graph Editor)
2. Привязать VFX к StormLightningVfx.Vfx в инспекторе
3. Play-тест с верификацией молний
