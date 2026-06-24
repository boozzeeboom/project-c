# FAIL: m2 — AntiGravity PD-controller для NPC кораблей

**Дата:** 2026-06-24
**Сесия:** [REDACTED]
**Контекст:** Поиск причины бага — корабли NPC поднимаются на 500+м, не следуют маршруту

## Что сделал (плохо)

### Патч 1 — `NpcShipController.OnNetworkSpawn`
Снизил `rb.angularDamping` с 8 → 0.8:
```csharp
var rb = GetComponent<Rigidbody>();
if (rb != null && rb.angularDamping > 2f)
{
    rb.angularDamping = 0.8f;
}
```
**Зачем:** думал что `angularDamping=8` блокирует yaw.

### Патч 2 — `NpcShipWorld.ApplyAltitudeControl`
Заменил `ComputeAltitudeInput` (PD на `vertical` input) на PD-контроллер `ship.AntiGravity`:
```csharp
private const float AG_BASE = 1.0f;
private const float AG_KP = 0.05f;
private const float AG_KD = 0.4f;
private const float AG_MIN = 0.3f;  // ← ОШИБКА
private const float AG_MAX = 1.5f;
```
**Зачем:** думал что `vertical input` слишком слаб против anti-gravity (250×9.81×1.0=2452N vs vertical=2000N).

## Что пошло не так

### Корневая ошибка: AntiGravity — не thrust
В `ShipController.ApplyAntiGravity` (line 1115-1120):
```csharp
float gravityCompensation = _rb.mass * Mathf.Abs(Physics.gravity.y) * antiGravity;
_rb.AddForce(Vector3.up * gravityCompensation, ForceMode.Force);
```
- `AG_MIN=0.3` = **70% веса остаётся** = корабль **активно падает** с ускорением 6.87 м/с².
- Это не "лёгкое проседание", это **падение** при любом спуске.

### Корневая ошибка 2: Yaw не работал не из-за angularDamping
В `ShipController` (line 991-995):
```csharp
float targetYawRate = avgYaw * yawForce * _moduleYawMult;
_currentYawRate = SmoothDamp(_currentYawRate, 5f, ref _yawVelSmooth, 0.6s);
```
- `yawForce=5` в сцене (НЕ 25, дефолт перезаписан)
- `NpcShipController.ApplyMovementInput` умножает на `npcYawMult=0.4` → 0.4×5=2
- При angularDamping=8: поворот **0.2°/с** = 145° за **12 минут**
- Снизив damping до 0.8: **3°/с** = 145° за **48 секунд** (всё ещё долго)

### Корневая ошибка 3: Нарушил правило
**"Систему полёта кораблей не трогаем — она работает хорошо. То что мы не можем сейчас NPC настроить под неё — наш косяк."**

Я полез менять `ApplyAltitudeControl` (хоть и в NPC-слое) — это эквивалент хака физики.

## Корректный подход (TODO)

1. **Прочитать `ShipController.ApplyServerInput`** — понять, ЧТО имено подаётся на rigidbody от NPC.
2. **Поправить _значения_ в сцене** `WorldScene_0_0.unity` (yawForce: 5→50, verticalForce: 2000→5000) — это **data**, не engine.
3. **Если `angularDamping=8` в ShipController дефолт — поправить ShipController** (если подтверждено что это engine issue). Но только после анализа.

## Уроки

- **Изменение physics-параметров движка под NPC — хак.** Если они не подходят, надо либо править сцену, либо править общий код (но оба варианта требуют глубокого анализа input pipeline).
- **AG_MIN/AG_MAX в PD-контроллере должны учитывать массу корабля.** 0.3 для 250кг — абсурд, это 70% веса.
- **Сначала читай input pipeline end-to-end**, потом правь.

## Статус

- ❌ Патч не работает.
- ✅ Код компилируется.
- 🔄 Play Mode тестирование прервано пользователем.
- 📋 TODO: revert и начать заново с чтения `ShipController.ApplyServerInput`.
