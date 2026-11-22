#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using ProjectC.Factions;
using ProjectC.Quests;
using ProjectC.Quests.Editor;
using ProjectC.World.NpcPrimum;

namespace ProjectC.EditorTools.Npc
{
    /// <summary>
    /// Сборка пилотной толпы PRIMUM (T1 = 100 NPC).
    ///
    /// Создаёт/обновляет:
    ///  • AnimatorController x2 (female/male): 1D blend Speed → Idle01 / Walk01_Forward (humanoid retarget);
    ///  • шаблон-префаб PrimumCrowdNpc_Template (NavMeshAgent + trigger capsule + NpcController + TMP);
    ///  • по одному NpcDefinition на NPC (уникальные имя/реплика), папка авто-сканится в QuestDatabase;
    ///  • конфиг PrimumCrowdConfig (каталог 106 моделей + шаблон + контроллеры + личности);
    ///  • опционально — корень PRIMUM_NPC_CROWD в WorldScene_0_0 (отдельный пункт меню).
    ///
    /// Сцену при Build не трогает. Повторный запуск идемпотентен для своих ассетов.
    /// </summary>
    public static class PrimumCrowdBuilder
    {
        private const string ConfigPath = "Assets/_Project/Data/NPC/PrimumCrowdConfig.asset";
        private const string FemaleControllerPath = "Assets/_Project/Animation/NPC/PrimumCrowd_Female.controller";
        private const string MaleControllerPath = "Assets/_Project/Animation/NPC/PrimumCrowd_Male.controller";
        private const string TemplatePath = "Assets/_Project/Prefabs/NPC/PrimumCrowd/PrimumCrowdNpc_Template.prefab";
        private const string NpcFolder = "Assets/_Project/Quests/Data/Npcs/PrimumCrowd";
        private const string ModelRoot = "Assets/Generated_Models_Rigged_NPC";
        private const string WorldScenePath = "Assets/_Project/Scenes/World/WorldScene_0_0.unity";
        private const string CrowdRootName = "PRIMUM_NPC_CROWD";

        private const string FemaleIdleFbx = "Assets/Kevin Iglesias/Human Animations/Animations/Female/Idles/HumanF@Idle01.fbx";
        private const string FemaleWalkFbx = "Assets/Kevin Iglesias/Human Animations/Animations/Female/Movement/Walk/HumanF@Walk01_Forward.fbx";
        private const string MaleIdleFbx = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Idles/HumanM@Idle01.fbx";
        private const string MaleWalkFbx = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Walk/HumanM@Walk01_Forward.fbx";

        [MenuItem("Tools/Project C/NPC/Primum Crowd/1) Build T1 (100)", priority = 300)]
        public static void BuildT1() => Build(100, true);

        [MenuItem("Tools/Project C/NPC/Primum Crowd/2) Rebuild All", priority = 301)]
        public static void RebuildAll()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PrimumCrowdConfig>(ConfigPath);
            int count = existing != null ? Mathf.Max(100, existing.targetCount) : 100;
            Build(count, true);
        }

        public static void Build(int count, bool log)
        {
            EnsureFolders();

            var femaleController = CreateController(FemaleControllerPath, FemaleIdleFbx, FemaleWalkFbx);
            var maleController = CreateController(MaleControllerPath, MaleIdleFbx, MaleWalkFbx);
            if (femaleController == null || maleController == null)
            {
                Debug.LogError("[PrimumCrowdBuilder] Не найдены humanoid-клипы Kevin Iglesias — сборка остановлена.");
                return;
            }

            var models = CollectModels();
            if (models.Count == 0)
            {
                Debug.LogError("[PrimumCrowdBuilder] Не найдено ни одной rigged-модели NPC.");
                return;
            }

            var template = CreateTemplate();
            var definitions = CreateNpcDefinitions(count, models);

            var config = AssetDatabase.LoadAssetAtPath<PrimumCrowdConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<PrimumCrowdConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            config.targetCount = count;
            config.maxResidentsPerHouse = 4;
            config.npcTemplate = template;
            config.animatorControllerFemale = femaleController;
            config.animatorControllerMale = maleController;
            config.modelCatalog = models.ToArray();
            config.residentDefinitions = definitions.ToArray();
            EditorUtility.SetDirty(config);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            QuestDatabaseAutoDiscover.Rescan();

            if (log)
                Debug.Log($"[PrimumCrowdBuilder] Готово: NPC={definitions.Count}, моделей={models.Count}, " +
                          $"шаблон={TemplatePath}, конфиг={ConfigPath}.");
        }

        [MenuItem("Tools/Project C/NPC/Primum Crowd/3) Place root in WorldScene_0_0", priority = 302)]
        public static void PlaceSceneRoot()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string previous = EditorSceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[PrimumCrowdBuilder] Не удалось открыть {WorldScenePath}.");
                return;
            }

            var worldRoot = GameObject.Find("WorldRoot_0_0");
            if (worldRoot == null)
            {
                Debug.LogError("[PrimumCrowdBuilder] В сцене нет WorldRoot_0_0 — коллекцию ставить некуда.");
                return;
            }

            var existing = worldRoot.transform.Find(CrowdRootName);
            GameObject crowd = existing != null ? existing.gameObject : new GameObject(CrowdRootName);
            if (existing == null) crowd.transform.SetParent(worldRoot.transform, false);
            crowd.transform.localPosition = Vector3.zero;
            crowd.transform.localRotation = Quaternion.identity;
            crowd.transform.localScale = Vector3.one;

            var director = crowd.GetComponent<PrimumNpcCrowdDirector>();
            if (director == null) director = crowd.AddComponent<PrimumNpcCrowdDirector>();

            var config = AssetDatabase.LoadAssetAtPath<PrimumCrowdConfig>(ConfigPath);
            var so = new SerializedObject(director);
            var configProperty = so.FindProperty("config");
            if (configProperty != null && config != null) configProperty.objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            if (!string.IsNullOrEmpty(previous) && previous != WorldScenePath)
                EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);

            Debug.Log($"[PrimumCrowdBuilder] {CrowdRootName} под WorldRoot_0_0 в {WorldScenePath} сохранён.");
        }

        // ---------------------------------------------------------------- контроллеры

        private static AnimatorController CreateController(string path, string idleFbx, string walkFbx)
        {
            var idle = LoadClip(idleFbx);
            var walk = LoadClip(walkFbx);
            if (idle == null || walk == null)
            {
                Debug.LogError($"[PrimumCrowdBuilder] Клипы не найдены: idle='{idleFbx}' walk='{walkFbx}'.");
                return null;
            }

            AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

            var stateMachine = controller.layers[0].stateMachine;
            var tree = new BlendTree
            {
                name = "Locomotion",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, 1f);

            var state = stateMachine.AddState("Locomotion", new Vector3(300, 120, 0));
            state.motion = tree;
            stateMachine.defaultState = state;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static AnimationClip LoadClip(string fbxPath)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                    return clip;
            return null;
        }

        // ---------------------------------------------------------------- модели

        private static List<GameObject> CollectModels()
        {
            var result = new List<GameObject>();
            var guids = AssetDatabase.FindAssets("t:Model", new[] { ModelRoot });
            var paths = new List<string>(guids.Length);
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) paths.Add(path);
            }
            paths.Sort(StringComparer.Ordinal);

            foreach (var path in paths)
            {
                bool humanoid = false;
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (asset is Avatar avatar && avatar.isValid && avatar.isHuman) { humanoid = true; break; }
                if (!humanoid)
                {
                    Debug.LogWarning($"[PrimumCrowdBuilder] Пропущена модель без валидного humanoid-аватара: {path}");
                    continue;
                }
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model != null) result.Add(model);
            }
            return result;
        }

        // ---------------------------------------------------------------- шаблон

        private static GameObject CreateTemplate()
        {
            var root = new GameObject("PrimumCrowdNpc_Template");
            try
            {
                var agent = root.AddComponent<NavMeshAgent>();
                agent.radius = 0.5f;
                agent.height = 2f;
                agent.speed = 1.2f;
                agent.angularSpeed = 270f;
                agent.acceleration = 8f;
                agent.stoppingDistance = 0.4f;
                agent.autoBraking = true;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
                agent.avoidancePriority = 60;
                // Включает PrimumCrowdNpc.Initialize уже на навмеше — иначе Unity ругается на спавн вне меша.
                agent.enabled = false;

                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.isTrigger = true;
                capsule.radius = 0.35f;
                capsule.height = 1.9f;
                capsule.center = new Vector3(0f, 0.95f, 0f);

                root.AddComponent<PrimumCrowdNpc>();
                var npcController = root.AddComponent<NpcController>();

                var plateGo = new GameObject("Nameplate");
                plateGo.transform.SetParent(root.transform, false);
                plateGo.transform.localPosition = new Vector3(0f, 2.1f, 0f);
                plateGo.transform.localScale = new Vector3(0.34f, 0.34f, 0.34f);
                var tmp = plateGo.AddComponent<TextMeshPro>();
                tmp.text = "NPC";
                tmp.fontSize = 36f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = new Color(1f, 0.94f, 0.76f, 1f);
                tmp.rectTransform.sizeDelta = new Vector2(20f, 5f);
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.enableAutoSizing = false;

                // Привязываем нейм-плейт к NpcController, чтобы его Awake не создавал заглушку-куб.
                var so = new SerializedObject(npcController);
                var labelProperty = so.FindProperty("nameLabel");
                if (labelProperty != null) labelProperty.objectReferenceValue = tmp;
                so.ApplyModifiedPropertiesWithoutUndo();

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, TemplatePath);
                if (prefab == null) Debug.LogError($"[PrimumCrowdBuilder] Не удалось сохранить шаблон {TemplatePath}.");
                return prefab != null ? prefab : AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // ---------------------------------------------------------------- личности

        private static readonly string[] FirstNames =
        {
            "Ансель", "Брайт", "Велда", "Гарм", "Дарра", "Ерш", "Ждан", "Зоря", "Ирма", "Кайл",
            "Лена", "Мило", "Нора", "Орен", "Пётр", "Рута", "Савва", "Тихон", "Ульрих", "Фаина",
            "Харитон", "Цвета", "Шура", "Эдвин", "Юна", "Ян", "Аглая", "Борис", "Влас", "Гордей",
            "Дина", "Ерофей", "Женя", "Зима", "Игнат", "Ксения", "Люм", "Марк", "Наум", "Остап"
        };

        private static readonly string[] LastNames =
        {
            "Медников", "Кузнецов", "Плотвин", "Соляной", "Ветров", "Каменев", "Гвоздёв", "Рудный",
            "Смолин", "Часовой", "Лаптев", "Оружейников", "Полев", "Ключников", "Бархатов", "Яровой",
            "Стрельцов", "Корабельщиков", "Рощин", "Ступин", "Кожевников", "Мельников", "Печников",
            "Ткачёв", "Литейщиков", "Хмурый", "Тихий", "Быстров", "Дальний", "Заречный", "Осенев",
            "Ковров", "Ситников", "Мохов", "Гранитов", "Кремнёв", "Ясный", "Полозов", "Воронов", "Сивер"
        };

        private static readonly Dictionary<string, string[]> RoleObservations = new Dictionary<string, string[]>
        {
            ["City"] = new[]
            {
                "Город шумит так, будто у него своя погода.",
                "На Примуме всегда кто-то что-то несёт и никогда не роняет.",
                "Я знаю здесь каждый переулок, и все они ведут к рынку.",
                "Стены старые, а порядки новые — привыкаю.",
                "С утра туман с порта, к вечеру пыль с дороги.",
                "Если потеряешься — иди на звон посуды."
            },
            ["Engineers"] = new[]
            {
                "Пар в трубах держит не хуже гвоздя в доске.",
                "Мой сменщик опять перепутал клапан, я весь день выправлял.",
                "Город скрипит, но держится — как и мы.",
                "Металл не врёт: если гудит, значит устал.",
                "Три моста, два подъёмника и одна моя гордость.",
                "Инструмент берегу больше, чем парадный камзол."
            },
            ["Medics"] = new[]
            {
                "Сейчас тихо, значит скоро кого-нибудь принесут.",
                "Пыль в лёгких — местная плата за красивый вид.",
                "Не спорь со мной о ранах, я их видела больше, чем ты шагов сделал.",
                "В моей сумке хватит бинтов на весь квартал.",
                "Хороший сон лечит лучше моей мази, но мазь быстрее.",
                "Руки мыл — уже победа над половиной болезней."
            },
            ["Guilds"] = new[]
            {
                "У каждой гильдии свой запах: у одной воск, у другой — порох.",
                "Меня учили слушать, прежде чем торговаться.",
                "Бумаги важнее золота, пока их не потерял.",
                "Сегодня я мальчик на побегушках, завтра — мастер.",
                "Секреты гильдий ходят по кругу быстрее сплетен.",
                "Считаю до десяти, прежде чем злиться. Обычно помогает."
            },
            ["Explorers"] = new[]
            {
                "За той стеной воздух другой — проверял дважды.",
                "Карта врёт только там, где ты сам не был.",
                "Вернулся с пустыми руками, зато с полной головой.",
                "Ветер с плато сбивает с ног даже бывалых.",
                "Мой компас однажды спас меня от красивой смерти.",
                "Начал собирать камни — теперь несу их домой."
            },
            ["CreationChem"] = new[]
            {
                "Не вдыхай здесь глубоко, и всё будет хорошо.",
                "Смесь зелёная — значит ещё не готова.",
                "Химия не прощает спешки, зато щедра на сюрпризы.",
                "Мой старший говорит: сначала нюхай, потом лей.",
                "Пятна на халате — это карта моих опытов.",
                "Сегодня синтез удался, и это уже праздник."
            },
            ["Pilots"] = new[]
            {
                "Небо над Примумом чистое, пока не поднимешься сам.",
                "Посадка легче взлёта — это я выучил на своей шкуре.",
                "Двигатель поёт, значит всё в порядке.",
                "Ветер меняется у самой воды, помни это.",
                "Мой штурман спорит со мной только в воздухе.",
                "Держись за поручень, тут все когда-то падали."
            },
            ["Archetypes"] = new[]
            {
                "У каждого в этом городе есть своя работа и своя тайна.",
                "Я не люблю громких слов, но Примум я люблю.",
                "Здесь встречаются все дороги, даже те, что не на карте.",
                "Смотри под ноги: камни клали быстро.",
                "Если тебе скажут, что здесь скучно — не верь.",
                "Просто иду и смотрю, кто чем живёт."
            }
        };

        private static readonly string[] PersonalNotes =
        {
            "Меня зовут {0}, если понадобится помощь — ищи у дома.",
            "Запомни: {0}. Здесь это имя ещё что-то значит.",
            "Я {0}. Живу вон в том доме, у самой стены.",
            "Зови меня {0}. Я редко ухожу далеко.",
            "{0} — так меня зовёт весь квартал.",
            "Я {0}, и я в этом городе надолго.",
            "Если увидишь свет в моём окне, значит я дома. Я {0}.",
            "Меня зовут {0}. Не путай с братом, он крупнее.",
            "{0}. Да, просто {0}, без титулов.",
            "Я {0}, и мне пора по делам — но ты заходи."
        };

        private static List<NpcDefinition> CreateNpcDefinitions(int count, List<GameObject> models)
        {
            var result = new List<NpcDefinition>(count);
            var usedNames = new HashSet<string>();
            var existingGuids = AssetDatabase.FindAssets("t:NpcDefinition", new[] { NpcFolder });
            var existing = new Dictionary<string, NpcDefinition>();
            foreach (var guid in existingGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var def = AssetDatabase.LoadAssetAtPath<NpcDefinition>(path);
                if (def != null && !string.IsNullOrEmpty(def.npcId)) existing[def.npcId] = def;
            }

            for (int i = 0; i < count; i++)
            {
                string npcId = $"primum_crowd_{i + 1:D4}";
                string role = ResolveRole(models[i % models.Count]);
                string fullName = MakeUniqueName(i, usedNames);
                string greeting = BuildGreeting(i, role, fullName);

                if (!existing.TryGetValue(npcId, out var def) || def == null)
                {
                    def = ScriptableObject.CreateInstance<NpcDefinition>();
                    AssetDatabase.CreateAsset(def, $"{NpcFolder}/{npcId}.asset");
                }

                def.npcId = npcId;
                def.displayName = fullName;
                def.faction = FactionId.Neutral;
                def.factionRef = null;
                def.services = NpcService.None;
                def.interactionRadius = 3f;
                def.showGreeting = true;
                def.greetingText = greeting;
                def.animatorTriggerPrefix = string.Empty;
                def.defaultDialogTree = null;
                def.prefab = null;
                EditorUtility.SetDirty(def);
                result.Add(def);
            }

            AssetDatabase.SaveAssets();
            return result;
        }

        private static string MakeUniqueName(int index, HashSet<string> used)
        {
            for (int step = 0; step < 4000; step++)
            {
                string first = FirstNames[(index + step) % FirstNames.Length];
                string last = LastNames[(index * 7 + step * 3 + index / FirstNames.Length) % LastNames.Length];
                string full = first + " " + last;
                if (used.Add(full)) return full;
            }
            return "Житель " + (index + 1);
        }

        private static string BuildGreeting(int index, string role, string fullName)
        {
            if (!RoleObservations.TryGetValue(role, out var pool)) pool = RoleObservations["City"];
            string hello = index % 3 == 0 ? "Доброго дня." : (index % 3 == 1 ? "Здравствуй." : "О, живой человек.");
            string observation = pool[index % pool.Length];
            string personal = string.Format(PersonalNotes[(index * 3) % PersonalNotes.Length], fullName);
            return $"{hello} {observation} {personal}";
        }

        private static string ResolveRole(GameObject model)
        {
            string path = AssetDatabase.GetAssetPath(model);
            const string marker = "01_Characters/";
            int start = path.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0) return "City";
            start += marker.Length;
            int end = path.IndexOf('/', start);
            if (end < 0) return "City";
            string folder = path.Substring(start, end - start);
            return RoleObservations.ContainsKey(folder) ? folder : "City";
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_Project/Data");
            EnsureFolder("Assets/_Project/Data/NPC");
            EnsureFolder("Assets/_Project/Animation");
            EnsureFolder("Assets/_Project/Animation/NPC");
            EnsureFolder("Assets/_Project/Prefabs/NPC/PrimumCrowd");
            EnsureFolder("Assets/_Project/Quests/Data/Npcs/PrimumCrowd");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif
