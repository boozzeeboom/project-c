# NPC DeckNav — dry-run (0…16)

| Корабль | Класс | Tris | Навмеш Y | Δ кресло→палуба | Точек | макс. R | Δ точки→палуба | Ассет |
|---|---|---|---|---|---|---|---|---|
| Берег | Heavy | 397 | -0.1…13.2 | -0.07 | 5 | 0.50 | 0.00 | — | (не записано)
| Вавилон | HeavyII | 766 | -0.1…29.1 | -5.08 | 5 | 0.50 | 0.00 | — | (не записано)
| Ветроворот | Heavy | 397 | -0.1…13.2 | -0.07 | 5 | 0.50 | 0.00 | — | (не записано)
| Горгона | Medium | 13 | -0.7…11.0 | -4.73 | 5 | 0.50 | 0.68 | — | (не записано)
| Жук | Medium | 13 | -0.7…11.0 | -4.73 | 5 | 0.50 | 0.68 | — | (не записано)
| Летучий | Medium | 13 | -0.7…11.0 | -4.73 | 5 | 0.50 | 0.68 | — | (не записано)
| Мастодонт | HeavyII | 766 | -0.1…29.1 | -5.08 | 5 | 0.50 | 0.00 | — | (не записано)
| Олимп | Heavy | 397 | -0.1…13.2 | -0.07 | 5 | 0.50 | 0.00 | — | (не записано)
| Пещера | HeavyII | 766 | -0.1…29.1 | -5.08 | 5 | 0.50 | 0.00 | — | (не записано)
| Река | Heavy | 397 | -0.1…13.2 | -0.07 | 5 | 0.50 | 0.00 | — | (не записано)
| Сильфида | Medium | 13 | -0.7…11.0 | -4.73 | 5 | 0.50 | 0.68 | — | (не записано)
| Скат | Light | 23 | -0.2…5.5 | 4.30 | 5 | 0.50 | 0.00 | — | (не записано)
| Странник | Heavy | 397 | -0.1…13.2 | -0.07 | 5 | 0.50 | 0.00 | — | (не записано)
| Торренс | Medium | 13 | -0.7…11.0 | -4.73 | 5 | 0.50 | 0.68 | — | (не записано)
| Угольщик | Medium | 13 | -0.7…11.0 | -4.73 | 5 | 0.50 | 0.68 | — | (не записано)
| Цитадель | Heavy | 397 | -0.1…13.2 | -0.07 | 5 | 0.50 | 0.00 | — | (не записано)
| Шмель | Light | 23 | -0.2…5.5 | 4.30 | 5 | 0.50 | 0.00 | — | (не записано)

Обработано кораблей: **17** (запись не выполнялась).







<!-- apply -->
# NPC DeckNav — применение (11…16)

| Корабль | Класс | Tris | Навмеш Y, м | Δ кресло→палуба, м | Точек | макс. R, м | Δ точки→палуба, м | Ассет |
|---|---|---|---|---|---|---|---|---|
| Скат | Light | 29 | -0.9…5.0 | 3.88 | 5 | 0.50 | 0.00 | Assets/_Project/Prefabs/Ships/Скат/NavMesh-DeckNavSurface.asset |
| Странник | Heavy | 397 | -0.6…13.3 | -0.44 | 5 | 0.50 | 0.00 | Assets/_Project/Prefabs/Ships/Странник/NavMesh-DeckNavSurface.asset |
| Торренс | Medium | 16 | -1.4…10.0 | -5.17 | 5 | 0.50 | 0.00 | Assets/_Project/Prefabs/Ships/Торренс/NavMesh-DeckNavSurface.asset |
| Угольщик | Medium | 16 | -1.4…10.0 | -5.17 | 5 | 0.50 | 0.00 | Assets/_Project/Prefabs/Ships/Угольщик/NavMesh-DeckNavSurface.asset |
| Цитадель | Heavy | 397 | -0.6…13.3 | -0.44 | 5 | 0.50 | 0.00 | Assets/_Project/Prefabs/Ships/Цитадель/NavMesh-DeckNavSurface.asset |
| Шмель | Light | 29 | -0.9…5.0 | 3.88 | 5 | 0.50 | 0.00 | Assets/_Project/Prefabs/Ships/Шмель/NavMesh-DeckNavSurface.asset |

Обработано кораблей: **6** (записано).

<!-- verify -->
# NPC DeckNav — проверка

| Корабль | Класс | Mask | Deck y | Surface | Tris | Навмеш Y, м | Δ кресло→палуба, м | Точек | макс. R, м | Δ точки→палуба, м | Ассет |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Берег | Heavy | 65 | 0.00 | выкл | 397 | -0.6…13.3 | -0.44 | 5 | 0.50 | 0.00 | Assets/_Project/Prefabs/Ships/Берег/NavMesh-DeckNavSurface.asset |
| Вавилон | HeavyII | 65 | 0.00 | выкл | 1171 | -0.2…26.3 | -5.48 | 5 | 0.50 | 0.00 | Assets/_Project/Prefabs/Ships/Вавилон/NavMesh-DeckNavSurface.asset |
| Ветроворот | Heavy | 65 | 0.00 | выкл | 397 | -0.6…13.3 | -0.44 | 5 | 0.50 | 0.00 | Assets/_Project/Prefabs/Ships/Ветроворот/NavMesh-DeckNavSurface.asset |
| Горгона | Medium | 65 | 0.00 | выкл | 16 | -1.4…10.0 | -5.17 | 5 | 0.50 | 0.00 | Assets/_Project/Prefabs/Ships/Горгона/NavMesh-DeckNavSurface.asset |
| Жук | Medium | 65 | 0.00 | выкл | 16 | -1.4…10.0 | -5.17 | 5 | 0.50 | 0.00 | Assets/_Project/Prefabs/Ships/Жук/NavMesh-DeckNavSurface.asset |
| Летучий | Medium | 65 | 0.00 | выкл | 16 | -1.4…10.0 | -5.17 | 5 | 0.50 | 0.00 | Assets/_Project/Prefabs/Ships/Летучий/NavMesh-DeckNavSurface.asset |
| Мастодонт | HeavyII | 65 | 0.00 | выкл | 1171 | -0.2…26.3 | -5.48 | 5 | 0.50 | 0.00 | Assets/_Project/Prefabs/Ships/Мастодонт/NavMesh-DeckNavSurface 1.asset |
| Олимп | Heavy | 65 | 0.00 | выкл | 397 | -0.6…13.3 | -0.44 | 5 | 0.50 | 0.00 | Assets/_Project/Prefabs/Ships/Олимп/NavMesh-DeckNavSurface.asset |
| Пещера | HeavyII | 65 | 0.00 | выкл | 1171 | -0.2…26.3 | -5.48 | 5 | 0.50 | 0.00 | Assets/_Project/Prefabs/Ships/Пещера/NavMesh-DeckNavSurface.asset |
| Река | Heavy | 65 | 0.00 | выкл | 397 | -0.6…13.3 | -0.44 | 5 | 0.50 | 0.00 | Assets/_Project/Prefabs/Ships/Река/NavMesh-DeckNavSurface.asset |
| Сильфида | Medium | 65 | 0.00 | выкл | 16 | -1.4…10.0 | -5.17 | 5 | 0.50 | 0.00 | Assets/_Project/Prefabs/Ships/Сильфида/NavMesh-DeckNavSurface.asset |
| Скат | Light | 65 | 0.00 | выкл | 29 | -0.9…5.0 | 3.88 | 5 | 0.50 | 0.00 | Assets/_Project/Prefabs/Ships/Скат/NavMesh-DeckNavSurface.asset |
| Странник | Heavy | 65 | 0.00 | выкл | 397 | -0.6…13.3 | -0.44 | 5 | 0.50 | 0.00 | Assets/_Project/Prefabs/Ships/Странник/NavMesh-DeckNavSurface.asset |
| Торренс | Medium | 65 | 0.00 | выкл | 16 | -1.4…10.0 | -5.17 | 5 | 0.50 | 0.00 | Assets/_Project/Prefabs/Ships/Торренс/NavMesh-DeckNavSurface.asset |
| Угольщик | Medium | 65 | 0.00 | выкл | 16 | -1.4…10.0 | -5.17 | 5 | 0.50 | 0.00 | Assets/_Project/Prefabs/Ships/Угольщик/NavMesh-DeckNavSurface.asset |
| Цитадель | Heavy | 65 | 0.00 | выкл | 397 | -0.6…13.3 | -0.44 | 5 | 0.50 | 0.00 | Assets/_Project/Prefabs/Ships/Цитадель/NavMesh-DeckNavSurface.asset |
| Шмель | Light | 65 | 0.00 | выкл | 29 | -0.9…5.0 | 3.88 | 5 | 0.50 | 0.00 | Assets/_Project/Prefabs/Ships/Шмель/NavMesh-DeckNavSurface.asset |

Проблем не найдено.
