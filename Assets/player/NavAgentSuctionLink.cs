using UnityEngine;
using UnityEngine.AI;

/// Puente que entrega el control del objeto:
/// - NavMeshAgent activo normalmente (RB kinemático).
/// - Al ser succionado: desactiva Agent y activa RB no-kinemático para que la física responda.
/// - Si deja de succionarse y no fue capturado, pasado un tiempo, reactiva el Agent.
[RequireComponent(typeof(Collider))]
public class NavAgentSuctionLink : MonoBehaviour
{
    [Header("Refs (auto si faltan)")]
    public NavMeshAgent agent;
    public Rigidbody rb;

    [Header("Retorno a IA")]
    [Tooltip("Segundos sin succión antes de reactivar el NavMeshAgent.")]
    public float tiempoSinSuccionParaVolver = 0.35f;

    [Tooltip("Suaviza el ‘regreso’ de la orientación cuando vuelve la IA.")]
    public float suavizadoReentradaRot = 10f;

    [Tooltip("Si el objeto está muy cerca del suelo al volver, puedes forzar Y = 0 o lo que necesites (0 = sin forzar).")]
    public bool fijarAlturaAlVolver = false;
    public float alturaFijada = 0f;

    private float _timerSinSuccion;
    private bool _enModoFisica;

    void Reset()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
    }

    void Awake()
    {
        if (!agent) agent = GetComponent<NavMeshAgent>();
        if (!rb) rb = GetComponent<Rigidbody>();

        // Estado inicial: IA controla (RB kinemático para que no moleste a Agent)
        SetModoIA(true);
    }

    void Update()
    {
        if (_enModoFisica)
        {
            _timerSinSuccion += Time.deltaTime;
            if (_timerSinSuccion >= tiempoSinSuccionParaVolver)
            {
                // Volver a la IA (si no fue destruido/capturado)
                SetModoIA(true);
            }
        }
    }

    /// Llamado cada FixedUpdate en el que el vacuum está aplicando fuerza a este objeto.
    public void NotificarSuccionTick()
    {
        _timerSinSuccion = 0f;

        if (!_enModoFisica)
        {
            // Handover a física
            SetModoIA(false);
        }
    }

    private void SetModoIA(bool activoIA)
    {
        _enModoFisica = !activoIA;

        if (agent)
        {
            if (activoIA)
            {
                // Reposiciona de forma segura por si el objeto se movió por física
                if (agent.enabled)
                {
                    agent.Warp(transform.position);
                }
                else
                {
                    agent.enabled = true;
                    agent.Warp(transform.position);
                }

                if (fijarAlturaAlVolver)
                {
                    var p = transform.position;
                    p.y = alturaFijada;
                    transform.position = p;
                    agent.Warp(p);
                }

                agent.isStopped = false;
                agent.updatePosition = true;
                agent.updateRotation = true;
            }
            else
            {
                if (agent.enabled)
                {
                    agent.isStopped = true;
                    agent.updatePosition = false;
                    agent.updateRotation = false;
                    agent.enabled = false;
                }
            }
        }

        if (rb)
        {
            if (activoIA)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.useGravity = true;         // opcional según tu escena
                rb.isKinematic = true;        // IA controla el transform
            }
            else
            {
                rb.isKinematic = false;       // física controla el movimiento
                rb.useGravity = true;         // opcional según tu escena
            }
        }
    }
}
