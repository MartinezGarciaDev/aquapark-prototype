using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public class AirborneDragGlider : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody rb; // Rigidbody on PlayerCapsule
    [SerializeField] private Transform playerCapsule;

    [Header("Movement")]
    [SerializeField] private float forwardSpeed = 12f;
    [SerializeField] private float yawSpeed = 90f;
    [SerializeField] private float glideGravityMultiplier = 0.35f;

    [Header("Drag Control")]
    [SerializeField] private float dragSensitivity = 0.02f;
    [SerializeField] private float maxSteerInput = 1f;
    [SerializeField] private float steerReturnSpeed = 6f;

    [Header("Camera")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float cameraFollowSpeed = 8f;

    private Vector3 cameraOffset;
    private Quaternion cameraRotationOffset;

    [Header("Ground Check")]
    [SerializeField] private float groundCheckDistance = 0.7f;
    [SerializeField] private LayerMask groundMask = ~0;

    private float steerInput;
    private float currentYaw;
    private bool isGrounded;
    private Quaternion capsuleLocalRotation;

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
            Debug.LogError("Rigidbody not assigned");
            return;
        }

        rb.freezeRotation = true;

        if (playerCapsule == null)
            playerCapsule = rb.transform;

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

        // Accumulate yaw
        currentYaw += steerInput * yawSpeed * Time.fixedDeltaTime;

        // Rotate the Player root for gameplay heading
        transform.rotation = Quaternion.Euler(0f, currentYaw, 0f);

        // Keep the capsule using its original local visual rotation
        playerCapsule.localRotation = capsuleLocalRotation;

        // Move forward in Player root facing direction
        Vector3 velocity = transform.forward * forwardSpeed;
        velocity.y = rb.linearVelocity.y;

        rb.linearVelocity = velocity;
    }

    private void LateUpdate()
    {
        UpdateCamera();
    }

    private void ReadDragInput()
    {
        float targetInput = 0f;

        if (Touch.activeTouches.Count > 0)
        {
            var touch = Touch.activeTouches[0];
            targetInput = touch.delta.x * dragSensitivity;
        }
        else if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            targetInput = Mouse.current.delta.ReadValue().x * dragSensitivity;
        }

        targetInput = Mathf.Clamp(targetInput, -maxSteerInput, maxSteerInput);

        steerInput = Mathf.Lerp(
            steerInput,
            targetInput,
            Time.deltaTime * steerReturnSpeed
        );
    }

    private void CheckGrounded()
    {
        isGrounded = Physics.Raycast(
            playerCapsule.position,
            Vector3.down,
            groundCheckDistance,
            groundMask
        );
    }

    private void CacheCameraOffset()
    {
        if (cameraTransform == null || rb == null)
            return;

        cameraOffset = Quaternion.Inverse(transform.rotation) * (cameraTransform.position - playerCapsule.position);
        cameraRotationOffset = Quaternion.Inverse(transform.rotation) * cameraTransform.rotation;
    }

    private void UpdateCamera()
    {
        if (cameraTransform == null || rb == null)
            return;

        Vector3 targetPosition = playerCapsule.position + transform.rotation * cameraOffset;
        Quaternion targetRotation = transform.rotation * cameraRotationOffset;

        cameraTransform.position = targetPosition;
        cameraTransform.rotation = targetRotation;
    }
}
