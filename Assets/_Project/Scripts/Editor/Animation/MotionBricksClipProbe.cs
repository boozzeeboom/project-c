#if UNITY_EDITOR
// T-MB02: проверка СОБРАННОГО клипа MotionBricks независимым измерением.
//
// Читает кривые клипа, применяет их к свежему инстансу HumanM_Model.fbx (все Animator выключены)
// и печатает мировые направления и высоты костей по кадрам. Не повторяет математику бейкера,
// поэтому ловит ошибки ретаргета, которые «согласованы» внутри бейкера.
//
// Меню: ProjectC/MotionBricks/T-MB02 Probe Baked Clip
// Запуск из кода: MotionBricksClipProbe.ProbeBakedClip()

using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ProjectC.Animation.MotionBricks.EditorTools
{
    public static class MotionBricksClipProbe
    {
        private const string ModelPath = "Assets/Kevin Iglesias/Human Animations/Models/HumanM_Model.fbx";
        private const string ClipPath = "Assets/_Project/Animations/Generated/MotionBricks/walk_to_jog/Male/WalkToJog_Male.anim";

        private static readonly float[] SampleTimes = { 0f, 0.2f, 0.4f, 0.8f, 1.2f, 1.6f };

        [MenuItem("ProjectC/MotionBricks/T-MB02 Probe Baked Clip")]
        public static void ProbeBakedClip()
        {
            Execute();
        }

        public static string Execute()
        {
            var sb = new StringBuilder();
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (clip == null || model == null)
            {
                Debug.LogError("[MB-Probe] не найден клип или модель.");
                return "fail";
            }

            var bindings = AnimationUtility.GetCurveBindings(clip);
            var curves = new AnimationCurve[bindings.Length];
            for (int i = 0; i < bindings.Length; i++) curves[i] = AnimationUtility.GetEditorCurve(clip, bindings[i]);

            var holder = new GameObject("__MB_Probe_Tmp");
            holder.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var inst = UnityEngine.Object.Instantiate(model, holder.transform);
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;
                inst.transform.localScale = Vector3.one;

                foreach (var animator in inst.GetComponentsInChildren<Animator>(true)) animator.enabled = false;

                Transform hips = FindDeep(inst.transform, "B-hips");
                Transform head = FindDeep(inst.transform, "B-head");
                Transform upArmL = FindDeep(inst.transform, "B-upperArm.L");
                Transform upArmR = FindDeep(inst.transform, "B-upperArm.R");
                Transform foreArmL = FindDeep(inst.transform, "B-forearm.L");
                Transform foreArmR = FindDeep(inst.transform, "B-forearm.R");
                Transform handL = FindDeep(inst.transform, "B-hand.L");
                Transform handR = FindDeep(inst.transform, "B-hand.R");
                Transform thighL = FindDeep(inst.transform, "B-thigh.L");
                Transform thighR = FindDeep(inst.transform, "B-thigh.R");
                Transform shinL = FindDeep(inst.transform, "B-shin.L");
                Transform shinR = FindDeep(inst.transform, "B-shin.R");
                Transform footL = FindDeep(inst.transform, "B-foot.L");
                Transform footR = FindDeep(inst.transform, "B-foot.R");
                Transform toeL = FindDeep(inst.transform, "B-toe.L");
                Transform toeR = FindDeep(inst.transform, "B-toe.R");

                if (hips == null || head == null || upArmL == null || footL == null)
                {
                    sb.AppendLine("в риге не найдены ожидаемые кости B-*");
                }
                else
                {
                    sb.AppendLine(string.Format("rest: hipsY={0:F3} headY={1:F3} footLY={2:F3} footRY={3:F3}",
                        hips.position.y, head.position.y, footL.position.y, footR.position.y));

                    Vector3 armRestL = (foreArmL.position - upArmL.position).normalized;
                    Vector3 armRestR = (foreArmR.position - upArmR.position).normalized;
                    sb.AppendLine(string.Format("rest dirs: upperArmL=({0:F2},{1:F2},{2:F2}) upperArmR=({3:F2},{4:F2},{5:F2})",
                        armRestL.x, armRestL.y, armRestL.z, armRestR.x, armRestR.y, armRestR.z));

                    sb.AppendLine("t     hipsY  headY  footLY footRY | armL·dn armR·dn legL·dn legR·dn toeL·dn toeR·dn");

                    foreach (float time in SampleTimes)
                    {
                        ApplyPose(inst.transform, bindings, curves, time);

                        Vector3 armL = (foreArmL.position - upArmL.position).normalized;
                        Vector3 armR = (foreArmR.position - upArmR.position).normalized;
                        Vector3 legL = (shinL.position - thighL.position).normalized;
                        Vector3 legR = (shinR.position - thighR.position).normalized;
                        Vector3 toL = (toeL.position - footL.position).normalized;
                        Vector3 toR = (toeR.position - footR.position).normalized;

                        sb.AppendLine(string.Format(
                            "{0:F2}  {1:F3}  {2:F3}  {3:F3}  {4:F3} | {5,6:F2}  {6,6:F2}  {7,6:F2}  {8,6:F2}  {9,6:F2}  {10,6:F2}",
                            time, hips.position.y, head.position.y, footL.position.y, footR.position.y,
                            Vector3.Dot(armL, Vector3.down), Vector3.Dot(armR, Vector3.down),
                            Vector3.Dot(legL, Vector3.down), Vector3.Dot(legR, Vector3.down),
                            Vector3.Dot(toL, Vector3.down), Vector3.Dot(toR, Vector3.down)));
                    }

                    sb.AppendLine(string.Format("armLen L={0:F3} R={1:F3}  legLen L={2:F3} R={3:F3}  handWorldY L={4:F3} R={5:F3}",
                        Vector3.Distance(upArmL.position, foreArmL.position),
                        Vector3.Distance(upArmR.position, foreArmR.position),
                        Vector3.Distance(thighL.position, shinL.position),
                        Vector3.Distance(thighR.position, shinR.position),
                        handL.position.y, handR.position.y));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
            }

            foreach (string line in sb.ToString().Split('\n'))
            {
                string trimmed = line.TrimEnd('\r');
                if (trimmed.Length > 0) Debug.Log("[MB-Probe] " + trimmed);
            }

            return "done";
        }

        private static void ApplyPose(Transform root, EditorCurveBinding[] bindings, AnimationCurve[] curves, float time)
        {
            var acc = new Dictionary<string, float[]>();
            var hasPos = new Dictionary<string, bool>();
            var order = new List<string>();

            for (int i = 0; i < bindings.Length; i++)
            {
                string path = bindings[i].path;
                if (!acc.TryGetValue(path, out float[] v))
                {
                    v = new float[7];
                    v[3] = 1f; // w
                    acc[path] = v;
                    hasPos[path] = false;
                    order.Add(path);
                }

                float value = curves[i].Evaluate(time);
                string prop = bindings[i].propertyName;
                // Unity хранит кривые Transform как m_LocalRotation.* / m_LocalPosition.*,
                // хотя SetCurve принимает и localRotation.* — сверяемся по окончанию имени.
                if (prop.EndsWith("Rotation.x")) v[0] = value;
                else if (prop.EndsWith("Rotation.y")) v[1] = value;
                else if (prop.EndsWith("Rotation.z")) v[2] = value;
                else if (prop.EndsWith("Rotation.w")) v[3] = value;
                else if (prop.EndsWith("Position.x")) { v[4] = value; hasPos[path] = true; }
                else if (prop.EndsWith("Position.y")) { v[5] = value; hasPos[path] = true; }
                else if (prop.EndsWith("Position.z")) { v[6] = value; hasPos[path] = true; }
            }

            foreach (string path in order)
            {
                Transform t = string.IsNullOrEmpty(path) ? root : root.Find(path);
                if (t == null) continue;

                float[] v = acc[path];
                float mag = Mathf.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2] + v[3] * v[3]);
                if (mag > 1e-6f) t.localRotation = new Quaternion(v[0] / mag, v[1] / mag, v[2] / mag, v[3] / mag);

                if (hasPos[path]) t.localPosition = new Vector3(v[4], v[5], v[6]);
            }
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }

            return null;
        }
    }
}
#endif
