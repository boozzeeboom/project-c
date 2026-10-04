using UnityEditor;
using UnityEngine;

namespace ProjectC.EditorTools
{
    // SteelDiag — численная диагностика ProceduralSteel без скриншотов и Play Mode.
    // Рендерит тестовый куб в offscreen RT и возвращает mean/white/coverage.
    // Якоря: Z_base (фон без куба), Y_litRed (контрольный URP Lit).
    // T-STEEL01.
    public static class SteelDiag
    {
        [MenuItem("Tools/ProjectC/Materials/Run Steel Diag (logs numbers)...")]
        public static void RunMenu() { Debug.Log("[SteelDiag] " + Run()); }

        // Факты о меше выбранного объекта: нормали/цвета/материал/масштаб.
        [MenuItem("Tools/ProjectC/Materials/Dump Selected Mesh Stats...")]
        public static void DumpMenu()
        {
            var gos = UnityEditor.Selection.gameObjects;
            if (gos == null || gos.Length == 0) { Debug.LogWarning("[SteelDiag] Ничего не выбрано."); return; }
            foreach (var go in gos)
            {
                var mf = go.GetComponent<MeshFilter>();
                var rend = go.GetComponent<Renderer>();
                if (mf == null || mf.sharedMesh == null) { Debug.Log("[SteelDiag] " + go.name + ": нет меша."); continue; }
                Mesh m = mf.sharedMesh;
                string n0 = (m.normals != null && m.normals.Length > 0)
                    ? "(" + m.normals[0].x.ToString("F2") + "," + m.normals[0].y.ToString("F2") + "," + m.normals[0].z.ToString("F2") + ")" : "NONE";
                string c0 = (m.colors != null && m.colors.Length > 0)
                    ? "(" + m.colors[0].r.ToString("F2") + "," + m.colors[0].g.ToString("F2") + "," + m.colors[0].b.ToString("F2") + ")" : "NONE(white in shader)";
                string mat = "none";
                if (rend != null && rend.sharedMaterial != null)
                    mat = rend.sharedMaterial.name + " / " + rend.sharedMaterial.shader.name;
                Debug.Log("[SteelDiag] " + go.name + ": mesh=" + m.name
                    + " verts=" + m.vertexCount + " readable=" + m.isReadable
                    + " normals=" + (m.normals == null ? 0 : m.normals.Length) + " n0=" + n0
                    + " colors=" + (m.colors == null ? 0 : m.colors.Length) + " c0=" + c0
                    + " scale=" + go.transform.lossyScale.ToString()
                    + " mat=[" + mat + "]", go);
            }
        }

        public static string Run()
        {
            var steel = Shader.Find("ProjectC/ProceduralSteel");
            if (steel == null) return "ERR steel shader not found";
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) return "ERR urp lit not found";

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "DiagCube_tmp";
            go.transform.position = Vector3.zero;
            Renderer rend = go.GetComponent<Renderer>();

            GameObject goCam = new GameObject("DiagCam_tmp");
            Camera cam = goCam.AddComponent<Camera>();
            cam.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.transform.position = new Vector3(0, 0, -4);
            cam.transform.LookAt(Vector3.zero);

            string acc = "";
            go.SetActive(false);
            acc += Case(cam, null, "Z_base", 0f, 0f, 0f);
            go.SetActive(true);

            Material red = new Material(lit);
            red.color = new Color(1f, 0.1f, 0.1f, 1f);
            rend.sharedMaterial = red;
            acc += Case(cam, red, "Y_litRed", 0f, 0f, 0f);

            Material m = new Material(steel);
            rend.sharedMaterial = m;
            acc += Case(cam, m, "A_cavVC1", 0f, 2f, 1f);
            acc += Case(cam, m, "B_bandVC1", 1f, 3f, 1f);
            acc += Case(cam, m, "C_bandVC0", 1f, 3f, 0f);
            acc += Case(cam, m, "D_final", 1f, 0f, 0f);
            acc += Case(cam, m, "E_pattern", 1f, 4f, 0f);

            cam.targetTexture = null;
            Object.DestroyImmediate(m);
            Object.DestroyImmediate(red);
            Object.DestroyImmediate(goCam);
            Object.DestroyImmediate(go);
            return acc;
        }

        private static string Case(Camera cam, Material m, string name, float deriv, float view, float vc)
        {
            if (m != null && m.shader != null && m.shader.name == "ProjectC/ProceduralSteel")
            {
                m.SetFloat("_DerivEdges", deriv);
                m.SetFloat("_DebugView", view);
                m.SetFloat("_UseVertexColors", vc);
            }
            RenderTexture rt = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            Texture2D tex = new Texture2D(256, 256, TextureFormat.RGB24, false);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            Color32[] px = tex.GetPixels32();
            long sumF = 0, sumC = 0;
            int nF = 0, nC = 0, wC = 0;
            for (int y = 0; y < 256; y++)
            {
                for (int x = 0; x < 256; x++)
                {
                    Color32 p = px[y * 256 + x];
                    int lum = (p.r + p.g + p.b) / 3;
                    if (lum > 8) { nF++; sumF += lum; }
                    if (x >= 96 && x < 160 && y >= 96 && y < 160)
                    {
                        nC++; sumC += lum;
                        if (lum > 200) wC++;
                    }
                }
            }
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
            double fullMean = nF == 0 ? 0 : sumF / (255.0 * nF);
            double cropMean = nC == 0 ? 0 : sumC / (255.0 * nC);
            return name + " full=" + fullMean.ToString("F3")
                + " crop=" + cropMean.ToString("F3")
                + " cropWhite=" + (wC / (float)nC).ToString("F3") + "; ";
        }
    }
}
