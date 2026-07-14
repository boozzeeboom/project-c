# T-ENG01: ShipEngineVisual — Post-Mortem и план переинтеграции

## Что пошло не так

Коммит `d6fde5f` (T-ENG01: ShipEngineVisual) сломал управление кораблём. Корабль не реагировал на W/S/A/D — `currentThrust` всегда оставался 0.

Диагностика подтвердила, что вся цепочка ввода работает:
- `Keyboard.current.wKey.isPressed = True` ✅
- `IsActionHeld(ShipThrustForward) → true` ✅
- `SendShipInput(thrust=1)` вызывается ✅
- `SubmitShipInputRpc` исполняется с `pilotsContains=True` ✅
- Но `_sumThrust` не накапливается → `currentThrust = 0` 🔴

## Корневые причины

### 1. GlobalObjectIdHash — перегенерация хешей (КРИТИЧНАЯ)

При добавлении любого MonoBehaviour на префаб, являющийся частью иерархии NetworkObject, Unity пересчитывает `GlobalObjectIdHash` для всего NetworkObject. В коммите изменились хеши **всех 18 NetworkObject'ов** в BootstrapScene.

Netcode использует `GlobalObjectIdHash` в `NetworkPrefabHandler` для:
- Регистрации префабов
- Маршрутизации RPC-сообщений
- Идентификации NetworkObject в сетевых пакетах

При изменении хеша RPC-сообщения попадают в очередь, но `NetworkUpdate` не может корректно их диспатчить → `_sumThrust` не накапливается.

**Решение:** Никогда не сохранять BootstrapScene после добавления компонентов на префабы без полной проверки. Использовать отдельную тестовую сцену.

### 4. НЕДОСТАТОЧНЫЙ АНАЛИЗ КОДА (Коренная причина)

T-ENG01 создавал `ShipEngineVisual` с нуля, не проанализировав существующие подсистемы:
- `ShipModuleVisualApplier` (L1 визуал) — уже был готов, мог спавнить/уничтожать визуал модулей
- `ShipRootReference` / `ShipComponentLocator` — существовали для доступа к ShipController без FindObjectOfType
- `ShipInputReader` — содержал сырой ввод, но не имел публичных геттеров
- `ShipTelemetryState` — уже синхронизировал данные для HUD

ShipEngineVisual пытался управлять `transform.localRotation` напрямую в Update и не знал о существовании `ShipModuleVisualApplier`, который уже занимался спавном визуала.

**Решение:** Перед любым новым компонентом — прочитать все связанные файлы (grep + read_file), составить карту зависимостей, определить точки интеграции. После анализа — документировать план и только потом писать код.

### 2. `_shipWindMultiplier: 1 → 10` (СЛУЧАЙНОЕ)

Значение `_shipWindMultiplier` на WindManager в BootstrapScene было изменено с 1 на 10 — ветер на корабли стал в 10 раз сильнее.

### 3. `ApplyDeflection` — прямая модификация transform (ПОТЕНЦИАЛЬНАЯ)

```csharp
// ShipEngineVisual.cs:115-122
private void ApplyDeflection(float angleDegrees)
{
    var baseRotation = transform.localRotation;
    var baseEuler = baseRotation.eulerAngles;
    transform.localRotation = Quaternion.Euler(baseEuler.x, angleDegrees, baseEuler.z);
}
```

Модификация `transform.localRotation` каждый кадр в `Update()` конфликтует с Rigidbody-физикой. Если слот двигателя находится на GameObject с Rigidbody — физика ломается.

**Решение:** Использовать `Rigidbody.MoveRotation()` для объектов с Rigidbody, либо перенести визуал на отдельный дочерний Transform без Rigidbody.

## План безопасной переинтеграции

### Этап 1: Исправить ShipEngineVisual.cs

1. **ApplyDeflection**: заменить `transform.localRotation = ...` на безопасный подход:
   - Если есть Rigidbody → `_rb.MoveRotation(Quaternion.Euler(...))`
   - Если нет → оставить `transform.localRotation`

2. Вынести `ShipEngineVisual` в **отдельный дочерний GameObject** (не на слот с Rigidbody).

### Этап 2: Не менять BootstrapScene

1. **НЕ сохранять** BootstrapScene при добавлении компонентов.
2. Тестировать на **WorldScene** или отдельной тестовой сцене.
3. Проверить что `GlobalObjectIdHash` не изменились:
   ```bash
   git diff -- Assets/BootstrapScene.unity | grep GlobalObjectIdHash
   ```
   Если есть изменения — откатить сцену.

### Этап 3: Проверочный чеклист

- [ ] `ShipEngineVisual` не на GameObject с Rigidbody
- [ ] BootstrapScene `GlobalObjectIdHash` не изменились
- [ ] `_shipWindMultiplier` = 1
- [ ] Корабль движется на W/S
- [ ] Корабль поворачивается на A/D
- [ ] Лопасти вращаются пропорционально тяге
- [ ] Отклонение двигателя следует за yaw
