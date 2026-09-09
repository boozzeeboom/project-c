using System;

namespace ProjectC.World.FloatingOrigin.Network
{
    public enum MotionAdmissionReject
    {
        None, NotActive, WrongSender, InvalidSnapshot, WrongBinding, OldSequence,
        TimeWindow, SourceTimeRegression, ReceiveTimeRegression, RateLimited, GameRuleRejected,
        ReconfiguredDuringValidation, ReentrantValidation
    }

    /// <summary>Server ingress guard. Sender identity is supplied by NGO, never by a packet field.</summary>
    public sealed class GlobalMotionAdmission
    {
        private readonly double _maxPast;
        private readonly double _maxFuture;
        private readonly double _rate;
        private readonly int _burst;
        private GlobalMotionSnapshot _latest;
        private ulong _publisher;
        private bool _active;
        private bool _hasSourceTime;
        private double _sourceTime;
        private double _receiveTime;
        private double _tokens;
        private ulong _configurationVersion;
        private bool _validating;
        public GlobalMotionSnapshot Latest => _latest;

        public GlobalMotionAdmission(double maxPastSeconds = 1d, double maxFutureSeconds = 0.25d,
            double samplesPerSecond = 120d, int burst = 8)
        {
            if (!GlobalPosition.IsFiniteValue(maxPastSeconds) || maxPastSeconds < 0d ||
                !GlobalPosition.IsFiniteValue(maxFutureSeconds) || maxFutureSeconds < 0d ||
                !GlobalPosition.IsFiniteValue(samplesPerSecond) || samplesPerSecond <= 0d || burst < 1 || burst > 256)
                throw new ArgumentOutOfRangeException(nameof(maxPastSeconds));
            _maxPast = maxPastSeconds; _maxFuture = maxFutureSeconds; _rate = samplesPerSecond; _burst = burst;
        }

        public void Begin(ulong publisher, GlobalMotionSnapshot baseline, double serverTime)
        {
            if (!baseline.TryValidate(out _) || !GlobalPosition.IsFiniteValue(serverTime) || serverTime < 0d || baseline.SampleTime > serverTime)
                throw new ArgumentException("Invalid authoritative baseline.");
            _configurationVersion = checked(_configurationVersion + 1);
            _latest = baseline; _publisher = publisher; _receiveTime = serverTime;
            _tokens = _burst; _hasSourceTime = false; _sourceTime = 0d; _active = true;
        }

        public void Stop() { _configurationVersion = checked(_configurationVersion + 1); _active = false; }

        public bool TryAccept(ulong sender, GlobalMotionSnapshot input, double serverTime,
            Func<GlobalMotionSnapshot, bool> gameValidator, out GlobalMotionSnapshot accepted, out MotionAdmissionReject reason)
        {
            accepted = default;
            reason = MotionAdmissionReject.ReentrantValidation; if (_validating) return false;
            reason = MotionAdmissionReject.NotActive; if (!_active) return false;
            reason = MotionAdmissionReject.WrongSender; if (sender != _publisher) return false;
            reason = MotionAdmissionReject.InvalidSnapshot; if (!input.TryValidate(out _)) return false;
            reason = MotionAdmissionReject.WrongBinding; if (input.Binding != _latest.Binding) return false;
            reason = MotionAdmissionReject.OldSequence; if (!GlobalMotionBuffer.IsNewerSequence(input.Sequence, _latest.Sequence)) return false;
            reason = MotionAdmissionReject.ReceiveTimeRegression;
            if (!GlobalPosition.IsFiniteValue(serverTime) || serverTime < _receiveTime) return false;
            reason = MotionAdmissionReject.TimeWindow;
            if (input.SampleTime < serverTime - _maxPast || input.SampleTime > serverTime + _maxFuture) return false;
            reason = MotionAdmissionReject.SourceTimeRegression;
            if (_hasSourceTime && input.SampleTime < _sourceTime) return false;
            double tokens = Math.Min(_burst, _tokens + (serverTime - _receiveTime) * _rate);
            reason = MotionAdmissionReject.RateLimited; if (tokens < 1d) return false;
            // Charge validated attempts before expensive game rules, including rejected/throwing callbacks.
            _receiveTime = serverTime; _tokens = tokens - 1d;
            ulong configuration = _configurationVersion;
            bool approved;
            _validating = true;
            try { approved = gameValidator == null || gameValidator(input); }
            finally { _validating = false; }
            reason = MotionAdmissionReject.ReconfiguredDuringValidation;
            if (!_active || configuration != _configurationVersion) return false;
            reason = MotionAdmissionReject.GameRuleRejected;
            if (!approved) return false;

            // Commit the pose/sequence only after every validation. Client clock never drives peer interpolation.
            accepted = input;
            accepted.SampleTime = serverTime;
            _latest = accepted; _sourceTime = input.SampleTime; _hasSourceTime = true;
            reason = MotionAdmissionReject.None;
            return true;
        }
    }
}
