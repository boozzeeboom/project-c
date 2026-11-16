#if UNITY_EDITOR
// Project C: T-MB06 — покадровый SOMA → Project C retarget для runtime-поз.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Animation.MotionBricks
{
    internal sealed class MotionBricksSomaRetarget
    {
        private const int Joints = 77;
        private const int FloatsPerJoint = 7;
        private const float MaxClavicleDeg = 45f;
        private const float MaxClavicleLocalDeg = 45f;
        private const float MaxRootVerticalOffsetMeters = 5f;

        private sealed class RestNode
        {
            public Transform Target;
            public Vector3 RestLocalPosition;
            public Quaternion RestLocalRotation;
            public Quaternion RestWorld;
            public int Pose = -1;
            public readonly List<RestNode> Children = new List<RestNode>();
        }

        private readonly RestNode _root;
        private readonly Dictionary<int, RestNode> _nodeByPose = new Dictionary<int, RestNode>();
        private readonly Dictionary<int, Quaternion> _deltaByPose = new Dictionary<int, Quaternion>();
        private readonly Dictionary<int, Quaternion> _clavByPose = new Dictionary<int, Quaternion>();
        private readonly Quaternion[] _bindRot = new Quaternion[Joints];
        private readonly Vector3[] _bindPos = new Vector3[Joints];
        private readonly Quaternion[] _frameRot = new Quaternion[Joints];
        private readonly Vector3[] _framePos = new Vector3[Joints];
        private readonly Vector3[] _segmentRestDirections = new Vector3[Segments.Length];
        private readonly Vector3[] _clavicleRestDirections = new Vector3[Clavicles.Length];
        private readonly float[] _bind;
        private readonly RestNode _hips;
        private readonly Vector3 _bindRootUnity;

        private MotionBricksSomaRetarget(Animator animator, GameObject restTemplate, float[] bind)
        {
            if (bind == null || bind.Length != Joints * FloatsPerJoint)
                throw new ArgumentException($"SOMA bind must contain {Joints * FloatsPerJoint} floats", nameof(bind));
            _bind = (float[])bind.Clone();
            SomaPose(_bind, _bindRot, _bindPos);
            _bindRootUnity = ToUnityPos(_bind[0], _bind[1], _bind[2]);

            var holder = new GameObject("__MB_Runtime_Rest");
            holder.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                GameObject rest = UnityEngine.Object.Instantiate(restTemplate, holder.transform, false);
                rest.SetActive(false);
                foreach (Animator item in rest.GetComponentsInChildren<Animator>(true)) item.enabled = false;
                _root = BuildRest(rest.transform, animator.transform, Quaternion.identity);
                _hips = _nodeByPose[0];
                for (int i = 0; i < Segments.Length; i++)
                    _segmentRestDirections[i] = (_bindPos[Segments[i].Child] - _bindPos[Segments[i].Bone]).normalized;
                for (int i = 0; i < Clavicles.Length; i++)
                    _clavicleRestDirections[i] = (_bindPos[Clavicles[i].Arm] - _bindPos[Clavicles[i].Chest]).normalized;
            }
            finally
            {
                UnityEngine.Object.Destroy(holder);
            }
        }

        public static MotionBricksSomaRetarget Create(Animator animator, GameObject restTemplate, float[] bind)
        {
            if (animator == null) throw new ArgumentNullException(nameof(animator));
            if (restTemplate == null) throw new ArgumentNullException(nameof(restTemplate));
            var result = new MotionBricksSomaRetarget(animator, restTemplate, bind);
            foreach (var item in BoneMap)
            {
                // Кастомные female-риги проекта не имеют B-jaw; челюсть — необязательный runtime-bone.
                if (item.Pose == 8) continue;
                if (!result._nodeByPose.TryGetValue(item.Pose, out RestNode node) || node.Target == null)
                    throw new InvalidOperationException($"Runtime rig bone missing: {item.Bone}");
            }
            return result;
        }

        public void Apply(float[] frame)
        {
            if (frame == null || frame.Length != Joints * FloatsPerJoint)
                throw new ArgumentException("Invalid SOMA pose frame", nameof(frame));
            SomaPose(frame, _frameRot, _framePos);
            _deltaByPose.Clear();
            _clavByPose.Clear();

            for (int i = 0; i < Segments.Length; i++)
            {
                var segment = Segments[i];
                _deltaByPose[segment.Bone] = _frameRot[segment.Bone] * Quaternion.Inverse(_bindRot[segment.Bone]);
            }

            for (int i = 0; i < Clavicles.Length; i++)
            {
                var clavicle = Clavicles[i];
                Vector3 direction = _framePos[clavicle.Arm] - _framePos[clavicle.Chest];
                if (direction.sqrMagnitude < 1e-10f) continue;
                Quaternion swing = Quaternion.FromToRotation(_clavicleRestDirections[i], direction.normalized);
                float angle = Quaternion.Angle(Quaternion.identity, swing);
                if (angle > MaxClavicleDeg)
                    swing = Quaternion.Slerp(Quaternion.identity, swing, MaxClavicleDeg / angle);
                int pose = FindPoseByBone(clavicle.Bone);
                _clavByPose[pose] = swing;
                foreach (int chainPose in ClavicleChains[i])
                {
                    if (_deltaByPose.TryGetValue(chainPose, out Quaternion delta))
                        _deltaByPose[chainPose] = swing * delta;
                }
            }

            Evaluate(_root, _root.Target.localRotation, true);
            Vector3 rootUnity = ToUnityPos(frame[0], frame[1], frame[2]);
            float verticalOffset = rootUnity.y - _bindRootUnity.y;
            if (float.IsNaN(rootUnity.y) || float.IsInfinity(rootUnity.y) ||
                Mathf.Abs(verticalOffset) > MaxRootVerticalOffsetMeters)
                throw new InvalidOperationException(
                    $"SOMA root vertical offset {verticalOffset:F2} m exceeds guard; frame root Y={rootUnity.y:F2}, bind Y={_bindRootUnity.y:F2}");
            _hips.Target.localPosition = _hips.RestLocalPosition + Vector3.up * verticalOffset;
            ApplyClavicleLocal(frame, 11);
            ApplyClavicleLocal(frame, 39);
        }

        private void Evaluate(RestNode node, Quaternion animatedParentWorld, bool isRoot)
        {
            Quaternion animatedWorld;
            if (isRoot)
            {
                animatedWorld = animatedParentWorld;
            }
            else if (node.Pose >= 0 && _deltaByPose.TryGetValue(node.Pose, out Quaternion delta))
            {
                animatedWorld = delta * node.RestWorld;
            }
            else if (node.Pose >= 0 && _clavByPose.TryGetValue(node.Pose, out Quaternion clavicle))
            {
                animatedWorld = clavicle * node.RestWorld;
            }
            else
            {
                animatedWorld = animatedParentWorld * node.RestLocalRotation;
            }

            if (!isRoot && node.Target != null)
                node.Target.localRotation = Quaternion.Inverse(animatedParentWorld) * animatedWorld;
            foreach (RestNode child in node.Children) Evaluate(child, animatedWorld, false);
        }

        private void ApplyClavicleLocal(float[] frame, int pose)
        {
            RestNode node = _nodeByPose[pose];
            Quaternion delta = SomaLocal(frame, pose) * Quaternion.Inverse(SomaLocal(_bind, pose));
            float angle = Quaternion.Angle(Quaternion.identity, delta);
            if (angle > MaxClavicleLocalDeg)
                delta = Quaternion.Slerp(Quaternion.identity, delta, MaxClavicleLocalDeg / angle);
            node.Target.localRotation = node.RestLocalRotation * delta;
        }

        private RestNode BuildRest(Transform source, Transform target, Quaternion parentWorld)
        {
            Quaternion world = parentWorld * source.localRotation;
            var node = new RestNode
            {
                Target = target,
                RestLocalPosition = source.localPosition,
                RestLocalRotation = source.localRotation,
                RestWorld = world,
            };
            foreach (var item in BoneMap)
            {
                if (item.Bone == source.name)
                {
                    node.Pose = item.Pose;
                    _nodeByPose[item.Pose] = node;
                    break;
                }
            }
            for (int i = 0; i < source.childCount; i++)
            {
                Transform sourceChild = source.GetChild(i);
                Transform targetChild = target != null ? target.Find(sourceChild.name) : null;
                node.Children.Add(BuildRest(sourceChild, targetChild, world));
            }
            return node;
        }

        private static Quaternion SomaLocal(float[] frame, int pose)
        {
            int offset = pose * FloatsPerJoint;
            return ToUnityRot(new Quaternion(frame[offset + 3], frame[offset + 4], frame[offset + 5], frame[offset + 6]));
        }

        private static void SomaPose(float[] frame, Quaternion[] rotations, Vector3[] positions)
        {
            for (int joint = 0; joint < Joints; joint++)
            {
                int offset = joint * FloatsPerJoint;
                Quaternion localRotation = Normalize(ToUnityRot(new Quaternion(
                    frame[offset + 3], frame[offset + 4], frame[offset + 5], frame[offset + 6])));
                Vector3 localPosition = ToUnityPos(frame[offset], frame[offset + 1], frame[offset + 2]);
                int parent = Parents[joint];
                if (parent < 0)
                {
                    rotations[joint] = localRotation;
                    positions[joint] = localPosition;
                }
                else
                {
                    rotations[joint] = rotations[parent] * localRotation;
                    positions[joint] = positions[parent] + rotations[parent] * localPosition;
                }
            }
        }

        private static Quaternion Normalize(Quaternion value)
        {
            float magnitude = Mathf.Sqrt(value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w);
            return magnitude > 1e-6f && !float.IsNaN(magnitude) && !float.IsInfinity(magnitude)
                ? new Quaternion(value.x / magnitude, value.y / magnitude, value.z / magnitude, value.w / magnitude)
                : Quaternion.identity;
        }

        private static Vector3 ToUnityPos(float x, float y, float z) => new Vector3(-x, y, z);
        private static Quaternion ToUnityRot(Quaternion q) => new Quaternion(q.x, -q.y, -q.z, q.w);

        private static int FindPoseByBone(string boneName)
        {
            foreach (var item in BoneMap) if (item.Bone == boneName) return item.Pose;
            return -1;
        }

        private static readonly int[] Parents =
        {
            -1, 0, 1, 2, 3, 4, 5, 6, 6, 6, 6, 3, 11, 12, 13, 14, 15, 16, 17, 14, 19, 20, 21, 22, 14, 24, 25, 26, 27, 14,
            29, 30, 31, 32, 14, 34, 35, 36, 37, 3, 39, 40, 41, 42, 43, 44, 45, 42, 47, 48, 49, 50, 42, 52, 53, 54, 55, 42,
            57, 58, 59, 60, 42, 62, 63, 64, 65, 0, 67, 68, 69, 70, 0, 72, 73, 74, 75,
        };

        private static readonly (int Pose, string Bone)[] BoneMap =
        {
            (0, "B-hips"), (1, "B-spine"), (3, "B-chest"), (4, "B-neck"), (6, "B-head"), (8, "B-jaw"),
            (11, "B-shoulder.L"), (12, "B-upperArm.L"), (13, "B-forearm.L"), (14, "B-hand.L"),
            (39, "B-shoulder.R"), (40, "B-upperArm.R"), (41, "B-forearm.R"), (42, "B-hand.R"),
            (67, "B-thigh.L"), (68, "B-shin.L"), (69, "B-foot.L"), (70, "B-toe.L"),
            (72, "B-thigh.R"), (73, "B-shin.R"), (74, "B-foot.R"), (75, "B-toe.R"),
        };

        private static readonly (int Bone, int Child)[] Segments =
        {
            (0, 1), (1, 3), (3, 4), (4, 6),
            (12, 13), (13, 14), (40, 41), (41, 42),
            (67, 68), (68, 69), (69, 70), (72, 73), (73, 74), (74, 75),
        };

        private static readonly (int Chest, int Arm, string Bone)[] Clavicles =
        {
            (3, 11, "B-shoulder.L"), (3, 39, "B-shoulder.R"),
        };

        private static readonly int[][] ClavicleChains =
        {
            new[] { 12, 13, 14 }, new[] { 40, 41, 42 },
        };
    }
}
#endif
