# Перерегистрации ShipDeckNav — исследование (T-PERF02, пункт «б»)

Файл: `Assets/_Project/Scripts/Ship/ShipDeckNav.cs` (357 строк). Кода не меняли —
только разбор. Причина: рядом FO-контракт `T-FO06DF`, вслепую чинить нельзя.

## Триггеры перерегистрации (все пути ведут в `RegisterAt` → `NavMesh.AddNavMeshData`)

1. **Спавн:** `OnNetworkSpawn:221` → `s_pendingRegistrations.Enqueue(this)`.
   Волна спавна 20+ кораблей = 20 Add подряд (смягчено round-robin, п. 4).
2. **Дрейф:** `LateUpdate:236` — ShipRoot ушёл от `_navFrameOrigin` дальше
   `_navFrameSeparation/2` (дефолт 5000/2 = 2500 м) + кулдаун 30 с
   (`_nextReregistrationTime`) → `Unregister()` + enqueue. NPC-корабли летают
   километровые leg'и — срабатывает штатно в полёте.
3. **Floating Origin:** `TryRebuildFloatingOriginSnapshot:156` и
   `TryRestoreFloatingOriginSnapshot:177` вызывают `RegisterAt` **синхронно**,
   в обход round-robin. Плюс `T-FO06DF` (см. `:328`): при мировом сдвиге кулдауны
   сбрасываются всем живым инстансам → следующий `LateUpdate` ставит в очередь всё сразу.
4. **Снятие:** `OnNetworkDespawn` / `OnDisable` → `Unregister()` (дешёвый, без Add).

## Цена одного Add (из комментариев и замеров)

- `AddNavMeshData` — 6–50 ms (комментарий `:27`), рвёт пути агентов внутри слота.
- Внутри Unity дёргает `NavMeshManager.NotifyNavMeshAdded` → `LogStringToConsole`
  на каждый Add. Митигации уже стоят: round-robin ≤1 Add/кадр (`:96-100`),
  `filterLogType=Exception` вокруг Add (`:293-302`), в редакторе выключены стектрейсы
  обычных логов (`ConfigureEditorLogging:106-112`). Собственные логи (`:306,316`)
  уже под `#if UNITY_EDITOR` (прошлая работа T-PERF).
- Замер 3: **18.4 MB за 10 срабатываний** под `ShipDeckNav.LateUpdate`, собственные логи
  при этом молчат (Фаза 5a + `#if`) — деньги внутри нативного/управляемого пути Add.
  Отдельно `NavMesh.Internal_CallPreUpdateListeners` — 0.9 MB × 3.

## Гипотезы про 1.8 MB за вспышку (не доказаны — нужны замеры ниже)

1. Массовая перерегистрация после F8-сдвига (сброс кулдаунов T-FO06DF): round-robin
   размазывает Add по кадрам, но merged-предки в профайлере группируют их под одним
   `LateUpdate` — 10 «хитов» могут быть 10 кадрами по несколько Add.
2. Естественный дрейф летящих NPC (leg'и > 2500 м) — вспышки в полёте без сдвигов.
3. Волна спавна/деспавна.

## Что измерить дальше (пользователь или deep-профиль)

1. Deep-профиль одного прогона (или консоль Eyes on `NavMesh`): частота Add,
   совпадение вспышек с F8/спавнами/дальними leg'ами.
2. Рост `RegistrationGeneration` за сессию (поле уже есть, `:91,130`) — сколько
   перерегистраций на корабль за час.
3. Кандидаты в лечение (только после п. 1–2): поднять `_navFrameSeparation` для
   дальних NPC; батчить FO-перестройки вместо сброса всех кулдаунов сразу;
   проверить, нужен ли `NotifyNavMeshAdded`-лог в билде (там `ConfigureEditorLogging`
   не работает — только `#if UNITY_EDITOR`).

## Статус

Исследование закрыто, код не тронут. Следующий шаг — замеры п. 1–2, затем дизайн-правка.
