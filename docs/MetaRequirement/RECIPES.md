# MetaRequirement — Рецепты конфигураций

**Документ:** примеры использования `MetaRequirement` для разных игровых кейсов
**Дата:** 2026-06-06
**Связанный документ:** `docs/MetaRequirement/00_OVERVIEW.md`

---

## Как читать рецепты

Каждый рецепт:
1. **Сценарий** — игровая ситуация
2. **Что нужно** — какие `ItemData` создать
3. **Конфигурация `MetaRequirement`** — значения полей в Inspector
4. **Поведение игрока** — что он видит и что происходит
5. **Заметки** — gotcha, edge case

**Условные обозначения в таблицах:**
- `[A, B, C]` = `_requiredItems[]` содержит A, B, C (порядок не важен)
- `_logic=All|Any|AtLeastN` = enum RequirementLogic
- `_requiredCount=N` = поле `_requiredCount` (только для `AtLeastN`)
- `_consumeOnUse=true|false` = забрать предметы после успеха
- `_interactableDisplayName="..."` = для toast'а
- `_customFailureMessage="..."` = кастомный текст (опционально)

---

## Recipe 1: Корабль с 1 ключом (базовый кейс из MVP)

**Сценарий:** Игрок подбирает жёлтый ключ, садится в `Ship_Light`. Без ключа — toast.

**ItemData:**
- `Item_Key_ShipLight.asset` (Equipment, maxStack=1)

**Конфигурация `MetaRequirement` на `Ship_Light`:**
| Поле | Значение |
|---|---|
| `_requiredItems[]` | `[Item_Key_ShipLight]` |
| `_logic` | `All` |
| `_requiredCount` | (не используется) |
| `_consumeOnUse` | `false` |
| `_interactableDisplayName` | `"Корабль Light"` |
| `_customFailureMessage` | (пусто) |

**Поведение:**
- Без ключа: toast `"Нет ключа корабля (Корабль Light)"`
- С ключом: игрок садится за штурвал, ключ остаётся в инвентаре

**Заметки:** Базовый случай. Backward compatible с Ship Key Subsystem MVP.

---

## Recipe 2: Дверь с 1 предметом (новый use case)

**Сценарий:** Игрок подбирает `Key_OldDoor`, открывает старую дверь. Ключ остаётся (можно открыть повторно).

**ItemData:**
- `Item_Key_OldDoor.asset` (Equipment, maxStack=1)

**Конфигурация `MetaRequirement` на `[OldDoor]`:**
| Поле | Значение |
|---|---|
| `_requiredItems[]` | `[Item_Key_OldDoor]` |
| `_logic` | `All` |
| `_consumeOnUse` | `false` |
| `_interactableDisplayName` | `"Старая дверь"` |
| `_customFailureMessage` | `"Нужен ключ от старой двери"` |

**Поведение:**
- Без ключа: toast `"Нужен ключ от старой двери"`
- С ключом: дверь открывается (через свой DoorController), F повторно → закрывается

**Заметки:** Это **новый** use case, который Ship Key Subsystem **не** покрывал. `MetaRequirement` — generic, работает с любым `Interactable`.

---

## Recipe 3: Босс-зона требует 3 квестовых предмета (AND-логика)

**Сценарий:** Игрок должен собрать 3 амулета (Солнца, Луны, Звёзд), чтобы войти в комнату босса.

**ItemData:**
- `Item_Amulet_Sun.asset`
- `Item_Amulet_Moon.asset`
- `Item_Amulet_Star.asset`

**Конфигурация `MetaRequirement` на `[BossZone]`:**
| Поле | Значение |
|---|---|
| `_requiredItems[]` | `[Item_Amulet_Sun, Item_Amulet_Moon, Item_Amulet_Star]` |
| `_logic` | `All` |
| `_consumeOnUse` | `false` |
| `_interactableDisplayName` | `"Зал Босса"` |
| `_customFailureMessage` | `"Соберите все 3 амулета, чтобы войти"` |

**Поведение:**
- Без амулетов: toast `"Соберите все 3 амулета, чтобы войти"`
- С 1-2 амулетами: toast `"Не хватает: [амулет Луны, амулет Звёзд]"`
- Со всеми 3: вход в зону босса

**Заметки:** `MetaRequirement` динамически генерирует reason по недостающим. UI показывает список.

---

## Recipe 4: Дверь с альтернативными ключами (OR-логика)

**Сценарий:** Старый склад можно открыть **любым из 3** ключей: Ключ Смотрителя, Ключ Охраны, Взлом-отмычка.

**ItemData:**
- `Item_Key_Warden.asset`
- `Item_Key_Guard.asset`
- `Item_Lockpick.asset`

**Конфигурация `MetaRequirement` на `[OldWarehouse]`:**
| Поле | Значение |
|---|---|
| `_requiredItems[]` | `[Item_Key_Warden, Item_Key_Guard, Item_Lockpick]` |
| `_logic` | `Any` |
| `_consumeOnUse` | `false` |
| `_interactableDisplayName` | `"Старый склад"` |
| `_customFailureMessage` | `"Нужен один из ключей: Смотрителя, Охраны или Взлом-отмычка"` |

**Поведение:**
- Без ключей: toast с кастомным сообщением
- С любым 1 ключом: дверь открывается

**Заметки:** Or-логика — игрок может пройти разными путями. UI показывает **все варианты** для подсказки.

---

## Recipe 5: Прогресс-квест "собрать 5 из 8 фрагментов" (AT_LEAST_N)

**Сценарий:** В мире 8 фрагментов карты. Игрок должен собрать **минимум 5** для прохождения.

**ItemData:**
- `Item_Map_Fragment_1..8` (8 штук)

**Конфигурация `MetaRequirement` на `[FinalDoor]`:**
| Поле | Значение |
|---|---|
| `_requiredItems[]` | `[Fragment1, Fragment2, ..., Fragment8]` |
| `_logic` | `AtLeastN` |
| `_requiredCount` | `5` |
| `_consumeOnUse` | `false` |
| `_interactableDisplayName` | `"Финальная дверь"` |
| `_customFailureMessage` | (пусто — UI сгенерирует `"Соберите ещё X фрагментов"`) |

**Поведение:**
- С 0 фрагментов: toast `"Не хватает: [F1, F2, F3, F4, F5]"`
- С 3 из 5: toast `"Прогресс: 3/5. Не хватает: [F4, F5]"`
- С 5+: дверь открывается (любые 5)

**Заметки:** В UI показывается прогресс через `ProgressInfo { Have, Required }`. Полезно для quest tracker.

---

## Recipe 6: Дверь-жертва (потребляет предмет)

**Сценарий:** Древний алтарь принимает "Жертвенный клинок" и **забирает его** из инвентаря, открывая проход.

**ItemData:**
- `Item_Sacrifice_Blade.asset` (Equipment, maxStack=1)

**Конфигурация `MetaRequirement` на `[AncientAltar]`:**
| Поле | Значение |
|---|---|
| `_requiredItems[]` | `[Item_Sacrifice_Blade]` |
| `_logic` | `All` |
| `_consumeOnUse` | `true` |
| `_interactableDisplayName` | `"Древний алтарь"` |
| `_customFailureMessage` | `"Алтарю нужна жертва. Положите клинок."` |

**Поведение:**
- Без клинка: toast
- С клинком: алтарь активируется, клинок **исчезает** из инвентаря, дверь открывается. Повторное использование — невозможно (клинок пропал)

**Заметки:**
- `_consumeOnUse=true` → `MetaRequirement.ConsumeRequiredItems` вызывается на сервере **после** успешного доступа.
- Транзакция НЕ атомарна в v1 (может быть race condition — см. `00_OVERVIEW.md` §6.6).
- **TODO v2:** резервирование предметов на время RPC.

---

## Recipe 7: NPC-диалог требует репутацию + предмет (комбинированное)

**Сценарий:** Старейшина пустит игрока дальше, **если** тот принёс "Свиток Приглашения" **И** имеет репутацию `>= 50`.

**⚠️ ВАЖНО:** Репутация — это **не предмет**. Это `Condition` (отдельная подсистема, **TODO**).

**Что НЕ входит в Этап 1:** условия-не-предметы (репутация, фракция, время суток). В `00_OVERVIEW.md` §7 явно указано, что это **отдельная подсистема `Conditions`**.

**Workaround для Этапа 1:** создать `ItemData` "Свиток Приглашения" + "Доказательство Дружбы" (флафф-предмет, выдаётся автоматически при репутации >= 50) → оба обязательны в `_requiredItems[]`.

**Нормальное решение:** `MetaRequirement` (предметы) **+** `ConditionRequirement` (репутация) — обе подсистемы. NPC проверяет обе.

---

## Recipe 8: Каскадный квест (ключ 1 → ключ 2 → дверь)

**Сценарий:** Игрок подбирает `Key_Lvl1`, открывает дверь Lvl1, находит внутри `Key_Lvl2`, открывает дверь Lvl2.

**ItemData:**
- `Item_Key_Lvl1.asset`
- `Item_Key_Lvl2.asset`

**Конфигурация:**

`MetaRequirement` на `[Door_Lvl1]`:
| Поле | Значение |
|---|---|
| `_requiredItems[]` | `[Item_Key_Lvl1]` |
| `_logic` | `All` |
| `_consumeOnUse` | `false` |

`MetaRequirement` на `[Door_Lvl2]`:
| Поле | Значение |
|---|---|
| `_requiredItems[]` | `[Item_Key_Lvl2]` |
| `_logic` | `All` |
| `_consumeOnUse` | `false` |

**Поведение:** Игрок проходит уровни последовательно. Ключи остаются в инвентаре (можно переиграть уровни).

**Заметки:** Никакой специальной поддержки каскадов не нужно — это просто **несколько** `MetaRequirement` в сцене.

---

## Recipe 9: NPC-торговец (без требований)

**Сценарий:** Любой игрок может поговорить с NPC-торговцем.

**Конфигурация `MetaRequirement` на `[TraderNPC]`:**
| Поле | Значение |
|---|---|
| `_requiredItems[]` | `[]` (пустой массив) |
| `_logic` | `All` |
| `_customFailureMessage` | (пусто) |

**Поведение:** всегда `true` (нет требований). Можно упростить — **не вешать** `MetaRequirement` вообще. Но если хочется иметь **консистентный UI** (все interactable показывают подсказку), можно оставить пустой `MetaRequirement`.

**Заметки:** `OnValidate` warning "Empty _requiredItems" — оставить или удалить. Решение за дизайнером.

---

## Recipe 10: Замок с подсказкой (Hint UI)

**Сценарий:** Замок с 3 ключами. UI при наведении показывает прогресс "2/3 собрано".

**ItemData:**
- `Item_Key_A.asset`, `Item_Key_B.asset`, `Item_Key_C.asset`

**Конфигурация `MetaRequirement` на `[TripleLockDoor]`:**
| Поле | Значение |
|---|---|
| `_requiredItems[]` | `[Item_Key_A, Item_Key_B, Item_Key_C]` |
| `_logic` | `All` |
| `_consumeOnUse` | `false` |
| `_interactableDisplayName` | `"Замок с тремя ключами"` |

**UI подсказка (через `ProgressInfo`):**
- У игрока есть A и B → tooltip: `"Прогресс: 2/3. Не хватает: [C]"`
- У игрока есть все → tooltip: `"Можно открыть"`

**Поведение:** Игрок подходит к двери, наводит мышь → tooltip показывает прогресс. Нажимает F → если `>=` требования, открывается. Иначе — toast.

**Заметки:** `ProgressInfo` API доступен через `MetaRequirement.GetPlayerProgress(clientId)`. UI подписывается на `MetaRequirementClientState.OnBindingsUpdated` и `InventoryClientState.OnSnapshotUpdated` для автообновления.

---

## Сводная таблица (cheat sheet для дизайнеров)

| Кейс | `_requiredItems` | `_logic` | `_requiredCount` | `_consumeOnUse` |
|---|---|---|---|---|
| 1 ключ | `[Key]` | `All` | - | `false` |
| Много предметов (AND) | `[A, B, C]` | `All` | - | `false` |
| Альтернативы (OR) | `[A, B, C]` | `Any` | - | `false` |
| Прогресс N из M | `[F1, F2, ..., FM]` | `AtLeastN` | `N` | `false` |
| Жертва (потребление) | `[Sacrifice]` | `All` | - | `true` |
| Без требований | `[]` | `All` | - | `false` |

---

## Что **не** покрывают рецепты (за пределами MVP)

- **Время суток** (`Condition` подсистема)
- **Репутация/фракция** (`Condition` подсистема)
- **Кооперативный доступ** (требует N игроков с разными ключами) — TODO после Этапа 2
- **Динамические требования** (NPC даёт квест, и только после этого дверь открывается) — `Quest` подсистема
- **Crafting** (A + B → C) — отдельная подсистема (см. `00_OVERVIEW.md` §7)

Эти кейсы **решаются** на уровне других подсистем, которые могут **делегировать** проверку доступа к `MetaRequirement`, или работать параллельно.
