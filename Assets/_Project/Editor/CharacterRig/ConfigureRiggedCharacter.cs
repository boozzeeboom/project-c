using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Mirrors the proven legacy Humanoid importer onto a freshly rigged FBX and reports avatar validity.
public static class ConfigureRiggedCharacter
{
    const string LegacyPath = "Assets/Generated_Models/_Legacy_PreAugust/Character_17866748877224/BLENDER/1_5.fbx";
    const string TargetPath = "Assets/Generated_Models_Rigged/01_Characters/Archetypes/PC_Char_Adjudicator_Female_APOSE_v01/PC_Char_Adjudicator_Female_APOSE_v01_Rigged_Humanoid.fbx";
    const string ReportPath = "Assets/Editor/_AuraTemp/rig_import_report.txt";

    public static void Execute()
    {
        var log = new StringBuilder();

        var legacy = AssetImporter.GetAtPath(LegacyPath) as ModelImporter;
        var target = AssetImporter.GetAtPath(TargetPath) as ModelImporter;
        if (legacy == null) { Debug.LogError("[RIG] legacy importer not found: " + LegacyPath); return; }
        if (target == null) { Debug.LogError("[RIG] target importer not found (refresh asset db): " + TargetPath); return; }

        // ---------- target skeleton names ----------
        var names = new HashSet<string>(StringComparer.Ordinal);
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(TargetPath);
        if (root != null)
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) names.Add(t.name);
        log.AppendLine("target transforms: " + names.Count);

        var src = legacy.humanDescription;
        log.AppendLine("legacy human bones: " + src.human.Length + "  legacy skeleton: " + src.skeleton.Length);

        var human = new List<HumanBone>();
        var dropped = new List<string>();
        foreach (var hb in src.human)
        {
            if (names.Contains(hb.boneName)) human.Add(hb);
            else dropped.Add(hb.humanName + "->" + hb.boneName);
        }
        log.AppendLine("mapped: " + human.Count + "  dropped: " + (dropped.Count == 0 ? "-" : string.Join(", ", dropped)));

        // ---------- mirror import settings ----------
        target.animationType = ModelImporterAnimationType.Human;
        target.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        target.autoGenerateAvatarMappingIfUnspecified = true;
        target.globalScale = legacy.globalScale;
        target.useFileUnits = legacy.useFileUnits;
        target.bakeAxisConversion = legacy.bakeAxisConversion;
        target.skinWeights = legacy.skinWeights;
        target.maxBonesPerVertex = legacy.maxBonesPerVertex;
        target.minBoneWeight = legacy.minBoneWeight;
        target.optimizeBones = legacy.optimizeBones;
        target.importNormals = legacy.importNormals;
        target.normalSmoothingSource = legacy.normalSmoothingSource;
        target.normalSmoothingAngle = legacy.normalSmoothingAngle;
        target.importTangents = legacy.importTangents;
        target.importBlendShapes = legacy.importBlendShapes;
        target.meshCompression = legacy.meshCompression;
        target.optimizeMeshPolygons = legacy.optimizeMeshPolygons;
        target.optimizeMeshVertices = legacy.optimizeMeshVertices;
        target.weldVertices = legacy.weldVertices;
        target.keepQuads = legacy.keepQuads;
        target.indexFormat = legacy.indexFormat;
        target.isReadable = legacy.isReadable;
        target.preserveHierarchy = legacy.preserveHierarchy;
        target.sortHierarchyByName = legacy.sortHierarchyByName;
        target.optimizeGameObjects = legacy.optimizeGameObjects;
        target.extraExposedTransformPaths = legacy.extraExposedTransformPaths;
        target.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        target.materialLocation = legacy.materialLocation;
        target.materialName = legacy.materialName;
        target.materialSearch = legacy.materialSearch;
        target.useSRGBMaterialColor = legacy.useSRGBMaterialColor;
        target.searchTexturesGlobally = true;
        target.generateSecondaryUV = false;
        target.importCameras = false;
        target.importLights = false;
        target.importAnimation = false;

        log.AppendLine("settings: scale=" + target.globalScale + " fileUnits=" + target.useFileUnits +
                       " bakeAxis=" + target.bakeAxisConversion + " skin=" + target.skinWeights +
                       " maxBonesPerVertex=" + target.maxBonesPerVertex + " minBoneWeight=" + target.minBoneWeight);

        // ---------- human mapping (skeleton left empty so Unity rebuilds it from the mesh rest pose) ----------
        var hd = new HumanDescription
        {
            human = human.ToArray(),
            skeleton = new SkeletonBone[0],
            armStretch = src.armStretch,
            legStretch = src.legStretch,
            feetSpacing = src.feetSpacing,
            hasTranslationDoF = src.hasTranslationDoF,
            upperArmTwist = src.upperArmTwist,
            lowerArmTwist = src.lowerArmTwist,
            upperLegTwist = src.upperLegTwist,
            lowerLegTwist = src.lowerLegTwist,
            rootMotionBoneName = src.rootMotionBoneName
        };
        target.humanDescription = hd;
        target.SaveAndReimport();
        AssetDatabase.Refresh();

        // ---------- verify ----------
        Avatar av = null;
        SkinnedMeshRenderer smr = null;
        GameObject inst = null;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(TargetPath))
            if (o is Avatar a && av == null) av = a;

        inst = AssetDatabase.LoadAssetAtPath<GameObject>(TargetPath);
        if (inst != null) smr = inst.GetComponentInChildren<SkinnedMeshRenderer>(true);

        log.AppendLine("--- verify ---");
        if (av == null) log.AppendLine("AVATAR: none generated");
        else
        {
            log.AppendLine("AVATAR: name=" + av.name + " isValid=" + av.isValid +
                           " isHuman=" + av.isHuman);
            var ahd = av.humanDescription;
            log.AppendLine("avatar human bones=" + ahd.human.Length + " skeleton=" + ahd.skeleton.Length);
            var missing = new List<string>();
            foreach (var hb in src.human)
                if (Array.FindIndex(ahd.human, h => h.humanName == hb.humanName) < 0) missing.Add(hb.humanName);
            log.AppendLine("missing vs legacy: " + (missing.Count == 0 ? "-" : string.Join(", ", missing)));
        }

        if (inst != null)
        {
            var t = FindDeep(inst.transform, "B-hips");
            log.AppendLine("root armature object B-hips: " + (t != null ? "found" : "MISSING"));
            log.AppendLine("root children: " + string.Join(", ", inst.transform.Cast<Transform>().Select(c => c.name)));

            ReportBone(log, inst.transform, "Hips", "B-spine");
            ReportBone(log, inst.transform, "Head", "B-head");
            ReportBone(log, inst.transform, "LeftHand", "B-hand.L");
            ReportBone(log, inst.transform, "LeftFoot", "B-foot.L");
            ReportBone(log, inst.transform, "LeftToes", "B-toe.L");

            if (smr != null && smr.sharedMesh != null)
            {
                var b = smr.sharedMesh.bounds;
                var wc = smr.transform.TransformPoint(b.center);
                var ws = Vector3.Scale(b.size, Abs(smr.transform.lossyScale));
                log.AppendLine("mesh verts=" + smr.sharedMesh.vertexCount +
                               " bounds center=" + wc.ToString("F3") + " size=" + ws.ToString("F3"));
                log.AppendLine("height(m)=" + ws.y.ToString("F3") + "  bones=" + smr.bones.Length);
                var toeT = FindDeep(inst.transform, "B-toe.L");
                var ankT = FindDeep(inst.transform, "B-foot.L");
                if (toeT != null && ankT != null)
                    log.AppendLine("facing: toe.L.z=" + toeT.position.z.ToString("F3") +
                                   " foot.L.z=" + ankT.position.z.ToString("F3") +
                                   " head.z=" + (FindDeep(inst.transform, "B-head")?.position.z ?? 0f).ToString("F3") +
                                   "  -> " + (toeT.position.z > ankT.position.z ? "+Z (forward OK)" : "-Z (BACKWARDS)"));
            }
        }

        System.IO.Directory.CreateDirectory("Assets/Editor/_AuraTemp");
        System.IO.File.WriteAllText(ReportPath, log.ToString());
        Debug.Log("[RIG]\n" + log);
    }

    static void ReportBone(StringBuilder log, Transform root, string humanName, string boneName)
    {
        var t = FindDeep(root, boneName);
        log.AppendLine(humanName + " <- " + boneName + " : " +
                       (t == null ? "NOT FOUND" : t.position.ToString("F4") + " parent=" + (t.parent != null ? t.parent.name : "-")));
    }

    static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    static Transform FindDeep(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }
}
