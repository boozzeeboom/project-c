// Project C: экспорт всех source clips для locomotion transition pack.
// Читает manifest, снимает Humanoid-позы KL и пишет по одному pose pack на clip.
// Только Edit Mode; сцены, prefabs, controller и runtime-код не меняются.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectC.Animation.MotionBricks.EditorTools
{
    public static class MotionBricksLocomotionSourceExporter
    {
        private const string ModelPath = "Assets/Kevin Iglesias/Human Animations/Models/HumanM_Model.fbx";
        private const string ManifestPath = "docs/world/MOTIONBRICK_animations/batches/locomotion_v1.json";
        private const string OutputRoot = "Assets/_Project/Animations/Generated/MotionBricks/locomotion_sources";

        [Serializable]
        public sealed class LocomotionManifest
        {
            public int schema;
            public string id;
            public SourceClip[] sources;
        }

        [Serializable]
        public sealed class SourceClip
        {
            public string id;
            public string clipPath;
            public string clipName;
        }

        [Serializable]
        public sealed class PosePack
        {
            public int schema = 1;
            public string sourceId;
            public string modelPath;
            public string clipPath;
            public string clipName;
            public int frameCount;
            public float frameRate;
            public BoneRest[] bones;
            public Frame[] frames;
        }

        [Serializable]
        public sealed class BoneRest
        {
            public int poseIndex;
            public string boneName;
            public float qx, qy, qz, qw;
        }

        [Serializable]
        public sealed class Frame
        {
            public int index;
            public float time;
            public float hipX, hipY, hipZ;
            public BonePose[] bones;
        }

        [Serializable]
        public sealed class BonePose
        {
            public int poseIndex;
            public float px, py, pz;
            public float qx, qy, qz, qw;
        }

        private static readonly (int Pose, string Bone)[] BoneMap =
        {
            (0, "B-hips"), (1, "B-spine"), (3, "B-chest"), (4, "B-neck"), (6, "B-head"), (8, "B-jaw"),
            (11, "B-shoulder.L"), (12, "B-upperArm.L"), (13, "B-forearm.L"), (14, "B-hand.L"),
            (39, "B-shoulder.R"), (40, "B-upperArm.R"), (41, "B-forearm.R"), (42, "B-hand.R"),
            (67, "B-thigh.L"), (68, "B-shin.L"), (69, "B-foot.L"), (70, "B-toe.L"),
            (72, "B-thigh.R"), (73, "B-shin.R"), (74, "B-foot.R"), (75, "B-toe.R"),
        };

        [MenuItem("ProjectC/MotionBricks/Official Slice/Export locomotion transition sources (Edit Mode)")]
        public static void ExportAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Locomotion source export запускается только в Edit Mode.");

            string manifestFullPath = Path.GetFullPath(ManifestPath);
            if (!File.Exists(manifestFullPath))
                throw new FileNotFoundException($"Manifest не найден: {manifestFullPath}");
            var manifest = JsonUtility.FromJson<LocomotionManifest>(File.ReadAllText(manifestFullPath));
            if (manifest?.sources == null || manifest.sources.Length == 0)
                throw new InvalidDataException($"В manifest нет sources: {ManifestPath}");

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) throw new InvalidOperationException($"Не найдена модель: {ModelPath}");

            var holder = new GameObject("__MB_Locomotion_Source_Export");
            holder.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var modelInstance = UnityEngine.Object.Instantiate(model, holder.transform);
                modelInstance.transform.localPosition = Vector3.zero;
                modelInstance.transform.localRotation = Quaternion.identity;
                modelInstance.transform.localScale = Vector3.one;

                var byName = new Dictionary<string, Transform>();
                foreach (var t in modelInstance.GetComponentsInChildren<Transform>(true)) byName[t.name] = t;
                var bones = new Transform[BoneMap.Length];
                var restBones = new BoneRest[BoneMap.Length];
                for (int i = 0; i < BoneMap.Length; i++)
                {
                    if (!byName.TryGetValue(BoneMap[i].Bone, out bones[i]))
                        throw new InvalidOperationException($"В KL model не найдена кость {BoneMap[i].Bone}.");
                    Quaternion q = bones[i].rotation;
                    restBones[i] = new BoneRest
                    {
                        poseIndex = BoneMap[i].Pose, boneName = BoneMap[i].Bone,
                        qx = q.x, qy = q.y, qz = q.z, qw = q.w,
                    };
                }

                var animator = modelInstance.GetComponentInChildren<Animator>(true);
                if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                    throw new InvalidOperationException("У KL model нет Humanoid Avatar.");
                animator.enabled = false;

                var handler = new HumanPoseHandler(animator.avatar, animator.transform);
                var pose = new HumanPose { muscles = new float[HumanTrait.MuscleCount] };
                try
                {
                    int done = 0;
                    foreach (var source in manifest.sources)
                    {
                        AnimationClip clip = LoadClip(source);
                        var curves = new Dictionary<string, AnimationCurve>();
                        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                            curves[binding.propertyName] = AnimationUtility.GetEditorCurve(clip, binding);

                        int frameCount = Mathf.RoundToInt(clip.length * clip.frameRate) + 1;
                        var pack = new PosePack
                        {
                            sourceId = source.id, modelPath = ModelPath, clipPath = source.clipPath,
                            clipName = source.clipName, frameCount = frameCount, frameRate = clip.frameRate,
                            bones = restBones, frames = new Frame[frameCount],
                        };

                        for (int f = 0; f < frameCount; f++)
                        {
                            float time = f / clip.frameRate;
                            for (int m = 0; m < pose.muscles.Length; m++)
                                pose.muscles[m] = Eval(curves, HumanTrait.MuscleName[m], time);
                            pose.bodyPosition = new Vector3(
                                Eval(curves, "RootT.x", time), Eval(curves, "RootT.y", time), Eval(curves, "RootT.z", time));
                            pose.bodyRotation = new Quaternion(
                                Eval(curves, "RootQ.x", time), Eval(curves, "RootQ.y", time),
                                Eval(curves, "RootQ.z", time), Eval(curves, "RootQ.w", time));
                            handler.SetHumanPose(ref pose);

                            var frame = new Frame
                            {
                                index = f, time = time,
                                hipX = bones[0].position.x, hipY = bones[0].position.y, hipZ = bones[0].position.z,
                                bones = new BonePose[BoneMap.Length],
                            };
                            for (int i = 0; i < BoneMap.Length; i++)
                            {
                                Vector3 p = bones[i].position;
                                Quaternion q = bones[i].rotation;
                                frame.bones[i] = new BonePose
                                {
                                    poseIndex = BoneMap[i].Pose,
                                    px = p.x, py = p.y, pz = p.z,
                                    qx = q.x, qy = q.y, qz = q.z, qw = q.w,
                                };
                            }
                            pack.frames[f] = frame;
                        }

                        string outputDirectory = Path.GetFullPath($"{OutputRoot}/{source.id}/Male");
                        Directory.CreateDirectory(outputDirectory);
                        File.WriteAllText(Path.Combine(outputDirectory, "unity_pose_pack.json"), JsonUtility.ToJson(pack, true));
                        done++;
                        Debug.Log($"[MB-Locomotion-Export] {source.id}: {frameCount} кадров → {outputDirectory}");
                    }
                    Debug.Log($"[MB-Locomotion-Export] Готово: {done}/{manifest.sources.Length} source packs.");
                }
                finally
                {
                    handler.Dispose();
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
            }
        }

        private static AnimationClip LoadClip(SourceClip source)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(source.clipPath))
                if (asset is AnimationClip clip && clip.name == source.clipName) return clip;
            throw new InvalidOperationException($"Не найден клип '{source.clipName}' в '{source.clipPath}'.");
        }

        private static float Eval(Dictionary<string, AnimationCurve> curves, string property, float time) =>
            curves.TryGetValue(property, out var curve) ? curve.Evaluate(time) : 0f;
    }
}
