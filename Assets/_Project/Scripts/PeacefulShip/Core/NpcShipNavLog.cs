// T-NS-LOG01: глобальный Nav-лог NPC-кораблей в CSV-файл сессии (server-only).
// Зачем: по консоли не посчитать частоту переходов (дребезг WallFollow↔Cruising,
// наложение Avoiding-ship и Avoiding-build). Пользователь пишет сессию, отдаёт
// файл — по нему видно что/как часто тупит.
// Формат: t;event;npc;ship;from;to;reason;detail (разделитель ';' — Excel RU).
// Путь файла печатается в консоль при старте ( persistentDataPath ).
// Overhead: строки только на переходах + heartbeat 1/5с на корабль + сводка 1/60с.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace ProjectC.PeacefulShip.Core
{
    public static class NpcShipNavLog
    {
        private static StreamWriter _writer;
        private static string _path;
        private static float _startTime;
        private static bool _failed;

        // transition key "From→To" → count (для SUMMARY: что как часто дёргается)
        private static readonly Dictionary<string, int> _transitions = new Dictionary<string, int>();
        private static int _heartbeats;

        public static string Path => _path;
        public static bool Active => _writer != null;

        /// <summary>Ленивый старт: первый вызов из server-only кода.</summary>
        public static void Begin()
        {
            if (_writer != null || _failed) return;
            try
            {
                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                _path = System.IO.Path.Combine(Application.persistentDataPath, $"NpcShipNavLog_{stamp}.csv");
                _writer = new StreamWriter(_path, false, Encoding.UTF8) { AutoFlush = true };
                _startTime = Time.time;
                _writer.WriteLine("t;event;npc;ship;from;to;reason;detail");
                Debug.Log($"[NpcShipNavLog] T-NS-LOG01 logging to {_path}");
            }
            catch (Exception e)
            {
                _failed = true;
                Debug.LogWarning($"[NpcShipNavLog] T-NS-LOG01 cannot open log file: {e.Message}");
            }
        }

        public static void End()
        {
            try
            {
                if (_writer != null)
                {
                    WriteSummary();
                    _writer.Dispose();
                }
            }
            catch (Exception) { }
            finally
            {
                _writer = null;
            }
        }

        private static string F(float v) => v.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        private static string V(Vector3 v) =>
            $"{F(v.x)},{F(v.y)},{F(v.z)}";

        /// <summary>Переход режима. reason — коротко: LOS/timeout/stuck/loop/probe/gate/avoid/pad/leg/fo…</summary>
        public static void Transition(string shipName, ulong npcId, string from, string to, string reason, string detail = "")
        {
            Begin();
            if (_writer == null) return;
            try
            {
                string key = $"{from}→{to}";
                _transitions.TryGetValue(key, out int n);
                _transitions[key] = n + 1;
                _writer.WriteLine($"{F(Time.time - _startTime)};TRANS;{npcId:X};{shipName};{from};{to};{reason};{detail}");
            }
            catch (Exception) { }
        }

        /// <summary>Снапшот корабля раз в N секунд: видно «стоит носом» (spd≈0 вне Docked).</summary>
        public static void Heartbeat(string shipName, ulong npcId, string mode, float speed,
            float distToTarget, Vector3 pos, string detail = "")
        {
            Begin();
            if (_writer == null) return;
            try
            {
                _heartbeats++;
                _writer.WriteLine($"{F(Time.time - _startTime)};BEAT;{npcId:X};{shipName};{mode};;;spd={F(speed)};dist={F(distToTarget)};pos={V(pos)};{detail}");
            }
            catch (Exception) { }
        }

        /// <summary>Сводка: счётчики переходов + частота в минуту. Зовёт NpcShipWorld раз в 60с.</summary>
        public static void WriteSummary()
        {
            Begin();
            if (_writer == null) return;
            try
            {
                float mins = Mathf.Max(0.01f, (Time.time - _startTime) / 60f);
                var sb = new StringBuilder();
                sb.Append($"uptime_min={F(mins)};beats={_heartbeats}");
                foreach (var kv in _transitions)
                    sb.Append($";[{kv.Key}×{kv.Value}={F(kv.Value / mins)}/min]");
                _writer.WriteLine($"{F(Time.time - _startTime)};SUMMARY;;;;;;{sb}");
                Debug.Log($"[NpcShipNavLog] T-NS-LOG01 summary: {sb}");
            }
            catch (Exception) { }
        }
    }
}
