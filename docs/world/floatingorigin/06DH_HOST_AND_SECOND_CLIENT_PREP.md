# T-FO06DH — Host + второй клиент: подготовка кода без тестов

Date: 2026-09-13. Статус: код написан, compile PASS, Play Mode НЕ запускался
ни в какой топологии (по поручению: зафиксировать для будущих
мультиплеер-тестов).

## Что не работало бы во втором клиенте без этого тикета

1. Участники — только локальный игрок хоста. PlayerObject второго клиента
   серверно остался бы в старых координатах, пока мир уехал на 56 км.
2. F8 на клиенте выполнял бы транзакцию локально (без authority) —
   рассинхрон сцены и потенциальный чит-вектор. Запрета не было.
3. Client-side состояние второго клиента (камера, deathY, кэш платформы)
   никто не сдвигал: серверный код slice на клиенте не выполняется.

## Решение (3 части, всё best-effort с маркерами)

### 1. Участники: все подключённые игроки

`TryBuildParticipants` принимает `NetworkManager` и после локального игрока
добавляет каждый `ConnectedClients[].PlayerObject` как
`PLAYER_FRAME/REMOTE_<clientId>` (если не покрыт зарегистрированным корнем).
Сервер двигает всех игроков вместе с миром.

### 2. F8 только на сервере

`RequestControlledRebase` отклоняет (`Rejected(rebase_requires_server)`),
если `NetworkManager.Singleton == null || !IsServer`. Клиент не может
запустить транзакцию локально. Хосту (IsServer=true) не мешает.

### 3. Broadcast сдвига клиентам (CustomMessagingManager)

Сцена/префабы не меняются, RPC-инфраструктуры нет — именованные сообщения
NGO (`CustomMessagingManager`, проверено по исходникам пакета 2.13):

- имя `"FO06_REBASE_SHIFT"`, payload `RebaseShiftMessage : INetworkSerializable`
  (`float Dx/Dy/Dz + ulong FrameGeneration`, каждый float через
  `BufferSerializer.SerializeValue`, запись `WriteValueSafe`, чтение
  `ReadValueSafe` — все overloads подтверждены в исходниках NGO);
- отправка только на success-пути после `Completed`
  (`ReliableSequenced`, маркер `BroadcastShifted`);
- rollback вещественного сдвига не делает (apply + restore = net zero) —
  broadcast нет;
- приёмник регистрируется в `OnEnable`, снимается в `OnDisable`;
  сервер входящее игнорирует (применил синхронно);
- клиент применяет ТОЛЬКО локальное состояние: камера (`ShiftCameraHistory`),
  deathY + платформа локального игрока (`ShiftPlayerFrameReferences`
  без touching флагов транзакции). Телепорт NT и палубы — серверные,
  клиент их не трогает. Маркер `ClientShiftApplied`.

## Явно НЕ покрыто (будущие gates, зафиксировать тестами)

- Late join во время/после сдвига: клиент подключается к уже сдвинутому миру,
  shift-сообщение он пропустил. Нужен handshake (frame generation в hello —
  задел есть в T-FO06E, не подключён).
- Потеря пакета сверх ReliableSequenced / переподключение: восстановления нет.
- F8 на клиенте теперь отклоняется с `Rejected` — UX-заглушка, не объяснение.
- Проверка в топологии Host + 1 клиент (движение, прыжок, палуба, камера,
  второй F8) — NOT RUN. `runtimeRebaseReadiness=NOT_READY` сохраняется.

## Проверка

1. Compile: `refresh_unity` + `read_console` — 0 errors, 0 CS.
2. Мультиплеер-тесты — NOT RUN (зафиксировано этим тикетом для будущего).
