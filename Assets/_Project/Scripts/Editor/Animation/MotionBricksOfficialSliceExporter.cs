// Project C: MotionBricks official vertical slice — экспорт собственного KL walking clip в pose pack.
// Только Edit Mode и локальные JSON-данные. Не меняет сцены, Animator, prefabs и runtime-код.
// SOMA-конвертация и генерация выполняются в docs/world/MOTIONBRICK_animations/tools/official_vertical_slice.py.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectC.Animation.MotionBricks.EditorTools
{
    public static class MotionBricksOfficialSliceExporter
    {
        private const string ModelPath = "Assets/Kevin Iglesias/Human Animations/Models/HumanM_Model.fbx";
        private const string ClipPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Walk/RootMotion/HumanM@Walk01_Forward [RM].fbx";
        private const string ClipName = "HumanM@Walk01_Forward [RM]";
        private const string OutputDirectory = "Assets/_Project/Animations/Generated/MotionBricks/official_vertical_slice/Male";

        [Serializable]
        public sealed class PosePack
        {
            public int schema = 1;
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

        // Индексы официального 77-joint SOMA USD → кости KL. Совпадает с BoneMap текущего baker.
        private static readonly (int Pose, string Bone)[] BoneMap =
        {
            (0, "B-hips"), (1, "B-spine"), (3, "B-chest"), (4, "B-neck"), (6, "B-head"), (8, "B-jaw"),
            (11, "B-shoulder.L"), (12, "B-upperArm.L"), (13, "B-forearm.L"), (14, "B-hand.L"),
            (39, "B-shoulder.R"), (40, "B-upperArm.R"), (41, "B-forearm.R"), (42, "B-hand.R"),
            (67, "B-thigh.L"), (68, "B-shin.L"), (69, "B-foot.L"), (70, "B-toe.L"),
            (72, "B-thigh.R"), (73, "B-shin.R"), (74, "B-foot.R"), (75, "B-toe.R"),
        };

        [MenuItem("ProjectC/MotionBricks/Official Slice/Export KL walk seed (Edit Mode)")]
        public static void Export()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Official slice exporter запускается только в Edit Mode.");

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            AnimationClip clip = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(ClipPath))
                if (asset is AnimationClip candidate && candidate.name == ClipName) clip = candidate;

            if (model == null || clip == null)
                throw new InvalidOperationException("Не найден KL model/clip для official vertical slice.");

            var curves = new Dictionary<string, AnimationCurve>();
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                curves[binding.propertyName] = AnimationUtility.GetEditorCurve(clip, binding);

            int frameCount = Mathf.RoundToInt(clip.length * clip.frameRate) + 1;
            var pack = new PosePack
            {
                modelPath = ModelPath, clipPath = ClipPath, clipName = ClipName,
                frameCount = frameCount, frameRate = clip.frameRate,
                bones = new BoneRest[BoneMap.Length], frames = new Frame[frameCount],
            };

            var holder = new GameObject("__MB_Official_Seed_Export");
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
                for (int i = 0; i < BoneMap.Length; i++)
                {
                    if (!byName.TryGetValue(BoneMap[i].Bone, out bones[i]))
                        throw new InvalidOperationException($"В KL model не найдена кость {BoneMap[i].Bone}.");

                    Quaternion restWorld = bones[i].rotation;
                    pack.bones[i] = new BoneRest
                    {
                        poseIndex = BoneMap[i].Pose, boneName = BoneMap[i].Bone,
                        qx = restWorld.x, qy = restWorld.y, qz = restWorld.z, qw = restWorld.w,
                    };
                }

                var animator = modelInstance.GetComponentInChildren<Animator>(true);
                if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                    throw new InvalidOperationException("У KL model нет Humanoid Avatar.");
                animator.enabled = false;

                var pose = new HumanPose { muscles = new float[HumanTrait.MuscleCount] };
                var handler = new HumanPoseHandler(animator.avatar, animator.transform);
                try
                {
                    for (int f = 0; f < frameCount; f++)
                    {
                        float time = f / clip.frameRate;
                        for (int m = 0; m < pose.muscles.Length; m++)
                            pose.muscles[m] = Eval(curves, HumanTrait.MuscleName[m], time);
                        pose.bodyPosition = new Vector3(Eval(curves, "RootT.x", time), Eval(curves, "RootT.y", time), Eval(curves, "RootT.z", time));
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
                }
                finally
                {
                    handler.Dispose();
                }

                string outputDirectory = Path.GetFullPath(OutputDirectory);
                Directory.CreateDirectory(outputDirectory);
                string outputPath = Path.Combine(outputDirectory, "unity_pose_pack.json");
                File.WriteAllText(outputPath, JsonUtility.ToJson(pack, true));
                Debug.Log($"[MB-Official-Slice] Экспортировано {frameCount} кадров движения KL → {outputPath}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
            }
        }

        private static float Eval(Dictionary<string, AnimationCurve> curves, string property, float time) =>
            curves.TryGetValue(property, out var curve) ? curve.Evaluate(time) : 0f;
    }
}
