# T-FO06P — reviewed participant identity mapping

Дата: 2026-09-11. Основание: `06O_PARTICIPANT_IDENTITY_CENSUS.json` и closed-world manifest contract из `06N_PARTICIPANT_MANIFEST_DESIGN.md`.

## 1. Граница этапа

Этап выполняет только read-only mapping census records к фиксированным candidate IDs. Live participant manifest не создаётся, runtime discovery не подключается, сцены и объекты Unity не изменяются.

Исходный census:

`docs/world/floatingorigin/06O_PARTICIPANT_IDENTITY_CENSUS.json`

Результат mapping:

`docs/world/floatingorigin/06P_PARTICIPANT_IDENTITY_MAPPING.json`

## 2. Реализация

Создан Editor-only инструмент:

`Assets/_Project/Editor/FloatingOrigin/BuildGlobalMotionRebaseParticipantMapping.cs`

Команда Unity:

`ProjectC/World/Floating Origin/Build Reviewed Participant Mapping (Read Only)`

Инструмент читает только ранее созданный census report и проверяет:

- обе canonical scenes присутствуют в отчёте, загружены и не dirty;
- ровно `22` ship-root records;
- ровно `20` `ShipDeckNav` records;
- ровно один camera candidate `BootstrapScene/MainCamera`;
- точное pairing-соответствие `SHIP_DECK_NAV/01–20` с `SHIP_ROOT/03–22` по loaded hierarchy path;
- наличие `WorldRoot_0_0` и `Respawn_Default` как отдельных boundary/anchor records.

## 3. Mapping fixed participants

Deterministic candidate mapping:

- `SHIP_ROOT/01–22` — ship roots в ordinal census order;
- `SHIP_DECK_NAV/01–20` — named ship roots `SHIP_ROOT/03–22` с тем же exact path и `GlobalObjectId`;
- `CITY_STATIC` — `WorldRoot_0_0` как boundary candidate only;
- `WORLD_ANCHORS` — `Respawn_Default` как explicit anchor candidate;
- `CAMERA` — `BootstrapScene/MainCamera` как identity candidate.

Текущие первые записи подтверждены так:

| Participant ID | Source path | Status |
|---|---|---|
| `SHIP_ROOT/01` | `Ship_Light_root` | candidate, not admitted |
| `SHIP_ROOT/02` | `Ship_Light_root (копия с компьютера DESKTOP-K00O7HK)` | candidate, not admitted |
| `SHIP_ROOT/03` | `Альбатрос` | candidate, not admitted |
| `SHIP_DECK_NAV/01` | `Альбатрос` | runtime registration unverified |
| `CITY_STATIC` | `WorldRoot_0_0` | boundary only |
| `WORLD_ANCHORS` | `Respawn_Default` | pose/runtime role unverified |
| `CAMERA` | `MainCamera` | owner/history unverified |

Ordinal assignment is deterministic census-order mapping, not a declaration that the order is the final gameplay/ship identity order. Owner review is still required before live manifest publication.

## 4. NetworkObject policy

Все `96` census `NetworkObject` candidates записаны в `networkPolicy` с `admitted=false`.

Классификация:

- exact ship root — `SHIP_ROOT_COVERED_BY_FIXED_MAPPING`;
- NetworkObject внутри ship root subtree — `SHIP_SUBTREE_REVIEW_REQUIRED`;
- BootstrapScene candidate — `BOOTSTRAP_NETWORK_REVIEW_REQUIRED`;
- WorldScene_0_0 candidate вне ship root — `WORLD_NETWORK_REVIEW_REQUIRED`.

Ни один `NETWORK_GAMEPLAY_ROOT/<stable-id>` автоматически не добавлен. Это сохраняет closed-world правило и не делает выводов об ownership, spawn lifetime, authority или необходимости spatial shift.

## 5. Что доказано и что остаётся inconclusive

Доказано:

- census records имеют стабильные `GlobalObjectId` и exact loaded hierarchy paths;
- fixed-count mapping покрывает `22` ship roots и `20` deck-nav candidates;
- deck-nav pairing не расходится с соответствующими ship-root paths;
- boundary candidates для city, world anchor и camera существуют;
- network candidates получили явную policy classification без automatic admission.

Остаётся **INCONCLUSIVE**:

- окончательный владелец ordinal mapping для `SHIP_ROOT/01–22`;
- active camera owner, target binding и history continuity;
- runtime `ShipDeckNav` registration, NavMesh origin и passenger provenance;
- classification scene-owned NetworkObjects в `NETWORK_GAMEPLAY_ROOT` или explicit exclusion;
- serialized live manifest, digest publication и adapter readiness.

## 6. Решение этапа

Reviewed candidate identity mapping — **PASS**.

Live participant manifest, concrete adapters, `Apply/Rebuild/Validate/Publish` и runtime frame mutation — **NOT CREATED / NOT READY**.

Следующий этап должен получить owner-reviewed approval для ordinal mapping и отдельно закрыть camera/runtime deck/network policy evidence. До этого mapping report остаётся evidence artifact, а не runtime configuration.
