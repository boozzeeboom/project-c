// T-MERCH80: editor-only generator for 80 named PRIMUM merchants.
// Creates NpcDefinition + NpcMerchantTable + DialogTree assets, registers them in
// QuestDatabase, places instances of the canonical [Mira] prefab under
// WorldScene_0_0/NPC_PRIMUM_MERCHANTS, and bakes the Floating Origin catalog.
// Run through Aura execute_script: Preview / CreateAssets / PlaceInScene / BakeFo.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using ProjectC.Items;
using ProjectC.Equipment;
using ProjectC.Factions;
using ProjectC.AI;
using ProjectC.Quests;
using ProjectC.Dialogue;
using ProjectC.World.FloatingOrigin.Network;
using ProjectC.EditorTools.FloatingOrigin;

namespace ProjectC.EditorTools.Merchants
{
    public static class MerchantPrimumn80Generator
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/NPC/[Mira] - DON`T DELETE DEFOULT.prefab";
        private const string WorldPath = "Assets/_Project/Scenes/World/WorldScene_0_0.unity";
        private const string DatabasePath = "Assets/_Project/Quests/Data/QuestDatabase.asset";
        private const string DefDir = "Assets/_Project/Quests/Data/Npcs/Merchants";
        private const string TableDir = "Assets/_Project/Quests/Data/Merchants";
        private const string TreeDir = "Assets/_Project/Quests/Data/Dialogs/Merchants";
        private const string FactionPath = "Assets/_Project/Resources/Data/Factions/FreeTraders.asset";
        private const string GroupObjectName = "NPC_PRIMUM_MERCHANTS";
        private const string CatalogPath = "Assets/_Project/Prefabs/FloatingOrigin/GlobalMotionPilotSceneCatalog.asset";
        private const string ProfilePath = "Assets/_Project/Prefabs/FloatingOrigin/GlobalMotionPilotProfile.asset";

        private sealed class MerchantSpec
        {
            public readonly string Group;
            public readonly string First;
            public readonly string Last;
            public readonly string Greeting;

            public MerchantSpec(string group, string first, string last, string greeting)
            {
                Group = group; First = first; Last = last; Greeting = greeting;
            }

            public string FullName { get { return First + " " + Last; } }
        }

        private sealed class GroupMeta
        {
            public readonly string Title;
            public readonly string[] Browse;
            public readonly string[] Haggle;

            public GroupMeta(string title, string[] browse, string[] haggle)
            {
                Title = title; Browse = browse; Haggle = haggle;
            }
        }

        // ── 80 named merchants: group, first name, last name, personal greeting ──
        private static readonly MerchantSpec[] Specs = new MerchantSpec[]
        {
            // Бакалея / продукты (11)
            new MerchantSpec("grocery", "Юрий", "Ткач", "Заходи. Хлеб сегодня мягкий, мука сухая. Остальное сам смотри."),
            new MerchantSpec("grocery", "Марта", "Соловьёва", "Овощи с утреннего обхода. Кто раньше встал, тот и с кашей."),
            new MerchantSpec("grocery", "Григорий", "Пекарев", "Печь с ночи топится. Бери, пока тёплое."),
            new MerchantSpec("grocery", "Нина", "Крупенина", "Крупы много не бывает. Особенно когда зима длинная."),
            new MerchantSpec("grocery", "Аркадий", "Свечкин", "Масло, соль, немного копчёного. Всё, что нужно, чтобы дожить до ужина."),
            new MerchantSpec("grocery", "Люба", "Медведева", "Сладости берут хорошо. Народ на небесах любит подсластить жизнь."),
            new MerchantSpec("grocery", "Пётр", "Рыбаков", "Рыба свежая, не сомневайся. Сам утром принимал."),
            new MerchantSpec("grocery", "Зоя", "Хлебникова", "Запасы у меня простые, зато честные. Смотри, выбирай."),
            new MerchantSpec("grocery", "Тимур", "Солодов", "Солод, дрожжи, зерно. Если варишь — тебе сюда."),
            new MerchantSpec("grocery", "Вера", "Пастухова", "Молоко, сыр, масло. Всё от наших, не привозное."),
            new MerchantSpec("grocery", "Савелий", "Мухин", "Мукой торгую давно. Плохого не держу."),

            // Оружие (10)
            new MerchantSpec("weapons", "Демид", "Кузнецов", "Оружие простое, без украшений. Работает — и ладно."),
            new MerchantSpec("weapons", "Ростислав", "Стальнов", "Смотри по руке. Тяжёлое не бери, если не уверен."),
            new MerchantSpec("weapons", "Аглая", "Клименко", "Клинки точёные. Пальцем не проверяй, я предупредила."),
            new MerchantSpec("weapons", "Борис", "Дорохов", "Порох сухой, стволы чистые. Остальное — как повезёт."),
            new MerchantSpec("weapons", "Мирон", "Тетерин", "Тетиву натянешь — сразу поймёшь, моё это или нет."),
            new MerchantSpec("weapons", "Ярослава", "Наумова", "Лёгкое оружие для тех, кто думает быстро. Тяжёлое — для тех, кто не думает вовсе."),
            new MerchantSpec("weapons", "Фаддей", "Порошин", "Дробь, картечь, порох. Всё по-честному, не разбавлено."),
            new MerchantSpec("weapons", "Ксения", "Артемьева", "Арбалеты надёжнее любого разговора. Проверено."),
            new MerchantSpec("weapons", "Лев", "Гранин", "Сталь держит заточку. Бери, не пожалеешь."),
            new MerchantSpec("weapons", "Устин", "Обухов", "Молот, топор, что угодно. Главное — не промахнись."),

            // Одежда и броня (10)
            new MerchantSpec("clothing", "Марина", "Портнова", "Ткани плотные, швы ровные. Не то что на рынке у реки."),
            new MerchantSpec("clothing", "Ефим", "Скорняков", "Кожа выделки хорошей. Переживёт тебя, если будешь беречь."),
            new MerchantSpec("clothing", "Ольга", "Кружевская", "Одежда должна сидеть, а не висеть. Меряй, не стесняйся."),
            new MerchantSpec("clothing", "Андрей", "Валенков", "Валенки, куртки, плащи. На высоте тепло важнее красоты."),
            new MerchantSpec("clothing", "Таисия", "Шелкова", "Шёлк редкий, но есть. Для тех, кто хочет выглядеть живым."),
            new MerchantSpec("clothing", "Игнат", "Дубровин", "Дублёнки крепкие. Ветер продувает — не беда."),
            new MerchantSpec("clothing", "Дарья", "Кожевникова", "Кожу беру и продаю. Своя выделка, не перекуп."),
            new MerchantSpec("clothing", "Прохор", "Сапожников", "Сапоги шью на совесть. Не жмут, не текут."),
            new MerchantSpec("clothing", "Эмилия", "Ткаченко", "Ткани на любой вкус. Выбирай, пока не разобрали."),
            new MerchantSpec("clothing", "Фома", "Платонов", "Плащи, накидки, капюшоны. На небесах без капюшона никак."),

            // Снаряжение и инструмент (10)
            new MerchantSpec("equipment", "Глеб", "Иноземцев", "Инструмент хороший. Дешёвый дважды покупаешь."),
            new MerchantSpec("equipment", "Анастасия", "Верстакова", "Верстаки, тиски, крепёж. Всё, чтобы мастерская жила."),
            new MerchantSpec("equipment", "Никита", "Молчанов", "Молотки, зубила, напильники. Бери комплект — дешевле выйдет."),
            new MerchantSpec("equipment", "Полина", "Ключникова", "Замки, ключи, петли. Открыть и закрыть — моя работа."),
            new MerchantSpec("equipment", "Захар", "Ремизов", "Ремни, пряжки, стропы. Держит — значит, годится."),
            new MerchantSpec("equipment", "Инна", "Канаева", "Канаты и тросы. На высоте экономить на них не стоит."),
            new MerchantSpec("equipment", "Матвей", "Налимов", "Напильники и пилы. Острые, сам точу."),
            new MerchantSpec("equipment", "Софья", "Клепикова", "Клёпки, заклёпки, немного всякой мелочи. Без неё никуда."),
            new MerchantSpec("equipment", "Егор", "Дремин", "Дрель, сверла, метизы. Мастерская без этого — просто комната."),
            new MerchantSpec("equipment", "Лада", "Стрельцова", "Стремянки, лестницы, лебёдки. Высота не прощает халтуры."),

            // Аптека (8)
            new MerchantSpec("medical", "Арина", "Лекарева", "Бинты, антисептик, жаропонижающее. Остальное — по рецепту."),
            new MerchantSpec("medical", "Вениамин", "Бирюков", "Лекарства не панацея, но без них хуже. Смотри."),
            new MerchantSpec("medical", "Клавдия", "Наумова", "Всё стерильное, проверенное. На себе проверяла."),
            new MerchantSpec("medical", "Илья", "Апухтин", "Аптека простая. Раны, кашель, головная боль — справимся."),
            new MerchantSpec("medical", "Наталья", "Сычёва", "Сыворотки свежие. Не все, но большинство."),
            new MerchantSpec("medical", "Донат", "Жуков", "Жгуты, шины, обезболивающее. На всякий случай держи под рукой."),
            new MerchantSpec("medical", "Елена", "Пирогова", "Пихта, мёд, травы. Кому-то помогает лучше аптеки."),
            new MerchantSpec("medical", "Семён", "Капустин", "Капли, мази, порошки. Всё разложено, не спеши."),

            // Техника и связь (10)
            new MerchantSpec("tech", "Роман", "Бельский", "Батареи, провода, разъёмы. Без электричества небо мёртвое."),
            new MerchantSpec("tech", "Виктория", "Радова", "Рации и антенны. Связь — это жизнь, особенно в воздухе."),
            new MerchantSpec("tech", "Артём", "Проводников", "Проводка, реле, предохранители. Всё, чтобы не загорелось."),
            new MerchantSpec("tech", "Инга", "Сенцова", "Датчики и сенсоры. Чутьё у машины тоже нужно."),
            new MerchantSpec("tech", "Кирилл", "Резников", "Резисторы, платы, мелочёвка. Ремонт начинается с неё."),
            new MerchantSpec("tech", "Милана", "Чащина", "Частоты, фильтры, приёмники. Настрою — услышишь."),
            new MerchantSpec("tech", "Олег", "Тарасов", "Инструмент для электроники. Тонкий, точный, не дешёвый."),
            new MerchantSpec("tech", "Жанна", "Антонова", "Лампы, диоды, контакты. Старое, но рабочее."),
            new MerchantSpec("tech", "Тарас", "Раздольский", "Кабели любой длины. Отрежу по метру."),
            new MerchantSpec("tech", "Алиса", "Платонова", "Платы и модули связи. С миру по микросхеме — вот и лавка."),

            // Сырьё и ресурсы (8)
            new MerchantSpec("resources", "Макар", "Руднев", "Руда, слитки, немного цветного металла. Всё с рудников."),
            new MerchantSpec("resources", "Степанида", "Глинская", "Глина, известь, песок. Стройка без этого стоит."),
            new MerchantSpec("resources", "Тихон", "Сливин", "Слитки честные, вес проверен. Не люблю обманывать."),
            new MerchantSpec("resources", "Агата", "Кузьмина", "Кварц, стекло, редкие камни. Для тех, кто мастерит."),
            new MerchantSpec("resources", "Влас", "Смолин", "Смола, дёготь, масла. Пахнет — зато держит."),
            new MerchantSpec("resources", "Прасковья", "Глинкина", "Горшки, кирпич, черепица. Из глины можно многое."),
            new MerchantSpec("resources", "Добрыня", "Кравцов", "Кремень, обсидиан, крепёж. Простое, но надёжное."),
            new MerchantSpec("resources", "Мирослава", "Волкова", "Волокно, ткань, шпагат. Всё, что вьётся и держит."),

            // Топливо (3)
            new MerchantSpec("fuel", "Гаврила", "Нефёдов", "Горючее есть. Не всё, но на дорогу хватит."),
            new MerchantSpec("fuel", "Оксана", "Керженцева", "Керосин, масла, немного газа. Осторожно с огнём."),
            new MerchantSpec("fuel", "Пахом", "Углицкий", "Уголь, дрова, растопка. Печь топить — ко мне."),

            // Антиграв (4)
            new MerchantSpec("antigrav", "Эльдар", "Парнов", "Антиграв разный. Дешёвый — для груза, дорогой — для людей."),
            new MerchantSpec("antigrav", "Ника", "Левитина", "Платы антиграва, катушки, крепёж. Собирай сам, если умеешь."),
            new MerchantSpec("antigrav", "Святослав", "Антипов", "Подъёмная сила — товар серьёзный. Не экономь на ней."),
            new MerchantSpec("antigrav", "Регина", "Подъякова", "Антигравовые чипы. Маленькие, а держат целый ящик."),

            // Мезий (3)
            new MerchantSpec("meziy", "Арсений", "Мезин", "Мезий — не для всех. Но если понимаешь, зачем — бери."),
            new MerchantSpec("meziy", "Лидия", "Катаева", "Катализаторы свежие. Не храни долго, потеряют силу."),
            new MerchantSpec("meziy", "Гордей", "Сыромятников", "Сырьё и стабилизатор. Всё, чтобы мезий не рванул."),

            // Импланты и модули (3)
            new MerchantSpec("implants", "Владлен", "Ильин", "Модули простые, без чудес. Ставь — и работай."),
            new MerchantSpec("implants", "Есения", "Морозова", "Импланты для тех, кто хочет больше. Но сначала подумай."),
            new MerchantSpec("implants", "Родион", "Смирнов", "Схемы, модули, крепления. Соберу под тебя, если надо."),
        };

        private static readonly string[] FoodKeywords = new string[]
        {
            "bread","jam","rice","meat","fish","cheese","ale","coffee","tea","honey","salt","sugar","soup","hardtack","stew","milk","butter","flour",
            "water","rum","wine","food","ration","provision","fruit","vegetable","apple","carrot","potato","cabbage","sausage","pie","cake","porridge",
            "grain","oat","barley","wheat","nut","dried","smoked","pickle","kvass","beer","syrup","cocoa","pepper","spice","egg","bean","lentil",
            "mushroom","berry","ham","bacon","lard","biscuit","cookie","marmalade","chocolate","juice","lemonade"
        };

        private static readonly string[] ClothingKeywords = new string[]
        {
            "coat","cloak","jacket","boot","glove","hat","mask","suit","uniform","shirt","pants","vest","armor","helmet","scarf","belt","apron",
            "robe","dress","shoe","mitten","hood","cape","trouser","skirt","blouse","camisole","overall","coverall","frock","leather","fur",
            "sweater","sock","sandal","gauntlet","bracer","pad","greave","cuirass","garb"
        };

        private static readonly Dictionary<string, GroupMeta> MetaMap = BuildMetaMap();

        private static Dictionary<string, GroupMeta> BuildMetaMap()
        {
            var map = new Dictionary<string, GroupMeta>();
            map["grocery"] = new GroupMeta("Бакалея", new[]
            {
                "Смотри, что есть. Если чего нет — спроси, может, придерживаю.",
                "Бери на неделю. На высоте второй раз не сбегаешь.",
                "Всё разложено. Цены простые, без торга."
            }, new[]
            {
                "Цена уже с учётом дороги. Меньше — только если берёшь мешок.",
                "Торг уместен, но не до слёз. Я тоже ем."
            });
            map["weapons"] = new GroupMeta("Оружейная лавка", new[]
            {
                "Оружие не игрушка. Бери то, что сможешь удержать.",
                "Проверь баланс. Если неудобно в руке — не твоё.",
                "Уход простой: чисти, смазывай, не бросай."
            }, new[]
            {
                "Сталь дешевле не станет. Могу скинуть за комплект.",
                "На оружии не экономят. Или бери плохое и молись."
            });
            map["clothing"] = new GroupMeta("Одежда и броня", new[]
            {
                "Меряй. Если жмёт — не бери, потом спасибо не скажешь.",
                "Ткань плотная. Переживёт пару сезонов, если не рвать.",
                "Одежда должна двигаться вместе с тобой, а не мешать."
            }, new[]
            {
                "Скину, если возьмёшь две. Одну — вряд ли.",
                "Ткань не я тку, цена не с неба."
            });
            map["equipment"] = new GroupMeta("Снаряжение и инструмент", new[]
            {
                "Инструмент бери тот, который не жалко в руках держать.",
                "Дешёвое ломается в самый неподходящий момент.",
                "Всё проверено. Если что — возвращайся, разберёмся."
            }, new[]
            {
                "За комплект отдам чуть дешевле. По одной — в минус.",
                "Хороший инструмент окупается. Плохой — нет."
            });
            map["medical"] = new GroupMeta("Аптека", new[]
            {
                "Держи аптечку под рукой. Не тогда, когда уже поздно.",
                "Смотри сроки. Старое не беру и тебе не советую.",
                "Лекарство — не конфета. Читай, что написано."
            }, new[]
            {
                "На лекарствах не торгуюсь. Это не роскошь.",
                "Скину, если берёшь коробкой."
            });
            map["tech"] = new GroupMeta("Техника и связь", new[]
            {
                "Электричество не любит спешки. Бери с запасом.",
                "Проводку сам проверяй. Я продаю, а отвечаешь ты.",
                "Всё рабочее. Если не так — приноси, заменю."
            }, new[]
            {
                "Платы дорогие, я тут ни при чём. Могу подобрать дешевле.",
                "Хочешь дешевле — бери б/у, но без гарантии."
            });
            map["resources"] = new GroupMeta("Сырьё и материалы", new[]
            {
                "Сырьё разное. Скажи, что мастеришь, — подберу.",
                "Вес честный. Могу взвесить при тебе.",
                "Бери столько, сколько унесёшь. Остальное подожду."
            }, new[]
            {
                "Цена по весу. Хочешь дешевле — бери больше.",
                "Слитки не портятся. Подожду лучшего покупателя."
            });
            map["fuel"] = new GroupMeta("Топливо", new[]
            {
                "С огнём шутить не надо. Держи отдельно от искр.",
                "На дорогу хватит. Сколько — сам считай.",
                "Горючее свежее. Не храни в сырости."
            }, new[]
            {
                "Горючее само не появляется. Цена честная.",
                "Скину за опт. По литру — нет."
            });
            map["antigrav"] = new GroupMeta("Антигравий", new[]
            {
                "Антиграв не терпит ударов. Обращайся аккуратно.",
                "Если не знаешь, как ставить, — спроси. Научу.",
                "Подъём считай с запасом. Всегда."
            }, new[]
            {
                "Антиграв не подешевеет от разговора.",
                "Могу отдать с крепежом, но не дешевле."
            });
            map["meziy"] = new GroupMeta("Мезий", new[]
            {
                "Мезий — товар нервный. Держи в чехле.",
                "Не смешивай с чем попало. Потом не отмоешься.",
                "Если не понимаешь — не бери. Честно."
            }, new[]
            {
                "Мезий не торгую. Не тот товар.",
                "Цена как есть. Хочешь дешевле — бери сырьё."
            });
            map["implants"] = new GroupMeta("Импланты и модули", new[]
            {
                "Модуль ставится один раз. Сначала подумай, потом плати.",
                "Совместимость проверь. Не всё со всем работает.",
                "Если не уверен — принеси свою схему, посмотрю."
            }, new[]
            {
                "Модуль — это не тряпка. Цена окончательная.",
                "Скину только за установку у моего мастера."
            });
            return map;
        }

        private static string NpcIdFor(int i) { return "npc_merchant_primum_" + (i + 1).ToString("000"); }
        private static string TreeIdFor(int i) { return "dlg_merchant_primum_" + (i + 1).ToString("000"); }
        private static string DefPathFor(int i) { return DefDir + "/Merchant_" + (i + 1).ToString("000") + ".asset"; }
        private static string TablePathFor(int i) { return TableDir + "/Merchant_" + (i + 1).ToString("000") + "_Table.asset"; }
        private static string TreePathFor(int i) { return TreeDir + "/" + TreeIdFor(i) + ".asset"; }

        public static string Preview()
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("SPECS=" + Specs.Length);
                var ids = new HashSet<string>();
                var names = new HashSet<string>();
                foreach (var s in Specs) { if (!names.Add(s.FullName)) sb.AppendLine("DUP NAME: " + s.FullName); }
                for (int i = 0; i < Specs.Length; i++) { if (!ids.Add(NpcIdFor(i))) sb.AppendLine("DUP ID: " + NpcIdFor(i)); }
                var all = LoadAllItems();
                sb.AppendLine("ITEM_ASSETS=" + all.Count);
                foreach (var key in new[] { "grocery", "weapons", "clothing", "equipment", "medical", "tech", "resources", "fuel", "antigrav", "meziy", "implants" })
                {
                    var pool = BuildPool(key, all);
                    int count = 0;
                    for (int i = 0; i < Specs.Length; i++) if (Specs[i].Group == key) count++;
                    sb.Append("POOL " + key + " merchants=" + count + " items=" + pool.Count + " :: ");
                    sb.AppendLine(string.Join(" | ", pool.Take(6).Select(x => DisplayItemName(x)).ToArray()));
                }
                var db = AssetDatabase.LoadAssetAtPath<QuestDatabase>(DatabasePath);
                sb.AppendLine("QuestDatabase loaded=" + (db != null) + " npcs=" + (db != null && db.npcs != null ? db.npcs.Length : -1));
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                sb.AppendLine("Prefab loaded=" + (prefab != null));
                var fac = AssetDatabase.LoadAssetAtPath<FactionDefinition>(FactionPath);
                sb.AppendLine("FreeTraders loaded=" + (fac != null));
                return sb.ToString();
            }
            catch (Exception ex) { return "EXCEPTION: " + ex; }
        }

        public static string CreateAssets()
        {
            try
            {
                EnsureFolder(DefDir);
                EnsureFolder(TableDir);
                EnsureFolder(TreeDir);

                var freeTraders = AssetDatabase.LoadAssetAtPath<FactionDefinition>(FactionPath);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var db = AssetDatabase.LoadAssetAtPath<QuestDatabase>(DatabasePath);
                if (freeTraders == null) return "FreeTraders faction asset missing";
                if (prefab == null) return "Canonical Mira prefab missing";
                if (db == null) return "QuestDatabase missing";

                var all = LoadAllItems();
                var pools = new Dictionary<string, List<ItemData>>();
                foreach (var key in new[] { "grocery", "weapons", "clothing", "equipment", "medical", "tech", "resources", "fuel", "antigrav", "meziy", "implants" })
                    pools[key] = BuildPool(key, all);

                var defs = new List<NpcDefinition>();
                var trees = new List<DialogTree>();
                var warnings = new List<string>();

                for (int i = 0; i < Specs.Length; i++)
                {
                    var spec = Specs[i];
                    var meta = MetaMap[spec.Group];
                    var pool = pools[spec.Group];
                    if (pool.Count == 0) { warnings.Add("empty pool for " + spec.Group); continue; }

                    // 1) Merchant table
                    var table = LoadOrCreate<NpcMerchantTable>(TablePathFor(i));
                    table.shopTitle = meta.Title;
                    table.priceMultiplier = 0.90f + (i % 7) * 0.05f;
                    table.allowPlayerSellToNpc = true;
                    table.sellBackPriceMultiplier = 0.40f + (i % 4) * 0.05f;
                    table.enableReputationDiscount = true;
                    table.maxReputationDiscountRatio = MerchantPriceResolver.DefaultMaxDiscountRatio;
                    table.maxReputationMarkupRatio = MerchantPriceResolver.DefaultMaxMarkupRatio;
                    table.refuseTradeOnLowReputation = true;
                    table.refuseTradeNpcAttitudeThreshold = MerchantPriceResolver.DefaultRefuseNpcAttitude;
                    table.refuseTradeFactionRepThreshold = MerchantPriceResolver.DefaultRefuseFactionRep;
                    table.refuseTradeStandingScoreThreshold = MerchantPriceResolver.DefaultRefuseStandingScore;
                    table.items = BuildEntries(pool, spec, i);
                    EditorUtility.SetDirty(table);

                    // 2) Npc definition
                    var def = LoadOrCreate<NpcDefinition>(DefPathFor(i));
                    def.npcId = NpcIdFor(i);
                    def.displayName = spec.FullName;
                    def.factionRef = freeTraders;
                    def.faction = FactionId.FreeTraders;
                    def.portrait = null;
                    def.prefab = prefab;
                    def.animatorTriggerPrefix = "";
                    def.questOffers = new string[0];
                    def.questOfferRefs = new QuestDefinition[0];
                    def.questTurnIns = new string[0];
                    def.questTurnInRefs = new QuestDefinition[0];
                    def.services = NpcService.Trade;
                    def.merchantTable = table;
                    def.sellAllItemsForTesting = false;
                    def.testModeDefaultPrice = 25;
                    def.allowPlayerSellInTestMode = true;
                    def.enableBlacksmith = false;
                    def.blacksmithConfig = null;
                    def.blacksmithAllowedTiers = BlacksmithTierMask.Bronze | BlacksmithTierMask.Steel;
                    def.blacksmithAllowAllTiersForTesting = false;
                    def.interactionRadius = 3f;
                    def.showGreeting = true;
                    def.greetingText = spec.Greeting;
                    def.attitudeLinks = new AttitudeLink[0];
                    def.personalAttitudeMin = -100;
                    def.personalAttitudeMax = 200;
                    def.voicePrefix = "";
                    EditorUtility.SetDirty(def);

                    // 3) Dialog tree
                    var tree = LoadOrCreate<DialogTree>(TreePathFor(i));
                    tree.treeId = TreeIdFor(i);
                    tree.displayName = "Торговец: " + spec.FullName;
                    tree.rootNodeId = "greeting";
                    tree.nodes = BuildNodes(spec, def, meta, i);
                    tree.localizationTable = null;
                    EditorUtility.SetDirty(tree);

                    def.defaultDialogTree = tree;
                    EditorUtility.SetDirty(def);

                    defs.Add(def);
                    trees.Add(tree);
                }

                // 4) Register in QuestDatabase (additive, no duplicates)
                var npcList = new List<NpcDefinition>(db.npcs ?? new NpcDefinition[0]);
                foreach (var def in defs)
                {
                    bool exists = false;
                    foreach (var x in npcList) if (x != null && x.npcId == def.npcId) { exists = true; break; }
                    if (!exists) npcList.Add(def);
                }
                db.npcs = npcList.ToArray();

                var treeList = new List<DialogTree>(db.dialogTrees ?? new DialogTree[0]);
                foreach (var tree in trees)
                {
                    bool exists = false;
                    foreach (var x in treeList) if (x != null && x.treeId == tree.treeId) { exists = true; break; }
                    if (!exists) treeList.Add(tree);
                }
                db.dialogTrees = treeList.ToArray();

                var facList = new List<FactionDefinition>(db.factions ?? new FactionDefinition[0]);
                bool hasFac = false;
                foreach (var f in facList) if (f == freeTraders) { hasFac = true; break; }
                if (!hasFac) facList.Add(freeTraders);
                db.factions = facList.ToArray();
                EditorUtility.SetDirty(db);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("OK created/updated defs=" + defs.Count + " tables=" + defs.Count + " trees=" + trees.Count + " dbNpcs=" + db.npcs.Length);
                foreach (var w in warnings) sb.AppendLine("WARN " + w);
                return sb.ToString();
            }
            catch (Exception ex) { return "EXCEPTION: " + ex; }
        }

        public static string PlaceInScene()
        {
            try
            {
                if (EditorApplication.isPlaying) return "Refuse: Play Mode is running.";
                var scene = SceneManager.GetSceneByPath(WorldPath);
                if (!scene.IsValid() || !scene.isLoaded)
                    scene = EditorSceneManager.OpenScene(WorldPath, OpenSceneMode.Additive);

                var root = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "WorldRoot_0_0");
                if (root == null) return "WorldRoot_0_0 not found in WorldScene_0_0";

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var freeTraders = AssetDatabase.LoadAssetAtPath<FactionDefinition>(FactionPath);
                if (prefab == null) return "Canonical Mira prefab missing";
                if (freeTraders == null) return "FreeTraders faction missing";

                var mira = scene.GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                    .FirstOrDefault(t => t.name == "[Mira]");
                if (mira == null) return "[Mira] not found in WorldScene_0_0";

                var container = root.transform.Find(GroupObjectName);
                if (container == null)
                {
                    var groupGo = new GameObject(GroupObjectName);
                    container = groupGo.transform;
                    container.SetParent(root.transform, false);
                    container.localPosition = Vector3.zero;
                    container.localRotation = Quaternion.identity;
                    container.localScale = Vector3.one;
                }

                // Precompute positions BEFORE instantiating so raycasts do not hit our own NPCs.
                var positions = new Vector3[Specs.Length];
                Vector3 origin = mira.position + new Vector3(6f, 0f, -4f);
                const int Columns = 10;
                const float Step = 3.0f;
                for (int i = 0; i < Specs.Length; i++)
                {
                    int c = i % Columns;
                    int r = i / Columns;
                    Vector3 target = origin + new Vector3(c * Step, 0f, r * Step);
                    positions[i] = ComputeGroundPosition(target, mira.position.y);
                }

                int created = 0, updated = 0;
                for (int i = 0; i < Specs.Length; i++)
                {
                    var spec = Specs[i];
                    var def = AssetDatabase.LoadAssetAtPath<NpcDefinition>(DefPathFor(i));
                    if (def == null) return "Missing NpcDefinition: " + DefPathFor(i);

                    string objName = "MERCHANT_" + (i + 1).ToString("000") + "_" + spec.First + "_" + spec.Last;
                    var existing = container.Find(objName);
                    GameObject go;
                    if (existing == null)
                    {
                        go = PrefabUtility.InstantiatePrefab(prefab, container) as GameObject;
                        if (go == null) return "Failed to instantiate prefab for " + objName;
                        go.name = objName;
                        created++;
                    }
                    else
                    {
                        go = existing.gameObject;
                        updated++;
                    }

                    go.transform.position = positions[i];
                    go.transform.rotation = Quaternion.Euler(0f, (i * 37) % 360, 0f);
                    ApplyOverrides(go, def, spec, freeTraders);
                }

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) return "WorldScene_0_0 save failed";

                return "OK placed=" + Specs.Length + " created=" + created + " updated=" + updated + " group=" + GroupObjectName;
            }
            catch (Exception ex) { return "EXCEPTION: " + ex; }
        }

        public static string BakeFo()
        {
            try
            {
                if (EditorApplication.isPlaying) return "Refuse: Play Mode is running.";

                var catalog = AssetDatabase.LoadAssetAtPath<GlobalMotionSceneCatalog>(CatalogPath);
                var profile = AssetDatabase.LoadAssetAtPath<GlobalMotionNetworkProfile>(ProfilePath);
                if (catalog == null) return "Catalog missing: " + CatalogPath;
                if (profile == null) return "Profile missing: " + ProfilePath;

                var scene = SceneManager.GetSceneByPath(WorldPath);
                if (!scene.IsValid() || !scene.isLoaded) return "WorldScene_0_0 is not loaded";
                if (scene.isDirty) return "WorldScene_0_0 is dirty; save it first";

                string guid = AssetDatabase.AssetPathToGUID(WorldPath);
                if (string.IsNullOrEmpty(guid)) return "WorldScene_0_0 guid not found";

                GlobalSceneSource source = null;
                foreach (var s in catalog.Data.scenes) if (s != null && s.sceneGuid == guid) { source = s; break; }
                if (source == null) return "Catalog has no source block for WorldScene_0_0";

                // Candidates exactly like BakePilotSceneCatalog: scene roots + every NetworkObject subtree root.
                var candidates = new HashSet<GameObject>();
                foreach (var r in scene.GetRootGameObjects())
                {
                    candidates.Add(r);
                    var nos = r.GetComponentsInChildren<Unity.Netcode.NetworkObject>(true);
                    foreach (var no in nos) if (no != null) candidates.Add(no.gameObject);
                }

                var ids = new Dictionary<GameObject, string>();
                foreach (var go in candidates)
                {
                    string sid = AuditGlobalSceneCatalog.SourceId(go);
                    if (!GlobalSceneCatalogCompiler.IsSourceId(sid, guid))
                        return "Transient identity (scene not saved?): " + go.name + " -> " + sid;
                    ids[go] = sid;

                    var marker = go.GetComponent<GlobalSceneSourceMarker>();
                    if (marker == null) marker = go.AddComponent<GlobalSceneSourceMarker>();
                    var mso = new SerializedObject(marker);
                    mso.FindProperty("_sourceId").stringValue = sid;
                    mso.FindProperty("_frameId").intValue = 0;
                    mso.FindProperty("_activateWhenReady").boolValue = false;
                    mso.ApplyModifiedPropertiesWithoutUndo();
                }

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) return "WorldScene_0_0 save failed after markers";

                // Rebuild observations from the live saved scene.
                var obsList = new List<GlobalSceneObservation>();
                foreach (var pair in ids)
                {
                    var go = pair.Key;
                    Transform parent = go.transform.parent;
                    string parentSid = "";
                    while (parent != null)
                    {
                        if (ids.ContainsKey(parent.gameObject)) { parentSid = ids[parent.gameObject]; break; }
                        parent = parent.parent;
                    }
                    bool missing = false;
                    string hash = AuditGlobalSceneCatalog.LayoutHash(go, out missing);
                    if (missing) return "Missing script/component in subtree: " + go.name;

                    var obs = new GlobalSceneObservation();
                    obs.sourceId = pair.Value;
                    obs.parentSourceId = parentSid;
                    obs.isRoot = go.transform.parent == null;
                    obs.isNetworkObject = go.GetComponent<Unity.Netcode.NetworkObject>() != null;
                    obs.layoutHash = hash;
                    obsList.Add(obs);
                }
                obsList.Sort((a, b) => string.CompareOrdinal(a.sourceId, b.sourceId));
                source.observations = obsList.ToArray();
                source.dependencyHash = AssetDatabase.GetAssetDependencyHash(WorldPath).ToString();
                source.saved = true;
                source.inspectedComplete = true;

                // Keep reviewed entries, add entries only for observations that have none.
                var byId = new Dictionary<string, GlobalSceneEntry>();
                foreach (var e in catalog.Data.entries) if (e != null && e.sceneGuid == guid) byId[e.sourceId] = e;

                var worldEntries = new List<GlobalSceneEntry>();
                int added = 0;
                foreach (var obs in obsList)
                {
                    GlobalSceneEntry e;
                    if (byId.TryGetValue(obs.sourceId, out e))
                    {
                        if (e.parentSourceId != obs.parentSourceId)
                            return "Parent drift for " + obs.sourceId + ": catalog=" + e.parentSourceId + " live=" + obs.parentSourceId;
                        worldEntries.Add(e);
                    }
                    else
                    {
                        e = new GlobalSceneEntry();
                        e.sceneGuid = guid;
                        e.sourceId = obs.sourceId;
                        e.parentSourceId = obs.parentSourceId;
                        e.reviewNote = "T-MERCH80: PRIMUM merchant NPC on the canonical [Mira] prefab; authored scene content reviewed as Unmanaged (FO leaves it to ScenePlacedObjectSpawner/NGO).";
                        e.treatment = GlobalSceneTreatment.Unmanaged;
                        e.ownership = obs.isNetworkObject ? GlobalSceneOwnership.SceneOwnedNetworkGameplay : GlobalSceneOwnership.AuthoredSceneContent;
                        e.spatial = false;
                        e.poseKind = GlobalScenePoseKind.None;
                        worldEntries.Add(e);
                        added++;
                    }
                }

                var obsSet = new HashSet<string>();
                foreach (var o in obsList) obsSet.Add(o.sourceId);
                foreach (var e in catalog.Data.entries)
                    if (e != null && e.sceneGuid == guid && !obsSet.Contains(e.sourceId))
                        return "Phantom catalog entry (no live object): " + e.sourceId;

                var allEntries = new List<GlobalSceneEntry>();
                foreach (var e in catalog.Data.entries) if (e != null && e.sceneGuid != guid) allEntries.Add(e);
                allEntries.AddRange(worldEntries);
                catalog.Data.entries = allEntries.ToArray();
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();

                GlobalScenePlan plan;
                string err;
                if (!GlobalSceneCatalogCompiler.TryCompile(catalog.Data, out plan, out err))
                    return "Catalog compile failed: " + err;

                string hex = GlobalMotionNetworkContract.DigestHex(plan.CopyDigest());
                var pso = new SerializedObject(profile);
                pso.FindProperty("_sceneLayoutDigest").stringValue = hex;
                pso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();

                GlobalScenePlan plan2;
                if (!GlobalSceneCatalogCompiler.TryCompile(catalog.Data, out plan2, out err))
                    return "Recompile failed: " + err;
                byte[] digest;
                if (!GlobalSceneCatalogCompiler.TryMatchDigest(plan2, profile.SceneLayoutDigest, out digest))
                    return "Digest mismatch after apply";

                return "OK entries=" + plan2.SpawnOrder.Count + " worldObs=" + obsList.Count + " addedEntries=" + added + " digest=" + hex.Substring(0, 16) + "...";
            }
            catch (Exception ex) { return "EXCEPTION: " + ex; }
        }

        // ── helpers ──

        // ── T-MERCH80b: factions + gender-matched models ──

        private static readonly HashSet<string> FemaleFirstNames = new HashSet<string>
        {
            "Марта","Нина","Люба","Зоя","Вера","Аглая","Ярослава","Ксения","Марина","Ольга","Таисия","Дарья","Эмилия",
            "Анастасия","Полина","Инна","Софья","Лада","Арина","Клавдия","Наталья","Елена","Виктория","Инга","Милана",
            "Жанна","Алиса","Степанида","Агата","Прасковья","Мирослава","Оксана","Ника","Регина","Лидия","Есения"
        };

        private const string GuildOfSuccessPath = "Assets/_Project/Resources/Data/Factions/GuildOfSuccess.asset";
        private const string VillagersPath = "Assets/_Project/Resources/Data/Factions/Faction_Villagers.asset";
        private const string NpcAnimatorPath = "Assets/_Project/Animation/AI/NpcAnimator_Goblin.overrideController";

        private static bool IsFemale(string first) { return FemaleFirstNames.Contains(first); }

        private static string FactionKeyForGroup(string group)
        {
            switch (group)
            {
                case "weapons":
                case "tech":
                case "implants":
                case "antigrav":
                case "meziy":
                    return "success";
                case "equipment":
                case "resources":
                case "fuel":
                    return "free";
                default: // grocery, medical, clothing
                    return "villagers";
            }
        }

        private static FactionId LegacyFaction(string key)
        {
            if (key == "success") return FactionId.GuildOfSuccess;
            if (key == "free") return FactionId.FreeTraders;
            return FactionId.Villagers;
        }

        private static string ModelPath(string category, string name)
        {
            return "Assets/Generated_Models_Rigged_NPC/01_Characters/" + category + "/" + name + "/" + name + "_Rigged_NPC_Humanoid.fbx";
        }

        private static string GuildSuccessModel(bool female)
        {
            return ModelPath("Guilds", female ? "PC_Char_Guild_Success_Female_APOSE_v01" : "PC_Char_Guild_Success_Male_APOSE_v01");
        }

        private static string VillagerBaseModel(bool female)
        {
            return female
                ? ModelPath("City", "PC_Char_City_MerchantWoman_APOSE_v02")
                : ModelPath("City", "PC_Char_City_LowerCraftsman_APOSE_v02");
        }

        private static string FreeTraderBaseModel(bool female, int index)
        {
            if (female)
                return (index % 2 == 0)
                    ? ModelPath("Archetypes", "PC_Char_Merchant_Intermediary_Female_APOSE_v02")
                    : ModelPath("City", "PC_Char_City_MerchantWoman_APOSE_v02");
            return ModelPath("Archetypes", "PC_Char_Merchant_Intermediary_Male_APOSE_v01");
        }

        private static string BlacksmithModel(bool female)
        {
            return ModelPath("Archetypes", female ? "PC_Char_Blacksmith_Female_APOSE_v02" : "PC_Char_Blacksmith_Male_APOSE_v02");
        }

        private static string EngineerModel(bool female, int index)
        {
            if (female)
                return (index % 2 == 0)
                    ? ModelPath("Engineers", "PC_Char_Engineer_Female_EastAsian_APOSE_v01")
                    : ModelPath("Engineers", "PC_Char_Engineer_Female_Nordic_APOSE_v01");
            return (index % 2 == 0)
                ? ModelPath("Engineers", "PC_Char_Engineer_Male_Andean_APOSE_v01")
                : ModelPath("Engineers", "PC_Char_Engineer_Male_Himalayan_APOSE_v01");
        }

        private static string CityEngineerModel(bool female, int index)
        {
            if (female) return EngineerModel(true, index);
            return ModelPath("City", "PC_Char_City_GuildEngineer_APOSE_v02");
        }

        private static string StylizedEngineerModel(bool female)
        {
            return ModelPath("Archetypes", female ? "PC_Char_Stylized_Engineer_Female_Hazard_APOSE_v01" : "PC_Char_Stylized_Engineer_Male_Hazard_APOSE_v01");
        }

        private static string PilotModel(bool female, int index)
        {
            string[] male =
            {
                "PC_Char_Pilot_Male_VioletJacket_APOSE_v01","PC_Char_Pilot_Male_SteelFull_APOSE_v01",
                "PC_Char_Pilot_Male_Composite_APOSE_v01","PC_Char_Pilot_Male_Stripped_APOSE_v01"
            };
            string[] fem =
            {
                "PC_Char_Pilot_Female_VioletJacket_APOSE_v01","PC_Char_Pilot_Female_SteelFull_APOSE_v01",
                "PC_Char_Pilot_Female_Composite_APOSE_v01","PC_Char_Pilot_Female_Stripped_APOSE_v01"
            };
            return ModelPath("Pilots", (female ? fem : male)[index % 4]);
        }

        private static string MesiumHandlerModel(bool female)
        {
            return ModelPath("Archetypes", female ? "PC_Char_Mesium_Handler_Female_APOSE_v01" : "PC_Char_Mesium_Handler_Male_APOSE_v01");
        }

        private static string MedicModel(bool female, int index)
        {
            if (female)
                return (index % 2 == 0)
                    ? ModelPath("Medics", "PC_Char_Medic_Female_Junior_CoatWinter_APOSE_v01")
                    : ModelPath("Medics", "PC_Char_Medic_Female_Senior_CoatWinter_APOSE_v01");
            return (index % 2 == 0)
                ? ModelPath("Medics", "PC_Char_Medic_Male_Junior_CoatWinter_APOSE_v01")
                : ModelPath("Medics", "PC_Char_Medic_Male_Senior_CoatWinter_APOSE_v01");
        }

        private static string ScavengerModel(bool female)
        {
            return ModelPath("Archetypes", female ? "PC_Char_Scavenger_Female_APOSE_v01" : "PC_Char_Scavenger_Male_APOSE_v01");
        }

        private static string ModelFor(MerchantSpec spec, int groupIndex, bool female)
        {
            switch (spec.Group)
            {
                case "weapons":
                    return (groupIndex % 2 == 0) ? BlacksmithModel(female) : GuildSuccessModel(female);
                case "tech":
                    return (groupIndex % 5 < 2) ? EngineerModel(female, groupIndex) : GuildSuccessModel(female);
                case "implants":
                    return (groupIndex == 0) ? StylizedEngineerModel(female) : GuildSuccessModel(female);
                case "antigrav":
                    return (groupIndex % 2 == 0) ? PilotModel(female, groupIndex) : GuildSuccessModel(female);
                case "meziy":
                    return (groupIndex == 0) ? MesiumHandlerModel(female) : GuildSuccessModel(female);
                case "medical":
                    return (groupIndex < 5) ? MedicModel(female, groupIndex) : VillagerBaseModel(female);
                case "equipment":
                    return (groupIndex % 5 < 2) ? CityEngineerModel(female, groupIndex) : FreeTraderBaseModel(female, groupIndex);
                case "resources":
                    return (groupIndex < 3) ? ScavengerModel(female) : FreeTraderBaseModel(female, groupIndex);
                case "fuel":
                    return (groupIndex < 2) ? ScavengerModel(female) : FreeTraderBaseModel(female, groupIndex);
                default: // grocery, clothing
                    return VillagerBaseModel(female);
            }
        }

        public static string PreviewModels()
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                var groupIndex = new Dictionary<string, int>();
                var factionCount = new Dictionary<string, int>();
                for (int i = 0; i < Specs.Length; i++)
                {
                    var spec = Specs[i];
                    int gi;
                    groupIndex.TryGetValue(spec.Group, out gi);
                    groupIndex[spec.Group] = gi + 1;
                    bool female = IsFemale(spec.First);
                    string key = FactionKeyForGroup(spec.Group);
                    int c;
                    factionCount.TryGetValue(key, out c);
                    factionCount[key] = c + 1;
                    string model = ModelFor(spec, gi, female);
                    sb.AppendLine((i + 1).ToString("000") + " " + (female ? "F" : "M") + " " + key + " " + spec.FullName + " -> " + System.IO.Path.GetFileNameWithoutExtension(model));
                }
                foreach (var pair in factionCount) sb.AppendLine("FACTION " + pair.Key + " = " + pair.Value);
                return sb.ToString();
            }
            catch (Exception ex) { return "EXCEPTION: " + ex; }
        }

        public static string ApplyFactionsAndModels()
        {
            try
            {
                if (EditorApplication.isPlaying) return "Refuse: Play Mode is running.";

                var guildSuccess = AssetDatabase.LoadAssetAtPath<FactionDefinition>(GuildOfSuccessPath);
                var freeTraders = AssetDatabase.LoadAssetAtPath<FactionDefinition>(FactionPath);
                var villagers = AssetDatabase.LoadAssetAtPath<FactionDefinition>(VillagersPath);
                var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(NpcAnimatorPath);
                if (guildSuccess == null) return "GuildOfSuccess faction missing";
                if (freeTraders == null) return "FreeTraders faction missing";
                if (villagers == null) return "Villagers faction missing";
                if (controller == null) return "NpcAnimator_Goblin override controller missing";

                var factions = new Dictionary<string, FactionDefinition>();
                factions["success"] = guildSuccess;
                factions["free"] = freeTraders;
                factions["villagers"] = villagers;

                var scene = SceneManager.GetSceneByPath(WorldPath);
                if (!scene.IsValid() || !scene.isLoaded) return "WorldScene_0_0 is not loaded";
                var root = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "WorldRoot_0_0");
                var container = root ? root.transform.Find(GroupObjectName) : null;
                if (container == null) return "NPC_PRIMUM_MERCHANTS container missing";

                var groupIndex = new Dictionary<string, int>();
                int changed = 0, errors = 0;
                var log = new System.Text.StringBuilder();

                for (int i = 0; i < Specs.Length; i++)
                {
                    var spec = Specs[i];
                    int gi;
                    groupIndex.TryGetValue(spec.Group, out gi);
                    groupIndex[spec.Group] = gi + 1;

                    string key = FactionKeyForGroup(spec.Group);
                    var faction = factions[key];
                    bool female = IsFemale(spec.First);

                    var def = AssetDatabase.LoadAssetAtPath<NpcDefinition>(DefPathFor(i));
                    if (def == null) { errors++; log.AppendLine("MISSING DEF " + DefPathFor(i)); continue; }
                    def.factionRef = faction;
                    def.faction = LegacyFaction(key);
                    EditorUtility.SetDirty(def);

                    string objName = "MERCHANT_" + (i + 1).ToString("000") + "_" + spec.First + "_" + spec.Last;
                    var child = container.Find(objName);
                    if (child == null) { errors++; log.AppendLine("MISSING SCENE NPC " + objName); continue; }

                    var social = child.GetComponent<NpcSocialBrain>();
                    if (social != null)
                    {
                        var so = new SerializedObject(social);
                        so.FindProperty("faction").objectReferenceValue = faction;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        PrefabUtility.RecordPrefabInstancePropertyModifications(social);
                    }

                    string modelPath = ModelFor(spec, gi, female);
                    string error;
                    if (!SwapModel(child.gameObject, modelPath, controller, out error))
                    {
                        errors++;
                        log.AppendLine("MODEL FAIL " + spec.FullName + " :: " + error);
                        continue;
                    }
                    changed++;
                }

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) return "WorldScene_0_0 save failed";
                AssetDatabase.SaveAssets();

                return "OK changed=" + changed + " errors=" + errors + (log.Length > 0 ? "\n" + log.ToString() : "");
            }
            catch (Exception ex) { return "EXCEPTION: " + ex; }
        }

        private static bool SwapModel(GameObject go, string fbxPath, RuntimeAnimatorController controller, out string error)
        {
            error = null;
            var visual = go.transform.Find("Visual");
            if (visual == null) { error = "no Visual child"; return false; }

            for (int i = visual.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(visual.GetChild(i).gameObject);

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbx == null) { error = "missing fbx: " + fbxPath; return false; }

            var model = PrefabUtility.InstantiatePrefab(fbx, visual) as GameObject;
            if (model == null) { error = "instantiate failed: " + fbxPath; return false; }

            string shortName = System.IO.Path.GetFileNameWithoutExtension(fbxPath).Replace("_Rigged_NPC_Humanoid", "");
            model.name = "Model_" + shortName;
            model.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            var anim = model.GetComponent<Animator>();
            if (anim == null) anim = model.GetComponentInChildren<Animator>(true);
            if (anim != null)
            {
                anim.runtimeAnimatorController = controller;
                anim.applyRootMotion = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(anim);
            }
            return true;
        }

        // T-MERCH80b: measure pivots so the swapped models stand on the same ground line.
        public static string ProbeModelBounds()
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                var scene = SceneManager.GetSceneByPath(WorldPath);
                if (!scene.IsValid() || !scene.isLoaded) return "WorldScene_0_0 is not loaded";
                var root = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "WorldRoot_0_0");
                var container = root ? root.transform.Find(GroupObjectName) : null;
                if (container == null) return "NPC_PRIMUM_MERCHANTS container missing";

                var sample = container.GetChild(0);
                var visual = sample.Find("Visual");
                sb.AppendLine("SAMPLE " + sample.name + " rootY=" + sample.position.y.ToString("F3") + " visualLocalY=" + (visual != null ? visual.localPosition.y.ToString("F3") : "n/a"));
                if (visual != null)
                    foreach (Transform c in visual)
                        sb.AppendLine("  OLD " + c.name + " " + BoundsDesc(c.gameObject));

                var used = new List<string>();
                var groupIndex = new Dictionary<string, int>();
                for (int i = 0; i < Specs.Length; i++)
                {
                    var spec = Specs[i];
                    int gi; groupIndex.TryGetValue(spec.Group, out gi); groupIndex[spec.Group] = gi + 1;
                    string p = ModelFor(spec, gi, IsFemale(spec.First));
                    if (!used.Contains(p)) used.Add(p);
                }

                foreach (var path in used)
                {
                    var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (fbx == null) { sb.AppendLine("MISSING " + path); continue; }
                    var temp = PrefabUtility.InstantiatePrefab(fbx) as GameObject;
                    if (temp == null) { sb.AppendLine("INSTANTIATE FAIL " + path); continue; }
                    sb.AppendLine("FBX " + System.IO.Path.GetFileNameWithoutExtension(path) + " " + BoundsDesc(temp) + " rootScale=" + temp.transform.localScale);
                    UnityEngine.Object.DestroyImmediate(temp);
                }
                return sb.ToString();
            }
            catch (Exception ex) { return "EXCEPTION: " + ex; }
        }

        private static string BoundsDesc(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return "no renderers";
            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return "minY=" + b.min.y.ToString("F3") + " maxY=" + b.max.y.ToString("F3") + " height=" + b.size.y.ToString("F3") + " centerXZ=" + b.center.x.ToString("F2") + "," + b.center.z.ToString("F2");
        }

        // T-MERCH80b: verify faction refs + model swaps on definitions and scene instances.
        public static string VerifyFactionsAndModels()
        {
            try
            {
                var scene = SceneManager.GetSceneByPath(WorldPath);
                if (!scene.IsValid() || !scene.isLoaded) return "WorldScene_0_0 is not loaded";
                var root = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "WorldRoot_0_0");
                var container = root ? root.transform.Find(GroupObjectName) : null;
                if (container == null) return "NPC_PRIMUM_MERCHANTS container missing";

                var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(NpcAnimatorPath);
                var factionCount = new Dictionary<string, int>();
                var modelCount = new Dictionary<string, int>();
                var problems = new List<string>();
                int ok = 0;

                for (int i = 0; i < Specs.Length; i++)
                {
                    var spec = Specs[i];
                    var def = AssetDatabase.LoadAssetAtPath<NpcDefinition>(DefPathFor(i));
                    if (def == null) { problems.Add("missing def " + (i + 1)); continue; }
                    string key = FactionKeyForGroup(spec.Group);
                    string wantFaction = key == "success" ? "GuildOfSuccess" : key == "free" ? "FreeTraders" : "Villagers";
                    FactionId wantLegacy = LegacyFaction(key);

                    if (def.factionRef == null || def.factionRef.factionKey != wantFaction)
                        problems.Add((i + 1) + " def factionRef=" + (def.factionRef ? def.factionRef.factionKey : "null") + " want " + wantFaction);
                    if (def.faction != wantLegacy)
                        problems.Add((i + 1) + " def legacy=" + def.faction + " want " + wantLegacy);
                    string fk = def.factionRef ? def.factionRef.factionKey : "null";
                    int fc; factionCount.TryGetValue(fk, out fc); factionCount[fk] = fc + 1;

                    string objName = "MERCHANT_" + (i + 1).ToString("000") + "_" + spec.First + "_" + spec.Last;
                    var child = container.Find(objName);
                    if (child == null) { problems.Add("missing scene " + objName); continue; }
                    var social = child.GetComponent<NpcSocialBrain>();
                    if (social == null || social.faction == null || social.faction.factionKey != wantFaction)
                        problems.Add(objName + " social faction=" + (social && social.faction ? social.faction.factionKey : "null"));

                    var visual = child.Find("Visual");
                    if (visual == null || visual.childCount != 1) { problems.Add(objName + " Visual children=" + (visual ? visual.childCount : -1)); continue; }
                    var model = visual.GetChild(0);
                    if (!model.name.StartsWith("Model_")) problems.Add(objName + " model name=" + model.name);
                    var anim = model.GetComponent<Animator>() ?? model.GetComponentInChildren<Animator>(true);
                    if (anim == null) problems.Add(objName + " no animator");
                    else
                    {
                        if (anim.runtimeAnimatorController != controller) problems.Add(objName + " controller=" + (anim.runtimeAnimatorController ? anim.runtimeAnimatorController.name : "null"));
                        if (anim.applyRootMotion) problems.Add(objName + " applyRootMotion=true");
                        if (anim.avatar == null || !anim.avatar.isHuman) problems.Add(objName + " avatar not humanoid");
                    }
                    if (Mathf.Abs(model.localPosition.y - 1.5f) > 0.001f) problems.Add(objName + " model localY=" + model.localPosition.y);
                    string mk = model.name; int mc; modelCount.TryGetValue(mk, out mc); modelCount[mk] = mc + 1;
                    ok++;
                }

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("merchants ok=" + ok + " problems=" + problems.Count);
                foreach (var p in problems) sb.AppendLine("PROBLEM " + p);
                foreach (var pair in factionCount) sb.AppendLine("FACTION " + pair.Key + " = " + pair.Value);
                foreach (var pair in modelCount) sb.AppendLine("MODEL " + pair.Key + " = " + pair.Value);
                return sb.ToString();
            }
            catch (Exception ex) { return "EXCEPTION: " + ex; }
        }

        // T-MERCH80b: every trade-capable NpcDefinition in the project must belong to one of the 3 allowed factions.
        public static string CheckModelMaterials()
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                var used = new List<string>();
                var groupIndex = new Dictionary<string, int>();
                for (int i = 0; i < Specs.Length; i++)
                {
                    var spec = Specs[i];
                    int gi; groupIndex.TryGetValue(spec.Group, out gi); groupIndex[spec.Group] = gi + 1;
                    string p = ModelFor(spec, gi, IsFemale(spec.First));
                    if (!used.Contains(p)) used.Add(p);
                }
                foreach (var path in used)
                {
                    var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (fbx == null) { sb.AppendLine("MISSING " + path); continue; }
                    var temp = PrefabUtility.InstantiatePrefab(fbx) as GameObject;
                    if (temp == null) { sb.AppendLine("INSTANTIATE FAIL " + path); continue; }
                    var rends = temp.GetComponentsInChildren<Renderer>(true);
                    var shaders = new HashSet<string>();
                    int nullMats = 0;
                    foreach (var r in rends)
                        foreach (var m in r.sharedMaterials)
                        {
                            if (m == null || m.shader == null) { nullMats++; continue; }
                            shaders.Add(m.shader.name);
                        }
                    sb.AppendLine(System.IO.Path.GetFileNameWithoutExtension(path) + " renderers=" + rends.Length + " nullMats=" + nullMats + " shaders=" + string.Join(",", shaders));
                    UnityEngine.Object.DestroyImmediate(temp);
                }
                return sb.ToString();
            }
            catch (Exception ex) { return "EXCEPTION: " + ex; }
        }

        public static string AuditAllTradeNpcs()
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                var allowed = new HashSet<string> { "GuildOfSuccess", "FreeTraders", "Villagers" };
                var guids = AssetDatabase.FindAssets("t:NpcDefinition", new[] { "Assets/_Project/Quests/Data/Npcs" });
                int trade = 0, bad = 0;
                var list = new List<NpcDefinition>();
                foreach (var g in guids)
                {
                    var def = AssetDatabase.LoadAssetAtPath<NpcDefinition>(AssetDatabase.GUIDToAssetPath(g));
                    if (def != null) list.Add(def);
                }
                list.Sort((a, b) => string.Compare(a.npcId, b.npcId, StringComparison.Ordinal));
                foreach (var def in list)
                {
                    bool isTrade = def.IsMerchant || (def.services & NpcService.Trade) != 0;
                    if (!isTrade) continue;
                    trade++;
                    string key = def.factionRef != null ? def.factionRef.factionKey : "ref:null";
                    bool ok = allowed.Contains(key);
                    if (!ok) bad++;
                    sb.AppendLine((ok ? "OK   " : "BAD  ") + def.npcId + " key=" + key + " legacy=" + def.faction + " ref=" + (def.factionRef ? def.factionRef.name : "null"));
                }
                sb.AppendLine("tradeNpcs=" + trade + " outsideAllowed=" + bad);
                return sb.ToString();
            }
            catch (Exception ex) { return "EXCEPTION: " + ex; }
        }

        private static List<ItemData> LoadAllItems()
        {
            var guids = new HashSet<string>();
            foreach (var filter in new[] { "t:ItemData", "t:WeaponItemData", "t:ClothingItemData", "t:ModuleItemData" })
                foreach (var g in AssetDatabase.FindAssets(filter, new[] { "Assets/_Project/Resources/Items" }))
                    guids.Add(g);

            var list = new List<ItemData>();
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (item != null && item.itemType != ItemType.Key) list.Add(item);
            }
            list.Sort((a, b) => string.Compare(DisplayItemName(a), DisplayItemName(b), StringComparison.OrdinalIgnoreCase));
            return list;
        }

        private static string DisplayItemName(ItemData item)
        {
            if (item == null) return "<null>";
            if (!string.IsNullOrEmpty(item.itemName)) return item.itemName;
            return item.name;
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.ToLowerInvariant().Replace('_', ' ').Replace('-', ' ');
        }

        private static bool ContainsAny(string value, string[] keywords)
        {
            string s = Normalize(value);
            for (int i = 0; i < keywords.Length; i++) if (s.Contains(keywords[i])) return true;
            return false;
        }

        private static List<ItemData> BuildPool(string group, List<ItemData> all)
        {
            var result = new List<ItemData>();
            foreach (var it in all)
            {
                if (it == null) continue;
                if (it is WeaponItemData) { if (group == "weapons") result.Add(it); continue; }
                if (it is ClothingItemData) { if (group == "clothing") result.Add(it); continue; }
                if (it is ModuleItemData) { if (group == "implants") result.Add(it); continue; }

                var t = it.itemType;
                if (group == "grocery" && t == ItemType.Food) result.Add(it);
                else if (group == "medical" && t == ItemType.Medical) result.Add(it);
                else if (group == "tech" && t == ItemType.Tech) result.Add(it);
                else if (group == "resources" && t == ItemType.Resources) result.Add(it);
                else if (group == "fuel" && t == ItemType.Fuel) result.Add(it);
                else if (group == "antigrav" && t == ItemType.Antigrav) result.Add(it);
                else if (group == "meziy" && t == ItemType.Meziy) result.Add(it);
                else if (group == "equipment" && t == ItemType.Equipment) result.Add(it);
            }

            if (group == "grocery")
            {
                var filtered = new List<ItemData>();
                foreach (var it in result) if (ContainsAny(DisplayItemName(it), FoodKeywords)) filtered.Add(it);
                if (filtered.Count >= 6) result = filtered;
            }
            if (group == "clothing")
            {
                foreach (var it in all)
                {
                    if (it == null || it.itemType != ItemType.Equipment) continue;
                    if (it is WeaponItemData || it is ModuleItemData || it is ClothingItemData) continue;
                    if (ContainsAny(DisplayItemName(it), ClothingKeywords)) result.Add(it);
                }
            }
            if (group == "equipment")
            {
                var filtered = new List<ItemData>();
                foreach (var it in result) if (!ContainsAny(DisplayItemName(it), ClothingKeywords)) filtered.Add(it);
                result = filtered;
            }

            var seen = new HashSet<ItemData>();
            var deduped = new List<ItemData>();
            foreach (var it in result) if (seen.Add(it)) deduped.Add(it);
            deduped.Sort((a, b) => string.Compare(DisplayItemName(a), DisplayItemName(b), StringComparison.OrdinalIgnoreCase));
            return deduped;
        }

        private static NpcMerchantItemEntry[] BuildEntries(List<ItemData> pool, MerchantSpec spec, int index)
        {
            if (pool == null || pool.Count == 0) return new NpcMerchantItemEntry[0];

            int take = Math.Min(pool.Count, spec.Group == "implants" ? 3 : 6 + (index % 5));
            var rng = new System.Random(12345 + index * 7919);
            var shuffled = new List<ItemData>(pool);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var tmp = shuffled[i]; shuffled[i] = shuffled[j]; shuffled[j] = tmp;
            }

            var picked = shuffled.Take(take).ToList();
            picked.Sort((a, b) => string.Compare(DisplayItemName(a), DisplayItemName(b), StringComparison.OrdinalIgnoreCase));

            var list = new List<NpcMerchantItemEntry>();
            for (int k = 0; k < picked.Count; k++)
            {
                var e = new NpcMerchantItemEntry();
                e.item = picked[k];
                e.customBuyPrice = 0;
                e.customSellPrice = 0;
                e.stock = ((index + k) % 9 == 0) ? 3 : -1;
                e.minNpcAttitude = -100;
                e.minFactionReputation = -100;
                e.requiredQuest = null;
                e.requiredQuestId = "";
                list.Add(e);
            }
            return list.ToArray();
        }

        private static DialogueNode[] BuildNodes(MerchantSpec spec, NpcDefinition def, GroupMeta meta, int index)
        {
            string browseText = meta.Browse[index % meta.Browse.Length];
            string haggleText = meta.Haggle[index % meta.Haggle.Length];

            var greeting = new DialogueNode();
            greeting.nodeId = "greeting";
            greeting.speaker = MakeSpeaker(def, NpcIdFor(index));
            greeting.text = spec.Greeting;
            greeting.portraitEmotion = "neutral";
            greeting.edges = new DialogueEdge[]
            {
                MakeEdge("Покажите товар", "browse", null),
                MakeEdge("Пока", "", new DialogueAction { type = DialogueActionType.EndConversation })
            };
            greeting.onEnterActions = new DialogueAction[0];

            var browse = new DialogueNode();
            browse.nodeId = "browse";
            browse.speaker = MakeSpeaker(def, NpcIdFor(index));
            browse.text = browseText;
            browse.portraitEmotion = "thoughtful";
            browse.edges = new DialogueEdge[]
            {
                MakeEdge("Спасибо, посмотрю", "", new DialogueAction { type = DialogueActionType.EndConversation }),
                MakeEdge("Дороговато", "haggle", null)
            };
            browse.onEnterActions = new DialogueAction[0];

            var haggle = new DialogueNode();
            haggle.nodeId = "haggle";
            haggle.speaker = MakeSpeaker(def, NpcIdFor(index));
            haggle.text = haggleText;
            haggle.portraitEmotion = "neutral";
            haggle.edges = new DialogueEdge[]
            {
                MakeEdge("Ладно, беру", "", new DialogueAction { type = DialogueActionType.EndConversation }),
                MakeEdge("Подумаю", "", new DialogueAction { type = DialogueActionType.EndConversation })
            };
            haggle.onEnterActions = new DialogueAction[0];

            return new DialogueNode[] { greeting, browse, haggle };
        }

        private static SpeakerRef MakeSpeaker(NpcDefinition def, string npcId)
        {
            var s = new SpeakerRef();
            s.speakerKind = SpeakerRef.Kind.Npc;
            s.refId = npcId;
            s.speakerNpc = def;
            return s;
        }

        private static DialogueEdge MakeEdge(string label, string target, DialogueAction action)
        {
            var e = new DialogueEdge();
            e.label = label;
            e.targetNodeId = target;
            e.condition = null;
            e.conditions = new DialogueCondition[0];
            e.action = action;
            e.hideIfUnavailable = true;
            return e;
        }

        private static Vector3 ComputeGroundPosition(Vector3 target, float fallbackY)
        {
            Vector3 probe = new Vector3(target.x, fallbackY, target.z);
            try
            {
                UnityEngine.AI.NavMeshHit navHit;
                if (UnityEngine.AI.NavMesh.SamplePosition(probe, out navHit, 6f, UnityEngine.AI.NavMesh.AllAreas))
                    return navHit.position + Vector3.up * 0.05f;
            }
            catch { }

            RaycastHit hit;
            Vector3 origin = new Vector3(target.x, fallbackY + 40f, target.z);
            if (Physics.Raycast(origin, Vector3.down, out hit, 120f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * 0.05f;

            return new Vector3(target.x, fallbackY, target.z);
        }

        private static void ApplyOverrides(GameObject go, NpcDefinition def, MerchantSpec spec, FactionDefinition faction)
        {
            var ctrl = go.GetComponent<NpcController>();
            if (ctrl != null)
            {
                var so = new SerializedObject(ctrl);
                so.FindProperty("definition").objectReferenceValue = def;
                so.FindProperty("interactionDistance").floatValue = 3f;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(ctrl);
            }

            var brain = go.GetComponent<NpcBrain>();
            if (brain != null)
            {
                var bso = new SerializedObject(brain);
                bso.FindProperty("_behaviorType").enumValueIndex = (int)NpcBrain.BehaviorType.Passive;
                bso.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(brain);
            }

            var social = go.GetComponent<NpcSocialBrain>();
            if (social != null)
            {
                var sso = new SerializedObject(social);
                sso.FindProperty("faction").objectReferenceValue = faction;
                sso.FindProperty("idleActivity").enumValueIndex = (int)NpcIdleActivity.Wander;
                sso.FindProperty("wanderRadius").floatValue = 3f;
                sso.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(social);
            }

            TextMeshPro label = null;
            var labels = go.GetComponentsInChildren<TextMeshPro>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (label == null) label = labels[i];
                if (labels[i].gameObject.name == "Text (TMP)") { label = labels[i]; break; }
            }
            if (label != null)
            {
                label.enableAutoSizing = true;
                label.fontSizeMin = 12f;
                label.fontSizeMax = 36f;
                label.text = spec.FullName.ToUpper();
                PrefabUtility.RecordPrefabInstancePropertyModifications(label);
            }
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(parent)) parent = parent.Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
