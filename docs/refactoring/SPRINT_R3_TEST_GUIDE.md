# SPRINT_R3 — Гайд тестирования

## R2-BUG-001: Контракты RPC
1. Play → подойти к NPC-агенту
2. Нажать **C** → открывается ContractBoardUI
3. ✅ Появляются доступные контракты из сервера
4. Выбрать → **Enter** → "Взять"
5. ✅ Console: нет варнингов "NetworkPlayer не Owner"

## R2-BUG-002: Торговля RPC
1. Play → подойти к NPC-торговцу
2. Нажать **E** → открывается TradeUI
3. Нажать **1** или кликнуть КУПИТЬ
4. ✅ Кредиты уменьшились, товар на складе
5. ✅ Console: нет варнингов "NetworkPlayer не найден"

## HOTFIX: NetworkManager initialization
- При старте Play проверь что **НЕТ** ошибки "Rpc methods can only be invoked after starting the NetworkManager"
- TradeUI.TryBuyItem/TrySellItem теперь проверяют IsListening перед отправкой RPC

## R3-001: Inventory без reflection
1. Tab → открыть инвентарь
2. Собрать предмет в мире
3. Tab → закрыть, проверить обновление
4. ✅ Console: нет ошибок reflection

## Общее
- [ ] Console: 0 ошибок
- [ ] Build: 0 варнингов
- [ ] **Multiplayer**: 2 клиента — контракты и торговля работают для обоих
