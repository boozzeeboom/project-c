# Итерации разработки Input System

## Итерация от 2026-07-01

**Задача:** Перенос подбора предметов с E на F с высшим приоритетом
**Коммит:** `d3430f9edd6e506e2fbd535d42b29603fcd102b5` — T-INP15: перенос подбора предметов с E на F с высшим приоритетом
**Изменения:**
- `Assets/_Project/Scripts/Input/InputBindingsConfig.cs` — добавлен `GameAction.PickupItem` в enum и default actions list
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs` — блок `PickupItem` перед `ModeSwitch` (высший приоритет на F), удалены `TryPickup()` из E-блока
- `Assets/_Project/Resources/InputBindingsConfig.asset` — добавлена запись `PickupItem → Key.F`
- `docs/Character/input-system/10_CURRENT_STATE.md` — обновлена таблица клавиш
- `docs/Character/input-system/20_KEYBIND_INVENTORY.md` — обновлены записи E и F
