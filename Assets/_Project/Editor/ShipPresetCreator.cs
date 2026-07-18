#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using ProjectC.Player;
using ProjectC.Ship;
using ProjectC.Ship.Engine;
using ProjectC.Ship.Cargo;
using ProjectC.Ship.Key;
using ProjectC.Ship.Combat;
using ProjectC.PeacefulShip.Stations;
using ProjectC.PeacefulShip.Core;
using ProjectC.AI;
using Unity.AI.Navigation;
using UnityEngine.AI;
using System.IO;

namespace ProjectC.Editor
{
    /// <summary>
    /// ShipPresetCreator — универсальный создатель кораблей (Player + NPC).
    /// 
    /// Создаёт полный префаб корабля со ВСЕМИ компонентами:
    ///   - ShipController (с пресетом класса)
    ///   - ShipFuelSystem, ShipModuleManager, MeziyModuleActivator
    ///   - ShipHull, ShipOwnershipRequirement, ShipModuleVisualApplier
    ///   - NetworkObject, NetworkTransform
    ///   - NpcShipController, NpcProximityZone, ShipDeckNav
    ///   - PilotSeat, Door, Exchanger, CargoVisual, Slot_Engine
    ///   - 9 ModuleSlots (MEZIY_PITCH/ROLL/YAW/THRUST, LIFT_ENH, PITCH_ENH, YAW_ENH, ROLL, cargo)
    /// 
    /// А также автосоздаёт зависимые ассеты:
    ///   - Key ItemData (Resources/Items/)
    ///   - ShipDamageConfig (Resources/Ship_hull/) — для каждого класса
    ///   - Пустой NpcShipSchedule (Resources/PeacefulShip/)
    ///   - NavMeshSurface asset (Prefabs/Ships/)
    /// 
    /// Паттерн визуала: корень scale=(1,1,1), MainVisual дочерний объект.
    /// Документация: docs/world/PLACEMENT_SCRIPTS/Ships/README.md
    /// </summary>
    public class ShipPresetCreator : EditorWindow
    {
        private ShipFlightClass _selectedClass = ShipFlightClass.Medium;
        private string _shipName = "Ship_Medium";

        [MenuItem("Tools/Project C/Create Ship Preset", false, 200)]
        public static void ShowWindow()
        {
            var window = GetWindow<ShipPresetCreator>("Create Ship Preset");
            window.minSize = new Vector2(400, 220);
            window.ShowUtility();
        }

        private void OnGUI()
        {
            GUILayout.Label("Универсальный создатель корабля", EditorStyles.boldLabel);
            GUILayout.Space(4);
            GUILayout.Label("Создаёт префаб со всеми компонентами (Player + NPC).\n"
                + "Визуал настраивается дизайнером после создания.",
                EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(10);

            GUILayout.Label("Класс корабля (физика полёта):", EditorStyles.boldLabel);
            string[] classNames = System.Enum.GetNames(typeof(ShipFlightClass));
            int idx = System.Array.IndexOf(classNames, _selectedClass.ToString());
            idx = EditorGUILayout.Popup(idx, classNames);
            if (idx >= 0 && idx < classNames.Length)
                _selectedClass = (ShipFlightClass)System.Enum.Parse(typeof(ShipFlightClass), classNames[idx]);

            GUILayout.Space(6);

            GUILayout.Label("Имя корабля (префаб и GameObject):", EditorStyles.boldLabel);
            _shipName = EditorGUILayout.TextField(_shipName);

            GUILayout.Space(12);

            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
            if (GUILayout.Button("Создать префаб корабля", GUILayout.Height(34)))
            {
                if (EditorApplication.isPlaying)
                {
                    EditorUtility.DisplayDialog("Ошибка", "Выйдите из Play Mode перед созданием.", "OK");
                    return;
                }
                if (string.IsNullOrWhiteSpace(_shipName))
                {
                    EditorUtility.DisplayDialog("Ошибка", "Введите имя корабля.", "OK");
                    return;
                }

                CreateShipPreset(_shipName, _selectedClass);
                Close();
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Отмена")) Close();
        }

        // ================================================================
        // Константы путей
        // ================================================================
        private const string PrefabFolder      = "Assets/_Project/Prefabs/Ships";
        private const string KeyItemFolder     = "Assets/_Project/Resources/Items";
        private const string DamageCfgFolder   = "Assets/_Project/Resources/Ship_hull";
        private const string ScheduleFolder    = "Assets/_Project/Resources/PeacefulShip";
        private const string NavMeshFolder     = "Assets/_Project/Prefabs/Ships";
        private const string URP_LitMat        = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Lit.mat";

        // ================================================================
        // Пресеты параметров по классам (из анализа лайв-кораблей + ShipController.ApplyShipClass)
        // ================================================================
        private struct ClassPreset
        {
            public float thrustForce, maxSpeed, yawForce, pitchForce, verticalForce;
            public float yawSmooth, pitchSmooth, liftSmooth, thrustSmooth, yawDecay;
            public float windExposure;
            public float massMultiplier;
            public float fuelMax, fuelConsumption;
            public int   hullHP, modulePower;
            public Vector3 visualScale;
            public Color  classColor;
        }

        private static readonly ClassPreset[] Presets = new ClassPreset[]
        {
            // Light
            new ClassPreset {
                thrustForce=5000f, maxSpeed=5000f, yawForce=70000f, pitchForce=25f, verticalForce=7000f,
                yawSmooth=0.25f, pitchSmooth=0.6f, liftSmooth=0.8f, thrustSmooth=0.2f, yawDecay=0.8f,
                windExposure=1.2f, massMultiplier=15f,
                fuelMax=50f, fuelConsumption=0.5f, hullHP=100, modulePower=100,
                visualScale=new Vector3(6f, 1f, 12f), classColor=new Color(0.3f,0.8f,0.3f)
            },
            // Medium
            new ClassPreset {
                thrustForce=30000f, maxSpeed=400f, yawForce=150000f, pitchForce=20f, verticalForce=30000f,
                yawSmooth=0.3f, pitchSmooth=0.7f, liftSmooth=1.0f, thrustSmooth=0.3f, yawDecay=1.0f,
                windExposure=1.0f, massMultiplier=10f,
                fuelMax=100f, fuelConsumption=0.8f, hullHP=200, modulePower=200,
                visualScale=new Vector3(8f,1.5f,15f), classColor=new Color(0.8f,0.3f,0.3f)
            },
            // Heavy
            new ClassPreset {
                thrustForce=100000f, maxSpeed=70f, yawForce=200000f, pitchForce=15f, verticalForce=50000f,
                yawSmooth=0.5f, pitchSmooth=0.9f, liftSmooth=1.2f, thrustSmooth=0.4f, yawDecay=1.5f,
                windExposure=0.7f, massMultiplier=25f,
                fuelMax=200f, fuelConsumption=1.2f, hullHP=400, modulePower=300,
                visualScale=new Vector3(11f,1.5f,19f), classColor=new Color(0.3f,0.3f,0.8f)
            },
            // HeavyII
            new ClassPreset {
                thrustForce=65000f, maxSpeed=100f, yawForce=500000f, pitchForce=0f, verticalForce=120000f,
                yawSmooth=0.7f, pitchSmooth=1.1f, liftSmooth=1.5f, thrustSmooth=0.5f, yawDecay=2.0f,
                windExposure=0.5f, massMultiplier=25f,
                fuelMax=300f, fuelConsumption=1.5f, hullHP=600, modulePower=400,
                visualScale=new Vector3(13.3f,1f,22f), classColor=new Color(0.8f,0.8f,0.3f)
            },
        };

        // ================================================================
        // Главный метод
        // ================================================================
        private static void CreateShipPreset(string shipName, ShipFlightClass shipClass)
        {
            int classIdx = (int)shipClass;
            var p = Presets[classIdx];
            string classStr = shipClass.ToString();

            // Ensure folders
            EnsureFolder(PrefabFolder);
            EnsureFolder(KeyItemFolder);
            EnsureFolder(DamageCfgFolder);
            EnsureFolder(ScheduleFolder);
            EnsureFolder(NavMeshFolder);

            // --- Phase 1: Create dependent assets ---
            string keyPath      = CreateKeyItemData(shipName, classStr);
            string damagePath   = CreateDamageConfig(classStr, p);
            string schedulePath = CreateEmptySchedule(classStr);
            // NavMeshData created AFTER hierarchy is built (needs baking)

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Load created assets for linking
            var keyItem      = AssetDatabase.LoadAssetAtPath<ProjectC.Items.ItemData>(keyPath);
            var damageCfg    = AssetDatabase.LoadAssetAtPath<ShipDamageConfig>(damagePath);
            var schedule     = AssetDatabase.LoadAssetAtPath<NpcShipSchedule>(schedulePath);

            // --- Phase 2: Build hierarchy ---
            var root = new GameObject(shipName);
            root.tag = "Ship";
            root.transform.position = Vector3.zero;
            root.transform.localScale = Vector3.one;

            // --- Root components ---
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 1000f;
            rb.linearDamping = 0.4f;
            rb.angularDamping = 8.0f;
            rb.useGravity = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

            var netObj = root.AddComponent<NetworkObject>();

            var sc = root.AddComponent<ShipController>();
            SetPrivateField(sc, "shipFlightClass", shipClass);
            SetPrivateField(sc, "_customDisplayName", shipName);
            SetPrivateField(sc, "_keyItemData", keyItem);
            // Flight params
            SetPrivateField(sc, "thrustForce",          p.thrustForce);
            SetPrivateField(sc, "maxSpeed",             p.maxSpeed);
            SetPrivateField(sc, "yawForce",             p.yawForce);
            SetPrivateField(sc, "pitchForce",           p.pitchForce);
            SetPrivateField(sc, "verticalForce",        p.verticalForce);
            SetPrivateField(sc, "yawSmoothTime",        p.yawSmooth);
            SetPrivateField(sc, "pitchSmoothTime",      p.pitchSmooth);
            SetPrivateField(sc, "liftSmoothTime",       p.liftSmooth);
            SetPrivateField(sc, "thrustSmoothTime",     p.thrustSmooth);
            SetPrivateField(sc, "yawDecayTime",         p.yawDecay);
            SetPrivateField(sc, "windExposure",         p.windExposure);
            SetPrivateField(sc, "massMultiplier",       p.massMultiplier);
            SetPrivateField(sc, "linearDrag",           0.4f);
            SetPrivateField(sc, "angularDrag",          8.0f);
            SetPrivateField(sc, "antiGravity",          1.0f);
            SetPrivateField(sc, "autoStabilize",        true);
            SetPrivateField(sc, "pitchStabForce",       15f);
            SetPrivateField(sc, "rollStabForce",        20f);
            SetPrivateField(sc, "maxPitchAngle",        20f);
            SetPrivateField(sc, "windInfluence",        0.5f);
            SetPrivateField(sc, "windDecayTime",        1.5f);
            SetPrivateField(sc, "_globalWindEnabled",   true);
            SetPrivateField(sc, "_globalWindForceScale", 8f);
            SetPrivateField(sc, "_globalWindVerticalFactor", 0f);
            SetPrivateField(sc, "baseMaxCargoSlots",    4);
            SetPrivateField(sc, "baseMaxCargoWeight",   100f);
            SetPrivateField(sc, "baseMaxCargoVolume",   3f);
            SetPrivateField(sc, "baseCargoPenaltyFactor", 0.05f);

            // NetworkTransform
            var nt = root.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            nt.Interpolate = true;

            // ShipFuelSystem
            var fuel = root.AddComponent<ShipFuelSystem>();
            SetPrivateField(fuel, "currentFuel",         p.fuelMax);
            SetPrivateField(fuel, "maxFuel",             p.fuelMax);
            SetPrivateField(fuel, "fuelConsumptionRate", p.fuelConsumption);
            SetPrivateField(fuel, "fuelRegenRate",       0.3f);
            SetPrivateField(fuel, "startEngineConsumption", 0.1f);
            SetPrivateField(fuel, "idleConsumptionRate", 0.05f);
            SetPrivateField(fuel, "atmosphericRefuelRate", 2.0f);
            SetPrivateField(fuel, "thrustPenaltyDuringRefuel", 0.5f);
            SetPrivateField(fuel, "speedPenaltyDuringRefuel", 0.7f);

            // ShipModuleManager
            var modMgr = root.AddComponent<ShipModuleManager>();
            SetPrivateField(modMgr, "availablePower", p.modulePower);

            // MeziyModuleActivator
            var mez = root.AddComponent<MeziyModuleActivator>();
            SetPrivateField(mez, "overheatThreshold", 10f);
            SetPrivateField(mez, "cooldownDuration", 15f);
            SetPrivateField(mez, "passiveModifier", 1.1f);
            SetPrivateField(mez, "fuelSystem", fuel);
            SetPrivateField(mez, "moduleManager", modMgr);

            // ShipHull
            var hull = root.AddComponent<ShipHull>();
            SetPrivateField(hull, "_damageConfig", damageCfg);
            SetPrivateField(hull, "_debugLog", false);

            // ShipModuleVisualApplier
            root.AddComponent<ShipModuleVisualApplier>();

            // ShipOwnershipRequirement (авто-добавляется в ShipController.Awake, но для чистоты префаба)
            root.AddComponent<ShipOwnershipRequirement>();

            // ShipInputReader
            var inputReader = root.AddComponent<ShipInputReader>();
            SetPrivateField(inputReader, "mouseSensitivityX", 2f);
            SetPrivateField(inputReader, "mouseSensitivityY", 2f);

            // ShipRootReference (на корне, после всех Player-компонентов)
            var rootSrr = root.AddComponent<ShipRootReference>();
            SetPrivateField(rootSrr, "_shipController", sc);
            SetPrivateField(rootSrr, "_rigidbody", rb);
            SetPrivateField(rootSrr, "_networkObject", netObj);
            SetPrivateField(rootSrr, "_root", root.transform);

            // --- NPC components (на том же root) ---
            var npcCtrl = root.AddComponent<NpcShipController>();
            SetPrivateField(npcCtrl, "npcInstanceId", 0UL);
            SetPrivateField(npcCtrl, "npcThrustMult", 0.6f);
            SetPrivateField(npcCtrl, "npcYawMult", 0.4f);
            SetPrivateField(npcCtrl, "npcArrivalToleranceMeters", 50f);
            SetPrivateField(npcCtrl, "antiGravityBoostDuration", 5f);
            SetPrivateField(npcCtrl, "antiGravityBoostValue", 1.5f);
            SetPrivateField(npcCtrl, "debugMode", false);

            var proxZone = root.AddComponent<NpcProximityZone>();
            SetPrivateField(proxZone, "awarenessRadius", 400f);
            SetPrivateField(proxZone, "avoidanceRadius", 120f);
            SetPrivateField(proxZone, "clearHysteresis", 1.5f);
            SetPrivateField(proxZone, "drawGizmos", true);

            // NpcSpawner (на том же root)
            var npcSpawner = root.AddComponent<NpcSpawner>();
            SetPrivateField(npcSpawner, "_config",
                AssetDatabase.LoadAssetAtPath<ProjectC.AI.NpcSpawnerConfig>(
                    "Assets/_Project/Resources/AI/NpcSpawner_ship_deck.asset"));

            var deckNav = root.AddComponent<ShipDeckNav>();
            SetPrivateField(deckNav, "_registerServerOnly", true);
            SetPrivateField(deckNav, "_navFrameSeparation", 5000f);
            SetPrivateField(deckNav, "_registerUnderShip", true);

            // Wire references on ShipController
            SetPrivateField(sc, "moduleManager", modMgr);
            SetPrivateField(sc, "meziyActivator", mez);
            SetPrivateField(sc, "fuelSystem", fuel);

            // --- Children ---

            // Platform — единый визуал + твёрдый коллайдер палубы (аналог Ship_Light_root).
            // Cube с BoxCollider (isTrigger=false), Layer Default, position (0,0,0).
            var platform = CreateChildCube(root, "Platform",
                Vector3.zero, Vector3.zero,
                new Vector3(p.visualScale.x, 0.9f, p.visualScale.z), p.classColor);
            platform.layer = LayerMask.NameToLayer("ShipDeck");
            platform.tag = "Ship";
            var platformCol = platform.GetComponent<BoxCollider>();
            if (platformCol == null) platformCol = platform.AddComponent<BoxCollider>();
            platformCol.isTrigger = false;
            platform.AddComponent<ShipRootReference>();
            WireShipRootReference(platform, root, sc, rb, netObj);

            // PilotSeat
            var pilotSeat = CreateChildCube(root, "PilotSeat",
                new Vector3(0, 1.18f, p.visualScale.z * 0.45f), Vector3.zero,
                new Vector3(1f, 1f, 1f), Color.white);
            var pilotCol = pilotSeat.GetComponent<BoxCollider>();
            pilotCol.isTrigger = true;
            var pilotCtrl = pilotSeat.AddComponent<PilotSeatController>();
            SetPrivateField(pilotCtrl, "seatType", PilotSeatController.PilotSeatType.Pilot);
            SetPrivateField(pilotCtrl, "interactRadius", 7.37f);
            pilotSeat.AddComponent<ShipRootReference>();
            WireShipRootReference(pilotSeat, root, sc, rb, netObj);

            // Door
            var door = CreateChildCube(root, "Door",
                new Vector3(-(p.visualScale.x * 0.3f), 1.25f, 0), Vector3.zero,
                new Vector3(0.04f, 1.49f, 0.13f), Color.white);
            var doorCol = door.GetComponent<BoxCollider>();
            // Дверь — твёрдая (не триггер). DoorController НЕ меняет isTrigger в Awake(),
            // в отличие от PilotSeatController (который принудительно ставит isTrigger=true).
            doorCol.isTrigger = false;
            var doorCtrl = door.AddComponent<DoorController>();
            SetPrivateField(doorCtrl, "slideDirection", new Vector3(0, 0, -1));
            SetPrivateField(doorCtrl, "slideDistance", 0.2f);
            SetPrivateField(doorCtrl, "slideSpeed", 1.5f);
            SetPrivateField(doorCtrl, "startOpen", true);
            door.AddComponent<ShipRootReference>();
            WireShipRootReference(door, root, sc, rb, netObj);
            var doorShake = door.AddComponent<ShipPartShake>();
            SetPrivateField(doorShake, "_frequency", 5f);
            SetPrivateField(doorShake, "_positionAmplitude", new Vector3(0.01f, 0.01f, 0.02f));
            SetPrivateField(doorShake, "_rotationAmplitude", new Vector3(0.5f, 0.3f, 0.5f));
            SetPrivateField(doorShake, "_thrustThreshold", 0.05f);
            SetPrivateField(doorShake, "_smoothTime", 0.4f);

            // DeckNavSurface
            var navSurf = new GameObject("DeckNavSurface");
            navSurf.transform.SetParent(root.transform);
            navSurf.transform.localPosition = new Vector3(0, -0.37f, 0);
            navSurf.transform.localRotation = Quaternion.identity;
            navSurf.transform.localScale = Vector3.one;
            var nms = navSurf.AddComponent<NavMeshSurface>();
            nms.collectObjects = CollectObjects.All;
            nms.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            nms.size = new Vector3(p.visualScale.x * 1.1f, 0.9f * 1.1f, p.visualScale.z * 1.1f);
            nms.center = new Vector3(0, 0.45f, 0);
            nms.ignoreNavMeshAgent = true;
            nms.ignoreNavMeshObstacle = true;
            nms.enabled = false;

            // ShipCargoVisual
            var cargoVis = new GameObject("ShipCargoVisual");
            cargoVis.transform.SetParent(root.transform);
            cargoVis.transform.localPosition = new Vector3(0, 5.86f, 0);
            cargoVis.transform.localRotation = Quaternion.identity;
            cargoVis.transform.localScale = new Vector3(p.visualScale.x * 0.9f, 10f, p.visualScale.z * 0.75f);
            var cargoVisBox = cargoVis.AddComponent<BoxCollider>();
            cargoVisBox.isTrigger = true;
            var cargoVisComp = cargoVis.AddComponent<ShipCargoVisual>();
            SetPrivateField(cargoVisComp, "_spawnZone", cargoVisBox);
            SetPrivateField(cargoVisComp, "_boxBaseSize", 0.5f);
            SetPrivateField(cargoVisComp, "_boxGap", 0.1f);
            SetPrivateField(cargoVisComp, "_maxVisibleBoxes", 50);
            SetPrivateField(cargoVisComp, "_debugLog", false);
            SetPrivateField(cargoVisComp, "_showOverflowIndicator", true);
            cargoVis.AddComponent<ShipRootReference>();
            WireShipRootReference(cargoVis, root, sc, rb, netObj);

            // Exchanger (Cargo Console)
            var exchanger = new GameObject("Exchanger");
            exchanger.transform.SetParent(root.transform);
            exchanger.transform.localPosition = new Vector3(0, 0.57f, -(p.visualScale.z * 0.3f));
            exchanger.transform.localRotation = Quaternion.identity;
            exchanger.transform.localScale = new Vector3(0.4f, 1f, 0.07f);
            var exchFilter = exchanger.AddComponent<MeshFilter>();
            exchFilter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            var exchRenderer = exchanger.AddComponent<MeshRenderer>();
            exchRenderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(URP_LitMat);
            var exchCol = exchanger.AddComponent<SphereCollider>();
            exchCol.isTrigger = true;
            exchCol.radius = 1.35f;
            exchanger.AddComponent<ShipRootReference>();
            WireShipRootReference(exchanger, root, sc, rb, netObj);
            var exchConsole = exchanger.AddComponent<ShipCargoConsole>();
            SetPrivateField(exchConsole, "_interactionRadius", 1.35f);
            SetPrivateField(exchConsole, "_displayName", "Грузовой отсек");

            // Slot_Engine (with visuals)
            var slotEngine = new GameObject("Slot_Engine");
            slotEngine.transform.SetParent(root.transform);
            slotEngine.transform.localPosition = new Vector3(0, 0, -(p.visualScale.z * 0.47f));
            slotEngine.transform.localRotation = Quaternion.identity;
            slotEngine.transform.localScale = new Vector3(0.1667f, 1.111f, 0.0833f);
            var engineSlot = slotEngine.AddComponent<ModuleSlot>();
            engineSlot.slotType = SlotType.Engine;
            var engineVis = slotEngine.AddComponent<EngineThrusterVisual>();
            SetPrivateField(engineVis, "_maxRpm", 10f);
            SetPrivateField(engineVis, "_rotationAxis", new Vector3(0, 0, 1));
            SetPrivateField(engineVis, "_maxDeflectionAngle", 40f);
            SetPrivateField(engineVis, "_deflectionSmoothTime", 0.3f);
            var engineShake = slotEngine.AddComponent<ShipPartShake>();
            SetPrivateField(engineShake, "_frequency", 15f);
            SetPrivateField(engineShake, "_positionAmplitude", new Vector3(0.01f, 0.01f, 0.02f));
            SetPrivateField(engineShake, "_rotationAmplitude", new Vector3(0.5f, 0.3f, 0.5f));
            SetPrivateField(engineShake, "_thrustThreshold", 0.05f);
            SetPrivateField(engineShake, "_smoothTime", 1.5f);

            // Engine children: RotationAnchor + EngineVisuals
            var rotAnchor = new GameObject("RotationAnchor");
            rotAnchor.transform.SetParent(slotEngine.transform);
            rotAnchor.transform.localPosition = Vector3.zero;
            rotAnchor.transform.localRotation = Quaternion.identity;
            rotAnchor.transform.localScale = Vector3.one;

            var engVisuals = new GameObject("EngineVisuals");
            engVisuals.transform.SetParent(slotEngine.transform);
            engVisuals.transform.localPosition = Vector3.zero;
            engVisuals.transform.localRotation = Quaternion.identity;
            engVisuals.transform.localScale = Vector3.one;

            var cyl = CreateChildCube(engVisuals, "Cylinder",
                Vector3.zero, Vector3.zero, Vector3.one, Color.gray);
            DestroyImmediate(cyl.GetComponent<BoxCollider>());
            cyl.AddComponent<CapsuleCollider>();

            var ecube = CreateChildCube(engVisuals, "Cube",
                Vector3.zero, Vector3.zero, Vector3.one, Color.gray);

            // Wire engine visual references
            SetPrivateField(engineVis, "_propeller", ecube.transform);
            SetPrivateField(engineVis, "_pivotPoint", rotAnchor.transform);
            SetPrivateField(engineVis, "_visuals", engVisuals.transform);

            // --- 9 ModuleSlots ---
            CreateModuleSlot(root, "Slot_MODULE_LIFT_ENH",   new Vector3(4f, 1.5f, 0),     SlotType.Propulsion);
            CreateModuleSlot(root, "Slot_MODULE_MEZIY_PITCH", new Vector3(1.5f, 1.5f, 2.5f), SlotType.Special);
            CreateModuleSlot(root, "Slot_MODULE_MEZIY_ROLL",  new Vector3(-1.5f, 1.5f, 2.5f),SlotType.Special);
            CreateModuleSlot(root, "Slot_MODULE_MEZIY_THRUST",new Vector3(0, 1.5f, -3.5f),   SlotType.Special);
            CreateModuleSlot(root, "Slot_MODULE_MEZIY_YAW",   new Vector3(2.5f, 1.5f, 0),    SlotType.Special);
            CreateModuleSlot(root, "Slot_MODULE_PITCH_ENH",   new Vector3(-2f, 1.5f, 1.5f),  SlotType.Utility);
            CreateModuleSlot(root, "Slot_MODULE_ROLL",        new Vector3(2f, 1.5f, -1.5f),  SlotType.Utility);
            CreateModuleSlot(root, "Slot_MODULE_YAW_ENH",     new Vector3(-3f, 1.5f, -2f),   SlotType.Utility);
            CreateModuleSlot(root, "Slot_cargo",              new Vector3(0, 2.5f, 2f),      SlotType.Utility);

            // --- Wire ShipModuleManager slots ---
            // (Will be auto-discovered by Initialize in Awake via GetComponentsInChildren)

            // --- Phase 3: Bake deck NavMesh ---
            string navMeshPath = GetNavMeshAssetPath(shipName);
            var navMeshData = BakeDeckNavMesh(root, navMeshPath);
            if (navMeshData != null)
            {
                SetPrivateField(deckNav, "_deckNavMeshData", navMeshData);
            }

            // --- Save as Prefab ---
            string prefabPath = $"{PrefabFolder}/{shipName}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // --- Select & log ---
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"✅ Ship preset created: {prefabPath} (class={classStr})");
            Debug.Log($"   Key:         {keyPath}");
            Debug.Log($"   Damage Cfg:  {damagePath}");
            Debug.Log($"   Schedule:    {schedulePath}");
            Debug.Log($"   NavMesh:     {navMeshPath}");
            Debug.Log($"   ───────────────────────────────");
            Debug.Log($"   Thrust={p.thrustForce}  MaxSpeed={p.maxSpeed}  Yaw={p.yawForce}  Pitch={p.pitchForce}");
            Debug.Log($"   Fuel={p.fuelMax}  HullHP={p.hullHP}  Power={p.modulePower}");
            Debug.Log($"   Visual scale={p.visualScale}");

            EditorUtility.DisplayDialog("Готово",
                $"Префаб создан:\n{prefabPath}\n\n"
                + $"Ключ: {keyPath}\n"
                + $"HullConfig: {damagePath}\n\n"
                + "Дизайнер: замени MainVisual на модель, настрой визуал.",
                "OK");
        }

        // ================================================================
        // Helpers
        // ================================================================

        private static GameObject CreateChildCube(GameObject parent, string name,
            Vector3 localPos, Vector3 localRot, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent.transform);
            go.transform.localPosition = localPos;
            go.transform.localEulerAngles = localRot;
            go.transform.localScale = scale;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(URP_LitMat);
            }
            return go;
        }

        /// <summary>
        /// Назначает поля ShipRootReference на дочернем объекте — ссылки на корневые компоненты.
        /// Вызывается после AddComponent&lt;ShipRootReference&gt;().
        /// </summary>
        private static void WireShipRootReference(GameObject child, GameObject shipRoot,
            ShipController sc, Rigidbody rb, NetworkObject netObj)
        {
            var srr = child.GetComponent<ShipRootReference>();
            if (srr == null) return;
            SetPrivateField(srr, "_shipController", sc);
            SetPrivateField(srr, "_rigidbody", rb);
            SetPrivateField(srr, "_networkObject", netObj);
            SetPrivateField(srr, "_root", shipRoot.transform);
        }

        private static void CreateModuleSlot(GameObject root, string slotName, Vector3 localPos, SlotType slotType)
        {
            var go = new GameObject(slotName);
            go.transform.SetParent(root.transform);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            var slot = go.AddComponent<ModuleSlot>();
            slot.slotType = slotType;
        }

        private static void SetPrivateField(object obj, string fieldName, object value)
        {
            if (obj == null || value == null) return;
            var field = obj.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(obj, value);
        }

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = Path.GetDirectoryName(path).Replace("\\", "/");
                string folder = Path.GetFileName(path);
                if (!AssetDatabase.IsValidFolder(parent))
                    EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, folder);
            }
        }

        // ================================================================
        // Asset creators
        // ================================================================

        private static string CreateKeyItemData(string shipName, string classStr)
        {
            string fileName = $"Key_{shipName}";
            string path = $"{KeyItemFolder}/{fileName}.asset";

            if (AssetDatabase.LoadAssetAtPath<ProjectC.Items.ItemData>(path) != null)
                return path; // already exists

            var item = ScriptableObject.CreateInstance<ProjectC.Items.ItemData>();
            item.itemName = fileName;
            item.itemType = ProjectC.Items.ItemType.Key;
            item.description = $"Ключ корабля «{shipName}» ({classStr})";
            item.maxStack = 20;
            item.weightKg = 1.0f;
            item.equipSlot = ProjectC.Equipment.EquipSlot.None;

            AssetDatabase.CreateAsset(item, path);
            return path;
        }

        private static string CreateDamageConfig(string classStr, ClassPreset p)
        {
            string fileName = $"ShipDamage{classStr}";
            string path = $"{DamageCfgFolder}/{fileName}.asset";

            if (AssetDatabase.LoadAssetAtPath<ShipDamageConfig>(path) != null)
                return path;

            var cfg = ScriptableObject.CreateInstance<ShipDamageConfig>();
            cfg.maxHullLight   = Presets[0].hullHP;
            cfg.maxHullMedium  = Presets[1].hullHP;
            cfg.maxHullHeavy   = Presets[2].hullHP;
            cfg.maxHullHeavyII = Presets[3].hullHP;
            cfg.armorHull = 5;
            cfg.collisionEnergyThreshold = 8f;
            cfg.collisionDamageCoefficient = 0.05f;
            cfg.collisionDamageCap = 5;
            cfg.minCollisionRelativeSpeed = 3f;
            cfg.postUndockGraceSeconds = 3f;
            cfg.brokenSpeedMultiplier = 0.1f;
            cfg.repairCostCredits = 300;
            cfg.verboseLogging = true;

            AssetDatabase.CreateAsset(cfg, path);
            return path;
        }

        private static string CreateEmptySchedule(string classStr)
        {
            string fileName = $"NpcShipSchedule_{classStr}_Default";
            string path = $"{ScheduleFolder}/{fileName}.asset";

            if (AssetDatabase.LoadAssetAtPath<NpcShipSchedule>(path) != null)
                return path;

            var sched = ScriptableObject.CreateInstance<NpcShipSchedule>();
            sched.scheduleId = $"SCH-NPC-{classStr.ToUpperInvariant()}-DEFAULT";
            sched.displayName = $"Расписание {classStr} (пустое)";
            sched.scheduleType = NpcShipSchedule.ScheduleType.RoundTrip;
            sched.routes = new NpcShipRoute[0];
            sched.meanArrivalIntervalSec = 480f;
            sched.arrivalIntervalStdDev = 90f;
            sched.minArrivalSpacingSec = 60f;
            sched.minDwellTimeSec = 60f;
            sched.maxDwellTimeSec = 90f;

            AssetDatabase.CreateAsset(sched, path);
            return path;
        }

        private static string GetNavMeshAssetPath(string shipName)
        {
            string navDir = $"{NavMeshFolder}/NavMesh-DeckNavSurface_{shipName}";
            EnsureFolder(navDir);
            return $"{navDir}/NavMesh-DeckNavSurface.asset";
        }

        /// <summary>
        /// Включает NavMeshSurface на DeckNavSurface, печёт навмеш по MainVisual+BoxCollider,
        /// сохраняет NavMeshData в ассет, выключает Surface обратно.
        /// Возвращает запечённый NavMeshData (или null при ошибке).
        /// </summary>
        private static NavMeshData BakeDeckNavMesh(GameObject root, string assetPath)
        {
            var navSurf = root.transform.Find("DeckNavSurface");
            if (navSurf == null)
            {
                Debug.LogError("[ShipPresetCreator] DeckNavSurface не найден — пропускаем бейк навмеша.");
                return null;
            }

            var nms = navSurf.GetComponent<NavMeshSurface>();
            if (nms == null)
            {
                Debug.LogError("[ShipPresetCreator] NavMeshSurface не найден на DeckNavSurface.");
                return null;
            }

            // Временно включаем для бейка
            nms.enabled = true;
            try
            {
                nms.BuildNavMesh(); // void — результат попадает в nms.navMeshData
            }
            finally
            {
                nms.enabled = false;
            }

            var baked = nms.navMeshData;
            if (baked == null)
            {
                Debug.LogError("[ShipPresetCreator] BuildNavMesh() отработал, но navMeshData = null — проверьте что Platform имеет MeshRenderer + BoxCollider.");
                return null;
            }

            // Удаляем старый ассет если есть, сохраняем новый
            if (AssetDatabase.LoadAssetAtPath<NavMeshData>(assetPath) != null)
                AssetDatabase.DeleteAsset(assetPath);

            AssetDatabase.CreateAsset(baked, assetPath);
            AssetDatabase.SaveAssets();

            // Отвязываем NavMeshData от поверхности — ассет уже сохранён,
            // ссылка на него теперь в ShipDeckNav._deckNavMeshData
            nms.navMeshData = null;

            Debug.Log($"[ShipPresetCreator] NavMesh запечён и сохранён: {assetPath}");
            return AssetDatabase.LoadAssetAtPath<NavMeshData>(assetPath);
        }
    }
}
#endif
