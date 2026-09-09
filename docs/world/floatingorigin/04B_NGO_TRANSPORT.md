# T-FO04B — NGO lifecycle/control/motion transport

Дата: 2026-09-09. Baseline: `dc0ba2a1`. Статус: **T-FO04B реализован как неподключённый NGO transport-компонент; компиляция и 88 чистых проверок пройдены**. Gameplay/prefab integration ещё не выполнена. Код и этот отчёт фиксируются одним коммитом.

## Scope

Добавить настоящий NetworkBehaviour с reliable control и unreliable motion RPC, initial sync/late join snapshot, server-issued session/lifetime generations и входной проверкой. Компонент в этом этапе НЕ устанавливается на существующие префабы/сцены и НЕ пишет Transform. Следующий pose/frame adapter обязан заменить прежний writer согласованно. Не включать world shift и не считать библиотечные тесты игровым PASS.

## Проверенные API/порядок

Установленный NGO 2.13.0: `NetworkObject.SpawnInternal` вызывает `AuthorityLocalSpawn` (1868), затем `SendSpawnCallForObject` (1888). В SpawnManager локальный spawn вызывает `InvokeBehaviourNetworkSpawn` (1384). Таким образом для прочитанного dynamic server spawn local initialization предшествует отправке spawn; нельзя делать противоположный вывод из раздельного существования OnSynchronize.

Reflection с NonPublic flags подтвердила protected virtual OnSynchronize<T>(ref BufferSerializer<T>) и OnOwnershipChanged(ulong,ulong); обычный публичный поиск эти методы не показывал. NetworkTickSystem.Tick — публичное событие Action, также подтверждено reflection. OnNetworkObjectParentChanged публичный virtual.

Реализация не зависит от того, успел ли координатный загрузчик до initial sync: unconfigured control сериализуется как пустой; server activation после spawn рассылает reliable control. Client OnSynchronize только запоминает pending control, применяет после OnNetworkSpawn. Новая пустая/старая initial sync не отменяет уже принятую control revision.

Официальные API (ветка @2.13 показывает документацию 2.13.2; установленный пакет НЕ обновлялся):
- `https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/api/Unity.Netcode.NetworkBehaviour.html`
- `https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/api/Unity.Netcode.RpcAttribute.html`
- `https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/api/Unity.Netcode.NetworkTickSystem.html`

## Контракты

- Session service создаётся явно один раз на серверную сессию и разделяется её репликаторами; существующий Bootstrap не меняется. Session token — identity discriminator, не пароль и не аутентификация.
- Начальная/повторная активация, остановка, authority/parent/teleport разрывы контролируются только сервером. Control имеет монотонную revision и полный baseline. Empty/default state допустим до активации, но не обнуляет работающий поток.
- Motion RPC не может выбрать новый binding или publisher. Сервер проверяет реальный SenderClientId и текущий NetworkObject.OwnerClientId, sequence, finite payload, окно source timestamp, rate limit и обязательную игровую validation callback для remote owner.
- Accepted motion получает server receive timestamp; непроверенный клиентский timestamp не становится временем интерполяции других клиентов. Source time проверяется отдельно, invalid packet не обновляет sequence/latest pose.
- После нового binding публикация владельца заблокирована до явного acknowledgement от pose adapter, что baseline применён. Это локальный API gate, не сетевое подтверждение достоверности клиентского движения.
- Reliable keyframe текущего состояния периодически устраняет потерю последнего unreliable snapshot, включая idle. Control повтор не очищает новые motion samples.
- Ownership change переводит owner-authoritative поток на новый authority epoch и **останавливает его**: старую validation callback нельзя переносить на нового владельца, поскольку она может замыкать старый client ID. Серверный координатор должен явно реактивировать поток с актуальными правилами. Изменение NGO parent также останавливает поток до согласованной перенастройки. Компонент не делает SetParent/телепорт и не создаёт второй источник физических перемещений.
- Despawn/Destroy отписывает Tick и сбрасывает очереди/pending/control; новая lifetime требует нового server generation. Реальный packet ordering/reconnect/host запуск проверит пользователь после prefab/frame интеграции.

## Проверки

Чистые Edit Mode проверки session issuer, control codec, authenticated control receiver, admission, sequence/time/rate limit, tombstones, keyframe recovery. Компиляция NetworkBehaviour и RPC ILPP. Предыдущие 56 тестов повторены. Play Mode, тестовые сцены и screenshots не запускались.

## Реализованные файлы

В `Assets/_Project/Scripts/World/FloatingOrigin/Network/`:

- `GlobalMotionSession.cs` — явный session issuer, монотонные spawn/authority/discontinuity generations, overflow/foreign-session guards. Один экземпляр должен разделяться серверной сессией; автоматического подключения к Bootstrap пока нет.
- `GlobalMotionControl.cs` — reliable envelope + pure receiver. Default initial state сериализуется безопасно, control revision упорядочивает команды, stop сохраняет tombstone, запоздавший keyframe не затирает более свежий unreliable pose.
- `GlobalMotionAdmission.cs` — отдельная проверка реального sender, binding, sequence, времени и rate budget. Callback game rules вызывается только после базовых проверок; rejected/throwing callbacks расходуют budget. Pose/sequence не коммитятся после ошибки, recursive acceptance или смены lifecycle внутри callback.
- `GlobalMotionReplicator.cs` — настоящий NGO NetworkBehaviour с тремя RPC: unreliable owner→server, reliable server→clients control, unreliable server→clients accepted motion. Initial/late-join данные проходят OnSynchronize. Tick отписывается при despawn/destroy. Host применяет данные только один раз.

Новый Editor validator: `Assets/_Project/Editor/FloatingOrigin/ValidateGlobalMotionTransport.cs`.
Меню: `ProjectC/World/Floating Origin/Validate Motion Transport Contracts`.

### Интеграционный API

1. Будущий server coordinator создаёт GlobalMotionSession один раз на серверную сессию.
2. После spawn и корректного размещения в frame вызывает `ActivateWorldServer` либо `ActivateParentLocalServer`. Для remote owner обязательна validation callback; при действующем NetworkTransform активация блокируется.
3. Pose adapter применяет baseline к соответствующей локальной системе координат и вызывает `AcknowledgeBaselineApplied` для ТОЧНОГО binding. До этого `CanPublish=false`.
4. Авторитетная сторона передаёт уже рассчитанные global либо parent-local координаты через `PublishWorld/PublishParentLocal`. Компонент ограничивает отправку одним sample на NGO tick; валидный bool на клиенте означает отправку, а не server acceptance.
5. Получатель читает Control и `TrySampleForDisplay`. Компонент сам НЕ применяет pose к Transform/CharacterController/Rigidbody, не решает physics region и не выполняет prediction/correction.
6. При смене владельца/родителя координатор обязан реактивировать поток с новым binding и актуальными правилами; это не автоматическая миграция текущей логики пилотирования.

Проверка активного NT выполняется динамически, чтобы обнаружить его последующее включение/появление, а не только состояние в момент spawn. Префабы/сцены не изменялись; к существующим объектам этот компонент не добавлен.

### Надёжность и проверяемые ограничения

- Reliable keyframe: раз в 1 секунду при активном потоке. Включает latest accepted pose; применяется также локально на server/host, чтобы public Control.Revision совпадала с публикуемой revision.
- Admission defaults: past window 1 сек, future window 0.25 сек, 120 попыток/сек с burst=8 после базовых проверок. Это библиотечные защитные значения, НЕ подтверждённая настройка для всех RTT/tick rates.
- Финальные peer sample timestamps — время приёма сервером. Непроверенные client clocks не управляют interpolation других клиентов. В этом этапе не реализована компенсация latency; визуальное качество надо оценивать после подключения.
- Более новый sequence с регрессирующим временем keyframe по-прежнему отвергается. Ослаблять этот контракт не стали: отдельный тест подтверждает, что невалидный control не портит revision и исправленный повтор той же revision принимается.
- Проверка sender и обязательный callback НЕ являются полной anti-cheat системой. Реальные game rules скорости, дистанции, разрешённых teleport и пространственных регионов должен предоставить следующий адаптер.
- Parent identity/lifetime передаются, но pose adapter обязан проверять, что нужный parent действительно существует, активен и представлен в правильном frame. Не применять parent-local как global при отсутствии родителя.
- Набор NetworkBehaviour должен совпадать в server/client prefab layout. Runtime AddComponent и автоматическая замена NGO компонентов не используются.
- Существующий NGO NetworkObject spawn transform ещё не заменён global-aware placement. Поэтому этот компонент нельзя считать готовой миграцией сцены или просто включить поверх прежней архитектуры.

## Фактически выполненные проверки

Последняя компиляция: **No compile errors**. RPC-атрибуты и lifecycle overrides приняты компиляцией установленного NGO; это не утверждение об отправке RPC в живой сессии.

Фактически выполнены Run() в стабильном Edit Mode:

| Набор | PASS | FAIL |
|---|---:|---:|
| ValidateGlobalMotionTransport | 32 | 0 |
| ValidateGlobalMotionProtocol | 33 | 0 |
| ValidateFloatingOriginFoundation | 23 | 0 |
| **Всего** | **88** | **0** |

Проверки T-FO04B: session token/generations/overflow; control codec; все truncated lengths активного control без partial mutation; sender/object identity; empty/duplicate sync; stale epochs; reliable recovery; newer unreliable preservation; stop/restart; publisher/authority binding; despawn/session reset; unbound/Stopped admission; sender/binding; server restamping; timestamp windows и regressions; budget/refill/rejected/throwing callbacks; lifecycle/recursive reentry; config guards; наличие реальных RPC/lifecycle overrides в compiled типе; owner-stop/reactivation; parent-local control.

Payload (измерено FastBufferWriter): unconfigured control **3 байта**, active world control **144 байта**, active parent-local control **132 байта**. Это без NGO/transport overhead. Старый motion payload остаётся 123/111 байт.

Реальные Host/remote clients, late join/reconnect, latency/packet loss, owner/parent transition в игре и Tick-публикация **не запускались**. Проверки с названиями этих сценариев моделируют входные сообщения pure state machine, а не игровой процесс.

## Следующий этап — T-FO04C

Реализовать единый frame/pose adapter и server session coordinator: authoritative local capture, безопасное применение baseline и remote pose, parent readiness, реактивация после handoff, GameObject/physics-specific запреты и validation rules. Затем отдельно мигрировать необходимые prefab/scene contracts вместе с content placement и RPC/persistence. До их готовности world shift остаётся выключен, а пользовательские игровые проверки этого транспорта пока не требуются.
