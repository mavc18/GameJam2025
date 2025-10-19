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
    public Transform boquilla;                   // Punto en la punta del tubo
    public Transform contenedorInterno;         // Donde almacenar objetos (si no son micro)

    // ---------------------------
    // Geometría
    // ---------------------------
    [Header("Geometría de succión")]
    public float rangoMax = 3.0f;
    [Tooltip("Ángulo total del cono (grados).")]
    public float anguloCono = 30f;
    public float radioZonaCaptura = 0.35f;

    // ---------------------------
    // Física híbrida
    // ---------------------------
    [Header("Física de succión (spring-damper + asistencia)")]
    [Tooltip("Rigidez base del 'resorte'. (25–60 recomendado)")]
    public float rigidezBase = 40f;

    [Tooltip("Curva que escala la rigidez con la distancia (input: 0=boquilla, 1=rango).")]
    public AnimationCurve rigidezPorDistancia = AnimationCurve.EaseInOut(0, 1, 1, 0.35f);

    [Tooltip("Factor sobre amortiguación crítica (0.6–1.4).")]
    public float factorAmortiguacion = 1.0f;

    [Tooltip("Límite de aceleración para estabilidad.")]
    public float limiteAceleracion = 50f;

    [Header("Asistencia cercana")]
    [Tooltip("Distancia para comenzar asistencia (snap).")]
    public float distanciaSnap = 0.5f;
    [Tooltip("Mezcla con movimiento cinemático (0..1).")]
    public float mezclaSnap = 0.4f;
    [Tooltip("Velocidad de snap (m/s).")]
    public float snapVelocidad = 12f;

    // ---------------------------
    // Captura + capacidad
    // ---------------------------
    [Header("Captura + Capacidad")]
    public int capacidadMax = 10;
    public bool destruirMicroBasura = true;   // fallback si el objeto no tiene material
    public float cooldownCaptura = 0.1f;
    public float masaMaxAspirable = 8f;
    public bool requiereLineaDeVision = true;

    // ---------------------------
    // Capas
    // ---------------------------
    [Header("Capas")]
    public LayerMask capasAspirables;  // "Basura"
    public LayerMask capasBloqueo;     // "Entorno"

    // ---------------------------
    // Eventos
    // ---------------------------
    [Header("Eventos")]
    public IntEvent OnCapturadoMicro;
    public GOEvent  OnCapturadoNormal;
    public UnityEvent OnContenedorLleno;

    // ---------------------------
    // Estado público
    // ---------------------------
    [HideInInspector] public bool aspirando = false;
    public int ContenidoActual => _contenedor.Count;
    public int CapacidadMax   => capacidadMax;
    public int PuntajeMicro   => _microBasuraContador;

    // ---------------------------
    // Internos
    // ---------------------------
    private readonly Collider[] _buffer = new Collider[64];
    private readonly List<GameObject> _contenedor = new List<GameObject>();
    private readonly Dictionary<int, float> _cooldownPorId = new Dictionary<int, float>();
    private int _microBasuraContador = 0;

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

        for (int i = 0; i < n; i++)
        {
            Collider col = _buffer[i];
            if (col == null) continue;

            Rigidbody rb = col.attachedRigidbody;
            if (rb == null || rb.isKinematic) continue;

            // Datos del objetivo (material / overrides)
            VacuumObjetivo vo = col.GetComponent<VacuumObjetivo>();

            // Masa efectiva para succión (cap al filtro de masa aspirable)
            float masaEfectiva = vo ? vo.MasaEfectiva(rb) : rb.mass;
            if (masaEfectiva > masaMaxAspirable) continue;

            // Cooldown por instancia (para no capturar varias veces en el mismo frame)
            int id = rb.GetInstanceID();
            if (_cooldownPorId.TryGetValue(id, out float tReady) && Time.time < tReady) continue;

            Vector3 haciaObj = col.bounds.center - boquilla.position;
            float dist = haciaObj.magnitude;
            if (dist <= 0.0001f) continue;

            // Cono
            float ang = Vector3.Angle(boquilla.forward, haciaObj);
            if (ang > half) continue;

            // Línea de visión
            if (requiereLineaDeVision &&
                Physics.Raycast(boquilla.position, haciaObj.normalized, out RaycastHit hit, dist, capasBloqueo, QueryTriggerInteraction.Ignore))
            {
                continue;
            }

            // --------- SPRING-DAMPER ----------
            Vector3 x = (boquilla.position - rb.worldCenterOfMass);
            Vector3 v = rb.linearVelocity;

            float t = Mathf.Clamp01(dist / rangoMax); // 0=boquilla .. 1=rango
            float kBase = rigidezBase * Mathf.Max(0.05f, rigidezPorDistancia.Evaluate(t));
            float multSuccion = vo ? vo.MultiplicadorSuccionTotal : 1f;
            float k = kBase * multSuccion;

            // amortiguación crítica aprox en función de masaEfectiva (no rb.mass)
            float c = factorAmortiguacion * 2f * Mathf.Sqrt(Mathf.Max(0.0001f, k * masaEfectiva));

            Vector3 fuerzaSpring  = k * x;
            Vector3 fuerzaDamping = -c * v;
            Vector3 fuerza        = fuerzaSpring + fuerzaDamping;

            // Cap de aceleración
            Vector3 acc = fuerza / Mathf.Max(0.0001f, masaEfectiva);
            if (acc.sqrMagnitude > limiteAceleracion * limiteAceleracion)
                acc = acc.normalized * limiteAceleracion;

            rb.AddForce(acc * rb.mass, ForceMode.Force);

            // --------- SNAP CERCANO ----------
            if (dist < distanciaSnap && mezclaSnap > 0f)
            {
                Vector3 target = Vector3.MoveTowards(rb.position, boquilla.position, snapVelocidad * Time.fixedDeltaTime);
                Vector3 delta = (target - rb.position) * mezclaSnap;
                rb.MovePosition(rb.position + delta);
            }

            // --------- CAPTURA ----------
            if (dist < radioZonaCaptura)
            {
                _cooldownPorId[id] = Time.time + cooldownCaptura;

                bool capturable = vo ? vo.EsCapturable : true;
                bool esMicro    = vo ? vo.EsMicroBasura : destruirMicroBasura;

                if (!capturable) continue;

                // FX de captura (si hay material con datos)
                if (vo && vo.material)
                {
                    if (vo.material.vfxCapturaPrefab)
                        Instantiate(vo.material.vfxCapturaPrefab, col.bounds.center, Quaternion.identity);

                    if (vo.material.sfxCaptura)
                        AudioSource.PlayClipAtPoint(vo.material.sfxCaptura, col.bounds.center, vo.material.volumenSfx);
                }

                if (esMicro && destruirMicroBasura)
                {
                    _microBasuraContador++;
                    OnCapturadoMicro?.Invoke(_microBasuraContador);
                    Destroy(rb.gameObject);
                }
                else
                {
                    // almacenar si hay espacio
                    if (_contenedor.Count < capacidadMax)
                    {
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                        rb.isKinematic = true;
                        rb.useGravity = false;

                        rb.gameObject.SetActive(false); // oculto hasta expulsar
                        if (contenedorInterno != null)
                            rb.transform.SetParent(contenedorInterno, worldPositionStays: true);

                        _contenedor.Add(rb.gameObject);
                        OnCapturadoNormal?.Invoke(rb.gameObject);
                    }
                    else
                    {
                        OnContenedorLleno?.Invoke();
                    }
                }
            }
        }

        // limpiar buffer
        for (int i = 0; i < n; i++) _buffer[i] = null;
    }

    // API pública
    public void IniciarSuccion()  { aspirando = true; }
    public void DetenerSuccion()  { aspirando = false; }

    public int GetContenidoActual()     => _contenedor.Count;
    public int GetCapacidadMax()        => capacidadMax;
    public int GetPuntajeMicrobasura()  => _microBasuraContador;

    // (Próximo paso) Expulsar último almacenado:
    // public GameObject ExpulsarUltimo() { ... }
}
