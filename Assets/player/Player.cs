using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerControllerFPS_DualJoystick : MonoBehaviour
{
    // =========================
    // MOVIMIENTO
    // =========================
    [Header("Movimiento")]
    public float velocidadMovimiento = 6f;
    public float gravedad = -9.81f;
    public float alturaSalto = 1.5f;
    public float zonaMuertaMovimiento = 0.05f;

    // =========================
    // CÁMARA / MIRAR (joystick derecho)
    // =========================
    [Header("Cámara / Mirar (joystick derecho)")]
    public Transform transformCamara;
    public Vector2 limitesPitch = new Vector2(-80f, 80f);
    public float sensibilidadMirarGradosPorSeg = 240f;
    public float zonaMuertaMirar = 0.05f;

    // =========================
    // JOYSTICKS (UI)
    // =========================
    [Header("Joysticks (UI)")]
    public Joystick joystickMovimiento; // Izquierdo
    public Joystick joystickMirar;      // Derecho

    // =========================
    // HEAD-BOB (sensación de paso)
    // =========================
    [Header("Head-Bob (sensación de paso)")]
    public float bobAltura = 0.03f;
    public float bobFrecuencia = 9f;
    public float bobLateral = 0.015f;
    public float suavizadoCamara = 10f;

    // =========================
    // ASPIRADORA (solo rotación / sway)
    // =========================
    [Header("Aspiradora (rotación / sway)")]
    public Transform transformHerramienta;       // Hija de la cámara (no se mueve de posición)
    public float herramientaSway = 0.15f;        // Magnitud (radianes aprox.) por input de movimiento
    public float herramientaSwayMaxGrados = 6f;  // Límite de inclinación (grados)
    public float herramientaSuavizado = 12f;     // Suavizado de la rotación
    public float herramientaLeanGiroGrados = 3f; // Micro roll por giro (inverso a la cámara)

    // =========================
    // ASPIRAR (velocidad + zoom/tilt)
    // =========================
    [Header("Aspirar (velocidad + zoom/tilt)")]
    [SerializeField] public bool aspirando = false; 
    public float multiplicadorVelocidadAspirando = 0.6f;

    [Tooltip("Cuántos grados reduce el FOV al aspirar (zoom).")]
    public float aspirarZoomFOV = 6f;
    [Tooltip("Tiempo que tarda en aplicar el zoom al aspirar (lento).")]
    public float aspirarTiempoZoom = 0.6f;
    [Tooltip("Inclinación hacia adelante de la cámara al aspirar (grados).")]
    public float aspirarInclinacionGrados = 3f;
    [Tooltip("Tiempo que tarda en inclinar/volver.")]
    public float aspirarTiempoInclinacion = 0.6f;

    // =========================
    // FEEDBACK DE GIRO (joystick derecho)
    // =========================
    [Header("Feedback de giro (joystick derecho)")]
    public float giroLeanGrados = 4f;       // Roll de cámara
    public float giroOffsetLateral = 0.03f; // Desplazamiento lateral de cámara (m)
    public float giroSuavizado = 10f;
    public bool usarGiroFOVKick = true;
    public float giroFOVKick = 3f;
    public float giroFOVSuavizado = 6f;

    // =========================
    // INTERNOS
    // =========================
    private CharacterController controller;
    private Vector3 velocidad;      // Y para gravedad/salto
    private float pitch;            // Rotación vertical acumulada

    private Vector3 camaraPosLocalBase;
    private Quaternion herramientaRotBase;
    private Camera camara;
    private float fovBase;

    private float bobTimer;
    private float giroOffsetX;      // Offset lateral por giro (se suma al head-bob)

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (transformCamara == null && Camera.main != null)
            transformCamara = Camera.main.transform;

        if (transformCamara != null)
        {
            camaraPosLocalBase = transformCamara.localPosition;

            float x = transformCamara.localEulerAngles.x;
            pitch = (x > 180f) ? x - 360f : x;
            pitch = Mathf.Clamp(pitch, limitesPitch.x, limitesPitch.y);

            camara = transformCamara.GetComponent<Camera>();
            if (camara != null) fovBase = camara.fieldOfView;
        }

        if (transformHerramienta != null)
        {
            herramientaRotBase = transformHerramienta.localRotation; // Solo guardamos ROTACIÓN
        }
    }

    void Update()
    {
        AplicarMirarDesdeJoystick();

        Vector2 inputMovimiento = LeerInputMovimiento();

        float lookX = (joystickMirar && Mathf.Abs(joystickMirar.Horizontal) > zonaMuertaMirar) ? joystickMirar.Horizontal : 0f;
        AplicarFeedbackGiro(lookX);

        AplicarMovimientoDesdeJoystick(inputMovimiento);
        AplicarHeadBob(inputMovimiento);
        AplicarSwayHerramienta(inputMovimiento, lookX);
        AplicarSaltoYGravedad();
    }

    // ========== MIRAR ==========
    private void AplicarMirarDesdeJoystick()
    {
        if (joystickMirar == null || transformCamara == null) return;

        float lx = Mathf.Abs(joystickMirar.Horizontal) > zonaMuertaMirar ? joystickMirar.Horizontal : 0f;
        float ly = Mathf.Abs(joystickMirar.Vertical)   > zonaMuertaMirar ? joystickMirar.Vertical   : 0f;

        float yawDelta   = lx * sensibilidadMirarGradosPorSeg * Time.deltaTime;
        float pitchDelta = ly * sensibilidadMirarGradosPorSeg * Time.deltaTime;

        transform.Rotate(0f, yawDelta, 0f, Space.Self);

        pitch -= pitchDelta;
        pitch = Mathf.Clamp(pitch, limitesPitch.x, limitesPitch.y);

        Vector3 camEuler = transformCamara.localEulerAngles;
        camEuler.x = pitch;
        camEuler.y = 0f;
        camEuler.z = 0f; // el roll se añade en el feedback de giro
        transformCamara.localEulerAngles = camEuler;
    }

    // ========== INPUT MOVIMIENTO ==========
    private Vector2 LeerInputMovimiento()
    {
        float mx = 0f, my = 0f;
        if (joystickMovimiento != null)
        {
            mx = Mathf.Abs(joystickMovimiento.Horizontal) > zonaMuertaMovimiento ? joystickMovimiento.Horizontal : 0f;
            my = Mathf.Abs(joystickMovimiento.Vertical)   > zonaMuertaMovimiento ? joystickMovimiento.Vertical   : 0f;
        }
        return new Vector2(mx, my);
    }

    // ========== MOVER ==========
    private void AplicarMovimientoDesdeJoystick(Vector2 input)
    {
        Vector3 camForward = transformCamara ? Vector3.Scale(transformCamara.forward, new Vector3(1, 0, 1)).normalized : Vector3.forward;
        Vector3 camRight   = transformCamara ? Vector3.Scale(transformCamara.right,   new Vector3(1, 0, 1)).normalized : Vector3.right;

        Vector3 dir = camForward * (input.y) + camRight * (input.x);
        if (dir.sqrMagnitude > 1f) dir.Normalize();

        float velActual = velocidadMovimiento * (aspirando ? multiplicadorVelocidadAspirando : 1f);
        controller.Move(dir * velActual * Time.deltaTime);
    }

    // ========== HEAD-BOB CÁMARA ==========
    private void AplicarHeadBob(Vector2 input)
    {
        if (transformCamara == null) return;

        bool enSuelo = controller.isGrounded;
        float mov = Mathf.Clamp01(input.magnitude);

        if (enSuelo && mov > 0.01f)
            bobTimer += Time.deltaTime * Mathf.Lerp(bobFrecuencia * 0.8f, bobFrecuencia * 1.2f, mov);
        else
            bobTimer = Mathf.MoveTowards(bobTimer, 0f, Time.deltaTime * bobFrecuencia);

        float y = Mathf.Sin(bobTimer * Mathf.PI * 2f) * bobAltura * mov;
        float x = Mathf.Cos(bobTimer * Mathf.PI * 2f) * bobLateral * mov;

        float xConGiro = camaraPosLocalBase.x + x + giroOffsetX;

        Vector3 objetivo = new Vector3(xConGiro, camaraPosLocalBase.y + y, camaraPosLocalBase.z);
        transformCamara.localPosition = Vector3.Lerp(transformCamara.localPosition, objetivo, Time.deltaTime * suavizadoCamara);
    }

    // ========== SWAY HERRAMIENTA (SOLO ROTACIÓN) ==========
    private void AplicarSwayHerramienta(Vector2 inputMov, float lookX)
    {
        if (transformHerramienta == null) return;

        // Sway por movimiento
        float swayX = -inputMov.x * herramientaSway; // roll (Z) por strafe
        float swayY =  inputMov.y * herramientaSway; // pitch (X) por avance/retroceso

        // Lean inverso por giro (sensación de masa)
        float leanGiroRad = Mathf.Deg2Rad * (herramientaLeanGiroGrados * lookX);
        swayX += -leanGiroRad;

        float maxRad = Mathf.Deg2Rad * herramientaSwayMaxGrados;
        swayX = Mathf.Clamp(swayX, -maxRad, maxRad);
        swayY = Mathf.Clamp(swayY, -maxRad, maxRad);

        Quaternion rotObjetivo =
            Quaternion.AngleAxis(Mathf.Rad2Deg * swayY, Vector3.right) *
            Quaternion.AngleAxis(Mathf.Rad2Deg * swayX, Vector3.forward) *
            herramientaRotBase;

        transformHerramienta.localRotation = Quaternion.Slerp(transformHerramienta.localRotation, rotObjetivo, Time.deltaTime * herramientaSuavizado);

        // IMPORTANTE: no tocamos transformHerramienta.localPosition
    }

    // ========== FEEDBACK DE GIRO ==========
    private void AplicarFeedbackGiro(float lookX)
    {
        if (transformCamara == null) return;

        float rollObjetivo = -lookX * giroLeanGrados;
        float offsetXObj   =  lookX * giroOffsetLateral;

        giroOffsetX = Mathf.Lerp(giroOffsetX, offsetXObj, Time.deltaTime * giroSuavizado);

        Vector3 e = transformCamara.localEulerAngles;
        float rollActual = (e.z > 180f) ? e.z - 360f : e.z;
        float nuevoRoll = Mathf.Lerp(rollActual, rollObjetivo, Time.deltaTime * giroSuavizado);
        transformCamara.localEulerAngles = new Vector3(pitch, 0f, nuevoRoll);

        if (camara != null && usarGiroFOVKick)
        {
            float fovObjetivo = (Mathf.Abs(lookX) > 0.001f) ? fovBase + Mathf.Abs(lookX) * giroFOVKick : fovBase;
            camara.fieldOfView = Mathf.Lerp(camara.fieldOfView, fovObjetivo, Time.deltaTime * giroFOVSuavizado);
        }
    }

    // ========== SALTO / GRAVEDAD ==========
    private void AplicarSaltoYGravedad()
    {
        bool enSuelo = controller.isGrounded;

        if (enSuelo && velocidad.y < 0f)
            velocidad.y = -2f;

        velocidad.y += gravedad * Time.deltaTime;
        controller.Move(velocidad * Time.deltaTime);
    }

    public void OnBotonSaltar()
    {
        if (controller.isGrounded)
            velocidad.y = Mathf.Sqrt(alturaSalto * -2f * gravedad);
    }

    // ========== BOTÓN ASPIRAR (UI) ==========
    public void OnBotonAspirarPresionado()
    {
        if (aspirando) return;
        aspirando = true;

        if (camara != null)
        {
            StopAllCoroutines();
            StartCoroutine(EfectoVisualAspirar(true)); // Zoom lento y se queda en el máximo
        }
    }

    public void OnBotonAspirarSoltado()
    {
        if (!aspirando) return;
        aspirando = false;

        if (camara != null)
        {
            StopAllCoroutines();
            StartCoroutine(EfectoVisualAspirar(false)); // Vuelve al FOV base
        }
    }

    // ========== CORUTINA ZOOM/INCLINACIÓN (ASPIRAR) ==========
    private System.Collections.IEnumerator EfectoVisualAspirar(bool activando)
    {
        float durZoom  = activando ? aspirarTiempoZoom : aspirarTiempoZoom * 0.6f;
        float durTilt  = activando ? aspirarTiempoInclinacion : aspirarTiempoInclinacion * 0.6f;

        float tiempo = 0f;

        float fovInicio = camara.fieldOfView;
        float fovObjetivo = activando ? fovBase - aspirarZoomFOV : fovBase;

        Vector3 rotInicio = transformCamara.localEulerAngles;
        Vector3 rotObjetivo = rotInicio + (activando ? new Vector3(aspirarInclinacionGrados, 0f, 0f) : new Vector3(-aspirarInclinacionGrados, 0f, 0f));

        while (tiempo < Mathf.Max(durZoom, durTilt))
        {
            tiempo += Time.deltaTime;

            float tZoom = Mathf.Clamp01(tiempo / durZoom);
            float tTilt = Mathf.Clamp01(tiempo / durTilt);

            // Ease-out
            tZoom = 1f - Mathf.Pow(1f - tZoom, 3f);
            tTilt = 1f - Mathf.Pow(1f - tTilt, 3f);

            camara.fieldOfView = Mathf.Lerp(fovInicio, fovObjetivo, tZoom);
            Vector3 rotActual = Vector3.Lerp(rotInicio, rotObjetivo, tTilt);

            // Mantiene roll de giro y pitch real
            transformCamara.localEulerAngles = new Vector3(rotActual.x, 0f, transformCamara.localEulerAngles.z);

            yield return null;
        }

        camara.fieldOfView = fovObjetivo; // se queda en el máximo mientras sigas aspirando
    }
}
