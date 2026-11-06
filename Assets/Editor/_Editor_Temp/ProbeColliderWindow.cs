using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Открывает окно Settlement Colliders (проверка фолдаутов на исключения в OnGUI)
/// и ищет конфликты хоткеев %&amp;b / %&amp;s среди всех MenuItem в проекте и пакетах.
/// </summary>
public static class ProbeColliderWindow
{
    public static void Execute()
    {
        var sb = new StringBuilder();
        try
        {
            Type t = FindType("ProjectC.EditorTools.BuildSettlementCollidersWindow");
            sb.AppendLine($"[PROBE-WIN] окно найдено: {t != null}");

            // Все MenuItem с хоткеями %&b / %&s
            var found = new List<string>();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types; }
                if (types == null) continue;
                foreach (var type in types)
                {
                    if (type == null) continue;
                    MethodInfo[] methods;
                    try { methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly); }
                    catch { continue; }
                    foreach (var m in methods)
                    {
                        MenuItem[] attrs;
                        try { attrs = (MenuItem[])m.GetCustomAttributes(typeof(MenuItem), false); }
                        catch { continue; }
                        foreach (var a in attrs)
                        {
                            if (a.menuItem != null && (a.menuItem.Contains("%&b") || a.menuItem.Contains("%&s")))
                                found.Add($"{a.menuItem}  ← {type.FullName}.{m.Name}");
                        }
                    }
                }
            }
            sb.AppendLine("  MenuItem с %&b / %&s: " + (found.Count == 0 ? "нет" : string.Join(" | ", found)));

            if (t != null)
            {
                var w = EditorWindow.GetWindow(t, false, "Settlement Colliders");
                w.minSize = new Vector2(380f, 660f);
                w.Repaint();
                sb.AppendLine($"  окно открыто: {w != null}, title={w.titleContent.text}, размер={w.position.width}x{w.position.height}");

                // Значения фолдаутов после OnEnable (читаем приватные поля)
                var flags = new[] { "foldFilters", "foldShapes", "foldBuild", "foldSlice", "foldExact", "foldStairs", "foldCollect", "foldMerge" };
                var vals = new List<string>();
                foreach (var f in flags)
                {
                    var fi = t.GetField(f, BindingFlags.NonPublic | BindingFlags.Instance);
                    vals.Add(f + "=" + (fi == null ? "?" : fi.GetValue(w)));
                }
                sb.AppendLine("  фолдауты: " + string.Join(", ", vals));

                var settingsField = t.GetField("settings", BindingFlags.NonPublic | BindingFlags.Instance);
                var s = settingsField != null ? settingsField.GetValue(w) : null;
                if (s != null)
                    sb.AppendLine($"  settings: sliceStep={s.GetType().GetField("sliceStep").GetValue(s)} " +
                                  $"minWorldSize={s.GetType().GetField("minWorldSize").GetValue(s)} " +
                                  $"include='{s.GetType().GetField("includeTokens").GetValue(s)}'");

                // Сохранить состояние в EditorPrefs и прочитать обратно (persist хоткеев)
                t.GetMethod("SavePersisted", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(w, null);
                var p = t.GetMethod("LoadPersisted", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
                bool persisted = p != null && p.GetType().GetField("settings").GetValue(p) != null;
                sb.AppendLine($"  EditorPrefs round-trip: {persisted}; ключ='{EditorPrefs.GetString("ProjectC.SettlementColliders.Window", "").Length} символов'");

                w.Repaint();
            }
        }
        catch (Exception e)
        {
            sb.AppendLine("[PROBE-WIN] ИСКЛЮЧЕНИЕ: " + e);
        }
        Debug.Log(sb.ToString());
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
}
