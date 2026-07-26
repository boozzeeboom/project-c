# Журнал итераций

## Итерация от 2026-07

**Задача:** Исправить микротряску персонажа при standing  
**Коммит:** `192017d` — T-JITTER01: исправление микротряски персонажа при standing — фильтрация стационарных Rigidbody в moving-platform carry  
**Изменения:**
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs` — переписаны `DetectGroundPlatform()` и `ApplyPlatformCarry()`, добавлено поле `_platformMinDelta`
- `Assets/_Project/Docs/INVESTIGATION_CHARACTER_MICRO_JITTER.md` — документ расследования (создан)

**Стратегия отката:** `git revert 192017d`
