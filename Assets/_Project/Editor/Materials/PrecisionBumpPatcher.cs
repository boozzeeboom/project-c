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

        public static string Patch(string graphPath)
        {
            if (graphPath.EndsWith(FrozenGraph, StringComparison.Ordinal)) return "SKIP " + graphPath + ": замороженный эталон";
            string guid = AssetDatabase.AssetPathToGUID(HlslPath);
            if (string.IsNullOrEmpty(guid)) return "ERROR: нет " + HlslPath;

            string raw = File.ReadAllText(graphPath);
            if (raw.Contains("\r\n")) return "SKIP " + graphPath + ": CRLF (нормализовать в LF перед патчем)";
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

            return "PATCHED " + graphPath + ": Normal From Height → " + FunctionName + " ×" + nfhIdx.Count +
                   ", новых узлов " + (newNodeIds.Count) + ", новых рёбер " + newEdges.Count + ", backup " + backup;
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
