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
    [SerializeField] private float yawSpeed = 90f;
    [SerializeField] private float glideGravityMultiplier = 0.35f;

    [Header("Drag Control")]
    [SerializeField] private float screenWidthSwipeDegrees = 180f;
    [SerializeField] private bool holdKeepsTurning = true;
    [SerializeField] private float holdDelay = 0.2f;

    [Header("Camera")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float cameraFollowSpeed = 8f;

    private Vector3 cameraOffset;
    private Quaternion cameraRotationOffset;

    [Header("Ground Check")]
    [SerializeField] private float groundCheckDistance = 0.7f;
    [SerializeField] private LayerMask groundMask = ~0;

    private float currentYaw;
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

        if (!isGrounded)
        {
            rb.AddForce(Physics.gravity * (glideGravityMultiplier - 1f), ForceMode.Acceleration);
        }

        // Rotate the Player root for gameplay heading
        rb.MoveRotation(Quaternion.Euler(0f, currentYaw, 0f));

        // Keep the capsule using its original local visual rotation
        playerCapsule.localRotation = capsuleLocalRotation;

        // Move forward in Player root facing direction
        Vector3 velocity = rb.transform.forward * forwardSpeed;
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
