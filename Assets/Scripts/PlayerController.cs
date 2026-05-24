using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerController : MonoBehaviour
{
    [Header("Stats")]
    public float speed = 6f;
    public float rotateSpeed = 0.15f;
    public float bulletForce = 20f;
    public float acceleration = 12f;

    [Header("Refs")]
    public GameObject bulletPrefab;
    public Transform firePoint;

    [Header("Camera FPS")]
    public Transform playerCamera;
    public float verticalSensitivity = 0.15f;

    private CharacterController _cc;
    private Vector2 _moveInput;
    private Vector2 _lookInput;
    private Vector3 _horizontalVelocity;
    private float _verticalVelocity;
    private float _cameraPitch;

    void Start()
    {
        _cc = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnMove(InputValue value) => _moveInput = value.Get<Vector2>();
    void OnLook(InputValue value) => _lookInput = value.Get<Vector2>();
    void OnAttack() => Shoot();

    void Update()
    {
        if (_cc.isGrounded)
            _verticalVelocity = -2f;
        else
            _verticalVelocity += Physics.gravity.y * Time.deltaTime;

        Vector3 target = (transform.forward * _moveInput.y + transform.right * _moveInput.x) * speed;
        _horizontalVelocity = Vector3.Lerp(_horizontalVelocity, target, acceleration * Time.deltaTime);

        Vector3 finalMove = _horizontalVelocity + Vector3.up * _verticalVelocity;
        _cc.Move(finalMove * Time.deltaTime);

        transform.Rotate(0f, _lookInput.x * rotateSpeed, 0f);

        if (playerCamera != null)
        {
            _cameraPitch -= _lookInput.y * verticalSensitivity;
            _cameraPitch = Mathf.Clamp(_cameraPitch, -80f, 80f);
            playerCamera.localEulerAngles = new Vector3(_cameraPitch, 0f, 0f);
        }
    }

    void Shoot()
    {
        if (bulletPrefab == null || firePoint == null) return;
        var b = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        b.tag = "Bullet";
        var rb = b.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;
            rb.linearVelocity = firePoint.forward * bulletForce;
        }
        Destroy(b, 3f);

        GameManager.Instance.OnPlayerShoot(
            Vector3Int.RoundToInt(transform.position),
            firePoint.forward, "TIR", 1);
    }

    void OnDestroy()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void OnEnable()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}