# T-X1 — Trade.Core.NPCTrader → MarketTrader (small, 10 мин) ✅ DONE 2026-06-08

**Дата:** 2026-06-08
**Roadmap:** `docs/NPC_quests/08_ROADMAP.md` §8.3 T-X1 (M9 cleanup)

## Скоуп

### Готово
- **Rename** `class NPCTrader` → `class MarketTrader` (in namespace `ProjectC.Trade.Core`). ✅
- **Rename file** `NPCTrader.cs` → `MarketTrader.cs` (+ meta deleted). ✅
- **Update references** в `TradeWorld.cs`:
  - `List<NPCTrader> _npcTraders` → `List<MarketTrader> _npcTraders` (list name kept для source compat)
  - `IReadOnlyList<NPCTrader> NpcTraders` → `IReadOnlyList<MarketTrader> NpcTraders` (property name kept)
  - 4× `NPCTrader.CreateDefault(...)` → `MarketTrader.CreateDefault(...)` ✅
- Method name `InitDefaultNPCTraders` оставлен (private, internal API).

### Не в скоупе T-X1 (deferred)
- **`TradeWorld.NpcTraders` public property** name kept (используется в других местах как conceptual noun "NPC traders" — оставлен для source compat, не breaking).
- **`_npcTraders` field** name kept (same).
- **`InitDefaultNPCTraders` method** name kept (private).

## Transitive deps audit

| File | Status |
|------|--------|
| `Assets/_Project/Trade/Scripts/Core/TradeWorld.cs` | Real refs (5 places: field, property, 4 CreateDefault calls). Updated. ✅ |
| `Assets/_Project/Trade/Scripts/Core/MarketTrader.cs` | (new) ✅ |
| All other .cs files | **0 references** to `NPCTrader` class ✅ |
| `graphify-out/cache/*.json` (2 files) | **Read-only cache** — не load-bearing, не требует обновления (можно `rm graphify-out/cache/` и перегенерировать если будет жаловаться). |

## Файлы

### New
- `Assets/_Project/Trade/Scripts/Core/MarketTrader.cs` (NEW, 121 LOC, идентичная логика с NPCTrader)

### Deleted
- `Assets/_Project/Trade/Scripts/Core/NPCTrader.cs` + `.meta`

### Modified
- `Assets/_Project/Trade/Scripts/Core/TradeWorld.cs` — 5 refs updated

## Verify

1. **Compile:** 0 errors. ✅
2. **Play Mode:** Start host → проверять economy:
   - `[TradeWorld] Initialized: markets=4, npcTraders=4` (теперь `MarketTrader` instances, но log text same).
   - NPC traders перемещают cargo между рынками каждый tick (no regression).
3. **Warnings:** никаких новых warnings (rename internal type — не deprecated API).

## Risk

**Low.** ✅ Internal rename. No external API changes (public property name kept).
