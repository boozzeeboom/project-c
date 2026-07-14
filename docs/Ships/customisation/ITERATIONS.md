# Итерации реализации — Engine Visual System

## Итерация от 2026-07-14

**Задача:** T-ENG02 — Engine Visual System (этапы 1-2 + настройка сцены)
**Коммит:** `6c6f50c` — T-ENG02: Engine Visual System — этапы 1-2 + настройка сцены
**Изменения:**
- `Assets/_Project/Scripts/Ship/ModuleSlot.cs` — добавлен `SlotType.Engine`
- `Assets/_Project/Scripts/Ship/ShipModule.cs` — добавлен `ModuleType.Engine`
- `Assets/_Project/Scripts/Ship/Engine/EngineThrusterVisual.cs` — новый компонент (вращение лопастей + отклонение по yaw)
- `Assets/_Project/Scripts/Ship/Engine/EngineThrusterVisual.cs.meta`
- `Assets/_Project/Scenes/World/WorldScene_0_0.unity` — ShipRootReference + Slot_Engine_Left/Right
- `docs/Ships/customisation/02_ENGINE_VISUAL_ANALYSIS_AND_PLAN.md` — статус обновлён

**Что сделано:**
1. Enum'ы `SlotType` и `ModuleType` синхронно расширены значением `Engine` (позиция 3)
2. `EngineThrusterVisual` — клиентский визуальный компонент:
   - Вращает `_propeller` пропорционально thrust (из `ShipInputReader.CurrentThrust`)
   - Отклоняет `transform.localRotation` по Y пропорционально yaw
   - Никаких RPC, никакой модификации Rigidbody
3. `ShipInputReader` уже имел `CurrentThrust`/`CurrentYaw` — этап 3 пропущен
4. `ShipRootReference` добавлен на `Ship_Light_root`
5. `Slot_Engine_Left` и `Slot_Engine_Right` созданы в `WorldScene_0_0`

**Что НЕ сделано (согласно плану):**
- `thrustNormalized` в `ShipTelemetryState` — отдельный тикет после MVP
- SO-модули двигателей — вручную дизайнером
- Multi-crew поддержка анимации — отдельная задача

**Проверки:**
- `BootstrapScene` не тронут ✅
- `ShipController.cs` без изменений ✅
- 0 ошибок компиляции ✅

---

## Итерация от 2026-07-14 (fix)

**Задача:** T-ENG02 — исправление: ShipInputReader + Slot_Engine_Left + постмортем
**Коммит:** `c00f766` — T-ENG02: фикс — ShipInputReader на корабль + Slot_Engine_Left + постмортем
**Изменения:**
- `Assets/_Project/Scenes/World/WorldScene_0_0.unity` — ShipInputReader добавлен, Slot_Engine_Left пересоздан
- `docs/Ships/customisation/T-ENG01_ShipEngineVisual_PostMortem.md` — корневая причина исправлена

**Причины неработоспособности визуала:**
1. `ShipInputReader` отсутствовал на `Ship_Light_root` — `EngineThrusterVisual._inputReader` был null
2. `Slot_Engine_Left` пропал (сцена не сохранилась в прошлый раз)
3. Постмортем T-ENG01 неверно указывал GlobalObjectIdHash как корневую причину — реальная: модуль с множителями 0
