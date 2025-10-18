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

    [Header("Mouse Look")]
    public float mouseSensitivity = 2.0f;
    public Transform cameraTransform;
    public float cameraHeight = 0.9f;

    private CharacterController cc;
    private PlayerInputActions input; // ❌ NO inicializar aquí
    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool jumpPressed;
    private bool runPressed;

    private float verticalVelocity;
    private float pitch;

    void Awake()
    {
        cc = GetComponent<CharacterController>();

        // Inicializar input en Awake ✅
        input = new PlayerInputActions();

        // Movimiento (WASD)
        input.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        input.Player.Move.canceled += _ => moveInput = Vector2.zero;

        // Mouse look
        input.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        input.Player.Look.canceled += _ => lookInput = Vector2.zero;

        // Jump
        input.Player.Jump.performed += _ => jumpPressed = true;
        input.Player.Jump.canceled += _ => jumpPressed = false;

        // Run
        input.Player.Run.performed += _ => runPressed = true;
        input.Player.Run.canceled += _ => runPressed = false;
    }

    void OnEnable()
    {
        if (input != null)
            input.Enable();
    }

    void OnDisable()
    {
        if (input != null)
            input.Disable();
    }

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
