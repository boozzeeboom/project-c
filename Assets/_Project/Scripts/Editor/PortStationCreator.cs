using System.IO;
using UnityEditor;
using UnityEngine;
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
    private float _commRange = 1000f;
    private float _tradeRadius = 5f;
    private float _shipDockRadius = 240f;
    private Transform _parent;

    private const string DockDefsPath = "Assets/_Project/Docking/Resources/Data";
    private const string MarketConfigsPath = "Assets/_Project/Trade/Data/Markets";
    private const string TemplateMarketConfig = "Assets/_Project/Trade/Data/Markets/MarketConfig_Primium_farm_0_0.asset";
    private const string ModuleShopDbPath = "Assets/_Project/Data/Modules/ModuleShopDatabase.asset";
    private const string NpcPrefabPath = "Assets/_Project/Prefabs/NPC_ZONES/Npc_peacfull_market_zone Variant.prefab";

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
        _commRange = EditorGUILayout.FloatField("Comm Range (OuterCommZone)", _commRange);
        _tradeRadius = EditorGUILayout.FloatField("Trade Radius (MarketZone)", _tradeRadius);
        _shipDockRadius = EditorGUILayout.FloatField("Ship Dock Radius (MarketZone)", _shipDockRadius);
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

        var locationId = ns.ToUpperInvariant();
        var stationId = $"DockStation_{ns}";
        var marketConfigName = $"MarketConfig_{ns}";

        // ============================================================
        // 1. DockStationDefinition SO
        // ============================================================
        EnsureDir(DockDefsPath);
        var dockDefPath = $"{DockDefsPath}/DockStation_{ns}.asset";
        AssetDatabase.DeleteAsset(dockDefPath);

        var dockDef = ScriptableObject.CreateInstance<DockStationDefinition>();
        using (var so = new SerializedObject(dockDef))
        {
            so.FindProperty("stationId").stringValue = stationId;
            so.FindProperty("locationId").stringValue = locationId;
            so.FindProperty("displayName").stringValue = display;
            so.FindProperty("maxConcurrentLandings").intValue = 10;
            so.FindProperty("landingWindowSeconds").floatValue = 300f;
            so.ApplyModifiedProperties();
        }
        AssetDatabase.CreateAsset(dockDef, dockDefPath);
        AssetDatabase.SaveAssets();

        // ============================================================
        // 2. MarketConfig SO (copy template → override IDs)
        // ============================================================
        EnsureDir(MarketConfigsPath);
        var marketConfigPath = $"{MarketConfigsPath}/{marketConfigName}.asset";
        AssetDatabase.DeleteAsset(marketConfigPath);

        if (!AssetDatabase.CopyAsset(TemplateMarketConfig, marketConfigPath))
        {
            Debug.LogError($"[PortStationCreator] Failed to copy MarketConfig to {marketConfigPath}");
            return;
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var marketConfig = AssetDatabase.LoadAssetAtPath<MarketConfig>(marketConfigPath);
        if (marketConfig == null)
        {
            Debug.LogError($"[PortStationCreator] Failed to load MarketConfig at {marketConfigPath}");
            return;
        }

        using (var so = new SerializedObject(marketConfig))
        {
            so.FindProperty("locationId").stringValue = locationId;
            so.FindProperty("displayName").stringValue = display;
            so.ApplyModifiedProperties();
        }
        EditorUtility.SetDirty(marketConfig);
        AssetDatabase.SaveAssets();

        // ============================================================
        // 3. Load shared assets
        // ============================================================
        var moduleShopDb = AssetDatabase.LoadAssetAtPath<ModuleShopDatabase>(ModuleShopDbPath);

        // ============================================================
        // 4. Root GameObject
        // ============================================================
        var root = new GameObject(display);
        root.transform.position = Vector3.zero;
        root.transform.localScale = Vector3.one;

        if (_parent != null)
        {
            root.transform.SetParent(_parent, true);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            var pls = _parent.lossyScale;
            root.transform.localScale = new Vector3(1f / pls.x, 1f / pls.y, 1f / pls.z);
        }

        // Plane visual
        var planeFilter = root.AddComponent<MeshFilter>();
        planeFilter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Plane.fbx")
            ?? Resources.GetBuiltinResource<Mesh>("Plane");
        root.AddComponent<MeshRenderer>();

        // NetworkObject + SphereCollider (commRange radius)
        root.AddComponent<NetworkObject>();
        var sphereCol = root.AddComponent<SphereCollider>();
        sphereCol.isTrigger = true;
        sphereCol.radius = _commRange;

        // OuterCommZone
        var outerComm = root.AddComponent<OuterCommZone>();
        using (var so = new SerializedObject(outerComm))
        {
            so.FindProperty("stationId").stringValue = stationId;
            so.FindProperty("commRange").floatValue = _commRange;
            so.FindProperty("drawGizmos").boolValue = true;
            so.ApplyModifiedProperties();
        }
        EditorUtility.SetDirty(outerComm);

        // DockStationController
        var dockCtrl = root.AddComponent<DockStationController>();
        using (var so = new SerializedObject(dockCtrl))
        {
            so.FindProperty("dockStationDefinition").objectReferenceValue = dockDef;
            so.FindProperty("debugMode").boolValue = false;
            so.ApplyModifiedProperties();
        }
        EditorUtility.SetDirty(dockCtrl);

        // ============================================================
        // 5. Dockings container + RepairManager
        // ============================================================
        var dockings = new GameObject("Dockings");
        dockings.transform.SetParent(root.transform, false);

        var repairManager = new GameObject("RepairManager");
        repairManager.transform.SetParent(dockings.transform, false);
        repairManager.transform.localPosition = new Vector3(0.132f, 0f, -0.155f);
        repairManager.transform.localScale = Vector3.one;

        var capsuleFilter = repairManager.AddComponent<MeshFilter>();
        capsuleFilter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Capsule.fbx")
            ?? Resources.GetBuiltinResource<Mesh>("Capsule");
        var rmRenderer = repairManager.AddComponent<MeshRenderer>();
        rmRenderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Lit.mat");

        var capsuleCol = repairManager.AddComponent<CapsuleCollider>();
        capsuleCol.isTrigger = true;
        capsuleCol.radius = 0.5f;
        capsuleCol.height = 2f;

        var rmSphereCol = repairManager.AddComponent<SphereCollider>();
        rmSphereCol.radius = 1f;

        var repMgr = repairManager.AddComponent<RepairManager>();
        using (var so = new SerializedObject(repMgr))
        {
            so.FindProperty("_shopDatabase").objectReferenceValue = moduleShopDb;
            so.FindProperty("_repaintCost").intValue = 500;
            so.FindProperty("_interactionRadius").floatValue = 20.76f;
            so.FindProperty("_interactHint").stringValue = "🛠 Ремонтный менеджер [E]";
            so.ApplyModifiedProperties();
        }
        EditorUtility.SetDirty(repMgr);

        repairManager.AddComponent<StationRootReference>();

        // ============================================================
        // 6. Pads
        // ============================================================
        for (int i = 1; i <= _padCount; i++)
        {
            var padName = $"Pad_{i:D2}";
            var pad = new GameObject(padName);
            pad.transform.SetParent(root.transform, false);
            pad.transform.localPosition = new Vector3(-24.086f + (i - 1) * _padSpacing, 0f, 7.34f);
            pad.transform.localScale = Vector3.one;

            var boxCol = pad.AddComponent<BoxCollider>();
            boxCol.isTrigger = true;
            boxCol.size = new Vector3(8f, 3f, 8f);

            // DockingPadTriggerBox: padId + все 4 класса кораблей
            var triggerBox = pad.AddComponent<DockingPadTriggerBox>();
            using (var so = new SerializedObject(triggerBox))
            {
                so.FindProperty("padId").stringValue = padName;
                var compat = so.FindProperty("compatibleShipClasses");
                compat.arraySize = 4;
                compat.GetArrayElementAtIndex(0).enumValueIndex = 0; // Light
                compat.GetArrayElementAtIndex(1).enumValueIndex = 1; // Medium
                compat.GetArrayElementAtIndex(2).enumValueIndex = 2; // Heavy
                compat.GetArrayElementAtIndex(3).enumValueIndex = 3; // HeavyII
                so.ApplyModifiedProperties();
            }
            EditorUtility.SetDirty(triggerBox);

            pad.AddComponent<PadTriggerReference>();

            var visualMarker = pad.AddComponent<DockPadVisualMarker>();
            using (var so = new SerializedObject(visualMarker))
            {
                so.FindProperty("freeColor").colorValue = new Color(0.2f, 1f, 0.2f, 0.25f);
                so.FindProperty("occupiedColor").colorValue = new Color(1f, 0.2f, 0.2f, 0.35f);
                so.FindProperty("markerSize").floatValue = 6f;
                so.FindProperty("markerHeight").floatValue = 0.05f;
                so.ApplyModifiedProperties();
            }
            EditorUtility.SetDirty(visualMarker);

            // TMP label
            var label = new GameObject(padName);
            label.transform.SetParent(pad.transform, false);
            label.transform.localPosition = Vector3.up * 5f;
            var tmp = label.AddComponent<TMPro.TextMeshPro>();
            tmp.text = padName;
            tmp.fontSize = 18;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;

            // Empty visual
            var emptyVis = new GameObject("Empty_visual");
            emptyVis.transform.SetParent(pad.transform, false);
            emptyVis.AddComponent<MeshFilter>();
            emptyVis.AddComponent<MeshRenderer>();
            var evCol = emptyVis.AddComponent<BoxCollider>();
            evCol.isTrigger = true;

            Debug.Log($"[PortStationCreator]   {padName}: padId=\"{padName}\", shipClasses=Light|Medium|Heavy|HeavyII");
        }

        // ============================================================
        // 7. Market zone + NPC (prefab instantiate → override MarketConfig)
        // ============================================================
        var marketZone = new GameObject($"Market_zone_{ns}");
        marketZone.transform.SetParent(root.transform, false);

        var npcPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NpcPrefabPath);
        GameObject npcInstance;
        if (npcPrefab != null)
        {
            npcInstance = (GameObject)PrefabUtility.InstantiatePrefab(npcPrefab, marketZone.transform);
            npcInstance.name = "Npc_peacfull_market_zone";

            // Override MarketConfig на экземпляре + record prefab override
            var mz = npcInstance.GetComponent<MarketZone>();
            if (mz != null)
            {
                using (var so = new SerializedObject(mz))
                {
                    so.FindProperty("_marketConfig").objectReferenceValue = marketConfig;
                    so.FindProperty("tradeRadius").floatValue = _tradeRadius;
                    so.FindProperty("shipDockRadius").floatValue = _shipDockRadius;
                    so.ApplyModifiedProperties();
                }
                EditorUtility.SetDirty(mz);
                PrefabUtility.RecordPrefabInstancePropertyModifications(mz);
            }
        }
        else
        {
            Debug.LogWarning($"[PortStationCreator] NPC prefab not found at {NpcPrefabPath}");
            npcInstance = new GameObject("Npc_peacfull_market_zone");
            npcInstance.transform.SetParent(marketZone.transform, false);
        }

        npcInstance.transform.localPosition = Vector3.zero;
        npcInstance.transform.localScale = Vector3.one;

        // ============================================================
        // 8. Finalize
        // ============================================================
        Selection.activeGameObject = root;
        EditorUtility.SetDirty(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[PortStationCreator] Станция «{display}» создана.\n" +
                  $"  DockStationDefinition: {dockDefPath}\n" +
                  $"  MarketConfig: {marketConfigPath}\n" +
                  $"  GameObject: {display}\n" +
                  $"  Pads: {_padCount}\n" +
                  $"  NPC: {npcInstance.name}");
    }

    private static void EnsureDir(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }
}
