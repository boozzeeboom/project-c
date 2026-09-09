using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using ProjectC.Core.ShipPosition;
using ProjectC.World.FloatingOrigin;
using ProjectC.World.FloatingOrigin.Persistence;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>In-memory DTO/JSON/math checks only. No repository IO, scene objects, networking or actual save data.</summary>
    public static class ValidateGlobalPlayerPersistence
    {
        [Serializable] public sealed class Report { public int passed; public int failed; public string[] checks; public string[] failures; }
        [MenuItem("ProjectC/World/Floating Origin/Validate Global Player Persistence")]
        public static void Execute() { var r = Run(); if (r.failed != 0) throw new InvalidOperationException(JsonUtility.ToJson(r, true)); Debug.Log($"[T-FO05A] {r.passed} in-memory checks PASS. Runtime persistence UNTESTED."); }
        public static Report Run()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stable Edit Mode required.");
            var good = new List<string>(); var bad = new List<string>();
            void Check(string name, Action action) { try { action(); good.Add(name); } catch (Exception e) { bad.Add(name + ": " + e.GetType().Name + ": " + e.Message); } }
            Check("Frozen v1 JSON and checksum remain readable and byte-for-byte encodable", () =>
            {
                const string golden = "{\"format\":\"projectc.global-player-position\",\"schemaVersion\":1,\"coordinateSpace\":\"global-double\",\"playerId\":\"player-one\",\"x\":\"1.25\",\"y\":\"2.5\",\"z\":\"-3.75\",\"inShip\":false,\"shipPersistentId\":\"\",\"savedAtUnix\":123,\"checksum\":\"f2b618611c7438f5efbb84e08fd3db8a98d94e7d56aea358e77e2550a6c78479\"}";
                Require(Encode(Record()) == golden && Decode(golden).Position == Record().Position);
            });
            Check("Frozen current-legacy layout still imports without regenerating its DTO", () =>
            {
                const string golden = "{\"ships\":[],\"players\":[{\"clientId\":0,\"px\":1.25,\"py\":2.5,\"pz\":-3.75,\"inShip\":false,\"shipPersistentId\":\"\",\"savedAtUnix\":123}]}";
                Require(ImportJson(golden, new[] { Bind() }).Players[0].Position == new GlobalPosition(1.25, 2.5, -3.75));
            });
            Check("Global point beyond 56km preserves sub-float precision", () => RoundTrip(new GlobalPosition(56500.000000123, 3000.000000001, -39999.50000001)));
            Check("Large finite and tiny double coordinates round trip", () => RoundTrip(new GlobalPosition(double.MaxValue, double.Epsilon, -1e200)));
            Check("Explicit global zero is valid", () => RoundTrip(GlobalPosition.Zero));
            Check("Negative zero has one canonical point representation", () => { var p = Record(new GlobalPosition(BitConverter.Int64BitsToDouble(long.MinValue), 0, 0)); Require(BitConverter.DoubleToInt64Bits(p.Position.X) == 0); Decode(Encode(p)); });
            Check("Deterministic checkpoint encoding", () => Require(Encode(Record()) == Encode(Record())));
            Check("100 generated double points preserve values", () => { var random = new System.Random(1705); for (int i = 0; i < 100; i++) RoundTrip(new GlobalPosition((random.NextDouble() - 0.5) * 1e12, random.NextDouble() / 10000d, random.NextDouble() * -1e8)); });
            Check("No frame origin or NGO client identity in v1 payload", () => { string j = Encode(Record()); Require(!j.Contains("frameId") && !j.Contains("origin") && !j.Contains("clientId") && j.Contains("global-double")); });
            Check("Independent frames reconstruct the same persisted global point", () => { var p = Decode(Encode(Record(new GlobalPosition(1000000000.125, 12, 4)))); var a = new LocalCoordinateFrame(new GlobalPosition(1000000000, 0, 0)); var b = new LocalCoordinateFrame(new GlobalPosition(1000000100, 0, 0)); Require(a.ToGlobal(a.ToLocal(p.Position)) == p.Position && b.ToGlobal(b.ToLocal(p.Position)) == p.Position); });
            Check("Locale does not change checkpoint encoding", () => { string expected = Encode(Record()); var old = CultureInfo.CurrentCulture; try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU"); Require(Encode(Record()) == expected); Decode(expected); } finally { CultureInfo.CurrentCulture = old; } });
            Check("Player ship affinity preserved without resolving or moving ship", () => { var p = Decode(Encode(new GlobalPlayerPositionRecord("account/player", new GlobalPosition(1, 2, 3), true, "ship-guid-1", 1))); Require(p.InShip && p.ShipPersistentId == "ship-guid-1" && p.Position == new GlobalPosition(1, 2, 3)); });
            Check("Metadata timestamps preserve epoch and upper boundary", () => { foreach (long time in new[] { 0L, GlobalPlayerPositionRecord.MaxSavedAtUnix }) Require(Decode(Encode(new GlobalPlayerPositionRecord("id", GlobalPosition.Zero, false, "", time))).SavedAtUnix == time); });
            Check("Missing checkpoint rejected", () => Bad(null));
            Check("Empty checkpoint rejected", () => Bad(""));
            Check("Missing JSON fields cannot become zero checkpoint", () => Bad("{}"));
            Check("Unknown checkpoint version rejected without legacy fallback", () => Bad(Encode(Record()).Replace("\"schemaVersion\":1", "\"schemaVersion\":2")));
            Check("Unknown format rejected", () => Bad(Encode(Record()).Replace(GlobalPlayerPositionCodec.Format, "other-format")));
            Check("Parent-local coordinate space rejected", () => Bad(Encode(Record()).Replace("global-double", "parent-local")));
            Check("Tampered coordinate invalidates checksum", () => Bad(Encode(Record()).Replace("\"x\":\"1.25\"", "\"x\":\"1.5\"")));
            Check("Tampered persistent identity invalidates checksum", () => Bad(Encode(Record()).Replace("player-one", "player-two")));
            Check("Duplicate identical required field rejected", () => { string j = Encode(Record()); Bad(j.Insert(1, "\"x\":\"1.25\",")); });
            Check("Unknown JSON field is not silently ignored", () => Bad(Encode(Record()).Insert(1, "\"extra\":true,")));
            Check("Missing coordinate field is not defaulted", () => Bad(Remove(Encode(Record()), "x")));
            Check("Missing boolean field is not defaulted", () => Bad(Remove(Encode(Record()), "inShip")));
            Check("Truncated checksum payload rejected", () => { string j = Encode(Record()); Bad(j.Substring(0, j.Length - 5)); });
            Check("Oversized payload rejected before native parsing", () => Bad(new string(' ', GlobalPlayerPositionCodec.MaxJsonCharacters + 1)));
            Check("Deeply nested checkpoint rejected before native parsing", () => Bad("{\"x\":[[[[[[[[[[0]]]]]]]]]]}"));
            Check("Trailing object rejected", () => Bad(Encode(Record()) + "{}"));
            Check("Array root rejected", () => Bad("[" + Encode(Record()) + "]"));
            Check("Nonfinite coordinate strings rejected", () => { foreach (string n in new[] { "NaN", "Infinity", "-Infinity", "1e9999" }) Bad(Encode(Record()).Replace("\"x\":\"1.25\"", "\"x\":\"" + n + "\"")); });
            Check("Comma decimal and noncanonical number spelling rejected", () => { foreach (string n in new[] { "1,25", " 1.25", "+1.25", "1.250" }) Bad(Encode(Record()).Replace("\"x\":\"1.25\"", "\"x\":\"" + n + "\"")); });
            Check("Only outer whitespace is tolerated", () => { Decode(" \r\n" + Encode(Record()) + "\t "); Bad(Encode(Record()).Replace(",", ", ")); });
            Check("Control characters in JSON strings rejected", () => Bad(Encode(Record()).Replace("player-one", "player\none")));
            Check("Null record is not encoded", () => Require(!GlobalPlayerPositionCodec.TryEncode(null, out var json, out var error) && json == null && error != null));
            Check("Persistent identity is explicit, bounded, case-sensitive", () => { foreach (string id in new[] { "", " padded", "space id", "line\nid", new string('a', 129) }) Throws(() => new GlobalPlayerPositionRecord(id, GlobalPosition.Zero, false, "", 1)); Require(Encode(new GlobalPlayerPositionRecord("A", GlobalPosition.Zero, false, "", 1)) != Encode(new GlobalPlayerPositionRecord("a", GlobalPosition.Zero, false, "", 1))); });
            Check("Invalid mutated GlobalPosition rejected by record constructor", () => { var p = GlobalPosition.Zero; p.X = double.NaN; Throws(() => Record(p)); });
            Check("Ship flag and identity must agree", () => { Throws(() => new GlobalPlayerPositionRecord("id", GlobalPosition.Zero, true, "", 1)); Throws(() => new GlobalPlayerPositionRecord("id", GlobalPosition.Zero, false, "stale-ship", 1)); Throws(() => new GlobalPlayerPositionRecord("id", GlobalPosition.Zero, false, null, 1)); });
            Check("Invalid timestamp rejected", () => { Throws(() => new GlobalPlayerPositionRecord("id", GlobalPosition.Zero, false, "", -1)); Throws(() => new GlobalPlayerPositionRecord("id", GlobalPosition.Zero, false, "", GlobalPlayerPositionRecord.MaxSavedAtUnix + 1)); });
            Check("Legacy absolute import preserves stored float, not imaginary lost precision", () => { var w = Legacy(); w.players[0].px = 56500.123456789f; var d = Import(w, Bind()); Require(d.Players[0].Position.X == (double)w.players[0].px && d.Players[0].PlayerId == "account-one"); });
            Check("Known-local import adds only the explicitly supplied global origin", () => { var frame = new LocalCoordinateFrame(new GlobalPosition(1e9, 200, -1e9)); var w = Legacy(); var d = Import(w, Bind(LegacyPlayerCoordinates.KnownLocalFrame, frame)); Require(d.Players[0].Position == frame.ToGlobal(new Vector3(w.players[0].px, w.players[0].py, w.players[0].pz))); });
            Check("Unreviewed legacy coordinate provenance rejected", () => BadImport(Legacy(), Bind(LegacyPlayerCoordinates.Unspecified)));
            Check("Known-local without origin rejected", () => BadImport(Legacy(), Bind(LegacyPlayerCoordinates.KnownLocalFrame)));
            Check("Default local frame rejected", () => BadImport(Legacy(), Bind(LegacyPlayerCoordinates.KnownLocalFrame, default(LocalCoordinateFrame))));
            Check("Absolute point plus frame is ambiguous and rejected", () => BadImport(Legacy(), Bind(LegacyPlayerCoordinates.AbsoluteWorldFloat, new LocalCoordinateFrame(GlobalPosition.Zero))));
            Check("Local point outside trusted frame budget rejected", () => { var w = Legacy(); w.players[0].px = 9000; BadImport(w, Bind(LegacyPlayerCoordinates.KnownLocalFrame, new LocalCoordinateFrame(GlobalPosition.Zero))); });
            Check("Entire identity binding set required", () => { string j = JsonUtility.ToJson(Legacy()); BadImportJson(j, Array.Empty<LegacyPlayerPositionBinding>()); });
            Check("No identity auto-mapping from NGO id", () => BadImport(Legacy(), new LegacyPlayerPositionBinding(0, 123, null, LegacyPlayerCoordinates.AbsoluteWorldFloat)));
            Check("NGO host id zero is valid only with explicit persistent identity mapping", () => Require(Import(Legacy(), Bind()).Players[0].PlayerId == "account-one"));
            Check("Wrong snapshot digest rejected", () => { string j = JsonUtility.ToJson(Legacy()); Require(!LegacyPlayerPositionImport.TryCreateDraft(j, new string('a', 64), new[] { Bind() }, out var d, out _) && d == null); });
            Check("Same NGO id with changed save timestamp rejected", () => BadImport(Legacy(), new LegacyPlayerPositionBinding(0, 124, "account-one", LegacyPlayerCoordinates.AbsoluteWorldFloat)));
            Check("Wrong client record mapping rejected", () => BadImport(Legacy(), new LegacyPlayerPositionBinding(1, 123, "account-one", LegacyPlayerCoordinates.AbsoluteWorldFloat)));
            Check("Duplicate legacy client records rejected", () => { var w = Legacy(); w.players.Add(w.players[0]); BadImportJson(JsonUtility.ToJson(w), new[] { Bind(), new LegacyPlayerPositionBinding(1, 123, "other", LegacyPlayerCoordinates.AbsoluteWorldFloat) }); });
            Check("Two network ids cannot map to one durable identity", () => { var w = Legacy(); w.players.Add(new PlayerPositionSaveData { clientId = 1, px = 2, shipPersistentId = "", savedAtUnix = 123 }); BadImportJson(JsonUtility.ToJson(w), new[] { Bind(), new LegacyPlayerPositionBinding(1, 123, "account-one", LegacyPlayerCoordinates.AbsoluteWorldFloat) }); });
            Check("Duplicate binding entries rejected", () => { var w = Legacy(); w.players.Add(new PlayerPositionSaveData { clientId = 1, shipPersistentId = "", savedAtUnix = 123 }); BadImportJson(JsonUtility.ToJson(w), new[] { Bind(), Bind() }); });
            Check("Legacy null player record rejected", () => { var w = Legacy(); w.players[0] = null; BadImport(w, Bind()); });
            Check("Legacy missing px rejected instead of default zero", () => BadImportJson(Remove(JsonUtility.ToJson(Legacy()), "px"), new[] { Bind() }));
            Check("Unknown or extended legacy fields not dropped", () => BadImportJson(JsonUtility.ToJson(Legacy()).Insert(1, "\"extra\":1,"), new[] { Bind() }));
            Check("Legacy duplicate field rejected", () => BadImportJson(JsonUtility.ToJson(Legacy()).Replace("\"clientId\":0,", "\"clientId\":0,\"clientId\":0,"), new[] { Bind() }));
            Check("Legacy pretty or reordered layout requires a separately reviewed reader", () => BadImportJson(JsonUtility.ToJson(Legacy(), true), new[] { Bind() }));
            Check("Modern record is not mistaken for unified legacy wrapper", () => BadImportJson(Encode(Record()), Array.Empty<LegacyPlayerPositionBinding>()));
            Check("Legacy wrapper is not silently accepted as modern checkpoint", () => Bad(JsonUtility.ToJson(Legacy())));
            Check("Unconverted ships and exact original input text retained", () => { var w = Legacy(); w.ships.Add(new ShipPositionSaveData { shipId = "unchanged", pxCruise = 123.25f, liftStartY = 456.5f, navMode = 99 }); string json = "\n" + JsonUtility.ToJson(w) + "\r\n"; var d = ImportJson(json, new[] { Bind() }); Require(d.OriginalJson == json && d.UnconvertedShipCount == 1 && JsonUtility.ToJson(w) == json.Trim()); });
            Check("One invalid player rejects entire import with no partial result", () => { var w = Legacy(); w.players.Add(new PlayerPositionSaveData { clientId = 1, inShip = true, shipPersistentId = "", savedAtUnix = 123 }); BadImportJson(JsonUtility.ToJson(w), new[] { Bind(), new LegacyPlayerPositionBinding(1, 123, "account-two", LegacyPlayerCoordinates.AbsoluteWorldFloat) }); });
            Check("Draft records cannot be mutated through collection", () => { var d = Import(Legacy(), Bind()); Throws(() => ((IList<GlobalPlayerPositionRecord>)d.Players).Clear()); });
            Check("Empty player list retains ships without fabricating player", () => { var w = Legacy(); w.players.Clear(); w.ships.Add(new ShipPositionSaveData()); var d = ImportJson(JsonUtility.ToJson(w), Array.Empty<LegacyPlayerPositionBinding>()); Require(d.Players.Count == 0 && d.UnconvertedShipCount == 1); });
            Check("Review digest changes with original text even if whitespace equivalent", () => { string j = JsonUtility.ToJson(Legacy()); Require(LegacyPlayerPositionImport.TryComputeSourceTextDigest(j, out string a, out _) && LegacyPlayerPositionImport.TryComputeSourceTextDigest(j + "\n", out string b, out _) && a != b); });
            Check("Legacy save DTO shape was not extended with frame or global fields", () => { var t = typeof(PlayerPositionSaveData); Require(t.GetFields(BindingFlags.Public | BindingFlags.Instance).Length == 7 && t.GetField("frameId") == null && t.GetField("globalPosition") == null); });
            Check("New record has no runtime NGO id frame or transform members", () => { var t = typeof(GlobalPlayerPositionRecord); Require(t.GetProperty("ClientId") == null && t.GetProperty("FrameId") == null && t.GetProperty("Transform") == null && !typeof(UnityEngine.Object).IsAssignableFrom(t)); });
            return new Report { passed = good.Count, failed = bad.Count, checks = good.ToArray(), failures = bad.ToArray() };
        }
        private static GlobalPlayerPositionRecord Record(GlobalPosition? position = null) => new GlobalPlayerPositionRecord("player-one", position ?? new GlobalPosition(1.25, 2.5, -3.75), false, "", 123);
        private static string Encode(GlobalPlayerPositionRecord record) { Require(GlobalPlayerPositionCodec.TryEncode(record, out string json, out var e), e); return json; }
        private static GlobalPlayerPositionRecord Decode(string json) { Require(GlobalPlayerPositionCodec.TryDecode(json, out var record, out var e), e); return record; }
        private static void RoundTrip(GlobalPosition p) { var decoded = Decode(Encode(Record(p))); Require(decoded.Position == p && decoded.PlayerId == "player-one"); }
        private static void Bad(string json) { Require(!GlobalPlayerPositionCodec.TryDecode(json, out var p, out var e) && p == null && !string.IsNullOrEmpty(e)); }
        private static ShipPositionListWrapper Legacy() => new ShipPositionListWrapper { players = new List<PlayerPositionSaveData> { new PlayerPositionSaveData { clientId = 0, px = 1.25f, py = 2.5f, pz = -3.75f, inShip = false, shipPersistentId = "", savedAtUnix = 123 } } };
        private static LegacyPlayerPositionBinding Bind(LegacyPlayerCoordinates mode = LegacyPlayerCoordinates.AbsoluteWorldFloat, LocalCoordinateFrame? frame = null) => new LegacyPlayerPositionBinding(0, 123, "account-one", mode, frame);
        private static LegacyPlayerPositionDraft Import(ShipPositionListWrapper wrapper, LegacyPlayerPositionBinding binding) => ImportJson(JsonUtility.ToJson(wrapper, false), new[] { binding });
        private static LegacyPlayerPositionDraft ImportJson(string json, IReadOnlyList<LegacyPlayerPositionBinding> bindings) { Require(LegacyPlayerPositionImport.TryComputeSourceTextDigest(json, out var digest, out var e), e); Require(LegacyPlayerPositionImport.TryCreateDraft(json, digest, bindings, out var draft, out e), e); return draft; }
        private static void BadImport(ShipPositionListWrapper w, LegacyPlayerPositionBinding binding) => BadImportJson(JsonUtility.ToJson(w, false), new[] { binding });
        private static void BadImportJson(string j, IReadOnlyList<LegacyPlayerPositionBinding> bindings) { Require(LegacyPlayerPositionImport.TryComputeSourceTextDigest(j, out var digest, out _)); Require(!LegacyPlayerPositionImport.TryCreateDraft(j, digest, bindings, out var d, out var e) && d == null && !string.IsNullOrEmpty(e)); }
        private static string Remove(string json, string field) { var regex = new Regex("\"" + field + "\":(?:\"[^\"]*\"|[^,}]+),"); string result = regex.Replace(json, "", 1); Require(result != json, "Test mutation did not change payload."); return result; }
        private static void Throws(Action action) { bool threw = false; try { action(); } catch (Exception) { threw = true; } Require(threw); }
        private static void Require(bool value, string error = null) { if (!value) throw new InvalidOperationException(error ?? "Assertion failed."); }
    }
}
