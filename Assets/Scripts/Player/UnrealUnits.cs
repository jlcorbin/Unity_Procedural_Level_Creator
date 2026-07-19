namespace LevelGen.Player
{
    /// <summary>
    /// UE5 → Unity unit-conversion constants for the M22 player-parity port.
    ///
    /// The source UE5 spec (<c>PlayerCharacter_MechanicsSpec_ForUnity.md</c>) is
    /// authored in centimeters; this project is in meters. Canonical rule: divide
    /// distances/speeds by 100. Angles and unitless scalars are unchanged.
    ///
    /// Centralizing these here keeps every ported system reading the same numbers
    /// and makes re-tuning against the UE source a single-file edit.
    /// </summary>
    public static class UnrealUnits
    {
        /// <summary>Centimeters-to-meters factor (UE → Unity).</summary>
        public const float CmToM = 0.01f;

        /// <summary>Convert a UE centimeter value to Unity meters.</summary>
        public static float ToMeters(float centimeters) => centimeters * CmToM;

        // ---- Movement (spec §3) ----
        /// <summary>UE 600 cm/s → 6.0 m/s max walk speed.</summary>
        public const float MaxWalkSpeed = 6.0f;
        /// <summary>UE 2048 cm/s² → 20.48 m/s² acceleration.</summary>
        public const float MaxAcceleration = 20.48f;
        /// <summary>UE 2048 cm/s² → 20.48 m/s² braking deceleration.</summary>
        public const float BrakingDeceleration = 20.48f;
        /// <summary>UE 8 ground-friction coefficient (unitless, unchanged).</summary>
        public const float GroundFriction = 8f;
        /// <summary>UE yaw rotation rate 500°/s (angular, unchanged).</summary>
        public const float YawRotationRate = 500f;
        /// <summary>UE 420 cm/s jump launch → 4.2 m/s.</summary>
        public const float JumpVelocity = 4.2f;
        /// <summary>UE 0.05 air control (unitless, unchanged).</summary>
        public const float AirControl = 0.05f;

        // ---- Capsule (spec §1) ----
        /// <summary>UE half-height 88 → 1.76 m full character height.</summary>
        public const float CapsuleHeight = 1.76f;
        /// <summary>UE radius 34 → 0.34 m.</summary>
        public const float CapsuleRadius = 0.34f;

        // ---- Camera (spec §2), adapted to the Cinemachine orbital rig ----
        /// <summary>UE boom length 350 → 3.5 m orbit radius.</summary>
        public const float BoomLength = 3.5f;
        /// <summary>UE boom Y offset 60 → 0.60 m shoulder (right) offset.</summary>
        public const float ShoulderOffsetRight = 0.60f;
        /// <summary>UE boom Z offset 25 → 0.25 m height offset.</summary>
        public const float ShoulderOffsetUp = 0.25f;
        /// <summary>UE camera FOV 90.</summary>
        public const float CameraFov = 90f;

        // ---- Melee (spec §7) — kept for reference; hit detection stays collider-based ----
        /// <summary>UE overlap radius 150 → 1.5 m (reference only).</summary>
        public const float MeleeOverlapRadius = 1.5f;
        /// <summary>UE melee damage 20.</summary>
        public const int MeleeDamage = 20;

        // ---- Ranged (spec §8) ----
        /// <summary>UE arrow speed 6000 cm/s → 60 m/s.</summary>
        public const float ArrowSpeed = 60f;
        /// <summary>UE arrow gravity scale 0.4 (unitless, unchanged).</summary>
        public const float ArrowGravityScale = 0.4f;
        /// <summary>UE arrow collision radius 5 → 0.05 m.</summary>
        public const float ArrowCollisionRadius = 0.05f;
        /// <summary>UE arrow lifespan 5 s.</summary>
        public const float ArrowLifespan = 5f;
        /// <summary>UE arrow damage 30.</summary>
        public const int ArrowDamage = 30;
        /// <summary>UE free-aim camera ray length 5000 cm → 50 m.</summary>
        public const float FreeAimRayLength = 50f;

        // ---- Target lock (spec §10) ----
        /// <summary>UE lock spherecast distance 3000 cm → 30 m.</summary>
        public const float LockCastDistance = 30f;
        /// <summary>UE lock spherecast radius 125 cm → 1.25 m.</summary>
        public const float LockCastRadius = 1.25f;
        /// <summary>UE lock aim Z offset −100 cm → −1.0 m (frames enemy slightly high).</summary>
        public const float LockAimZOffset = 1.0f;
    }
}
