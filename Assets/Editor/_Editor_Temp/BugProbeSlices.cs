// TEMP diagnostic probe (delete after use): воспроизводит баг нарезки SlicePlan
// на боксе с двумя сквозными квадратными проёмами.
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class BugProbeSlices
{
    public static void Execute()
    {
        Type t = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            t = asm.GetType("ProjectC.EditorTools.BuildSettlementColliders");
            if (t != null) break;
        }
        if (t == null) { Debug.LogError("[Probe] BuildSettlementColliders not found"); return; }

        var mi = t.GetMethod("SlicePlan", BindingFlags.NonPublic | BindingFlags.Static);
        if (mi == null) { Debug.LogError("[Probe] SlicePlan not found"); return; }

        Probe(t, mi, "solid 8x4x0.4", BuildBox(0f, 8f, 0f, 4f, 0f, 0.4f));
        Probe(t, mi, "wall 8x4x0.4 + 2 holes (x2..3/x5..6, y1..2)", BuildWallWithTwoHoles());
        Probe(t, mi, "plate 10x0.2x10 + 2 openings (y-thin)", BuildPlateWithOpenings());
        Probe(t, mi, "two legs 8x1x0.4 (gap y1..3)", BuildTwoLegs());
        Probe(t, mi, "hollow shell 8x2x2 t=0.2", BuildHollowShell());
    }

    private static void Probe(Type t, MethodInfo mi, string label, Mesh mesh)
    {
        var res = (System.Collections.IList)mi.Invoke(null, new object[] { mesh, 0, 1.0f, 0.05f, Vector3.one });
        if (res == null)
        {
            Debug.Log($"[Probe] {label}: SlicePlan = null  => фолбэк на ОДИН общий бокс {mesh.bounds.size}");
            return;
        }
        Debug.Log($"[Probe] {label}: SlicePlan = {res.Count} boxes");
        int i = 0;
        foreach (var sl in res)
        {
            var ty = sl.GetType();
            Vector3 mn = (Vector3)ty.GetField("min").GetValue(sl);
            Vector3 mx = (Vector3)ty.GetField("max").GetValue(sl);
            int bin = (int)ty.GetField("bin").GetValue(sl);
            int sub = (int)ty.GetField("sub").GetValue(sl);
            Debug.Log($"[Probe]   #{i++} bin={bin} sub={sub} min=({mn.x:F2},{mn.y:F2},{mn.z:F2}) max=({mx.x:F2},{mx.y:F2},{mx.z:F2})");
        }
    }

    private static Mesh BuildWallWithTwoHoles()
    {
        // Сплошная стена X 0..8, Y 0..4, Z 0..0.4 с двумя сквозными проёмами по Z.
        var rects = new List<Rect>
        {
            new Rect(0f, 0f, 8f, 1f),  // низ
            new Rect(0f, 1f, 2f, 1f),  // между левым краем и проёмом 1
            new Rect(3f, 1f, 2f, 1f),  // между проёмами
            new Rect(6f, 1f, 2f, 1f),  // между проёмом 2 и правым краем
            new Rect(0f, 2f, 8f, 2f),  // верх
        };
        var verts = new List<Vector3>();
        var tris = new List<int>();
        foreach (var r in rects) AddBox(verts, tris, r.xMin, r.xMax, r.yMin, r.yMax, 0f, 0.4f);
        return MakeMesh(verts, tris, "probe_wall_holes");
    }

    private static Mesh BuildBox(float x0, float x1, float y0, float y1, float z0, float z1)
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();
        AddBox(verts, tris, x0, x1, y0, y1, z0, z1);
        return MakeMesh(verts, tris, "probe_solid_box");
    }

    // Плита 10x0.2x10 с двумя проёмами насквозь по Y: (x3..4, z3..4) и (x6..7, z4..6).
    private static Mesh BuildPlateWithOpenings()
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();
        AddPrism(verts, tris, 0f, 10f, 0f, 3f, 0f, 0.2f);   // z 0..3, x full
        AddPrism(verts, tris, 0f, 3f, 3f, 4f, 0f, 0.2f);   // z 3..4 слева от проёма 1
        AddPrism(verts, tris, 4f, 10f, 3f, 4f, 0f, 0.2f);  // z 3..4 справа
        AddPrism(verts, tris, 0f, 6f, 4f, 6f, 0f, 0.2f);   // z 4..6 слева от проёма 2
        AddPrism(verts, tris, 7f, 10f, 4f, 6f, 0f, 0.2f);  // z 4..6 справа
        AddPrism(verts, tris, 0f, 10f, 6f, 10f, 0f, 0.2f); // z 6..10
        return MakeMesh(verts, tris, "probe_plate_openings");
    }

    // Две отдельные «ножки» 8x1x0.4 с зазором по Y 1..3 (аналог U-трубы).
    private static Mesh BuildTwoLegs()
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();
        AddBox(verts, tris, 0f, 8f, 0f, 1f, 0f, 0.4f);
        AddBox(verts, tris, 0f, 8f, 3f, 4f, 0f, 0.4f);
        return MakeMesh(verts, tris, "probe_two_legs");
    }

    // Полый короб 8x2x2, стенка 0.2 (4 непересекающиеся плиты).
    private static Mesh BuildHollowShell()
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();
        AddBox(verts, tris, 0f, 8f, 0f, 0.2f, 0f, 2f);     // низ
        AddBox(verts, tris, 0f, 8f, 1.8f, 2f, 0f, 2f);     // верх
        AddBox(verts, tris, 0f, 8f, 0.2f, 1.8f, 0f, 0.2f); // стенка -Z
        AddBox(verts, tris, 0f, 8f, 0.2f, 1.8f, 1.8f, 2f); // стенка +Z
        return MakeMesh(verts, tris, "probe_hollow_shell");
    }

    // Призма по XZ-прямоугольнику, выдавленная по Y [y0..y1].
    private static void AddPrism(List<Vector3> v, List<int> tr, float x0, float x1, float z0, float z1, float y0, float y1)
    {
        AddBox(v, tr, x0, x1, y0, y1, z0, z1);
    }

    private static void AddBox(List<Vector3> v, List<int> tr, float x0, float x1, float y0, float y1, float z0, float z1)
    {
        int b = v.Count;
        v.Add(new Vector3(x0, y0, z0)); v.Add(new Vector3(x1, y0, z0));
        v.Add(new Vector3(x1, y1, z0)); v.Add(new Vector3(x0, y1, z0));
        v.Add(new Vector3(x0, y0, z1)); v.Add(new Vector3(x1, y0, z1));
        v.Add(new Vector3(x1, y1, z1)); v.Add(new Vector3(x0, y1, z1));
        // z- (0..3) наружу -Z
        tr.Add(b + 0); tr.Add(b + 2); tr.Add(b + 1); tr.Add(b + 0); tr.Add(b + 3); tr.Add(b + 2);
        // z+ (4..7) наружу +Z
        tr.Add(b + 4); tr.Add(b + 5); tr.Add(b + 6); tr.Add(b + 4); tr.Add(b + 6); tr.Add(b + 7);
        // y- (0,1,5,4)
        tr.Add(b + 0); tr.Add(b + 1); tr.Add(b + 5); tr.Add(b + 0); tr.Add(b + 5); tr.Add(b + 4);
        // y+ (3,7,6,2)
        tr.Add(b + 3); tr.Add(b + 7); tr.Add(b + 6); tr.Add(b + 3); tr.Add(b + 6); tr.Add(b + 2);
        // x- (0,4,7,3)
        tr.Add(b + 0); tr.Add(b + 4); tr.Add(b + 7); tr.Add(b + 0); tr.Add(b + 7); tr.Add(b + 3);
        // x+ (1,2,6,5)
        tr.Add(b + 1); tr.Add(b + 2); tr.Add(b + 6); tr.Add(b + 1); tr.Add(b + 6); tr.Add(b + 5);
    }

    private static Mesh MakeMesh(List<Vector3> v, List<int> tr, string name)
    {
        var m = new Mesh { name = name };
        m.SetVertices(v);
        m.SetTriangles(tr, 0);
        m.RecalculateBounds();
        m.RecalculateNormals();
        return m;
    }
}
