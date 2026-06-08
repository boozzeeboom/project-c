import json, subprocess
code = r'''
var sb = new System.Text.StringBuilder();

// --- Open WorldScene_0_0 if not already active ---
string scenePath = "Assets/_Project/Scenes/World/WorldScene_0_0.unity";
UnityEngine.SceneManagement.Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (!activeScene.path.EndsWith("WorldScene_0_0.unity")) {
    activeScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
    sb.AppendLine("Opened scene: " + activeScene.path);
} else {
    sb.AppendLine("Scene already active: " + activeScene.path);
}

// --- Create or reuse ItemData: "Медная руда" в Resources/Items/ ---

var mira = GameObject.Find("[Mira]");
UnityEngine.Vector3 miraPos = UnityEngine.Vector3.zero;
if (mira != null) miraPos = mira.transform.position;
else sb.AppendLine("WARN: [Mira] not found");
sb.AppendLine("[Mira] pos: " + miraPos);

string questPath = "Assets/_Project/Quests/Data/Quests/CollectCopperOre.asset";
var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<ProjectC.Quests.QuestDefinition>(questPath);
ProjectC.Quests.QuestDefinition copperQuest = null;
if (existing != null) {
    sb.AppendLine("Quest already exists, reusing: " + questPath);
    copperQuest = existing;
} else {
    var quest = UnityEngine.ScriptableObject.CreateInstance<ProjectC.Quests.QuestDefinition>();
    quest.questId = "collect_copper_ore";
    quest.displayName = "Собрать 3 медных руды";
    quest.description = "Горняки просят принести 3 медных руды для ремонта.";
    quest.faction = ProjectC.Factions.FactionId.GuildOfThoughts;
    quest.minReputation = 0;
    quest.oneShot = true;
    quest.discoverable = true;

    var stageCollect = new ProjectC.Quests.QuestStage();
    stageCollect.stageId = "collect";
    stageCollect.description = "Собери 3 медных руды.";
    var obj1 = new ProjectC.Quests.QuestObjective();
    obj1.objectiveId = "gather_copper";
    obj1.objectiveType = ProjectC.Quests.QuestObjectiveType.HaveItem;
    obj1.description = "Медная руда";
    obj1.itemTradeItemId = "Медная руда";
    obj1.requiredQuantity = 3;
    obj1.required = true;
    obj1.optional = false;
    stageCollect.objectives = new ProjectC.Quests.QuestObjective[] { obj1 };
    var giveCredits = new ProjectC.Dialogue.DialogueAction();
    giveCredits.type = ProjectC.Dialogue.DialogueActionType.GiveCredits;
    giveCredits.intParam = 50;
    stageCollect.onCompleteActions = new ProjectC.Dialogue.DialogueAction[] { giveCredits };

    var stageDeliver = new ProjectC.Quests.QuestStage();
    stageDeliver.stageId = "deliver";
    stageDeliver.description = "Отнеси руду Мире.";
    var obj2 = new ProjectC.Quests.QuestObjective();
    obj2.objectiveId = "deliver_to_mira";
    obj2.objectiveType = ProjectC.Quests.QuestObjectiveType.TalkToNpc;
    obj2.description = "Поговори с Мирой";
    obj2.targetNpcId = "mira_01";
    obj2.required = true;
    obj2.optional = false;
    stageDeliver.objectives = new ProjectC.Quests.QuestObjective[] { obj2 };
    var addRep = new ProjectC.Dialogue.DialogueAction();
    addRep.type = ProjectC.Dialogue.DialogueActionType.AddReputation;
    addRep.factionParam = ProjectC.Factions.FactionId.GuildOfThoughts;
    addRep.intParam = 15;
    stageDeliver.onCompleteActions = new ProjectC.Dialogue.DialogueAction[] { addRep };

    stageCollect.nextStageId = "deliver";
    quest.stages = new ProjectC.Quests.QuestStage[] { stageCollect, stageDeliver };
    quest.rewards = new ProjectC.Quests.QuestReward();
    quest.rewards.credits = 200;
    quest.rewards.reputation = new ProjectC.Quests.QuestRewardReputation[]
    {
        new ProjectC.Quests.QuestRewardReputation { faction = ProjectC.Factions.FactionId.GuildOfThoughts, value = 25 }
    };

    UnityEditor.AssetDatabase.CreateAsset(quest, questPath);
    UnityEditor.AssetDatabase.SaveAssets();
    sb.AppendLine("Created quest: " + questPath);
    copperQuest = quest;
}

sb.AppendLine("Quest: " + (copperQuest != null ? "OK" : "NULL"));

var dbPath = "Assets/_Project/Quests/Data/QuestDatabase.asset";
var db = UnityEditor.AssetDatabase.LoadAssetAtPath<ProjectC.Quests.QuestDatabase>(dbPath);
if (db != null && copperQuest != null) {
    var quests = new System.Collections.Generic.List<ProjectC.Quests.QuestDefinition>(db.quests ?? new ProjectC.Quests.QuestDefinition[0]);
    if (!quests.Contains(copperQuest)) {
        quests.Add(copperQuest);
        db.quests = quests.ToArray();
        UnityEditor.EditorUtility.SetDirty(db);
        UnityEditor.AssetDatabase.SaveAssets();
        sb.AppendLine("Registered quest in QuestDatabase");
    } else {
        sb.AppendLine("Quest already in QuestDatabase");
    }
}

// --- Create or reuse ItemData: "Медная руда" в Resources/Items/ ---
string itemResourcePath = "Assets/_Project/Resources/Items/Item_Resource_МеднаяРуда.asset";
ProjectC.Items.ItemData copperItem = UnityEditor.AssetDatabase.LoadAssetAtPath<ProjectC.Items.ItemData>(itemResourcePath);
if (copperItem == null) {
    copperItem = UnityEngine.ScriptableObject.CreateInstance<ProjectC.Items.ItemData>();
    copperItem.itemName = "Медная руда";
    copperItem.itemType = ProjectC.Items.ItemType.Resources;
    copperItem.description = "Руда для ремонта шахтёрского оборудования.";
    copperItem.maxStack = 99;
    copperItem.weightKg = 0.5f;
    UnityEditor.AssetDatabase.CreateAsset(copperItem, itemResourcePath);
    UnityEditor.AssetDatabase.SaveAssets();
    sb.AppendLine("Created ItemData: " + itemResourcePath);
} else {
    sb.AppendLine("Reusing ItemData: " + itemResourcePath);
}
// Also create an icon-less Material. AssetDatabase может не отдать itemId сразу — note.
var allItems = UnityEditor.AssetDatabase.FindAssets("t:ItemData");
sb.AppendLine("All ItemData assets in project: " + allItems.Length);

var pickupPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/PickupItem_Test.prefab");
sb.AppendLine("Pickup prefab: " + (pickupPrefab != null ? pickupPrefab.name : "NULL"));

if (pickupPrefab != null && copperItem != null) {
    var angle = 0f;
    for (int i = 0; i < 3; i++) {
        var angleRad = angle * UnityEngine.Mathf.Deg2Rad;
        var offset = new UnityEngine.Vector3(UnityEngine.Mathf.Cos(angleRad) * 5f, 0f, UnityEngine.Mathf.Sin(angleRad) * 5f);
        var pos = miraPos + offset;
        var existingPickup = GameObject.Find("[Pickup_CopperOre_" + (i+1) + "]");
        GameObject instance;
        if (existingPickup != null) {
            instance = existingPickup;
        } else {
            instance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(pickupPrefab, activeScene);
            instance.name = "[Pickup_CopperOre_" + (i+1) + "]";
        }
        instance.transform.position = pos;
        var pickupComp = instance.GetComponent<ProjectC.Items.PickupItem>();
        if (pickupComp != null) {
            var so = new UnityEditor.SerializedObject(pickupComp);
            var itemDataProp = so.FindProperty("itemData");
            if (itemDataProp != null) itemDataProp.objectReferenceValue = copperItem;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        angle += 90f;
    }
    sb.AppendLine("Placed 3 [Pickup_CopperOre_*] around Mira at 5m radius");
}

var triggerPos = miraPos + new UnityEngine.Vector3(0f, 0f, 8f);
var triggerGo = GameObject.Find("TriggerZone_DiscoverQuest");
if (triggerGo == null) {
    triggerGo = new GameObject("TriggerZone_DiscoverQuest");
    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(triggerGo, activeScene);
}
triggerGo.transform.position = triggerPos;
var box = triggerGo.GetComponent<BoxCollider>();
if (box == null) box = triggerGo.AddComponent<BoxCollider>();
box.size = new UnityEngine.Vector3(3f, 3f, 3f);
box.isTrigger = true;

if (triggerGo.transform.childCount == 0) {
    var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    marker.name = "TriggerZone_Marker";
    marker.transform.SetParent(triggerGo.transform, false);
    marker.transform.localPosition = UnityEngine.Vector3.zero;
    marker.transform.localScale = new UnityEngine.Vector3(2.5f, 0.1f, 2.5f);
    var rend = marker.GetComponent<Renderer>();
    if (rend != null) {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (mat.shader == null) mat.shader = Shader.Find("Standard");
        mat.color = new UnityEngine.Color(0.2f, 1f, 0.3f, 0.6f);
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
        rend.material = mat;
    }
    var markerCol = marker.GetComponent<Collider>();
    if (markerCol != null) UnityEngine.Object.DestroyImmediate(markerCol);
}

sb.AppendLine("Trigger zone placed at: " + triggerPos);

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
bool saved = UnityEditor.SceneManagement.EditorSceneManager.SaveScene(activeScene);
sb.AppendLine("Scene saved: " + saved);

UnityEditor.AssetDatabase.SaveAssets();
UnityEditor.AssetDatabase.Refresh();

return sb.ToString();
'''
payload = json.dumps({"action":"execute","code":code})
r = subprocess.run(["python","C:/Users/leon7/AppData/Local/hermes/profiles/project-c/skills/unity-mcp-orchestrator/scripts/mcp_unity_client.py","tool","execute_code",payload],capture_output=True,text=True,timeout=120)
print("STDOUT:",r.stdout[:3000])
print("STDERR:",r.stderr[:500])
