using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable] public class IntEvent : UnityEvent<int> {}
[System.Serializable] public class GOEvent  : UnityEvent<GameObject> {}

public class VacuumController : MonoBehaviour
{
    [Header("Referencias")]
    public Transform boquilla;
    public Transform contenedorInterno;

    [Header("Feedback sensorial")]
    public AudioSource motorAudio;
    public ParticleSystem vfxSuccion;
    public Camera cam;
    [Range(0f, 5f)] public float temblorIntensidad = 0.1f;
    [Range(0f, 5f)] public float fovKick = 1.5f;
    public float fovRecuperacion = 4f;

    // Temblor Perlin (suave)
    [Header("Temblor (Perlin)")]
    public float temblorAmplitud = 0.02f;
    public float temblorFrecuencia = 9f;
    public float temblorSuavizado = 12f;

    private float fovBase;
    private float temblorT;
    private Vector3 camLocalBasePos;

    [Header("Geometría de succión")]
    public float rangoMax = 3.0f;
    public float anguloCono = 30f;
    public float radioZonaCaptura = 0.35f;

    [Header("Física (spring-damper + asistencia)")]
    public float rigidezBase = 40f;
    public AnimationCurve rigidezPorDistancia = AnimationCurve.EaseInOut(0, 1, 1, 0.35f);
    public float factorAmortiguacion = 1.0f;
    public float limiteAceleracion = 50f;
    public float distanciaSnap = 0.5f;
    public float mezclaSnap = 0.4f;
    public float snapVelocidad = 12f;

    [Header("Captura + Capacidad")]
    public int capacidadMax = 10;
    public bool destruirMicroBasura = true;
    [Tooltip("Ignora capacidad y destruye TODOS los objetos al capturar.")]
    public bool destruirTodosAlCapturar = true;   // ← activa esto si quieres que TODO desaparezca
    public float cooldownCaptura = 0.1f;
    public float masaMaxAspirable = 8f;
    public bool requiereLineaDeVision = true;

    [Header("Capas")]
    public LayerMask capasAspirables;
    public LayerMask capasBloqueo;

    [Header("Eventos")]
    public IntEvent OnCapturadoMicro;
    public GOEvent  OnCapturadoNormal;
    public UnityEvent OnContenedorLleno;

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
            boquilla.position, rangoMax, _buffer, capasAspirables, QueryTriggerInteraction.Ignore);

        float half = anguloCono * 0.5f;
        int objetosAtraidos = 0;

        for (int i = 0; i < n; i++)
        {
            Collider col = _buffer[i];
            if (col == null) continue;

            Rigidbody rb = col.attachedRigidbody;
            if (rb == null || rb.isKinematic) continue;

            VacuumObjetivo vo = col.GetComponent<VacuumObjetivo>();

            float masaEfectiva = vo ? vo.MasaEfectiva(rb) : rb.mass;
            if (masaEfectiva > masaMaxAspirable) continue;

            int id = rb.GetInstanceID();
            if (_cooldownPorId.TryGetValue(id, out float tReady) && Time.time < tReady) continue;

            Vector3 haciaObj = col.bounds.center - boquilla.position;
            float dist = haciaObj.magnitude;
            if (dist <= 0.0001f) continue;

            float ang = Vector3.Angle(boquilla.forward, haciaObj);
            if (ang > half) continue;

            if (requiereLineaDeVision &&
                Physics.Raycast(boquilla.position, haciaObj.normalized, out RaycastHit hit, dist, capasBloqueo, QueryTriggerInteraction.Ignore))
                continue;

            // --- succión física ---
            objetosAtraidos++;
            float t = Mathf.Clamp01(dist / rangoMax);
            float k = rigidezBase * Mathf.Max(0.05f, rigidezPorDistancia.Evaluate(t));
            if (vo) k *= vo.MultiplicadorSuccionTotal;
            float c = factorAmortiguacion * 2f * Mathf.Sqrt(Mathf.Max(0.0001f, k * masaEfectiva));

            Vector3 x = (boquilla.position - rb.worldCenterOfMass);
            Vector3 v = rb.linearVelocity;
            Vector3 fuerza = (k * x) - (c * v);
            Vector3 acc = fuerza / Mathf.Max(0.0001f, masaEfectiva);
            if (acc.sqrMagnitude > limiteAceleracion * limiteAceleracion)
                acc = acc.normalized * limiteAceleracion;

            rb.AddForce(acc * rb.mass, ForceMode.Force);

            // --- snap cercano ---
            if (dist < distanciaSnap && mezclaSnap > 0f)
            {
                Vector3 target = Vector3.MoveTowards(rb.position, boquilla.position, snapVelocidad * Time.fixedDeltaTime);
                Vector3 delta = (target - rb.position) * mezclaSnap;
                rb.MovePosition(rb.position + delta);
            }

            // --- captura ---
            if (dist < radioZonaCaptura)
            {
                _cooldownPorId[id] = Time.time + cooldownCaptura;

                bool capturable = vo ? vo.EsCapturable : true;
                bool esMicro    = vo ? vo.EsMicroBasura : destruirMicroBasura;
                if (!capturable) continue;

                // decide qué GO destruir / ocultar
                GameObject raizDestruir = (vo && vo.raizParaDestruir != null)
                    ? vo.raizParaDestruir
                    : rb.transform.root.gameObject; // fallback seguro

                // FX de captura de material (si están)
                if (vo && vo.material)
                {
                    if (vo.material.vfxCapturaPrefab)
                        Instantiate(vo.material.vfxCapturaPrefab, col.bounds.center, Quaternion.identity);
                    if (vo.material.sfxCaptura)
                        AudioSource.PlayClipAtPoint(vo.material.sfxCaptura, col.bounds.center, vo.material.volumenSfx);
                }

                // 1) si quieres que TODO desaparezca al capturar:
                if (destruirTodosAlCapturar)
                {
                    raizDestruir.SetActive(false); // oculta inmediatamente
                    Destroy(raizDestruir, 0.1f);
                    // si usas contenedor luego para expulsar, desactiva esta opción
                    continue;
                }

                // 2) comportamiento por defecto: micro = destruir, normal = almacenar
                if (esMicro && destruirMicroBasura)
                {
                    _microBasuraContador++;
                    OnCapturadoMicro?.Invoke(_microBasuraContador);
                    raizDestruir.SetActive(false); // oculta inmediatamente
                    Destroy(raizDestruir, 0.1f);
                }
                else
                {
                    if (_contenedor.Count < capacidadMax)
                    {
                        // almacenar (no destruimos)
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                        rb.isKinematic = true;
                        rb.useGravity = false;

                        raizDestruir.SetActive(false); // oculto hasta expulsar
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

        ActualizarFeedback(objetosAtraidos);
        for (int i = 0; i < n; i++) _buffer[i] = null;
    }

    private void ActualizarFeedback(int objetos)
    {
        float carga = Mathf.Clamp01(objetos / 8f);

        if (motorAudio)
        {
            motorAudio.pitch = Mathf.Lerp(1f, 1.3f, carga);
            motorAudio.volume = Mathf.Lerp(0.4f, 0.8f, carga);
        }
        if (vfxSuccion)
        {
            var emission = vfxSuccion.emission;
            emission.rateOverTime = Mathf.Lerp(10f, 80f, carga);
        }
        if (cam)
        {
            temblorT += Time.deltaTime * temblorFrecuencia * Mathf.Max(0.01f, carga);
            float nx = Mathf.PerlinNoise(temblorT, 0.0f) * 2f - 1f;
            float ny = Mathf.PerlinNoise(0.0f, temblorT) * 2f - 1f;
            Vector3 objetivo = camLocalBasePos + new Vector3(nx, ny, 0f) * (temblorAmplitud * carga);
            cam.transform.localPosition = Vector3.Lerp(cam.transform.localPosition, objetivo, Time.deltaTime * temblorSuavizado);

            float targetFOV = fovBase + fovKick * carga;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.deltaTime * fovRecuperacion);
        }
    }

    private System.Collections.IEnumerator DestruirConEfecto(GameObject obj)
    {
        float dur = 0.12f;
        if (obj == null) yield break;
        Vector3 inicio = obj.transform.localScale;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float f = 1f - (t / dur);
            obj.transform.localScale = inicio * f;
            yield return null;
        }
        Destroy(obj);
    }

    // API
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
}
