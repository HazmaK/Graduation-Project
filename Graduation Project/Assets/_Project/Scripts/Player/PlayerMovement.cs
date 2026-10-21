using UnityEngine;
using UnityEngine.InputSystem;

namespace RetroFPS
{
    /// <summary>
    /// Fast-Paced Retro Boomer Shooter Karakter Kontrolcüsü.
    /// Quake/ULTRAKILL fiziği: İvmelenme, Strafe-Jumping, Bunny-Hop, Slide ve I-Frame Dash içerir.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Zemin Hareketi")]
        [SerializeField] private float walkSpeed = 8.0f;
        [SerializeField] private float crouchSpeed = 3.8f;
        [SerializeField] private float groundAcceleration = 18f;
        [SerializeField] private float groundFriction = 8.5f;

        [Header("Hava Hareketi & Bunny Hop (Quake Physics)")]
        [SerializeField] private float airAcceleration = 4.0f;
        [SerializeField] private float airWishSpeed = 3.5f; // Hava yönlendirme hedef hızı
        [SerializeField] private float maxAirSpeed = 11.5f;  // Bunny-hop hız tavanı (Parkur hissini önler, kontrollü hız verir)
        [SerializeField] private float jumpForce = 7.2f;
        [SerializeField] private float gravity = 25f;

        [Header("Slide (Kayma)")]
        [SerializeField] private float slideBoost = 13.0f;
        [SerializeField] private float slideFriction = 3.2f;
        [SerializeField] private float minSlideSpeed = 4.0f;
        [SerializeField] private float slideHeight = 1.0f;
        [SerializeField] private float normalHeight = 2.0f;
        [SerializeField] private float slideCameraY = 0.65f;
        [SerializeField] private float normalCameraY = 1.55f;
        [SerializeField] private float slideDurationMax = 0.85f;

        [Header("Dash (Atılma & I-Frames)")]
        [SerializeField] private float dashSpeed = 30f;
        [SerializeField] private float dashDuration = 0.25f;
        [SerializeField] private int maxDashCharges = 3;
        [SerializeField] private float dashRechargeTime = 1.2f;

        [Header("Referanslar")]
        [SerializeField] private Transform cameraHolder;

        // Bileşenler ve dahili durumlar
        private CharacterController _controller;
        private PlayerLook _look;
        private Vector3 _velocity;
        private Vector2 _inputVector;
        private bool _isGrounded;

        // Slide & Crouch durumları
        private bool _isSliding;
        private bool _isCrouching;
        private float _slideTimer;
        private Vector3 _slideDirection;

        // Dash durumları
        private bool _isDashing;
        private float _dashTimer;
        private Vector3 _dashDirection;
        private int _currentDashCharges;
        private float _dashRechargeTimer;
        private bool _isInvincible;

        // Jump tamponu & Coyote time
        private float _jumpBufferTimer;
        private float _coyoteTimer;
        private const float JumpBufferDuration = 0.15f;
        private const float CoyoteDuration = 0.12f;

        // Dışarıya açık özellikler (Getters)
        public Vector2 InputVector => _inputVector;
        public float CurrentHorizontalSpeed => new Vector3(_velocity.x, 0f, _velocity.z).magnitude;
        public bool IsGrounded => _isGrounded;
        public bool IsSliding => _isSliding;
        public bool IsCrouching => _isCrouching;
        public bool IsDashing => _isDashing;
        public bool IsInvincible => _isInvincible;
        public int CurrentDashCharges => _currentDashCharges;

        private void Awake()
        {
            // Eğer bu script yanlışlıkla bir Camera objesine eklendiyse hemen kendini ve CharacterController'ı yok et
            if (GetComponent<Camera>() != null)
            {
                CharacterController camCC = GetComponent<CharacterController>();
                if (camCC != null) Destroy(camCC);
                Destroy(this);
                return;
            }

            _controller = GetComponent<CharacterController>();
            _look = GetComponent<PlayerLook>();
            _controller.height = normalHeight;
            _controller.center = new Vector3(0f, normalHeight * 0.5f, 0f);

            _currentDashCharges = maxDashCharges;

            // Göz hizasını 1.55m (1.80m manken ile tam göz seviyesi) olarak sabitle
            normalCameraY = 1.55f;
            slideCameraY = 0.65f;

            ResolveCameraHolder();
            ApplyCameraHeight(normalCameraY);
        }

        private void Start()
        {
            // Sahne başlangıcında göz hizasını kesin olarak garantiye al
            ApplyCameraHeight(normalCameraY);
        }

        private void ResolveCameraHolder()
        {
            if (cameraHolder == null)
            {
                Transform holder = transform.Find("CameraHolder");
                if (holder != null)
                {
                    cameraHolder = holder;
                }
                else
                {
                    var cam = GetComponentInChildren<Camera>();
                    if (cam != null)
                    {
                        cameraHolder = (cam.transform.parent != null && cam.transform.parent != transform)
                            ? cam.transform.parent
                            : cam.transform;
                    }
                }
            }
        }

        private void ApplyCameraHeight(float heightY)
        {
            if (cameraHolder != null && cameraHolder != transform)
            {
                Vector3 p = cameraHolder.localPosition;
                p.y = heightY;
                cameraHolder.localPosition = p;
            }
        }

        private void Update()
        {
            ReadInput();
            UpdateRechargeTimers();

            // Zemin kontrolü
            CheckGround();

            // Hareket durum makineleri
            if (_isDashing)
            {
                ExecuteDash();
            }
            else
            {
                if (_isGrounded)
                {
                    _coyoteTimer = CoyoteDuration;

                    if (_isSliding)
                    {
                        ExecuteSlide();
                    }
                    else
                    {
                        ExecuteGroundMove();
                    }
                }
                else
                {
                    _coyoteTimer -= Time.deltaTime;
                    if (_isSliding) StopSlide();
                    ExecuteAirMove();
                }

                // Zıplama denetimi
                HandleJump();

                // Yerçekimi
                ApplyGravity();
            }

            // Nihai hareketi uygula
            _controller.Move(_velocity * Time.deltaTime);

            // Zemin Güvenlik Ağı: Karakterin zemin altına gömülmesini veya düşmesini kesin olarak engeller
            if (transform.position.y < 0f)
            {
                _controller.enabled = false;
                Vector3 safePos = transform.position;
                safePos.y = 0.05f;
                transform.position = safePos;
                _controller.enabled = true;
                _velocity.y = 0f;
            }
        }

        #region Girdi Okuma
        private void ReadInput()
        {
            if (Keyboard.current == null) return;

            // WASD Hareketi
            float x = 0f;
            float z = 0f;
            if (Keyboard.current.wKey.isPressed) z += 1f;
            if (Keyboard.current.sKey.isPressed) z -= 1f;
            if (Keyboard.current.dKey.isPressed) x += 1f;
            if (Keyboard.current.aKey.isPressed) x -= 1f;

            _inputVector = new Vector2(x, z).normalized;

            // Zıplama tamponu
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                _jumpBufferTimer = JumpBufferDuration;
            }
            else
            {
                _jumpBufferTimer -= Time.deltaTime;
            }

            // Dash tetikleme (Sol Shift)
            if (Keyboard.current.leftShiftKey.wasPressedThisFrame)
            {
                TryDash();
            }

            // Crouch ve Slide girdi kontrolleri
            bool crouchHeld = Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.cKey.isPressed;
            bool crouchJustPressed = Keyboard.current.leftCtrlKey.wasPressedThisFrame || Keyboard.current.cKey.wasPressedThisFrame;

            // 1. Slide: YALNIZCA tuşa basıldığı o ilk karede ve yeterli hız varsa tetiklenir
            if (crouchJustPressed && !_isSliding && _isGrounded && CurrentHorizontalSpeed > minSlideSpeed)
            {
                StartSlide();
            }
            // 2. Dururken veya yavaşken basıldıysa normal çömel (Crouch)
            else if (crouchJustPressed && !_isSliding && !_isCrouching && _isGrounded && CurrentHorizontalSpeed <= minSlideSpeed)
            {
                StartCrouch();
            }
            // 3. Normal çömelmede tuş bırakıldığında ayağa kalk (Slide sürerken asla yarıda kesilmez)
            else if (!crouchHeld && _isCrouching && !_isSliding)
            {
                StopCrouch();
            }
        }
        #endregion

        #region Zemin Kontrolü & Sürtünme
        private void CheckGround()
        {
            _isGrounded = _controller.isGrounded;

            // KarakterController zemin kontrolü dik inişlerde bazen titreme yapabilir;
            // Velocity y negatifken küçük bir değerde tutarak zemine yapışmasını sağlarız.
            if (_isGrounded && _velocity.y < 0f)
            {
                _velocity.y = -2f;
            }
        }

        private void ApplyFriction(float frictionFactor)
        {
            Vector3 horizontalVelocity = new Vector3(_velocity.x, 0f, _velocity.z);
            float speed = horizontalVelocity.magnitude;

            if (speed < 0.001f)
            {
                _velocity.x = 0f;
                _velocity.z = 0f;
                return;
            }

            float drop = speed * frictionFactor * Time.deltaTime;
            float newSpeed = Mathf.Max(speed - drop, 0f);

            horizontalVelocity = horizontalVelocity * (newSpeed / speed);
            _velocity.x = horizontalVelocity.x;
            _velocity.z = horizontalVelocity.z;
        }
        #endregion

        #region Zemin & Hava Fiziği (Quake Accelerate)
        private void ExecuteGroundMove()
        {
            Vector3 wishDir = transform.right * _inputVector.x + transform.forward * _inputVector.y;
            wishDir.Normalize();

            ApplyFriction(groundFriction);
            float targetSpeed = _isCrouching ? crouchSpeed : walkSpeed;
            Accelerate(wishDir, targetSpeed, groundAcceleration);
        }

        private void ExecuteAirMove()
        {
            Vector3 wishDir = transform.right * _inputVector.x + transform.forward * _inputVector.y;
            wishDir.Normalize();

            // Quake air strafe ile kontrollü hava yönlendirmesi
            Accelerate(wishDir, airWishSpeed, airAcceleration);

            // Bunny hop hız tavanı: Hızın kontrolsüz parkur oyununa dönüşmesini engeller
            Vector3 horizontalVel = new Vector3(_velocity.x, 0f, _velocity.z);
            if (horizontalVel.magnitude > maxAirSpeed)
            {
                horizontalVel = horizontalVel.normalized * maxAirSpeed;
                _velocity.x = horizontalVel.x;
                _velocity.z = horizontalVel.z;
            }
        }

        private void Accelerate(Vector3 wishDir, float targetSpeed, float accel)
        {
            if (wishDir.sqrMagnitude < 0.001f) return;

            // Mevcut hızın gitmek istenen yöndeki izdüşümü
            float currentSpeedInWishDir = Vector3.Dot(_velocity, wishDir);
            float speedToAdd = targetSpeed - currentSpeedInWishDir;

            if (speedToAdd <= 0f) return;

            float accelSpeed = accel * targetSpeed * Time.deltaTime;
            accelSpeed = Mathf.Min(accelSpeed, speedToAdd);

            _velocity += wishDir * accelSpeed;
        }

        private void ApplyGravity()
        {
            _velocity.y -= gravity * Time.deltaTime;
        }
        #endregion

        #region Zıplama & Bunny Hop
        private void HandleJump()
        {
            // Slide sürerken zıplanamaz; slide süresi dolana kadar kilitlidir ve yarıda kesilemez
            if (_isSliding) return;

            // Zıplama şartı: Tampon süresi içinde basıldıysa ve (yerdeysek veya coyote time aktifse)
            if (_jumpBufferTimer > 0f && (_isGrounded || _coyoteTimer > 0f))
            {
                _jumpBufferTimer = 0f;
                _coyoteTimer = 0f;
                _velocity.y = jumpForce;
            }
        }
        #endregion

        #region Slide (Kayma) & Crouch (Çömelme) Mekaniği
        private void StartSlide()
        {
            _isSliding = true;
            _isCrouching = false;
            _slideTimer = slideDurationMax;

            // Kayma yönü mevcut yatay hareket yönüdür
            Vector3 hVel = new Vector3(_velocity.x, 0f, _velocity.z);
            _slideDirection = hVel.sqrMagnitude > 1f ? hVel.normalized : transform.forward;

            // Hız patlaması ekle
            _velocity = _slideDirection * Mathf.Max(hVel.magnitude, slideBoost);

            // Kapsülü ve kamerayı küçült
            SetPlayerHeight(slideHeight, slideCameraY);
        }

        private void ExecuteSlide()
        {
            _slideTimer -= Time.deltaTime;

            // Sürtünme ile hızın dengeli sönümlenmesi
            ApplyFriction(slideFriction);

            // Slide süresi tamamen dolana kadar karakter ayağa kalkamaz
            if (_slideTimer <= 0f)
            {
                bool stillHoldingCrouch = Keyboard.current != null &&
                    (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.cKey.isPressed);
                StopSlide(stillHoldingCrouch);
            }
        }

        private void StopSlide(bool transitionToCrouch = false)
        {
            if (!_isSliding) return;
            _isSliding = false;

            if (transitionToCrouch)
            {
                _isCrouching = true;
                SetPlayerHeight(slideHeight, slideCameraY);
            }
            else
            {
                _isCrouching = false;
                SetPlayerHeight(normalHeight, normalCameraY);
            }
        }

        private void StartCrouch()
        {
            _isCrouching = true;
            SetPlayerHeight(slideHeight, slideCameraY);
        }

        private void StopCrouch()
        {
            _isCrouching = false;
            SetPlayerHeight(normalHeight, normalCameraY);
        }

        private void SetPlayerHeight(float height, float cameraY)
        {
            _controller.height = height;
            _controller.center = new Vector3(0f, height * 0.5f, 0f);

            if (cameraHolder != null && cameraHolder != transform)
            {
                Vector3 camPos = cameraHolder.localPosition;
                camPos.y = cameraY;
                cameraHolder.localPosition = camPos;
            }
        }
        #endregion

        #region Dash (Atılma & I-Frames)
        private void TryDash()
        {
            if (_currentDashCharges <= 0 || _isDashing) return;

            _currentDashCharges--;
            _isDashing = true;
            _isInvincible = true; // Hasar almama penceresi (I-Frame)
            _dashTimer = dashDuration;

            // Dash yönü: Girdi varsa o yöne, yoksa baktığı yöne
            Vector3 inputDir = transform.right * _inputVector.x + transform.forward * _inputVector.y;
            _dashDirection = inputDir.sqrMagnitude > 0.01f ? inputDir.normalized : transform.forward;

            // Dash sırasında dikey yerçekimini sıfırla, ani patlama hızını ver
            _velocity = _dashDirection * dashSpeed;
            _velocity.y = 0f;

            // Kameraya hissedilir ve tatmin edici FOV şoku ver (Warp darbesi)
            if (_look != null)
            {
                _look.DoFovKick(16f);
            }
        }

        private void ExecuteDash()
        {
            _dashTimer -= Time.deltaTime;

            // Dinamik Easing Out: İlk anlarda yüksek hız korunarak belirgin bir mesafe (~7 metre) kat edilir
            float t = 1f - Mathf.Clamp01(_dashTimer / dashDuration);
            float currentSpeed = Mathf.Lerp(dashSpeed, walkSpeed * 1.25f, Mathf.Pow(t, 1.5f));
            _velocity = _dashDirection * currentSpeed;

            if (_dashTimer <= 0f)
            {
                _isDashing = false;
                _isInvincible = false;
                _velocity = _dashDirection * walkSpeed;
            }
        }

        private void UpdateRechargeTimers()
        {
            if (_currentDashCharges < maxDashCharges)
            {
                _dashRechargeTimer += Time.deltaTime;
                if (_dashRechargeTimer >= dashRechargeTime)
                {
                    _dashRechargeTimer = 0f;
                    _currentDashCharges++;
                }
            }
            else
            {
                _dashRechargeTimer = 0f;
            }
        }
        #endregion
    }
}
