# P8 DAOM Project — CLAUDE.md

This is a **Unity VR game** (PC VR, Windows/OpenXR) where the player manipulates out-of-reach objects using different arm-extension techniques: HOMER, Go-Go, and exploring a launched DAOM (Distant Arm Object Manipulation) arm. Built for academic research (AAU MED8 semester).

---

## Tech Stack

| Layer | Package / Version |
|---|---|
| Engine | Unity 6 (6000.3.6f1) |
| Rendering | URP 17.3.0 |
| XR SDK | XR Interaction Toolkit 3.3.1 |
| XR Runtime | OpenXR 1.16.1 |
| XR Management | 4.5.4 |
| Input | Unity InputSystem 1.18.0 |
| Animation | Animation Rigging 1.4.1 |
| Level Design | ProBuilder 6.0.9 |
| Camera | Cinemachine 3.1.5 |
| VFX | Visual Effect Graph 17.3.0 |

---

## Scene Hierarchy (main: MainGameplay)

```
NewKylePlayer is the VR player
```

Other scenes: `NikoScene` (DAOM arm), `JakobScene` (environment), `Felix Scene Intro/Shader` (VFX/cel-shading), `Beck's VFX Test Area`.

---

## Scripts by System

### Teleportation (`Assets/Scripts/Teleport/`)

| Script | Purpose |
|---|---|
| `TeleportationActivator.cs` | Enables XRRayInteractor on thumbstick hold/release. Gate: `orbConnected` must be true. Events: `onBeforeTeleport`, `onAfterTeleport`. |
| `TeleportBlink.cs` | Blink-to-black fade. Screen Space - Overlay Canvas (`sortingOrder = 32767`). Coroutine: fade out → 2 frames black → ExecuteTeleport → 2 frames → fade in. |
| `TeleportLegsReticle.cs` | Spawns TeleportLegs FBX at aim point with dissolve material on all MeshRenderers. |
| `TeleportPad.cs` | Empty marker for pad objects. |

### Player (`Assets/Scripts/Player/`)

| Script | Purpose |
|---|---|
| `TeleportOrb.cs` | Marker on the teleport orb. Has `OnPlacedOnPad()` callback stub. |
| `HandTPOrbConnect.cs` | Left-hand snap zone (radius 0.12 m). Detects orb proximity, force-releases from right hand, snaps, sets `orbConnected = true` on TeleportationActivator. 0.5 s re-snap cooldown. |

### Tutorial & Objectives (`Assets/Scripts/`)

| Script | Purpose |
|---|---|
| `TutorialManager.cs` | World-space subtitle panel that smoothly follows the camera (1.5 m ahead). 30+ configurable steps, optional auto-advance timers, dynamic TextMeshPro panel width. |
| `ObjectivesManager.cs` | Central state machine for named objectives. Events: `onObjectiveCompleted(string)`, `onAllObjectivesCompleted`. API: `CompleteObjective()`, `IsCompleted()`, `AllCompleted()`. |
| `TutorialObjective.cs` | Bridges ObjectivesManager events → TutorialManager step advances. |
| `OrbTutorialObjective.cs` | Tutorial sequencer for orb pickup. HOMER mode (4 steps: extend → grab → retract → snap) and GoGo mode (4 steps: reach ≥ 5 m → grab → camera near → snap). |

### Door & Trigger (`Assets/Scripts/`)

| Script | Purpose |
|---|---|
| `DoorTrigger.cs` | Relay for one door condition. Idempotent `Activate()` / `Deactivate()`. |
| `DoorLinker.cs` | Slides two door panels when all linked DoorTriggers are activated. SmoothStep easing, configurable axis/distance/duration. Event: `OnDoorOpened`. |
| `AllTriggersCompleted.cs` | Fires `OnAllTriggersCompleted` once when every DoorTrigger in list is active. |

### Puzzles (`Assets/Scripts/Puzzles/`)

| Script | Purpose |
|---|---|
| `PressurePlate.cs` | Trigger zone activated by player ("Player" tag) or `PressurePlateTrigger` components. Events: `OnPlateActivated`, `OnPlateDeactivated`. |
| `PressurePlateTrigger.cs` | Marker on movable blocks. References its PressurePlate. |
| `CameraPresencePlate.cs` | Head-position pressure plate (XZ footprint + height range, 0–2.5 m). Allocation-free Update. |
| `BatterySocket.cs` | Snap mechanism that aligns battery poles (Plus→Plus, Minus→Minus). Force-releases from XRI and HOMER hands. Makes kinematic, disables grab permanently. |
| `WireEndGrabbable.cs` | Makes WireBuilder EndAnchor grabbable. Kinematic while held (MovePosition), dynamic on release. VelocityTracking mode. |
| `OrbPedestal.cs` | Snap zone (radius 0.15 m) for the teleport orb. Accepts from XRI or HandTPOrbConnect. Event: `OrbPlaced`. |
| `PuzzleInteractableGate.cs` | Disables all XRBaseInteractables until `Unlock()` is called. |

### Extended Reach — HOMER (`Assets/Scripts/HOMER/`)

| Script | Purpose |
|---|---|
| `HOMERRaycast.cs` | State machine: Idle → Aiming → Extending → Extended → Grabbed → Retracting. Trigger = aim/extend/retract; Select = grab. Events: `ExtendStarted`, `GrabStarted`, `GrabEnded`, `RetractStarted`. Public: `IsGrabbing`, `IsHandExtended`, `VirtualHand`. |
| `HOMERManipulator.cs` | During Grabbed: moves virtual hand by `handDelta × scaleFactor × speedScale`. scaleFactor = `|virtualHand−torso| / |physicalHand−torso|` (computed once on extend). Velocity-scaled: Lerp(0.1, 1.0, v) between minVelocity–maxVelocity. |
| `ExtendRaycast.cs` | Simplified extend-only version of HOMER (no grab). Same state machine, thumbstick reel. |
| `ExtendManipulator.cs` | Movement for extend-only mode. Clamps to sphere minDist (0.3 m) – maxDist (25 m). Thumbstick Y reels along extension vector. |

### Extended Reach — Go-Go (`Assets/Scripts/GoGo/`)

| Script | Purpose |
|---|---|
| `GoGoExtend.cs` | Poupyrev 1996 Go-Go. Chest origin = camera − 0.3 m Y. Linear zone R_v = R_r if R_r < D (D = 2/3 × armLength). Non-linear: R_v = R_r + k(R_r−D)². Public: `CurrentRr`, `CurrentRv`. Tunable: armLength (0.6 m), maxReachDistance (5 m). |

### Avatar & DAOM Arm (`Assets/Niko/`)

| Script | Purpose |
|---|---|
| `FollowXROrigin.cs` | Maps HMD/controller poses to IK targets on the avatar. |
| `VRAvatarScaler.cs` | Scales avatar limbs from measured player height (1.2–2.2 m range). |
| `LaunchArm.cs` | Aiming, raycast feedback (green/red line), and launch trigger for the DAOM arm. Static events: `SetInteractorHandedness`, `ArmRecalled`, `GrabbedGameObject`, `EarlyRecall`. |
| `DAOMArm.cs` | Launched-arm behavior. Travel → rotate to surface normal → attach → mirror player hand. Recall retracts. If arm hits object, auto-recall delivers object to player hand. |

### Misc

| Script | Purpose |
|---|---|
| `AnimateHandOnInput.cs` | Maps trigger/grip float values → Animator "Trigger"/"Grip" parameters. |

---

## Key Design Patterns

**Snap-Point Pattern** — Used by HandTPOrbConnect, OrbPedestal, BatterySocket:
1. `Physics.OverlapSphere` detects target
2. Force-release from any holding interactor
3. Disable gravity / make kinematic
4. Lerp/snap to exact position+rotation
5. Disable re-grab or apply cooldown

**Teleport–Blink Handshake** — TeleportationActivator → `onBeforeTeleport` → TeleportBlink coroutine → `ExecuteTeleport()` (built-in XRI call) → continue fade-in. Never call `ExecuteTeleport()` outside this coroutine.

**Event-Driven Puzzle Wiring** — Puzzles use C# `Action` delegates + `UnityEvent`. Wire in the Inspector. Don't create direct script references between unrelated systems.

**VR Canvas Overlay Fade** — For screen fades use a Screen Space - Overlay Canvas with `sortingOrder = 32767` and a `CanvasGroup.alpha` driven by a coroutine. This composites correctly on all OpenXR runtimes (including Oculus).

**Velocity-Scaled HOMER Movement** — Don't change the speed scaling formula in HOMER without reviewing `HOMERManipulator.cs`. The Lerp(minSpeedScale, 1.0, velocity) approach is intentional for fine vs. fast control.

---

## Prefabs

| Prefab | Path | Purpose |
|---|---|---|
| LegTeleportReticle | `Assets/Prefabs/Teleport/LegTeleportReticle.prefab` | Teleport target reticle with legs model |
| Teleport Interaction | `Assets/Prefabs/Teleport/Teleport Interaction.prefab` | Full teleport interaction setup |
| ObjectivesManager | `Assets/Prefabs/Managers/ObjectivesManager.prefab` | Persistent objective tracker |

---

## Shaders (`Assets/Shaders/`)

- **`CelShadedLighting/`** — Felix's cel-shading: `Graph Test Cel.shadergraph`, `Felix Docs Cel.shader`, `Felix Test Cel.shader`, `Felix Trans Cel.shader`, `FLitBased.shader`, `Halftone Unlit.shadergraph`
- **`LegTeleportDissolve.shadergraph`** — Dissolve effect for teleport legs reticle

---

## Gizmo Color Convention

| Color | Meaning |
|---|---|
| Purple | Orb snap zone (HandTPOrbConnect) |
| Orange | Pedestal snap zone (OrbPedestal) |
| Green | Battery Plus pole / pressure plate active |
| Red | Battery Minus pole |
| Cyan | Camera presence detection volume |

---

## XRI Event Names (v3.x)

- `LocomotionProvider.locomotionStarted` — fires before position change (once per teleport)
- `LocomotionProvider.locomotionEnded` — fires after position change

---

## Common Gotchas

- `orbConnected` must be true on TeleportationActivator before thumbstick input is accepted.
- BatterySocket waits up to 10 frames for XRI to release before forcing snap — don't add delays elsewhere.
- WireEndGrabbable uses `MovePosition()` (not transform.position) while held to avoid joint chain oscillation.
- DAOMArm uses `VectorLerp` (scale-compensated) not `Vector3.Lerp` — needed because arm parent hierarchy has non-unit scale.
- GoGo's chest origin is `camera.position − (0, 0.3, 0)` — not a tracked bone.

---

## Workflow

- Verify changes before committing them, and ask for feedback if necessary. 
- If you are unsure about how to implement something, ask for clarification or suggestions. 
- Always refer to the project structure and conventions when making changes. 
- Do not make changes that deviate from the established architecture without discussing it first.


---

## Key Gameplay Flow

- The game is split up into 4 distinct parts:
  1. **Intro Cutscene**: A short, non-interactive sequence that sets the scene and introduces the player to the environment. Cinematic camera work, voiceover narration, and visual storytelling elements.
  2. **Tutorial**: Introduces the player to the teleportation mechanics and arm-extension techniques (HOMER, Go-Go, DAOM). Step-by-step instructions with world-space subtitles and interactive objectives.
  3. **Puzzle Room**: A small environment after the tutorial with 5 different puzzles that require using the learned mechanics to manipulate objects to activate different objectives.
  4. **Outro Cutscene**: A concluding sequence that wraps up the story and provides closure. Similar cinematic techniques to the intro, but with a focus on resolution and player accomplishment.

### Tutorial

1. Player starts in a small room with a teleportation orb on a pedestal.
2. Step 1: Pick up the orb using the selected extension technique (extend arm → grab → retract)
3. Step 2: Place the orb on the left hand (activates the teleport technique)
4. Step 3: Use the orb to teleport to a new location.
5. Step 4: Step on the puzzle plate.
6. Step 5: Regrab the orb off the left hand and place it on the pedestal to start the next step.
7. Step 6: Pick up 3 fuses and place them in the box.
8. Step 7: Pull the lever to activate the door.
9. Step 8: Pick up the orb from the pedestal and place it on the left hand.
10. Step 9: Use the orb to teleport to the puzzle room, completing the tutorial.

### Puzzle Room

- Puzzle 1 (Far-to-Near Object Placement): Place the fuses in the box, just like in the tutorial.
- Puzzle 2 (Near-to-Far Object Placement): Pick up wires close to the player and place them in their respective plugs on the wall (color coded).
- Puzzle 3 (Cog Turning): Use the DAOM arm to reach out and turn cogs that are too far for the player's physical reach (3 different distances).
- Puzzle 4 (Lever Pulling): Pull 3 levers at different distances to turn on the machine.
- Puzzle 5 (ISO Task): To be decided.