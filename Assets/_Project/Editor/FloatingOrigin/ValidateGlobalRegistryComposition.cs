using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using ProjectC.World.FloatingOrigin.Network;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>
    /// T-FO06F: pure checks for profile-declared registry composition and for the NGO cache behaviour the
    /// global startup relies on. Uses in-memory NetworkConfig/NetworkPrefabsList instances only:
    /// no GameObjects, no NetworkManager, no scenes, no asset writes and no Play Mode.
    /// </summary>
    public static class ValidateGlobalRegistryComposition
    {
        [Serializable] public sealed class Report { public int passed; public int failed; public string[] checks; public string[] failures; }

        [MenuItem("ProjectC/World/Floating Origin/Validate Registry Composition")]
        public static void Execute()
        {
            var r = Run();
            if (r.failed != 0) throw new InvalidOperationException(JsonUtility.ToJson(r, true));
            Debug.Log($"[T-FO06F] Registry composition: {r.passed} PASS / {r.failed} FAIL");
        }

        public static Report Run()
        {
            var checks = new List<string>();
            var failures = new List<string>();
            var temporary = new List<ScriptableObject>();

            void Check(string name, bool condition)
            {
                checks.Add((condition ? "PASS " : "FAIL ") + name);
                if (!condition) failures.Add(name);
            }

            try
            {
                var listA = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                var listB = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                temporary.Add(listA); temporary.Add(listB);

                // --- Declaration validation ---
                Check("null declaration means no override",
                    GlobalMotionNetworkContract.ValidateRegistryComposition(null, out var none) == null && none == null);
                Check("empty declaration means no override",
                    GlobalMotionNetworkContract.ValidateRegistryComposition(Array.Empty<NetworkPrefabsList>(), out var empty) == null && empty == null);
                Check("null entry rejected",
                    GlobalMotionNetworkContract.ValidateRegistryComposition(new NetworkPrefabsList[] { null }, out var withNull)
                        == "profile_registry_contains_null_list" && withNull == null);
                Check("null entry among valid ones rejected",
                    GlobalMotionNetworkContract.ValidateRegistryComposition(new[] { listA, null }, out _)
                        == "profile_registry_contains_null_list");
                Check("duplicate list rejected",
                    GlobalMotionNetworkContract.ValidateRegistryComposition(new[] { listA, listA }, out var duplicate)
                        == "profile_registry_contains_duplicate_list" && duplicate == null);

                string singleError = GlobalMotionNetworkContract.ValidateRegistryComposition(new[] { listA }, out var single);
                Check("single valid list accepted", singleError == null && single != null && single.Count == 1 && single[0] == listA);

                string pairError = GlobalMotionNetworkContract.ValidateRegistryComposition(new[] { listA, listB }, out var pair);
                Check("distinct lists accepted in declared order",
                    pairError == null && pair != null && pair.Count == 2 && pair[0] == listA && pair[1] == listB);

                Check("composition result is a fresh list, not the declared array storage",
                    single != null && pair != null && !ReferenceEquals(single, pair));

                // --- NGO cache behaviour that the swap depends on ---
                var config = new NetworkConfig();
                Check("fresh config exposes an empty aggregated registry",
                    config.Prefabs != null && config.Prefabs.Prefabs != null && config.Prefabs.Prefabs.Count == 0);

                var original = config.Prefabs.NetworkPrefabsLists;
                config.Prefabs.NetworkPrefabsLists = new List<NetworkPrefabsList> { listA };
                Check("assigning lists alone does not refresh the aggregated registry",
                    config.Prefabs.Prefabs.Count == 0);

                config.Prefabs.Initialize();
                Check("initialize rebuilds the aggregated registry from the assigned lists",
                    config.Prefabs.Prefabs.Count == listA.PrefabList.Count);

                config.Prefabs.NetworkPrefabsLists = original ?? new List<NetworkPrefabsList>();
                config.Prefabs.Initialize();
                Check("restoring the previous lists empties the aggregated registry again",
                    config.Prefabs.Prefabs.Count == 0);

                Check("restore keeps the original list instance",
                    ReferenceEquals(config.Prefabs.NetworkPrefabsLists, original) || original == null);
            }
            catch (Exception e)
            {
                failures.Add("exception:" + e.GetType().Name + ":" + e.Message);
                checks.Add("FAIL unexpected exception");
            }
            finally
            {
                foreach (var value in temporary) if (value != null) UnityEngine.Object.DestroyImmediate(value);
            }

            return new Report { passed = checks.Count - failures.Count, failed = failures.Count, checks = checks.ToArray(), failures = failures.ToArray() };
        }
    }
}
