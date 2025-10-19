using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable] public class IntEvent : UnityEvent<int> {}
[System.Serializable] public class GOEvent  : UnityEvent<GameObject> {}

public class VacuumController : MonoBehaviour
{
    // ---------------------------
    // Referencias
    // ---------------------------
    [Header("Referencias")]
    public Transform boquilla;                    // Empty en la punta del tubo
    public Transform contenedorInterno;          // Empty para almacenar (si no destruyes)

    // ---------------------------
    // Feedback sensorial
    // ---------------------------
    [Header("Feedback sensorial")]
    public AudioSource motorAudio;               // Loop del motor
    public ParticleSystem vfxSuccion;            // Partículas en la boquilla
    public Camera cam;                           // Cámara FPS

    [Range(0f, 5f)] public float fovKick = 1.2f;
    public float fovRecuperacion = 7f;

    [Header("Temblor (Perlin)")]
    public float temblorAmplitud = 0.015f;       // 0.01–0.02
    public float temblorFrecuencia = 9f;         // 6–12
    public float temblorSuavizado = 14f;         // 10–16

    private float fovBase;
    private float perlinT;
    private Vector3 camLocalBasePos;

    // ---------------------------
    // Geometría de succión
    // ---------------------------
    [Header("Geometría de succión")]
    public float rangoMax = 3.0f;                // m
    [Tooltip("Ángulo total del cono (grados).")]
    public float anguloCono = 30f;               // total (no mitad)
    public float radioZonaCaptura = 0.35f;       // m
    [Tooltip("Desplaza el centro de captura unos cm hacia delante.")]
    public float capturaOffset = 0.05f;          // m, 0.00–0.07

    // ---------------------------
    // Física (spring-damper + asistencia)
    // ---------------------------
    [Header("Física (spring-damper + asistencia)")]
    [Tooltip("Rigidez base del resorte (25–60 recomendado).")]
    public float rigidezBase = 40f;
    [Tooltip("Curva de rigidez según distancia (input: 0=boquilla, 1=rango).")]
    public AnimationCurve rigidezPorDistancia = AnimationCurve.EaseInOut(0, 1, 1, 0.35f);
    [Tooltip("Factor sobre amortiguación crítica (0.6–1.4).")]
    public float factorAmortiguacion = 1.0f;
    [Tooltip("Límite de aceleración (m/s^2) para estabilidad.")]
    public float limiteAceleracion = 50f;

    [Header("Asistencia cercana (snap)")]
    public float distanciaSnap = 0.5f;           // m
    [Range(0f, 1f)] public float mezclaSnap = 0.4f;
    public float snapVelocidad = 12f;            // m/s

    // ---------------------------
    // Captura + capacidad
    // ---------------------------
    [Header("Captura + Capacidad")]
    public int capacidadMax = 10;
    public bool destruirMicroBasura = true;
    [Tooltip("Ignora capacidad y destruye TODOS los objetos al capturar.")]
    public bool destruirTodosAlCapturar = false;
    public float cooldownCaptura = 0.1f;         // s
    public float masaMaxAspirable = 8f;          // kg virtuales
    public bool requiereLineaDeVision = true;

    // ---------------------------
    // Capas
    // ---------------------------
    [Header("Capas")]
    public LayerMask capasAspirables;            // "Basura"
    public LayerMask capasBloqueo;               // "Entorno"

    // ---------------------------
    // Eventos
    // ---------------------------
    [Header("Eventos")]
    public IntEvent OnCapturadoMicro;
    public GOEvent  OnCapturadoNormal;
    public UnityEvent OnContenedorLleno;

    // ---------------------------
    // Estado
    // ---------------------------
    [HideInInspector] public bool aspirando = false;
    private readonly Collider[] _buffer = new Collider[64];
    private readonly List<GameObject> _contenedor = new List<GameObject>();
    private readonly Dictionary<int, float> _cooldownPorId = new Dictionary<int, float>();
    private int _microBasuraContador = 0;

    void Start()
    {
        if (cam == null && Camera.main != null) cam = Camera.main;
        if (cam != null)
        {
            fovBase = cam.fieldOfView;
            camLocalBasePos = cam.transform.localPosition;
        }
    }

    void FixedUpdate()
    {
        if (!aspirando || boquilla == null) return;

        int n = Physics.OverlapSphereNonAlloc(
            boquilla.position,
            rangoMax,
            _buffer,
            capasAspirables,
            QueryTriggerInteraction.Ignore
        );

        float half = anguloCono * 0.5f;
        int objetosAtraidos = 0;

        // Centro de captura adelantado
        Vector3 capturaCentro = boquilla.position + boquilla.forward * capturaOffset;

        for (int i = 0; i < n; i++)
        {
            Collider col = _buffer[i];
            if (col == null) continue;

            Rigidbody rb = col.attachedRigidbody;
            if (rb == null || rb.isKinematic) continue;

            // Datos del objetivo
            VacuumObjetivo vo = col.GetComponent<VacuumObjetivo>();
            float masaEfectiva = vo ? vo.MasaEfectiva(rb) : rb.mass;
            if (masaEfectiva > masaMaxAspirable) continue;

            int id = rb.GetInstanceID();
            if (_cooldownPorId.TryGetValue(id, out float tReady) && Time.time < tReady) continue;

            // Cono por ángulo (usamos el centro aproximado para el test de cono)
            Vector3 toApprox = col.bounds.center - boquilla.position;
            float ang = Vector3.Angle(boquilla.forward, toApprox);
            if (ang > half) continue;

            // Distancia real: punto más cercano del collider a la esfera de captura
            Vector3 puntoMasCercano = col.ClosestPoint(capturaCentro);
            float dist = Vector3.Distance(puntoMasCercano, capturaCentro);

            // Línea de visión (opcional)
            if (requiereLineaDeVision)
            {
                Vector3 dirLoS = (puntoMasCercano - boquilla.position).normalized;
                float distLoS = Vector3.Distance(boquilla.position, puntoMasCercano);
                if (Physics.Raycast(boquilla.position, dirLoS, out RaycastHit hit, distLoS, capasBloqueo, QueryTriggerInteraction.Ignore))
                    continue;
            }

            // --- SUCCIÓN (spring-damper) ---
            objetosAtraidos++;

            // x y v desde el mundo: desplazamiento hacia boquilla
            Vector3 x = (boquilla.position - rb.worldCenterOfMass);
            Vector3 v = rb.linearVelocity;

            float t = Mathf.Clamp01(dist / rangoMax); // 0=boquilla..1=rango
            float kBase = rigidezBase * Mathf.Max(0.05f, rigidezPorDistancia.Evaluate(t));
            float multSuc = vo ? vo.MultiplicadorSuccionTotal : 1f;
            float k = kBase * multSuc;

            float c = factorAmortiguacion * 2f * Mathf.Sqrt(Mathf.Max(0.0001f, k * masaEfectiva));
            Vector3 fuerza = (k * x) - (c * v);

            // cap de aceleración
            Vector3 acc = fuerza / Mathf.Max(0.0001f, masaEfectiva);
            if (acc.sqrMagnitude > limiteAceleracion * limiteAceleracion)
                acc = acc.normalized * limiteAceleracion;

            rb.AddForce(acc * rb.mass, ForceMode.Force);

            // --- SNAP CERCANO ---
            if (dist < distanciaSnap && mezclaSnap > 0f)
            {
                Vector3 target = Vector3.MoveTowards(rb.position, boquilla.position, snapVelocidad * Time.fixedDeltaTime);
                Vector3 delta = (target - rb.position) * mezclaSnap;
                rb.MovePosition(rb.position + delta);
            }

            // --- CAPTURA (usa distancia por punto más cercano) ---
            if (dist < radioZonaCaptura)
            {
                _cooldownPorId[id] = Time.time + cooldownCaptura;

                bool capturable = vo ? vo.EsCapturable : true;
                bool esMicro    = vo ? vo.EsMicroBasura : destruirMicroBasura;
                if (!capturable) continue;

                // SFX/VFX por material
                if (vo && vo.material)
                {
                    if (vo.material.vfxCapturaPrefab)
                        Instantiate(vo.material.vfxCapturaPrefab, puntoMasCercano, Quaternion.identity);
                    if (vo.material.sfxCaptura)
                        AudioSource.PlayClipAtPoint(vo.material.sfxCaptura, puntoMasCercano, vo.material.volumenSfx);
                }

                // Determinar qué GO eliminar/ocultar
                GameObject raizDestruir = (vo && vo.raizParaDestruir != null)
                    ? vo.raizParaDestruir
                    : rb.transform.root.gameObject;

                // Opción 1: destruir todos siempre
                if (destruirTodosAlCapturar)
                {
                    raizDestruir.SetActive(false);
                    Destroy(raizDestruir, 0.1f);
                    continue;
                }

                // Opción 2: micro → destruir; normal → almacenar si hay espacio
                if (esMicro && destruirMicroBasura)
                {
                    _microBasuraContador++;
                    OnCapturadoMicro?.Invoke(_microBasuraContador);

                    raizDestruir.SetActive(false);
                    Destroy(raizDestruir, 0.1f);
                }
                else
                {
                    if (_contenedor.Count < capacidadMax)
                    {
                        // Guardar para expulsar luego (no destruimos)
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                        rb.isKinematic = true;
                        rb.useGravity = false;

                        raizDestruir.SetActive(false);
                        if (contenedorInterno != null)
                            raizDestruir.transform.SetParent(contenedorInterno, worldPositionStays: true);

                        _contenedor.Add(raizDestruir);
                        OnCapturadoNormal?.Invoke(raizDestruir);
                    }
                    else
                    {
                        OnContenedorLleno?.Invoke();
                    }
                }
            }
        }

        // Feedback dinámico (audio/partículas/cámara)
        ActualizarFeedback(objetosAtraidos);
        for (int i = 0; i < n; i++) _buffer[i] = null;
    }

    private void ActualizarFeedback(int objetos)
    {
        float carga = Mathf.Clamp01(objetos / 8f);

        // Audio
        if (motorAudio)
        {
            motorAudio.pitch  = Mathf.Lerp(1f, 1.25f, carga);
            motorAudio.volume = Mathf.Lerp(0.4f, 0.8f,  carga);
        }

        // Partículas
        if (vfxSuccion)
        {
            var emission = vfxSuccion.emission;
            emission.rateOverTime = Mathf.Lerp(10f, 80f, carga);
        }

        // Cámara (Perlin suave + FOV kick)
        if (cam)
        {
            perlinT += Time.deltaTime * temblorFrecuencia * Mathf.Max(0.01f, carga);
            float nx = Mathf.PerlinNoise(perlinT, 0f) * 2f - 1f;
            float ny = Mathf.PerlinNoise(0f, perlinT) * 2f - 1f;
            Vector3 objetivo = camLocalBasePos + new Vector3(nx, ny, 0f) * (temblorAmplitud * carga);
            cam.transform.localPosition = Vector3.Lerp(cam.transform.localPosition, objetivo, Time.deltaTime * temblorSuavizado);

            float targetFOV = fovBase + fovKick * carga;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.deltaTime * fovRecuperacion);
        }
    }

    // ---------------------------
    // API
    // ---------------------------
    public void IniciarSuccion()
    {
        aspirando = true;
        if (motorAudio && !motorAudio.isPlaying) motorAudio.Play();
        if (vfxSuccion) vfxSuccion.Play();
    }

    public void DetenerSuccion()
    {
        aspirando = false;
        if (motorAudio) motorAudio.Stop();
        if (vfxSuccion) vfxSuccion.Stop();
        if (cam)
        {
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fovBase, 0.2f);
            cam.transform.localPosition = Vector3.Lerp(cam.transform.localPosition, camLocalBasePos, 0.4f);
        }
    }

    // ---------------------------
    // Gizmos (muestra rango/cono y esfera de captura)
    // ---------------------------
    void OnDrawGizmosSelected()
    {
        if (boquilla == null) return;

        // rango
        Gizmos.color = new Color(0f, 0.7f, 1f, 0.25f);
        Gizmos.DrawWireSphere(boquilla.position, rangoMax);

        // esfera de captura (con offset)
        Vector3 capturaCentro = boquilla.position + boquilla.forward * capturaOffset;
        Gizmos.color = new Color(1f, 0.8f, 0f, 0.6f);
        Gizmos.DrawWireSphere(capturaCentro, radioZonaCaptura);

        // cono aproximado (4 rayos)
        float half = anguloCono * 0.5f;
        Quaternion q1 = Quaternion.AngleAxis(half, boquilla.up);
        Quaternion q2 = Quaternion.AngleAxis(-half, boquilla.up);
        Quaternion q3 = Quaternion.AngleAxis(half, boquilla.right);
        Quaternion q4 = Quaternion.AngleAxis(-half, boquilla.right);

        Gizmos.color = new Color(0f, 0.7f, 1f, 0.9f);
        Gizmos.DrawLine(boquilla.position, boquilla.position + (q1 * boquilla.forward) * rangoMax);
        Gizmos.DrawLine(boquilla.position, boquilla.position + (q2 * boquilla.forward) * rangoMax);
        Gizmos.DrawLine(boquilla.position, boquilla.position + (q3 * boquilla.forward) * rangoMax);
        Gizmos.DrawLine(boquilla.position, boquilla.position + (q4 * boquilla.forward) * rangoMax);
    }
}
