using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable] public class IntEvent : UnityEvent<int> {}
[System.Serializable] public class GOEvent  : UnityEvent<GameObject> {}
[System.Serializable] public class FloatEvent : UnityEvent<float> {}
[System.Serializable] public class StringEvent : UnityEvent<string> {}

public class VacuumController : MonoBehaviour
{
    [SerializeField] public GameManager manager; // o GameManagerTMP si usas ese

    // ========== Referencias ==========
    [Header("Referencias")]
    public Transform boquilla;           // Empty en la punta del tubo
    public Transform contenedorInterno;  // Empty para almacenar (si no destruyes)
    public AudioSource motorAudio;
    public ParticleSystem vfxSuccion;
    public Camera cam;

    // ========== Feedback ==========
    [Header("Feedback sensorial")]
    [Range(0f, 5f)] public float fovKick = 1.2f;
    public float fovRecuperacion = 7f;
    [Header("Temblor (Perlin)")]
    public float temblorAmplitud = 0.015f;
    public float temblorFrecuencia = 9f;
    public float temblorSuavizado = 14f;

    private float fovBase;
    private float perlinT;
    private Vector3 camLocalBasePos;

    // ========== Geometría ==========
    [Header("Geometría de succión")]
    public float rangoMax = 3.0f;
    [Tooltip("Ángulo total del cono (grados).")]
    public float anguloCono = 30f;
    public float radioZonaCaptura = 0.45f; // un poco mayor para “boca”
    [Tooltip("Desplaza el centro de captura unos cm hacia delante.")]
    public float capturaOffset = 0.05f;

    // ========== Física ==========
    [Header("Física")]
    public float rigidezBase = 40f;
    public AnimationCurve rigidezPorDistancia = AnimationCurve.EaseInOut(0, 1, 1, 0.35f);
    public float factorAmortiguacion = 1.0f;
    public float limiteAceleracion = 50f;

    [Header("Asistencia cercana (snap)")]
    public float distanciaSnap = 0.5f;
    [Range(0f, 1f)] public float mezclaSnap = 0.4f;
    public float snapVelocidad = 12f;

    // ========== Captura/Capacidad ==========
    [Header("Captura + Capacidad")]
    public int capacidadMax = 10;
    public bool destruirMicroBasura = true;
    public bool destruirTodosAlCapturar = false;
    public float cooldownCaptura = 0.1f;
    public float masaMaxAspirable = 100f; // alto para evitar bloqueos mientras pruebas
    public bool requiereLineaDeVision = false; // para pruebas: OFF; luego ON si quieres

    // ========== Capas ==========
    [Header("Capas")]
    public LayerMask capasAspirables;
    public LayerMask capasBloqueo;

    // ========== Eventos ==========
    [Header("Eventos")]
    public IntEvent OnCapturadoMicro;
    public GOEvent  OnCapturadoNormal;
    public UnityEvent OnContenedorLleno;
    public FloatEvent OnEnergiaCambiada;   // 0..1
    public StringEvent OnModoCambiado;     // nombre del modo

    // ========== Optimización ==========
    [Header("Optimización")]
    public int maxObjetosPorFrame = 16;
    public int maxRaycastsPorFrame = 12;
    public float cacheLoS_vidaSeg = 0.05f;

    private int rrIndex = 0;
    private struct LoSEntry { public bool libre; public float expira; public float ultimaDist; }
    private readonly Dictionary<int, LoSEntry> losCache = new Dictionary<int, LoSEntry>(128);

    // ========== Debug ==========
    [Header("Debug/Perf")]
    public bool debugOverlayGUI = true;
    public bool debugDrawRays = false;
    public bool debugDrawCono = false;

    // ========== Estado ==========
    [HideInInspector] public bool aspirando = false;
    private readonly Collider[] _buffer = new Collider[128];
    private readonly List<Collider> _candidatos = new List<Collider>(128);
    private readonly List<GameObject> _contenedor = new List<GameObject>();
    private readonly Dictionary<int, float> _cooldownPorId = new Dictionary<int, float>();
    private int _microBasuraContador = 0;

    // Métricas por frame (incluye m_almacenados para evitar CS0103)
    private int m_overlap, m_procesados, m_conoSkip, m_masaSkip, m_losSkip, m_raycasts, m_aplicoFuerza, m_capturados, m_destruidos, m_almacenados;

    // ========== Modos ==========
    public enum ModoBoquilla { Amplio = 0, Precision = 1, Turbo = 2 }

    [System.Serializable]
    public class ParamModo
    {
        [Header("Nombres/UX")] public string nombre = "Amplio";
        [Header("Geometría")]  public float rangoMax = 3.0f; public float anguloCono = 40f;
        [Header("Física")]     public float rigidezBase = 35f; public float factorAmortiguacion = 1.0f;
        [Header("Feedback")]   public float fovKick = 1.0f;    public float temblorAmplitud = 0.012f;
        [Header("Energía")]    public float consumoPorSegundo = 2.0f;
    }

    [Header("Modos de Boquilla")]
    public ParamModo modoAmplio    = new ParamModo { nombre="Amplio",    rangoMax=3.2f, anguloCono=60f, rigidezBase=32f, factorAmortiguacion=1.0f,  fovKick=1.2f, temblorAmplitud=0.012f, consumoPorSegundo=2.0f };
    public ParamModo modoPrecision = new ParamModo { nombre="Precisión", rangoMax=2.5f, anguloCono=22f, rigidezBase=48f, factorAmortiguacion=1.05f, fovKick=0.9f, temblorAmplitud=0.010f, consumoPorSegundo=1.4f };
    public ParamModo modoTurbo     = new ParamModo { nombre="Turbo",     rangoMax=3.6f, anguloCono=32f, rigidezBase=60f, factorAmortiguacion=0.95f, fovKick=1.8f, temblorAmplitud=0.018f, consumoPorSegundo=4.0f };

    private ModoBoquilla _modo = ModoBoquilla.Amplio;
    private ParamModo _p;

    // ========== Energía ==========
    [Header("Energía")]
    public float energiaMax = 100f;
    public float energiaInicial = 100f;
    public float energiaActual { get; private set; }
    public float energiaMinParaAspirar = 1f;
    public float recargaBasePorSeg = 12f;

    private float _recargaActualPorSeg = 0f;
    private int _zonasRecargaDentro = 0;

    [Header("Triggers (fiabilidad)")]
    public bool asegurarRigidbodyCinematico = true;

    public bool PuedeAspirar => energiaActual > energiaMinParaAspirar;


    void Start()
    {

        if (manager == null)
        {
            manager = FindObjectOfType<GameManager>(); // cambia a GameManagerTMP si ese es tu script
            if (manager == null)
                Debug.LogWarning("[VacuumController] No se encontró GameManager en la escena.");
        }

        if (cam == null && Camera.main != null) cam = Camera.main;
        if (cam != null)
        {
            fovBase = cam.fieldOfView;
            camLocalBasePos = cam.transform.localPosition;
        }

        // Asegurar un RB kinemático en el player si usas CharacterController y triggers
        if (asegurarRigidbodyCinematico)
        {
            var rbSelf = GetComponent<Rigidbody>();
            if (rbSelf == null) rbSelf = gameObject.AddComponent<Rigidbody>();
            rbSelf.isKinematic = true;
            rbSelf.useGravity = false;
        }

        energiaActual = Mathf.Clamp(energiaInicial, 0f, energiaMax);
        AplicarModo(_modo);
        EmitirEventosEstado();
    }

    void Update()
    {
        if (_zonasRecargaDentro > 0)
        {
            float delta = (_recargaActualPorSeg > 0f ? _recargaActualPorSeg : recargaBasePorSeg) * Time.deltaTime;
            ModEnergia(delta);
        }
    }

    void FixedUpdate()
    {
        ReiniciarMetricas();

        if (boquilla == null) { ResetFeedbackIdle(); return; }

        // Consumo / estado
        if (aspirando)
        {
            if (energiaActual <= energiaMinParaAspirar)
            {
                if (motorAudio && motorAudio.isPlaying) motorAudio.Stop();
                if (vfxSuccion && vfxSuccion.isPlaying) vfxSuccion.Stop();
                aspirando = false;
                EmitirEventosEstado();
                ResetFeedbackIdle();
                return;
            }
            ModEnergia(-_p.consumoPorSegundo * Time.fixedDeltaTime);
        }

        if (!aspirando) { ResetFeedbackIdle(); return; }

        // Overlap candidatos
        m_overlap = Physics.OverlapSphereNonAlloc(boquilla.position, _p.rangoMax, _buffer, capasAspirables, QueryTriggerInteraction.Ignore);

        _candidatos.Clear();
        for (int i = 0; i < m_overlap; i++)
        {
            var c = _buffer[i];
            if (c != null) _candidatos.Add(c);
            _buffer[i] = null;
        }
        if (_candidatos.Count == 0) { ActualizarFeedback(0); return; }

        float half = _p.anguloCono * 0.5f;
        int toProcess = Mathf.Min(maxObjetosPorFrame, _candidatos.Count);
        Vector3 capturaCentro = boquilla.position + boquilla.forward * capturaOffset;
        int raycastsDisponibles = maxRaycastsPorFrame;

        for (int rrStep = 0; rrStep < toProcess; rrStep++)
        {
            int idx = (rrIndex + rrStep) % _candidatos.Count;
            var col = _candidatos[idx];
            if (col == null) continue;

            Rigidbody rb = col.attachedRigidbody;
            if (rb == null) continue; // necesita RB

            VacuumObjetivo vo = col.GetComponent<VacuumObjetivo>();

            // Filtro por masa
            float masaEfectiva = vo ? vo.MasaEfectiva(rb) : rb.mass;
            if (masaEfectiva > masaMaxAspirable) { m_masaSkip++; continue; }

            // Filtro por cono
            Vector3 toApprox = col.bounds.center - boquilla.position;
            float ang = Vector3.Angle(boquilla.forward, toApprox);
            if (ang > half) { m_conoSkip++; continue; }

            // Distancia a esfera de captura
            Vector3 puntoMasCercano = col.ClosestPoint(capturaCentro);
            float dist = Vector3.Distance(puntoMasCercano, capturaCentro);

            // Línea de visión (opcional)
            if (requiereLineaDeVision)
            {
                int idCol = col.GetInstanceID();
                bool libre;
                if (losCache.TryGetValue(idCol, out var entry) && Time.time < entry.expira && Mathf.Abs(entry.ultimaDist - dist) < 0.05f)
                {
                    libre = entry.libre;
                }
                else
                {
                    if (raycastsDisponibles <= 0) { libre = false; m_losSkip++; }
                    else
                    {
                        Vector3 dirLoS = (puntoMasCercano - boquilla.position).normalized;
                        float distLoS  = Vector3.Distance(boquilla.position, puntoMasCercano);
                        bool hit = Physics.Raycast(boquilla.position, dirLoS, distLoS, capasBloqueo, QueryTriggerInteraction.Ignore);
                        m_raycasts++; raycastsDisponibles--;
                        libre = !hit;
                        losCache[idCol] = new LoSEntry { libre = libre, expira = Time.time + cacheLoS_vidaSeg, ultimaDist = dist };
                        if (debugDrawRays) Debug.DrawRay(boquilla.position, dirLoS * distLoS, libre ? Color.green : Color.red, 0.02f, false);
                    }
                }
                if (!libre) continue;
            }

            m_procesados++;

            // Spring-damper hacia la boquilla (sin resistencia al suelo)
            float t = Mathf.Clamp01(dist / _p.rangoMax); // 0=boquilla..1=rango
            float rigidezBaseDist = _p.rigidezBase * Mathf.Max(0.05f, rigidezPorDistancia.Evaluate(t));
            float multSuc = vo ? vo.MultiplicadorSuccionTotal : 1f;
            float rigidezEf = rigidezBaseDist * multSuc;

            float amortiguacion = _p.factorAmortiguacion * 2f * Mathf.Sqrt(Mathf.Max(0.0001f, rigidezEf * masaEfectiva));

            Vector3 x = (boquilla.position - rb.worldCenterOfMass);
            Vector3 v = rb.linearVelocity;
            Vector3 fuerza = (rigidezEf * x) - (amortiguacion * v);

            Vector3 acc = fuerza / Mathf.Max(0.0001f, masaEfectiva);
            if (acc.sqrMagnitude > limiteAceleracion * limiteAceleracion)
                acc = acc.normalized * limiteAceleracion;

            // Notificar al puente NavMesh↔Física ANTES de aplicar fuerza
            var link = col.GetComponent<NavAgentSuctionLink>();
            if (link != null) link.NotificarSuccionTick();

            // (Botón rojo de diagnóstico: si aún no se mueve, descomenta)
            // var ag = col.GetComponent<UnityEngine.AI.NavMeshAgent>();
            // if (ag && ag.enabled) { ag.isStopped = true; ag.enabled = false; }
            // if (rb.isKinematic)   { rb.isKinematic = false; rb.useGravity = true; }

            rb.AddForce(acc * rb.mass, ForceMode.Force);
            m_aplicoFuerza++;

            // Snap cercano
            if (dist < distanciaSnap && mezclaSnap > 0f)
            {
                Vector3 target = Vector3.MoveTowards(rb.position, boquilla.position, snapVelocidad * Time.fixedDeltaTime);
                Vector3 delta = (target - rb.position) * mezclaSnap;
                rb.MovePosition(rb.position + delta);
            }

            // Captura
            if (dist < radioZonaCaptura)
            {
                int idRb = rb.GetInstanceID();
                if (_cooldownPorId.TryGetValue(idRb, out float tReady) && Time.time < tReady) continue;
                _cooldownPorId[idRb] = Time.time + cooldownCaptura;

                bool capturable = vo ? vo.EsCapturable : true;
                bool esMicro    = vo ? vo.EsMicroBasura : destruirMicroBasura;
                if (!capturable) continue;

                m_capturados++;

                // SFX/VFX por material
                if (vo && vo.material)
                {
                    if (vo.material.vfxCapturaPrefab) Instantiate(vo.material.vfxCapturaPrefab, puntoMasCercano, Quaternion.identity);
                    if (vo.material.sfxCaptura) AudioSource.PlayClipAtPoint(vo.material.sfxCaptura, puntoMasCercano, vo.material.volumenSfx);
                }

                GameObject raizDestruir = (vo && vo.raizParaDestruir != null) ? vo.raizParaDestruir : rb.transform.root.gameObject;

                if (destruirTodosAlCapturar || (esMicro && destruirMicroBasura))
                {
                    raizDestruir.SetActive(false);
                    Destroy(raizDestruir, 0.1f);
                    m_destruidos++;
                    FindObjectOfType<GameManager>()?.RegistrarBasuraRecolectada();


                    if (!destruirTodosAlCapturar)
                    {
                        _microBasuraContador++;
                        OnCapturadoMicro?.Invoke(_microBasuraContador);
                    }
                }
                else
                {
                    if (_contenedor.Count < capacidadMax)
                    {
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                        rb.isKinematic = true;
                        rb.useGravity = false;

                        raizDestruir.SetActive(false);
                        if (contenedorInterno != null)
                            raizDestruir.transform.SetParent(contenedorInterno, worldPositionStays: true);

                        _contenedor.Add(raizDestruir);
                        OnCapturadoNormal?.Invoke(raizDestruir);
                        m_almacenados++; // <<<<<< MÉTRICA: ahora existe y no da CS0103
                    }
                    else
                    {
                        OnContenedorLleno?.Invoke();
                    }
                }
            }
        }

        rrIndex = (rrIndex + toProcess) % Mathf.Max(1, _candidatos.Count);
        ActualizarFeedback(m_aplicoFuerza);
    }

    public void ModEnergia(float delta)
    {
        float prev = energiaActual;
        energiaActual = Mathf.Clamp(energiaActual + delta, 0f, energiaMax);
        if (!Mathf.Approximately(prev, energiaActual))
            OnEnergiaCambiada?.Invoke(energiaActual / Mathf.Max(0.0001f, energiaMax));
    }

    private void ActualizarFeedback(int objetosConFuerza)
    {
        float carga = Mathf.Clamp01(objetosConFuerza / 8f);

        if (motorAudio)
        {
            motorAudio.pitch  = Mathf.Lerp(1f, 1.25f, carga);
            motorAudio.volume = Mathf.Lerp(0.4f, 0.8f,  carga);
        }
        if (vfxSuccion)
        {
            var emission = vfxSuccion.emission;
            emission.rateOverTime = Mathf.Lerp(10f, 80f, carga);
        }
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

    private void ResetFeedbackIdle() { /* intencionalmente vacío */ }

    public void IniciarSuccion()
    {
        if (energiaActual <= energiaMinParaAspirar) return;
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

    public void SiguienteModo()
    {
        var next = (int)_modo + 1;
        if (next > 2) next = 0;
        CambiarModo((ModoBoquilla)next);
    }

    public void CambiarModo(ModoBoquilla modo)
    {
        _modo = modo;
        AplicarModo(_modo);
        EmitirEventosEstado();
    }

    private void AplicarModo(ModoBoquilla modo)
    {
        switch (modo)
        {
            case ModoBoquilla.Precision: _p = modoPrecision; break;
            case ModoBoquilla.Turbo:     _p = modoTurbo;     break;
            default:                     _p = modoAmplio;    break;
        }
    }

    private void EmitirEventosEstado()
    {
        OnModoCambiado?.Invoke(_p != null ? _p.nombre : _modo.ToString());
        OnEnergiaCambiada?.Invoke(energiaActual / Mathf.Max(0.0001f, energiaMax));
    }

    private void ReiniciarMetricas()
    {
        m_overlap = m_procesados = m_conoSkip = m_masaSkip = m_losSkip = m_raycasts = m_aplicoFuerza = m_capturados = m_destruidos = m_almacenados = 0;
    }

    void OnGUI()
    {
    }

    void OnDrawGizmosSelected()
    {
        if (boquilla == null) return;

        float r = Application.isPlaying ? (_p?.rangoMax ?? rangoMax) : rangoMax;
        float a = Application.isPlaying ? (_p?.anguloCono ?? anguloCono) : anguloCono;

        Gizmos.color = new Color(0f, 0.7f, 1f, 0.25f);
        Gizmos.DrawWireSphere(boquilla.position, r);

        Vector3 capturaCentro = boquilla.position + boquilla.forward * capturaOffset;
        Gizmos.color = new Color(1f, 0.8f, 0f, 0.6f);
        Gizmos.DrawWireSphere(capturaCentro, radioZonaCaptura);

        if (debugDrawCono)
        {
            float half = a * 0.5f;
            Quaternion q1 = Quaternion.AngleAxis(half, boquilla.up);
            Quaternion q2 = Quaternion.AngleAxis(-half, boquilla.up);
            Quaternion q3 = Quaternion.AngleAxis(half, boquilla.right);
            Quaternion q4 = Quaternion.AngleAxis(-half, boquilla.right);
            Gizmos.color = new Color(0f, 0.7f, 1f, 0.9f);
            Gizmos.DrawLine(boquilla.position, boquilla.position + (q1 * boquilla.forward) * r);
            Gizmos.DrawLine(boquilla.position, boquilla.position + (q2 * boquilla.forward) * r);
            Gizmos.DrawLine(boquilla.position, boquilla.position + (q3 * boquilla.forward) * r);
            Gizmos.DrawLine(boquilla.position, boquilla.position + (q4 * boquilla.forward) * r);
        }
    }

    public void ModificarEnergia(float delta, bool detenerAlMaximo = true)
    {
        // asumiendo que tienes una variable energiaActual normalizada (0–1)
        ModEnergia(delta);
        energiaActual += delta;
        if (detenerAlMaximo)
            energiaActual = Mathf.Clamp01(energiaActual);

        OnEnergiaCambiada?.Invoke(energiaActual);
    }

    // Botón UI (PointerDown / OnClick)
    public void OnUIAspirarDown()
    {
        if (!PuedeAspirar) return;   // ← BLOQUEA si energía 0
        IniciarSuccion();
    }

    // Botón UI (PointerUp)
    public void OnUIAspirarUp()
    {
        DetenerSuccion();
    }

    // Devuelve cuántas piezas había almacenadas y las elimina (simula meterlas al contenedor final).
    public int VaciarContenedorInterno()
    {
        int cantidad = 0;

        // _contenedor es tu lista interna de objetos guardados (ya la tienes en el controller)
        for (int i = 0; i < _contenedor.Count; i++)
        {
            var go = _contenedor[i];
            if (!go) continue;
            // estaban inactivos dentro de la aspiradora; al entregar, simplemente los destruimos
            Destroy(go);
            cantidad++;
        }
        _contenedor.Clear();
        return cantidad;
    }

    
    private void NotificarBasuraRecolectada()
    {
        if (manager != null)
        {
            manager.RegistrarBasuraRecolectada();
            // Debug opcional:
            // Debug.Log("[VacuumController] Notifiqué recolección al GameManager.");
        }
        else
        {
            Debug.LogWarning("[VacuumController] Manager es null. Asigna el GameManager en el Inspector o revisa el FindObjectOfType.");
        }
    }


}
