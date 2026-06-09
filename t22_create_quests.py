import json, subprocess
# T-Q22: create 2 quest assets, append to QuestDatabase, place pickup — всё через Roslyn,
# НЕ удаляя ничего (additive).

code = r'''
var sb = new System.Text.StringBuilder();

// === Open WorldScene_0_0 ===
string scenePath = "Assets/_Project/Scenes/World/WorldScene_0_0.unity";
UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
sb.AppendLine("Scene opened: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

// === Verify TestStageItem exists ===
string itemResPath = "Assets/_Project/Resources/Items/Item_Resource_TestStageItem.asset";
var testItem = UnityEditor.AssetDatabase.LoadAssetAtPath<ProjectC.Items.ItemData>(itemResPath);
if (testItem == null) { sb.AppendLine("ERROR: ItemData not found!"); return sb.ToString(); }
sb.AppendLine("ItemData: " + testItem.itemName + " type=" + testItem.itemType);

// === Quest 1: StageIntroDemo (single-stage с onEnter) ===
string q1Path = "Assets/_Project/Quests/Data/Quests/StageIntroDemo.asset";
var q1 = UnityEngine.ScriptableObject.CreateInstance<ProjectC.Quests.QuestDefinition>();
q1.questId = "stage_intro_demo";
q1.displayName = "Демо: stage с onEnter";
q1.description = "Поговори с Мирой. onEnter: +5 к отношению. onComplete: +10 CR.";
q1.faction = ProjectC.Factions.FactionId.GuildOfThoughts;
q1.minReputation = 0;
q1.oneShot = true;
q1.discoverable = true;

var stage1 = new ProjectC.Quests.QuestStage();
stage1.stageId = "intro";
stage1.description = "Поговори с Мирой.";

var obj1 = new ProjectC.Quests.QuestObjective();
obj1.objectiveId = "talk_mira";
obj1.objectiveType = ProjectC.Quests.QuestObjectiveType.TalkToNpc;
obj1.targetNpcId = "mira_01";
obj1.description = "Поговори с Мирой";
obj1.required = true;
obj1.requiredQuantity = 1;
stage1.objectives = new ProjectC.Quests.QuestObjective[] { obj1 };

// onEnter: AddNpcAttitude(mira, +5) — fire при Accept
var onEnter1 = new ProjectC.Dialogue.DialogueAction();
onEnter1.type = ProjectC.Dialogue.DialogueActionType.AddNpcAttitude;
onEnter1.stringParam = "mira_01";
onEnter1.intParam = 5;
stage1.onEnterActions = new ProjectC.Dialogue.DialogueAction[] { onEnter1 };

// onComplete: GiveCredits(10) — fire при completing
var onComplete1 = new ProjectC.Dialogue.DialogueAction();
onComplete1.type = ProjectC.Dialogue.DialogueActionType.GiveCredits;
onComplete1.intParam = 10;
stage1.onCompleteActions = new ProjectC.Dialogue.DialogueAction[] { onComplete1 };

stage1.nextStageId = "";
q1.stages = new ProjectC.Quests.QuestStage[] { stage1 };
q1.rewards = new ProjectC.Quests.QuestReward();
q1.prerequisites = new ProjectC.Quests.QuestPrerequisite[0];

UnityEditor.AssetDatabase.CreateAsset(q1, q1Path);
sb.AppendLine("Created: " + q1Path);

// === Quest 2: StageMultiDemo (2 stages: collect HaveItem → deliver TalkToNpc) ===
string q2Path = "Assets/_Project/Quests/Data/Quests/StageMultiDemo.asset";
var q2 = UnityEngine.ScriptableObject.CreateInstance<ProjectC.Quests.QuestDefinition>();
q2.questId = "stage_multi_demo";
q2.displayName = "Демо: 2 стадии (collect→deliver)";
q2.description = "Stage A: собрать предмет. Stage B: поговорить с Мирой.";
q2.faction = ProjectC.Factions.FactionId.GuildOfThoughts;
q2.minReputation = 0;
q2.oneShot = true;
q2.discoverable = true;

// Stage A: collect HaveItem
var stageA = new ProjectC.Quests.QuestStage();
stageA.stageId = "collect";
stageA.description = "Собрать TestStageItem.";

var objA = new ProjectC.Quests.QuestObjective();
objA.objectiveId = "collect_item";
objA.objectiveType = ProjectC.Quests.QuestObjectiveType.HaveItem;
objA.itemTradeItemId = "TestStageItem";
objA.description = "Test Stage Item";
objA.required = true;
objA.requiredQuantity = 1;
stageA.objectives = new ProjectC.Quests.QuestObjective[] { objA };

// onEnter A: AddReputation(GuildOfThoughts, +3)
var onEnterA = new ProjectC.Dialogue.DialogueAction();
onEnterA.type = ProjectC.Dialogue.DialogueActionType.AddReputation;
onEnterA.factionParam = ProjectC.Factions.FactionId.GuildOfThoughts;
onEnterA.intParam = 3;
stageA.onEnterActions = new ProjectC.Dialogue.DialogueAction[] { onEnterA };

// onComplete A: GiveCredits(20)
var onCompleteA = new ProjectC.Dialogue.DialogueAction();
onCompleteA.type = ProjectC.Dialogue.DialogueActionType.GiveCredits;
onCompleteA.intParam = 20;
stageA.onCompleteActions = new ProjectC.Dialogue.DialogueAction[] { onCompleteA };

stageA.nextStageId = "deliver";
q2.stages = new ProjectC.Quests.QuestStage[] { stageA };

// Stage B: TalkToNpc
var stageB = new ProjectC.Quests.QuestStage();
stageB.stageId = "deliver";
stageB.description = "Сдать Мире.";

var objB = new ProjectC.Quests.QuestObjective();
objB.objectiveId = "talk_mira";
objB.objectiveType = ProjectC.Quests.QuestObjectiveType.TalkToNpc;
objB.targetNpcId = "mira_01";
objB.description = "Поговори с Мирой";
objB.required = true;
objB.requiredQuantity = 1;
stageB.objectives = new ProjectC.Quests.QuestObjective[] { objB };

// onEnter B: AddNpcAttitude(mira, +10)
var onEnterB = new ProjectC.Dialogue.DialogueAction();
onEnterB.type = ProjectC.Dialogue.DialogueActionType.AddNpcAttitude;
onEnterB.stringParam = "mira_01";
onEnterB.intParam = 10;
stageB.onEnterActions = new ProjectC.Dialogue.DialogueAction[] { onEnterB };

// onComplete B: GiveCredits(50) — final
var onCompleteB = new ProjectC.Dialogue.DialogueAction();
onCompleteB.type = ProjectC.Dialogue.DialogueActionType.GiveCredits;
onCompleteB.intParam = 50;
stageB.onCompleteActions = new ProjectC.Dialogue.DialogueAction[] { onCompleteB };

stageB.nextStageId = "";
q2.stages = new ProjectC.Quests.QuestStage[] { stageA, stageB };
q2.rewards = new ProjectC.Quests.QuestReward();
q2.prerequisites = new ProjectC.Quests.QuestPrerequisite[0];

UnityEditor.AssetDatabase.CreateAsset(q2, q2Path);
sb.AppendLine("Created: " + q2Path);

// === Append to QuestDatabase (additive) ===
string dbPath = "Assets/_Project/Quests/Data/QuestDatabase.asset";
var db = UnityEditor.AssetDatabase.LoadAssetAtPath<ProjectC.Quests.QuestDatabase>(dbPath);
if (db == null) {
    sb.AppendLine("ERROR: QuestDatabase not found at " + dbPath);
} else {
    var list = new System.Collections.Generic.List<ProjectC.Quests.QuestDefinition>(db.quests ?? new ProjectC.Quests.QuestDefinition[0]);
    // remove existing copies (idempotent)
    list.RemoveAll(x => x == null || x.questId == "stage_intro_demo" || x.questId == "stage_multi_demo");
    list.Add(q1);
    list.Add(q2);
    db.quests = list.ToArray();
    UnityEditor.EditorUtility.SetDirty(db);
    UnityEditor.AssetDatabase.SaveAssets();
    sb.AppendLine("QuestDatabase updated: " + list.Count + " quests total");
    foreach (var q in list) sb.AppendLine("  - " + (q != null ? q.questId : "null"));
}

// === Place [Pickup_TestStageItem] in scene (additive, NOT removing existing) ===
string pickupName = "[Pickup_TestStageItem]";
var existingPickup = GameObject.Find(pickupName);
if (existingPickup == null) {
    var mira = GameObject.Find("[Mira]");
    UnityEngine.Vector3 miraPos = UnityEngine.Vector3.zero;
    if (mira != null) miraPos = mira.transform.position;

    var prefabPath = "Assets/_Project/Prefabs/PickupItem_Test.prefab";
    var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(prefabPath);
    GameObject instance;
    if (prefab != null) {
        instance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab);
        instance.name = pickupName;
    } else {
        instance = new GameObject(pickupName);
        instance.AddComponent<UnityEngine.BoxCollider>();
    }
    // Place в стороне от copper pickup'ов (3m N of Mira, copper — 5m E/S/W)
    instance.transform.position = new UnityEngine.Vector3(miraPos.x, miraPos.y, miraPos.z + 3f);

    var pickupComp = instance.GetComponent<ProjectC.Items.PickupItem>();
    if (pickupComp != null) {
        var so = new UnityEditor.SerializedObject(pickupComp);
        var itemDataProp = so.FindProperty("itemData");
        if (itemDataProp != null) {
            itemDataProp.objectReferenceValue = testItem;
            so.ApplyModifiedProperties();
        }
    }
    sb.AppendLine("Placed: " + pickupName + " @ " + instance.transform.position);
} else {
    sb.AppendLine("Pickup already exists: " + pickupName);
}

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
sb.AppendLine("Scene saved");

return sb.ToString();
'''

payload = json.dumps({"action":"execute","code":code})
r = subprocess.run(["python","C:/Users/leon7/AppData/Local/hermes/profiles/project-c/skills/unity-mcp-orchestrator/scripts/mcp_unity_client.py","tool","execute_code",payload],capture_output=True,text=True,timeout=120)
print(r.stdout[:3000])
