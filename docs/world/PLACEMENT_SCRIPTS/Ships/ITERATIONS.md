# Итерации разработки — ShipPresetCreator

## Итерация от 2026-07-17 (v1.3 — ходьба + Key)

**Задача:** Персонаж проваливался сквозь палубу; ключ корабля был пустой (itemName, description, itemType не заполнялись).

**Коммит:** `f8d441f` — T-SHIP05: fix — ходьба по палубе (NavMesh печётся при создании), Key ItemData заполняется корректно

**Исправления:**
- NavMesh: вместо пустого `new NavMeshData()` — вызов `NavMeshSurface.BuildNavMesh()` и сохранение запечённого NavMeshData в ассет
- Key ItemData: `itemName`, `itemType`, `description` — публичные поля, `SetPrivateField` (NonPublic binding) их не видел → прямое присваивание
- `itemType = ItemType.Key` (8), а не int 0

---

## Итерация от 2026-07-17 (v1.2 — багфиксы по ревью)

**Задача:** Убрана лишняя Platform, починены розовые материалы, восстановлена ходьба по палубе.

**Коммит:** `2a26128` — T-SHIP04: фикс префаб-генератора — убрана лишняя Platform, починены розовые материалы, восстановлена ходьба по палубе

**Изменения:**
- Удалён дочерний объект Platform — на NPC-кораблях его нет, MainVisual с BoxCollider сам служит палубой
- `CreateChildCube`: вместо `new Material(Shader.Find("URP/Lit"))` используется `new Material(AssetDatabase.LoadAssetAtPath<Material>(URP_LitMat))` — материалы больше не розовые
- Ходьба: без перекрывающего коллайдера Platform, NavMeshSurface корректно печёт navmesh по MainVisual

---

## Итерация от 2026-07-17 (v1.1 fix)

**Задача:** Исправление багов после ревью: ShipRootReference wiring, Key naming, Meziy fuel/module, CargoVisual spawnZone.

**Коммит:** `0176568` — T-SHIP01: fix — ShipRootReference wiring, Key naming, Meziy fuel/module, CargoVisual spawnZone

**Исправления:**
- ShipRootReference: всем дочерним объектам (MainVisual, Platform, PilotSeat, Door, CargoVisual, Exchanger) проставляются ссылки на корневые _shipController / _rigidbody / _networkObject / _root
- Ключ: имя ассета = `Key_{ShipName}` (вместо `Key_{class}_ship`)
- MeziyModuleActivator: проставлены fuelSystem и moduleManager
- ShipCargoVisual: BoxCollider добавляется ДО присвоения в _spawnZone

---

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
