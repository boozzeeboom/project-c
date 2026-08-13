# Investigation: Микротряска персонажа при стоянии (Character Micro-Jitter)

**Дата:** 2026-07-26  
**Статус:** ⛔ НЕ РЕШЕНО (2026-07-26) — первопричина не найдена. Локализовано: humanoid-скиннинг при игре анимации; R1 (GPU Deformation/skinning) и R5 (float-точность) исключены. См. §9.  
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

## 5-бис. Результаты диагностики (2026-07-26)

**Все проверенные гипотезы исключены:**

| Гипотеза | Тест | Результат | Вывод |
|----------|------|-----------|-------|
| H1: keep-grounded -2f → CC.Move penetration | -0.5f (T-JITTER04) | ❌ тряска осталась | CC.Move penetration — не причина |
| H1: CC.Move вообще | skip-Move при стоянии (T-JITTER05) | ❌ тряска осталась | **CC.Move НЕ является источником тряски** |
| H2: stepOffset tug-of-war | stepOffset=0.01 (T-JITTER06) | ❌ тряска осталась | stepOffset не причастен |
| H3: NT.Interpolate | Interpolate=false для всех (T-JITTER07) | ❌ тряска осталась | NetworkTransform не причастен |
| FixedUpdate dirty | no-op Slerp удалён + NT smoothing off (T-JITTER08) | ❌ тряска осталась | Transform dirty не причина |

**Ключевой инсайт:** источник тряски — **НЕ** CharacterController и **НЕ** NetworkTransform. При полном пропуске `CC.Move()` тряска сохраняется.

**Что остаётся (нерасследованные гипотезы):**

### 🆕 H4: Animator bone animation в idle

Idle-анимация `HumanM@Idle01` (Kevin Iglesias) — даже без root motion, кости скелета микро-двигаются (дыхание, перенос веса). Через SkinnedMeshRenderer это даёт видимое смещение вершин меша каждый кадр.

**Проверка:** отключить Animator на `Visual_Model` → если тряска пропала → H4.

### 🆕 H5: CharacterController skinWidth bounce

`skinWidth = 0.08` (префаб). CC постоянно «касается» земли. Даже без Move(), CC резолвит overlaps через skinWidth → микро-толчки.

**Проверка:** `_controller.enabled = false` на 1 кадр при стоянии → если тряска пропала → H5.

### 🆕 H6: Скрипты кастомизации (CharacterCustomisationApplier / CharacterEquipmentVisualApplier)

Модифицируют кости/меш в Update/LateUpdate через Animator API.

**Проверка:** временно отключить оба компонента на префабе.

### 🆕 H7: Floating-point precision / GPU skinning

Персонаж далеко от origin (0,0,0)? FP-ошибки в матрице трансформации → sub-pixel jitter в SkinnedMeshRenderer.

**Проверка:** телепортироваться к origin и проверить.

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
| 2026-07-26 | T-JITTER05 (89613f8) | H1: skip CC.Move при стоянии (Вариант C) ❌ |
| 2026-07-26 | T-JITTER06 (99e11ef) | H2: stepOffset 0.3 → 0.01 ❌ |
| 2026-07-26 | T-JITTER07 (ce6f658) | H3: NT.Interpolate=false для всех клиентов ❌ |
| 2026-07-26 | T-JITTER08 (107ce4d) | FixedUpdate no-op Slerp удалён + NT.PositionLerpSmoothing=false ❌ |
| 2026-07-26 | T-JITTER09 (53bfdf1) | H5: CC.enabled=false при idle ❌ |
| 2026-07-26 | T-JITTER10 (83a62ec) | H4/H8: diagnostic — _diagnosticDisableAnimator ✅ Аниматор подтверждён |
| 2026-07-26 | T-JITTER11 (5fc5768) | skinnedMotionVectors=false в коде — motion vectors усиливают микро-кости |
| 2026-07 | T-JITTER12 (7487e8ab) | Ресерч №2: edit-mode зонд — humanoid-оценка клипа ГЛАДКАЯ, шум не в данных/аватаре/origin ≤3км |
| 2026-07 | T-JITTER13 (acc15f2a, c4e608d6, e56a4f00) | Runtime-зонд BoneJitterRuntimeProbe: Input System fix, ленивый поиск аниматора, поиск по avatar. Замеры: world-дельты костей @56.7км = float32-шум, реальную тряску не видят |
| 2026-07 | T-JITTER14 (6b2777cb → 109e78dc) | R5 fix (порог FloatingOrigin 100км→3км) ❌ опровергнут → revert. R1 тест (GPU Deformation Off + GPU Skinning Off) ❌ тряска осталась. Итог: ответ НЕ найден (§9) |

---

## 8. Ресерч №2 (T-JITTER12/13): изоляция слоя шума измерением

### 8.1 Метод

Все предыдущие тесты были «фиксами вслепую». Добавлен **измерительный инструментарий**:

- **`Assets/_Project/Scripts/Editor/JitterClipProbe.cs`** (edit-mode, без плейтеста):
  сэмплирует клип через `AnimationMode.SampleAnimationClip` (тот же путь, что окно Animation,
  корректная humanoid muscle-оценка) и меряет покадровые дельты костей (мм/град),
  осцилляции (sign-flips) и RMS второй разности (плавность).
- **`Assets/_Project/Scripts/Debug/BoneJitterRuntimeProbe.cs`** (runtime, вешать на root
  персонажа/NPC, F9 — пауза): логирует переходы state machine, покадровые дельты костей
  и дистанцию от origin — различает 3 слоя (state machine / кости / рендер) за 1 сессию.

### 8.2 Измерения (JitterClipProbe, ~90fps с неравномерным dt, 600 шагов)

| Прогон | Hips maxStep | Head | Hand.L | Foot.L | Sign-flips |
|--------|-------------|------|--------|--------|------------|
| A) Humanoid Idle01 @ origin | 0.48мм | 1.26мм | 0.68мм | 0.013мм | 0–1 за 6.7с |
| B) Humanoid Idle01 @ (3000,0,0) | 0.48мм | 1.26мм | 0.68мм | 0.107мм | 0 |
| C) Generic @Idle @ origin | 5.75мм | 3.64мм | 4.32мм | 8.52мм | 3–8 за 6.7с |

**Выводы:**

1. **Humanoid-оценка клипа математически гладкая** — шум НЕ в данных клипа, НЕ в muscle-конверсии, НЕ в аватаре.
2. **Расстояние от origin до 3км не влияет** на трансформы костей (CPU-side). H7 ослаблена
   (остаётся актуальной только если реальные игровые координаты ≫3км — GPU-матрицы float32
   дают ~0.36мм/3км, ~3.6мм/30км).
3. Клип НЕ сжат (`animationCompression: 0` на HumanM@Idle01.fbx).

### 8.3 Проверенная конфигурация (факты)

- Unity **6000.5.2f1**, URP. `gpuSkinning: 1`, `meshDeformation: 2` (GPU Deformation включена),
  GPUResidentDrawerMode: 0 (выкл), TAA на камере выключен (`m_Antialiasing: 0`),
  Motion Blur volume в проекте не найден.
- Аватар Humanoid (`HumanM_Model.fbx`, CreateFromThisModel, auto-mapping). Клипы Humanoid
  (CopyFromOther avatar). SMR: updateWhenOffscreen=false, AABB полноразмерный (1.83×1.93×0.50),
  52 кости, rootBone=B-hips.
- Animator (игрок, на Visual_Model): updateMode=Normal, cullingMode=AlwaysAnimate, applyRootMotion=false.
  Animator (NPC, на child "Visual"): Normal / CullUpdateTransforms / no root motion; внутренний
  Animator модели у NPC **отключён** (m_Enabled: 0).
- Скрипты кастомизации/экипировки кости **покадрово не трогают** (только по событиям snapshot).
- SkillAnimationPlayer в idle неактивен (но см. 8.5, R4).
- Игрок: `SetBool("IsGrounded", _controller.isGrounded)` **каждый кадр** (NetworkPlayer.cs:904).
  Переход Idle→Fall по `IsGrounded IfNot` (duration 0.1) — 1 кадр false = цикл блендинга.
- NPC: `SetFloat("Speed", _agent.velocity.magnitude)` каждый brain-tick (NpcBrain.cs:1243);
  пороги Idle→Walk 0.1 / Walk→Idle 0.05 — velocity-шум NavMeshAgent может пересекать пороги.

### 8.4 Оставшиеся гипотезы (ранжированы)

| # | Гипотеза | Почему жива | Проверка |
|---|----------|-------------|----------|
| R1 | **GPU Deformation/skinning path** (Unity 6.5) — единственный слой между «гладкими костями» и пикселями | Всё выше слоя измерено и чисто | Тест 1 (ниже) — 1 чекбокс |
| R2 | **Idle↔Fall фликер** от 1-кадровых `_controller.isGrounded=false` (keep-grounded -2f пенетрация) | Не исключён чисто: в T-JITTER09 (CC off) параметры замораживались, но характер тряски тогда не задокументирован | Тест 2 (runtime probe): пачки переходов Idle→Fall→Idle |
| R3 | **NPC Speed-шум** через пороги 0.05/0.1 → Idle↔Walk блендинг | `velocity.magnitude` у стоящего NavMeshAgent нестабилен | Тот же probe на NPC |
| R4 | **applyRootMotion=true застревает после скилла** (SkillAnimationPlayer включает, восстанавливает в Restore(); прерванный каст = root motion включён навсегда) | Косвенно | В probe: `rootMotion=True` в стартовом логе |
| R5 | Реальные координаты ≫3км (GPU float) | FloatingOrigin threshold=150км | Teleport к origin |

### 8.5 План бинарных тестов (плейтест, ~5 минут)

1. **Тест 1 — GPU Deformation Off.** Project Settings → Player → Other Settings → GPU Deformation → **Off**. Play → наблюдать. Тряска пропала → R1. (52 кости × 2 персонажа — CPU skinning бесплатен.)
2. **Тест 2 — Runtime probe.** Повесить `BoneJitterRuntimeProbe` на игрока и NPC, постоять 30с.
   - `transitions=N/s` при стоянии → R2/R3 (state machine фликер)
   - `transitions=0`, `maxStep hips ≫1.3мм` → шум в костях (рантайм-специфика, уточнить)
   - `transitions=0`, `maxStep ≤1.3мм`, а на экране тряска → слой рендера/камеры → R1/R5
3. **Тест 3 — Teleport к (0,1,0).** Тряска пропала → R5.
4. **Тест 4 — После любого скилла** посмотреть стартовый лог probe: `rootMotion=True` → R4.

### 8.6 Фиксы по исходам

- **R1**: GPU Deformation = Off (постоянно) или обновление Unity.
- **R2**: dead-zone на IsGrounded: сообщать `false` в Animator только после 3+ подряд кадров
  `isGrounded==false` (или ~0.05с), `true` — сразу. 1-кадровые провалы CC гасятся.
- **R3**: dead-zone на Speed: `if (speed < 0.15f) speed = 0f` перед SetFloat + `_agent.isStopped=true` в Idle.
- **R4**: в SkillAnimationPlayer добавить восстановление `applyRootMotion` в OnDisable/по таймауту.
- **R5**: снизить FloatingOrigin threshold (150км → 3–5км) или включить player roots в shift-набор.

---

## 9. Ресерч №2 — результат (T-JITTER13/14): ❌ ответ НЕ найден

### 9.1 Runtime-измерения (BoneJitterRuntimeProbe, плейтест пользователя)

Чистый Idle на `distOrigin=56.7км`:

| | Hips | Head | Hand.L |
|---|---|---|---|
| Edit-mode @ origin (клип гладкий) | 0.48мм | 1.26мм | 0.68мм |
| Runtime @ 56.7км (Idle) | 3.97мм | 7.83мм | 5.55мм |
| float32 точность на 56.7км | ≈ 6.7мм | | |

**Ключевой вывод о методе:** цифры runtime @56.7км — это **квантование float32 в самом
измерении** (6.7мм), а не сигнал тряски. Тот же шум был бы у Т-позы и у любых объектов.
Следствие: `BoneJitterRuntimeProbe` (меряет world-дельты костей) **в принципе не видит**
реальную тряску, т.к. она возникает не в костях (они гладкие, §8.2), а в вершинах на
скиннинге. Для R1/R5 зонд непригоден — только визуальный тест.

Переходы `Idle→Fall→Land→Idle` в логе — настоящие падения (Fall длился ~5с), а не
1-кадровый фликер → **R2 этими данными не подтверждён**.

### 9.2 Результаты бинарных тестов (§8.5)

| Гипотеза | Тест | Результат | Вывод |
|----------|------|-----------|-------|
| R5: float-точность на 56км | Тест 3 + косвенные наблюдения | ❌ | Другие объекты рядом, Т-поза при движении, анимированные предметы на тех же координатах НЕ трясутся → артефакт НЕ глобальный |
| R1: GPU Deformation/skinning | Тест 1 (`meshDeformation=CPU`, `gpuSkinning=false`) | ❌ тряска осталась | GPU-скиннинг/деформация НЕ причина (настройки возвращены к исходным) |

### 9.3 Что осталось неразрешённым

Точка локализации сузилась до **рендер-слоя humanoid-скиннинга при игре анимации**:

- Animator выключен (Т-поза) → тряски НЕТ (подтверждено пользователем).
- Другие объекты / брошенные предметы / корабль в тех же координатах → тряски НЕТ.
- Edit-mode humanoid muscle-оценка клипа → ГЛАДКАЯ (0.48мм, §8.2).
- GPU Deformation Off + GPU Skinning Off → тряска ОСТАЛАСЬ.

Т.е. шум возникает **между гладкими костями и отрендеренными вершинами**, но не в GPU
Deformation/skinning (выключены — не помогло). Неисследованными остались:

- CPU-скиннинг `SkinnedMeshRenderer` сам по себе (vertex pipeline при 60–140 fps и переменном dt);
- Animator update timing (флуктуации deltaTime 60–140 fps в muscle-оценке);
- специфика Idle-клипа (дыхание) на sub-pixel масштабе.

**Итог: первопричина НЕ установлена. Дальнейшая работа остановлена по решению пользователя («больше не кодим»).**

### 9.4 Следы в коде (для будущей сессии)

- `Assets/_Project/Scripts/Debug/BoneJitterRuntimeProbe.cs` — runtime-зонд (оставлен, рабочий, Input System).
- `Assets/_Project/Scripts/Editor/JitterClipProbe.cs` — edit-mode зонд (оставлен).
- FloatingOrigin порог ВОЗВРАЩЁН к 100км (revert `109e78dc`) — R5 исключён.
- GPU Deformation/Skinning ВОЗВРАЩЕНЫ к GPUBatched/True — R1 исключён, проект в исходном состоянии.

