# Итерации реализации — NPC Social Behavior

## Итерация от 2026-07-15

**Задача:** Глубокий анализ и проектирование расширения поведенческой модели NPC — реалистичные социальные паттерны для «человека социального»
**Коммит:** `6365a8f` — T-NPC-S00: анализ и проектирование социального поведения NPC

**Документы:**
- `02_SOCIAL_HUMAN_BEHAVIOR.md` — модульная архитектура (NpcBehaviorModule), 4 слоя поведения, приоритетная арбитражная система, план ~44-62 ч
- `03_SOCIAL_HUMAN_BEHAVIOR_ANALYSIS.md` — теоретическая база (6 соц-псих теорий), эмоциональная система (NpcEmotion), личностные черты (NpcPersonalityConfig), социальные триггеры, vocal cues
- `04_UNIFIED_BEHAVIOR_ARCHITECTURE.md` — **синтез (целевой документ)**: composition-first архитектура (NpcSocialBrain), объединённый план 4 фаз, ~54 ч

**Ключевые решения:**
- Composition-first: NpcBrain не трогаем, вся новая логика в NpcSocialBrain
- Эмоции (6) + Personality (5 traits) из 03
- 7 Social Triggers + 5 Vocal Cues из 03
- Patrol/Flee/Alarm/Grudge — Phase 1 P0
- GroupCoordinator + Morale — Phase 2 P1
- Cover/Surrender/Post-combat — Phase 3 P2
