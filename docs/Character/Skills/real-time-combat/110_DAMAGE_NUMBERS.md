# Всплывающие цифры урона (Damage Numbers)

> **Дата:** 2026-07-28
> **Статус:** ✅ Реализовано
> **Контекст:** любая атака (primary/secondary, навыки, AOE) → цифры над головой цели
> **Технологии:** TextMeshPro (World Space), client-side singleton + object pool, URP

---

## 0. Результаты анализа текущего кода

### 0.1. Как сейчас течёт урон

```
Клиент                              Сервер                             Все клиенты
──────                              ──────                             ───────────
SkillInputService.TryActivate()
  → server.RequestAttackRpc()
  → server.RequestSkillCastRpc()    CombatServer.ResolveAttack()
  → server.RequestSkillCastAtPointRpc  → DamageCalculator.Calculate()
                                       → target.ApplyDamage()
                                       → AttackLandedTargetRpc(dto, Everyone)
                                                                       CombatClientState.HandleAttackLanded(dto)
                                                                         → OnAttackLanded?.Invoke(result)
                                                                         → OnDamageDealt?.Invoke(result)   ← НАШ ХУК
                                                                         → ProjectileVisual.Fire() (баллистика)
```

**Ключевой вывод:** `CombatClientState.OnDamageDealt` стреляет на **всех клиентах** с полным `DamageResult` (включая `finalDamage`, `isCrit`, `damageType`, `targetId`, `targetPosition`). Это идеальная точка для спавна цифр урона — клиент-сайд, все данные есть, работает и для атак, и для навыков.

### 0.2. AOE и мульти-цели

`CombatServer.ResolveSkillCast` для AOE делает цикл:

```csharp
for (int i = 0; i < results.Count; i++)  // каждая цель отдельно
{
    var result = DamageCalculator.Calculate(attacker, target, source, ...);
    target.ApplyDamage(result, attackerId);
    AttackLandedTargetRpc(dto, Everyone);  // ← отдельный RPC на каждую цель!
    ...
}
```

→ `OnDamageDealt` дёргается **по одному разу на каждую цель**. Мульти-цели и AOE обрабатываются естественно, без доп. логики.

### 0.3. Что уже есть в проекте

| Ресурс | Файл | Роль |
|--------|------|------|
| `CombatClientState` | `Combat/Client/CombatClientState.cs` | Клиентский event-bus: `OnDamageDealt` |
| `CombatConfig` | `Combat/Config/CombatConfig.cs` | Поля `showDamageNumbers`, `damageNumberDuration`, `showHitFlash` — дизайнерский конфиг готов |
| `DamageResult` | `Combat/Core/DamageResult.cs` | Полные данные урона (finalDamage, isCrit, damageType, targetPosition...) |
| `DamageType` | `Combat/Core/DamageType.cs` | 5 типов: Physical, Ballistic, Antigrav, Explosive, Mesium |
| `NetworkManagerController` | `Core/NetworkManagerController.cs` | Паттерн создания root-GO синглтонов (CreateCombatClientState, CreateTargetHighlightService...) |
| `TargetHighlightService` | `Combat/Client/TargetHighlightService.cs` | Эталонный паттерн client-side singleton для новой фичи |
| TextMeshPro | встроен в com.unity.ugui 2.5.0+ | Доступен, не требует установки |

### 0.4. Чего НЕТ (и нужно создать)

- ❌ `DamageNumberService` — клиентский singleton для спавна/управления цифрами
- ❌ `DamageNumberInstance` — компонент на world-space TMP GameObject (анимация + возврат в пул)
- ❌ `DamageNumberConfig` — SO с настройками (цвета по типам урона, размеры, крит-оверрайды)
- ❌ Prefab `PF_DamageNumber` — world-space Canvas + TextMeshProUGUI
- ❌ Object pool — нет в проекте, нужен простой пул для GameObject'ов

---

## 1. Архитектурный план

### 1.1. Обзор компонентов

```
┌──────────────────────────────────────────────────────────────┐
│                    CLIENT-SIDE ONLY                          │
│                                                              │
│  CombatClientState.OnDamageDealt                             │
│         │                                                    │
│         ▼                                                    │
│  DamageNumberService (MonoBehaviour singleton, root GO)      │
│         │                                                    │
│         │  читает CombatConfig:                              │
│         │    showDamageNumbers (вкл/выкл)                     │
│         │    damageNumberDuration (float)                     │
│         │  читает DamageNumberConfig:                        │
│         │    цвета по DamageType                             │
│         │    размеры шрифта (normal / crit)                  │
│         │    offset над головой                              │
│         │                                                    │
│         ▼                                                    │
│  DamageNumberInstance (MonoBehaviour, world-space TMP)        │
│    ├─ Spawn(worldPos, damage, isCrit, damageType)            │
│    ├─ Анимация: float up + fade out                          │
│    └─ По завершении → возврат в ObjectPool                   │
│                                                              │
│  ObjectPool<DamageNumberInstance> (встроен в Service)         │
│    └─ Префаб PF_DamageNumber (World Space Canvas + TMP)      │
└──────────────────────────────────────────────────────────────┘
```

### 1.2. Конфигурация (не хардкод)

`DamageNumberConfig` ScriptableObject (создаётся через `[CreateAssetMenu]`):

| Поле | Тип | Назначение |
|------|-----|------------|
| `physicalColor` | Color | Цвет для Physical урона |
| `ballisticColor` | Color | Цвет для Ballistic |
| `antigravColor` | Color | Цвет для Antigrav |
| `explosiveColor` | Color | Цвет для Explosive |
| `mesiumColor` | Color | Цвет для Mesium |
| `normalFontSize` | float | Размер шрифта (обычный урон) |
| `critFontSize` | float | Размер шрифта (крит) |
| `critColor` | Color | Цвет текста при крите |
| `floatSpeed` | float | Скорость всплытия (м/с) |
| `fadeCurve` | AnimationCurve | Кривая прозрачности (0..1 → alpha) |
| `worldOffsetY` | float | Смещение над целью (метры) |
| `randomSpreadX` | float | Случайный разброс по X (±метры) |

Файл: `Assets/_Project/Resources/Combat/DamageNumberConfig_Default.asset`

### 1.3. Object Pool

Простой пул внутри `DamageNumberService`:
- Размер: prewarm 10, expandable
- Префаб: `PF_DamageNumber` (World Space Canvas)
- Возврат в пул через `OnAnimationComplete` (анимация через DOTween или кастомный таймер)

### 1.4. Интеграция с CombatConfig

`CombatConfig` (уже существует) содержит:
- `showDamageNumbers` (bool) — `DamageNumberService` проверяет перед спавном
- `damageNumberDuration` (float) — длительность показа (передаётся в `DamageNumberInstance`)

Также:
- `showHitFlash` (bool) — задел на будущее, в этом плане НЕ реализуем

---

## 2. План реализации (4 шага)

### Шаг 1: DamageNumberConfig + Prefab

Создать:
1. `Assets/_Project/Scripts/Combat/Config/DamageNumberConfig.cs` — ScriptableObject
2. `Assets/_Project/Resources/Combat/DamageNumberConfig_Default.asset` — дефолтный конфиг
3. `Assets/_Project/Resources/Prefabs/PF_DamageNumber.prefab` — World Space Canvas с TMP

Метод анимации: **кастомный (без DOTween)** — простая корутина: `while (timer < duration) { move up; fade; yield return null; }`. Не добавляем зависимостей.

### Шаг 2: DamageNumberInstance

Создать:
1. `Assets/_Project/Scripts/Combat/Client/DamageNumberInstance.cs` — MonoBehaviour:
   - `Spawn(config, worldPos, damage, isCrit, damageType)` — инициализация и старт анимации
   - Корутина анимации: float up + fade (через TMP alpha)
   - Событие `OnComplete` — для возврата в пул

### Шаг 3: DamageNumberService + ObjectPool

Создать:
1. `Assets/_Project/Scripts/Combat/Client/DamageNumberService.cs` — MonoBehaviour singleton:
   - Подписка на `CombatClientState.OnDamageDealt`
   - Object pool (prewarm 10, expandable)
   - Чтение `CombatConfig.Instance.showDamageNumbers`
   - Поиск цели по `targetId` для получения актуальной позиции
   - Спавн `DamageNumberInstance` на позиции цели

### Шаг 4: Интеграция в NetworkManagerController

Модифицировать:
1. `Assets/_Project/Scripts/Core/NetworkManagerController.cs`:
   - Добавить `CreateDamageNumberService()` (паттерн как у `CreateTargetHighlightService`)
   - Вызвать в `Awake()` после `CreateTargetLockService()`

---

## 3. Детали реализации

### 3.1. DamageNumberService

```csharp
namespace ProjectC.Combat.Client
{
    public class DamageNumberService : MonoBehaviour
    {
        public static DamageNumberService Instance { get; private set; }
        
        [SerializeField] private DamageNumberConfig _config;
        [SerializeField] private GameObject _prefab;        // PF_DamageNumber
        [SerializeField] private int _poolPrewarm = 10;
        
        private readonly Queue<DamageNumberInstance> _pool = new();
        private CombatClientState _combatState;
        
        void Awake() { /* singleton + DontDestroyOnLoad */ }
        void Start() { /* prewarm pool + subscribe */ }
        void OnDestroy() { /* unsubscribe + clear pool */ }
        
        void OnDamageDealt(DamageResult result)
        {
            if (CombatConfig.Instance?.showDamageNumbers == false) return;
            if (!result.isHit || result.finalDamage <= 0) return;
            
            var worldPos = GetTargetPosition(result.targetId) 
                           ?? result.targetPosition;
            worldPos.y += _config.worldOffsetY;
            worldPos.x += Random.Range(-_config.randomSpreadX, _config.randomSpreadX);
            
            var instance = GetOrCreate();
            instance.Spawn(_config, worldPos, result.finalDamage, 
                          result.isCrit, result.damageType, 
                          CombatConfig.Instance?.damageNumberDuration ?? 1.5f,
                          () => ReturnToPool(instance));
        }
    }
}
```

### 3.2. Анимация (без DOTween)

```csharp
IEnumerator AnimateRoutine(DamageNumberConfig cfg, float duration, Action onComplete)
{
    float elapsed = 0f;
    Vector3 startPos = transform.position;
    
    while (elapsed < duration)
    {
        elapsed += Time.deltaTime;
        float t = elapsed / duration;
        
        // Подъём вверх
        transform.position = startPos + Vector3.up * cfg.floatSpeed * elapsed;
        // Прозрачность
        Color c = _tmpText.color;
        c.a = cfg.fadeCurve.Evaluate(t);
        _tmpText.color = c;
        
        yield return null;
    }
    
    onComplete?.Invoke();
}
```

### 3.3. Object Pool (простой)

```csharp
DamageNumberInstance GetOrCreate()
{
    if (_pool.Count > 0) return _pool.Dequeue();
    var go = Instantiate(_prefab, transform);
    return go.GetComponent<DamageNumberInstance>();
}

void ReturnToPool(DamageNumberInstance instance)
{
    instance.gameObject.SetActive(false);
    _pool.Enqueue(instance);
}
```

---

## 4. Что НЕ делаем

- ❌ DOTween / сторонние анимационные библиотеки — кастомная корутина
- ❌ Hit flash (красная вспышка на модели) — это отдельная задача (`CombatConfig.showHitFlash`)
- ❌ Miss-текст («Промах!») — можно добавить позже, `DamageResult.isHit == false`
- ❌ Скриншот-эффекты (Screen Space Overlay) — только World Space
- ❌ Серверная логика — чисто клиент-сайд

---

## 5. Commit History

| Коммит | Описание |
|--------|----------|
| `e81221a` | Базовая реализация: DamageNumberConfig, DamageNumberInstance, DamageNumberService, префаб, NetworkManagerController |
| `1b5ca18` | Фикс deprecation: FindObjectsByType(FindObjectsSortMode) → FindObjectsInactive |
| `1bdbba5` | Billboard (ProjectC.UI.Billboard), унифицированный размер (distance scaling), пересоздан префаб с SDF-шрифтом |

---

## 6. Как настраивать

1. **Цвета, шрифт, анимация:** `Assets/_Project/Resources/Combat/DamageNumberConfig_Default.asset` → Inspector
   - `physicalColor` / `ballisticColor` / ... — цвет цифр по типу урона
   - `normalFontSize` / `critFontSizeMultiplier` — размер шрифта
   - `floatSpeed` — скорость всплытия (м/с)
   - `fadeCurve` — кривая прозрачности (X=0..1 время, Y=alpha)
   - `worldOffsetY` — высота над головой цели
   - `randomSpreadX` — случайный горизонтальный разброс

2. **Глобальное вкл/выкл:** `CombatConfig.showDamageNumbers` (в `Assets/_Project/Resources/Combat/CombatConfig_Default.asset`)

3. **Длительность показа:** `CombatConfig.damageNumberDuration` (там же)

4. **Эталонное расстояние (uniform size):** `_referenceDistance` на префабе `PF_DamageNumber`

## 5. Файлы (план)

| Файл | Действие | Назначение |
|------|----------|------------|
| `Assets/_Project/Scripts/Combat/Config/DamageNumberConfig.cs` | NEW | SO-конфиг |
| `Assets/_Project/Resources/Combat/DamageNumberConfig_Default.asset` | NEW | Дефолтный конфиг |
| `Assets/_Project/Resources/Prefabs/PF_DamageNumber.prefab` | NEW | Префаб world-space TMP |
| `Assets/_Project/Scripts/Combat/Client/DamageNumberInstance.cs` | NEW | Компонент анимации |
| `Assets/_Project/Scripts/Combat/Client/DamageNumberService.cs` | NEW | Синглтон + пул |
| `Assets/_Project/Scripts/Core/NetworkManagerController.cs` | MOD | Добавить CreateDamageNumberService |
