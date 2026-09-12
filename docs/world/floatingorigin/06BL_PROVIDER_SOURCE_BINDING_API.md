# T-FO06BL — provider source binding API

Дата: 2026-09-12  
Статус: **IMPLEMENTED / COMPILE_VERIFIED / DORMANT**

## 1. Реальное действие

`GlobalMotionRebaseAdmissionEvidenceProvider` расширен explicit source-binding API.

Добавлены:

- `TryConfigureSources(...)`;
- `TryValidateSourceBindings(...)`;
- typed contract validation через `Type.IsInstanceOfType`.

## 2. Интеграция

Provider теперь может быть подключён к четырём concrete `MonoBehaviour`-источникам только через проверенный контракт:

```text
reviewed admission source
native adapter source
runtime proof source
rollback source
```

`TryConfigureSources` не принимает компоненты, которые не реализуют требуемый interface. `TryValidateSourceBindings` позволяет проверить serialized binding до попытки получить admission evidence.

После успешной настройки provider всё равно выполняет полную цепочку:

```text
reviewed evidence
→ sealed/full native adapter set
→ runtime proof validation
→ rollback validation
→ participant/frame lineage
→ admission evidence
```

Binding сам по себе не является readiness и не выполняет runtime mutation.

## 3. Текущее состояние

API реализован, но вызов `TryConfigureSources` не выполнялся. В canonical `BootstrapScene` provider и source-компоненты не установлены. У пользователя пока нет accepted structured capture, полного adapter set, reviewed admission source или rollback producer.

```text
provider binding API       = IMPLEMENTED
provider binding executed  = NO
structured intake          = IMPLEMENTED / EMPTY
native adapter set         = INCOMPLETE
reviewed source            = MISSING
runtimeRebaseReadiness     = NOT_READY
```

## 4. Проверки

- `check_compile_errors`: **No compile errors**;
- `git diff --check`: **PASS**;
- `BootstrapScene`: **UNCHANGED**;
- Play Mode: **NOT RUN**;
- manifest publication: **NOT OBSERVED**.

Следующий шаг теперь должен быть не новый контракт, а фактическая serial binding только после появления всех concrete source components и пользовательского evidence.
