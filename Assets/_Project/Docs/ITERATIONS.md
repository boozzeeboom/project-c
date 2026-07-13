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
