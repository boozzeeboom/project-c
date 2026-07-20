# Итерации разработки — NPC Quests

## Итерация от 2026-07-09

**Задача:** DialogWindow: текст NPC всегда виден сверху, кнопки квестов прокручиваются (scroll)
**Коммит:** `aa2a1ec` — T-UI04: фикс DialogWindow — текст NPC всегда виден, кнопки квестов прокручиваются

**Изменения:**
- `Assets/_Project/Quests/Resources/UI/DialogWindow.uxml` — options обёрнут в `<ui:ScrollView name="options-scroll">`
- `Assets/_Project/Quests/Resources/UI/DialogWindow.uss` — panel: `min-height:400px` + `max-height:85vh`; text-scroll: `min-height:80px`; options-scroll: `max-height:220px`

## Итерация от 2026-07-20

**Задача:** T-CNPC-01: интеграция AI+Quest через репутацию — связываем атакующего и квестового NPC на одном GameObject
**Коммит:** `f27c857b03044b61366333a314edf171a7e41d4a` — T-CNPC-01: интеграция AI+Quest через репутацию
**Изменения:**
- `Assets/_Project/Scripts/AI/NpcBrain.cs` (+70 строк): поля `_npcId`, `_hostilityThreshold`, `_respawnConfig`; кэширование npcId из NpcController; ModifyNpcAttitude(-2) при ударе; подписка на NpcAttitudeChangedEvent для смены BehaviorType; OnNpcDeath + RespawnCoroutine
- `Assets/_Project/Scripts/Combat/Implementations/NpcTarget.cs` (+8 строк): public OnKilledEvent; ResetHealth(); замена Destroy на NpcBrain.OnNpcDeath
- `Assets/_Project/Scenes/World/WorldScene_0_0.unity`: [Mira] — добавлены NetworkObject, NavMeshAgent, NpcBrain(Passive), NpcTarget, NpcAttacker, NpcSocialBrain(faction=villagers), NetworkTransform
- `Assets/_Project/Resources/Combat/NpcCombatData_Mira.asset` (новый SO: HP=500)
- `docs/NPC_quests/Complete_v2/*` (3 документа: полный анализ + архитектура + план)

## Итерация от 2026-07-09 (аудит)

**Задача:** Глубокий аудит всей системы квестов — архитектура, стабы, дублирование, интеграции
**Коммит:** `13f3c7f` — T-QAUDIT: Глубокий аудит системы квестов (NPC Quests v2)

**Изменения:**
- `docs/NPC_quests/DEEP_AUDIT_2026-07-09.md` — полный аудит (319 строк)

## Итерация от 2026-07-13 (комбинированный аудит)

**Задача:** Повторный глубокий аудит системы квестов — сравнение с предыдущим, выявление регрессов и незавершённых интеграций
**Коммит:** (pending — пользователь)

**Изменения:**
- `docs/NPC_quests/DEEP_AUDIT_2026-07-13.md` — комбинированный аудит (сопоставлен с предыдущим)
- **Критическое открытие:** квестовые ассеты (FactionDefinition, NpcDefinition, QuestDefinition) утеряны — файлы отсутствуют, GUIDs в QuestDatabase висят в никуда
