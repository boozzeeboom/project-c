# T-FO06AG — aggregated rebase readiness bundle

Дата: 2026-09-11.

## Назначение тикета

`T-FO06AG` выбран как следующий свободный подэтап после T-FO06AF; существующего отчёта `06AG` в floating-origin документах не найдено.

Пользователь разрешил укрупнить этап, если независимые contracts можно безопасно объединить. Поэтому в один serial-independent slice объединены только уже существующие validators:

- live manifest session evidence;
- native adapter readiness;
- runtime readiness gate.

## Реализация

Создан:

```text
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionRebaseReadinessBundle.cs
```

Добавлены:

- `GlobalMotionRebaseReadinessBundle`;
- `GlobalMotionRebaseReadinessBundleBuilder.Build`.

Builder выполняет проверки в фиксированном порядке:

```text
manifest/session evidence
→ native adapter + runtime readiness
→ final readiness bundle
```

Первый отказ останавливает проверку и сохраняется как namespaced reason:

```text
manifest_session:<reason>
runtime_readiness:<reason>
```

## Почему укрупнение безопасно

Этап не создаёт новые runtime side effects и не объединяет независимые native mutations. Он только агрегирует ранее созданные pure validators в единый evidence result. Поэтому не меняются:

- participant discovery;
- manifest publication;
- native adapter invocation;
- runtime driver;
- Unity state;
- Apply/Rebuild/Validate/Publish;
- rollback.

## Текущее состояние

Bundle не может перейти в `IsReady = true`, пока не доказаны реальные session evidence, participant admission, live publication и native adapter readiness:

```text
manifestSessionProven = false
nativeAdaptersReady = false
runtimeReady = false
participantAdmission = NOT_READY
liveManifestPublication = NOT_PROVEN
runtimeRebaseReadiness = NOT_READY
```

## Проверка

```text
check_compile_errors = No compile errors
```

Play Mode не запускался.

## Следующий этап

Использовать bundle как единый read-only evidence result в отдельной user-controlled runtime capture процедуре. До получения валидного bundle не подключать его к runtime driver и не разрешать native mutation.
