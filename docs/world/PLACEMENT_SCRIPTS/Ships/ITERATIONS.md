# Итерации разработки — ShipPresetCreator

## Итерация от 2026-07-17

**Задача:** Создать универсальный Editor-тул для генерации префаба корабля (Player + NPC) на основе анализа реальных кораблей Ship_Light_root и NPC_Ship_HeavyII_03 из сцены world0_0.

**Коммит:** `82447b2` — T-SHIP01: ShipPresetCreator — универсальный Editor-тул создания кораблей

**Изменения:**
- Удалён `CreateTestShip.cs` (устаревший, неверные данные)
- Создан `Assets/_Project/Editor/ShipPresetCreator.cs` — EditorWindow + полный билдер префаба
- Создана документация `docs/world/PLACEMENT_SCRIPTS/Ships/README.md`

**Архитектура:**
- 4 пресета (Light/Medium/Heavy/HeavyII) на основе анализа живых кораблей
- Универсальный префаб: ВСЕ компоненты (Player: FuelSystem/ModuleManager/Hull/Key + NPC: NpcShipController/ProximityZone/DeckNav)
- Автосоздание зависимых SO-ассетов (ключ, damage config, schedule, navmesh)
- Визуальный паттерн от NPC: root scale=1, MainVisual дочерний
