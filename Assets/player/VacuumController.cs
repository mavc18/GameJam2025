using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable] public class IntEvent : UnityEvent<int> {}
[System.Serializable] public class GOEvent  : UnityEvent<GameObject> {}
[System.Serializable] public class FloatEvent : UnityEvent<float> {}
[System.Serializable] public class StringEvent : UnityEvent<string> {}

public class VacuumController : MonoBehaviour
{
    // =========================
    // REFERENCIAS
    // =========================
    [Header("Referencias")]
    public Transform boquilla;                    // Empty en la punta del tubo
    public Transform contenedorInterno;          // Empty para almacenar (si no destruyes)
    public AudioSource motorAudio;               // Loop del motor
    public ParticleSystem vfxSuccion;            // Partículas en la boquilla
    public Camera cam;                           // Cámara FPS (para FOV y temblor)

    // =========================
    // FEEDBACK SENSORIAL
    // =========================
    [Header("Feedback sensorial")]
    [Range(0f, 5f)] public float fovKick = 1.2f;   // escalará según modo
    public float fovRecuperacion = 7f;
    [Header("Temblor (Perlin)")]
    public float temblorAmplitud = 0.015f;         // escalará según modo
    public float temblorFrecuencia = 9f;
    public float temblorSuavizado = 14f;

    private float fovBase;
    private float perlinT;
    private Vector3 camLocalBasePos;

    // =========================
    // GEOMETRÍA BASE
    // =========================
    [Header("Geometría de succión (base)")]
    public float rangoMax = 3.0f;                  // m
    [Tooltip("Ángulo total del cono (grados).")]
    public float anguloCono = 30f;                 // total
    public float radioZonaCaptura = 0.35f;         // m
    [Tooltip("Desplaza el centro de captura unos cm hacia delante.")]
    public float capturaOffset = 0.05f;            // m

    // =========================
    // FÍSICA BASE
    // =========================
    [Header("Física (base)")]
    [Tooltip("Rigidez base del resorte (25–60 recomendado).")]
    public float rigidezBase = 40f;
    [Tooltip("Curva de rigidez según distancia (input: 0=boquilla, 1=rango).")]
    public AnimationCurve rigidezPorDistancia = AnimationCurve.EaseInOut(0, 1, 1, 0.35f);
    [Tooltip("Factor sobre amortiguación crítica (0.6–1.4).")]
    public float factorAmortiguacion = 1.0f;
    [Tooltip("Límite de aceleración (m/s^2) para estabilidad.")]
    public float limiteAceleracion = 50f;

    [Header("Asistencia cercana (snap)")]
    public float distanciaSnap = 0.5f;
    [Range(0f, 1f)] public float mezclaSnap = 0.4f;
    public float snapVelocidad = 12f;

    // =========================
    // CAPTURA + CAPACIDAD
    // =========================
    [Header("Captura + Capacidad")]
    public int capacidadMax = 10;
    public bool destruirMicroBasura = true;
    [Tooltip("Ignora capacidad y destruye TODOS los objetos al capturar.")]
    public bool destruirTodosAlCapturar = false;
    public float cooldownCaptura = 0.1f;
    public float masaMaxAspirable = 8f;
    public bool requiereLineaDeVision = true;

    // =========================
    // CAPAS
    // =========================
    [Header("Capas")]
    public LayerMask capasAspirables;            // "Basura"
    public LayerMask capasBloqueo;               // "Entorno"
    [Tooltip("Capas consideradas como suelo para detectar 'pegado'.")]
    public LayerMask capasSuelo;                 // e.g., Ground, Default

    // =========================
    // EVENTOS
    // =========================
    [Header("Eventos")]
    public IntEvent OnCapturadoMicro;
    public GOEvent  OnCapturadoNormal;
    public UnityEvent OnContenedorLleno;
    public FloatEvent OnEnergiaCambiada;   // 0..1
    public StringEvent OnModoCambiado;     // nombre del modo

    // =========================
    // OPTIMIZACIONES
    // =========================
    [Header("Optimización")]
    public int maxObjetosPorFrame = 16;
    public int maxRaycastsPorFrame = 12;
    public float cacheLoS_vidaSeg = 0.05f;

    private int rrIndex = 0;
    private struct LoSEntry { public bool libre; public float expira; public float ultimaDist; }
    private readonly Dictionary<int, LoSEntry> losCache = new Dictionary<int, LoSEntry>(128);

    // =========================
    // DEBUG / PERF HUD
    // =========================
    [Header("Debug/Perf")]
    public bool debugOverlayGUI = true;
    public bool debugDrawRays = false;
    public bool debugDrawCono = false;

    [Header("Debug suelo (opcional)")]
    public bool debugDrawSuelo = false;          // dibuja rayos de suelo por objeto
    public Color debugSueloHitColor = Color.yellow;
    public Color debugSueloMissColor = new Color(1f, 0.5f, 0f);

    // Métricas por frame
    private int m_overlap, m_procesados, m_conoSkip, m_masaSkip, m_losSkip, m_raycasts, m_aplicoFuerza, m_capturados, m_destruidos, m_almacenados;

    // =========================
    // ESTADO GENERAL
    // =========================
    [HideInInspector] public bool aspirando = false;
    private readonly Collider[] _buffer = new Collider[128];
    private readonly List<Collider> _candidatos = new List<Collider>(128);
    private readonly List<GameObject> _contenedor = new List<GameObject>();
    private readonly Dictionary<int, float> _cooldownPorId = new Dictionary<int, float>();
    private int _microBasuraContador = 0;

    // =========================
    // MODOS DE BOQUILLA
    // =========================
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
    public ParamModo modoAmplio    = new ParamModo { nombre="Amplio",    rangoMax=3.2f, anguloCono=50f, rigidezBase=32f, factorAmortiguacion=1.0f,  fovKick=1.2f, temblorAmplitud=0.012f, consumoPorSegundo=2.0f };
    public ParamModo modoPrecision = new ParamModo { nombre="Precisión", rangoMax=2.5f, anguloCono=22f, rigidezBase=48f, factorAmortiguacion=1.05f, fovKick=0.9f, temblorAmplitud=0.010f, consumoPorSegundo=1.4f };
    public ParamModo modoTurbo     = new ParamModo { nombre="Turbo",     rangoMax=3.6f, anguloCono=32f, rigidezBase=60f, factorAmortiguacion=0.95f, fovKick=1.8f, temblorAmplitud=0.018f, consumoPorSegundo=4.0f };

    private ModoBoquilla _modo = ModoBoquilla.Amplio;
    private ParamModo _p;

    // =========================
    // ENERGÍA / RECARGA
    // =========================
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

    // =========================
    // RESISTENCIA / LUCHA (sistema)
    // =========================
    [Header("Resistencia / Lucha (sistema)")]
    public bool usarResistencia = true;

    [Tooltip("Distancia desde la boquilla donde comienza la lucha (m).")]
    public float distanciaInicioLucha = 1.2f;

    [Tooltip("Velocidad base con la que 'ganas' la lucha a intensidad 1 (u/s).")]
    public float luchaGananciaBasePorSeg = 1.0f;

    [Tooltip("Escala adicional por cercanía (input: 0=boquilla, 1=rango).")]
    public AnimationCurve luchaEscalaPorCercania = AnimationCurve.EaseInOut(0, 1, 1, 0.4f);

    [Tooltip("Margen extra del raycast para detectar suelo bajo el objeto (m).")]
    public float sueloRayExtra = 0.05f;

    // =========================
    // UNITY
    // =========================
    void Start()
    {
        if (cam == null && Camera.main != null) cam = Camera.main;
        if (cam != null)
        {
            fovBase = cam.fieldOfView;
            camLocalBasePos = cam.transform.localPosition;
        }

        // Asegurar Rigidbody kinemático para triggers si usas CharacterController
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

        // Consumo por succión
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

        // 1) Overlap candidatos
        m_overlap = Physics.OverlapSphereNonAlloc(
            boquilla.position, _p.rangoMax, _buffer, capasAspirables, QueryTriggerInteraction.Ignore);

        // Construir lista
        _candidatos.Clear();
        for (int i = 0; i < m_overlap; i++)
        {
            var col = _buffer[i];
            if (col != null) _candidatos.Add(col);
            _buffer[i] = null;
        }
        if (_candidatos.Count == 0) { ActualizarFeedback(0); return; }

        float half = _p.anguloCono * 0.5f;
        int toProcess = Mathf.Min(maxObjetosPorFrame, _candidatos.Count);
        Vector3 capturaCentro = boquilla.position + boquilla.forward * capturaOffset;
        int raycastsDisponibles = maxRaycastsPorFrame;

        // 2) Round-robin
        for (int rrStep = 0; rrStep < toProcess; rrStep++)
        {
            int idx = (rrIndex + rrStep) % _candidatos.Count;
            var col = _candidatos[idx];
            if (col == null) continue;

            Rigidbody rb = col.attachedRigidbody;
            if (rb == null || rb.isKinematic) continue;

            VacuumObjetivo vo = col.GetComponent<VacuumObjetivo>();

            // Masa filtro
            float masaEfectiva = vo ? vo.MasaEfectiva(rb) : rb.mass;
            if (masaEfectiva > masaMaxAspirable) { m_masaSkip++; continue; }

            // Cono
            Vector3 toApprox = col.bounds.center - boquilla.position;
            float ang = Vector3.Angle(boquilla.forward, toApprox);
            if (ang > half) { m_conoSkip++; if (debugDrawCono) DebugDrawRay(boquilla.position, toApprox, new Color(1f,0.6f,0f),0.02f); continue; }

            // Distancia real a esfera de captura
            Vector3 puntoMasCercano = col.ClosestPoint(capturaCentro);
            float dist = Vector3.Distance(puntoMasCercano, capturaCentro);

            // LoS
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
                        if (debugDrawRays) DebugDrawRay(boquilla.position, dirLoS * distLoS, libre ? Color.green : Color.red, 0.02f);
                    }
                }
                if (!libre) continue;
            }

            m_procesados++;

            // 3) Succión spring-damper
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

            rb.AddForce(acc * rb.mass, ForceMode.Force);
            m_aplicoFuerza++;
            if (debugDrawRays) DebugDrawRay(rb.worldCenterOfMass, acc, Color.cyan, 0.02f);

            // 4) Lucha / Resistencia (per-objeto con adherencia en suelo)
            bool enSuelo = EstaEnSuelo(col);
            bool dentroLucha = usarResistencia && (dist <= Mathf.Max(distanciaInicioLucha, radioZonaCaptura));

            if (usarResistencia && vo != null)
            {
                if ((!vo.resistirSoloEnSuelo || enSuelo) && dentroLucha)
                {
                    vo.enLucha = true;

                    float escalaCercania = Mathf.Max(0.05f, luchaEscalaPorCercania.Evaluate(1f - t)); // más cerca, mayor
                    float intensidad = escalaCercania * multSuc / Mathf.Max(0.001f, vo.MultiplicadorResistenciaTotal);

                    // Avance de la lucha
                    vo.progresoActual += luchaGananciaBasePorSeg * intensidad * Time.fixedDeltaTime;
                    vo.progresoActual = Mathf.Clamp(vo.progresoActual, 0f, Mathf.Max(0.0001f, vo.resistencia));

                    // Adhesión hacia abajo (proporcional a lo que falta por vencer)
                    float pct = Mathf.Clamp01(vo.progresoActual / Mathf.Max(0.0001f, vo.resistencia));
                    float aDown = Mathf.Max(0f, vo.adhesionAceleracion) * (1f - pct);
                    if (aDown > 0f) rb.AddForce(Vector3.down * aDown * rb.mass, ForceMode.Force);
                }
                else
                {
                    // No lucha (por distancia o por no estar en suelo)
                    if (vo.enLucha && enSuelo)
                    {
                        // Si aún toca piso pero no está en zona de lucha, decae progreso
                        vo.progresoActual = Mathf.Max(0f, vo.progresoActual - vo.regeneracionResistencia * Time.fixedDeltaTime);
                        if (vo.progresoActual <= 0.0001f) vo.enLucha = false;
                    }
                    else
                    {
                        // Si NO está en suelo => se considera "despegado": lucha vencida (si el objeto lo permite)
                        if (!enSuelo && vo.capturarInmediatoAlDespegar)
                        {
                            vo.progresoActual = Mathf.Max(vo.progresoActual, Mathf.Max(0.0001f, vo.resistencia));
                            vo.enLucha = false;
                        }
                    }
                }
            }

            // 5) Snap cercano
            if (dist < distanciaSnap && mezclaSnap > 0f)
            {
                Vector3 target = Vector3.MoveTowards(rb.position, boquilla.position, snapVelocidad * Time.fixedDeltaTime);
                Vector3 delta = (target - rb.position) * mezclaSnap;
                rb.MovePosition(rb.position + delta);
            }

            // 6) Captura (requiere haber vencido resistencia si está activa)
            if (dist < radioZonaCaptura)
            {
                bool listoPorResistencia = true;
                if (usarResistencia && vo != null)
                    listoPorResistencia = vo.progresoActual >= Mathf.Max(0.0001f, vo.resistencia);

                if (!listoPorResistencia) continue;

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
                        m_almacenados++;
                        OnCapturadoNormal?.Invoke(raizDestruir);
                    }
                    else
                    {
                        OnContenedorLleno?.Invoke();
                    }
                }

                if (vo != null) { vo.progresoActual = 0f; vo.enLucha = false; }
            }
        }

        rrIndex = (rrIndex + toProcess) % Mathf.Max(1, _candidatos.Count);
        ActualizarFeedback(m_aplicoFuerza);
    }

    // ====== Suelo robusto (5 raycasts: centro + 4 esquinas) ======
    private bool EstaEnSuelo(Collider col)
    {
        Bounds b = col.bounds;
        float distancia = b.extents.y + sueloRayExtra;

        Vector3 c = b.center;
        Vector3[] puntos =
        {
            c,
            c + new Vector3( b.extents.x, 0f,  b.extents.z) * 0.7f,
            c + new Vector3(-b.extents.x, 0f,  b.extents.z) * 0.7f,
            c + new Vector3( b.extents.x, 0f, -b.extents.z) * 0.7f,
            c + new Vector3(-b.extents.x, 0f, -b.extents.z) * 0.7f,
        };

        bool hitAlgo = false;
        for (int i = 0; i < puntos.Length; i++)
        {
            if (Physics.Raycast(puntos[i], Vector3.down, out RaycastHit hit, distancia, capasSuelo, QueryTriggerInteraction.Ignore))
            {
                hitAlgo = true;
                if (debugDrawSuelo) Debug.DrawLine(puntos[i], hit.point, debugSueloHitColor, 0.02f, false);
                else if (debugDrawRays) Debug.DrawRay(puntos[i], Vector3.down * distancia, Color.green, 0.02f, false);
                break;
            }
            else
            {
                if (debugDrawSuelo) Debug.DrawRay(puntos[i], Vector3.down * distancia, debugSueloMissColor, 0.02f, false);
            }
        }
        return hitAlgo;
    }

    // =========================
    // ENERGÍA
    // =========================
    private void ModEnergia(float delta)
    {
        float prev = energiaActual;
        energiaActual = Mathf.Clamp(energiaActual + delta, 0f, energiaMax);
        if (!Mathf.Approximately(prev, energiaActual))
            OnEnergiaCambiada?.Invoke(energiaActual / Mathf.Max(0.0001f, energiaMax));
    }

    // =========================
    // FEEDBACK
    // =========================
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
            Vector3 objetivo = camLocalBasePos + new Vector3(nx, ny, 0f) * (_p.temblorAmplitud * carga);
            cam.transform.localPosition = Vector3.Lerp(cam.transform.localPosition, objetivo, Time.deltaTime * temblorSuavizado);

            float targetFOV = fovBase + _p.fovKick * carga;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.deltaTime * fovRecuperacion);
        }
    }

    private void ResetFeedbackIdle() { /* intencionalmente vacío */ }

    // =========================
    // API: SUCCIÓN
    // =========================
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

    // =========================
    // API: MODOS
    // =========================
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

    // =========================
    // TRIGGERS: RECARGA (zonas)
    // =========================
    private void OnTriggerEnter(Collider other)
    {
        var rz = other.GetComponent<RechargeZone>()
             ?? other.GetComponentInParent<RechargeZone>()
             ?? other.GetComponentInChildren<RechargeZone>();

        if (rz != null)
        {
            _zonasRecargaDentro++;
            float tasa = rz.recargaPorSegundo > 0f ? rz.recargaPorSegundo : recargaBasePorSeg;
            _recargaActualPorSeg = Mathf.Max(_recargaActualPorSeg, tasa);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var rz = other.GetComponent<RechargeZone>()
             ?? other.GetComponentInParent<RechargeZone>()
             ?? other.GetComponentInChildren<RechargeZone>();

        if (rz != null)
        {
            _zonasRecargaDentro = Mathf.Max(0, _zonasRecargaDentro - 1);
            if (_zonasRecargaDentro <= 0) _recargaActualPorSeg = 0f;
        }
    }

    // =========================
    // MÉTRICAS / DEBUG
    // =========================
    private void ReiniciarMetricas()
    {
        m_overlap = m_procesados = m_conoSkip = m_masaSkip = m_losSkip = m_raycasts = m_aplicoFuerza = m_capturados = m_destruidos = m_almacenados = 0;
    }

    void OnGUI()
    {
        if (!debugOverlayGUI) return;

        const int pad = 8;
        int y = pad;
        GUI.color = Color.white;
        string modoTxt = _p != null ? _p.nombre : _modo.ToString();
        GUI.Label(new Rect(pad, y, 760, 22), $"Vacuum: {(aspirando ? "ON" : "OFF")}  |  Modo: {modoTxt}  |  Energía: {energiaActual:0}/{energiaMax:0} ({(energiaActual/Mathf.Max(1,energiaMax))*100f:0}%)");
        y += 18;
        GUI.Label(new Rect(pad, y, 760, 22), $"Rango:{_p.rangoMax:0.0}m  Cono:{_p.anguloCono:0}°  CapturaR:{radioZonaCaptura:0.00}  Offset:{capturaOffset:0.00}  Capacidad:{_contenedor.Count}/{capacidadMax}");
        y += 18;
        GUI.Label(new Rect(pad, y, 760, 22), $"Overlap:{m_overlap}  Proc:{m_procesados}/{maxObjetosPorFrame}  Raycasts:{m_raycasts}/{maxRaycastsPorFrame}  LoS-skip:{m_losSkip}  Cono-skip:{m_conoSkip}  Masa-skip:{m_masaSkip}");
        y += 18;
        GUI.Label(new Rect(pad, y, 760, 22), $"Fuerza:{m_aplicoFuerza}  Capt:{m_capturados}  Dest:{m_destruidos}  Store:{m_almacenados}  ZonasRecarga:{_zonasRecargaDentro}  Recarga/s:{(_recargaActualPorSeg>0?_recargaActualPorSeg:recargaBasePorSeg):0.0}");
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

    // Helper para dibujar rays en modo play
    private void DebugDrawRay(Vector3 from, Vector3 vec, Color c, float persist)
    {
        if (!debugDrawRays) return;
        Debug.DrawRay(from, vec, c, persist, false);
    }
}
