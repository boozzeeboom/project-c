import json, subprocess

code = r'''
var sb = new System.Text.StringBuilder();

// === Step 1: Open WorldScene_0_0 ===
string scenePath = "Assets/_Project/Scenes/World/WorldScene_0_0.unity";
UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
sb.AppendLine("Scene opened: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

// === Step 2: Create ItemData "TestStageItem" (если нет) ===
string itemResPath = "Assets/_Project/Resources/Items/Item_Resource_TestStageItem.asset";
var existingItem = UnityEditor.AssetDatabase.LoadAssetAtPath<ProjectC.Items.ItemData>(itemResPath);
ProjectC.Items.ItemData testItem;
if (existingItem == null)
{
    testItem = UnityEngine.ScriptableObject.CreateInstance<ProjectC.Items.ItemData>();
    testItem.itemName = "TestStageItem";
    testItem.itemType = ProjectC.Items.ItemType.Resources;
    testItem.maxStack = 99;
    UnityEditor.AssetDatabase.CreateAsset(testItem, itemResPath);
    sb.AppendLine("Created ItemData: " + itemResPath);
}
else
{
    testItem = existingItem;
    sb.AppendLine("Reused ItemData: " + itemResPath);
}
UnityEditor.AssetDatabase.SaveAssets();

// === Step 3: Create Quest 1: "stage_intro_demo" (TalkToNpc objective, single stage with onEnter) ===
string quest1Path = "Assets/_Project/Quests/Data/Quests/StageIntroDemo.asset";
var existingQ1 = UnityEditor.AssetDatabase.LoadAssetAtPath<ProjectC.Quests.QuestDefinition>(quest1Path);
if (existingQ1 != null) UnityEditor.AssetDatabase.DeleteAsset(quest1Path);

var q1 = UnityEngine.ScriptableObject.CreateInstance<ProjectC.Quests.QuestDefinition>();
q1.questId = "stage_intro_demo";
q1.displayName = "Демо: stage intro";
q1.description = "Поговори с Мирой чтобы получить следующее задание.";
q1.faction = ProjectC.Factions.FactionId.GuildOfThoughts;
q1.minReputation = 0;
q1.oneShot = true;
q1.discoverable = true;

// Stage 1: talk to Mira → onComplete: GiveCredits(10)
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

// onComplete: GiveCredits(10)
var onComplete1 = new ProjectC.Dialogue.DialogueAction();
onComplete1.type = ProjectC.Dialogue.DialogueActionType.GiveCredits;
onComplete1.intParam = 10;
stage1.onCompleteActions = new ProjectC.Dialogue.DialogueAction[] { onComplete1 };

// onEnter: AddNpcAttitude(+5) at start
var onEnter1 = new ProjectC.Dialogue.DialogueAction();
onEnter1.type = ProjectC.Dialogue.DialogueActionType.AddNpcAttitude;
onEnter1.stringParam = "mira_01";
onEnter1.intParam = 5;
stage1.onEnterActions = new ProjectC.Dialogue.DialogueAction[] { onEnter1 };

stage1.nextStageId = "";  // single-stage для простоты (но с onEnter)
q1.stages = new ProjectC.Quests.QuestStage[] { stage1 };

// rewards: пустые (даём через onComplete)
var reward1 = new ProjectC.Quests.QuestReward();
q1.rewards = reward1;

q1.prerequisites = new System.Collections.Generic.List<ProjectC.Quests.QuestPrerequisite>();

UnityEditor.AssetDatabase.CreateAsset(q1, quest1Path);
sb.AppendLine("Created Quest1: " + quest1Path);

// === Step 4: Create Quest 2: "stage_multi_demo" (2 stages: collect → turnin) ===
string quest2Path = "Assets/_Project/Quests/Data/Quests/StageMultiDemo.asset";
var existingQ2 = UnityEditor.AssetDatabase.LoadAssetAtPath<ProjectC.Quests.QuestDefinition>(quest2Path);
if (existingQ2 != null) UnityEditor.AssetDatabase.DeleteAsset(quest2Path);

var q2 = UnityEngine.ScriptableObject.CreateInstance<ProjectC.Quests.QuestDefinition>();
q2.questId = "stage_multi_demo";
q2.displayName = "Демо: 2 стадии";
q2.description = "Собрать предмет (stage 1) → сдать Мире (stage 2).";
q2.faction = ProjectC.Factions.FactionId.GuildOfThoughts;
q2.minReputation = 0;
q2.oneShot = true;
q2.discoverable = true;

// Stage A: collect HaveItem(TestStageItem, 1) → onComplete: GiveCredits(20)
var stageA = new ProjectC.Quests.QuestStage();
stageA.stageId = "collect";
stageA.description = "Собрать предмет.";
var objA = new ProjectC.Quests.QuestObjective();
objA.objectiveId = "collect_item";
objA.objectiveType = ProjectC.Quests.QuestObjectiveType.HaveItem;
objA.itemTradeItemId = "TestStageItem";
objA.description = "Test Stage Item";
objA.required = true;
objA.requiredQuantity = 1;
stageA.objectives = new ProjectC.Quests.QuestObjective[] { objA };

// onEnter stage A: AddReputation(GuildOfThoughts, +3) — fired при entering collect stage
var onEnterA = new ProjectC.Dialogue.DialogueAction();
onEnterA.type = ProjectC.Dialogue.DialogueActionType.AddReputation;
onEnterA.factionParam = ProjectC.Factions.FactionId.GuildOfThoughts;
onEnterA.intParam = 3;
stageA.onEnterActions = new ProjectC.Dialogue.DialogueAction[] { onEnterA };

// onComplete stage A: GiveCredits(20)
var onCompleteA = new ProjectC.Dialogue.DialogueAction();
onCompleteA.type = ProjectC.Dialogue.DialogueActionType.GiveCredits;
onCompleteA.intParam = 20;
stageA.onCompleteActions = new ProjectC.Dialogue.DialogueAction[] { onCompleteA };

stageA.nextStageId = "deliver";
q2.stages = new ProjectC.Quests.QuestStage[] { stageA };

// Stage B: TalkToNpc(mira) → onComplete: GiveCredits(50) + onEnter: AddNpcAttitude(+10)
var stageB = new ProjectC.Quests.QuestStage();
stageB.stageId = "deliver";
stageB.description = "Поговори с Мирой для сдачи.";
var objB = new ProjectC.Quests.QuestObjective();
objB.objectiveId = "talk_mira_deliver";
objB.objectiveType = ProjectC.Quests.QuestObjectiveType.TalkToNpc;
objB.targetNpcId = "mira_01";
objB.description = "Поговори с Мирой";
objB.required = true;
objB.requiredQuantity = 1;
stageB.objectives = new ProjectC.Quests.QuestObjective[] { objB };

// onEnter stage B: AddNpcAttitude(+10) — fired при entering deliver stage
var onEnterB = new ProjectC.Dialogue.DialogueAction();
onEnterB.type = ProjectC.Dialogue.DialogueActionType.AddNpcAttitude;
onEnterB.stringParam = "mira_01";
onEnterB.intParam = 10;
stageB.onEnterActions = new ProjectC.Dialogue.DialogueAction[] { onEnterB };

// onComplete stage B: GiveCredits(50) — финальный reward (nextStageId = "")
var onCompleteB = new ProjectC.Dialogue.DialogueAction();
onCompleteB.type = ProjectC.Dialogue.DialogueActionType.GiveCredits;
onCompleteB.intParam = 50;
stageB.onCompleteActions = new ProjectC.Dialogue.DialogueAction[] { onCompleteB };

stageB.nextStageId = "";
q2.stages = new ProjectC.Quests.QuestStage[] { stageA, stageB };

// rewards
var reward2 = new ProjectC.Quests.QuestReward();
q2.rewards = reward2;
q2.prerequisites = new System.Collections.Generic.List<ProjectC.Quests.QuestPrerequisite>();

UnityEditor.AssetDatabase.CreateAsset(q2, quest2Path);
sb.AppendLine("Created Quest2: " + quest2Path);

// === Step 5: Add quests to QuestDatabase ===
string dbPath = "Assets/_Project/Quests/Data/QuestDatabase.asset";
var db = UnityEditor.AssetDatabase.LoadAssetAtPath<ProjectC.Quests.QuestDatabase>(dbPath);
if (db == null) {
    sb.AppendLine("ERROR: QuestDatabase not found at " + dbPath);
} else {
    var existing = new System.Collections.Generic.List<ProjectC.Quests.QuestDefinition>(db.quests ?? new ProjectC.Quests.QuestDefinition[0]);
    // Remove old versions if any
    existing.RemoveAll(x => x == null || x.questId == "stage_intro_demo" || x.questId == "stage_multi_demo");
    existing.Add(q1);
    existing.Add(q2);
    db.quests = existing.ToArray();
    UnityEditor.EditorUtility.SetDirty(db);
    UnityEditor.AssetDatabase.SaveAssets();
    sb.AppendLine("QuestDatabase updated: " + existing.Count + " quests total");
    for (int i = 0; i < existing.Count; i++) {
        sb.AppendLine("  [" + i + "] " + (existing[i] != null ? existing[i].questId : "null"));
    }
}

// === Step 6: Place 1 test pickup в WorldScene_0_0 ===
string pickupName = "[Pickup_TestStageItem]";
var existingPickup = GameObject.Find(pickupName);
if (existingPickup != null) UnityEngine.Object.DestroyImmediate(existingPickup);

var mira = GameObject.Find("[Mira]");
UnityEngine.Vector3 miraPos = UnityEngine.Vector3.zero;
if (mira != null) miraPos = mira.transform.position;

var prefabPath = "Assets/_Project/Prefabs/PickupItem_Test.prefab";
var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(prefabPath);
GameObject instance;
if (prefab != null) {
    instance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab);
    instance.name = pickupName;
    instance.transform.position = new UnityEngine.Vector3(miraPos.x + 8f, miraPos.y, miraPos.z - 3f);
} else {
    instance = new GameObject(pickupName);
    instance.transform.position = new UnityEngine.Vector3(miraPos.x + 8f, miraPos.y, miraPos.z - 3f);
    instance.AddComponent<UnityEngine.BoxCollider>();
}
var pickupComp = instance.GetComponent<ProjectC.Items.PickupItem>();
if (pickupComp != null) {
    var so = new UnityEditor.SerializedObject(pickupComp);
    var itemDataProp = so.FindProperty("itemData");
    if (itemDataProp != null) {
        itemDataProp.objectReferenceValue = testItem;
        so.ApplyModifiedProperties();
    }
}
sb.AppendLine("Placed " + pickupName + " at " + instance.transform.position);

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
sb.AppendLine("Scene saved");

return sb.ToString();
'''

payload = json.dumps({"action":"execute","code":code})
r = subprocess.run(["python","C:/Users/leon7/AppData/Local/hermes/profiles/project-c/skills/unity-mcp-orchestrator/scripts/mcp_unity_client.py","tool","execute_code",payload],capture_output=True,text=True,timeout=120)
print(r.stdout[:3000])
