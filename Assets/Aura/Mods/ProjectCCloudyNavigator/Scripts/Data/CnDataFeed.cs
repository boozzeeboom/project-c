// Cloudy Navigator — шаг 3 плана переноса (04_PORT_RULES_AND_PLAN.md §4).
//
// Слой данных окна: ТОЛЬКО ЧТЕНИЕ существующих систем игры. Ни одного своего значения «об игре»:
// каждый ряд — проекция реального DTO или реального компонента. Свои поля есть только там, где значение
// вычисляется на клиенте для показа (вес багажа, число установленных модулей, формат строки) — они помечены Derived.
//
// Почему проекция, а не сами DTO: окну (шаг 4) нужны плоские значения без Unity-объектов и без знания
// о namespace'ах игровых DTO; кроме того проекция не даёт «протащить» в UI объект, который может умереть.
//
// Запросов к серверу здесь нет. Детали груза читаются из уже полученного кэша (`TelemetryCargoState.cargoDetail`,
// как в легаси MyShipsTab); `ShipCargoClientState.RequestDetail` в этом шаге не вызывается — это действие шага 7 (D5).
//
// Время окна — только Tick(dt) от хоста: setTimeout/@keyframes в порте нет (04 §1).
using System.Collections.Generic;
using System.Text;
using ProjectC.Combat;
using ProjectC.Core;
using ProjectC.Equipment;
using ProjectC.Factions;
using ProjectC.Items;
using ProjectC.Items.Client;
using ProjectC.Knowledge;
using ProjectC.Localization;
using ProjectC.Player;
using ProjectC.Quests;
using ProjectC.Quests.Client;
using ProjectC.Quests.UI;
using ProjectC.Reputation;
using ProjectC.Ship;
using ProjectC.Ship.Key;
using ProjectC.Skills;
using ProjectC.Stats;
using ProjectC.Trade.Client;
using Unity.Netcode;
using UnityEngine;

namespace ProjectCCloudyNavigator.Data
{
    /// <summary>Одна цель квеста — проекция <c>ObjectiveProgressDto</c>.</summary>
    public struct CnQuestGoal
    {
        public string description;
        public bool completed;
        public int currentValue;
        public int requiredQuantity;
    }

    /// <summary>Строка квеста — проекция <c>QuestProgressDto</c>.</summary>
    public struct CnQuestRow
    {
        public string questId;
        public string displayName;
        public string currentStageId;
        public byte state;
        public bool isTracked;
        public CnQuestGoal[] goals;
    }

    /// <summary>Строка контракта — проекция <c>ContractDto</c>.</summary>
    public struct CnContractRow
    {
        public string contractId;
        public byte type;
        public string typeLocalizationKey;
        public string typeUiClass;
        public byte state;
        public string itemId;
        public string displayName;
        public int quantity;
        public string fromLocationId;
        public string toLocationId;
        public float reward;
        public float cargoValue;
        public float timeLimit;
        public float timeRemaining;
        public bool isReceiptContract;
        public bool receiptCargoIssued;
        public bool isActive;
    }

    /// <summary>Предмет багажа — проекция <c>InventoryItemDto</c> + вес из <c>ItemData</c>.</summary>
    public struct CnBagItem
    {
        public int itemId;
        public byte type;
        public int quantity;
        public int slotIndex;
        public int instanceId;
        public string itemName;
        public float weightKg;
    }

    /// <summary>Модуль корабля — проекция <c>ModuleSlot</c> + <c>ShipModule</c>.</summary>
    public struct CnModuleRow
    {
        public string slotType;   // ProjectC.Ship.SlotType.ToString() — имя члена enum, без своей копии
        public bool isOccupied;
        public string moduleId;
        public string displayName;
        public int powerConsumption;
        public int cargoSlotsBonus;
    }

    /// <summary>Строка груза корабля — проекция <c>CargoDetailDto</c>.</summary>
    public struct CnCargoRow
    {
        public string itemId;
        public string displayName;
        public int quantity;
        public float unitWeight;
        public byte flags;      // бит0 = опасный (IsDangerous), бит1 = хрупкий (IsFragile)
        public bool dangerous;
        public bool fragile;
    }

    /// <summary>Корабль — проекция <c>ShipTelemetryState</c> + слоты + груз.</summary>
    public struct CnShipRow
    {
        public ulong shipNetworkObjectId;
        public int keyInstanceId;
        public string displayName;
        public string className;
        public Vector3 position;
        public float fuelNormalized;
        public float fuelMax;
        public int hullCurrent;
        public int hullMax;
        public int cargoUsed;
        public int cargoMax;
        public int moduleCount;
        public bool isEngineRunning;
        public CnModuleRow[] modules;
        public CnCargoRow[] cargoDetail;
    }

    /// <summary>Фракция — проекция <c>ReputationEntryDto</c> + <c>FactionDefinition.GetTier</c>.</summary>
    public struct CnFactionRow
    {
        public byte wireId;
        public string displayName;
        public int value;
        public bool hasTiers;
        public string tier;
        public string ussClass;
        public Color tierColor;
    }

    /// <summary>NPC — проекция <c>NpcAttitudeEntryDto</c> + имя из <c>QuestDatabase.GetNpc</c>.</summary>
    public struct CnNpcRow
    {
        public string npcId;
        public string displayName;
        public int value;
    }

    /// <summary>
    /// Узел графа связей: фракция, человек или квест. Узлы строятся только по ВИДИМЫМ субъектам — тем, кого
    /// прислал сервер (репутация, отношение) или кто есть в видимых заданиях: скрытое знание окно не раскрывает.
    /// </summary>
    public struct CnGraphNode
    {
        public string key;      // "f:1" / "n:mira_01" / "q:onboarding_alfa"
        public byte kind;       // 0 фракция, 1 человек, 2 квест
        public string name;
        public int value;       // репутация / отношение; у квеста 0
    }

    /// <summary>
    /// Ребро графа — только поле ассета: фракция NPC, поведение NPC, вражда/союз фракций, кто предлагает квест,
    /// кто принимает сдачу, награда, требование. Шесть типов и их номера — как в моде (relations.js, EDGE_TYPES):
    /// легенда будущего вида переносится один в один, без сдвига на единицу.
    /// </summary>
    public struct CnGraphEdge
    {
        public string from;
        public string to;
        // 1 состоит в, 2 влияет на, 3 союз/вражда, 4 выдаёт/принимает, 5 награда, 6 требует
        public byte type;
        public string label;    // глагол ребра (у 2 — дельты, у 5 и 6 — значение условия)
        public bool negative;   // вражда либо минус в награде/поведении
    }

    /// <summary>Слот навыка — проекция <c>SkillInputService.GetSkillForSlot</c> + имени из <c>SkillNodeConfig</c>.</summary>
    public struct CnSkillSlotRow
    {
        public byte slot;            // (byte)SkillInputSlot: Primary 1, Secondary 2, Slot1…4 = 10…13
        public string code;          // «ЛКМ», «ПКМ», «1»…«4»
        public string skillId;
        public string displayName;
        public string description;
        public bool bound;
        public bool onCooldown;
        public bool known;
    }

    /// <summary>Узел графа навыков — проекция <c>SkillNodeConfig</c> + <c>SkillsClientState</c> (выучен / известен).</summary>
    public struct CnSkillNodeRow
    {
        public string skillId;
        public string displayName;
        public string description;
        public byte category;        // SkillCategory: Social 0, Combat 1
        public int treeX;
        public int treeY;
        public bool learned;
        public bool known;           // выучен или известен (KnownSkillIds) — как knowledgeUnlockType: learned ∪ known
        public bool alwaysVisible;   // knowledgeUnlockType == AlwaysVisible — единственный навык, видимый без знания
        public bool active;
        public float cooldown;
        public float learnXp;
        public int requiredStrength;
        public int requiredDexterity;
        public int requiredIntelligence;
        public string[] prerequisites;
        /// <summary>Гейт легаси <c>SkillTreeWindow.CanLearn</c>: не выучен и все предпосылки выучены.</summary>
        public bool canLearn;
    }

    /// <summary>
    /// Читающий слой окна. Держит последнюю прочитанную проекцию и обновляет её по кадру:
    /// снапшоты перечитываются каждый кадр (это копия ссылки, не запрос), тяжёлые источники — по интервалу,
    /// как в легаси (высота и телеметрия 0.2 с, календарь 2 с).
    /// </summary>
    public sealed class CnDataFeed
    {
        private const float ShipPollInterval = 0.2f;   // как AltitudeUI / MyShipsTab в легаси
        private const float TimePollInterval = 2f;     // как легаси-календарь
        private const float LogRepeatInterval = 1f;

        private readonly bool _log;

        private float _shipTimer;
        private float _timeTimer;
        private float _logTimer;
        private string _lastLog;

        // Локальный игрок и его HP (источник — PlayerTarget, как RefreshHpFromNetworkVariable в легаси).
        private PlayerTarget _playerTarget;

        // Каталог NPC нужен для имён и для подписи «сдать у …»; ищем один раз и только если он есть в сцене.
        private ProjectC.Quests.QuestDatabase _questDatabase;
        private bool _questDatabaseSearched;

        // Определения фракций тем же путём, что и каталог игры; заполняется лениво (см. FactionDef).
        private Dictionary<int, FactionDefinition> _factionDefs;

        // Каталог навыков — тот же Resources/Skills, что грузит легаси SkillTreeWindow; читается один раз.
        private SkillNodeConfig[] _skillCatalog;
        private Dictionary<string, SkillNodeConfig> _skillById;
        private bool _skillNodesBuilt;
        private HashSet<string> _skillNodesSigLearned;   // снапшот заменяет набор целиком — следим за ссылкой
        private HashSet<string> _skillNodesSigKnown;

        // --- последняя проекция ---
        public int currentHp { get; private set; }
        public int maxHp { get; private set; }
        public bool hasHp { get; private set; }

        public float credits { get; private set; }
        public bool hasCredits { get; private set; }

        public float strength { get; private set; }

        /// <summary>Имена надетых предметов по EquipmentData.SlotToIndex; пустая строка — слот пуст.</summary>
        public string[] equipNames { get; private set; }
        public bool hasEquipment { get; private set; }
        public float dexterity { get; private set; }
        public float intelligence { get; private set; }
        public bool hasStats { get; private set; }

        public CnQuestRow[] quests { get; private set; }
        public string trackedQuestId { get; private set; }

        public CnContractRow[] contracts { get; private set; }
        public bool canAcceptContracts { get; private set; }
        public float secondsUntilNextTick { get; private set; }
        public float debtAmount { get; private set; }
        public int debtLevel { get; private set; }

        public CnBagItem[] bagItems { get; private set; }
        public int bagMaxSlots { get; private set; }
        public string bagLocationId { get; private set; }

        public CnShipRow[] ships { get; private set; }

        public CnFactionRow[] factions { get; private set; }
        public CnNpcRow[] npcs { get; private set; }

        /// <summary>Шесть боевых слотов (SkillInputSlot) в порядке показа: ЛКМ, ПКМ, 1…4.</summary>
        public CnSkillSlotRow[] skillSlots { get; private set; }

        /// <summary>Узлы графа связей (фракции, люди, видимые квесты) — только то, что игра уже показала игроку.</summary>
        public CnGraphNode[] graphNodes { get; private set; }

        /// <summary>Рёбра графа: те же шесть типов, что в моде, каждое — реальное поле ассета.</summary>
        public CnGraphEdge[] graphEdges { get; private set; }

        public bool hasGraph => graphNodes != null && graphNodes.Length > 0;

        /// <summary>Каталог навыков из Resources/Skills — тот же, что грузит легаси SkillTreeWindow.</summary>
        public CnSkillNodeRow[] skillNodes { get; private set; }
        public bool hasSkills { get; private set; }
        public int learnedSkillCount { get; private set; }
        public int knownSkillCount { get; private set; }

        public bool hasTime { get; private set; }
        public int year { get; private set; }
        public int month { get; private set; }
        public int day { get; private set; }
        public int dayOfYear { get; private set; }
        public string weekdayName { get; private set; }
        public int weekday { get; private set; }               // GameTimeData.Weekday, с нуля
        public string[] weekdayNames { get; private set; }     // GetWeekdayName(0…6)

        /// <summary>CalendarConfig.daysPerWeek = 7 (03 §1).</summary>
        public const int DaysPerWeek = 7;
        public string monthName { get; private set; }
        public float timeOfDay { get; private set; }

        /// <summary>
        /// Правила календаря из того же <c>CalendarConfig</c>, что читает легаси-окно (30 дней, 12 месяцев):
        /// окну календаря нужна вся сетка года, а не только текущая дата.
        /// </summary>
        public bool hasCalendarRules { get; private set; }
        public int daysPerMonth { get; private set; }
        public int monthsPerYear { get; private set; }
        public string[] monthNames { get; private set; }
        public float dayCycleRealHours { get; private set; }

        /// <summary>Имя месяца 1…monthsPerYear из CalendarConfig; номер, если имени нет.</summary>
        public string MonthName(int month)
        {
            if (monthNames != null && month >= 1 && month <= monthNames.Length &&
                !string.IsNullOrEmpty(monthNames[month - 1]))
                return monthNames[month - 1];
            return month.ToString();
        }

        public bool hasAltitude { get; private set; }
        public float altitude { get; private set; }          // как легаси AltitudeUI: сырой transform.position.y
        public float rebaseOffsetY { get; private set; }     // Derived-диагностика, не для показа без решения
        public string corridorId { get; private set; }
        public string corridorDisplayName { get; private set; }
        public float corridorMin { get; private set; }
        public float corridorMax { get; private set; }
        public float corridorWarningMargin { get; private set; }
        public float corridorCriticalUpperMargin { get; private set; }
        public bool corridorIsGlobal { get; private set; }
        public int altitudeStatusValue { get; private set; }
        public string altitudeStatusName { get; private set; }

        public CnDataFeed(bool log)
        {
            _log = log;
            quests = new CnQuestRow[0];
            contracts = new CnContractRow[0];
            bagItems = new CnBagItem[0];
            ships = new CnShipRow[0];
            factions = new CnFactionRow[0];
            npcs = new CnNpcRow[0];
            trackedQuestId = string.Empty;
            bagLocationId = string.Empty;
            skillSlots = new CnSkillSlotRow[0];
            graphNodes = new CnGraphNode[0];
            graphEdges = new CnGraphEdge[0];
            skillNodes = new CnSkillNodeRow[0];
            weekdayName = string.Empty;
            monthName = string.Empty;
            monthNames = new string[0];
            corridorId = string.Empty;
            corridorDisplayName = string.Empty;
            altitudeStatusName = string.Empty;
        }

        /// <summary>Кадр окна. dt уже зажат хостом.</summary>
        public void Tick(float dt)
        {
            ReadCharacter();
            ReadEquipment();
            ReadQuests();
            ReadContracts();
            ReadInventory();
            ReadRelations();
            ReadGraph();
            ReadSkills();

            _shipTimer -= dt;
            if (_shipTimer <= 0f)
            {
                _shipTimer = ShipPollInterval;
                ReadShips();
                ReadAltitude();
            }

            _timeTimer -= dt;
            if (_timeTimer <= 0f)
            {
                _timeTimer = TimePollInterval;
                ReadTime();
            }

            if (!_log) return;
            _logTimer -= dt;
            if (_logTimer > 0f) return;
            _logTimer = LogRepeatInterval;

            // Пишем только при изменении: иначе за сессию консоль съест память.
            string text = Describe();
            if (text == _lastLog) return;
            _lastLog = text;
            Debug.Log("[CN] " + text);
        }

        /// <summary>
        /// Многострочный снимок в ТЕХ ЖЕ форматах, что показывает легаси-окно, — по нему идёт сверка шага 3
        /// (04 §4, ворота: значения провайдера = значения легаси).
        /// </summary>
        public string Describe()
        {
            var sb = new StringBuilder(512);

            sb.Append('\n').Append("CN-снимок (сверять с легаси P)")
              .Append('\n').Append("HP: ").Append(hasHp ? currentHp + "/" + maxHp : "нет данных")
              .Append('\n').Append("Кредиты: ").Append(hasCredits ? Mathf.FloorToInt(credits).ToString() + " CR" : "нет данных")
              .Append('\n').Append("STR ").Append(hasStats ? strength.ToString("0.#") : "—")
              .Append(" · DEX ").Append(hasStats ? dexterity.ToString("0.#") : "—")
              .Append(" · INT ").Append(hasStats ? intelligence.ToString("0.#") : "—");

            int activeQuests = 0;
            for (int i = 0; i < quests.Length; i++) if (quests[i].state == 2) activeQuests++; // QuestState.Active
            sb.Append('\n').Append("Квесты: ").Append(quests.Length).Append(" (активных ").Append(activeQuests).Append(")")
              .Append(" · слежу: ").Append(string.IsNullOrEmpty(trackedQuestId) ? "нет" : trackedQuestId);

            int activeContracts = 0;
            for (int i = 0; i < contracts.Length; i++) if (contracts[i].isActive) activeContracts++;
            sb.Append('\n').Append("Контракты: активных ").Append(activeContracts)
              .Append(", доступных ").Append(contracts.Length - activeContracts)
              .Append(", брать можно: ").Append(canAcceptContracts ? "да" : "нет")
              .Append(", до тика ").Append(FormatTimer(secondsUntilNextTick))
              .Append(", долг ").Append(Mathf.FloorToInt(debtAmount)).Append(" CR ур.").Append(debtLevel);

            float bagWeight = 0f;
            for (int i = 0; i < bagItems.Length; i++) bagWeight += bagItems[i].quantity * bagItems[i].weightKg;
            sb.Append('\n').Append("Багаж: ").Append(bagItems.Length).Append(" поз.")
              .Append(" · Слоты ").Append(bagItems.Length).Append(" / ").Append(bagMaxSlots)
              .Append(" · ").Append(bagWeight.ToString("0.0")).Append(" кг")
              .Append(" · локация ").Append(string.IsNullOrEmpty(bagLocationId) ? "—" : bagLocationId);

            for (int i = 0; i < ships.Length; i++)
            {
                var s = ships[i];
                sb.Append('\n').Append("Корабль ").Append(i + 1).Append(": ").Append(s.displayName)
                  .Append(" · ").Append(s.className)
                  .Append(" · Прочность: ").Append(s.hullCurrent).Append('/').Append(s.hullMax)
                  .Append(" · Топливо: ").Append((s.fuelNormalized * 100f).ToString("0.0")).Append('%')
                  .Append(" (").Append(s.fuelMax.ToString("0")).Append(" max)")
                  .Append(" · Груз: ").Append(s.cargoUsed).Append('/').Append(s.cargoMax)
                  .Append(" · ENGINE ").Append(s.isEngineRunning ? "ON" : "OFF")
                  .Append(" · модулей ").Append(s.moduleCount)
                  .Append(" · груз-строк ").Append(s.cargoDetail.Length)
                  .Append(" · 📍 (").Append(s.position.x.ToString("0.0")).Append(", ")
                  .Append(s.position.y.ToString("0.0")).Append(", ")
                  .Append(s.position.z.ToString("0.0")).Append(')');
            }
            if (ships.Length == 0) sb.Append('\n').Append("Корабли: нет данных");

            sb.Append('\n').Append("Репутация: ").Append(factions.Length).Append(" фракц.");
            for (int i = 0; i < factions.Length; i++)
            {
                var f = factions[i];
                sb.Append('\n').Append("  ").Append(f.displayName).Append(' ').Append(f.value);
                if (f.hasTiers) sb.Append(" · ").Append(f.tier);
                else sb.Append(" · тиры не заданы");
            }

            sb.Append('\n').Append("Люди: ").Append(npcs.Length);
            for (int i = 0; i < npcs.Length; i++)
                sb.Append('\n').Append("  ").Append(npcs[i].displayName).Append(' ').Append(npcs[i].value);

            sb.Append('\n').Append(hasTime
                ? "Время: " + year + " г., " + monthName + " " + day + " (" + weekdayName + "), день года " + dayOfYear +
                  ", TimeOfDay " + timeOfDay.ToString("0.00")
                : "Время: нет данных");

            sb.Append('\n').Append(hasAltitude
                ? "Высота: " + altitude.ToString("0.0") + " м · коридор " + corridorDisplayName + " (" + corridorId + ")" +
                  " [" + corridorMin.ToString("0") + "…" + corridorMax.ToString("0") + "]" +
                  " · статус " + altitudeStatusName + " · rebase Y " + rebaseOffsetY.ToString("0.0")
                : "Высота: нет данных");

            return sb.ToString();
        }

        private static string FormatTimer(float seconds)
        {
            if (seconds <= 0f) return "∞";
            int total = Mathf.FloorToInt(seconds);
            return (total / 60) + ":" + (total % 60).ToString("00");
        }

        // ------------------------------------------------------------------ персонаж

        private void ReadCharacter()
        {
            var player = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClient?.PlayerObject : null;
            if (player == null)
            {
                _playerTarget = null;
                hasHp = false;
                hasStats = false;
            }
            else
            {
                if (_playerTarget == null) _playerTarget = player.GetComponent<PlayerTarget>();

                if (_playerTarget != null)
                {
                    currentHp = _playerTarget.GetCurrentHp();
                    maxHp = _playerTarget.GetMaxHp();
                    hasHp = maxHp > 0;
                }
                else hasHp = false;

                var stats = StatsClientState.Instance;
                var snap = stats != null ? stats.CurrentStats : null;
                if (snap.HasValue)
                {
                    var dto = snap.Value;
                    strength = dto.strength;
                    dexterity = dto.dexterity;
                    intelligence = dto.intelligence;
                    hasStats = true;
                }
                else hasStats = false;
            }

            // Кредиты — как легаси (решение пользователя №4); дубль D1 в InventorySnapshotDto.credits не читаем.
            var market = MarketClientState.Instance;
            var marketSnap = market != null ? market.CurrentSnapshot : null;
            if (marketSnap.HasValue)
            {
                credits = marketSnap.Value.credits;
                hasCredits = true;
            }
            else hasCredits = false;
        }

        // ------------------------------------------------------------------ квесты

        private void ReadQuests()
        {
            var tracker = QuestTracker.GetOrFindInstance();
            trackedQuestId = tracker != null ? tracker.TrackedQuestId : string.Empty;

            var state = QuestClientState.Instance;
            var snap = state != null ? state.CurrentSnapshot : null;
            if (!snap.HasValue)
            {
                quests = new CnQuestRow[0];
                return;
            }

            var dto = snap.Value;
            var list = dto.quests;
            if (list == null || list.Length == 0)
            {
                quests = new CnQuestRow[0];
                return;
            }

            var rows = new CnQuestRow[list.Length];
            for (int i = 0; i < list.Length; i++)
            {
                var q = list[i];
                var goals = q.objectives ?? new ProjectC.Quests.Dto.ObjectiveProgressDto[0];
                var goalRows = new CnQuestGoal[goals.Length];
                for (int g = 0; g < goals.Length; g++)
                {
                    var o = goals[g];
                    goalRows[g] = new CnQuestGoal
                    {
                        description = o.description,
                        completed = o.completed,
                        currentValue = o.currentValue,
                        requiredQuantity = o.requiredQuantity
                    };
                }

                rows[i] = new CnQuestRow
                {
                    questId = q.questId,
                    displayName = q.displayName,
                    currentStageId = q.currentStageId,
                    state = q.state,
                    // В игре слежение одно: серверное isTracked и локальный QuestTracker могут расходиться —
                    // окно показывает как легаси, по QuestTracker (03 §3).
                    isTracked = !string.IsNullOrEmpty(trackedQuestId) && trackedQuestId == q.questId,
                    goals = goalRows
                };
            }

            quests = rows;
        }

        // ------------------------------------------------------------------ контракты

        private void ReadContracts()
        {
            var state = ContractClientState.Instance;
            var snap = state != null ? state.CurrentSnapshot : null;
            if (!snap.HasValue)
            {
                contracts = new CnContractRow[0];
                canAcceptContracts = false;
                secondsUntilNextTick = 0f;
                debtAmount = 0f;
                debtLevel = 0;
                return;
            }

            var dto = snap.Value;
            canAcceptContracts = dto.canAcceptContracts;
            secondsUntilNextTick = dto.secondsUntilNextTick;
            debtAmount = dto.debtAmount;
            debtLevel = dto.debtLevel;

            var active = dto.active;
            var available = dto.available;
            if (active == null) active = new ProjectC.Trade.Dto.ContractDto[0];
            if (available == null) available = new ProjectC.Trade.Dto.ContractDto[0];

            var rows = new CnContractRow[active.Length + available.Length];
            int n = 0;
            for (int i = 0; i < active.Length; i++) rows[n++] = ProjectContract(active[i], true);
            for (int i = 0; i < available.Length; i++) rows[n++] = ProjectContract(available[i], false);
            contracts = rows;
        }

        private static CnContractRow ProjectContract(ProjectC.Trade.Dto.ContractDto c, bool isActive)
        {
            return new CnContractRow
            {
                contractId = c.contractId,
                type = c.type,
                typeLocalizationKey = c.typeLocalizationKey,
                typeUiClass = c.typeUiClass,
                state = c.state,
                itemId = c.itemId,
                displayName = c.displayName,
                quantity = c.quantity,
                fromLocationId = c.fromLocationId,
                toLocationId = c.toLocationId,
                reward = c.reward,
                cargoValue = c.cargoValue,
                timeLimit = c.timeLimit,
                timeRemaining = c.timeRemaining,
                isReceiptContract = c.isReceiptContract,
                receiptCargoIssued = c.receiptCargoIssued,
                isActive = isActive
            };
        }

        // ------------------------------------------------------------------ багаж

        private void ReadInventory()
        {
            var inv = InventoryClientState.Instance;
            var snap = inv != null ? inv.CurrentSnapshot : null;
            if (!snap.HasValue)
            {
                bagItems = new CnBagItem[0];
                bagMaxSlots = 0;
                bagLocationId = string.Empty;
                return;
            }

            var dto = snap.Value;
            bagMaxSlots = dto.maxSlots;
            bagLocationId = dto.locationId ?? string.Empty;

            var items = dto.items;
            if (items == null || items.Length == 0)
            {
                bagItems = new CnBagItem[0];
                return;
            }

            var rows = new CnBagItem[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                var it = items[i];
                var def = inv.GetItemDefinition(it.itemId);
                rows[i] = new CnBagItem
                {
                    itemId = it.itemId,
                    type = it.type,
                    quantity = it.quantity,
                    slotIndex = it.slotIndex,
                    instanceId = it.instanceId,
                    itemName = string.IsNullOrEmpty(it.itemName) && def != null ? def.itemName : it.itemName,
                    weightKg = def != null ? def.weightKg : 0f   // Derived: вес из ItemData, у снапшота его нет
                };
            }

            bagItems = rows;
        }

        // ------------------------------------------------------------------ корабли и высота

        private void ReadShips()
        {
            var controllers = CollectShipControllers();
            if (controllers.Count == 0)
            {
                ships = new CnShipRow[0];
                return;
            }

            var rows = new CnShipRow[controllers.Count];
            for (int i = 0; i < controllers.Count; i++)
            {
                var sc = controllers[i];
                var t = sc.TelemetryState;

                var moduleRows = new CnModuleRow[0];
                var mm = sc.ShipModuleManager;
                if (mm != null && mm.slots != null && mm.slots.Count > 0)
                {
                    moduleRows = new CnModuleRow[mm.slots.Count];
                    for (int s = 0; s < mm.slots.Count; s++)
                    {
                        var slot = mm.slots[s];
                        var mod = slot.installedModule;
                        moduleRows[s] = new CnModuleRow
                        {
                            slotType = slot.slotType.ToString(),
                            isOccupied = slot.isOccupied,
                            moduleId = mod != null ? mod.moduleId : string.Empty,
                            displayName = mod != null ? mod.displayName : string.Empty,
                            powerConsumption = mod != null ? mod.powerConsumption : 0,
                            cargoSlotsBonus = mod != null ? mod.cargoSlotsBonus : 0
                        };
                    }
                }

                var cargoRows = new CnCargoRow[0];
                // D5: легаси MyShipsTab читает детали из TelemetryCargoState.cargoDetail — берём тот же канал,
                // чтобы значения совпали при сверке. Обязательный (Obsolete) broadcast-кэш не используется.
                var cargo = sc.TelemetryCargoState.cargoDetail;
                if (cargo != null && cargo.Length > 0)
                {
                    cargoRows = new CnCargoRow[cargo.Length];
                    for (int c = 0; c < cargo.Length; c++)
                    {
                        var e = cargo[c];
                        cargoRows[c] = new CnCargoRow
                        {
                            itemId = e.itemId,
                            displayName = e.displayName.ToString(),
                            quantity = e.quantity,
                            unitWeight = e.unitWeight,
                            flags = e.flags,
                            dangerous = (e.flags & 0x01) != 0,
                            fragile = (e.flags & 0x02) != 0
                        };
                    }
                }

                string name = t.displayName.ToString();
                if (string.IsNullOrWhiteSpace(name)) name = sc.gameObject.name;

                rows[i] = new CnShipRow
                {
                    shipNetworkObjectId = t.shipNetworkObjectId,
                    keyInstanceId = t.keyInstanceId,
                    displayName = name,
                    className = t.className.ToString(),
                    position = t.position,
                    fuelNormalized = t.fuelNormalized,
                    fuelMax = t.fuelMax,
                    hullCurrent = t.hullCurrent,
                    hullMax = t.hullMax,
                    cargoUsed = t.cargoUsed,
                    cargoMax = t.cargoMax,
                    moduleCount = t.moduleCount,
                    isEngineRunning = sc.IsEngineRunning,
                    modules = moduleRows,
                    cargoDetail = cargoRows
                };
            }

            ships = rows;
        }

        /// <summary>
        /// Список кораблей локального игрока — тот же порядок поиска, что в легаси MyShipsTab.RefreshShipList:
        /// хост → InventoryWorld.GetMyShips (серверные данные), клиент → Key-предметы через KeyRodInstanceWorld.
        /// </summary>
        private List<ShipController> CollectShipControllers()
        {
            var result = new List<ShipController>(4);

            var nm = NetworkManager.Singleton;
            if (nm == null || nm.SpawnManager == null) return result;

            ulong myId = nm.LocalClientId;

            var invWorld = InventoryWorld.Instance;
            if (invWorld != null)
            {
                var myShips = invWorld.GetMyShips(myId);
                if (myShips != null)
                {
                    foreach (var (_, shipNetId) in myShips)
                    {
                        if (!nm.SpawnManager.SpawnedObjects.TryGetValue(shipNetId, out var netObj)) continue;
                        var sc = netObj.GetComponent<ShipController>();
                        if (sc != null) result.Add(sc);
                    }
                }
            }

            if (result.Count > 0) return result;

            var inv = InventoryClientState.Instance;
            var snap = inv != null ? inv.CurrentSnapshot : null;
            if (!snap.HasValue) return result;

            var items = snap.Value.items;
            if (items == null || !KeyRodInstanceWorld.IsInitialized) return result;

            for (int i = 0; i < items.Length; i++)
            {
                var it = items[i];
                if ((ItemType)it.type != ItemType.Key) continue;
                if (it.instanceId <= 0) continue;

                var inst = KeyRodInstanceWorld.GetInstance(it.instanceId);
                if (inst == null || inst.state != KeyRodInstanceState.Active || inst.registeredShipId == 0) continue;
                if (!nm.SpawnManager.SpawnedObjects.TryGetValue(inst.registeredShipId, out var netObj)) continue;

                var sc = netObj.GetComponent<ShipController>();
                if (sc != null) result.Add(sc);
            }

            return result;
        }

        private void ReadAltitude()
        {
            var sys = AltitudeCorridorSystem.Instance;
            if (sys == null)
            {
                hasAltitude = false;
                return;
            }

            // Позиция — как AltitudeUI: у корабля; если корабля ещё нет, у локального игрока.
            Vector3 position;
            if (ships.Length > 0) position = ships[0].position;
            else
            {
                var player = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClient?.PlayerObject : null;
                if (player == null)
                {
                    hasAltitude = false;
                    return;
                }
                position = player.transform.position;
            }

            altitude = position.y;
            // Floating origin: сырое значение совпадает с легаси-индикатором; сдвиг логируем отдельно как диагностику,
            // решение «показывать абсолютную высоту» — отдельная задача (03 §1, D16-сосед).
            rebaseOffsetY = ProjectC.World.FloatingOrigin.Network.GlobalMotionControlledRebaseSlice.CumulativeRebaseOffset.y;

            var corridor = sys.GetActiveCorridor(position);
            if (corridor == null)
            {
                hasAltitude = true;
                corridorId = string.Empty;
                corridorDisplayName = string.Empty;
                altitudeStatusName = "нет коридора";
                altitudeStatusValue = -1;
                return;
            }

            corridorId = corridor.corridorId;
            corridorDisplayName = corridor.displayName;
            corridorMin = corridor.minAltitude;
            corridorMax = corridor.maxAltitude;
            corridorWarningMargin = corridor.warningMargin;
            corridorCriticalUpperMargin = corridor.criticalUpperMargin;
            corridorIsGlobal = corridor.isGlobal;

            var status = sys.ValidateAltitude(position, corridor);
            altitudeStatusValue = (int)status;
            altitudeStatusName = status.ToString();
            hasAltitude = true;
        }

        // ------------------------------------------------------------------ репутация и люди

        private void ReadRelations()
        {
            var rep = ReputationClientState.Instance;
            var repSnap = rep != null ? rep.CurrentReputation : null;
            if (repSnap.HasValue)
            {
                var dto = repSnap.Value;
                var entries = dto.entries;
                if (entries == null || entries.Length == 0)
                {
                    factions = new CnFactionRow[0];
                }
                else
                {
                    var rows = new CnFactionRow[entries.Length];
                    for (int i = 0; i < entries.Length; i++)
                    {
                        var e = entries[i];
                        var def = FactionDef(e.faction);
                        bool hasTiers = def != null && def.reputationThresholds != null && def.reputationThresholds.Length > 0;

                        var row = new CnFactionRow
                        {
                            wireId = e.faction,
                            displayName = def != null && !string.IsNullOrEmpty(def.displayName)
                                ? def.displayName
                                : "фракция " + e.faction,
                            value = e.value,
                            hasTiers = hasTiers
                        };

                        if (hasTiers)
                        {
                            var tier = def.GetTier(e.value);
                            if (tier != null)
                            {
                                row.tier = tier.tier;
                                row.ussClass = tier.ussClass;
                                row.tierColor = tier.color;
                            }
                        }

                        rows[i] = row;
                    }
                    factions = rows;
                }
            }
            else factions = new CnFactionRow[0];

            var att = NpcAttitudeClientState.Instance;
            var attSnap = att != null ? att.CurrentNpcAttitude : null;
            if (!attSnap.HasValue)
            {
                npcs = new CnNpcRow[0];
                return;
            }

            var entriesNpc = attSnap.Value.entries;
            if (entriesNpc == null || entriesNpc.Length == 0)
            {
                npcs = new CnNpcRow[0];
                return;
            }

            var npcRows = new CnNpcRow[entriesNpc.Length];
            for (int i = 0; i < entriesNpc.Length; i++)
            {
                var e = entriesNpc[i];
                npcRows[i] = new CnNpcRow
                {
                    npcId = e.npcId,
                    displayName = ResolveNpcName(e.npcId),
                    value = e.value
                };
            }
            npcs = npcRows;
        }

        // ------------------------------------------------------------------ граф связей

        private string _graphSignature = string.Empty;

        /// <summary>
        /// Граф связей. Узлы — только видимые субъекты: фракции и люди из снапшотов сервера (он присылает лишь
        /// известные) и задания из QuestClientState. Рёбра — реальные поля ассетов: NpcDefinition.factionRef и
        /// attitudeLinks, FactionDefinition.combatRelations, GetQuestOfferIds/GetQuestTurnInIds,
        /// QuestDefinition.factionRef и rewards.reputation, QuestPrerequisite.
        /// Неизвестные соседи не рисуются даже точкой «?»: их существование — тоже знание, которого у клиента нет.
        /// Каталог NPC/квестов — QuestWorld.Database (публичный путь host-played сессии); без него остаются
        /// рёбра между известными фракциями, которые целиком лежат в Resources/Data/Factions.
        /// Определения фракций — FactionDef(): сначала каталог игры, а если он ещё не создан (в редакторе его нет) —
        /// те же ассеты из Resources/Data/Factions, иначе имена, тиры и боевые отношения терялись бы целиком.
        /// </summary>
        private void ReadGraph()
        {
            EnsureQuestDatabase();

            string signature = GraphSignature();
            if (signature == _graphSignature) return;
            _graphSignature = signature;

            var factionRows = factions ?? new CnFactionRow[0];
            var npcRows = npcs ?? new CnNpcRow[0];
            var questRows = quests ?? new CnQuestRow[0];

            var nodes = new List<CnGraphNode>(factionRows.Length + npcRows.Length + questRows.Length);
            var factionByWire = new Dictionary<int, string>(factionRows.Length);
            var npcKeyById = new Dictionary<string, string>(npcRows.Length);
            var questKeyById = new Dictionary<string, string>(questRows.Length);

            for (int i = 0; i < factionRows.Length; i++)
            {
                string key = "f:" + factionRows[i].wireId;
                factionByWire[factionRows[i].wireId] = key;
                nodes.Add(new CnGraphNode
                {
                    key = key,
                    kind = 0,
                    name = factionRows[i].displayName,
                    value = factionRows[i].value
                });
            }

            for (int i = 0; i < npcRows.Length; i++)
            {
                if (string.IsNullOrEmpty(npcRows[i].npcId)) continue;
                string key = "n:" + npcRows[i].npcId;
                npcKeyById[npcRows[i].npcId] = key;
                nodes.Add(new CnGraphNode
                {
                    key = key,
                    kind = 1,
                    // Имя резолвится тем же путём, что и строка панели: displayName у NpcDefinition — ключ локализации.
                    name = ResolveNpcName(npcRows[i].npcId),
                    value = npcRows[i].value
                });
            }

            for (int i = 0; i < questRows.Length; i++)
            {
                if (string.IsNullOrEmpty(questRows[i].questId)) continue;
                string key = "q:" + questRows[i].questId;
                questKeyById[questRows[i].questId] = key;
                nodes.Add(new CnGraphNode
                {
                    key = key,
                    kind = 2,
                    name = questRows[i].displayName,
                    value = 0
                });
            }

            var edges = new List<CnGraphEdge>(nodes.Count * 2);

            void Add(string from, string to, byte type, string label, bool negative = false)
            {
                if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to) || from == to) return;
                edges.Add(new CnGraphEdge { from = from, to = to, type = type, label = label, negative = negative });
            }

            // 1 и 2: принадлежность NPC фракции и его поведение (AttitudeLink) — только по видимым фракциям.
            for (int i = 0; i < npcRows.Length; i++)
            {
                string npcKey = npcKeyById.TryGetValue(npcRows[i].npcId, out var nk) ? nk : null;
                if (npcKey == null) continue;

                var def = _questDatabase != null ? _questDatabase.GetNpc(npcRows[i].npcId) : null;
                if (def == null) continue;

                // Подпись — глагол связи, а не имя NPC: displayName у NpcDefinition это ключ локализации.
                if (factionByWire.TryGetValue(def.EffectiveFactionWireId, out var ownFaction))
                    Add(npcKey, ownFaction, 1, "состоит в");

                var links = def.attitudeLinks;
                if (links == null) continue;

                for (int k = 0; k < links.Length; k++)
                {
                    var link = links[k];
                    if (!factionByWire.TryGetValue(link.EffectiveFactionWireId, out var target)) continue;

                    Add(npcKey, target, 2,
                        "нравится: " + Signed(link.deltaOnLike) + " · не нравится: " + Signed(link.deltaOnDislike),
                        link.deltaOnLike < 0);
                }
            }

            // 3: вражда и союз между известными фракциями. Нейтралитет не рисуется — это не связь, а её отсутствие.
            for (int i = 0; i < factionRows.Length; i++)
            {
                int wire = factionRows[i].wireId;
                if (!factionByWire.TryGetValue(wire, out var from)) continue;

                var def = FactionDef(wire);
                if (def == null || def.combatRelations == null) continue;

                for (int k = 0; k < def.combatRelations.Length; k++)
                {
                    var rel = def.combatRelations[k];
                    if (rel.relation == FactionRelation.Neutral) continue;
                    if (!factionByWire.TryGetValue(rel.EffectiveTargetWireId, out var to)) continue;

                    bool hostile = rel.relation == FactionRelation.Hostile;
                    Add(from, to, 3, hostile ? "вражда" : "союз", hostile);
                }
            }

            // 4: кто предлагает задание и кто принимает сдачу — у мода один тип с двумя глаголами.
            for (int i = 0; i < npcRows.Length; i++)
            {
                if (!npcKeyById.TryGetValue(npcRows[i].npcId, out var npcKey)) continue;

                var def = _questDatabase != null ? _questDatabase.GetNpc(npcRows[i].npcId) : null;
                if (def == null) continue;

                var offers = def.GetQuestOfferIds();
                if (offers != null)
                {
                    for (int k = 0; k < offers.Length; k++)
                        if (questKeyById.TryGetValue(offers[k], out var q)) Add(npcKey, q, 4, "выдаёт");
                }

                var turnIns = def.GetQuestTurnInIds();
                if (turnIns == null) continue;

                for (int k = 0; k < turnIns.Length; k++)
                    if (questKeyById.TryGetValue(turnIns[k], out var q)) Add(npcKey, q, 4, "принимает");
            }

            // 1, 5 и 6: фракция задания, награда репутацией и предпосылки — всё из QuestDefinition.
            for (int i = 0; i < questRows.Length; i++)
            {
                if (!questKeyById.TryGetValue(questRows[i].questId, out var questKey)) continue;

                var def = _questDatabase != null ? _questDatabase.GetQuest(questRows[i].questId) : null;
                if (def == null) continue;

                if (factionByWire.TryGetValue(def.EffectiveFactionWireId, out var ownFaction))
                    Add(questKey, ownFaction, 1, "состоит в");

                var reputation = def.rewards != null ? def.rewards.reputation : null;
                if (reputation != null)
                {
                    for (int k = 0; k < reputation.Length; k++)
                    {
                        if (!factionByWire.TryGetValue(reputation[k].EffectiveFactionWireId, out var target)) continue;
                        Add(questKey, target, 5, "награда " + Signed(reputation[k].value), reputation[k].value < 0);
                    }
                }

                var prerequisites = def.prerequisites;
                if (prerequisites == null) continue;

                for (int k = 0; k < prerequisites.Length; k++)
                {
                    var p = prerequisites[k];

                    if (p.type == QuestPrerequisiteType.QuestCompleted)
                    {
                        // Условие авторится либо ссылкой, либо строкой (CSV) — обе формы реальны.
                        string needed = p.requiredQuest != null ? p.requiredQuest.questId : p.stringParam;
                        if (!string.IsNullOrEmpty(needed) && questKeyById.TryGetValue(needed, out var need))
                            Add(questKey, need, 6, "требует задание");
                    }
                    else if (p.type == QuestPrerequisiteType.ReputationAtLeast)
                    {
                        if (factionByWire.TryGetValue(p.EffectiveFactionWireId, out var target))
                            Add(questKey, target, 6, "нужна репутация ≥ " + p.intParam);
                    }
                    else if (p.type == QuestPrerequisiteType.NpcAttitudeAtLeast)
                    {
                        string npcId = p.requiredNpc != null ? p.requiredNpc.npcId : p.stringParam;
                        if (!string.IsNullOrEmpty(npcId) && npcKeyById.TryGetValue(npcId, out var npcKey))
                            Add(questKey, npcKey, 6, "нужно отношение ≥ " + p.intParam);
                    }
                    // HaveItem / FlagIsSet / PlayerFaction — не субъекты графа: рисовать нечего, поэтому пропуск.
                }
            }

            graphNodes = nodes.ToArray();
            graphEdges = edges.ToArray();
        }

        /// <summary>
        /// Определение фракции по wireId. Сначала каталог игры (<c>FactionCatalog.Instance</c> создаёт
        /// <c>NetworkManagerController</c> на клиенте), затем те же ассеты напрямую из Resources/Data/Factions:
        /// окно — читающий слой и не должно терять имена, тиры и боевые отношения, если каталог ещё не
        /// сконструирован (в редакторе Instance = null, проверено).
        /// </summary>
        private FactionDefinition FactionDef(int wire)
        {
            var catalog = FactionCatalog.Instance;
            if (catalog != null)
            {
                var fromCatalog = catalog.GetByWireId(wire);
                if (fromCatalog != null) return fromCatalog;
            }

            if (_factionDefs == null)
            {
                _factionDefs = new Dictionary<int, FactionDefinition>();
                var all = Resources.LoadAll<FactionDefinition>("Data/Factions");
                for (int i = 0; i < all.Length; i++)
                {
                    var def = all[i];
                    if (def == null || _factionDefs.ContainsKey(def.EffectiveWireId)) continue;
                    _factionDefs[def.EffectiveWireId] = def;
                }
            }

            return _factionDefs.TryGetValue(wire, out var found) ? found : null;
        }

        /// <summary>Знак для подписи ребра: «+3», «−1», «0». Минус типографский — как в остальных подписях окна.</summary>
        private static string Signed(int value)
        {
            if (value > 0) return "+" + value;
            if (value < 0) return "−" + Mathf.Abs(value);
            return "0";
        }

        /// <summary>Пересборка только по смене видимого знания или появлению каталога — не каждый кадр.</summary>
        private string GraphSignature()
        {
            var sb = new StringBuilder(256);
            sb.Append(_questDatabase != null ? 'D' : '-');

            var factionRows = factions;
            if (factionRows != null)
                for (int i = 0; i < factionRows.Length; i++)
                    sb.Append('f').Append(factionRows[i].wireId).Append(factionRows[i].value).Append(';');

            var npcRows = npcs;
            if (npcRows != null)
                for (int i = 0; i < npcRows.Length; i++)
                    sb.Append('n').Append(npcRows[i].npcId).Append(npcRows[i].value).Append(';');

            var questRows = quests;
            if (questRows != null)
                for (int i = 0; i < questRows.Length; i++)
                    sb.Append('q').Append(questRows[i].questId).Append(questRows[i].state).Append(';');

            return sb.ToString();
        }

        private string ResolveNpcName(string npcId)
        {
            if (string.IsNullOrEmpty(npcId)) return string.Empty;

            EnsureQuestDatabase();
            if (_questDatabase != null)
            {
                string name = NpcName(_questDatabase.GetNpc(npcId));
                if (!string.IsNullOrEmpty(name)) return name;
            }

            return npcId; // имени нет — показываем id, а не выдуманную подпись
        }

        /// <summary>
        /// Имя NPC. <c>NpcDefinition.displayName</c> — ключ локализации («loc key в будущем», NpcDefinition.cs:71),
        /// поэтому Loc.Get; если перевода нет, Loc возвращает фолбэк — npcId, а не сам ключ.
        /// </summary>
        private static string NpcName(NpcDefinition def)
        {
            if (def == null) return string.Empty;

            string name = Loc.Get(def.displayName, def.npcId);
            if (!string.IsNullOrWhiteSpace(name)) return name;
            return def.npcId;
        }

        /// <summary>
        /// Кто принимает сдачу квеста — по <c>NpcDefinition.GetQuestTurnInIds()</c>. Окно не сдаёт квест само
        /// (сдача идёт через диалог, 02 §7), поэтому ему нужна только честная подпись «сдать у {NPC}».
        /// Пустая строка — такого NPC в каталоге нет; подпись тогда не показывается.
        /// </summary>
        public string ResolveTurnInNpc(string questId)
        {
            if (string.IsNullOrEmpty(questId)) return string.Empty;

            EnsureQuestDatabase();
            var npcs = _questDatabase != null ? _questDatabase.npcs : null;
            if (npcs == null) return string.Empty;

            for (int i = 0; i < npcs.Length; i++)
            {
                var npc = npcs[i];
                if (npc == null) continue;

                var turnIns = npc.GetQuestTurnInIds();
                if (turnIns == null) continue;

                for (int k = 0; k < turnIns.Length; k++)
                {
                    if (turnIns[k] != questId) continue;
                    return NpcName(npc);
                }
            }

            return string.Empty;
        }

        /// <summary>Каталог NPC/квестов ищется в сцене один раз и только если он там есть.</summary>
        private void EnsureQuestDatabase()
        {
            if (_questDatabaseSearched) return;
            _questDatabaseSearched = true;

            // QuestWorld.Database — публичный путь к каталогу, которым инициализирован сервер: на host-played
            // сессии он есть. FindAnyObjectByType<QuestDatabase>() оставлен запасом: он ищет объекты сцены,
            // а QuestDatabase — ассет, поэтому в редакторе даёт null (проверено пробой).
            var world = QuestWorld.Instance;
            if (world != null) _questDatabase = world.Database;
            if (_questDatabase == null) _questDatabase = UnityEngine.Object.FindAnyObjectByType<ProjectC.Quests.QuestDatabase>();
        }

        // ------------------------------------------------------------------ навыки

        /// <summary>Слоты в порядке показа — как в легаси-панели: ЛКМ, ПКМ, затем 1…4.</summary>
        private static readonly SkillInputSlot[] SkillSlotOrder =
        {
            SkillInputSlot.Primary, SkillInputSlot.Secondary,
            SkillInputSlot.Slot1, SkillInputSlot.Slot2, SkillInputSlot.Slot3, SkillInputSlot.Slot4
        };

        /// <summary>
        /// Фолбэк подписей слотов — дефолты <c>InputBindingsConfig.combatSkills</c> (01 §2): слоты висят на мыши
        /// с модификатором, а не на цифрах. Живые подписи берутся из <c>InputBindingsRuntime.GetSkillDisplayName</c>.
        /// </summary>
        private static readonly string[] SkillSlotCodes = { "ЛКМ", "ПКМ", "Ctrl+ЛКМ", "Ctrl+ПКМ", "Shift+ЛКМ", "Shift+ПКМ" };

        /// <summary>
        /// Навыки: шесть слотов (только выученное можно забиндить, поэтому слот = реальный биндинг
        /// <c>SkillInputService.GetSkillForSlot</c>) и весь каталог для графов. Числа выученных/известных — из
        /// <c>SkillsClientState</c>; своего состояния окно не вводит.
        /// </summary>
        private void ReadSkills()
        {
            var state = SkillsClientState.Instance;
            var input = SkillInputService.Instance;

            learnedSkillCount = state != null ? state.CurrentSkills.Count : 0;
            knownSkillCount = state != null ? state.KnownSkillIds.Count : 0;

            EnsureSkillCatalog();
            hasSkills = _skillCatalog != null && _skillCatalog.Length > 0;

            var learned = state != null ? state.CurrentSkills : null;
            var known = state != null ? state.KnownSkillIds : null;

            ReadSkillSlots(input, learned, known);

            // Узлов столько же, сколько ассетов навыков, и меняются они только новым снапшотом —
            // пересобираем по смене набора, а не каждый кадр (семь слотов читаются всегда).
            if (_skillNodesBuilt && ReferenceEquals(learned, _skillNodesSigLearned) &&
                ReferenceEquals(known, _skillNodesSigKnown)) return;

            _skillNodesBuilt = true;
            _skillNodesSigLearned = learned;
            _skillNodesSigKnown = known;
            ReadSkillNodes(learned, known);
        }

        private void ReadSkillSlots(SkillInputService input, HashSet<string> learned, HashSet<string> known)
        {
            if (skillSlots == null || skillSlots.Length != SkillSlotOrder.Length)
                skillSlots = new CnSkillSlotRow[SkillSlotOrder.Length];

            for (int i = 0; i < SkillSlotOrder.Length; i++)
            {
                var slot = SkillSlotOrder[i];
                string skillId = input != null ? input.GetSkillForSlot(slot) : string.Empty;
                if (skillId == null) skillId = string.Empty;

                bool bound = skillId.Length > 0;
                var cfg = bound ? FindSkill(skillId) : null;

                skillSlots[i] = new CnSkillSlotRow
                {
                    slot = (byte)slot,
                    code = SkillSlotCode(i, slot),
                    skillId = skillId,
                    displayName = cfg != null ? SkillName(cfg) : string.Empty,
                    description = cfg != null ? SkillDescription(cfg) : string.Empty,
                    bound = bound,
                    onCooldown = bound && input != null && input.IsOnCooldown(slot),
                    known = bound && (IsIn(learned, skillId) || IsIn(known, skillId))
                };
            }
        }

        /// <summary>Подпись слота: переназначенная в игре, иначе дефолт биндинга. Своих подписей окно не держит.</summary>
        private static string SkillSlotCode(int index, SkillInputSlot slot)
        {
            var runtime = ProjectC.Input.InputBindingsRuntime.Instance;
            if (runtime != null)
            {
                string label = runtime.GetSkillDisplayName(slot);
                if (!string.IsNullOrEmpty(label)) return label;
            }

            return index < SkillSlotCodes.Length ? SkillSlotCodes[index] : slot.ToString();
        }

        private void ReadSkillNodes(HashSet<string> learned, HashSet<string> known)
        {
            if (_skillCatalog == null || _skillCatalog.Length == 0)
            {
                skillNodes = new CnSkillNodeRow[0];
                return;
            }

            // Порядок как в легаси SkillTreeWindow: узлы без позиции (0,0) — в конце.
            var ordered = new List<SkillNodeConfig>(_skillCatalog);
            ordered.Sort((a, b) =>
            {
                int yA = (a.treeY == 0 && a.treeX == 0) ? int.MaxValue : a.treeY;
                int yB = (b.treeY == 0 && b.treeX == 0) ? int.MaxValue : b.treeY;
                if (yA != yB) return yA.CompareTo(yB);
                if (a.treeX != b.treeX) return a.treeX.CompareTo(b.treeX);
                return string.CompareOrdinal(a.skillId, b.skillId);
            });

            var rows = new CnSkillNodeRow[ordered.Count];
            for (int i = 0; i < ordered.Count; i++)
            {
                var cfg = ordered[i];
                var pre = cfg.prerequisites;
                var preIds = new string[0];
                if (pre != null && pre.Length > 0)
                {
                    var buf = new List<string>(pre.Length);
                    for (int p = 0; p < pre.Length; p++)
                        if (pre[p] != null && !string.IsNullOrEmpty(pre[p].skillId)) buf.Add(pre[p].skillId);
                    preIds = buf.ToArray();
                }

                rows[i] = new CnSkillNodeRow
                {
                    skillId = cfg.skillId,
                    displayName = SkillName(cfg),
                    description = SkillDescription(cfg),
                    category = (byte)cfg.category,
                    treeX = cfg.treeX,
                    treeY = cfg.treeY,
                    learned = IsIn(learned, cfg.skillId),
                    known = IsIn(learned, cfg.skillId) || IsIn(known, cfg.skillId),
                    alwaysVisible = cfg.knowledgeUnlockType == KnowledgeUnlockType.AlwaysVisible,
                    active = cfg.isActive,
                    cooldown = cfg.cooldownSeconds,
                    learnXp = cfg.LearnXpCost,
                    requiredStrength = cfg.RequiredStrengthTier,
                    requiredDexterity = cfg.RequiredDexterityTier,
                    requiredIntelligence = cfg.RequiredIntelligenceTier,
                    prerequisites = preIds,
                    canLearn = CanLearn(cfg.skillId, preIds, learned)
                };
            }

            skillNodes = rows;
        }

        /// <summary>
        /// Ровно гейт легаси: выученное не учится повторно, а новое требует все предпосылки выученными.
        /// Уровневые требования (Required*Tier) в легаси-гейте не участвуют — они проверяются сервером.
        /// </summary>
        private static bool CanLearn(string skillId, string[] prerequisites, HashSet<string> learned)
        {
            if (IsIn(learned, skillId)) return false;
            if (prerequisites == null) return true;

            for (int i = 0; i < prerequisites.Length; i++)
                if (!IsIn(learned, prerequisites[i])) return false;

            return true;
        }

        private static bool IsIn(HashSet<string> set, string skillId)
        {
            return set != null && !string.IsNullOrEmpty(skillId) && set.Contains(skillId);
        }

        /// <summary>
        /// Каталог навыков из <c>Resources/Skills</c> — тот же путь, что у <c>SkillsClientState</c> и легаси-дерева.
        /// Кэш свой (а не <c>TryGetSkillConfig</c>), потому что графу нужны ВСЕ узлы, включая неизвестные и невыученные.
        /// </summary>
        private void EnsureSkillCatalog()
        {
            if (_skillCatalog != null) return;

            var all = Resources.LoadAll<SkillNodeConfig>("Skills");
            var list = new List<SkillNodeConfig>(all.Length);
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && !string.IsNullOrEmpty(all[i].skillId)) list.Add(all[i]);

            _skillCatalog = list.ToArray();
            _skillById = new Dictionary<string, SkillNodeConfig>(_skillCatalog.Length);
            for (int i = 0; i < _skillCatalog.Length; i++) _skillById[_skillCatalog[i].skillId] = _skillCatalog[i];
        }

        private SkillNodeConfig FindSkill(string skillId)
        {
            if (_skillById == null || string.IsNullOrEmpty(skillId)) return null;
            return _skillById.TryGetValue(skillId, out var cfg) ? cfg : null;
        }

        /// <summary>Имя навыка — тем же ключом локализации, что легаси (static.skill.{id}.displayName).</summary>
        private static string SkillName(SkillNodeConfig cfg)
        {
            string fallback = string.IsNullOrEmpty(cfg.displayName) ? cfg.skillId : cfg.displayName;
            return Loc.Get($"static.skill.{cfg.skillId}.displayName", fallback);
        }

        private static string SkillDescription(SkillNodeConfig cfg)
        {
            string fallback = string.IsNullOrEmpty(cfg.description)
                ? Loc.Get("ui.skill.no_description")
                : cfg.description;
            return Loc.Get($"static.skill.{cfg.skillId}.description", fallback);
        }

        // ------------------------------------------------------------------ время

        /// <summary>
        /// Экипировка (D19): <c>EquipmentClientState.CurrentSnapshot.equip</c> → имя предмета из <c>ItemData.itemName</c>.
        /// Индекс массива — <c>EquipmentData.SlotToIndex</c> (Head..WeaponOff → 0..9, Module1..3 → 10..12). Пустая строка — слот пуст.
        /// </summary>
        private void ReadEquipment()
        {
            if (equipNames == null || equipNames.Length != EquipmentData.SLOT_COUNT)
                equipNames = new string[EquipmentData.SLOT_COUNT];

            var state = EquipmentClientState.Instance;
            var snapshot = state != null ? state.CurrentSnapshot : null;

            // TryGetItemId индексирует массивы без проверки на null: до первого серверного снапшота они пустые.
            bool valid = snapshot.HasValue && snapshot.Value.equip.slotOccupied != null &&
                         snapshot.Value.equip.slotItemIds != null &&
                         snapshot.Value.equip.slotOccupied.Length >= EquipmentData.SLOT_COUNT &&
                         snapshot.Value.equip.slotItemIds.Length >= EquipmentData.SLOT_COUNT;
            hasEquipment = valid;

            var inv = InventoryClientState.Instance;
            for (int i = 0; i < EquipmentData.SLOT_COUNT; i++)
            {
                string name = string.Empty;
                if (valid && snapshot.Value.equip.TryGetItemId(EquipmentData.IndexToSlot(i), out int itemId))
                {
                    var def = inv != null ? inv.GetItemDefinition(itemId) : null;
                    name = def != null && !string.IsNullOrEmpty(def.itemName) ? def.itemName : "#" + itemId;
                }

                equipNames[i] = name;
            }
        }

        private void ReadTime()
        {
            // Instance выставляется ТОЛЬКО на сервере (ServerWeatherController.OnNetworkSpawn: if (IsServer)
            // Instance = this, иначе enabled = false и ранний выход). На клиенте синглтон пуст, хотя сам компонент
            // в сцене есть и получает время ClientRpc-рассылкой: BroadcastCalendarClientRpc / BroadcastTimeOfDayClientRpc
            // пишут прямо в его поля _gameTime и _timeOfDay (ServerWeatherController.cs:370, 377).
            // Поэтому при пустом Instance берём тот же компонент из сцены: без этого запасного пути на клиенте
            // hasTime = false и весь календарь показывал «время игры недоступно».
            var weather = ServerWeatherController.Instance;
            if (weather == null) weather = Object.FindFirstObjectByType<ServerWeatherController>();

            if (weather == null)
            {
                hasTime = false;
                hasCalendarRules = false;
                return;
            }

            var t = weather.CurrentGameTime;
            year = t.Year;
            month = t.Month;
            day = t.Day;
            dayOfYear = t.DayOfYear;
            weekday = t.Weekday;
            weekdayName = weather.GetWeekdayName(t.Weekday);
            monthName = weather.GetMonthName(t.Month);
            timeOfDay = weather.TimeOfDay;

            // Подписи недели для прибора-календаря станции — те же имена игры, неделя 7 дней (CalendarConfig).
            if (weekdayNames == null || weekdayNames.Length != DaysPerWeek) weekdayNames = new string[DaysPerWeek];
            for (int i = 0; i < DaysPerWeek; i++) weekdayNames[i] = weather.GetWeekdayName(i);

            // Правила года и имена всех месяцев — для полного календаря; своего календаря окно не держит.
            var cfg = weather.CalendarConfig;
            if (cfg != null)
            {
                hasCalendarRules = true;
                daysPerMonth = cfg.daysPerMonth;
                monthsPerYear = cfg.monthsPerYear;
                dayCycleRealHours = weather.DayCycleRealHours;

                if (monthNames == null || monthNames.Length != monthsPerYear) monthNames = new string[monthsPerYear];
                for (int i = 0; i < monthsPerYear; i++) monthNames[i] = weather.GetMonthName(i + 1);
            }
            else
            {
                hasCalendarRules = false;
            }

            hasTime = true;
        }
    }
}
