using System;
using System.Collections.Generic;
using PurrNet;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(CharacterController))]

public class PlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float jumpForce = 1f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float groundCheckDistance = 0.2f;

    [Header("Look Settings")]
    [SerializeField] private float lookSensitivity = 2f;
    [SerializeField] private float maxLookAngle = 80f;

    [Header("Prop Lock Settings")]
    [SerializeField] private KeyCode lockKey = KeyCode.LeftShift;
    [SerializeField] private float orbitDistance = 5f; // Distance from prop when orbiting
    [SerializeField] private float orbitHeight = 2f; // Height offset when orbiting
    [SerializeField] private float orbitSensitivity = 2f; // Orbit rotation speed
    [SerializeField] private float minOrbitVerticalAngle = -30f; // Min vertical angle (looking up)
    [SerializeField] private float maxOrbitVerticalAngle = 60f; // Max vertical angle (looking down)

    [Header("References")]
    [SerializeField] private CinemachineCamera playerCamera;
    [SerializeField] private NetworkAnimator animator;
    [SerializeField] private List<Renderer> renderers = new();
    [SerializeField] private PlayerTeam playerTeam;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    private CharacterController characterController;
    private Vector3 velocity;
    private float verticalRotation = 0f;

    // Prop lock feature
    private bool _isLocked = false;
    private float _orbitHorizontalAngle = 0f; // Current horizontal orbit angle
    private float _orbitVerticalAngle = 20f; // Current vertical orbit angle
    private Vector3 _originalCameraLocalPos; // Camera position before locking
    private Quaternion _originalCameraLocalRot; // Camera rotation before locking

    // Seeker freeze (during hiding phase)
    private bool _isFrozen = false;

    public bool IsLocked => _isLocked;
    public bool IsFrozen => _isFrozen;

    protected override void OnSpawned()
    {
        base.OnSpawned();

        characterController = GetComponent<CharacterController>();

        if (playerTeam == null)
            playerTeam = GetComponent<PlayerTeam>();

        enabled = isOwner;
        characterController.enabled = isOwner;

        playerCamera.gameObject.SetActive(isOwner);

        if (isOwner)
        {
            foreach (var renderer in renderers)
            {
                renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Save original camera position for lock/unlock
            _originalCameraLocalPos = playerCamera.transform.localPosition;
            _originalCameraLocalRot = playerCamera.transform.localRotation;
        }
    }

    private void OnDisable()
    {
        if (isOwner)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void Start()
    {
        if (!isOwner) return;

        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }

        if (playerCamera == null)
        {
            Debug.LogError("PlayerController: No camera assigned!");
            enabled = false;
            return;
        }
    }

    private void Update()
    {
        if (!isOwner) return;

        // If frozen (seeker during hiding phase), only allow looking
        if (_isFrozen)
        {
            HandleRotation();
            return;
        }

        // Handle prop lock toggle
        HandleLockToggle();

        if (_isLocked)
        {
            // Only allow camera look while locked
            HandleLockedCameraLook();
        }
        else
        {
            // Normal movement and rotation
            HandleMovement();
            HandleRotation();
        }
    }

    private void HandleLockToggle()
    {
        // Only props can lock
        if (playerTeam == null || playerTeam.CurrentTeam != Team.Props)
            return;

        if (Input.GetKeyDown(lockKey))
        {
            _isLocked = !_isLocked;

            if (_isLocked)
            {
                // Save original camera position/rotation
                _originalCameraLocalPos = playerCamera.transform.localPosition;
                _originalCameraLocalRot = playerCamera.transform.localRotation;

                // Reset orbit angles (start behind the player)
                _orbitHorizontalAngle = 0f;
                _orbitVerticalAngle = 20f;

                // Reset animations to idle
                if (animator != null)
                {
                    animator.SetFloat("Forward", 0f);
                    animator.SetFloat("Sideways", 0f);
                }

                if (showDebug)
                    Debug.Log("[PlayerController] Prop LOCKED - Orbit camera enabled");
            }
            else
            {
                // Restore camera to original position
                ResetCameraRotation();

                if (showDebug)
                    Debug.Log("[PlayerController] Prop UNLOCKED - Movement enabled");
            }
        }
    }

    private void HandleLockedCameraLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * orbitSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * orbitSensitivity;

        // Update orbit angles
        _orbitHorizontalAngle += mouseX;
        _orbitVerticalAngle += mouseY;

        // Clamp vertical angle
        _orbitVerticalAngle = Mathf.Clamp(_orbitVerticalAngle, minOrbitVerticalAngle, maxOrbitVerticalAngle);

        // Calculate orbit position
        // The pivot point is the player's position plus a small height offset
        Vector3 pivotPoint = transform.position + Vector3.up * orbitHeight;

        // Calculate camera position on the orbit sphere
        float horizontalRad = _orbitHorizontalAngle * Mathf.Deg2Rad;
        float verticalRad = _orbitVerticalAngle * Mathf.Deg2Rad;

        // Spherical to Cartesian coordinates
        float x = orbitDistance * Mathf.Sin(horizontalRad) * Mathf.Cos(verticalRad);
        float y = orbitDistance * Mathf.Sin(verticalRad);
        float z = -orbitDistance * Mathf.Cos(horizontalRad) * Mathf.Cos(verticalRad);

        // Position is relative to player's forward direction
        Vector3 offset = transform.right * x + Vector3.up * y + transform.forward * z;
        Vector3 targetCameraPos = pivotPoint + offset;

        // Set camera position (in world space, so we need to convert)
        playerCamera.transform.position = targetCameraPos;

        // Make camera look at the pivot point (player)
        playerCamera.transform.LookAt(pivotPoint);
    }

    private void ResetCameraRotation()
    {
        // Restore camera to original local position and rotation
        playerCamera.transform.localPosition = _originalCameraLocalPos;
        playerCamera.transform.localRotation = _originalCameraLocalRot;

        // Reset orbit angles
        _orbitHorizontalAngle = 0f;
        _orbitVerticalAngle = 20f;
    }

    private void HandleMovement()
    {
        bool isGrounded = IsGrounded();
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        Vector3 moveDirection = transform.right * horizontal + transform.forward * vertical;
        moveDirection = Vector3.ClampMagnitude(moveDirection, 1f);

        // Sprint only for non-props (seekers)
        bool canSprint = playerTeam == null || playerTeam.CurrentTeam != Team.Props;
        float currentSpeed = (canSprint && Input.GetKey(KeyCode.LeftShift)) ? sprintSpeed : moveSpeed;

        characterController.Move(moveDirection * currentSpeed * Time.deltaTime);

        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
        }

        velocity.y += gravity * Time.deltaTime;
        characterController.Move(velocity * Time.deltaTime);

        // Handle animations
        if (animator != null)
        {
            animator.SetFloat("Forward", vertical);
            animator.SetFloat("Sideways", horizontal);
        }
    }

    private void HandleRotation()
    {
        float mouseX = Input.GetAxis("Mouse X") * lookSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * lookSensitivity;

        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -maxLookAngle, maxLookAngle);
        playerCamera.transform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);

        transform.Rotate(Vector3.up * mouseX);
    }

    private bool IsGrounded()
    {
        return Physics.Raycast(transform.position + Vector3.up * 0.03f, Vector3.down, groundCheckDistance);
    }

    /// <summary>
    /// Force unlock the player (e.g., when taking damage)
    /// </summary>
    public void ForceUnlock()
    {
        if (_isLocked)
        {
            _isLocked = false;
            ResetCameraRotation();

            if (showDebug)
                Debug.Log("[PlayerController] Prop FORCE UNLOCKED");
        }
    }

    /// <summary>
    /// Check if the player is currently locked
    /// </summary>
    public bool GetIsLocked()
    {
        return _isLocked;
    }

    /// <summary>
    /// Set the frozen state (used during hiding phase for seekers)
    /// </summary>
    public void SetFrozen(bool frozen)
    {
        _isFrozen = frozen;

        if (showDebug)
            Debug.Log($"[PlayerController] Frozen: {frozen}");
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position + Vector3.up * 0.03f, Vector3.down * groundCheckDistance);
    }
#endif
}