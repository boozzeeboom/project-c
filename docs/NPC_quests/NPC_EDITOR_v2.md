# NPC Definition Custom Editor v2

> **Дата:** 2026-07-21
> **Задача:** Кастомный редактор NpcDefinition с drag-and-drop квестов, организованными блоками и сводкой.

---

## Что изменилось

### NpcDefinition.cs
- Добавлены `questOfferRefs` (QuestDefinition[]) и `questTurnInRefs` (QuestDefinition[]) поля
- Старые `questOffers` / `questTurnIns` (string[]) сохранены для CSV-импорта
- Добавлены хелперы: `GetQuestOfferIds()` / `GetQuestTurnInIds()` — возвращают ID из refs (приоритет) или string-фолбэк
- Все потребители (QuestServer, QuestWorld, NpcWorldInspectorWindow, QuestDatabaseWindow) обновлены на использование хелперов

### NpcDefinitionEditor.cs (NEW)
Кастомный Editor с цветными блоками:

```
┌──────────────────────────────────────────────────────────┐
│  👤 Мира Тихоступ                                        │
│  ID: mira_01    ⚑ Guild Of Thoughts          [📍 Ping]  │
├──────────────────────────────────────────────────────────┤
│  📜 Offers: 2    ✅ Turn‑ins: 1    🛠 Services: Trade    │
│  💬 Tree: MiraDefault                                    │
├──────────────────────────────────────────────────────────┤
│  🆔 Identity                           [свёрнуто]        │
│  🖼 Visuals                            [свёрнуто]        │
│  💬 Dialogue                           [свёрнуто]        │
│  📜 Quests                             [развёрнуто]      │
│    Offers (NPC can GIVE these quests):                   │
│    ┌──────────────────────────────────────────────────┐  │
│    │ Find Artifact          find_artifact    [▲][▼][×]│  │
│    │ Copper Ore Delivery    copper_ore_del   [▲][▼][×]│  │
│    └──────────────────────────────────────────────────┘  │
│                [+ Add Offer]                              │
│    Turn‑Ins (NPC ACCEPTS these quests back):              │
│    ┌──────────────────────────────────────────────────┐  │
│    │ Find Artifact          find_artifact    [▲][▼][×]│  │
│    └──────────────────────────────────────────────────┘  │
│                [+ Add Turn‑In]                            │
│  🛠 Services                           [свёрнуто]        │
│  🤝 Interaction                        [свёрнуто]        │
│  📈 Attitude & Reputation Links        [свёрнуто]        │
│  🔊 Audio                              [свёрнуто]        │
└──────────────────────────────────────────────────────────┘
```

### Ключевые фичи
- **Drag-and-drop квестов**: больше не нужно вводить ID вручную — перетаскиваешь .asset из Data/Quests/
- **Цветные блоки**: каждая секция свёрнута по умолчанию, раскрывается по клику
- **Сводка наверху**: количество offers/turn-ins, сервисы, диалоговое дерево
- **Чипы сервисов**: показывают активные сервисы (Trade, Repair, Refuel и т.д.) зелёным
- **Кнопки ▲▼×**: переупорядочивание и удаление элементов массива
- **Превью имени квеста**: рядом с ObjectField показывается questId синим цветом
- **Авто-fallback**: если DialogTree не назначен — показывается info-box про T-Q28 auto-build

### Обратная совместимость
- CSV-импорт продолжает писать в `questOffers[]` / `questTurnIns[]` (string[])
- `GetQuestOfferIds()` / `GetQuestTurnInIds()` проверяют refs сначала, затем строки
- Если заданы и refs и strings — refs имеют приоритет
- Старый код, читающий `npc.questOffers` напрямую, заменён на хелперы

---

## Затронутые файлы

| Файл | Изменение |
|------|-----------|
| `Quests/Npcs/NpcDefinition.cs` | +questOfferRefs, +questTurnInRefs, +GetQuestOfferIds(), +GetQuestTurnInIds() |
| `Quests/Editor/NpcDefinitionEditor.cs` | NEW: кастомный Editor |
| `Quests/Network/QuestServer.cs` | BuildFallbackDialogTree → GetQuestOfferIds()/GetQuestTurnInIds() |
| `Quests/Core/QuestWorld.cs` | TryTurnIn → GetQuestTurnInIds() |
| `Editor/Tools/NpcWorldInspectorWindow.cs` | → GetQuestOfferIds()/GetQuestTurnInIds() |
| `Quests/Editor/QuestDatabaseWindow.cs` | → GetQuestOfferIds()/GetQuestTurnInIds() |

---

## Как использовать

1. Открыть NPC .asset (например `Mira.asset`)
2. Развернуть секцию **📜 Quests**
3. В блок **Offers** перетащить QuestDefinition .asset из `Assets/_Project/Quests/Data/Quests/`
4. В блок **Turn‑Ins** — аналогично
5. Использовать ▲▼ для порядка, × для удаления

### Для CSV-импорта
Ничего не меняется — `questOffers[]` и `questTurnIns[]` (string[]) продолжают работать. При следующем открытии в инспекторе, если заданы только строки, они покажутся как fallback-поля под ref-полями.
