using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public class AirborneDragGlider : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody rb; // Rigidbody on Player root
    [SerializeField] private Transform playerCapsule;

    [Header("Movement")]
    [SerializeField] private float forwardSpeed = 12f;
    [SerializeField] private float yawSpeed = 240f;
    [SerializeField] private float glideGravityMultiplier = 0.35f;

    [Header("Drag Control")]
    [SerializeField] private float screenWidthSwipeDegrees = 180f;
    [SerializeField] private bool holdKeepsTurning = false;
    [SerializeField] private float holdDelay = 0.025f;

    [Header("Vertical Glide Control")]
    [SerializeField] private bool enableVerticalGlideControl = true;
    [SerializeField] private float screenHeightSwipePitchDegrees = 200f;
    [SerializeField] private float maxPitchDegrees = 60f;
    [SerializeField] private bool pitchReturnsToNeutral = false;
    [SerializeField] private float pitchReturnSpeed = 60f;
    [SerializeField] private float pitchDownForwardSpeedBonus = 4f;
    [SerializeField] private float pitchUpForwardSpeedPenalty = 4f;
    [SerializeField] private float maxPitchDownGravityIncrease = 0.5f;
    [SerializeField] private float maxPitchUpGravityReduction = 0.2f;
    // X axis: normalized pitch (0 = neutral, 1 = max pitch up/down). Y axis: gravity influence factor (0 = no change, 1 = full effect of maxPitchUpGravityReduction / maxPitchDownGravityIncrease).
    [SerializeField] private AnimationCurve pitchToGravityCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Camera")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float cameraFollowSpeed = 8f;

    private Vector3 cameraOffset;
    private Quaternion cameraRotationOffset;

    [Header("Ground Check")]
    [SerializeField] private float groundCheckDistance = 0.7f;
    [SerializeField] private LayerMask groundMask = ~0;

    private float currentYaw;
    private float currentPitch;
    private bool isGrounded;
    private Quaternion capsuleLocalRotation;
    private float lastDeltaX;
    private float timeSinceLastDragMovement;
    private bool pointerHeld;

    private void OnEnable()
    {
        EnhancedTouchSupport.Enable();
    }

    private void OnDisable()
    {
        EnhancedTouchSupport.Disable();
    }

    private void Awake()
    {
        if (rb == null)
        {
            Debug.LogError("Rigidbody not assigned on Player root");
            return;
        }

        rb.freezeRotation = true;

        if (playerCapsule == null)
            Debug.LogError("PlayerCapsule not assigned");

        capsuleLocalRotation = playerCapsule.localRotation;

        CacheCameraOffset();
    }

    private void Update()
    {
        ReadDragInput();
    }

    private void FixedUpdate()
    {
        CheckGrounded();

        float activeGravityMultiplier = glideGravityMultiplier;

        if (!isGrounded && enableVerticalGlideControl)
        {
            float normalizedPitch = Mathf.Clamp01(Mathf.Abs(currentPitch) / maxPitchDegrees);
            float gravityCurveValue = pitchToGravityCurve.Evaluate(normalizedPitch);

            if (currentPitch > 0f)
                activeGravityMultiplier = glideGravityMultiplier + maxPitchDownGravityIncrease * gravityCurveValue;
            else if (currentPitch < 0f)
                activeGravityMultiplier = Mathf.Max(0f, glideGravityMultiplier - maxPitchUpGravityReduction * gravityCurveValue);
        }

        if (!isGrounded)
        {
            rb.AddForce(Physics.gravity * (activeGravityMultiplier - 1f), ForceMode.Acceleration);
        }

        if (isGrounded)
            currentPitch = 0f;

        // Rotate the Player root for gameplay heading (yaw only)
        rb.MoveRotation(Quaternion.Euler(0f, currentYaw, 0f));

        // Apply visual pitch on the capsule around its local Z axis (initial Z=90)
        playerCapsule.localRotation = capsuleLocalRotation * Quaternion.Euler(0f, 0f, currentPitch);

        float activeForwardSpeed = forwardSpeed;

        if (!isGrounded && enableVerticalGlideControl)
        {
            if (currentPitch > 0f)
                activeForwardSpeed += pitchDownForwardSpeedBonus * (currentPitch / maxPitchDegrees);
            else if (currentPitch < 0f)
                activeForwardSpeed -= pitchUpForwardSpeedPenalty * (-currentPitch / maxPitchDegrees);
        }

        // Move forward in Player root facing direction
        Vector3 velocity = rb.transform.forward * activeForwardSpeed;
        velocity.y = rb.linearVelocity.y;

        rb.linearVelocity = velocity;
    }

    private void LateUpdate()
    {
        UpdateCamera();
    }

    private void ReadDragInput()
    {
        bool isPointerDown = TryGetPointerDelta(out Vector2 pointerDelta);
        float degreesPerPixel = screenWidthSwipeDegrees / Screen.width;
        float pitchDegreesPerPixel = screenHeightSwipePitchDegrees / Screen.height;

        if (!isPointerDown)
        {
            pointerHeld = false;
            lastDeltaX = 0f;
            timeSinceLastDragMovement = 0f;
            return;
        }

        pointerHeld = true;

        if (Mathf.Abs(pointerDelta.x) > 0.01f)
        {
            lastDeltaX = pointerDelta.x;
            timeSinceLastDragMovement = 0f;
            currentYaw += pointerDelta.x * degreesPerPixel;
        }
        else
        {
            timeSinceLastDragMovement += Time.deltaTime;

            if (holdKeepsTurning && timeSinceLastDragMovement >= holdDelay)
                currentYaw += lastDeltaX * degreesPerPixel;
        }

        if (!isGrounded && enableVerticalGlideControl && Mathf.Abs(pointerDelta.y) > 0.01f)
        {
            currentPitch = Mathf.Clamp(currentPitch + pointerDelta.y * pitchDegreesPerPixel, -maxPitchDegrees, maxPitchDegrees);
        }
        else if (pitchReturnsToNeutral && enableVerticalGlideControl)
        {
            currentPitch = Mathf.MoveTowards(currentPitch, 0f, pitchReturnSpeed * Time.deltaTime);
        }
    }

    private bool TryGetPointerDelta(out Vector2 pointerDelta)
    {
        if (Touch.activeTouches.Count > 0)
        {
            pointerDelta = Touch.activeTouches[0].delta;
            return true;
        }

        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            pointerDelta = Mouse.current.delta.ReadValue();
            return true;
        }

        pointerDelta = default;
        return false;
    }


    private void CheckGrounded()
    {
        isGrounded = Physics.Raycast(
            rb.position,
            Vector3.down,
            groundCheckDistance,
            groundMask
        );
    }

    private void CacheCameraOffset()
    {
        if (cameraTransform == null || rb == null)
            return;

        cameraOffset = Quaternion.Inverse(rb.rotation) * (cameraTransform.position - rb.position);
        cameraRotationOffset = Quaternion.Inverse(rb.rotation) * cameraTransform.rotation;
    }

    private void UpdateCamera()
    {
        if (cameraTransform == null || rb == null)
            return;

        Vector3 targetPosition = rb.position + rb.rotation * cameraOffset;
        Quaternion targetRotation = rb.rotation * cameraRotationOffset;

        cameraTransform.position = targetPosition;
        cameraTransform.rotation = targetRotation;
    }
}
