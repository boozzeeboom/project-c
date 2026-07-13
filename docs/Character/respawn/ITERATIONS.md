# Итерации разработки

## Итерация от 2026-07-21

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
