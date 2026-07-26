# Investigation: Микротряска персонажа при стоянии (Character Micro-Jitter)

**Дата:** 2026-07-26  
**Статус:** Тестирование H1 — skip CC.Move при стоянии (89613f8)  
**Тикет:** (связанные T-JITTER01, T-JITTER01-v2, T-JITTER02, T-JITTER02v2, T-JITTER03, T-CAM05v2)

---

## 1. Симптомы

- Персонаж заметно «трясётся» (микро-осцилляции) когда стоит на месте
- Другие объекты рядом с персонажем НЕ трясутся
- На корабле при полёте (режим Flying, F-ключ) — НЕ трясётся
- Другие анимированные подбираемые предметы (PickupItem) — НЕ трясутся
- Тряска видна как микросмещения позиции/модели персонажа, камера стабильна

---

## 2. Что уже было сделано (git log — не повторять)

| Коммит | Суть фикса | Результат |
|--------|-----------|-----------|
| T-JITTER01 (7d1293d) | Фильтрация стационарных Rigidbody в moving-platform carry | Статичная геометрия не считается платформой → платформенный перенос не спамит |
| T-JITTER01-v2 (c32b1e4) | NetworkTransform: `Interpolate=false`, `AuthorityMode=Owner` | Убрана борьба NT.Interpolate с CC.Move |
| T-JITTER02 (b383443) | keep-grounded -2f → -0.5f | Меньше пенетрация, но слабая привязка к склонам |
| T-JITTER02v2 (1c5a54e) | **Ревёрт -0.5f → -2f** + гравитация только в воздухе | Гравитация больше не копится поверх keep-grounded |
| T-JITTER03 (38717da) | Clamp _currentDistance по near-clip | Убрана осцилляция камеры от near-clip push |
| T-CAM05v2 (f56b447) | Near-clip защита на финальной позиции (не на target) + `WriteDefaultValues=0` | Убран push-Lerp-push цикл камеры |
| T-CAM12 (4c2fd05) | Цепной SphereCast, positionSmoothTime 0.04→0.06 | Более плавная камера |
| T-CAM13 (297dc99) | Dead-zone 3mm, vertical lag acceleration | Убиты микро-осцилляции камеры |

---

## 3. Анализ текущего кода

### 3.1 ProcessMovement (путь при стоянии)

Файл: `Assets/_Project/Scripts/Player/NetworkPlayer.cs`, строка 873–962

```csharp
// Каждый кадр при стоянии:
_isGrounded = _controller.isGrounded;
_velocity.y = -2f;                                           // keep-grounded
// нет ввода → horizontalVel = Vector3.zero
// ветер при grounded+без ввода = 0
// gravity НЕ применяется (только в воздухе)
motion.y = _velocity.y;                                      // = -2f
_controller.Move(new Vector3(0, -2f, 0) * Time.deltaTime);  // каждый кадр!
```

Даже при полном отсутствии ввода `CharacterController.Move()` вызывается с ненулевым downward-вектором.

### 3.2 NetworkTransform — расхождение префаба и кода

**В префабе** (`Assets/_Project/Prefabs/NetworkPlayer.prefab`):
```yaml
Interpolate: 1                    # true
AuthorityMode: 1                  # Owner
```

**В коде** (`OnNetworkSpawn`, строка 248–256):
```csharp
if (IsOwner) {
    nt.Interpolate = false;
    nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
}
```

→ На **remote-клиентах** Interpolate остаётся `true` (из префаба).  
→ У них `_controller.enabled = false`, но NetworkTransform интерполирует позицию от сервера.

### 3.3 Prefab-значения, отличающиеся от кода

| Поле | Prefab | Code default | Комментарий |
|------|--------|-------------|-------------|
| `runSpeed` | **50** | 10 | Значительное расхождение |
| `positionCorrectionThreshold` | **0.5** | 99999f | **АКТИВНО** (маленький порог!) |
| `positionCorrectionSpeed` | **10** | 5 | Усиленная коррекция |
| `_gatherScaleAmplitude` | **0.08** | 0 | Активен fallback gather-pulse |

**Важно:** `positionCorrectionThreshold=0.5` — потенциально active trigger в FixedUpdate (строка 831–841), НО `_hasServerPosition` выставляется только в `TeleportToPosition()` и тут же сбрасывается → код коррекции де-факто мёртв.

### 3.4 FixedUpdate — мёртвый код

```csharp
private void FixedUpdate() {
    // _hasServerPosition = true только в TeleportToPosition → почти никогда
    if (_hasServerPosition) { /* position correction */ }
    
    // NO-OP: Slerp rotation к самому себе
    transform.rotation = Quaternion.Slerp(transform.rotation, transform.rotation, rotationSpeed * Time.fixedDeltaTime);
}
```

Строка 844 — бессмысленный вызов каждый FixedUpdate (Slerp to self). Не влияет на позицию.

### 3.5 Camera Pipeline (SpringArmCamera.cs)

Хорошо отлажена:
- Dead-zone 3mm в `SmoothPosition` → фильтр микро-осцилляций
- Near-clip защита на финальной позиции (не на cameraTargetPos) → нет push-Lerp loop
- `Mathf.Exp` decay вместо SmoothDamp → без резонанса в каскаде с Lag
- Lag отключается в ship mode (`_isShip = true`)

### 3.6 Animator

- Controller: `Assets/_Project/Animations/PlayerAnimation.controller`  
- Idle motion: `HumanM@Idle01.fbx` (Kevin Iglesias)  
- `WriteDefaultValues: 0` на всех состояниях  
- `applyRootMotion` не установлен (дефолт false)  
- Animator сообщает параметры `IsGrounded`, `Speed`, `InCombat`, `MoveX`, `MoveY`

→ Animator не меняет transform персонажа, только кости скелета.

### 3.7 Moving-platform carry

`DetectGroundPlatform()` (строка 984–1010):
- SphereCast вниз по `_platformMask = ~0` (все слои)
- Если hit без Rigidbody → null (статичная геометрия — не платформа)
- Если Rigidbody спит → null
- Если Rigidbody awake → платформа (корабль, лифт)

→ На статичном грунте платформа не детектится → `_platformDelta = 0`.

---

## 4. Гипотезы

### 🥇 H1 (60%): keep-grounded -2f → CharacterController penetration → micro-bounce

**Механизм:**
```
Кадр 1: CC.Move(Vector3.down * 2 * 0.0167) → пенетрация 3.3 см
      → Physics engine выталкивает CC вверх
      → isGrounded может мигнуть false на 1 кадр
Кадр 2 (если !isGrounded): _velocity.y = -2f (ещё) + gravity (-20 * dt)
      → motion.y = -2.33f → пенетрация глубже
      → Physics выталкивает сильнее
```

Цикл push-resolve создаёт положительную обратную связь. Визуально — высокочастотная тряска (≈frame-rate / 2 — frame-rate).

**Почему корабль не трясёт:** `_controller.enabled = false` → Move() не зовётся.
**Почему pickup-предметы не трясут:** нет CharacterController и keep-grounded.

**Ранее пробовали:** T-JITTER02 сменил -2f→-0.5f. Ревертнули в T-JITTER02v2 с комментарием: *«-0.5f недостаточно для slope/platform-stick»*.  
**НО** на момент T-JITTER02 гравитация ещё копилась поверх keep-grounded (`_velocity.y += gravity * Time.deltaTime` БЕЗ условия `!groundedForMovement`).  
Сейчас гравитация отделена → -0.5f может быть достаточно.

---

### 🥈 H2 (20%): Step Offset (0.3m) + keep-grounded tug-of-war

**Механизм:**
- `CharacterController.stepOffset = 0.3` (префаб)
- CC пытается зашагивать на препятствия до 0.3 м
- keep-grounded -2f толкает вниз → CC интерпретирует как «спуск с уступа» → step detection двигает вверх
→ микро-конфликт step-UP vs keep-grounded-DOWN

---

### 🥉 H3 (15%): NetworkTransform Interpolate=true на remote-клиентах

**Механизм:**
- Remote-клиент: Interpolate=true (из префаба, код не перебивает для !IsOwner)
- Если PositionThreshold=0.001 (префаб) и серверная позиция флуктуирует (от H1) → Lerp между флуктуирующими позициями → видимая тряска

**Проверка:** в single-player (Host без remote clients) тряска есть? Если да — H3 не причина.

---

### H4 (5%): Animator root motion curves

Даже с applyRootMotion=false, некоторые анимации Kevin Iglesias имеют root-кривые. При `WriteDefaultValues=0` Animator не сбрасывает их, но на корневой transform они не применяются → маловероятно.

---

## 5. План решений

### Фаза 1 — Диагностика (в инспекторе / временный код)

| Шаг | Что сделать | Как проверить | Ожидание |
|-----|-------------|---------------|----------|
| 1.1 | Снизить keep-grounded до -0.5f | Закомментировать `_velocity.y = -2f` → `_velocity.y = -0.5f` | Тряска уменьшилась/исчезла → H1 |
| 1.2 | Убрать keep-grounded при стоянии | `if (hasInput || !groundedForMovement) { /* keep-grounded */ }` else skip | Тряска пропала → H1 |
| 1.3 | stepOffset = 0.01f | Временно в инспекторе префаба | Тряска пропала → H2 |
| 1.4 | Interpolate=false для всех | В `OnNetworkSpawn` убрать `if (IsOwner)` guard | Тряска на remote пропала → H3 |

### Фаза 2 — Выбор фикса

#### Вариант A: Мягкий keep-grounded (-0.5f)

```csharp
// NetworkPlayer.cs, ProcessMovement, строка 890
if (groundedForMovement && _velocity.y < 0) _velocity.y = -0.5f;
```

**Pros:** Минимальное изменение, 1 строка.  
**Cons:** Может не хватить для slope/platform-stick (был реверт в T-JITTER02v2, но тогда gravity не была отделена).

#### Вариант B: Кастомная grounded-проверка (рекомендуется)

Заменить `_controller.isGrounded` на `Physics.SphereCast` вниз.  
Если дистанция до земли < порога — считаем grounded и **не применяем keep-grounded вообще**.

```csharp
private bool CustomGroundCheck() {
    Vector3 origin = transform.position + _controller.center;
    float checkDist = _controller.stepOffset + _controller.skinWidth + 0.05f;
    return Physics.SphereCast(origin, _controller.radius * 0.8f, Vector3.down, 
                              out _, checkDist, ~0, QueryTriggerInteraction.Ignore);
}
```

И в ProcessMovement:
```csharp
bool customGrounded = CustomGroundCheck() || _onPlatform;
bool groundedForMovement = _isGrounded || _onPlatform;  // оставить как fallback

// keep-grounded только если кастомная проверка не прошла (висит в воздухе над землёй)
if (!customGrounded && groundedForMovement && _velocity.y < 0)
    _velocity.y = -1f;
```

**Pros:** Не создаёт пенетрацию, не зависит от isGrounded flicker, решает H1+H2 одновременно.  
**Cons:** Больше кода, нужно тестировать на склонах и ступеньках.

#### Вариант C: Минимальный — не вызывать Move при полном стоянии

```csharp
// Если нет ввода, grounded, и ветер = 0 — не двигаемся вообще
if (!hasInput && groundedForMovement && windVel.sqrMagnitude < 0.0001f) {
    // Пропускаем Move — keep-grounded не нужен, персонаж уже на земле
    // Обновляем только Animator
} else {
    _controller.Move(motion * Time.deltaTime + _platformDelta);
}
```

**Pros:** Минимальное изменение, не меняет keep-grounded логику.  
**Cons:** Нужно быть уверенным что `_velocity.y` остаётся консистентным для прыжка.

### Фаза 3 — Чистка FixedUpdate

```csharp
private void FixedUpdate() {
    // Удалить:
    // transform.rotation = Quaternion.Slerp(transform.rotation, transform.rotation, ...);
}
```

### Фаза 4 — Синхронизация префаба с кодом (если need)

Привести `positionCorrectionThreshold` и `positionCorrectionSpeed` в префабе к актуальным значениям из кода (или наоборот, если threshold=0.5 нужен для реконнекта).

---

## 6. Критерии успеха

- [ ] Персонаж стоит на месте — визуально неподвижен (±0.5px на экране)
- [ ] Персонаж на склоне — не сползает, не трясётся
- [ ] Персонаж на движущейся платформе (палуба корабля) — следует за платформой без джиттера
- [ ] Персонаж прыгает и приземляется — keep-grounded достаточно сильный, чтобы персонаж не «всплывал» после приземления
- [ ] Режим полёта (корабль) — поведение не изменилось

---

## 7. История изменений

| Дата | Сессия | Изменения |
|------|--------|-----------|
| 2026-07-26 | Диагностика | Первичный анализ кода, 4 гипотезы, 3 варианта фикса |
| 2026-07-26 | T-JITTER04 (a3cb625) | H1: keep-grounded -2f → -0.5f (Вариант A) ❌ |
| 2026-07-26 | T-JITTER05 (89613f8) | H1: skip CC.Move при стоянии (Вариант C) |
