# Puzzles — LeverGrab System

Supplementary instructions for `LeverGrab.cs` and its interactions with the arm-extension technique scripts. Read this alongside the root `CLAUDE.md`.

---

## LeverGrab Overview

`LeverGrab` is a **transform-owning** lever component. It replaces all Unity physics (no HingeJoint, no Rigidbody constraints) with explicit transform control every `LateUpdate`. The lever pivot is fixed in world space; only its rotation changes.

**Required components on the same GameObject:**
- `XRGrabInteractable` — with `trackPosition = false`, `trackRotation = false`, `throwOnDetach = false` (set in Awake, not Inspector)
- `Rigidbody` — must be `isKinematic = true` (set in Awake)
- `LeverGrab`

**Optional child:** an empty `leverHandlePoint` Transform at the grip end. If absent, the pivot is used. The attach point on `XRGrabInteractable` is forced to this handle at Awake.

---

## Inspector Setup

| Field | What to assign |
|---|---|
| `homer` | The `HOMERRaycast` component on the HOMER arm GameObject |
| `goGoExtend` | The `GoGoExtend` component |
| `goGoInteractor` | The `XRDirectInteractor` on GoGo's virtual hand |
| `leverHandlePoint` | Empty child Transform at the grip end of the lever arm |
| `hingeAxisLocal` | Local-space axis of the hinge shaft (default `Vector3.right`). Verify with the Scene-view gizmo — the **blue disc** should align with the swing plane. |
| `activationAngle` | Degrees of pull required to trigger snap (default 75°) |
| `snapToAngle` | Angle lever snaps to on activation (default 90°) |
| `snapDuration` | Duration of snap animation in seconds (default 0.15 s) |

DAOM is resolved at runtime via `DAOMArm.ActiveInstance` — no Inspector field required.

---

## Execution Order Contract

This is the **critical invariant**. Do not change execution orders without reviewing all three scripts.

| Order | Script | Role |
|---|---|---|
| -100 | XRInteractionManager | XRI grab detection; `trackPosition=false` so no object movement |
| 0 | `HOMERRaycast.Update` | State machine; `BeginGrab` skips position teleport for lever objects |
| 100 | `HOMERManipulator.LateUpdate` | Moves virtual hand; **skips** lever `position`/`rotation` |
| 100 | `GoGoExtend.LateUpdate` | Moves GoGo virtual hand |
| 200 | `LeverGrab.LateUpdate` | Reads virtual hand position, drives rotation, arc-locks hand back |

`LeverGrab` reads the virtual hand **after** technique scripts have updated it, then **overrides** it back onto the handle. This read-then-override pattern only works if order 200 > order 100.

---

## Arc-Lock Pattern

While grabbed, every `LateUpdate` (order 200) does:

1. **Lock position** — `transform.position = leverFixedPosition` (unconditional, even when not grabbed)
2. Read `virtualHandPos` from the active technique
3. Project `pivotToHand` onto the rotation plane (`normal = hingeWorldAxis`)
4. Compute `SignedAngle` between projected direction and rest direction → `currentAngle`
5. Clamp `currentAngle` to `[0, snapToAngle]`
6. Set `transform.rotation = Quaternion.AngleAxis(currentAngle, hingeWorldAxis) * restWorldRotation`
7. **Arc-lock** — override virtual hand position back to `leverHandlePoint.position` so it cannot drift off the plane

The arc-lock step (7) is what converts "player moves hand in 3D space" into "lever rotates around a single axis". Without it the virtual hand drifts and the angle calculation becomes incoherent.

---

## Key Invariants — Do Not Break

**Position is always locked.**
`transform.position = leverFixedPosition` runs at the **top** of `LateUpdate`, before any early returns. This must remain unconditional. If you move it inside an `if (isGrabbed)` block, HOMER's `BeginGrab` teleport and `HOMERManipulator`'s delta accumulation will drift the lever in world space.

**`HOMERManipulator` skips lever objects.**
`HOMERManipulator.LateUpdate` guards object movement with `GetComponent<LeverGrab>() == null`. If you remove this guard, the manipulator will fight `LeverGrab`'s position reset every frame (manipulator adds delta → LeverGrab resets → net drift on the first frame of each grab).

**`HOMERRaycast.BeginGrab` skips the position teleport for lever objects.**
`obj.transform.position = virtualHand.position` is guarded by `GetComponent<LeverGrab>() == null`. Without this guard, the lever visibly jumps to the extended hand position on the grab frame, one `Update` before `LeverGrab.LateUpdate` can repair it.

**Rest state is cached in `Awake`, not at grab time.**
`restWorldRotation`, `hingeWorldAxis`, and `restDir` are fixed at `Awake`. They must not be re-cached per-grab; doing so would make the "rest" angle shift after each pull, breaking the angle measurement.

**`currentAngle` persists across grabs.**
After a mid-pull release, `currentAngle` holds the released angle. The non-grabbed branch of `LateUpdate` uses it to hold the lever at that angle. On re-grab, the player must pull past `currentAngle` to advance further (or past `activationAngle` to snap).

---

## Technique Grab Detection

| Technique | How grab is detected |
|---|---|
| HOMER | `homer.GrabStarted` event, filtered by `obj == gameObject` |
| Go-Go | `XRGrabInteractable.selectEntered`, filtered by `args.interactorObject == goGoInteractor` |
| DAOM | `XRGrabInteractable.selectEntered`, filtered by `args.interactorObject == DAOMArm.ActiveInstance.Interactor` |

HOMER **also** fires `XRGrabInteractable.selectEntered` (because HOMER uses XRI internally), but `OnSelectEntered` ignores it — HOMER grab is handled exclusively through the `GrabStarted` event to avoid double-StartGrab.

---

## Snap Sequence

When `currentAngle >= activationAngle`:
1. `StartCoroutine(SnapAndActivate())` is called (only once — `isActivated` gates it)
2. `isActivated = true`, `ForceRelease()` programmatically drops the active technique
3. Coroutine lerps rotation from `currentAngle` → `snapToAngle` over `snapDuration` seconds (SmoothStep easing)
4. `leverGrabbable.enabled = false` — prevents re-grab
5. `OnLeverActivated.Invoke()`

The snap coroutine sets `transform.position = leverFixedPosition` indirectly through the unconditional LateUpdate block even while animating, so the lever cannot drift during the snap.

---

## Gizmo Reference (Scene View)

| Element | Meaning |
|---|---|
| Cyan line | Hinge axis through pivot |
| White line + sphere | Rest arm direction to handle |
| Blue disc (transparent) | Rotation plane — lever swings within this disc |
| Yellow arc | Pull zone: 0° → `activationAngle` |
| Red arc | Snap zone: `activationAngle` → `snapToAngle` |

If the blue disc is perpendicular to the lever's intended swing plane, flip `hingeAxisLocal` (e.g. `Vector3.right` → `Vector3.forward`).

---

## Common Mistakes

- **Lever moves in the air when grabbed** — Check that `HOMERManipulator` has the `LeverGrab` null-check, and that `HOMERRaycast.BeginGrab` skips the position teleport. Also confirm `transform.position = leverFixedPosition` is before the early returns in `LateUpdate`.
- **Lever angle jumps on re-grab** — Do not re-cache `restWorldRotation` or `hingeWorldAxis` at grab time.
- **Lever snaps backward past 0°** — `SignedAngle` can return negative values; confirm the clamp `Mathf.Clamp(angle, 0f, snapToAngle)` is present.
- **HOMER fires StartGrab twice** — Ensure `OnSelectEntered` does not handle HOMER interactors. Filter only for `goGoInteractor` and `DAOMArm.ActiveInstance.Interactor`.
- **Snap fires repeatedly** — The `isActivated` flag in `SnapAndActivate` prevents this, but only if `StartCoroutine` is guarded by `if (!isActivated)` before `isActivated = true`. The current implementation sets `isActivated = true` inside the coroutine, so the `LateUpdate` `if (isActivated) return` guard on the outer check blocks re-entry.
- **Lever falls under gravity** — Rigidbody `isKinematic` must be set in `Awake`. If the Inspector has it unchecked and `Awake` hasn't run yet (e.g. in prefab editing mode), gravity will apply for one frame.
