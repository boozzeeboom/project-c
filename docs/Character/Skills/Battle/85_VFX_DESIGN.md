# VFX Design — Визуальные эффекты скилов

> **Дата:** 2026-07-31  
> **Статус:** 🔵 Проектирование  
> **Связанные документы:**
> - `SkillNodeConfig.cs` — текущий data-контракт навыка (без VFX-полей)
> - `80_T-INP-08_SKILL_ANIMATION.md` — система анимаций скилов
> - `90_RANGED_AND_THROWABLES.md` — ranged + throwable (существующие VFX: ProjectileVisual, ThrowArcVisual)
> - `100_SKILL_REFACTOR_PLAN.md` — рефакторинг дисциплин (текущий state)
> - `real-time-combat/ITERATIONS.md` — история итераций

---

## TL;DR

Документ описывает архитектуру внедрения VFX в skill-систему. Ключевые решения:

1. **VFX-поля живут в `SkillNodeConfig`** — дизайнер настраивает эффекты на каждый навык в инспекторе
2. **Рантайм-слой абстракции `ISkillVfxProvider`** — позволяет подменять 3D-партиклы ↔ 2D-анимации без изменения кода скилов
3. **Три точки инжекции VFX:** cast (начало каста), projectile/trail (в полёте), impact (попадание)
4. **2D-ready:** архитектура изначально поддерживает покадровую 2D-анимацию через `SpriteAnimationAsset` + `SpriteVfxProvider`

---

## 1. Анализ текущего состояния

### 1.1 Что уже есть (VFX-like)

| Компонент | Файл | Что делает | Качество |
|-----------|------|-----------|----------|
| `ThrowArcVisual` | `Combat/Client/ThrowArcVisual.cs` | Примитивная сфера + LineRenderer trail + сфера-взрыв | 🔴 Временный: создаёт примитивы на лету, свой материал через `Shader.Find`, GC-мусор |
| `ProjectileVisual` | `Combat/Client/ProjectileVisual.cs` | Примитивная сфера + LineRenderer trail | 🔴 Временный: то же, без impact-эффекта |
| `DamageNumberInstance` | `Combat/Client/DamageNumberInstance.cs` | World-space TMP цифры урона | 🟢 Полноценный: object pool + Billboard + distance scaling |
| `SkillAoeDebugVisualizer` | `Skills/Debug/SkillAoeDebugVisualizer.cs` | LineRenderer wireframe AOE-зоны | 🟡 Только Editor/Dev builds, не для production |

### 1.2 Где VFX вызываются сейчас

```
SkillInputService.TryActivate(slot)
  ├─ [cast VFX: НЕТ]
  ├─ SkillAnimationPlayer.Play(config)     ← анимация
  │    ├─ OnAttackImpact (60% клипа)
  │    └─ Restore()
  ├─ ThrowArcVisual.Fire(...)              ← только для throwables (примитивы)
  └─ server.RequestSkillCastRpc(...)
       └─ CombatServer.ResolveSkillCast
            └─ AttackLandedTargetRpc (broadcast)
                 └─ CombatClientState.HandleAttackLanded
                      ├─ [impact VFX: НЕТ]
                      ├─ OnAttackLanded event
                      └─ OnDamageDealt → DamageNumberService ← цифры
```

### 1.3 Чего нет

| VFX-тип | Где должно быть | Статус |
|---------|----------------|--------|
| **Muzzle flash** (вспышка у дула / от персонажа) | `SkillInputService.TryActivate` — до или одновременно с анимацией | ❌ Отсутствует |
| **Melee swing trail** (след клинка) | `SkillAnimationPlayer` — между cast и impact | ❌ Отсутствует |
| **Projectile VFX** (стрела / болт / пуля — НЕ примитивы) | Замена `ProjectileVisual` на префаб-ориентированный спавн | ❌ Примитивы |
| **Throw arc VFX** (граната в полёте — НЕ примитивы) | Замена `ThrowArcVisual` на префаб-ориентированный спавн | ❌ Примитивы |
| **Impact VFX** (взрыв, искры, blood-splat) | `CombatClientState.HandleAttackLanded` | ❌ Отсутствует |
| **AOE zone indicator** (до/после каста) | `SkillInputService` — при зажатии кнопки (aim preview) | ❌ Отсутствует |
| **Cast charge-up** (накопление энергии) | `SkillAnimationPlayer` — до impact | ❌ Отсутствует |
| **2D frame-by-frame анимации** | Любая точка инжекции | ❌ Архитектура не готова |

### 1.4 Существующие VFX-поля в SkillNodeConfig

```
SkillNodeConfig:
  attackClip          AnimationClip   ← 3D анимация (T-INP-08)
  attackClipSpeed     float           ← скорость анимации
  debugVisualizeAoe   bool            ← Editor-only wireframe
  debugVisualizeDuration float        ← длительность wireframe
```

**VFX-полей НЕТ.** Всё что связано с визуальными эффектами отсутствует в data-контракте.

---

## 2. Целевая архитектура

### 2.1 Принципы

1. **Data-driven:** все VFX-параметры — в `SkillNodeConfig`. Дизайнер перетаскивает префабы/спрайты в инспектор.
2. **Абстракция рендера:** код скилов не знает, 3D это или 2D — работает через `ISkillVfxProvider`.
3. **Три чёткие точки инжекции:** cast / projectile+trail / impact.
4. **Object pooling из коробки:** для часто-спавнящихся эффектов (muzzle flash, impacts).
5. **Fallback-friendly:** если префаб не задан — эффект просто не проигрывается (не падает).

### 2.2 Три VFX-слота на навык

```
┌─────────────────────────────────────────────────────┐
│ SkillNodeConfig (SO)                                │
│                                                     │
│  [Cast VFX]           muzzle flash / свечение рук    │
│    ├─ vfxPrefab       GameObject (3D) / Sprite (2D) │
│    ├─ spawnPoint      AttachPoint (Hand/Weapon/Chest)│
│    ├─ duration        0.3s                          │
│    └─ delay           0s                            │
│                                                     │
│  [Projectile/Trail VFX]  снаряд / след              │
│    ├─ vfxPrefab       GameObject / Sprite           │
│    ├─ travelSpeed     30 m/s                        │
│    ├─ trailMaterial   Material (для LineRenderer)    │
│    └─ arcHeight       4m (для throwables)            │
│                                                     │
│  [Impact VFX]          взрыв / искры / blood        │
│    ├─ vfxPrefab       GameObject / Sprite           │
│    ├─ scaleByDamage   bool                          │
│    ├─ colorByDamageType bool                        │
│    └─ duration        0.5s                          │
│                                                     │
│  [Будущее: 2D Animation]                            │
│    ├─ spriteSheet     Sprite[] / SpriteAnimationAsset│
│    ├─ fps             12                            │
│    └─ loop            bool                          │
└─────────────────────────────────────────────────────┘
```

### 2.3 Ключевая абстракция: `ISkillVfxProvider`

```csharp
/// <summary>
/// Абстракция, позволяющая коду скилов не знать о том,
/// 3D-партиклы используются или 2D-спрайтовая анимация.
/// Одна реализация — ParticleSystem/GameObject.
/// Будущая реализация — SpriteRenderer + покадровая анимация.
/// </summary>
public interface ISkillVfxProvider
{
    void PlayCastVfx(SkillNodeConfig config, Transform character, float scale);
    void PlayProjectileVfx(SkillNodeConfig config, Vector3 from, Vector3 to, float speed, System.Action onArrived);
    void PlayImpactVfx(SkillNodeConfig config, Vector3 position, Vector3 normal, DamageType damageType, bool isCrit);
}
```

**Реализации (Strategy pattern):**

| Реализация | Для чего | Статус |
|-----------|----------|--------|
| `ParticleSystemVfxProvider` | ParticleSystem-based VFX | 🔵 Проектируется (Phase 1) |
| `SpriteVfxProvider` | 2D покадровая анимация (SpriteRenderer) | 🔵 Проектируется (Phase 3) |
| `HybridVfxProvider` | Авто-выбор 3D/2D по настройкам проекта | 🟣 Future |

### 2.4 VFX-конфиг (новые поля SkillNodeConfig)

```csharp
// === VFX: Cast (muzzle flash / заряд) ===
[Header("VFX: Cast")]
[Tooltip("Префаб эффекта каста. Если null — эффект не проигрывается.")]
public GameObject castVfxPrefab;

[Tooltip("Точка спавна на персонаже.")]
public VfxAttachPoint castSpawnPoint = VfxAttachPoint.WeaponMain;

[Tooltip("Длительность эффекта каста (сек). После — авто-destroy или возврат в pool.")]
[Range(0.05f, 2f)] public float castVfxDuration = 0.3f;

[Tooltip("Задержка перед спавном cast VFX (сек). Позволяет синхронизировать с анимацией.")]
[Range(0f, 1f)] public float castVfxDelay = 0f;

// === VFX: Projectile ===
[Header("VFX: Projectile")]
[Tooltip("Префаб снаряда. Если null — используется встроенный примитив (MVP fallback).")]
public GameObject projectileVfxPrefab;

[Tooltip("Скорость полёта (м/с).")]
[Range(5f, 100f)] public float projectileSpeed = 30f;

[Tooltip("Высота дуги для throwables (метры). 0 = прямой полёт.")]
[Range(0f, 15f)] public float projectileArcHeight = 0f;

[Tooltip("Материал trail (LineRenderer). Если null — trail не рисуется.")]
public Material projectileTrailMaterial;

// === VFX: Impact ===
[Header("VFX: Impact")]
[Tooltip("Префаб эффекта при попадании. Если null — эффект не проигрывается.")]
public GameObject impactVfxPrefab;

[Tooltip("Масштабировать эффект пропорционально урону.")]
public bool impactScaleByDamage = false;

[Tooltip("Окрашивать эффект по типу урона (Physical=красный, Explosive=оранжевый, Mesium=фиолетовый).")]
public bool impactColorByDamageType = true;

[Tooltip("Длительность impact VFX.")]
[Range(0.1f, 3f)] public float impactVfxDuration = 0.5f;

// === Будущее: 2D Animation Support ===
[Header("VFX: 2D (Future — Phase 3)")]
[Tooltip("SpriteAnimationAsset для 2D-покадровой анимации. Если задан — используется вместо ParticleSystem.")]
public SpriteAnimationAsset twoDVfxAnimation;   // Phase 3 — новый SO-тип

[Tooltip("FPS для 2D-анимации.")]
[Range(4, 60)] public int twoDFps = 12;

public enum VfxAttachPoint : byte
{
    WeaponMain = 0,  // основное оружие
    WeaponOff  = 1,  // off-hand
    Chest      = 2,  // центр персонажа
    Head       = 3,  // голова
    Root       = 4,  // корень (feet)
}
```

### 2.5 Рантайм-точки инжекции (где вызывается VFX)

```
SkillInputService.TryActivate(slot)
  │
  ├─ [1 ★ CAST VFX]
  │    _vfxProvider.PlayCastVfx(config, owner.transform, scale)
  │    Спавн в spawnPoint, с delay если задан
  │
  ├─ SkillAnimationPlayer.Play(config)
  │
  ├─ [2 ★ PROJECTILE VFX] (если subtype = Throwables / Bows / Crossbows)
  │    _vfxProvider.PlayProjectileVfx(config, from, to, speed, onArrived)
  │    Заменяет ThrowArcVisual.Fire / ProjectileVisual.Fire
  │
  └─ server RPC ...
       └─ CombatClientState.HandleAttackLanded(dto)
            ├─ [3 ★ IMPACT VFX]
            │    _vfxProvider.PlayImpactVfx(config, targetPos, normal, dmgType, isCrit)
            │    Вызывается для КАЖДОЙ цели в AOE
            │
            ├─ OnDamageDealt → DamageNumberService
            └─ OnAttackLanded → UI / SFX
```

### 2.6 Где хранить `ISkillVfxProvider`?

**Вариант A: синглтон-сервис** (рекомендуется):

```csharp
// SkillVfxService.cs — client-only MonoBehaviour, создаётся в NetworkManagerController
public class SkillVfxService : MonoBehaviour
{
    public static SkillVfxService Instance { get; private set; }
    
    private ISkillVfxProvider _provider;
    private readonly VfxObjectPool _pool = new VfxObjectPool();
    
    public void PlayCastVfx(SkillNodeConfig config, Transform character) { ... }
    public void PlayImpactVfx(SkillNodeConfig config, Vector3 pos, Vector3 normal, DamageType dmgType, bool isCrit) { ... }
}
```

Аналогично `DamageNumberService` и `TargetHighlightService` — создаётся в `NetworkManagerController`, живёт в DontDestroyOnLoad.

---

## 3. Типы VFX по категориям скилов

### 3.1 Melee (ближний бой)

| Навык | Cast VFX | Projectile | Impact VFX |
|-------|----------|-----------|------------|
| Basic Sword | Muzzle flash у рукояти | Trail (LineRenderer) по траектории клинка | Искры при попадании |
| Heavy Swing | Более яркий flash + зарядка | Широкий trail | Ударная волна (конус) |
| Dagger Mastery | Лёгкий flash | Тонкий trail | Кровь (blood splat) |
| Spear Reach | Flash на наконечнике | Trail копья | Пронзание (пробитие) |

**Источник VFX:** `SkillAnimationPlayer.Update()` — позиция оружия из кости (bone transform) в момент impact (60% анимации). Для trail — каждый кадр от кости руки/оружия.

**Сложность:** привязка к костям анимированного персонажа.

### 3.2 Ranged (дальний бой)

| Навык | Cast VFX | Projectile | Impact VFX |
|-------|----------|-----------|------------|
| Basic Bow | Натяжение тетивы (flash) | Стрела с trail | Попадание стрелы (dust/puff) |
| Crossbow Mastery | Дымок от выстрела | Болт (быстрее стрелы) | Попадание болта |
| Mesium Rifle | Синяя вспышка + beam | Лазерный луч (мгновенный) | Электрический разряд |

**Источник VFX:** `SkillInputService.TryActivate` → `PlayProjectileVfx` → projectile летит к цели. Impact — `CombatClientState.HandleAttackLanded`.

### 3.3 Throwables (гранаты, метательное)

| Навык | Cast VFX | Projectile | Impact VFX |
|-------|----------|-----------|------------|
| Grenade | Бросок (рука делает движение) | Граната по дуге + trail | Explosion sphere |
| Mine | Установка (приседание) | Нет | Мина на земле (beacon) |

**Источник VFX:** `SkillInputService.TryActivate` → `PlayProjectileVfx` с `arcHeight > 0`. Impact — по прибытии projectile в targetPoint.

### 3.4 Defense (защитные)

| Навык | Cast VFX | Projectile | Impact VFX |
|-------|----------|-----------|------------|
| Basic Armor | Свечение ауры вокруг персонажа | Нет | Нет |
| Antigrav Shield | Силовое поле | Нет | Блокирование удара (shield flash) |
| Antigrav Aura | Пульсирующее поле | Нет | Нет |

**Источник VFX:** только `PlayCastVfx` — мгновенный эффект вокруг персонажа.

### 3.5 Placed (устанавливаемое)

| Навык | Cast VFX | Projectile | Impact VFX |
|-------|----------|-----------|------------|
| Mine | Приседание + установка | Нет | Мина на месте (мигающий beacon) |
| Turret | Спавн турели | Нет | Турель появляется (spawn VFX) |

---

## 4. Object Pooling

Для часто-спавнящихся VFX (muzzle flash, impacts, projectiles) используем пул:

```csharp
public class VfxObjectPool
{
    // Один пул на префаб (как DamageNumberService)
    private readonly Dictionary<int, Queue<GameObject>> _pools = new();
    private const int DefaultPrewarm = 5;
    private const int MaxPoolSize = 20;
    
    public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation);
    public void Return(GameObject instance);
}
```

Для 2D-спрайтов в будущем — аналогичный пул `SpriteVfxPool`.

---

## 5. 2D-анимация: как не сделать противоречий сейчас

### 5.1 Что такое 2D покадровая анимация в контексте VFX

Спрайт-лист (sprite sheet) проигрывается покадрово через `SpriteRenderer`. Примеры: muzzle flash (4-8 кадров), explosion (12-16 кадров), дымок (8 кадров).

### 5.2 Как текущий дизайн готов к 2D

| Аспект | Решение | Почему не противоречит 2D |
|--------|---------|--------------------------|
| **Поля в SkillNodeConfig** | `castVfxPrefab` (GameObject) + `twoDVfxAnimation` (будущее) | 2D-анимация идёт через ОТДЕЛЬНОЕ поле `twoDVfxAnimation`. Если оба заданы — приоритет у того, что выбрано в настройках проекта (3D vs 2D mode). |
| **Интерфейс ISkillVfxProvider** | Абстракция над конкретным рендером | `ParticleSystemVfxProvider` спавнит GameObject, `SpriteVfxProvider` спавнит SpriteRenderer с анимацией. Код скилов вызывает только интерфейс. |
| **Object Pool** | `VfxObjectPool` для 3D, `SpriteVfxPool` для 2D | Разные пулы, общий интерфейс `IVfxPool`. |
| **Привязка к костям** | `VfxAttachPoint` enum | Для 2D позиция кости проецируется из 3D-world → экранные координаты для SpriteRenderer на world-space canvas. |
| **Trail (следы)** | Material на LineRenderer или Texture2D | Для 2D trail рисуется последовательностью спрайтов или через TrailRenderer (Unity 6 поддерживает 2D trails). |
| **Скорость анимации** | `attackClipSpeed` для 3D, `twoDFps` для 2D | Разные поля — не конфликтуют. |

### 5.3 Конкретные правила «не делать противоречий»

1. **Не хардкодить ParticleSystem** в коде VFX-провайдера. Всегда обращаться через `ISkillVfxProvider`.
2. **Не полагаться на 3D-физику** для VFX (collision, gravity). Использовать ручную интерполяцию как в текущих `ProjectileVisual`/`ThrowArcVisual`.
3. **Не использовать World-space координаты в UI/2D-контексте** без проекции. `SpriteVfxProvider` сам отвечает за конвертацию 3D → screen-space.
4. **Поля `castVfxPrefab` и `projectileVfxPrefab` — `GameObject`**, а не конкретный тип. Это позволяет в будущем положить туда префаб с `SpriteRenderer` вместо `ParticleSystem`.
5. **Новый SO-тип `SpriteAnimationAsset`** будет хранить массив спрайтов + fps. Поле `twoDVfxAnimation` ссылается на него. Сейчас поле добавляем, но не реализуем.

---

## 6. Фазы реализации

### Phase 0: Data Model (SkillNodeConfig + VFX поля)

**Файлы:** `SkillNodeConfig.cs`, `SkillNodeConfigEditor.cs`

- Добавить `VfxAttachPoint` enum
- Добавить `castVfxPrefab`, `castSpawnPoint`, `castVfxDuration`, `castVfxDelay`
- Добавить `projectileVfxPrefab`, `projectileSpeed`, `projectileArcHeight`, `projectileTrailMaterial`
- Добавить `impactVfxPrefab`, `impactScaleByDamage`, `impactColorByDamageType`, `impactVfxDuration`
- Добавить `twoDVfxAnimation` (тип — пока GameObject-заглушка или `SpriteAnimationAsset`)
- Обновить `SkillNodeConfigEditor` — показать VFX-секции только для `isActive` навыков, скрыть projectile для Melee, показать arc для Throwables

**Объём:** ~2-3 часа (data model + editor)

### Phase 1: VfxService + ISkillVfxProvider (ParticleSystem)

**Новые файлы:**
- `Assets/_Project/Scripts/Skills/Vfx/ISkillVfxProvider.cs`
- `Assets/_Project/Scripts/Skills/Vfx/ParticleSystemVfxProvider.cs`
- `Assets/_Project/Scripts/Skills/Vfx/SkillVfxService.cs`
- `Assets/_Project/Scripts/Skills/Vfx/VfxObjectPool.cs`

**Изменяемые файлы:**
- `SkillInputService.cs` — добавить вызов `SkillVfxService.PlayCastVfx` + заменить `ThrowArcVisual.Fire`/`ProjectileVisual.Fire` на `SkillVfxService.PlayProjectileVfx`
- `CombatClientState.cs` — добавить вызов `SkillVfxService.PlayImpactVfx` в `HandleAttackLanded`
- `NetworkManagerController.cs` — добавить `CreateSkillVfxService()`

**Объём:** ~4-5 часов

### Phase 2: Замена примитивов на префабы

- Создать префабы для базовых VFX: `PF_MuzzleFlash_Basic`, `PF_Impact_Melee`, `PF_Impact_Explosion`, `PF_Projectile_Arrow`, `PF_Grenade`
- Назначить их на существующие `.asset` навыки (через миграцию или вручную)
- Удалить примитивный `ThrowArcVisual` и `ProjectileVisual` (или оставить как fallback)

**Объём:** ~3-4 часа (требует VFX-художника или генерации через инструменты)

### Phase 3: 2D Animation Support

- Создать `SpriteAnimationAsset` SO-тип (Sprite[] + fps + loop)
- Создать `SpriteVfxProvider : ISkillVfxProvider`
- Добавить `SpriteVfxPool`
- Добавить переключение `ParticleSystemVfxProvider` ↔ `SpriteVfxProvider` через `SkillsConfig`

**Объём:** ~5-7 часов

### Phase 4: Полишинг

- Melee trail effect (LineRenderer от кости оружия)
- AOE zone indicator (aim preview при зажатии кнопки)
- Charge-up VFX для тяжёлых навыков
- Оптимизация пулов (prewarm при загрузке сцены)

---

## 7. Технические заметки

### 7.1 Кости и Attach Points

Для привязки VFX к оружию/руке используем именованные кости:

```csharp
public static class VfxBoneResolver
{
    public static Transform Resolve(Animator animator, VfxAttachPoint point)
    {
        return point switch
        {
            VfxAttachPoint.WeaponMain => FindBone(animator, "hand_r") ?? animator.transform,
            VfxAttachPoint.WeaponOff  => FindBone(animator, "hand_l") ?? animator.transform,
            VfxAttachPoint.Chest      => animator.GetBoneTransform(HumanBodyBones.Chest),
            VfxAttachPoint.Head       => animator.GetBoneTransform(HumanBodyBones.Head),
            _                         => animator.transform
        };
    }
}
```

### 7.2 Синхронизация с Animation Events

`SkillAnimationPlayer` уже имеет event-хуки `OnAttackImpact()` на 60% клипа и `OnSkillAnimationEnd()`. VFX могут подписаться на те же тайминги:

- **Cast VFX:** сразу при `TryActivate` (с учётом `castVfxDelay`)
- **Melee trail:** в `SkillAnimationPlayer.Update()` — каждый кадр от кости оружия к предыдущей позиции
- **Impact VFX:** через `OnAttackImpact` (если есть AnimationEvent) или через `CombatClientState.HandleAttackLanded` (серверный confirm)

### 7.3 Цвет по типу урона

```csharp
public static class DamageTypeColors
{
    public static Color Get(DamageType type) => type switch
    {
        DamageType.Physical  => new Color(0.8f, 0.2f, 0.1f),  // красный
        DamageType.Ballistic => new Color(1.0f, 0.8f, 0.1f),  // жёлтый
        DamageType.Explosive => new Color(1.0f, 0.4f, 0.05f), // оранжевый
        DamageType.Antigrav  => new Color(0.3f, 0.7f, 1.0f),  // голубой
        DamageType.Mesium    => new Color(0.6f, 0.1f, 0.9f),  // фиолетовый
        _                    => Color.white
    };
}
```

### 7.4 Отладка (Dev-режим)

Сохранить существующий `SkillAoeDebugVisualizer` для отладки AOE-зон. Добавить VFX-логи в консоль при `Debug.isDebugBuild`:

```csharp
Debug.Log($"[SkillVfxService] PlayCastVfx: skill='{config.skillId}' prefab='{config.castVfxPrefab?.name ?? "null"}' at {spawnPoint}");
```

---

## 8. Открытые вопросы

| # | Вопрос | Статус |
|---|--------|--------|
| **Q1** | Где брать VFX-ассеты? Генерировать (через AI) или ждать художника? | 🔴 Требует решения: без ассетов Phase 2 невозможен |
| **Q2** | Нужен ли preview VFX в инспекторе SkillNodeConfig? | 🟡 Nice-to-have (как preview анимаций) |
| **Q3** | Как 2D-анимации взаимодействуют с 3D-камерой? (billboarding?) | 🟡 Phase 3 решит — SpriteVfxProvider включает Billboard |
| **Q4** | VFX для NPC — те же префабы или отдельные? | 🟡 Навыки общие → VFX те же. NPC могут иметь уменьшенный scale |
| **Q5** | Звуки (SFX) — вместе с VFX или отдельно? | 🟣 Отдельная подсистема (AudioService), но триггерится в тех же точках инжекции |

---

## 9. Решение по 2D-готовности (ответ на замечание пользователя)

> «в будущем я хочу для vfx использовать 2д анимации (покадровые) в том числе — мы должны быть готовыми к ним, как минимум не делать так — чтобы работа сейчас была противоречием для будущих внедрений 2д»

**Что мы делаем сейчас:**

1. **Интерфейс `ISkillVfxProvider`** — абстрагирует рендер. Код скилов не знает про ParticleSystem или SpriteRenderer.
2. **Поле `twoDVfxAnimation`** добавляется в `SkillNodeConfig` уже сейчас, но не используется до Phase 3.
3. **Префабы `castVfxPrefab` / `impactVfxPrefab`** — тип `GameObject`, а не `ParticleSystem`. В будущем в этот же слот можно положить префаб со SpriteRenderer.
4. **Object pool** — общий интерфейс, разные реализации для 3D и 2D.
5. **Никакого хардкода ParticleSystem** в коде провайдера.

**Что НЕ делаем (потому что это противоречило бы 2D):**

- ❌ Не создаём типы `ParticleSystem castVfx` — только `GameObject`
- ❌ Не привязываемся к `ParticleSystem.Stop()` / `ParticleSystem.main` API в сервисном коде
- ❌ Не используем 3D-коллайдеры для детекта impact

---

## 10. История изменений

| Дата | Автор | Изменения |
|------|-------|-----------|
| 2026-07-31 | Aura | Первая версия: анализ текущего состояния, целевая архитектура, фазы реализации |
