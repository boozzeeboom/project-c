# Итерации реализации Cloud System 3.0

---

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
