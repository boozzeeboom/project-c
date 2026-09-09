# T-FO02 — реализованное координатное ядро

Дата: 2026-09-09. Предыдущий этап: T-FO01, `ba56bdc4`. Baseline игры: `53c86fe2`.
Статус: **код ядра и его in-memory проверки завершены; runtime floating origin НЕ включён**.

## 1. Изменения

Новые файлы в `Assets/_Project/Scripts/World/FloatingOrigin/`:

- `GlobalPosition.cs`: три double XYZ, явный импорт старых абсолютных float, finite guards, equality/hash/distance, NGO INetworkSerializable. Нет implicit cast в Vector3. Декодирование сначала в temporaries, затем присваивание проверенного значения.
- `LocalCoordinateFrame.cs`: immutable origin + допустимая локальная область; независимые значения для клиентов/регионов, без singleton/static global origin. Double subtraction до float cast; явные ToGlobal/ToLocal/TryConvertTo. Default frame недопустим.
- `GlobalGridCoordinates.cs`: double lookup существующих SceneID/ChunkId, Math.Floor для отрицательных индексов, проверка int overflow, origin/center без промежуточного глобального float. Размеры 79999/2000 и legacy API не менялись.
- `OriginRebasePlan.cs`: чистый расчёт будущего XYZ rebase, quantization и reprojection. Не двигает Transform, не регистрируется автоматически и не посылает RPC.

Новый `Assets/_Project/Editor/FloatingOrigin/ValidateFloatingOriginFoundation.cs`: воспроизводимый MenuItem и public Run(). Использует существующий в проекте подход Editor validators, не добавляет NUnit/asmdef/package. Не создаёт GameObject, не открывает сцены, не запускает physics/network session.

Меню: `ProjectC/World/Floating Origin/Validate Coordinate Foundation`.

## 2. Контракты интеграции

- GlobalPosition — **точка**, не universal double Vector3. Directions/forces/velocity/scale/rotations не добавляют origin.
- `FromLegacyAbsolute` применяется только к старым абсолютным данным, не к rebased Transform. Уже потерянные биты исходного float восстановить невозможно.
- Локальная область по умолчанию: cube ±8192 м по компоненте. Это guard дальнейшего использования, не доказанный окончательный runtime threshold и не обещание одинаковой точности. Тестовый планировщик использует threshold 2048/quantum 256; эти значения ещё не настройки работающей игры.
- `TryToLocal/TryConvertTo=false` означает, что объект нельзя размещать в этом frame; значение out=zero не использовать как fallback spawn. Для дальних сущностей нужен interest/region routing.
- Initial spawn/дальний teleport сначала выбирает frame из глобальной цели; нельзя подать 56 км как local и отключить guard ради прохождения.
- Grid bridge возвращает математические отрицательные SceneID; существующий `SceneID.IsValid` остаётся ограничением registry. Helper сам сцены не грузит.
- XYZ rebase требует переноса глобальных altitude/cloud/shader контрактов до runtime активации. Pure math PASS не разрешает включать вертикальное смещение прежних систем.
- Примитив GlobalPosition имеет payload 24 байта XYZ; в нём ещё НЕТ protocol/version/authority/parent/timestamp. Эти обязанности принадлежат будущему enclosing movement/RPC protocol. Наличие сериализации НЕ означает, что NetworkTransform или сохранения уже мигрированы.
- Double тоже конечной точности. Проверки на 10^9 м не являются гарантией sub-mm точности для всех конечных double.

## 3. Фактически выполненные проверки

- Unity compilation: **PASS**, `No compile errors` после импорта новых файлов.
- Выполнен `ValidateFloatingOriginFoundation.Run()` внутри Editor, НЕ в Play Mode: **passed=22, failed=0**, failures=[].
- В статическом review новые типы используются только своим каталогом и Editor validator. В работающий network/save/scene/controller код вызовы не добавлялись.

22 группы проверок:

1. Valid zero global.
2. NaN/±Infinity в конструкторе отвергаются.
3. Legacy absolute import сохраняет имеющуюся float точность.
4. Default spawn и сетка 79999 не изменились.
5. Миллиметровые остатки при origin=10^9: double subtraction до float; неправильный cast-before-subtract демонстрирует нулевой результат.
6. Одна global point в двух независимых frames и межframe conversion.
7. Два далёких frames независимы; дальняя точка корректно отвергается.
8. Default frame fails closed.
9. Bounds/finite validation.
10. Global height сохраняется при vertical frame conversion.
11. Scene boundaries, отрицательные индексы и неизменная IsValid policy.
12. Chunk boundary 2000.
13. Int overflow и крайние допустимые индексы.
14. Rebase сохраняет global point и не меняет прежний immutable frame.
15. Threshold/no-op/invalid config guards.
16. 10000 шагов с повторными rebases сохраняют точную тестовую global trajectory.
17. 2500 deterministic randomized frame round trips.
18. Equality/hash/global distance.
19. Настоящий NGO FastBufferWriter/Reader: точный double round trip, 24 байта.
20. Недопустимые incoming/outgoing wire points отвергаются.
21. Truncated NGO payload отвергается.
22. JsonUtility сохраняет double поля нового типа; это не тест миграции старых save files.

Это не Host+Clients проверка и не доказательство устранения микротряски. Игра, сборки, скриншоты, сетевые подключения и физика не запускались.

## 4. Что не изменено

- NetworkPlayer, NetworkTransform, RPC/DTO, authority и network prefab layout.
- SceneID/ChunkManager legacy implementation и действующий ClientSceneLoader.
- Save DTO/repositories/существующие JSON.
- Корабли, physics/NavMesh, Animator, камера, материалы/lighting.
- Scene/prefab YAML, .meta вручную, пакеты и настройки.
- Legacy FloatingOrigin/FloatingOriginMP и откат T-JITTER18.

.meta новых файлов создаёт Unity и они входят в этот же коммит. Никакого второго коммита для хеша этапа. В репозитории .meta игнорируются, поэтому семь новых metadata-файлов добавляются адресно через git add -f. Стандартные folder .meta Unity содержат пробел после пустых userData/assetBundle полей: обычный diff --check отмечает их; engine metadata не переписываются ради форматирования. Для C#/документации выполняется строгий diff --check без .meta, для metadata — проверка с разрешённым blank-at-eol.

## 5. Следующий этап

T-FO03: воспроизводимый полный source/asset census и проектирование точек интеграции. До миграции сетевых и сохранённых координат, контента, физики и кэшей origin нельзя активировать в обычной игре. Полная задача остаётся открытой.
