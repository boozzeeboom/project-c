# ShipController Custom Editor

> Created: 2026-Q2  
> File: `Assets/_Project/Scripts/Player/Editor/ShipControllerEditor.cs`  
> Pattern: `NpcShipControllerEditor`

## Purpose

Groups ~45 `[SerializeField]` fields of `ShipController` into 8 logical foldout sections, replacing the flat default inspector. Also adds a **Runtime Info** panel visible only in Play Mode.

## Foldout Sections

| # | Section | Default | Fields |
|---|---------|---------|--------|
| 1 | 🚀 Flight & Movement | open | `shipFlightClass`, `thrustForce`, `maxSpeed`, `yawForce`, `pitchForce`, `verticalForce`, `antiGravity` |
| 2 | 🔄 Smoothing | closed | `yawSmoothTime`, `pitchSmoothTime`, `liftSmoothTime`, `thrustSmoothTime`, `yawDecayTime`, `pitchDecayTime` |
| 3 | ⚖️ Physics & Mass | closed | `massMultiplier`, `massLight`..`massHeavyII`, `shipConstraints`, `linearDrag`, `angularDrag` |
| 4 | 🎯 Stabilization | closed | `autoStabilize`, `pitchStabForce`, `rollStabForce`, `maxPitchAngle` |
| 5 | 🌬️ Wind & Corridors | closed | `corridorSystem`, `windInfluence`, `windExposure`, `windDecayTime`, `_globalWindEnabled`, `_globalWindForceScale`, `_globalWindVerticalFactor` |
| 6 | 📦 Cargo Limits | closed | `baseMaxCargoSlots`, `baseMaxCargoWeight`, `baseMaxCargoVolume`, `baseCargoPenaltyFactor` + info box |
| 7 | 🔧 Modules, Meziy & Fuel | closed | `moduleManager`, `meziyActivator`, `fuelSystem`, `meziyVisual` |
| 8 | 🔑 Identity & Debug | open | `_customDisplayName`, `_keyItemData`, `_debugLog`, `_showLegacyMeziyHud` |

## Runtime Info (Play Mode only)

Appears below all foldouts when `Application.isPlaying == true`. Displays (read-only):

- **Speed**: CurrentSpeed, ForwardSpeedMps, VerticalSpeed
- **Altitude**: transform.position.y
- **State**: FlightClass, EngineRunning, IsDocked, IsHullBroken, PilotCount
- **Cargo**: CargoPenalty, ResolvedCargoClass
- **Corridor**: ActiveCorridor.corridorId, CurrentAltitudeStatus
- **Angles**: AngularVelocity, PitchAngle, RollAngle, YawAngle

## Safety Notes

- Uses `FindProperty(name)` with null-guard — silently skips renamed/removed fields
- `OnValidate()` → `ApplyShipClass()` is **not affected** (editor only changes inspector layout, doesn't touch MonoBehaviour code)
- Follows project convention: `#if UNITY_EDITOR` guard, `SerializedObject` pattern
