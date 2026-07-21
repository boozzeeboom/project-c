# Итерации разработки

## Итерация от 2026-07-21 (T-PLAYER-PERSIST)

**Задача:** Player-ship position persistence — привязка респавна и сохранения позиции игрока к кораблю. Игрок, вышедший в полёте, при возвращении появляется у своего корабля (зависшего в воздухе). При падении с корабля — респавн на нём же. Корабль без пилота замирает, не тратит топливо, не сдувается ветром.

**Коммит:** `269d4ad` — T-PLAYER-PERSIST: Player-ship position persistence — freeze, save, restore, ship-proximity respawn

**Изменения:**
- `ShipController.cs` — _frozenByNoPilot, freeze в RemovePilotRpc, unfreeze в AddPilotRpc, velocity-zeroing каждый FixedUpdate, guards на idle fuel/wind/antiGravity/ApplyExternalForce
- `PlayerPositionServer.cs` (новый) — singleton, CollectPlayers, GetPendingPlayers, LoadSavedPlayers, RestorePlayer
- `ShipPositionSaveData.cs` — PlayerPositionSaveData + ShipPositionListWrapper.players
- `ShipPositionRepository.cs` — LoadAllWrapper / SaveAll(ShipPositionListWrapper)
- `ShipPositionServer.cs` — единый write ships+players, загрузка players в RestoreCoroutine
- `NetworkPlayer.cs` — RestorePlayerPositionCoroutine (5s delay)
- `PlayerRespawnTracker.cs` — ship-proximity respawn (IsInShip→GetExitPosition, TryFindNearestOwnedShip через MetaRequirementRegistry)
- `BootstrapScene.unity` — [PlayerPositionServer] GameObject

## Итерация от 2026-07-21 (T-HP01)

**Задача:** Система здоровья персонажа — HP зависит от STR с настраиваемым множителем, отображение в CharacterWindow, респавн при смерти с восстановлением 30% HP.

**Коммит:** `d73322f` — T-HP01: Система здоровья персонажа (HP = base + STR × multiplier)

**Изменения:**
- `Assets/_Project/Scripts/Stats/HealthConfig.cs` — новый ScriptableObject (baseHp=100, strToHpMultiplier=10, respawnHpPercent=0.3)
- `Assets/_Project/Resources/Stats/HealthConfig_Default.asset` — дефолтный конфиг
- `Assets/_Project/Scripts/Stats/StatsServer.cs` — _healthConfig, ComputeMaxHp(), HP в снапшоте
- `Assets/_Project/Scripts/Stats/Dto/StatsSnapshotDto.cs` — currentHp, maxHp поля
- `Assets/_Project/Scripts/Combat/Implementations/PlayerTarget.cs` — динамический HP, death→respawn
- `Assets/_Project/Scripts/Player/PlayerRespawnTracker.cs` — RespawnWithHpRestore()
- `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` — HP bar в UI
- `Assets/_Project/UI/Resources/UI/CharacterWindow.uxml` — HP элементы
- `Assets/_Project/UI/Resources/UI/CharacterWindow.uss` — HP стили
- `Assets/_Project/Scripts/UI/EscMenu/EscMenuWindow.cs` — фикс SceneManager
- `Assets/BootstrapScene.unity` — StatsServer._healthConfig назначен

## Итерация от 2026-07-16

**Задача:** Fix death respawn — NetworkBehaviour.IsServer врал в coroutine/timer (баг NGO 2.x), респавн не срабатывал.

**Коммит:** `efd35c6` — T-HP01: fix death respawn — NetworkManager.Singleton.IsServer вместо NB.IsServer

**Изменения:**
- `Assets/_Project/Scripts/Combat/Implementations/PlayerTarget.cs` — timer вместо корутины, NetworkManager.Singleton.IsServer в TriggerDeathRespawn/SetHp
- `Assets/_Project/Scripts/Player/PlayerRespawnTracker.cs` — NetworkManager.Singleton.IsServer в RespawnWithHpRestore
- `Assets/_Project/Scripts/Combat/Implementations/PlayerAttacker.cs` — попутные
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs` — попутные
- `Assets/_Project/Scripts/Skills/SkillInputService.cs` — попутные
- `docs/Character/respawn/T-HP01-respawn-fix.md` — документация всех 4 итераций debugging
- `docs/Character/respawn/ITERATIONS.md` — перенесён из Assets/_Project/Docs

---

## Итерация от 2026-07-13

**Задача:** Аудит архитектуры систем HP / Damage / Death / Respawn.

**Коммит:** `1ae3690` (рабочих изменений нет, только документация)

**Изменения:**
- `docs/Character/respawn/03_ARCHITECTURE_AUDIT.md` — полный аудит

**Ключевые находки:**
1. 🔴 Три разных `IsServer` паттерна — `PlayerRespawnTracker.Update()` всё ещё на `NB.IsServer`
2. 🔴 Два канала синхронизации HP (NetworkVariable + StatsSnapshotDto)
3. 🟡 `RespawnWithHpRestore` не вызывает `ResetFallTimer()` — риск double-respawn
4. 🟡 Fallback HP=100 при race condition не пересчитывается
5. 🟡 Циклическая зависимость Stats ↔ Combat через GetComponent

**Рекомендация:** рефакторинг R4+R6 из аудита перед следующим этапом.

---

## История изменений

| Дата | Сессия | Изменения |
|------|--------|-----------|
| 2026-07-21 | T-HP01 | Первая имплементация: HealthConfig, HP=base+STR×multiplier, death→respawn |
| 2026-07-16 | T-HP01 fix | 4 итерации debug: IsServer→NM.IsServer, timer вместо coroutine |
| 2026-07-13 | Аудит | 03_ARCHITECTURE_AUDIT.md — 9 дефектов, анализ as-built |
| 2026-07-13 | T-AUDIT01 | Исправление 4 архитектурных расхождений (R1-R4) |

---

## Итерация от 2026-07-13 (T-AUDIT01 fix)

**Задача:** Применить 4 немедленные правки из аудита 03_ARCHITECTURE_AUDIT.md

**Коммит:** `f12e8dc` — T-AUDIT01: исправление 4 архитектурных расхождений HP/Death/Respawn

**Изменения:**
- `Assets/_Project/Scripts/Combat/Implementations/NpcTarget.cs` — R1: IsServer → NM.Singleton.IsServer
- `Assets/_Project/Scripts/Player/PlayerRespawnTracker.cs` — R1: IsServer в Update(), R2: ResetFallTimer() в RespawnWithHpRestore
- `Assets/_Project/Scripts/Combat/Implementations/PlayerTarget.cs` — R3: _hpFallbackUsed + retry-loop продолжается после fallback=100
- `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` — R4: RefreshHpFromNetworkVariable() читает HP из PlayerTarget напрямую
- `docs/Character/respawn/03_ARCHITECTURE_AUDIT.md` — статус исправлений
