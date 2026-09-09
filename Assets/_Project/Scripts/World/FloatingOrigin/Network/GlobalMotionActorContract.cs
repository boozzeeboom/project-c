using System;
using UnityEngine;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// Root actor hooks. Readiness must NOT recursively call adapter.IsBaselineReady/IsReadyForSimulation.
    /// Native physics/nav preparation is explicit; these hooks never change docking/death/ownership to pause an actor.
    /// </summary>
    public interface IGlobalMotionActorParticipant
    {
        bool CanApplyGlobalBaseline(MotionPoseRole role);
        void OnGlobalBaselineApplied(MotionStreamBinding binding);
        bool IsGlobalMotionReady(MotionPoseRole role);
    }

    /// <summary>Pure, fail-closed mode latch and exact-binding native readiness token.</summary>
    public sealed class GlobalMotionActorState
    {
        public bool CoordinatesRequired { get; private set; }
        public MotionStreamBinding AppliedBinding { get; private set; }
        private MotionStreamBinding _nativePrepared;
        public void ObserveRequirement(bool required) { if (required) CoordinatesRequired = true; }
        public bool AllowsSimulation(bool adapterPresent, bool adapterReady) =>
            !CoordinatesRequired || (adapterPresent && adapterReady);

        /// <returns>True only for the first baseline of a new session/object/spawn lifetime.</returns>
        public bool RecordBaseline(MotionStreamBinding binding)
        {
            if (!binding.IsValid) throw new ArgumentException("A valid applied baseline is required.", nameof(binding));
            if (AppliedBinding == binding) return false;
            bool first = !AppliedBinding.IsValid || AppliedBinding.SessionId != binding.SessionId ||
                AppliedBinding.NetworkObjectId != binding.NetworkObjectId || AppliedBinding.SpawnGeneration != binding.SpawnGeneration;
            CoordinatesRequired = true; AppliedBinding = binding; _nativePrepared = default;
            return first;
        }
        public bool ConfirmNativePrepared(MotionStreamBinding binding)
        {
            if (!CoordinatesRequired || !binding.IsValid || binding != AppliedBinding) return false;
            _nativePrepared = binding; return true;
        }
        public bool NativePreparedFor(MotionStreamBinding binding) => binding.IsValid &&
            binding == AppliedBinding && binding == _nativePrepared;
        public void ResetLifetime() { AppliedBinding = default; _nativePrepared = default; }
    }

    /// <summary>One cached lookup per actor. Missing/disabled/destroyed required adapters never fall back to legacy movement.</summary>
    public sealed class GlobalMotionActorLink
    {
        public GlobalMotionPoseAdapter Adapter { get; }
        public GlobalMotionActorState State { get; } = new GlobalMotionActorState();
        public GlobalMotionActorLink(Component actor)
        {
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            Adapter = actor.GetComponent<GlobalMotionPoseAdapter>();
            State.ObserveRequirement(Adapter != null && Adapter.CoordinatesRequired);
        }
        public bool Required
        {
            get { State.ObserveRequirement(Adapter != null && Adapter.CoordinatesRequired); return State.CoordinatesRequired; }
        }
        public bool CanSimulate => !Required || State.AllowsSimulation(Adapter != null,
            Adapter != null && Adapter.IsReadyForSimulation);
        public bool CanObserve => !Required || State.AllowsSimulation(Adapter != null,
            Adapter != null && Adapter.IsBaselineReady);
        public bool ConfirmNativePrepared(MotionStreamBinding binding) => Required && Adapter != null &&
            Adapter.IsBaselinePlaced && Adapter.Transport.Control.Baseline.Binding == binding && State.ConfirmNativePrepared(binding);
    }
}
