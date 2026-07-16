using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;
using ProjectC.Docking.Core;
using ProjectC.Docking.Network;
using ProjectC.Docking.Stations;
using ProjectC.Docking.Zones;
using ProjectC.Ship;
using ProjectC.Trade.Config;
using ProjectC.Trade.Network;

/// <summary>
/// Editor tool: создаёт заготовку портовой станции («Средняя») со всем необходимым.
/// 
/// Использование:
///   Tools → ProjectC → Create Port Station…
/// 
/// Что создаётся:
///   • DockStationDefinition SO (Assets/_Project/Docking/Resources/Data/)
///   • MarketConfig SO          (Assets/_Project/Trade/Data/Markets/) — копия шаблона с новыми ID
///   • GameObject-иерархия в сцене
/// </summary>
public class PortStationCreator : EditorWindow
{
    private string _namespaceId = "Primium_farm_1_1";
    private string _displayName = "Ферма Примума 1_1";
    private int _padCount = 5;
    private float _padSpacing = 10f;
    private Transform _parent;

    private const string DockDefsPath = "Assets/_Project/Docking/Resources/Data";
    private const string MarketConfigsPath = "Assets/_Project/Trade/Data/Markets";
    private const string TemplateMarketConfig = "Assets/_Project/Trade/Data/Markets/MarketConfig_Primium_farm_0_0.asset";
    private const string ModuleShopDbPath = "Assets/_Project/Data/Modules/ModuleShopDatabase.asset";
    private const string NpcPrefabPath = "Assets/_Project/Prefabs/NPC_ZONES/Npc_peacfull_market_zone Variant.prefab";
    private const string PlaneModelMaterialPath = "Assets/_Project/Models/City/Primum/gorod port.glb";

    [MenuItem("Tools/ProjectC/Create Port Station…", false, 100)]
    public static void ShowWindow() => GetWindow<PortStationCreator>("Port Station Creator");

    private void OnGUI()
    {
        GUILayout.Label("Создание портовой станции", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        _namespaceId = EditorGUILayout.TextField("Namespace / ID", _namespaceId);
        _displayName = EditorGUILayout.TextField("Display Name", _displayName);
        _padCount = EditorGUILayout.IntSlider("Кол-во pads", _padCount, 1, 20);
        _padSpacing = EditorGUILayout.FloatField("Расстояние между pads", _padSpacing);
        _parent = (Transform)EditorGUILayout.ObjectField("Parent (опционально)", _parent, typeof(Transform), true);

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            $"Будет создано:\n" +
            $"• DockStation_{_namespaceId}.asset\n" +
            $"• MarketConfig_{_namespaceId}.asset\n" +
            $"• GO «{_displayName}» + {_padCount} pads + NPC рынка",
            MessageType.Info);

        EditorGUILayout.Space();

        GUI.enabled = !string.IsNullOrWhiteSpace(_namespaceId) && !string.IsNullOrWhiteSpace(_displayName);
        if (GUILayout.Button("Создать", GUILayout.Height(30)))
        {
            Create();
        }
        GUI.enabled = true;
    }

    private void Create()
    {
        var ns = _namespaceId.Trim();
        var display = _displayName.Trim();

        // Derive IDs
        var locationId = ns.ToUpperInvariant();
        var stationId = $"DockStation_{ns}";
        var marketConfigName = $"MarketConfig_{ns}";
        var rootGoName = display;

        // --- 1. Create SO: DockStationDefinition ---
        var dockDef = ScriptableObject.CreateInstance<DockStationDefinition>();
        var dockDefSo = new SerializedObject(dockDef);
        dockDefSo.FindProperty("stationId").stringValue = stationId;
        dockDefSo.FindProperty("locationId").stringValue = locationId;
        dockDefSo.FindProperty("displayName").stringValue = display;
        dockDefSo.FindProperty("maxConcurrentLandings").intValue = 10;
        dockDefSo.FindProperty("landingWindowSeconds").floatValue = 300f;
        dockDefSo.ApplyModifiedProperties();

        EnsureDir(DockDefsPath);
        var dockDefPath = $"{DockDefsPath}/DockStation_{ns}.asset";
        AssetDatabase.CreateAsset(dockDef, dockDefPath);

        // --- 2. Create SO: MarketConfig (copy from template) ---
        EnsureDir(MarketConfigsPath);
        var marketConfigPath = $"{MarketConfigsPath}/{marketConfigName}.asset";
        if (!AssetDatabase.CopyAsset(TemplateMarketConfig, marketConfigPath))
        {
            Debug.LogError($"[PortStationCreator] Failed to copy MarketConfig template to {marketConfigPath}");
            return;
        }

        var marketConfig = AssetDatabase.LoadAssetAtPath<MarketConfig>(marketConfigPath);
        var mcSo = new SerializedObject(marketConfig);
        mcSo.FindProperty("locationId").stringValue = locationId;
        mcSo.FindProperty("displayName").stringValue = display;
        mcSo.ApplyModifiedProperties();
        EditorUtility.SetDirty(marketConfig);

        // --- 3. Load shared assets ---
        var moduleShopDb = AssetDatabase.LoadAssetAtPath<ModuleShopDatabase>(ModuleShopDbPath);
        var planeMaterial = AssetDatabase.LoadAssetAtPath<Material>(PlaneModelMaterialPath);

        // --- 4. Build hierarchy ---
        var root = new GameObject(rootGoName);
        root.transform.position = Vector3.zero;
        root.transform.localScale = Vector3.one;

        if (_parent != null)
            root.transform.SetParent(_parent, false);

        // Plane visual
        var planeFilter = root.AddComponent<MeshFilter>();
        planeFilter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Plane.fbx")
            ?? Resources.GetBuiltinResource<Mesh>("Plane");
        var planeRenderer = root.AddComponent<MeshRenderer>();
        if (planeMaterial != null)
            planeRenderer.sharedMaterial = planeMaterial;

        // Core network + docking components on root
        root.AddComponent<NetworkObject>();
        root.AddComponent<SphereCollider>().isTrigger = true;
        var outerComm = root.AddComponent<OuterCommZone>();
        var dockCtrl = root.AddComponent<DockStationController>();

        // Configure OuterCommZone
        var ocSo = new SerializedObject(outerComm);
        ocSo.FindProperty("stationId").stringValue = stationId;
        ocSo.FindProperty("commRange").floatValue = 1000f;
        ocSo.FindProperty("drawGizmos").boolValue = true;
        ocSo.ApplyModifiedProperties();

        // Configure DockStationController
        var dcSo = new SerializedObject(dockCtrl);
        dcSo.FindProperty("dockStationDefinition").objectReferenceValue = dockDef;
        dcSo.FindProperty("debugMode").boolValue = false;
        dcSo.ApplyModifiedProperties();

        // --- 5. Create Dockings container + RepairManager ---
        var dockings = new GameObject("Dockings");
        dockings.transform.SetParent(root.transform, false);

        var repairManager = new GameObject("RepairManager");
        repairManager.transform.SetParent(dockings.transform, false);
        repairManager.transform.localPosition = new Vector3(0.132f, 0f, -0.155f);
        repairManager.transform.localScale = Vector3.one;

        // Capsule visual
        var capsuleFilter = repairManager.AddComponent<MeshFilter>();
        capsuleFilter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Capsule.fbx")
            ?? Resources.GetBuiltinResource<Mesh>("Capsule");
        repairManager.AddComponent<MeshRenderer>();

        // Colliders
        var capsuleCol = repairManager.AddComponent<CapsuleCollider>();
        capsuleCol.isTrigger = true;
        capsuleCol.radius = 0.5f;
        capsuleCol.height = 2f;

        var sphereCol = repairManager.AddComponent<SphereCollider>();
        sphereCol.radius = 1f;

        // RepairManager + StationRootReference
        var repMgr = repairManager.AddComponent<RepairManager>();
        var repMgrSo = new SerializedObject(repMgr);
        repMgrSo.FindProperty("_shopDatabase").objectReferenceValue = moduleShopDb;
        repMgrSo.FindProperty("_repaintCost").intValue = 500;
        repMgrSo.FindProperty("_interactionRadius").floatValue = 20.76f;
        repMgrSo.FindProperty("_interactHint").stringValue = "🛠 Ремонтный менеджер [E]";
        repMgrSo.ApplyModifiedProperties();

        repairManager.AddComponent<StationRootReference>();

        // --- 6. Create Pads ---
        for (int i = 1; i <= _padCount; i++)
        {
            var padName = $"Pad_{i:D2}";
            var pad = new GameObject(padName);
            pad.transform.SetParent(root.transform, false);
            pad.transform.localPosition = new Vector3(
                -24.086f + (i - 1) * _padSpacing,
                0f,
                7.34f);
            pad.transform.localScale = Vector3.one;

            // BoxCollider trigger
            var boxCol = pad.AddComponent<BoxCollider>();
            boxCol.isTrigger = true;
            boxCol.size = new Vector3(8f, 3f, 8f);

            // Docking components
            var triggerBox = pad.AddComponent<DockingPadTriggerBox>();
            var tbSo = new SerializedObject(triggerBox);
            tbSo.FindProperty("padId").stringValue = padName;
            tbSo.ApplyModifiedProperties();

            pad.AddComponent<PadTriggerReference>();
            var visualMarker = pad.AddComponent<DockPadVisualMarker>();
            var vmSo = new SerializedObject(visualMarker);
            vmSo.FindProperty("freeColor").colorValue = new Color(0.2f, 1f, 0.2f, 0.25f);
            vmSo.FindProperty("occupiedColor").colorValue = new Color(1f, 0.2f, 0.2f, 0.35f);
            vmSo.FindProperty("markerSize").floatValue = 6f;
            vmSo.FindProperty("markerHeight").floatValue = 0.05f;
            vmSo.ApplyModifiedProperties();

            // TMP label child
            var label = new GameObject(padName);
            label.transform.SetParent(pad.transform, false);
            label.transform.localPosition = Vector3.up * 5f;
            var tmp = label.AddComponent<TMPro.TextMeshPro>();
            tmp.text = padName;
            tmp.fontSize = 18;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;

            // Empty visual child
            var emptyVis = new GameObject("Empty_visual");
            emptyVis.transform.SetParent(pad.transform, false);
            emptyVis.AddComponent<MeshFilter>();
            emptyVis.AddComponent<MeshRenderer>();
            var evCol = emptyVis.AddComponent<BoxCollider>();
            evCol.isTrigger = true;
        }

        // --- 7. Create Market zone + NPC ---
        var marketZone = new GameObject($"Market_zone_{ns}");
        marketZone.transform.SetParent(root.transform, false);

        var npcPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NpcPrefabPath);
        GameObject npcInstance;
        if (npcPrefab != null)
        {
            npcInstance = (GameObject)PrefabUtility.InstantiatePrefab(npcPrefab, marketZone.transform);
        }
        else
        {
            Debug.LogWarning($"[PortStationCreator] NPC prefab not found at {NpcPrefabPath}, creating empty");
            npcInstance = new GameObject("Npc_peacfull_market_zone");
            npcInstance.transform.SetParent(marketZone.transform, false);
        }

        npcInstance.name = "Npc_peacfull_market_zone";
        npcInstance.transform.localPosition = Vector3.zero;
        npcInstance.transform.localScale = Vector3.one;

        // Point NPC's MarketZone to the new MarketConfig
        var mz = npcInstance.GetComponent<MarketZone>();
        if (mz != null)
        {
            var mzSo = new SerializedObject(mz);
            mzSo.FindProperty("_marketConfig").objectReferenceValue = marketConfig;
            mzSo.ApplyModifiedProperties();
        }

        // --- 8. Selection & save ---
        Selection.activeGameObject = root;
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[PortStationCreator] Станция «{display}» создана.\n" +
                  $"  DockStationDefinition: {dockDefPath}\n" +
                  $"  MarketConfig: {marketConfigPath}\n" +
                  $"  GameObject: {rootGoName}\n" +
                  $"  Pads: {_padCount}\n" +
                  $"  NPC: {npcInstance.name}");
    }

    private static void EnsureDir(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }
}
