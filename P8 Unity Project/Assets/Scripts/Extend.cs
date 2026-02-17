using UnityEngine;
using UnityEngine.InputSystem;

public class Extend : MonoBehaviour
{
    [Header("Input")]
    public InputActionProperty triggerAction;

    [Header("Settings")]
    public float maxDistance = 20f;
    public float extendSpeed = 30f;
    public float retractSpeed = 30f;
    public LayerMask raycastMask = ~0;

    [Header("Line Visual")]
    public Material lineMaterial;
    public float lineWidth = 0.05f;

    private enum State { Idle, Aiming, Extending, Extended, Retracting }
    private State state = State.Idle;

    private Transform arm;
    private Transform hand;
    private Vector3 handLocalPos;
    private Quaternion handLocalRot;

    private Vector3 targetWorldPos;
    private GameObject lineObj;
    private LineRenderer line;

    void Awake()
    {
        arm = transform;

        foreach (Transform child in arm.GetComponentsInChildren<Transform>())
        {
            if (child.CompareTag("Hand"))
            {
                hand = child;
                break;
            }
        }

        handLocalPos = hand.localPosition;
        handLocalRot = hand.localRotation;

        lineObj = new GameObject("ExtendLine");
        line = lineObj.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        line.useWorldSpace = true;
        line.alignment = LineAlignment.View;
        if (lineMaterial != null)
            line.material = lineMaterial;
        line.enabled = false;
    }

    void Update()
    {
        bool triggerPressed = triggerAction.action.WasPressedThisFrame();
        bool triggerReleased = triggerAction.action.WasReleasedThisFrame();

        switch (state)
        {
            case State.Idle:
                if (triggerPressed)
                {
                    state = State.Aiming;
                    line.enabled = true;
                }
                break;

            case State.Aiming:
                if (triggerReleased)
                {
                    if (TryGetTarget(out Vector3 hitPoint))
                    {
                        targetWorldPos = hitPoint;
                        hand.SetParent(null);
                        state = State.Extending;
                    }
                    else
                    {
                        state = State.Idle;
                        line.enabled = false;
                    }
                }
                break;

            case State.Extending:
                MoveHandToward(targetWorldPos, extendSpeed);
                if (Vector3.Distance(hand.position, targetWorldPos) < 0.01f)
                {
                    hand.position = targetWorldPos;
                    state = State.Extended;
                }
                break;

            case State.Extended:
                if (triggerPressed)
                    state = State.Retracting;
                break;

            case State.Retracting:
                Vector3 armTip = arm.TransformPoint(handLocalPos);
                MoveHandToward(armTip, retractSpeed);
                if (Vector3.Distance(hand.position, armTip) < 0.01f)
                {
                    hand.SetParent(arm);
                    hand.localPosition = handLocalPos;
                    hand.localRotation = handLocalRot;
                    line.enabled = false;
                    state = State.Idle;
                }
                break;
        }
    }

    void LateUpdate()
    {
        if (!line.enabled) return;

        Vector3 handPos = hand.position;
        Vector3 armEnd = arm.TransformPoint(handLocalPos);

        switch (state)
        {
            case State.Aiming:
                line.SetPosition(0, armEnd);
                Vector3 direction = arm.forward;
                if (Physics.Raycast(armEnd, direction, out RaycastHit hit, maxDistance, raycastMask))
                    line.SetPosition(1, hit.point);
                else
                    line.SetPosition(1, armEnd + direction * maxDistance);
                break;

            case State.Extending:
            case State.Extended:
            case State.Retracting:
                line.SetPosition(0, armEnd);
                line.SetPosition(1, handPos);
                break;
        }
    }

    void OnDestroy()
    {
        if (lineObj != null)
            Destroy(lineObj);
    }

    private bool TryGetTarget(out Vector3 hitPoint)
    {
        if (Physics.Raycast(arm.position, arm.forward, out RaycastHit hit, maxDistance, raycastMask))
        {
            hitPoint = hit.point;
            return true;
        }
        hitPoint = Vector3.zero;
        return false;
    }

    private void MoveHandToward(Vector3 target, float speed)
    {
        hand.position = Vector3.MoveTowards(hand.position, target, speed * Time.deltaTime);
    }
}
