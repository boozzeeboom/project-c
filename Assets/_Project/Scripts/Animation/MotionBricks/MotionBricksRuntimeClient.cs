#if UNITY_EDITOR
// Project C: T-MB06 — localhost-клиент экспериментального runtime MotionBricks.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace ProjectC.Animation.MotionBricks
{
    internal sealed class MotionBricksRuntimeFrame
    {
        public uint Sequence;
        public int PlaybackSource;
        public int GenerationCount;
        public float InferenceWallMs;
        public float EncodeMs;
        public float InferenceMs;
        public float DecodeMs;
        public float PostProcessMs;
        public float ServerStepMs;
        public readonly float[] Pose = new float[77 * 7];
        public readonly float[] Contacts = new float[4];
    }

    internal sealed class MotionBricksRuntimeClient : IDisposable
    {
        private const string Magic = "MBR1";
        private const byte Version = 1;
        private const byte PacketPose = 1;
        private const byte PacketBind = 2;
        private const byte PacketError = 3;
        private const int ResponseHeaderBytes = 48;
        private const int PoseBytes = 77 * 7 * 4;
        private const int ContactBytes = 4 * 4;

        private readonly object _sync = new object();
        private readonly object _pendingSync = new object();
        private readonly Dictionary<uint, long> _pending = new Dictionary<uint, long>();
        private readonly List<float> _roundTrips = new List<float>();
        private readonly List<float> _inferenceTimes = new List<float>();
        private readonly ConcurrentQueue<string> _standardOutput = new ConcurrentQueue<string>();
        private readonly ConcurrentQueue<string> _standardError = new ConcurrentQueue<string>();
        private readonly string _pythonPath;
        private readonly string _repositoryPath;
        private readonly string _scriptPath;
        private readonly int _port;
        private readonly bool _fp16;

        private Process _process;
        private Socket _socket;
        private Thread _receiveThread;
        private volatile bool _running;
        private MotionBricksRuntimeFrame _latest;
        private float[] _bindPose;
        private uint _sequence;
        private int _lastGeneration = -1;
        private long _startedTimestamp;
        private long _lastReceiveTimestamp;
        private long _sentCount;
        private long _receivedCount;
        private long _malformedCount;
        private float _lastRoundTripMs;
        private string _lastError;

        public MotionBricksRuntimeClient(string pythonPath, string repositoryPath, string scriptPath, int port, bool fp16)
        {
            _pythonPath = pythonPath;
            _repositoryPath = repositoryPath;
            _scriptPath = scriptPath;
            _port = port;
            _fp16 = fp16;
        }

        public string LastError => _lastError;
        public long MalformedCount => Interlocked.Read(ref _malformedCount);
        public float LastRoundTripMs => _lastRoundTripMs;
        public int GenerationCount { get; private set; }
        public int ProcessId => _process != null && !HasProcessExited ? _process.Id : 0;

        public bool HasProcessExited
        {
            get
            {
                try { return _process == null || _process.HasExited; }
                catch (InvalidOperationException) { return true; }
            }
        }

        public float LastFrameAgeMs
        {
            get
            {
                long stamp = Interlocked.Read(ref _lastReceiveTimestamp);
                return stamp == 0 ? float.PositiveInfinity : (Stopwatch.GetTimestamp() - stamp) * 1000f / Stopwatch.Frequency;
            }
        }

        public void Start()
        {
            if (_running) return;
            foreach (string path in new[] { _pythonPath, _repositoryPath, _scriptPath })
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) && !Directory.Exists(path))
                    throw new FileNotFoundException("MotionBricks runtime path not found", path);
            }

            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            _socket.ReceiveTimeout = 100;

            var start = new ProcessStartInfo
            {
                FileName = _pythonPath,
                Arguments = Quote(_scriptPath) + " --repo " + Quote(_repositoryPath) +
                            " --host 127.0.0.1 --port " + _port + (_fp16 ? " --fp16" : string.Empty),
                WorkingDirectory = _repositoryPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            start.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";

            _process = new Process { StartInfo = start, EnableRaisingEvents = true };
            _process.OutputDataReceived += (_, args) => { if (args.Data != null) _standardOutput.Enqueue(args.Data); };
            _process.ErrorDataReceived += (_, args) => { if (args.Data != null) _standardError.Enqueue(args.Data); };
            if (!_process.Start()) throw new InvalidOperationException("MotionBricks sidecar process did not start");
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            _running = true;
            _startedTimestamp = Stopwatch.GetTimestamp();
            _receiveThread = new Thread(ReceiveLoop) { IsBackground = true, Name = "MotionBricks UDP receive" };
            _receiveThread.Start();
        }

        public void Send(float deltaTime, float walkSpeed, float runSpeed, Vector3 position, Quaternion rotation,
                         Vector3 velocity, bool reset)
        {
            if (!_running || _socket == null) return;
            uint sequence = ++_sequence;
            byte flags = reset ? (byte)1 : (byte)0;
            byte[] packet;
            using (var stream = new MemoryStream(64))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Encoding.ASCII.GetBytes(Magic));
                writer.Write(Version);
                writer.Write(flags);
                writer.Write((ushort)0);
                writer.Write(sequence);
                writer.Write(deltaTime);
                writer.Write(walkSpeed);
                writer.Write(runSpeed);
                writer.Write(position.x); writer.Write(position.y); writer.Write(position.z);
                writer.Write(rotation.x); writer.Write(rotation.y); writer.Write(rotation.z); writer.Write(rotation.w);
                writer.Write(velocity.x); writer.Write(velocity.y); writer.Write(velocity.z);
                packet = stream.ToArray();
            }

            long now = Stopwatch.GetTimestamp();
            lock (_pendingSync)
            {
                _pending[sequence] = now;
                if (_pending.Count > 512)
                {
                    uint oldest = _sequence - 512;
                    _pending.Remove(oldest);
                }
            }
            try
            {
                _socket.SendTo(packet, new IPEndPoint(IPAddress.Loopback, _port));
                Interlocked.Increment(ref _sentCount);
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset ||
                                              ex.SocketErrorCode == SocketError.WouldBlock) { }
            catch (SocketException ex) { _lastError = "send: " + ex.Message; }
        }

        public bool TryConsumeLatest(out MotionBricksRuntimeFrame frame)
        {
            lock (_sync)
            {
                frame = _latest;
                _latest = null;
                return frame != null;
            }
        }

        public bool TryGetBindPose(out float[] bindPose)
        {
            lock (_sync)
            {
                bindPose = _bindPose;
                return bindPose != null;
            }
        }

        public void DrainLogs(Action<string> output, Action<string> error)
        {
            while (_standardOutput.TryDequeue(out string line)) output(line);
            while (_standardError.TryDequeue(out string line)) error(line);
        }

        public string Diagnostics()
        {
            double seconds = Math.Max(0.001, (Stopwatch.GetTimestamp() - _startedTimestamp) / (double)Stopwatch.Frequency);
            long sent = Interlocked.Read(ref _sentCount);
            long received = Interlocked.Read(ref _receivedCount);
            float workingSet = 0f;
            if (!HasProcessExited) workingSet = _process.WorkingSet64 / (1024f * 1024f);
            lock (_pendingSync)
            {
                return $"pid={ProcessId}, sent={sent} ({sent / seconds:F1}/s), received={received} ({received / seconds:F1}/s), " +
                       $"age={LastFrameAgeMs:F1} ms, RTT last/p50/p95={_lastRoundTripMs:F1}/{Percentile(_roundTrips, 0.5f):F1}/{Percentile(_roundTrips, 0.95f):F1} ms, " +
                       $"generations={GenerationCount}, inference last/p50/p95/max={(_inferenceTimes.Count > 0 ? _inferenceTimes[_inferenceTimes.Count - 1] : 0f):F1}/" +
                       $"{Percentile(_inferenceTimes, 0.5f):F1}/{Percentile(_inferenceTimes, 0.95f):F1}/{(_inferenceTimes.Count > 0 ? _inferenceTimes.Max() : 0f):F1} ms, " +
                       $"malformed={MalformedCount}, sidecar RAM={workingSet:F0} MiB, error={LastError ?? "none"}";
            }
        }

        private void ReceiveLoop()
        {
            var endpoint = new IPEndPoint(IPAddress.Any, 0);
            EndPoint remote = endpoint;
            byte[] buffer = new byte[65535];
            while (_running)
            {
                try
                {
                    int count = _socket.ReceiveFrom(buffer, ref remote);
                    if (count >= ResponseHeaderBytes) ParsePacket(buffer, count);
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut) { }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.WouldBlock) { }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset) { }
                catch (ObjectDisposedException) { break; }
                catch (InvalidOperationException) { if (_running) continue; }
                catch (Exception ex)
                {
                    if (!_running) break;
                    _lastError = "receive: " + ex.Message;
                    Interlocked.Increment(ref _malformedCount);
                }
            }
        }

        private void ParsePacket(byte[] buffer, int count)
        {
            using (var stream = new MemoryStream(buffer, 0, count, false))
            using (var reader = new BinaryReader(stream))
            {
                string magic = Encoding.ASCII.GetString(reader.ReadBytes(4));
                byte version = reader.ReadByte();
                byte type = reader.ReadByte();
                reader.ReadUInt16();
                uint sequence = reader.ReadUInt32();
                int source = reader.ReadInt32();
                int generation = reader.ReadInt32();
                reader.ReadUInt32();
                float wall = reader.ReadSingle();
                float encode = reader.ReadSingle();
                float inference = reader.ReadSingle();
                float decode = reader.ReadSingle();
                float post = reader.ReadSingle();
                float step = reader.ReadSingle();
                if (magic != Magic || version != Version) throw new InvalidDataException("response magic/version mismatch");

                if (type == PacketError)
                {
                    _lastError = Encoding.UTF8.GetString(reader.ReadBytes((int)(stream.Length - stream.Position)));
                    return;
                }
                if (type == PacketBind)
                {
                    if (count != ResponseHeaderBytes + PoseBytes) throw new InvalidDataException("bind packet size mismatch");
                    float[] bind = ReadFloats(reader, 77 * 7);
                    lock (_sync) _bindPose = bind;
                    return;
                }
                if (type != PacketPose || count != ResponseHeaderBytes + PoseBytes + ContactBytes)
                    throw new InvalidDataException("pose packet size/type mismatch");

                var frame = new MotionBricksRuntimeFrame
                {
                    Sequence = sequence,
                    PlaybackSource = source,
                    GenerationCount = generation,
                    InferenceWallMs = wall,
                    EncodeMs = encode,
                    InferenceMs = inference,
                    DecodeMs = decode,
                    PostProcessMs = post,
                    ServerStepMs = step,
                };
                float[] pose = ReadFloats(reader, 77 * 7);
                float[] contacts = ReadFloats(reader, 4);
                Array.Copy(pose, frame.Pose, pose.Length);
                Array.Copy(contacts, frame.Contacts, contacts.Length);
                long now = Stopwatch.GetTimestamp();
                lock (_pendingSync)
                {
                    if (_pending.TryGetValue(sequence, out long sent))
                    {
                        _lastRoundTripMs = (now - sent) * 1000f / Stopwatch.Frequency;
                        _roundTrips.Add(_lastRoundTripMs);
                    }
                    if (generation != _lastGeneration)
                    {
                        _lastGeneration = generation;
                        GenerationCount = generation;
                        if (wall > 0f) _inferenceTimes.Add(wall);
                    }
                }
                lock (_sync) _latest = frame;
                Interlocked.Exchange(ref _lastReceiveTimestamp, now);
                Interlocked.Increment(ref _receivedCount);
            }
        }

        private static float[] ReadFloats(BinaryReader reader, int count)
        {
            var values = new float[count];
            for (int i = 0; i < count; i++) values[i] = reader.ReadSingle();
            return values;
        }

        private static float Percentile(List<float> values, float fraction)
        {
            if (values.Count == 0) return 0f;
            float[] sorted = values.ToArray();
            Array.Sort(sorted);
            int index = Mathf.Clamp(Mathf.RoundToInt((sorted.Length - 1) * fraction), 0, sorted.Length - 1);
            return sorted[index];
        }

        private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

        public void Dispose()
        {
            if (!_running && _process == null) return;
            try
            {
                if (_socket != null && !HasProcessExited)
                {
                    uint sequence = ++_sequence;
                    using (var stream = new MemoryStream(64))
                    using (var writer = new BinaryWriter(stream))
                    {
                        writer.Write(Encoding.ASCII.GetBytes(Magic)); writer.Write(Version); writer.Write((byte)2);
                        writer.Write((ushort)0); writer.Write(sequence);
                        for (int i = 0; i < 13; i++) writer.Write(0f);
                        _socket.SendTo(stream.ToArray(), new IPEndPoint(IPAddress.Loopback, _port));
                    }
                }
            }
            catch (Exception) { }
            _running = false;
            try { _socket?.Close(); } catch (Exception) { }
            try { if (_receiveThread != null && !_receiveThread.Join(300)) _receiveThread = null; } catch (Exception) { }
            try
            {
                if (_process != null && !_process.HasExited)
                {
                    if (!_process.WaitForExit(1000)) _process.Kill();
                }
                _process?.Dispose();
            }
            catch (Exception) { }
            _process = null;
            _socket = null;
        }
    }
}
#endif
