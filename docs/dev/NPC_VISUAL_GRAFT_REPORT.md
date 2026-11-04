# NPC Graft — dry-run

Источник эталонов по классу:
- **Light** → `Assets/_Project/Prefabs/Ships/Ship_Light_root_fbx_test.prefab`
- **Medium** → `Assets/_Project/Prefabs/Ships/Ship_medium_base.prefab`
- **Heavy** → `Assets/_Project/Prefabs/Ships/Альбатрос.prefab`
- **HeavyII** → `Assets/_Project/Prefabs/Ships/Ship_havyII_base.prefab`

| Корабль | Класс | Эталон | Удалено узлов | Перенесено | Сохранено из заглушки | +компоненты корня | Ref remap | MR до → после | Colliders | Doors |
|---|---|---|---|---|---|---|---|---|---|---|
| Берег | Heavy | Альбатрос | 17 | 18 | — | ShipShadowBudget | 33 | 6 → 97 | 125 | 8 |
| Вавилон | HeavyII | Ship_havyII_base | 14 | 17 | DeckNavSurface, Exchanger, CrewAnchors | MeshFilter, MeshRenderer, ShipWakeCloudCutter, GlobalSceneSourceMarker, ShipShadowBudget | 22 | 6 → 617 | 251 | 9 |
| Ветроворот | Heavy | Альбатрос | 17 | 18 | — | ShipShadowBudget | 33 | 6 → 97 | 125 | 8 |
| Горгона | Medium | Ship_medium_base | 14 | 16 | DeckNavSurface, Exchanger, CrewAnchors | MeshFilter, MeshRenderer, ShipWakeCloudCutter, GlobalSceneSourceMarker | 22 | 6 → 119 | 7 | 1 |
| Жук | Medium | Ship_medium_base | 14 | 16 | DeckNavSurface, Exchanger, CrewAnchors | MeshFilter, MeshRenderer, ShipWakeCloudCutter, GlobalSceneSourceMarker | 22 | 6 → 119 | 7 | 1 |
| Летучий | Medium | Ship_medium_base | 14 | 16 | DeckNavSurface, Exchanger, CrewAnchors | MeshFilter, MeshRenderer, ShipWakeCloudCutter, GlobalSceneSourceMarker | 22 | 6 → 119 | 7 | 1 |
| Мастодонт | HeavyII | Ship_havyII_base | 14 | 17 | DeckNavSurface, Exchanger, CrewAnchors | MeshFilter, MeshRenderer, ShipWakeCloudCutter, GlobalSceneSourceMarker, ShipShadowBudget | 22 | 6 → 617 | 251 | 9 |
| Олимп | Heavy | Альбатрос | 17 | 18 | — | ShipShadowBudget | 33 | 6 → 97 | 125 | 8 |
| Пещера | HeavyII | Ship_havyII_base | 14 | 17 | DeckNavSurface, Exchanger, CrewAnchors | MeshFilter, MeshRenderer, ShipWakeCloudCutter, GlobalSceneSourceMarker, ShipShadowBudget | 22 | 6 → 617 | 251 | 9 |
| Река | Heavy | Альбатрос | 17 | 18 | — | ShipShadowBudget | 33 | 6 → 97 | 125 | 8 |
| Сильфида | Medium | Ship_medium_base | 14 | 16 | DeckNavSurface, Exchanger, CrewAnchors | MeshFilter, MeshRenderer, ShipWakeCloudCutter, GlobalSceneSourceMarker | 22 | 6 → 119 | 7 | 1 |
| Скат | Light | Ship_Light_root_fbx_test | 14 | 15 | DeckNavSurface, Exchanger, CrewAnchors | MeshFilter, BoxCollider, MeshRenderer, ShipWakeCloudCutter | 20 | 6 → 429 | 18 | 1 |
| Странник | Heavy | Альбатрос | 17 | 18 | — | ShipShadowBudget | 33 | 6 → 97 | 125 | 8 |
| Торренс | Medium | Ship_medium_base | 14 | 16 | DeckNavSurface, Exchanger, CrewAnchors | MeshFilter, MeshRenderer, ShipWakeCloudCutter, GlobalSceneSourceMarker | 22 | 6 → 119 | 7 | 1 |
| Угольщик | Medium | Ship_medium_base | 14 | 16 | DeckNavSurface, Exchanger, CrewAnchors | MeshFilter, MeshRenderer, ShipWakeCloudCutter, GlobalSceneSourceMarker | 22 | 6 → 119 | 7 | 1 |
| Цитадель | Heavy | Альбатрос | 17 | 18 | — | ShipShadowBudget | 33 | 6 → 97 | 125 | 8 |
| Шмель | Light | Ship_Light_root_fbx_test | 14 | 15 | DeckNavSurface, Exchanger, CrewAnchors | MeshFilter, BoxCollider, MeshRenderer, ShipWakeCloudCutter | 20 | 6 → 429 | 18 | 1 |

Обработано префабов: **17** (запись не выполнялась).

<!-- apply -->
# NPC Graft — применение

Источник эталонов по классу:
- **Light** → `Assets/_Project/Prefabs/Ships/Ship_Light_root_fbx_test.prefab`
- **Medium** → `Assets/_Project/Prefabs/Ships/Ship_medium_base.prefab`
- **Heavy** → `Assets/_Project/Prefabs/Ships/Альбатрос.prefab`
- **HeavyII** → `Assets/_Project/Prefabs/Ships/Ship_havyII_base.prefab`

| Корабль | Класс | Эталон | Удалено узлов | Перенесено | Сохранено из заглушки | +компоненты корня | Ref remap | MR до → после | Colliders | Doors |
|---|---|---|---|---|---|---|---|---|---|---|
| Берег | Heavy | Альбатрос | 17 | 18 | — | ShipShadowBudget | 33 | 6 → 97 | 125 | 8 |
| Вавилон | HeavyII | Ship_havyII_base | 14 | 17 | DeckNavSurface, Exchanger, CrewAnchors | MeshFilter, MeshRenderer, ShipWakeCloudCutter, GlobalSceneSourceMarker, ShipShadowBudget | 22 | 6 → 617 | 251 | 9 |
| Ветроворот | Heavy | Альбатрос | 17 | 18 | — | ShipShadowBudget | 33 | 6 → 97 | 125 | 8 |
| Горгона | Medium | Ship_medium_base | 14 | 16 | DeckNavSurface, Exchanger, CrewAnchors | MeshFilter, MeshRenderer, ShipWakeCloudCutter, GlobalSceneSourceMarker | 22 | 6 → 119 | 7 | 1 |
| Жук | Medium | Ship_medium_base | 14 | 16 | DeckNavSurface, Exchanger, CrewAnchors | MeshFilter, MeshRenderer, ShipWakeCloudCutter, GlobalSceneSourceMarker | 22 | 6 → 119 | 7 | 1 |
| Летучий | Medium | Ship_medium_base | 14 | 16 | DeckNavSurface, Exchanger, CrewAnchors | MeshFilter, MeshRenderer, ShipWakeCloudCutter, GlobalSceneSourceMarker | 22 | 6 → 119 | 7 | 1 |
| Мастодонт | HeavyII | Ship_havyII_base | 14 | 17 | DeckNavSurface, Exchanger, CrewAnchors | MeshFilter, MeshRenderer, ShipWakeCloudCutter, GlobalSceneSourceMarker, ShipShadowBudget | 22 | 6 → 617 | 251 | 9 |
| Олимп | Heavy | Альбатрос | 17 | 18 | — | ShipShadowBudget | 33 | 6 → 97 | 125 | 8 |
| Пещера | HeavyII | Ship_havyII_base | 14 | 17 | DeckNavSurface, Exchanger, CrewAnchors | MeshFilter, MeshRenderer, ShipWakeCloudCutter, GlobalSceneSourceMarker, ShipShadowBudget | 22 | 6 → 617 | 251 | 9 |
| Река | Heavy | Альбатрос | 17 | 18 | — | ShipShadowBudget | 33 | 6 → 97 | 125 | 8 |
| Сильфида | Medium | Ship_medium_base | 14 | 16 | DeckNavSurface, Exchanger, CrewAnchors | MeshFilter, MeshRenderer, ShipWakeCloudCutter, GlobalSceneSourceMarker | 22 | 6 → 119 | 7 | 1 |
| Скат | Light | Ship_Light_root_fbx_test | 14 | 15 | DeckNavSurface, Exchanger, CrewAnchors | MeshFilter, BoxCollider, MeshRenderer, ShipWakeCloudCutter | 20 | 6 → 429 | 18 | 1 |
| Странник | Heavy | Альбатрос | 17 | 18 | — | ShipShadowBudget | 33 | 6 → 97 | 125 | 8 |
| Торренс | Medium | Ship_medium_base | 14 | 16 | DeckNavSurface, Exchanger, CrewAnchors | MeshFilter, MeshRenderer, ShipWakeCloudCutter, GlobalSceneSourceMarker | 22 | 6 → 119 | 7 | 1 |
| Угольщик | Medium | Ship_medium_base | 14 | 16 | DeckNavSurface, Exchanger, CrewAnchors | MeshFilter, MeshRenderer, ShipWakeCloudCutter, GlobalSceneSourceMarker | 22 | 6 → 119 | 7 | 1 |
| Цитадель | Heavy | Альбатрос | 17 | 18 | — | ShipShadowBudget | 33 | 6 → 97 | 125 | 8 |
| Шмель | Light | Ship_Light_root_fbx_test | 14 | 15 | DeckNavSurface, Exchanger, CrewAnchors | MeshFilter, BoxCollider, MeshRenderer, ShipWakeCloudCutter | 20 | 6 → 429 | 18 | 1 |

Обработано префабов: **17** (записано).


<!-- verify -->
# NPC Graft — проверка

| Корабль | Класс | Platform | MR | Colliders | Doors | CrewAnchors | Exchanger | Missing script |
|---|---|---|---|---|---|---|---|---|
| Берег | Heavy | нет | 97 | 125 | 8 | есть | есть | 0 |
| Вавилон | HeavyII | нет | 617 | 251 | 9 | есть | есть | 0 |
| Ветроворот | Heavy | нет | 97 | 125 | 8 | есть | есть | 0 |
| Горгона | Medium | нет | 119 | 7 | 1 | есть | есть | 0 |
| Жук | Medium | нет | 119 | 7 | 1 | есть | есть | 0 |
| Летучий | Medium | нет | 119 | 7 | 1 | есть | есть | 0 |
| Мастодонт | HeavyII | нет | 617 | 251 | 9 | есть | есть | 0 |
| Олимп | Heavy | нет | 97 | 125 | 8 | есть | есть | 0 |
| Пещера | HeavyII | нет | 617 | 251 | 9 | есть | есть | 0 |
| Река | Heavy | нет | 97 | 125 | 8 | есть | есть | 0 |
| Сильфида | Medium | нет | 119 | 7 | 1 | есть | есть | 0 |
| Скат | Light | нет | 429 | 18 | 1 | есть | есть | 0 |
| Странник | Heavy | нет | 97 | 125 | 8 | есть | есть | 0 |
| Торренс | Medium | нет | 119 | 7 | 1 | есть | есть | 0 |
| Угольщик | Medium | нет | 119 | 7 | 1 | есть | есть | 0 |
| Цитадель | Heavy | нет | 97 | 125 | 8 | есть | есть | 0 |
| Шмель | Light | нет | 429 | 18 | 1 | есть | есть | 0 |

Проблем не найдено.
