using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 6f;
    public float runSpeed = 10f;
    public float jumpForce = 7f;
    public float gravity = 20f;

    [Header("Flight")]
    public float flightSpeed = 15f;
    public float doubleTapDelay = 0.3f;

    [Header("Mouse Look")]
    public float mouseSensitivity = 2.0f;
    public Transform cameraTransform;
    public float cameraHeight = 0.9f;

    private CharacterController cc;
    private PlayerInputActions input;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool jumpPressed;
    private bool runPressed;

    private float verticalVelocity;
    private float pitch;

    private bool isFlying = false;
    private bool waitingForSecondJump = false;
    private float lastJumpTime = 0f;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        input = new PlayerInputActions();

        input.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        input.Player.Move.canceled += _ => moveInput = Vector2.zero;

        input.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        input.Player.Look.canceled += _ => lookInput = Vector2.zero;

        input.Player.Jump.performed += _ => jumpPressed = true;
        input.Player.Jump.canceled += _ => jumpPressed = false;

        input.Player.Run.performed += _ => runPressed = true;
        input.Player.Run.canceled += _ => runPressed = false;
    }

    void OnEnable() => input?.Enable();
    void OnDisable() => input?.Disable();

    void Start()
    {
        if (cameraTransform == null)
        {
            var cam = GetComponentInChildren<Camera>();
            if (cam != null) cameraTransform = cam.transform;
        }

        if (cameraTransform != null)
            cameraTransform.localPosition = new Vector3(0f, cameraHeight, 0f);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        HandleLook();
        HandleMovement();
    }

    private void HandleLook()
    {
        float mx = lookInput.x * mouseSensitivity;
        float my = lookInput.y * mouseSensitivity;

        transform.Rotate(Vector3.up * mx);
        pitch -= my;
        pitch = Mathf.Clamp(pitch, -85f, 85f);

        if (cameraTransform != null)
            cameraTransform.localEulerAngles = new Vector3(pitch, 0f, 0f);
    }

    private void HandleMovement()
    {
        Vector3 move = (transform.right * moveInput.x + transform.forward * moveInput.y).normalized;
        float currentSpeed = runPressed ? runSpeed : walkSpeed;

        // Doble salto para activar vuelo
        if (jumpPressed && cc.isGrounded)
        {
            if (!waitingForSecondJump)
            {
                waitingForSecondJump = true;
                lastJumpTime = Time.time;
            }
            else if (Time.time - lastJumpTime <= doubleTapDelay)
            {
                isFlying = !isFlying;
                waitingForSecondJump = false;
            }
        }

        if (waitingForSecondJump && Time.time - lastJumpTime > doubleTapDelay)
            waitingForSecondJump = false;

        // ========================
        // ✈️ MODO VUELO
        // ========================
        if (isFlying)
        {
            Vector3 flyDir = Vector3.zero;
            if (Keyboard.current.spaceKey.isPressed)
                flyDir += Vector3.up;
            if (Keyboard.current.leftCtrlKey.isPressed)
                flyDir += Vector3.down;

            Vector3 moveDir = (transform.forward * moveInput.y + transform.right * moveInput.x + flyDir).normalized;
            cc.Move(moveDir * flightSpeed * Time.deltaTime);
            return;
        }

        // ========================
        // 🚶 MODO NORMAL
        // ========================
        if (cc.isGrounded)
        {
            verticalVelocity = -1f;
            if (jumpPressed)
            {
                verticalVelocity = jumpForce;
                jumpPressed = false;
            }
        }
        else
        {
            verticalVelocity -= gravity * Time.deltaTime;
        }

        Vector3 velocity = move * currentSpeed + Vector3.up * verticalVelocity;
        cc.Move(velocity * Time.deltaTime);
    }
}
