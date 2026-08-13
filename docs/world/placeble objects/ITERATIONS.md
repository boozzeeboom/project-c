# Итерации: Placeble / Carryable Objects

## Итерация от 2026-08-10

**Задача:** Ресерч и дизайн системы переносимых физических объектов (бочки/коробки): hold-F 2с → outline (M_TargetOutline) → перенос с ограничением по весу/STR → повторное F = дроп с физикой; позиции принимает сервер, сохраняются между сессиями, без дубликатов.
**Коммит:** `d45beae1825fd5f8b2f1399bd99db9a6eea34b13` — T-CARRY01: ресерч и дизайн переносимых физических объектов
**Изменения:**
- `docs/world/placeble objects/00_DESIGN_CarryableObjects.md` — ресерч кодовой базы (инпут F, InteractableManager, TargetHighlightService, Stats/STR, ShipPositionServer-паттерн, SceneBoundNetworkObject), компонентный дизайн (CarryableObject / PlayerCarryController / CarryableOutline / CarryableObjectServer), сетевой дизайн (server-authoritative, RPC-flow, lock), persistence, edge cases, пошаговый план по фазам, тест-план.
- `docs/world/placeble objects/ITERATIONS.md` — этот файл.
