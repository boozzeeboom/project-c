using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace ProjectC.EditorTools
{
    /// <summary>
    /// Точный bump для процедурных Shader Graph проекта (PRIMUM Э3.1, шаг 2h).
    ///
    /// Штатный узел «Normal From Height» берёт ddx/ddy от мировой позиции. Город стоит в ~40 км от
    /// начала координат, шаг float там ≈ 3.9 мм → производная позиции «ступенчатая» → попиксельная
    /// чёрная грануляция на любом рельефе. Патчер заменяет КАЖДЫЙ узел Normal From Height в графе на
    /// Custom Function <c>PC_NormalFromHeightPrecise</c> (<c>Assets/_Project/Shaders/ProceduralBumpPrecise.hlsl</c>):
    /// та же формула, но производная берётся от object-space позиции и переводится матрицей объекта.
    ///
    /// Контракт замены: ObjectId узла и номера слотов In = 0, Out = 1, Strength = 2 сохраняются — все
    /// существующие рёбра остаются как есть. Добавляются входы 3–6 (PositionOS, NormalWS, TangentWS,
    /// BitangentWS) и при необходимости узлы Position(Object) и Normal/Tangent/Bitangent Vector (World).
    /// Выход — tangent space, как у штатного узла (мастер-стек не меняется).
    ///
    /// Перед записью — копия графа в <c>ShaderGraphBackup/</c> в корне проекта (Temp/ чистится при закрытии
    /// редактора; *.shadergraph в Git не хранится, поэтому это единственная точка отката).
    /// Замороженный эталон <c>BronzeEdgeNoise_forTEST.shadergraph</c> не трогается никогда.
    /// Формат документа: поток JSON-документов, разделитель ровно «}\n\n{», пустых строк внутри нет.
    /// </summary>
    public static class PrecisionBumpPatcher
    {
        private const string HlslPath = "Assets/_Project/Shaders/ProceduralBumpPrecise.hlsl";
        private const string FunctionName = "PC_NormalFromHeightPrecise";
        private const string FrozenGraph = "BronzeEdgeNoise_forTEST.shadergraph";
        private const string NfhType = "UnityEditor.ShaderGraph.NormalFromHeightNode";

        [MenuItem("ProjectC/Materials/Точный bump: заменить Normal From Height в выделенном графе", false, 400)]
        public static void MenuPatchSelected()
        {
            var sb = new StringBuilder();
            foreach (var o in Selection.objects)
            {
                string p = AssetDatabase.GetAssetPath(o);
                if (p.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase)) sb.AppendLine(Patch(p));
            }
            Debug.Log("[PrecisionBumpPatcher]\n" + (sb.Length > 0 ? sb.ToString() : "выделите .shadergraph"));
        }

        public static string Patch(string graphPath) => Patch(graphPath, false);

        /// <summary>
        /// Патч одного графа. <paramref name="normalizeLf"/> = true: CRLF-граф перед патчем переводится в LF
        /// (Unity импортирует оба варианта; backup — исходный CRLF-файл).
        /// </summary>
        public static string Patch(string graphPath, bool normalizeLf)
        {
            if (graphPath.EndsWith(FrozenGraph, StringComparison.Ordinal)) return "SKIP " + graphPath + ": замороженный эталон";
            string guid = AssetDatabase.AssetPathToGUID(HlslPath);
            if (string.IsNullOrEmpty(guid)) return "ERROR: нет " + HlslPath;

            string raw = File.ReadAllText(graphPath);
            bool wasCrlf = raw.Contains("\r\n");
            if (wasCrlf && !normalizeLf) return "SKIP " + graphPath + ": CRLF (нормализовать в LF перед патчем)";
            if (wasCrlf) raw = raw.Replace("\r\n", "\n");
            string[] docs = raw.Split(new[] { "}\n\n{" }, StringSplitOptions.None);
            // восстановить полные документы с фигурными скобками
            for (int i = 0; i < docs.Length; i++)
            {
                if (i > 0) docs[i] = "{" + docs[i];
                if (i < docs.Length - 1) docs[i] = docs[i] + "}";
            }
            var list = new List<string>(docs);

            var nfhIdx = new List<int>();
            for (int i = 0; i < list.Count; i++)
                if (list[i].Contains("\"m_Type\": \"" + NfhType + "\"")) nfhIdx.Add(i);
            if (nfhIdx.Count == 0) return "OK " + graphPath + ": узлов Normal From Height нет (уже заменены?)";

            foreach (int i in nfhIdx)
                if (!Regex.IsMatch(list[i], "\"m_OutputSpace\":\\s*0"))
                    return "SKIP " + graphPath + ": Normal From Height не в tangent space";

            Directory.CreateDirectory("ShaderGraphBackup");
            string backup = "ShaderGraphBackup/" + Path.GetFileNameWithoutExtension(graphPath) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".shadergraph";
            File.Copy(graphPath, backup, true);

            var newNodeIds = new List<string>();
            var newEdges = new List<string[]>(); // out node, out slot, in node, in slot

            // Position (Object): переиспользовать существующий или создать
            string posId = null;
            foreach (var d in list)
                if (d.Contains("\"m_Type\": \"UnityEditor.ShaderGraph.PositionNode\"") &&
                    Regex.IsMatch(d, "\"m_Space\":\\s*0") && Regex.IsMatch(d, "\"m_PositionSource\":\\s*0"))
                { posId = Id(d); break; }
            if (posId == null)
            {
                posId = NewId(); string slot = NewId();
                list.Add(GeometryNode("UnityEditor.ShaderGraph.PositionNode", posId, "Position", 0, slot, true));
                list.Add(OutSlot(slot));
                newNodeIds.Add(posId);
            }

            string nId = NewId(), tId = NewId(), bId = NewId();
            string nSlot = NewId(), tSlot = NewId(), bSlot = NewId();
            list.Add(GeometryNode("UnityEditor.ShaderGraph.NormalVectorNode", nId, "Normal Vector", 2, nSlot, false)); list.Add(OutSlot(nSlot));
            list.Add(GeometryNode("UnityEditor.ShaderGraph.TangentVectorNode", tId, "Tangent Vector", 2, tSlot, false)); list.Add(OutSlot(tSlot));
            list.Add(GeometryNode("UnityEditor.ShaderGraph.BitangentVectorNode", bId, "Bitangent Vector", 2, bSlot, false)); list.Add(OutSlot(bSlot));
            newNodeIds.Add(nId); newNodeIds.Add(tId); newNodeIds.Add(bId);

            foreach (int i in nfhIdx)
            {
                string doc = list[i];
                string nodeId = Id(doc);
                var slotIds = new List<string>();
                foreach (Match m in Regex.Matches(Between(doc, "\"m_Slots\":", "]"), "\"m_Id\":\\s*\"(\\w{32})\"")) slotIds.Add(m.Groups[1].Value);
                string inSlot = null, outSlot = null, strSlot = null;
                foreach (var sid in slotIds)
                {
                    int si = list.FindIndex(x => x.Contains("\"m_ObjectId\": \"" + sid + "\""));
                    var sd = list[si];
                    int num = int.Parse(Regex.Match(sd, "\"m_Id\":\\s*(-?\\d+)").Groups[1].Value);
                    if (num == 0) inSlot = sid; else if (num == 1) outSlot = sid; else if (num == 2) strSlot = sid;
                }
                if (inSlot == null || outSlot == null || strSlot == null) return "ERROR " + graphPath + ": у Normal From Height нет слотов 0/1/2";

                string s3 = NewId(), s4 = NewId(), s5 = NewId(), s6 = NewId();
                list.Add(InSlot3(s3, 3, "PositionOS"));
                list.Add(InSlot3(s4, 4, "NormalWS"));
                list.Add(InSlot3(s5, 5, "TangentWS"));
                list.Add(InSlot3(s6, 6, "BitangentWS"));

                string drawState = Between(doc, "\"m_DrawState\":", "\"m_Slots\"").Trim().TrimEnd(',');
                list[i] =
                    "{\n" +
                    "    \"m_SGVersion\": 1,\n" +
                    "    \"m_Type\": \"UnityEditor.ShaderGraph.CustomFunctionNode\",\n" +
                    "    \"m_ObjectId\": \"" + nodeId + "\",\n" +
                    "    \"m_Group\": {\"m_Id\": \"\"},\n" +
                    "    \"m_Name\": \"" + FunctionName + " (Custom Function)\",\n" +
                    "    \"m_DrawState\": " + drawState + ",\n" +
                    "    \"m_Slots\": [{\"m_Id\": \"" + inSlot + "\"},{\"m_Id\": \"" + strSlot + "\"},{\"m_Id\": \"" + s3 + "\"},{\"m_Id\": \"" + s4 + "\"},{\"m_Id\": \"" + s5 + "\"},{\"m_Id\": \"" + s6 + "\"},{\"m_Id\": \"" + outSlot + "\"}],\n" +
                    "    \"synonyms\": [\"code\",\"HLSL\"],\n" +
                    "    \"m_Precision\": 0,\n" +
                    "    \"m_PreviewExpanded\": false,\n" +
                    "    \"m_DismissedVersion\": 0,\n" +
                    "    \"m_PreviewMode\": 0,\n" +
                    "    \"m_CustomColors\": {\"m_SerializableColors\": []},\n" +
                    "    \"m_SourceType\": 0,\n" +
                    "    \"m_FunctionName\": \"" + FunctionName + "\",\n" +
                    "    \"m_FunctionSource\": \"" + guid + "\",\n" +
                    "    \"m_FunctionSourceUsePragmas\": true,\n" +
                    "    \"m_FunctionBody\": \"// precise NFH\"\n" +
                    "}";

                newEdges.Add(new[] { posId, "0", nodeId, "3" });
                newEdges.Add(new[] { nId, "0", nodeId, "4" });
                newEdges.Add(new[] { tId, "0", nodeId, "5" });
                newEdges.Add(new[] { bId, "0", nodeId, "6" });
            }

            // корень графа: узлы и рёбра
            string root = list[0];
            var nodesSb = new StringBuilder();
            foreach (var id in newNodeIds) nodesSb.Append("\n        {\n            \"m_Id\": \"" + id + "\"\n        },");
            root = InsertIntoList(root, "\"m_Nodes\": [", nodesSb.ToString());
            var edgesSb = new StringBuilder();
            foreach (var e in newEdges)
                edgesSb.Append("\n        {\n            \"m_OutputSlot\": {\n                \"m_Node\": {\n                    \"m_Id\": \"" + e[0] + "\"\n                },\n                \"m_SlotId\": " + e[1] +
                               "\n            },\n            \"m_InputSlot\": {\n                \"m_Node\": {\n                    \"m_Id\": \"" + e[2] + "\"\n                },\n                \"m_SlotId\": " + e[3] + "\n            }\n        },");
            root = InsertIntoList(root, "\"m_Edges\": [", edgesSb.ToString());
            list[0] = root;

            // сборка: внутренние документы без пустых строк, разделитель ровно "\n\n"
            var outSb = new StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) outSb.Append("\n\n");
                outSb.Append(Regex.Replace(list[i], "\n[ \t]*\n", "\n"));
            }
            string result = outSb.ToString();
            if (!result.EndsWith("\n")) result += "\n";
            File.WriteAllText(graphPath, result, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(graphPath, ImportAssetOptions.ForceUpdate);

            return "PATCHED " + graphPath + (wasCrlf ? " (CRLF→LF)" : "") + ": Normal From Height → " + FunctionName + " ×" + nfhIdx.Count +
                   ", новых узлов " + (newNodeIds.Count) + ", новых рёбер " + newEdges.Count + ", backup " + backup;
        }

        /// <summary>
        /// Патч + проверка (импорт, ShaderHasError, сообщения, в сгенерированном коде нет штатного узла и есть
        /// вызов PC_NormalFromHeightPrecise). При провале проверки граф восстанавливается из backup.
        /// </summary>
        public static string PatchAndVerify(string graphPath)
        {
            string r = Patch(graphPath, true);
            if (!r.StartsWith("PATCHED")) return r;
            string backup = r.Substring(r.LastIndexOf("backup ", StringComparison.Ordinal) + 7).Trim();

            var sh = AssetDatabase.LoadAssetAtPath<Shader>(graphPath);
            string problem = null;
            if (sh == null) problem = "шейдер не загружен";
            else if (ShaderUtil.ShaderHasError(sh)) problem = "ShaderHasError";
            else
            {
                foreach (var m in ShaderUtil.GetShaderMessages(sh))
                    if (m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error) { problem = "compile: " + m.message; break; }
            }
            if (problem == null)
            {
                string code = GeneratedCode(graphPath);
                if (code == null) problem = "нет сгенерированного кода";
                else if (code.Contains("Unity_NormalFromHeight_")) problem = "штатный узел остался в коде";
                else if (!code.Contains(FunctionName + "_float(")) problem = "нет вызова " + FunctionName;
            }
            if (problem != null)
            {
                File.Copy(backup, graphPath, true);
                AssetDatabase.ImportAsset(graphPath, ImportAssetOptions.ForceUpdate);
                return "RESTORED " + graphPath + ": " + problem + " (возвращён backup " + backup + ")";
            }
            return r + " | verify OK";
        }

        /// <summary>Сгенерированный текст шейдера графа (через внутренний ShaderGraphImporter.GetShaderText).</summary>
        public static string GeneratedCode(string graphPath)
        {
            var imp = AssetImporter.GetAtPath(graphPath);
            if (imp == null) return null;
            foreach (var mi in imp.GetType().GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            {
                if (mi.Name != "GetShaderText" || mi.GetParameters().Length != 4) continue;
                var ps = mi.GetParameters();
                try { return (string)mi.Invoke(null, new object[] { graphPath, null, Activator.CreateInstance(ps[2].ParameterType), null }); }
                catch { return null; }
            }
            return null;
        }

        // ------------------------------------------------------------------ точная object-space позиция (custom interpolator)

        private const string PosInterpName = "PC_PositionOS";

        /// <summary>
        /// Шаг 2p: во фрагменте Shader Graph считает <c>ObjectSpacePosition = TransformWorldToObject(positionWS)</c> — из
        /// АБСОЛЮТНОЙ мировой позиции, которая в ~40 км от начала координат квантована шагом ~4 мм. Все
        /// процедурные узоры и их производные (рельеф) считаются на этой «сетке» → шум рельефа, которого
        /// нет в превью (сфера в начале координат). Патч протаскивает точную <c>positionOS</c> вершины во
        /// фрагмент через custom interpolator <c>PC_PositionOS</c> и переводит на него все выходы узлов
        /// Position (Object). Узел Position остаётся и питает блок интерполятора в Vertex-контексте (там он равен
        /// <c>input.positionOS</c> — точно). FO-safe: мировые координаты в узор не входят. Идемпотентно.
        /// </summary>
        public static string PatchPreciseObjectPos(string graphPath) => PatchPreciseObjectPos(graphPath, true);

        public static string PatchPreciseObjectPos(string graphPath, bool restoreOnFail)
        {
            if (graphPath.EndsWith(FrozenGraph, StringComparison.Ordinal)) return "SKIP " + graphPath + ": замороженный эталон";
            string raw = File.ReadAllText(graphPath);
            if (raw.Contains("\"customBlockNodeName\": \"" + PosInterpName + "\"")) return "OK " + graphPath + ": уже есть " + PosInterpName;
            bool wasCrlf = raw.Contains("\r\n");
            if (wasCrlf) raw = raw.Replace("\r\n", "\n");
            string[] docs = raw.Split(new[] { "}\n\n{" }, StringSplitOptions.None);
            for (int i = 0; i < docs.Length; i++)
            {
                if (i > 0) docs[i] = "{" + docs[i];
                if (i < docs.Length - 1) docs[i] = docs[i] + "}";
            }
            var list = new List<string>(docs);

            var posIds = new List<string>();
            foreach (var d in list)
                if (d.Contains("\"m_Type\": \"UnityEditor.ShaderGraph.PositionNode\"") &&
                    Regex.IsMatch(d, "\"m_Space\":\\s*0") && Regex.IsMatch(d, "\"m_PositionSource\":\\s*0"))
                    posIds.Add(Id(d));
            if (posIds.Count == 0) return "SKIP " + graphPath + ": нет узла Position (Object)";

            Directory.CreateDirectory("ShaderGraphBackup");
            string backup = "ShaderGraphBackup/" + Path.GetFileNameWithoutExtension(graphPath) + "_pos_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".shadergraph";
            File.Copy(graphPath, backup, true);

            string blockId = NewId(), blockSlot = NewId(), ciId = NewId(), ciSlot = NewId();
            list.Add("{\n" +
                     "    \"m_SGVersion\": 0,\n" +
                     "    \"m_Type\": \"UnityEditor.ShaderGraph.BlockNode\",\n" +
                     "    \"m_ObjectId\": \"" + blockId + "\",\n" +
                     "    \"m_Group\": {\"m_Id\": \"\"},\n" +
                     "    \"m_Name\": \"VertexDescription.CustomInterpolator\",\n" +
                     "    \"m_DrawState\": {\"m_Expanded\": true,\"m_Position\": {\"serializedVersion\": \"2\",\"x\": 0.0,\"y\": 0.0,\"width\": 0.0,\"height\": 0.0}},\n" +
                     "    \"m_Slots\": [{\"m_Id\": \"" + blockSlot + "\"}],\n" +
                     "    \"synonyms\": [],\n" +
                     "    \"m_Precision\": 0,\n" +
                     "    \"m_PreviewExpanded\": true,\n" +
                     "    \"m_DismissedVersion\": 0,\n" +
                     "    \"m_PreviewMode\": 0,\n" +
                     "    \"m_CustomColors\": {\"m_SerializableColors\": []},\n" +
                     "    \"m_SerializedDescriptor\": \"VertexDescription." + PosInterpName + "#3\"\n" +
                     "}");
            list.Add("{\n" +
                     "    \"m_SGVersion\": 0,\n" +
                     "    \"m_Type\": \"UnityEditor.ShaderGraph.Vector3MaterialSlot\",\n" +
                     "    \"m_ObjectId\": \"" + blockSlot + "\",\n" +
                     "    \"m_Id\": 0,\n" +
                     "    \"m_DisplayName\": \"" + PosInterpName + "\",\n" +
                     "    \"m_SlotType\": 0,\n" +
                     "    \"m_Hidden\": false,\n" +
                     "    \"m_ShaderOutputName\": \"" + PosInterpName + "\",\n" +
                     "    \"m_StageCapability\": 1,\n" +
                     "    \"m_Value\": {\"x\": 0.0,\"y\": 0.0,\"z\": 0.0},\n" +
                     "    \"m_DefaultValue\": {\"x\": 0.0,\"y\": 0.0,\"z\": 0.0},\n" +
                     "    \"m_Labels\": []\n" +
                     "}");
            list.Add("{\n" +
                     "    \"m_SGVersion\": 0,\n" +
                     "    \"m_Type\": \"UnityEditor.ShaderGraph.CustomInterpolatorNode\",\n" +
                     "    \"m_ObjectId\": \"" + ciId + "\",\n" +
                     "    \"m_Group\": {\"m_Id\": \"\"},\n" +
                     "    \"m_Name\": \"" + PosInterpName + " (Custom Interpolator)\",\n" +
                     "    \"m_DrawState\": {\"m_Expanded\": true,\"m_Position\": {\"serializedVersion\": \"2\",\"x\": -4400,\"y\": -2400,\"width\": 232.0,\"height\": 94.0}},\n" +
                     "    \"m_Slots\": [{\"m_Id\": \"" + ciSlot + "\"}],\n" +
                     "    \"synonyms\": [],\n" +
                     "    \"m_Precision\": 0,\n" +
                     "    \"m_PreviewExpanded\": false,\n" +
                     "    \"m_DismissedVersion\": 0,\n" +
                     "    \"m_PreviewMode\": 0,\n" +
                     "    \"m_CustomColors\": {\"m_SerializableColors\": []},\n" +
                     "    \"customBlockNodeName\": \"" + PosInterpName + "\",\n" +
                     "    \"serializedType\": 3\n" +
                     "}");
            list.Add("{\n" +
                     "    \"m_SGVersion\": 0,\n" +
                     "    \"m_Type\": \"UnityEditor.ShaderGraph.Vector3MaterialSlot\",\n" +
                     "    \"m_ObjectId\": \"" + ciSlot + "\",\n" +
                     "    \"m_Id\": 0,\n" +
                     "    \"m_DisplayName\": \"Out\",\n" +
                     "    \"m_SlotType\": 1,\n" +
                     "    \"m_Hidden\": false,\n" +
                     "    \"m_ShaderOutputName\": \"Out\",\n" +
                     "    \"m_StageCapability\": 2,\n" +
                     "    \"m_Value\": {\"x\": 0.0,\"y\": 0.0,\"z\": 0.0},\n" +
                     "    \"m_DefaultValue\": {\"x\": 0.0,\"y\": 0.0,\"z\": 0.0},\n" +
                     "    \"m_Labels\": []\n" +
                     "}");

            // корень: перенаправить выходы Position(Object) на интерполятор — КРОМЕ рёбер в цепочки Vertex-контекста
            // (например, ветер/смещение вершин у Foliage): интерполятор существует только во фрагменте.
            string root = list[0];
            var edgeRx = new Regex("\"m_OutputSlot\":\\s*\\{\\s*\"m_Node\":\\s*\\{\\s*\"m_Id\":\\s*\"(\\w+)\"\\s*\\},\\s*\"m_SlotId\":\\s*(-?\\d+)\\s*\\},\\s*\"m_InputSlot\":\\s*\\{\\s*\"m_Node\":\\s*\\{\\s*\"m_Id\":\\s*\"(\\w+)\"");
            var allEdges = new List<string[]>();
            foreach (Match m in edgeRx.Matches(root)) allEdges.Add(new[] { m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value });
            var vertexNodes = new HashSet<string>();
            {
                int vc0 = root.IndexOf("\"m_VertexContext\"", StringComparison.Ordinal);
                int fc0 = root.IndexOf("\"m_FragmentContext\"", StringComparison.Ordinal);
                if (vc0 >= 0 && fc0 > vc0)
                    foreach (Match m in Regex.Matches(root.Substring(vc0, fc0 - vc0), "\"m_Id\":\\s*\"(\\w{32})\"")) vertexNodes.Add(m.Groups[1].Value);
                bool grew = true;
                while (grew)
                {
                    grew = false;
                    foreach (var e in allEdges) if (vertexNodes.Contains(e[2]) && vertexNodes.Add(e[0])) grew = true;
                }
            }
            int redirected = 0, keptVertex = 0;
            foreach (var pid in posIds)
            {
                var rx = new Regex("(\"m_OutputSlot\":\\s*\\{\\s*\"m_Node\":\\s*\\{\\s*\"m_Id\":\\s*\")" + pid + "(\"\\s*\\},\\s*\"m_SlotId\":\\s*0\\s*\\},\\s*\"m_InputSlot\":\\s*\\{\\s*\"m_Node\":\\s*\\{\\s*\"m_Id\":\\s*\")(\\w+)");
                root = rx.Replace(root, m =>
                {
                    if (vertexNodes.Contains(m.Groups[3].Value)) { keptVertex++; return m.Value; }
                    redirected++;
                    return m.Groups[1].Value + ciId + m.Groups[2].Value + m.Groups[3].Value;
                });
            }
            root = InsertIntoList(root, "\"m_Nodes\": [", "\n        {\n            \"m_Id\": \"" + ciId + "\"\n        },\n        {\n            \"m_Id\": \"" + blockId + "\"\n        },");
            root = InsertIntoList(root, "\"m_Edges\": [",
                "\n        {\n            \"m_OutputSlot\": {\n                \"m_Node\": {\n                    \"m_Id\": \"" + posIds[0] + "\"\n                },\n                \"m_SlotId\": 0" +
                "\n            },\n            \"m_InputSlot\": {\n                \"m_Node\": {\n                    \"m_Id\": \"" + blockId + "\"\n                },\n                \"m_SlotId\": 0\n            }\n        },");
            int vctx = root.IndexOf("\"m_VertexContext\"", StringComparison.Ordinal);
            if (vctx < 0) { File.Copy(backup, graphPath, true); return "ERROR " + graphPath + ": нет m_VertexContext"; }
            int blocks = root.IndexOf("\"m_Blocks\": [", vctx, StringComparison.Ordinal);
            root = root.Substring(0, blocks) + InsertIntoList(root.Substring(blocks), "\"m_Blocks\": [", "\n            {\n                \"m_Id\": \"" + blockId + "\"\n            },");
            list[0] = root;

            var outSb = new StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) outSb.Append("\n\n");
                outSb.Append(Regex.Replace(list[i], "\n[ \t]*\n", "\n"));
            }
            string result = outSb.ToString();
            if (!result.EndsWith("\n")) result += "\n";
            File.WriteAllText(graphPath, result, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(graphPath, ImportAssetOptions.ForceUpdate);

            // проверка + авто-откат
            var sh = AssetDatabase.LoadAssetAtPath<Shader>(graphPath);
            string problem = null;
            if (sh == null) problem = "шейдер не загружен";
            else if (ShaderUtil.ShaderHasError(sh)) problem = "ShaderHasError";
            if (problem == null)
            {
                string code = GeneratedCode(graphPath);
                if (code == null) problem = "нет сгенерированного кода";
                else if (!code.Contains(PosInterpName)) problem = "интерполятор не попал в код";
            }
            if (problem != null)
            {
                if (!restoreOnFail) return "FAILED (не откачено) " + graphPath + ": " + problem + ", backup " + backup;
                File.Copy(backup, graphPath, true);
                AssetDatabase.ImportAsset(graphPath, ImportAssetOptions.ForceUpdate);
                return "RESTORED " + graphPath + ": " + problem;
            }
            return "PATCHED " + graphPath + (wasCrlf ? " (CRLF→LF)" : "") + ": Position(Object) ×" + posIds.Count + " → " + PosInterpName +
                   ", перенаправлено рёбер " + redirected + (keptVertex > 0 ? " (в Vertex-цепочке оставлено " + keptVertex + ")" : "") + ", backup " + backup + " | verify OK";
        }

        // ------------------------------------------------------------------ JSON-шаблоны

        private static string GeometryNode(string type, string id, string name, int space, string slot, bool isPosition)
        {
            return "{\n" +
                   "    \"m_SGVersion\": 1,\n" +
                   "    \"m_Type\": \"" + type + "\",\n" +
                   "    \"m_ObjectId\": \"" + id + "\",\n" +
                   "    \"m_Group\": {\"m_Id\": \"\"},\n" +
                   "    \"m_Name\": \"" + name + "\",\n" +
                   "    \"m_DrawState\": {\"m_Expanded\": true,\"m_Position\": {\"serializedVersion\": \"2\",\"x\": 12400,\"y\": 5200,\"width\": 208.0,\"height\": 300.0}},\n" +
                   "    \"m_Slots\": [{\"m_Id\": \"" + slot + "\"}],\n" +
                   "    \"synonyms\": [],\n" +
                   "    \"m_Precision\": 1,\n" +
                   "    \"m_PreviewExpanded\": false,\n" +
                   "    \"m_DismissedVersion\": 0,\n" +
                   "    \"m_PreviewMode\": 2,\n" +
                   "    \"m_CustomColors\": {\"m_SerializableColors\": []},\n" +
                   "    \"m_Space\": " + space + (isPosition ? ",\n    \"m_PositionSource\": 0\n" : "\n") +
                   "}";
        }

        private static string OutSlot(string id)
        {
            return "{\n" +
                   "    \"m_SGVersion\": 0,\n" +
                   "    \"m_Type\": \"UnityEditor.ShaderGraph.Vector3MaterialSlot\",\n" +
                   "    \"m_ObjectId\": \"" + id + "\",\n" +
                   "    \"m_Id\": 0,\n" +
                   "    \"m_DisplayName\": \"Out\",\n" +
                   "    \"m_SlotType\": 1,\n" +
                   "    \"m_Hidden\": false,\n" +
                   "    \"m_HideConnector\": false,\n" +
                   "    \"m_ShaderOutputName\": \"Out\",\n" +
                   "    \"m_StageCapability\": 3,\n" +
                   "    \"m_CustomBinding\": \"\",\n" +
                   "    \"m_Value\": {\"x\":0.0,\"y\":0.0,\"z\":0.0},\n" +
                   "    \"m_DefaultValue\": {\"x\":0.0,\"y\":0.0,\"z\":0.0},\n" +
                   "    \"m_Labels\": []\n" +
                   "}";
        }

        private static string InSlot3(string id, int num, string name)
        {
            return "{\n" +
                   "    \"m_SGVersion\": 0,\n" +
                   "    \"m_Type\": \"UnityEditor.ShaderGraph.Vector3MaterialSlot\",\n" +
                   "    \"m_ObjectId\": \"" + id + "\",\n" +
                   "    \"m_Id\": " + num + ",\n" +
                   "    \"m_DisplayName\": \"" + name + "\",\n" +
                   "    \"m_SlotType\": 0,\n" +
                   "    \"m_Hidden\": false,\n" +
                   "    \"m_HideConnector\": false,\n" +
                   "    \"m_ShaderOutputName\": \"" + name + "\",\n" +
                   "    \"m_StageCapability\": 2,\n" +
                   "    \"m_CustomBinding\": \"\",\n" +
                   "    \"m_Value\": {\"x\":0.0,\"y\":0.0,\"z\":0.0},\n" +
                   "    \"m_DefaultValue\": {\"x\":0.0,\"y\":0.0,\"z\":0.0},\n" +
                   "    \"m_Labels\": []\n" +
                   "}";
        }

        // ------------------------------------------------------------------ утилиты

        private static string NewId() => Guid.NewGuid().ToString("N");

        private static string Id(string doc) => Regex.Match(doc, "\"m_ObjectId\":\\s*\"(\\w+)\"").Groups[1].Value;

        private static string Between(string s, string a, string b)
        {
            int i = s.IndexOf(a, StringComparison.Ordinal);
            if (i < 0) return "";
            i += a.Length;
            int j = s.IndexOf(b, i, StringComparison.Ordinal);
            return j < 0 ? s.Substring(i) : s.Substring(i, j - i + (b == "]" ? 1 : 0));
        }

        private static string InsertIntoList(string root, string listHead, string items)
        {
            int i = root.IndexOf(listHead, StringComparison.Ordinal);
            if (i < 0) throw new Exception("нет " + listHead);
            int after = i + listHead.Length;
            // пустой список "[]"
            if (root.Substring(after).TrimStart().StartsWith("]"))
                return root.Substring(0, after) + items.TrimEnd(',') + "\n    " + root.Substring(after).TrimStart();
            return root.Substring(0, after) + items + root.Substring(after);
        }
    }
}
