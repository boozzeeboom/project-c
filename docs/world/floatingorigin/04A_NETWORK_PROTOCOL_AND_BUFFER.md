# T-FO04A — протокол пространственных снимков и буфер приёма

Дата: 2026-09-09. Подэтап T-FO04, baseline после T-FO03: `091ed2de`.
Статус: **T-FO04A реализован и проверен вне Play Mode**. Формат сообщений и receive/interpolation state machine готовы как изолированный слой; NGO transport/scene-prefab integration ещё не подключены. Один коммит содержит код, результаты и документацию.

## Цель и границы

Реализовать независимый от origin формат сетевых координат и ограниченный буфер приёма/интерполяции. Это часть будущей замены NetworkTransform, а НЕ второй writer в действующую иерархию и не включение world shift.

T-FO03 semantic/closed-scene gate остаётся открытым. Чистые DTO/receiver-state-machine можно реализовать и проверить независимо; мигрировать активные prefab/scene компоненты через незакрытый gate нельзя. На этом подэтапе существующие NetworkBehaviour, RPC, prefab layout, physics, сохранения и сцены не изменяются.

## Подтверждённые зависимости

- `NetworkPlayer.cs:306` получает NetworkTransform и настраивает authority/interpolation. Нельзя молча заменить компонент так, чтобы этот вызов перестал описывать фактическую репликацию.
- `NetworkPlayer.cs:1266,1316` использует **Transform.SetParent**, а не NetworkObject.TrySetParent. Это существенное отличие от экипажа и требует отдельной миграции.
- `NpcBrain.cs:272,603,623` использует NGO TrySetParent; AttachToShipDeck и ShipCrewSpawner связаны с палубной навигацией. Parent identity должен включать lifetime, иначе переработанный NetworkObjectId может привязать NPC к другому кораблю.
- TeleportToPosition/TeleportAllClientRpc сейчас выполняют разрыв позиции; rebase не должен превращаться в teleport и сбрасывать velocity.
- В действующей репликации нет наших session/spawn/authority/discontinuity поколений; их потребуется выдавать и согласовывать в будущей транспортной интеграции, не доверять полученному motion packet.

## Выбранный контракт

1. `MotionStreamBinding`: session ID, NetworkObjectId, spawn generation, authority generation, discontinuity generation; явный World/ParentLocal space; parent NetworkObjectId и parent spawn generation. Нулевой object ID допустим; нулевые session/generation запрещены. В world space parent-поля строго нулевые, в parent space запрещено self-parenting.
2. `GlobalMotionSnapshot`: wire version, binding, sequence uint, server-timeline sample time double, world GlobalPosition ИЛИ parent-local Vector3, quaternion rotation в соответствующем space, local scale. Неиспользуемая позиционная ветка строго нулевая. Header и payload читаются в temporaries с проверкой до изменения результата.
3. `GlobalMotionBuffer`: фиксированный bounded buffer без покадровой аллокации. До trusted BeginStream(baseline) motion updates отвергаются. Binding не выбирается из первого случайного пакета и не меняется по пакету с «более новым» epoch.
4. Bind/epoch-transition разрешён только внешнему доверенному lifecycle-слою; этот API не является аутентификацией. Транспорт позже проверит SenderClientId, текущего owner/server и выдачу поколений. Между сессиями — EndStream, новый session token. В одной сессии поколения не откатываются.
5. Sequence использует half-range uint ordering, включая wrap. Duplicate/старые/неоднозначные sequence и time regression отвергаются без изменения ранее принятого состояния. Равное время + новый sequence заменяет последний кадр.
6. Teleport/parent switch/authority handoff открывают новый согласованный binding с baseline и сбрасывают историю. Дубликат старого begin не очищает уже накопленные snapshots. Новый spawn отделяется от повторного ID.
7. Интерполяция выполняется до local conversion. Буфер не хранит client origin. При rebase те же global snapshots проецируются в новый frame. Parent-local sample не превращается в global автоматически: при отсутствии точного parent ID+generation его нельзя применять.
8. До первого/после последнего кадра — hold, без экстраполяции. Через большой временной разрыв — hold старой точки до времени следующего snapshot. Порог настраиваемый; это консервативная политика потери пакетов, не финальная оценка визуального качества.
9. Направления, velocities, forces не входят в position codec. Movement authorization, prediction/anti-cheat, NGO transport, clock sync, actual late join, Transform application и региональный physics routing остаются следующими интеграционными задачами.

## Проверки до подключения к сценам

Сериализация обеих веток реальным FastBufferWriter/Reader; unknown version/space, NaN/Infinity, ненормальная quaternion, noncanonical fields, truncated read без partial mutation. Начальный baseline; чужой session/object/spawn/authority/teleport/parent token; packet before binding; sequence wrap/duplicates/reordering; trusted handoff; repeated begin; bounded storage; sample clamp/long-gap hold; независимые клиентские origin до/после rebase; parent generation mismatch. Старые 23 foundation проверки также повторяются. Только Edit Mode/in-memory, без Play Mode и screenshots.

## Источники

- Установленный NGO 2.13.0: `Runtime/Serialization/FastBufferReader.cs`, `FastBufferWriter.cs`, `BufferSerializer.cs` в PackageCache; API сериализации подтверждено reflection. PackageCache не редактируется.
- `https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/advanced-topics/serialization/inetworkserializable.html`: официальное описание пользовательской и условной сериализации. Веб-страница @2.13 сейчас показывает 2.13.2; это НЕ изменение установленной версии 2.13.0 и не команда обновить пакет. Совместимость проверяется реальным serializer установленного пакета.
- Чтение документации object-parenting не дало содержимого страницы; материалы networkvariable в этом проходе отдельно не подтверждены. Не использовать это как подтверждение отсутствия API; проектные вызовы прочитаны напрямую. Фактическая transport/parent интеграция ещё не проверена.

## Реализованные файлы

В `Assets/_Project/Scripts/World/FloatingOrigin/Network/`:

- `MotionStreamBinding.cs` — явная identity/generations/coordinate-space схема и её serializer.
- `GlobalMotionSnapshot.cs` — versioned conditional codec, finite/unit-quaternion/canonical guards, atomic in-place read.
- `GlobalMotionPose.cs` — результат sampling отдельно от wire packet; явная world projection либо parent-local выдача только при совпадении session/parent ID/lifetime.
- `GlobalMotionBuffer.cs` — preallocated ring 2..256 snapshots, trusted begin + baseline, strict packet binding, uint half-range order, time checks, interpolation/hold. Default capacity=32; default max interpolation gap=0.5 сек. Это настройки библиотеки, не изменение игрового NetworkTransform.

Editor validator: `Assets/_Project/Editor/FloatingOrigin/ValidateGlobalMotionProtocol.cs`.
Меню: `ProjectC/World/Floating Origin/Validate Motion Protocol`.

Никаких автоматически создаваемых компонентов, runtime initialize callbacks, scene changes, NetworkBehaviour AddComponent или замены существующего NT. Новый слой используется только собственными типами и валидатором. Новые .meta созданы Unity и добавляются адресно в тот же коммит.

## Фактические результаты проверки

После исправления восьми CS0165 в Editor validator (out-переменные внутри short-circuit Require выражений заменены последовательными проверками):

- Unity compilation: **No compile errors**.
- `ValidateGlobalMotionProtocol.Run()`: **33 PASS, 0 FAIL**.
- Повторно `ValidateFloatingOriginFoundation.Run()`: **23 PASS, 0 FAIL**.
- Итого: **56 групп проверок, 0 ошибок**. Внутри групп дополнительно перебираются все усечённые длины world payload и 2000 receive/sample циклов.
- World snapshot: **123 байта**, parent-local snapshot: **111 байт**, измерено настоящим FastBufferWriter установленного NGO. Это payload без transport/RPC/batching overhead, не замер сетевого трафика.
- Реальные NGO соединения, Host + 2 clients, late join/reconnect, owner handoff и parenting в игре: **НЕ ПРОВЕРЯЛИСЬ**. Тесты этих названий проверяют только моделируемые входы receiver state machine.
- Play Mode, игровые билды и screenshots: **не запускались**.

33 группы motion-checks покрывают: world/parent codec roundtrip; in-place branch clearing; все truncated world buffers с сохранением прежнего target; unknown version/space; binding guards и допустимый ID=0; non-finite/bad rotation; неканоническую неиспользуемую ветку; unbound/invalid baseline; endpoint hold; duplicate/reordering/uint wrap; time regression без poisoning; equal-time replacement; foreign identity fields; approved teleport/authority/spawn transitions; delayed initial sync; new session reset; parent transition; bounded ring; миллиметровую interpolation на 10^9 м; frame-independent rebase; два удалённых frames; точную parent identity; rotation/scale; long gap hold; invalid time/default pose/out-of-range; limits; 2000 циклов.

## Важные ограничения транспортного подключения

1. `BeginStream` не проверяет SenderClientId сам по себе: это pure library API. Будущий NGO layer должен проверять серверный lifecycle/control path, владение источником motion, версию соединения и server-issued generations. Нельзя вызвать BeginStream(snapshot.Binding, snapshot) из произвольного unreliable packet handler.
2. Выдача session token, spawn generations, authoritative timestamps и reliable baseline/epoch message **ещё не реализована**. Значения тестов не являются runtime ID generator.
3. После смены поколения buffer принимает baseline как начало новой истории. Транспорт должен использовать согласованный server timeline, а не произвольный локальный clock нового владельца; тест reset-time не является разрешением смешивать часы.
4. Здесь нет prediction, anti-cheat speed/range limits, reliable retransmit/ack, parent availability queue, scene placement и Transform writer. Отсутствие этих частей не маскируется словом «готовая репликация».
5. Quaternion near-unit tolerance относится только к корректности данных. Нельзя приравнивать pure interpolation PASS к отсутствию визуального джиттера на движущейся палубе.

## Следующий участок

T-FO04B: связать этот контракт с NGO lifecycle/control/motion transport и authoritative validation, подготовить единый frame/pose adapter без конкурирующего NT writer. Существующие runtime компоненты подключать только вместе с необходимыми scene placement/parent/physics/RPC контрактами и затем проводить пользовательский Host+Clients gate. На текущем этапе пользователю игровые проверки не требуются, поскольку рабочая игра продолжает использовать прежний путь.
