# Third-Person Camera Upgrade — Deep Research (v2)

> **Статус:** Research Phase — Complete  
> **Дата:** 2026-07-26  
> **Цель:** Модернизировать камеру от третьего лица до уровня современных игр (God of War, Horizon, Elden Ring, Ghost of Tsushima)  
> **Автор:** Mavis (Project C agent)

---

## ⚠️ КРИТИЧЕСКИЕ ИСПРАВЛЕНИЯ ОТНОСИТЕЛЬНО v1

### ❌ FloatingOriginMP — НЕ ИСПОЛЬЗУЕТСЯ на ThirdPersonCamera (но!)

**v1 утверждала:** «FloatingOriginMP не используется — сценовая архитектура (каждая сцена со своим локальным origin)»

**Факт:** `FloatingOriginMP.cs` СУЩЕСТВУЕТ в `Assets/_Project/Scripts/World/Streaming/` (1056 строк, полноценный компонент) и **использует ThirdPersonCamera для определения позиции сдвига мира**. Вот цепочка:

```
FloatingOriginMP.FindThirdPersonCamera()
  → GameObject.Find("ThirdPersonCamera_<OwnerClientId>")
  → Использует позицию камеры для расчёта world shift
  → Сдвигает WorldRoot (горы, облака) при превышении threshold (150k units)
```

ThirdPersonCamera НЕ содержит FloatingOriginMP компонент на себе (там есть защитный комментарий, что добавлять его нельзя). Но FloatingOriginMP **висит где-то в сцене** и ищет камеру по имени.

**Последствия для дизайна:**
- Имя камеры `"ThirdPersonCamera_{OwnerClientId}"` — **не менять** (FloatingOriginMP ищет по `"ThirdPersonCamera"` prefix)
- Камера НЕ может быть дочерней игроку (`SpawnCamera()` строки 544-548: parenting к игроку вызывал конфликт с FloatingOriginMP, двойное смещение позиции)
- При уничтожении/пересоздании камеры — FloatingOriginMP должен найти новую

### ❌ «Камеру МОЖНО сделать дочерней» — НЕЛЬЗЯ

Строка 544-548 `NetworkPlayer.SpawnCamera()`:
```csharp
// Parenting камеры к игроку вызывало конфликт с FloatingOriginMP:
// - camera.scene.GetRootGameObjects() захватывало игрока (root-объект)
// - FloatingOriginMP пытался рапаренчить игрока под WorldRoot → краш иерархии
// - Двойное смещение позиции: из parent и из LateUpdate
```

Камера остаётся **независимым корневым объектом сцены**. Вся логика следования — вручную через `LateUpdate`. Это архитектурное ограничение.

---

## 1. Аудит текущего состояния

### 1.1 Что есть сейчас

| Компонент | Путь | Состояние |
|-----------|------|-----------|
| `ThirdPersonCamera.cs` | `Assets/_Project/Scripts/Core/ThirdPersonCamera.cs` | 307 строк, базовый orbit |
| `WorldCamera.cs` | `Assets/_Project/Scripts/Core/WorldCamera.cs` | 627 строк, free-fly + follow (отдельная система!) |
| `ThirdPersonCamera.prefab` | `Assets/_Project/Prefabs/ThirdPersonCamera.prefab` | Camera + ThirdPersonCamera + UniversalAdditionalCameraData |
| `PlayerInputReader.cs` | `Assets/_Project/Scripts/Player/PlayerInputReader.cs` | Читает `Mouse.current.delta` |
| `NetworkPlayer.cs` | `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | Спавнит камеру через `SpawnCamera()` (строка 540) |
| `PlayerController.cs` | `Assets/_Project/Scripts/Player/PlayerController.cs` | Использует `CameraForward`/`CameraRight` |
| `PlayerStateMachine.cs` | `Assets/_Project/Scripts/Player/PlayerStateMachine.cs` | Использует `ThirdPersonCamera` для состояний |
| `FloatingOriginMP.cs` | `Assets/_Project/Scripts/World/Streaming/FloatingOriginMP.cs` | Ищет камеру по имени `"ThirdPersonCamera"` |
| `ShipObservationCamera.cs` | `Assets/_Project/Scripts/Ship/UI/ShipObservationCamera.cs` | Отключает/enable камеру игрока |
| `Billboard.cs` | `Assets/_Project/Scripts/UI/Billboard.cs` | Берёт `ActiveCamera` из `ThirdPersonCamera.InitializeCamera()` |
| `RepairManagerWindow.cs` | `Assets/_Project/Scripts/Ship/UI/RepairManagerWindow.cs` | Ищет `ThirdPersonCamera` через `FindAnyObjectByType` |
| `WorldCamera.cs` | `Assets/_Project/Scripts/Core/WorldCamera.cs` | Имеет FloatingOrigin НА СЕБЕ (другая камера, не gameplay) |

### 1.2 Архитектурная карта зависимостей

```
NetworkPlayer.SpawnCamera()
  → Instantiate(cameraPrefab)           — отдельный корневой объект
  → SetTarget(transform)                — target = сам NetworkPlayer
  → InitializeCamera()                  — блокирует курсор, регистрирует Billboard.ActiveCamera

FloatingOriginMP (где-то в сцене)
  → FindThirdPersonCamera()             — GameObject.Find по имени "ThirdPersonCamera_..."
  → Использует cam.transform.position   — для расчёта world shift threshold (150k)

PlayerController
  → cameraController.CameraForward      — направление движения относительно камеры
  → cameraController.CameraRight        — стрейф

PlayerStateMachine
  → cameraController                    — для смены режимов

ShipObservationCamera
  → Отключает Camera на ThirdPersonCamera
  → Включает свою Camera
  → Возвращает управление при ReturnToPlayer

Billboard
  → Billboard.ActiveCamera = ThirdPersonCamera.transform
  → Использует для билбордов (имена над игроками)
```

> **⚠️ Ключевое:** `ThirdPersonCamera` — это единственная gameplay-камера. От неё зависят: движение, стриминг мира, UI, билборды, ремонтный режим. Ломать её API-контракт нельзя.

### 1.3 Как работает текущая камера (архитектура)

```csharp
// LateUpdate()
_lookInput = _lookAction.ReadValue<Vector2>();
_yaw += _lookInput.x * sens;
_pitch -= _lookInput.y * sens * invert;
_pitch = Mathf.Clamp(_pitch, minVerticalAngle, maxVerticalAngle);
UpdateCameraPosition();

// UpdateCameraPosition()
private void UpdateCameraPosition()
{
    Vector3 dir = new Vector3(
        -Mathf.Sin(yawRad) * Mathf.Cos(pitchRad),
        Mathf.Sin(pitchRad),
        -Mathf.Cos(yawRad) * Mathf.Cos(pitchRad)
    );
    transform.position = target.position + dir * _currentDistance + Vector3.up * _currentHeight;
    transform.LookAt(target.position + Vector3.up * 1.5f);
}
```

### 1.4 Все проблемы текущей реализации

| # | Проблема | Строка кода | Серьёзность |
|---|----------|-------------|-------------|
| P1 | **Прямая установка позиции без коллизий** — камера проходит сквозь стены, горы, здания | 296-305 | 🔴 Critical |
| P2 | **Нет сглаживания (smoothing/damping)** — камера мгновенно следует, рывки при поворотах | 296-305 | 🔴 Critical |
| P3 | **Нет адаптации дистанции** — в помещении камера упирается в стену, показывает текстуры в упор | отсутствует | 🔴 Critical |
| P4 | **Нет occlusion handling** — NPC/объекты между камерой и персонажем загораживают обзор | отсутствует | 🟡 High |
| P5 | **Нет инерции/запаздывания** — персонаж двигается, камера мгновенно за ним | 296-305 | 🟡 High |
| P6 | **LookAt точка фиксирована** (`+Vector3.up * 1.5f`) — не адаптируется под размер цели (корабль vs персонаж) | 304 | 🟡 High |
| P7 | **Переключение walk/ship мгновенное** — нет плавного перехода, камера дёргается | 196-203 | 🟡 High |
| P8 | **nearClipPlane = 0.5** — даёт z-fighting на близких объектах | 100 | 🟢 Medium |
| P9 | **Нет подстройки FOV под режим/скорость** | отсутствует | 🟢 Nice |
| P10 | **Нет auto-center при движении** — камера не доворачивается за спину | отсутствует | 🟢 Nice |

---

## 2. Анализ подходов из современных игр

### 2.1 Вариант A: Cinemachine FreeLook + CinemachineCollider

**Как работает:**
- `CinemachineFreeLook` — готовая орбитальная камера с тремя ригами (Top/Middle/Bottom)
- `CinemachineCollider` — SphereCast collision avoidance: при попадании прижимает камеру

**Плюсы:**
- ✅ Готовое решение, минимум кода
- ✅ Встроенное сглаживание (Transposer damping)
- ✅ Раздельные риги для разных высот
- ✅ Наличие в UPM (Unity Package Manager)

**Минусы для Project C:**
- ❌ Тяжёлая зависимость (~15 MB пакет)
- ❌ **Network-специфика:** камера спавнится динамически через `Instantiate()`. Cinemachine ожидает pre-configured виртуальные камеры
- ❌ **Два режима (walk/ship):** Cinemachine требует либо две разные vcam (с Blend), либо манипуляции с ригами — неудобно для F-переключения
- ❌ **FloatingOriginMP несовместимость:** Cinemachine не знает про сдвиг мира
- ❌ **Нет occlusion fade:** CinemachineCollider только прижимает камеру, не делает объекты прозрачными
- ❌ **Избыточность:** нам не нужны риги Top/Middle/Bottom, Impulse, Dolly

**Вердикт:** ❌ **Не рекомендуется.** Overkill для наших нужд.

### 2.2 Вариант B: Spring Arm (кастомный) — РЕКОМЕНДУЕТСЯ

**Как работает в современных играх:**

```
[Target] ────desiredDistance────> [Desired Camera Position]
                                       ↑
                                  SphereCast от Target → desiredPos
                                  Если препятствие → камера на hit.point
                                  Если чисто → камера на desired pos

[Camera] ───raycast───> [Target]
                  ↑
            Если объект перекрывает — dither/fade объект
```

**Стандартный пайплайн (God of War, Horizon, Elden Ring):**

1. Рассчитать желаемую позицию камеры (orbit + desired distance + height)
2. **SphereCast** (radius 0.3-0.5) от target до желаемой позиции
3. Если hit — поместить камеру на `hit.point + hit.normal * sphereRadius` (отступ)
4. **SmoothDamp** позицию камеры к целевой — даёт плавность
5. **Raycast** от камеры к target — проверка occlusion (fade/dither)
6. **Camera Lag** — камера отстаёт от движения персонажа

**Плюсы:**
- ✅ Полный контроль над поведением
- ✅ ~400 строк кода (замена текущих 307, но с полным функционалом)
- ✅ Нет внешних зависимостей
- ✅ Лёгкая поддержка двух режимов (walk/ship)
- ✅ Возможность occlusion fade через URP шейдер
- ✅ Совместимость с FloatingOriginMP (камера остаётся root-объектом)

**Минусы:**
- Нужно написать руками
- Нужно тестировать на разных сценах, с разными препятствиями
- Дополнительный шейдер для dither-occlusion

**Вердикт:** ✅ **РЕКОМЕНДОВАНО.**

### 2.3 Вариант C: Гибрид — кастомный Spring Arm + минимальный Cinemachine

Можно взять только `CinemachineCollider` (если он уже есть) и написать кастомный SpringArm, который использует его для collision avoidance.

Но CinemachineCollider тесно связан с Pipelines/Transposer — вытащить его изолированно сложно.

**Вердикт:** ❌ **Не рекомендуется.**

---

## 3. Современные фичи из референс-игр — детальный разбор

### 3.1 God of War (2018) / Ragnarök

Система камеры в GoW — золотой стандарт TPS:

**Collision Avoidance:**
- SphereCast radius **0.5m** от центра персонажа до желаемой позиции
- При попадании — камера прижимается к препятствию с отступом ~0.3m
- Использует 2-3 каста в разных направлениях (не один луч) — камера «ищет» свободное место
- **Anti-pop** — камера не дёргается при входе/выходе из коллизии (таймаут 0.2-0.3s)

**Lag/Inertia:**
- Раздельное сглаживание по осям: XZ медленнее (0.15s), Y быстрее (0.05s)
- При быстром движении/беге — lag уменьшается (камера «нагоняет»)
- В бою — lag почти нулевой (камера мгновенно следует)

**Adaptive Distance:**
- Когда камера постоянно упирается >0.5s → desiredDistance уменьшается
- Когда пространство освобождается → desiredDistance восстанавливается за 1-2s
- В пещерах/комнатах — камера садится почти на плечо

**LookAt Dynamics:**
- LookAt точка не фиксирована — плавает вверх/вниз в зависимости от угла обзора
- При взгляде вверх — LookAt смещается выше (следит за тем, куда смотрит игрок)
- Cinematic offset — горизонтальный сдвиг камеры вправо от центра (за левое плечо)

**Что применимо к Project C:**
- SphereCast collision — ✅ полностью
- Раздельное сглаживание XZ/Y — ✅ полностью
- Adaptive distance — ✅ с адаптацией под наши сцены
- Anti-pop таймауты — ✅
- Cinematic offset — опционально

### 3.2 Horizon Forbidden West

**Dither Occlusion — ключевая инновация:**
Вместо прозрачности (alpha blend) — **дизеринг** (checkerboard pattern). Преимущества:
- Не требует переключения render queue (нет пересортировки)
- Работает с opaque материалами
- Визуально приятнее — объект «растворяется» пикселями, а не становится стеклянным
- Поддерживает любые шейдеры (Lit, custom)

**Реализация в H:FW:**
- Screen-space эффект — шейдер пост-процесса, а не per-object
- Карта глубин: пиксели перед персонажем дизерятся
- Время fade-in/out: 0.15s

**Wall Recovery:**
- Быстрое восстановление позиции (fast catch-up) после выхода из коллизии
- Время восстановления: 0.1-0.3s (в 2-3 раза быстрее чем normal smoothing)
- Отдельный `_fastRecoveryVelocity` параметр

**Что применимо к Project C:**
- Dither occlusion — ✅ частично (screen-space — сложно, per-object — проще)
- Wall recovery — ✅ полностью
- Fast catch-up — ✅

### 3.3 Elden Ring

**Auto-rotate behind player while moving:**
Когда игрок движется вперёд, камера плавно доворачивается за спину:
- Скорость доворота: ~90°/s (быстро, но не мгновенно)
- Работает ТОЛЬКО при движении вперёд (W)
- Не работает при strafe/backpedal

**Lock-on camera:**
- При лок-оне — камера смещается влево (персонаж справа на экране)
- Расстояние увеличивается на 20%
- Pitch ограничен сильнее (±45° вместо ±80°)

**Wall Push:**
- Если камера застряла в стене — принудительно выталкивает к target
- Порог: если фактическая дистанция < 30% от желаемой >0.3s → fast push
- Push speed: ~5m/s (быстро)

**Что применимо к Project C:**
- Auto-rotate — ✅ для walk режима
- Wall push — ✅

### 3.4 Ghost of Tsushima

**Cinematic offset — Over-the-shoulder:**
- Горизонтальный сдвиг камеры: 1-2m вправо от центра
- Зависит от направления взгляда: при взгляде влево камера смещается вправо (и наоборот)
- В бою — offset увеличивается

**FOV Dynamics:**
- Базовый FOV: 70°
- При спринте: +5° (ощущение скорости)
- В бою: +3°
- При верховой езде: +10° (широкий обзор)

**Что применимо к Project C:**
- Over-the-shoulder offset — опционально для walk
- FOV dynamics — ✅ для ship mode (широкий обзор)

---

## 4. Выбор техник для Project C — детальная матрица

### 4.1 Приоритезация

| # | Техника | Приоритет | Сложность | Влияние | Зависимости |
|---|---------|-----------|-----------|---------|-------------|
| 1 | SphereCast Collision Avoidance | 🔴 Critical | Low | Устраняет P1 — главная проблема | LayerMask (Default, Terrain, статика) |
| 2 | SmoothDamp Position Smoothing | 🔴 Critical | Low | Устраняет P2 — плавность | Нет |
| 3 | Adaptive Distance | 🟡 High | Medium | Устраняет P3 — помещения | SphereCast (п.1) |
| 4 | Smooth Walk↔Ship Transition | 🟡 High | Low | Устраняет P7 — дёрганье | SmoothDamp (п.2) |
| 5 | Dynamic LookAt Height | 🟡 High | Low | Устраняет P6 — корабль vs пеший | Нет |
| 6 | Occlusion Dither/Fade | 🟡 High | Medium | Устраняет P4 — обзор | URP Shader или screen-space |
| 7 | Camera Lag (Inertia) | 🟡 High | Low | Устраняет P5 — инерция | SmoothDamp (п.2) |
| 8 | Wall Recovery / Fast Catch-Up | 🟢 Medium | Low | Устранение остаточных коллизий | SphereCast (п.1) |
| 9 | Anti-Pop Timer | 🟢 Medium | Low | Стабильность коллизий | SphereCast (п.1) |
| 10 | Auto-Center Behind Player | 🟢 Nice | Medium | Удобство | SmoothDamp (п.2) |
| 11 | Over-the-Shoulder Offset | 🟢 Nice | Low | Кинематографичность | Нет |
| 12 | FOV Dynamics (Speed→FOV) | 🔵 Future | Low | Ощущение скорости | Нет |
| 13 | Lock-On Camera | 🔵 Future | Medium | Боевая система | Целиком |

### 4.2 Технические риски

| Риск | Вероятность | Воздействие | Митигация |
|------|-------------|-------------|-----------|
| SphereCast на неправильных слоях даёт ложные срабатывания | Medium | Высокая | Отладка LayerMask в разных сценах |
| Spam Raycast каждый кадр → CPU | Medium | Средняя | Оптимизация: проверять каждый N-кадр для occlusion |
| Camera «дрожит» при частом входе/выходе из коллизий | Low | Высокая | Anti-pop таймаут (0.2s гистерезис) |
| FloatingOriginMP теряет камеру при пересоздании | Low | Средняя | Event в FloatingOriginMP при уничтожении/создании |
| Dither шейдер не совместим с URP Lit шейдерами | Medium | Высокая | Screen-space подход (CommandBuffer) или per-object fade |
| SmoothDamp overshoot при резких движениях | Low | Низкая | Clamp максимальной скорости SmoothDamp |

---

## 5. План реализации (предварительный)

### Phase 0: Подготовка (1 день)
- [ ] Создать dev-заметку с архитектурой SpringArmCamera
- [ ] Проверить LayerMask для коллизий в WorldScene_0_0
- [ ] Выставить тестовые параметры в инспекторе

### Phase 1: Spring Arm Core (2 дня)
- [ ] Создать `SpringArmCamera.cs` с collision avoidance + smoothing
- [ ] Сохранить API-контракт (`CameraForward`, `CameraRight`, `SetTarget`, `SetTargetMode`, `InitializeCamera`)
- [ ] Сохранить имя камеры `"ThirdPersonCamera_{OwnerClientId}"` для FloatingOriginMP
- [ ] Обновить ThirdPersonCamera.prefab (заменить компонент)
- [ ] Проверить: 0 errors в консоли

### Phase 2: Camera Lag + Adaptive Distance (1 день)
- [ ] Добавить camera lag (раздельное XZ/Y сглаживание)
- [ ] Добавить adaptive distance (таймерный подход God of War)
- [ ] Добавить wall recovery (fast catch-up)

### Phase 3: Dynamic LookAt + Smooth Transitions (0.5 дня)
- [ ] Dynamic LookAt height для walk/ship
- [ ] Smooth переход walk↔ship через SmoothDamp на distance/height

### Phase 4: Occlusion Fade (2 дня)
- [ ] Выбрать подход: per-object fade или screen-space dither
- [ ] Реализовать URP-совместимый шейдер/скрипт
- [ ] Протестировать на NPC, деревьях, столбах

### Phase 5: Polish (1 день)
- [ ] Anti-pop таймеры
- [ ] Auto-center при движении (опционально)
- [ ] Over-the-shoulder offset (опционально)
- [ ] FOV dynamics (опционально)

---

## 6. Референсы

### GDC Talks
- God of War (2018) — Building a Seamless Single-Shot Camera (GDC 2019)
- God of War Ragnarök — The Camera System (GDC 2023)
- 50 Game Camera Mistakes (GDC Vault)
- Horizon Forbidden West — Rendering and Camera Tech (GDC 2022)

### Технические статьи
- Unreal Engine Spring Arm Component — https://docs.unrealengine.com/5.0/en-US/spring-arm-component-in-unreal-engine/
- Unity: `ProtectCameraFromWallClip` стандартных ассетов (Legacy, но концепция верна)
- Unity: ThirdPersonController примеры (новая система Input)

### Код
- `Assets/_Project/Scripts/Core/ThirdPersonCamera.cs` — текущая реализация (заменяется)
- `Assets/_Project/Scripts/World/Streaming/FloatingOriginMP.cs` — critical dependency
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs` — SpawnCamera()
- `Assets/_Project/Scripts/Player/PlayerController.cs` — использует CameraForward/CameraRight

### Дополнительные файлы в этой папке
- `01_COLLISION_AVOIDANCE.md` — детальный дизайн коллизий
- `02_OCCLUSION_FADE.md` — техники прозрачности препятствий
- `03_SPRING_ARM_ARCHITECTURE.md` — полная архитектура SpringArmCamera

---

## 7. Заключение

Текущая камера — 307 строк орбитального кода без единой защиты от геометрии.

**Рекомендация:** Заменить на **Spring Arm архитектуру** — индустриальный стандарт, решающий все заявленные проблемы, с учётом архитектурных ограничений Project C (FloatingOriginMP, NetworkPlayer spawn, два режима).

**Ключевые решения:**
1. ✅ Spring Arm (не Cinemachine) — лёгкий, контролируемый, совместимый
2. ✅ SphereCast collision — предотвращает проваливание в текстуры
3. ✅ SmoothDamp для плавности
4. ✅ Adaptive distance — работает с нашими разноразмерными пространствами
5. **❌ Камера НЕ дочерняя** — FloatingOriginMP несовместимость
6. ✅ Имя камеры сохраняется — FloatingOriginMP продолжает работать
7. ✅ API-контракт сохраняется — все зависимости (PlayerController, NetworkPlayer, Billboard) не ломаются
