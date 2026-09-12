using System;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// Immutable intake envelope for a user-supplied runtime evidence review.
    /// Sealing this package does not publish evidence, install a driver or mutate Unity state.
    /// </summary>
    public readonly struct GlobalMotionRebaseUserControlledEvidencePackage
    {
        public Guid PackageId { get; }
        public string CaptureReference { get; }
        public GlobalMotionRebaseUserControlledEvidenceReview Review { get; }
        public bool UserSupplied { get; }
        public bool IsValid => PackageId != Guid.Empty &&
            !string.IsNullOrWhiteSpace(CaptureReference) &&
            Review.IsReady && UserSupplied;

        internal GlobalMotionRebaseUserControlledEvidencePackage(
            Guid packageId,
            string captureReference,
            GlobalMotionRebaseUserControlledEvidenceReview review,
            bool userSupplied)
        {
            PackageId = packageId;
            CaptureReference = captureReference;
            Review = review;
            UserSupplied = userSupplied;
        }
    }

    /// <summary>
    /// Pure package-sealing boundary after the comprehensive evidence review passes.
    /// It requires an explicit user-supplied capture reference and never performs runtime work.
    /// </summary>
    public static class GlobalMotionRebaseUserControlledEvidencePackageGate
    {
        public static bool TrySeal(
            GlobalMotionRebaseUserControlledEvidenceReview review,
            string captureReference,
            bool userSupplied,
            out GlobalMotionRebaseUserControlledEvidencePackage package,
            out string error)
        {
            package = default;
            error = null;
            if (!review.IsReady)
                return Reject("evidence_review_not_ready", out error);
            if (!userSupplied)
                return Reject("user_supplied_capture_required", out error);
            if (string.IsNullOrWhiteSpace(captureReference) ||
                captureReference.Trim() != captureReference)
                return Reject("capture_reference_required", out error);

            package = new GlobalMotionRebaseUserControlledEvidencePackage(
                Guid.NewGuid(),
                captureReference,
                review,
                true);
            return true;
        }

        private static bool Reject(string reason, out string error)
        {
            error = reason;
            return false;
        }
    }
}
