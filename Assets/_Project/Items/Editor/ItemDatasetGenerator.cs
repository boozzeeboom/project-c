// =====================================================================================
// ItemDatasetGenerator.cs — генератор тестового датасета ItemData (Project C: The Clouds)
// =====================================================================================
// Документация:
//   • docs/dev/INVENTORY_V2_REFACTOR.md — Phase 6 (Test dataset)
//
// Назначение: создаёт 8 типов × 3 варианта = 24 ItemData для тестирования инвентаря.
// Запускается вручную через меню: Tools → Project C → Inventory → Generate Test Dataset.
//
// Поведение:
//   • Идемпотентно: если .asset уже есть — пропускает (можно перезапустить).
//   • НЕ перезаписывает существующие (если хочется обновить — удалить .asset вручную).
//   • Создаёт файлы в Resources/Items/ рядом со старыми заглушками Item_Type1..8.
//
// LEGACY: старые Item_Type1..8 (пустые заглушки) ОСТАЮТСЯ. Их можно удалить в cleanup
// после того как убедимся что новые предметы работают. Сейчас — не трогаем, чтобы не
// сломать LootTable'ы / PickupItem, которые могут на них ссылаться.
// =====================================================================================

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using ProjectC.Items;

namespace ProjectC.Items.EditorTools
{
    public static class ItemDatasetGenerator
    {
        private const string OUTPUT_DIR = "Assets/_Project/Resources/Items";
        private const string ITEM_PREFIX = "Item_";  // Item_Resources_IronOre.asset и т.д.

        // ====================================================================
        // Dataset definition: 8 типов × 3 варианта
        // ====================================================================

        private struct ItemSpec
        {
            public string baseName;    // "Железная руда"
            public string description; // 1-2 предложения для тестов
            public ItemType type;
            public int maxStack;       // 1, 5, 20, 99 — разные стеки для тестов
            public float weightKg;
        }

        private static readonly List<ItemSpec> _specs = new List<ItemSpec>
        {
            // === Resources (0) — руда, дерево, кристаллы ===
            new ItemSpec { baseName = "Железная руда",    description = "Обычная железная руда. Добывается в шахтах.", type = ItemType.Resources, maxStack = 20, weightKg = 2.0f },
            new ItemSpec { baseName = "Медная руда",     description = "Красноватая медная руда. Используется в проводке.", type = ItemType.Resources, maxStack = 20, weightKg = 1.5f },
            new ItemSpec { baseName = "Кристаллическая пыль", description = "Мерцающие кристаллы. Редкий ресурс.", type = ItemType.Resources, maxStack = 10, weightKg = 0.5f },

            // === Equipment (1) — снаряжение, инструменты ===
            new ItemSpec { baseName = "Верёвка 10м",      description = "Прочная верёвка длиной 10 метров. Нужна для восхождений.", type = ItemType.Equipment, maxStack = 5, weightKg = 0.8f },
            new ItemSpec { baseName = "Карабин",          description = "Металлический карабин для страховки.", type = ItemType.Equipment, maxStack = 10, weightKg = 0.1f },
            new ItemSpec { baseName = "Фонарь",           description = "Ручной фонарь. Освещает тёмные места.", type = ItemType.Equipment, maxStack = 1, weightKg = 0.3f },

            // === Food (2) — еда, вода ===
            new ItemSpec { baseName = "Сухпаёк",          description = "Военный сухой паёк. Восстанавливает силы.", type = ItemType.Food, maxStack = 10, weightKg = 0.4f },
            new ItemSpec { baseName = "Консервы",         description = "Мясные консервы. Долго хранятся.", type = ItemType.Food, maxStack = 10, weightKg = 0.5f },
            new ItemSpec { baseName = "Бутыль воды",      description = "Чистая питьевая вода. 0.5 литра.", type = ItemType.Food, maxStack = 5, weightKg = 0.6f },

            // === Fuel (3) — топливо ===
            new ItemSpec { baseName = "Антигравитационное топливо", description = "Жидкое топливо для антиграв-двигателей. Высокая плотность.", type = ItemType.Fuel, maxStack = 5, weightKg = 3.0f },
            new ItemSpec { baseName = "Угольные брикеты", description = "Спрессованный уголь. Дешёвое топливо.", type = ItemType.Fuel, maxStack = 20, weightKg = 1.0f },
            new ItemSpec { baseName = "Газовый баллон",   description = "Сжатый газ для нагревателей. Взрывоопасен.", type = ItemType.Fuel, maxStack = 3, weightKg = 2.5f },

            // === Antigrav (4) — антиграв-камни (двигатели) ===
            new ItemSpec { baseName = "Антиграв-камень малый", description = "Маленький левитирующий камень. Подходит для малых платформ.", type = ItemType.Antigrav, maxStack = 3, weightKg = 1.0f },
            new ItemSpec { baseName = "Антиграв-камень большой", description = "Большой левитирующий камень. Подходит для тяжёлых кораблей.", type = ItemType.Antigrav, maxStack = 1, weightKg = 5.0f },
            new ItemSpec { baseName = "Стабилизатор поля", description = "Устройство для стабилизации антиграв-поля. Расходник.", type = ItemType.Antigrav, maxStack = 5, weightKg = 0.2f },

            // === Meziy (5) — мезий (особый ресурс) ===
            new ItemSpec { baseName = "Мезий-крошка",     description = "Маленький осколок мезия. Слабо светится.", type = ItemType.Meziy, maxStack = 20, weightKg = 0.05f },
            new ItemSpec { baseName = "Мезий-кристалл",   description = "Целый кристалл мезия. Источник энергии.", type = ItemType.Meziy, maxStack = 5, weightKg = 0.3f },
            new ItemSpec { baseName = "Мезий-сердцевина", description = "Чистая сердцевина мезия. Редчайший компонент.", type = ItemType.Meziy, maxStack = 1, weightKg = 1.0f },

            // === Medical (6) — медикаменты ===
            new ItemSpec { baseName = "Бинт",             description = "Обычный марлевый бинт. Перевязка ран.", type = ItemType.Medical, maxStack = 20, weightKg = 0.05f },
            new ItemSpec { baseName = "Антисептик",       description = "Дезинфицирующий раствор. Предотвращает заражение.", type = ItemType.Medical, maxStack = 10, weightKg = 0.1f },
            new ItemSpec { baseName = "Стимулятор",       description = "Медицинский стимулятор. Восстанавливает силы.", type = ItemType.Medical, maxStack = 5, weightKg = 0.1f },

            // === Tech (7) — электроника, инструменты ===
            new ItemSpec { baseName = "Батарея",          description = "Стандартная электрическая батарея.", type = ItemType.Tech, maxStack = 20, weightKg = 0.1f },
            new ItemSpec { baseName = "Микросхема",       description = "Печатная плата. Нужна для ремонта техники.", type = ItemType.Tech, maxStack = 10, weightKg = 0.05f },
            new ItemSpec { baseName = "Кабель",           description = "Электрический кабель 1м. Универсальный.", type = ItemType.Tech, maxStack = 20, weightKg = 0.05f },
        };

        // ====================================================================
        // Menu entry
        // ====================================================================

        [MenuItem("Tools/Project C/Inventory/Generate Test Dataset")]
        public static void Generate()
        {
            if (!Directory.Exists(OUTPUT_DIR))
            {
                Directory.CreateDirectory(OUTPUT_DIR);
                AssetDatabase.Refresh();
            }

            int created = 0;
            int skipped = 0;
            var report = new List<string>();

            foreach (var spec in _specs)
            {
                string typeName = ItemTypeNames.GetDisplayName(spec.type);
                string typeLatin = spec.type.ToString();  // Resources, Equipment, ...
                string assetPath = $"{OUTPUT_DIR}/{ITEM_PREFIX}{typeLatin}_{SanitizeFileName(spec.baseName)}.asset";

                if (File.Exists(assetPath))
                {
                    skipped++;
                    continue;
                }

                var item = ScriptableObject.CreateInstance<ItemData>();
                item.itemName = spec.baseName;
                item.itemType = spec.type;
                item.description = spec.description;
                item.icon = null;   // icon зададим позже, если будут готовы спрайты
                item.maxStack = spec.maxStack;
                item.weightKg = spec.weightKg;

                AssetDatabase.CreateAsset(item, assetPath);
                created++;
                report.Add($"  + {Path.GetFileName(assetPath)}  ({typeName}, stack={spec.maxStack})");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string msg = $"[ItemDatasetGenerator] Done. Created: {created}, Skipped (already exist): {skipped}\n\n" + string.Join("\n", report);
            Debug.Log(msg);
            EditorUtility.DisplayDialog(
                "ItemDatasetGenerator",
                $"Создано: {created}\nПропущено (уже есть): {skipped}\n\nПуть: {OUTPUT_DIR}",
                "OK");
        }

        /// <summary>Утилита: убрать пробелы и спец-символы для имени файла.</summary>
        private static string SanitizeFileName(string s)
        {
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
                    sb.Append(c);
                else
                    sb.Append('_');
            }
            return sb.ToString();
        }
    }
}
