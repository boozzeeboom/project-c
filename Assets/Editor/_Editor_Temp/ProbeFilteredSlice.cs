using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Проверка нарезки по фильтрам: фильтры, холдеры-дети, идемпотентность, откат.
/// Play Mode не запускается. Цель — поддерево ADM_BOOTH (8 однoмешевых объектов).
/// </summary>
public static class ProbeFilteredSlice
{
    public static void Execute()
    {
        var sb = new StringBuilder();
        try
        {
            Type t = FindType("ProjectC.EditorTools.BuildSettlementColliders");
            if (t == null) { Debug.Log("[PROBE] тип не найден"); return; }

            object s = t.GetMethod("DefaultSettings", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
            SetField(s, "sliceStep", 1.0f);
            SetField(s, "sliceAdaptTol", 0.05f);
            SetField(s, "minWorldSize", 0.25f);
            SetField(s, "minThickness", 0.5f);
            SetField(s, "skipInactive", true);
            SetField(s, "shipMode", false);
            SetField(s, "capsuleTokens", "PIPE,REBAR,TUBE");
            SetField(s, "sliceTokens", "");
            SetField(s, "excludeTokens", "BOLT,SCREW,LAMP,GLASS");
            SetField(s, "includeTokens", "ADM_BOOTH");

            Transform admin = FindPath("WorldRoot_0_0/PRIMUM/ADMIN_FOR_COLLIDER");
            if (admin == null) { Debug.Log("[PROBE] ADMIN_FOR_COLLIDER не найден"); return; }
            Transform target = admin;

            int meshCount = target.GetComponentsInChildren<MeshFilter>(true).Length;
            sb.AppendLine($"[PROBE] корень={target.name}, MeshFilter={meshCount}, include=ADM_BOOTH");

            // 1) Dry run по фильтрам
            sb.AppendLine("  dry(include ADM_BOOTH): " + Report(Call(t, "DryRunSlicesFiltered", target.gameObject, s)));

            // 2) include-токен без совпадений → всё отсекается
            SetField(s, "includeTokens", "ZZZ_NO_MATCH_ZZZ");
            sb.AppendLine("  dry(include-miss):      " + Report(Call(t, "DryRunSlicesFiltered", target.gameObject, s)));

            // 3) exclude по тому же токену → всё отсекается по имени
            SetField(s, "includeTokens", "");
            SetField(s, "excludeTokens", "ADM_BOOTH");
            sb.AppendLine("  dry(exclude ADM_BOOTH): " + Report(Call(t, "DryRunSlicesFiltered", target.gameObject, s)));

            // 4) sliceTokens без совпадений → только одиночные боксы (created == sliced)
            SetField(s, "excludeTokens", "BOLT,SCREW,LAMP,GLASS");
            SetField(s, "sliceTokens", "ZZZ_NO_MATCH_ZZZ");
            string d4 = Report(Call(t, "DryRunSlicesFiltered", target.gameObject, s));
            SetField(s, "sliceTokens", "");
            string d0 = Report(Call(t, "DryRunSlicesFiltered", target.gameObject, s));
            sb.AppendLine("  dry(sliceTokens-miss):  " + d4);
            sb.AppendLine("  dry(без sliceTokens):   " + d0);

            // 5) Боевая нарезка
            SetField(s, "includeTokens", "ADM_BOOTH");
            var before = new HashSet<Transform>();
            foreach (var x in target.GetComponentsInChildren<Transform>(true)) before.Add(x);
            int sceneBefore = CountTransforms();

            sb.AppendLine("  build#1: " + Report(Call(t, "BuildSlicesFiltered", target.gameObject, s)));

            int created = 0, parentOk = 0, trsOk = 0, collOk = 0, nameOk = 0;
            foreach (var f in target.GetComponentsInChildren<MeshFilter>(true))
            {
                foreach (Transform ch in f.transform)
                {
                    if (before.Contains(ch)) continue;
                    created++;
                    if (ch.parent == f.transform) parentOk++;
                    if (ch.localPosition == Vector3.zero
                        && Quaternion.Angle(ch.localRotation, Quaternion.identity) < 0.01f
                        && (ch.localScale - Vector3.one).sqrMagnitude < 1e-8f) trsOk++;
                    if (ch.GetComponent<Collider>() != null) collOk++;
                    if (ch.name.StartsWith("BC_")) nameOk++;
                }
            }
            sb.AppendLine($"  создано={created}; parent=меш={parentOk}; identity TRS={trsOk}; с коллайдером={collOk}; имён BC_*={nameOk}");

            // 6) Повторный запуск — без дубликатов
            sb.AppendLine("  build#2: " + Report(Call(t, "BuildSlicesFiltered", target.gameObject, s)));
            int after2 = 0;
            foreach (var f in target.GetComponentsInChildren<MeshFilter>(true))
                foreach (Transform ch in f.transform)
                    if (!before.Contains(ch)) after2++;
            sb.AppendLine($"  после #2={after2} (было {created}) → идемпотентно={after2 == created}; Transform {sceneBefore} → {CountTransforms()}");

            // 7) Откат
            var doomed = new List<GameObject>();
            foreach (var f in target.GetComponentsInChildren<MeshFilter>(true))
                foreach (Transform ch in f.transform)
                    if (!before.Contains(ch)) doomed.Add(ch.gameObject);
            foreach (var go in doomed) if (go != null) Undo.DestroyObjectImmediate(go);
            int sceneFinal = CountTransforms();
            sb.AppendLine($"  откат: удалено {doomed.Count}; Transform {sceneFinal} (было {sceneBefore}) → чисто={sceneFinal == sceneBefore}");
        }
        catch (Exception e)
        {
            sb.AppendLine("[PROBE] ИСКЛЮЧЕНИЕ: " + e);
        }
        Debug.Log(sb.ToString());
    }

    private static int CountTransforms()
    {
        return UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Length;
    }

    private static Type FindType(string fullName)
    {
        foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type found = a.GetType(fullName, false);
            if (found != null) return found;
        }
        return null;
    }

    private static Transform FindPath(string path)
    {
        string[] parts = path.Split('/');
        var tr = GameObject.Find(parts[0]);
        if (tr == null) return null;
        Transform cur = tr.transform;
        for (int i = 1; i < parts.Length; i++)
        {
            cur = cur.Find(parts[i]);
            if (cur == null) return null;
        }
        return cur;
    }

    private static void SetField(object obj, string name, object value)
    {
        var f = obj.GetType().GetField(name);
        if (f != null) f.SetValue(obj, value);
    }

    private static object Call(Type t, string method, params object[] args)
    {
        return t.GetMethod(method, BindingFlags.Public | BindingFlags.Static).Invoke(null, args);
    }

    private static string Report(object r)
    {
        if (r == null) return "null";
        var t = r.GetType();
        Func<string, object> g = n => { var f = t.GetField(n); return f == null ? "?" : f.GetValue(r); };
        return $"created={g("created")} capsules={g("capsules")} sliced={g("slicedMeshes")} unread={g("unreadSlices")} " +
               $"noMesh={g("skippedNoMesh")} disabled={g("skippedDisabled")} name={g("skippedName")} " +
               $"notIncluded={g("skippedInclude")} tiny={g("skippedTiny")}";
    }
}
