# DEEP ANALYSIS: FloatingOriginMP

Date: 17.04.2026
Project: ProjectC_client
Focus: Why solutions do not work and how to break the cycle

== 1. HISTORY OF THE PROBLEM ==

Stage 1: FloatingOrigin creation (14.04.2026)
- Created FloatingOrigin.cs - basic world shift
- Created FloatingOriginMP.cs - multiplayer version
- Threshold = 100,000

Stage 2: Scene hierarchy destruction (14.04.2026)
- CollectWorldObjects() reparented ALL objects under WorldRoot
- ThirdPersonCamera.Awake() added FloatingOriginMP unconditionally
- NetworkManager, NetworkPlayer changed parent - EVERYTHING BROKE
- FIX: Removed auto-add from ThirdPersonCamera

Stage 3: Two conflicting FloatingOrigins
- WorldCamera (old FloatingOrigin) + ThirdPersonCamera (new FloatingOriginMP)
- Double world shift per frame - objects fly away

Stage 4: Camera dependency - NullReferenceException (17.04.2026)
- FloatingOriginMP on EMPTY OBJECT in scene
- _camera = GetComponent<Camera>() returns null
- Camera.main also null - CRASH at ResetOrigin()
- Two models conflict: Model A (on Camera) vs Model B (on empty object)


Stage 5: WorldRoot at 90 millions
- After failed iterations, WorldRoot at ~90,000,000
- threshold = 1,000,000 - shift only happens at >1M

== 2. WHY EACH SOLUTION DID NOT WORK ==

Solution 1: Reduce threshold
- Tried: threshold = 100,000 -> 150,000
- WorldRoot already at 90,000,000
- threshold 150,000 << 90,000,000
- Shift cannot return world to origin
- Root cause: threshold is RELATIVE TO camera, not absolute


Solution 2: Add Camera.main fallback
- _camera = GetComponent<Camera>(); if (null) _camera = Camera.main;
- Camera.main searched by tag - may not be initialized
- StreamingTest_AutoRun calls ResetOrigin() in TeleportToTestPosition()
- _camera still null - CRASH
- Root cause: Camera may be on different object

Solution 3: Reset WorldRoot in Editor
- Tried: Position = (0, 0, 0)
- Reset in Play Mode, not Editor
- Changes not saved after Play Mode
- Root cause: Short-term solution without understanding source

Solution 4: Remove FloatingOriginMP from prefab
- Removed from prefab, forgot about manual placement
- Multiple components - multiple shifts
- Root cause: No single entry point

Solution 5: Correct placement instructions
- Instructions contradictory (on Camera OR on empty object)
- Empty object + Camera.main = unreliable
- Root cause: No architectural solution, only workarounds


== 3. ARCHITECTURAL SOLUTIONS THAT BREAK THE CYCLE ==


Current Problem: Position tied to Camera
- _camera: Camera (TIGHT DEPENDENCY)
- Used in LateUpdate() and ResetOrigin()
- If no camera - component useless
- If camera not there - works incorrectly
- Cannot use in pure server mode

Solution: Separation of Concerns
- positionSource: Transform (Position Source)
- GetWorldPosition(): Vector3 (Abstraction)
  1. positionSource?.position (Priority)
  2. _camera?.transform.position (Fallback 1)
  3. Camera.main?.transform.position (Fallback 2)
  4. NetworkManager.Singleton?.LocalClient?.PlayerObject (Fallback 3)
- LateUpdate() uses GetWorldPosition()
- ResetOrigin() uses GetWorldPosition()
- ApplyWorldShift() does not depend on position

Benefits:
- Position is PROPERTY, not component
- Can use on empty object
- Can specify any Transform as source
- Works in server mode (without camera)


== 4. SPECIFIC ACTION PLAN ==


Phase 1: Fix NullReferenceException (priority 1)
Task: Make _camera optional
File: FloatingOriginMP.cs

Changes:
[Header("Position Source")]
public Transform positionSource;

private Vector3 GetWorldPosition() {
    if (positionSource != null) return positionSource.position;
    if (_camera != null) return _camera.transform.position;
    if (Camera.main != null) return Camera.main.transform.position;
    var player = NetworkManager.Singleton?.LocalClient?.PlayerObject;
    if (player != null) return player.transform.position;
    Debug.LogWarning("[FloatingOriginMP] No position source found!");
    return Vector3.zero;
}

Then replace:
- LateUpdate(): _camera.transform.position -> GetWorldPosition()
- ResetOrigin(): _camera.transform.position -> GetWorldPosition()


Phase 2: Fix world positions in Editor (priority 2)
ACTION IN EDITOR, NOT PLAY MODE:
1. Open scene Assets/ProjectC_1.unity
2. Find WorldRoot in Hierarchy
3. Inspector -> Transform -> Position = (0, 0, 0)
4. Find Clouds -> Position = (0, 0, 0)
5. Find TradeZones -> Position = (0, 0, 0)
6. All other world objects -> ~0, 0, 0

Phase 3: Remove duplicates (priority 3)
Find: Assets/_Project/Prefabs/ThirdPersonCamera.prefab
Leave FloatingOriginMP in ONE place only:
Scene
  - FloatingOriginController (SINGLE)
      - FloatingOriginMP
  - NetworkManagerController
  - Player
  - WorldRoot

Phase 4: Test (priority 4)
Expected results:
- Camera position stays in place (150,000)
- WorldRoot.position shifts (-150,000)
- TotalOffset grows
- Artifacts disappear

== 5. CHECKLIST FOR NEXT SESSION ==


- [ ] Add positionSource field to FloatingOriginMP
- [ ] Implement GetWorldPosition() with Player fallback
- [ ] Replace _camera.transform.position with GetWorldPosition()
- [ ] In Editor: reset WorldRoot position to (0,0,0)
- [ ] Remove FloatingOriginMP from ThirdPersonCamera prefab
- [ ] Create FloatingOriginController in scene
- [ ] Test F5 - position 150,000 - shift
- [ ] Test F8 - ResetOrigin
- [ ] Test multiplayer (Host + Client)


== 6. KEY LESSONS ==


Lesson 1: Separation of Concerns
Wrong: Component tied to specific type (Camera)
Right: Abstraction (Transform), multiple fallbacks

Lesson 2: Threshold - relative value
Wrong: threshold as absolute limit
Right: threshold as distance RELATIVE TO origin

Lesson 3: Solutions must be persistent
Wrong: Reset positions in Play Mode
Right: Fix in Editor, commit changes

Lesson 4: Single entry point
Wrong: FloatingOriginMP on prefab + manually = duplicates
Right: One component on dedicated object

Lesson 5: Position source - player, not camera
Wrong: Camera - position source (may not exist)
Right: Player - position source (always exists)


==
Author: Claude Code (Technical Writer)
Date: 17.04.2026
Next session: Fix FloatingOriginMP - phase 1