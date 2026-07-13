# Аудит архитектуры: HP / Damage / Death / Respawn

**Дата:** 2026-07-13  
**Аудитор:** Mavis  
**Версия кода:** commit `1ae3690` (T-HP01: fix death respawn)  
**Охваченные коммиты:** `89273db` (T-RESP01) → `d73322f` (T-HP01) → `1ae3690` (T-HP01 fix)

**Статус исправлений (2026-07-13):**
- ✅ **R1** — IsServer унифицирован (NpcTarget.ApplyDamage, PlayerRespawnTracker.Update)
- ✅ **R2** — ResetFallTimer() добавлен в RespawnWithHpRestore
- ✅ **R3** — fallback HP=100 → retry-цикл продолжается, HP пересчитывается при загрузке StatsServer
- ✅ **R4** — CharacterWindow читает HP из PlayerTarget.GetCurrentHp() напрямую (первичный источник)
- ⬜ R5-R8 — среднесрочные/стратегические (не в скоупе текущего спринта)

---

## 1. Структура системы (as-built)

### 1.1 Схема flow (combat death)

```
PlayerAttacker
  → CombatServer.ResolveAttack()        // server-only
    → DamageCalculator.Calculate()      // pure math
      → PlayerTarget.ApplyDamage()      // server-only (NM.IsServer guard)
        ├─ NetworkVariable<int> _currentHp -= damage
        ├─ StatsServer.RecomputeAndSendSnapshot()
        ├─ NetworkPlayer.SetInputEnabled(false)
        ├─ Animator.SetTrigger("Death")
        └─ _deathRespawnTimer = Time.time + delay
          → Update() detects timer
            → TriggerDeathRespawn()
              → PlayerRespawnTracker.RespawnWithHpRestore()
                ├─ PerformRespawn()      // teleport via ClientRpc
                ├─ Animator reset + Play("Idle")
                ├─ PlayerTarget.SetHp(restoreHp)
                └─ NetworkPlayer.SetInputEnabled(true)
```

### 1.2 Схема flow (fall death)

```
PlayerRespawnTracker.Update()
  → IsServer guard (NB.IsServer — не NM.IsServer!)
  → transform.position.y <= _deathY
    → PerformRespawn()
      → TeleportToClientRpc(targetPos)
```

### 1.3 Компоненты и их ответственность

| Компонент | Файл | Роль |
|---|---|---|
| **HealthConfig** | `Stats/HealthConfig.cs` | ScriptableObject: baseHp, strToHpMultiplier, respawnHpPercent |
| **StatsServer** | `Stats/StatsServer.cs` | Серверный: ComputeMaxHp(), RecomputeAndSendSnapshot() |
| **PlayerTarget** | `Combat/Implementations/PlayerTarget.cs` | NetworkBehaviour: HP NetworkVariable, ApplyDamage, death timer, TriggerDeathRespawn |
| **PlayerRespawnTracker** | `Player/PlayerRespawnTracker.cs` | NetworkBehaviour: fall detection, teleport, RespawnWithHpRestore |
| **NetworkPlayer** | `Player/NetworkPlayer.cs` | SetInputEnabled(bool), ResetVelocity(), RegisterWithCombatServer |
| **CombatServer** | `Combat/Network/CombatServer.cs` | Hub: ResolveAttack, регистрация attacker/target |
| **StatsSnapshotDto** | `Stats/Dto/StatsSnapshotDto.cs` | INetworkSerializable: currentHp, maxHp в snapshot |
| **CharacterWindow** | `UI/Client/CharacterWindow.cs` | UI: HP bar из StatsSnapshotDto |

---

## 2. Ключевые архитектурные дефекты

### 🔴 CRIT-1: IsServer — три разных паттерна в одном flow

В трёх компонентах используются три разных способа проверки «мы на сервере»:

| Место | Паттерн | Надёжность |
|---|---|---|
| `PlayerTarget.ApplyDamage` | `NM.Singleton.IsServer` | ✅ Стабильно |
| `PlayerTarget.TriggerDeathRespawn` | `NM.Singleton.IsServer` | ✅ Стабильно |
| `PlayerTarget.SetHp` | `NM.Singleton.IsServer` | ✅ Стабильно |
| `PlayerRespawnTracker.Update` (fall check) | `NB.IsServer` | ⚠️ **Тот же баг NGO 2.x** |
| `PlayerRespawnTracker.RespawnWithHpRestore` | `NM.Singleton.IsServer` | ✅ Стабильно |
| `PlayerRespawnTracker.PerformRespawn` | нет guard — вызывается из guarded методов | ✅ OK |
| `NetworkPlayer.SetInputEnabled` | нет guard — server-only по контракту | ⚠️ контрактный, не защищённый |

**Риск:** `PlayerRespawnTracker.Update()` использует `if (!IsServer) return;` — это именно тот баг, который был зафиксирован и исправлен в `PlayerTarget`. Если в будущем кто-то добавит корутину в `PlayerRespawnTracker`, fall-detection сломается так же, как ломался респавн.

### 🔴 CRIT-2: HP synchronized через два независимых канала

```
Канал A: NetworkVariable<int> _currentHp — реплицируется NGO автоматически всем клиентам
Канал B: StatsSnapshotDto.currentHp — шлётся через TargetRpc (reflection) конкретному владельцу
```

**Проблемы:**
- Два источника правды на клиенте. UI (CharacterWindow) читает из `StatsSnapshotDto`, а не из `NetworkVariable`. Если snapshot не пришёл (RPC потерян, timing race), HP bar показывает 0/null.
- `StatsServer.SendSnapshotToOwner` читает HP из `PlayerTarget.GetComponent()` — создаёт жёсткую связь Stats → Combat.
- `PlayerTarget.ComputeMaxHp` читает из `StatsServer.Instance` — обратная связь Combat → Stats.
- **Циклическая зависимость namespace'ов:** `ProjectC.Stats` → `ProjectC.Combat` и `ProjectC.Combat` → `ProjectC.Stats`. Формально C# это разрешает, но архитектурно означает, что две подсистемы не могут существовать друг без друга.

### 🟡 HIGH-3: Два независимых death-таймера

`PlayerTarget` и `PlayerRespawnTracker` оба имеют таймеры в `Update()`:

| Компонент | Таймер | Назначение |
|---|---|---|
| `PlayerTarget._deathRespawnTimer` | `Time.time + _deathRespawnDelay` (1.5s) | Combat death → respawn |
| `PlayerRespawnTracker._fallStartTime` | `Time.time + _respawnDelay` (0.5s) | Fall death → teleport |

**Проблема:** После combat-респавна (HP restore) таймер падения `_fallStartTime` сбрасывается только если `ResetFallTimer()` вызван явно. В текущем коде `RespawnWithHpRestore` → `PerformRespawn()` → телепорт, но `ResetFallTimer()` **не вызывается** после телепорта. Если точка респавна ниже Y=0 (например, на плавучей платформе), игрок мгновенно упадёт снова.

**Фикс существовал** — в `RespawnWithHpRestore` не хватает `tracker.ResetFallTimer()`.

### 🟡 HIGH-4: Fallback HP инициализации — race condition

```csharp
// PlayerTarget.ApplyDamage, строка 199-209
if (!_hpInitialized) {
    TryInitializeHp();           // пытается через StatsServer
    if (!_hpInitialized) {
        _maxHp.Value = 100;
        _currentHp.Value = 100;  // FALLBACK!
    }
}
```

**Риск:** Если игрок получает урон ДО того, как `StatsServer` проинициализировался (редкий timing race при старте хоста), HP устанавливается в 100 независимо от STR. После того как `StatsServer` загрузится, HP **никогда не пересчитывается**. Игрок застрянет с HP=100 до след. респавна.

Аналогичная проблема в `SendSnapshotToOwner` (StatsServer строка 505-509):
```csharp
if (maxHp <= 0) {
    maxHp = ComputeMaxHp(clientId);
    currentHp = maxHp; // fresh spawn = full HP
}
```
Здесь сервер считает что `maxHp <= 0` = fresh spawn, но это может быть timing race когда PlayerTarget ещё не установил NetworkVariable.

### 🟡 HIGH-5: Жёсткая связанность PlayerTarget ↔ PlayerRespawnTracker

`PlayerTarget.TriggerDeathRespawn()`:
1. Ищет `GetComponent<PlayerRespawnTracker>()`
2. Если не нашёл — падает в **fallback-логику** (ручное восстановление HP + включение ввода)

**Проблема:** Оба компонента должны быть на одном GameObject. Если `PlayerRespawnTracker` когда-нибудь будет вынесен на child-объект, респавн сломается тихо — fallback-логика сработает, но без телепортации. Игрок «оживёт» на месте смерти.

### 🟡 MEDIUM-6: NPC death vs Player death — асимметрия

| Аспект | NPC (NpcTarget) | Player (PlayerTarget) |
|---|---|---|
| Death action | `Destroy(gameObject, 3.0f)` | Timer → respawn |
| Loot | Spawns NpcLootPickup | N/A |
| Animation | Death trigger | Death trigger + reset + Idle |
| HP sync | NetworkVariable | NetworkVariable + StatsSnapshotDto |
| Server guard | `if (!IsServer) return` | `NM.Singleton.IsServer` |

Это нормально и ожидаемо (NPC ≠ player), но стоит зафиксировать что `NpcTarget` использует **старый `IsServer` guard** — если когда-нибудь `NpcTarget.ApplyDamage` будет вызываться из таймера/корутины, он сломается так же, как ломался PlayerTarget.

### 🟢 LOW-7: Update() на всех клиентах

```csharp
// PlayerTarget.Update()
private void Update() {
    if (_deathRespawnTimer < 0f) return;  // ← все клиенты проходят эту проверку
    ...
    TriggerDeathRespawn();
}
```

Хотя вызов `TriggerDeathRespawn` защищён `NM.IsServer`, проверка таймера выполняется на каждом клиенте каждый кадр. Микро-оптимизация, но для мобильных целей — заметный CPU waste. Можно было бы обернуть в `if (!IsServer) return;` выше.

### 🟢 LOW-8: Магические числа и хардкод

| Что | Где | Проблема |
|---|---|---|
| `_deathRespawnDelay = 1.5f` | PlayerTarget сериализовано | ОК |
| `_respawnDelay = 0.5f` | PlayerRespawnTracker сериализовано | ОК |
| HP fallback = 100 | PlayerTarget.ApplyDamage | hardcoded |
| respawnPercent = 0.3 по умолчанию | HealthConfig сериализовано | ОК |
| `maxHp <= 0 → fresh spawn` | StatsServer.SendSnapshotToOwner | предположение |

### 🟢 LOW-9: Нет метрик死亡

Отсутствует:
- Лог смерти игрока (кто убил, чем, где)
- K/D трекер
- Firebase/GameAnalytics ивент на death/respawn
На данном этапе это ожидаемо (Stage 2.5), но стоит отметить.

---

## 3. Скрытые баги (need verification)

1. **RespawnWithHpRestore → ResetFallTimer не вызван**  
   После телепорта на точку респавна, `_fallStartTime` не сбрасывается. Если точка респавна ниже `_deathY` (0), игрок упадёт и телепортируется снова через 0.5с.  
   **Fix:** добавить `ResetFallTimer()` в `RespawnWithHpRestore()` после `PerformRespawn()`.

2. **NpcTarget.IsServer guard** (строка 97) — использует `if (!IsServer) return;`, а не `NM.Singleton.IsServer`.  
   **Риск:** низкий, т.к. ApplyDamage вызывается синхронно из `CombatServer.ResolveAttack`, не из таймера. Но если в будущем появится death-delay timer для NPC — сломается.

3. **CharacterWindow читает HP из StatsSnapshotDto** — если `SendSnapshotToOwner` не вызван после `ApplyDamage` (например, `StatsServer.Instance` == null в момент ApplyDamage), UI не обновится до следующего snapshot'а.

---

## 4. Рекомендации

### 4.1 Немедленные (pre-production)

| # | Описание | Приоритет | Оценка |
|---|---|---|---|
| R1 | Унифицировать `IsServer` guard: **везде** `NM.Singleton.IsServer`, включая `PlayerRespawnTracker.Update()` и `NpcTarget.ApplyDamage()` | 🔴 High | 15 мин |
| R2 | Добавить `ResetFallTimer()` в `PlayerRespawnTracker.RespawnWithHpRestore()` | 🔴 High | 5 мин |
| R3 | После fallback-инициализации HP (100) — подписаться на `StatsServer.OnSnapshot` или пересчитать HP при первом успешном `StatsServer.ComputeMaxHp` | 🟡 Medium | 30 мин |

### 4.2 Среднесрочные

| # | Описание | Приоритет | Оценка |
|---|---|---|---|
| R4 | Устранить дуальную синхронизацию HP: либо `NetworkVariable` (основной), либо `StatsSnapshotDto` (UI-копия) — не оба. `CharacterWindow` читать из `NetworkVariable` напрямую через `PlayerTarget.GetCurrentHp()` | 🟡 Medium | 1-2 ч |
| R5 | Разорвать циклическую зависимость Stats ↔ Combat через событие/интерфейс: `StatsServer` публикует `HpChangedEvent`, `PlayerTarget` подписывается; `PlayerTarget` публикует `HpSnapshotEvent`, `StatsServer` читает | 🟡 Medium | 3-4 ч |
| R6 | Выделить единый `DeathRespawnController` на NetworkPlayer, который координирует: disable input → death anim → timer → teleport → HP restore → enable input → reset anim. `PlayerTarget` остаётся только HP NetworkVariable + ApplyDamage | 🟡 Medium | 4 ч |

### 4.3 Стратегические

| # | Описание | Приоритет |
|---|---|---|
| R7 | Добавить DeathEvent в WorldEventBus (причина, позиция, killerId, weaponId) для аналитики и квестов | 🟢 Low |
| R8 | Переписать `PlayerRespawnTracker.Update()` на `FixedUpdate` (fall detection — физика, не логика кадра) | 🟢 Low |

---

## 5. Итоговая оценка

**Система работает корректно** — в Play Mode респавн происходит, HP синхронизируется, UI обновляется.

**Архитектурные риски:**
- 2 критических дефекта (CRIT-1, CRIT-2) — могут проявиться при расширении системы
- 3 высоких (HIGH-3, HIGH-4, HIGH-5) — могут проявиться в edge cases
- Все дефекты — следствие эволюционного наслоения фиксов без рефакторинга

**Главная рекомендация:** Прежде чем добавлять новые фичи (анимации смерти, воскрешение другими игроками, респавн-меню), сделать рефакторинг R4 + R6 — вынести координацию death/respawn из PlayerTarget в отдельный контроллер.

---

## 6. Приложение: полный список затронутых файлов

| Файл | Строк |
|---|---|
| `Assets/_Project/Scripts/Combat/Implementations/PlayerTarget.cs` | 304 |
| `Assets/_Project/Scripts/Combat/Implementations/NpcTarget.cs` | 253 |
| `Assets/_Project/Scripts/Player/PlayerRespawnTracker.cs` | 271 |
| `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | 2133 |
| `Assets/_Project/Scripts/Stats/StatsServer.cs` | 724 |
| `Assets/_Project/Scripts/Stats/HealthConfig.cs` | 48 |
| `Assets/_Project/Scripts/Stats/Dto/StatsSnapshotDto.cs` | 126 |
| `Assets/_Project/Scripts/Combat/Network/CombatServer.cs` | 724 |
| `Assets/_Project/Scripts/Combat/Implementations/PlayerAttacker.cs` | 193 |
| `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` | 3476 |
