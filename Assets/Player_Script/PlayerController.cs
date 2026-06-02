using UnityEngine;
using UnityEngine.InputSystem;

// 임시 플레이어 컨트롤러 - 팀 합칠 때 교체 예정

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] float moveSpeed = 5.5f;
    [SerializeField] float acceleration = 10f; // 가속
    
    [Header("State")]
    public Vector2 lastMoveDir = Vector2.down; // 무기 발사 방향 등에 활용
    
    Rigidbody2D _rb;
    Vector2 _moveInput;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        GameContext.PlayerTransform = transform;
    }

    void Update()
    {
        GatherInput();
    }

    void FixedUpdate()
    {
        ApplyMovement();
    }

    void GatherInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        float h = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
        float v = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);

        _moveInput = new Vector2(h, v);

        // 입력이 있을 때만 마지막 방향 업데이트해줌
        if (_moveInput.sqrMagnitude > 0)
        {
            _moveInput = _moveInput.normalized;
            lastMoveDir = _moveInput;
        }
    }

    void ApplyMovement()
    {
        Vector2 targetVelocity = _moveInput * moveSpeed;
        _rb.linearVelocity = Vector2.Lerp(_rb.linearVelocity, targetVelocity, Time.fixedDeltaTime * acceleration);

        // 스프라이트 방향 전환 (캐릭터가 왼쪽/오른쪽을 보게 함)
        if (_moveInput.x != 0)
        {
            bool isLeft = _moveInput.x < 0;
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.flipX = isLeft;
        }
    }

    // 나중에 애니메이션 파라미터 전달용 함수
    public bool IsMoving => _moveInput.sqrMagnitude > 0;
}