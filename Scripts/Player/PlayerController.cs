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

    [Header("Block Interaction")]
    public float reachDistance = 6f;
    public LayerMask blockMask;
    public byte selectedBlock = 1;

    [Header("Hotbar")]
    public byte[] slots = new byte[9];
    public int selectedSlot = 0;

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

    private WorldGenerator world;

    // === Debug/Highlight ===
    private Vector3Int? highlightedBlock = null;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        input = new PlayerInputActions();

        // Movimiento
        input.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        input.Player.Move.canceled += _ => moveInput = Vector2.zero;

        input.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        input.Player.Look.canceled += _ => lookInput = Vector2.zero;
        input.Player.Look.Enable();

        input.Player.Jump.started += _ => jumpPressed = true;
        input.Player.Jump.canceled += _ => jumpPressed = false;

        input.Player.Run.started += _ => runPressed = true;
        input.Player.Run.canceled += _ => runPressed = false;

        // Interacción
        input.Player.Attack.performed += _ => TryBreakBlock();
        input.Player.Use.performed += _ => TryPlaceBlock();
    }

    void OnEnable() => input.Enable();
    void OnDisable() => input.Disable();

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

        world = FindFirstObjectByType<WorldGenerator>();

        slots[0] = 1; // dirt
        slots[1] = 2; // grass
        slots[2] = 3; // stone
    }

    void Update()
    {
        HandleLook();
        HandleMovement();
        HandleHotbarKeys();
        HighlightBlock();
    }

    // ============================================================
    // 🧭 Movimiento y cámara
    // ============================================================
    private void HandleLook()
    {
        // ⚙️ Usar el valor que viene del Input System (ya está normalizado)
        float mx = lookInput.x * mouseSensitivity;
        float my = lookInput.y * mouseSensitivity;

        // rotación horizontal (yaw)
        transform.Rotate(Vector3.up * mx);

        // rotación vertical (pitch)
        pitch -= my;
        pitch = Mathf.Clamp(pitch, -85f, 85f);

        if (cameraTransform != null)
            cameraTransform.localEulerAngles = new Vector3(pitch, 0f, 0f);
    }

    private void HandleMovement()
    {
        Vector3Int playerChunk = new(
        Mathf.FloorToInt(transform.position.x / world.chunkSize),
        0,
        Mathf.FloorToInt(transform.position.z / world.chunkSize)
        );

        if (!world.IsChunkLoaded(playerChunk))
            return; // no avanzar si el chunk no existe

        Vector3 move = (transform.right * moveInput.x + transform.forward * moveInput.y).normalized;
        float currentSpeed = runPressed ? runSpeed : walkSpeed;

        if (jumpPressed)
        {
            if (cc.isGrounded)
            {
                verticalVelocity = jumpForce;
            }
            else if (!isFlying && waitingForSecondJump && Time.time - lastJumpTime <= doubleTapDelay)
            {
                isFlying = true;
                waitingForSecondJump = false;
                verticalVelocity = 0f;
            }
            else if (!waitingForSecondJump)
            {
                waitingForSecondJump = true;
                lastJumpTime = Time.time;
            }

            jumpPressed = false;
        }

        if (waitingForSecondJump && Time.time - lastJumpTime > doubleTapDelay)
            waitingForSecondJump = false;

        if (isFlying)
        {
            Vector3 flyDir = Vector3.zero;
            if (Keyboard.current.spaceKey.isPressed) flyDir += Vector3.up;
            if (Keyboard.current.leftCtrlKey.isPressed) flyDir += Vector3.down;

            Vector3 moveDir = (transform.forward * moveInput.y + transform.right * moveInput.x + flyDir).normalized;
            cc.Move(moveDir * flightSpeed * Time.deltaTime);
            return;
        }

        if (cc.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -1f;
        else
            verticalVelocity -= gravity * Time.deltaTime;

        Vector3 velocity = move * currentSpeed + Vector3.up * verticalVelocity;
        cc.Move(velocity * Time.deltaTime);
    }

    // ============================================================
    // 🔧 Bloques
    // ============================================================
    private void TryBreakBlock()
    {
        if (world == null || cameraTransform == null) return;

        if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out RaycastHit hit, reachDistance))
        {
            Vector3Int pos = world.GetBlockCoords(hit.point - hit.normal * 0.01f);
            Debug.Log($"🪓 Romper bloque en {pos} (hit {hit.collider?.name})");
            world.SetBlock(pos, 0);
        }
        else
        {
            Debug.Log("🚫 Raycast (Break) no golpeó ningún bloque.");
        }
    }

    private void TryPlaceBlock()
    {
        if (world == null || cameraTransform == null) return;

        if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out RaycastHit hit, reachDistance))
        {
            Vector3Int pos = world.GetBlockCoords(hit.point + hit.normal * 0.01f);
            Debug.Log($"🧱 Colocar bloque {selectedBlock} en {pos} (hit {hit.collider?.name})");
            world.SetBlock(pos, selectedBlock);
        }
        else
        {
            Debug.Log("🚫 Raycast (Place) no golpeó ningún bloque.");
        }
    }

    // ============================================================
    // 🎯 Resaltado de bloque mirado
    // ============================================================
    private void HighlightBlock()
    {
        if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out RaycastHit hit, reachDistance))
        {
            highlightedBlock = world.GetBlockCoords(hit.point - hit.normal * 0.01f);
        }
        else
        {
            highlightedBlock = null;
        }
    }

    void OnDrawGizmos()
    {
        if (cameraTransform != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(cameraTransform.position, cameraTransform.forward * reachDistance);
        }

        if (highlightedBlock != null)
        {
            Gizmos.color = Color.green;
            Vector3 pos = highlightedBlock.Value + Vector3.one * 0.5f;
            Gizmos.DrawWireCube(pos, Vector3.one);
        }
    }

    // ============================================================
    // 🎒 Hotbar
    // ============================================================
    private void HandleHotbarKeys()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        for (int i = 0; i < 9; i++)
        {
            Key key = Key.Digit1 + i;
            if (keyboard[key].wasPressedThisFrame)
            {
                selectedSlot = i;
                selectedBlock = slots[i];
                Debug.Log($"Seleccionado slot {i + 1} → bloque {slots[i]}");
            }
        }
    }
}
