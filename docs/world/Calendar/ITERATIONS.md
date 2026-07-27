# Calendar / TimeManager — Итерации

## Итерация 1 от 2026-07-27

**Задача:** Реализовать полноценный TimeManager: календарь, персистенция, UI, квестовые триггеры  
**Коммит:** `30fc5dc` — T-TIME01: TimeManager — игровой календарь, персистенция, UI, квестовые триггеры

**Изменения:**
- `Assets/_Project/Scripts/Core/GameTimeData.cs` — **создан** (структура календаря)
- `Assets/_Project/Scripts/Core/ServerWeatherController.cs` — **изменён** (календарь, бродкаст, персистенция)
- `Assets/_Project/Core/WorldEvent.cs` — **изменён** (4 новых WorldEvent типа)
- `Assets/_Project/Scripts/Core/TimePersistence/ITimeRepository.cs` — **создан**
- `Assets/_Project/Scripts/Core/TimePersistence/JsonTimeRepository.cs` — **создан**
- `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` — **изменён** (time-info-label)
- `Assets/_Project/Quests/Triggers/ConcreteTriggers.cs` — **изменён** (4 календарных триггера)
- `Assets/_Project/Quests/Network/QuestServer.cs` — **изменён** (подписки)
- `docs/world/Calendar/PLAN_TimeManager.md` — **создан** (план)
