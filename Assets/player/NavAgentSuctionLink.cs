using UnityEngine;
using UnityEngine.AI;

/// Entrega el control al RB al succionar y se lo regresa al NavMeshAgent
/// si deja de succionarse por un tiempo.
[RequireComponent(typeof(Collider))]
public class NavAgentSuctionLink : MonoBehaviour
{
    [Header("Refs (auto si faltan)")]
    public NavMeshAgent agent;
    public Rigidbody rb;

    [Header("Retorno a IA")]
    [Tooltip("Segundos sin succión antes de reactivar el NavMeshAgent.")]
    public float tiempoSinSuccionParaVolver = 0.35f;

    [Tooltip("Si el objeto está muy cerca del suelo al volver, puedes fijar Y.")]
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

        // Estado inicial: IA controla
        SetModoIA(true, warp: true);
    }

    void Update()
    {
        if (_enModoFisica)
        {
            _timerSinSuccion += Time.deltaTime;
            if (_timerSinSuccion >= tiempoSinSuccionParaVolver)
            {
                // Volver a la IA (si no fue destruido/capturado)
                SetModoIA(true, warp: true);
            }
        }
    }

    /// Llamado cada FixedUpdate en el que el vacuum aplica fuerza a este objeto.
    public void NotificarSuccionTick()
    {
        _timerSinSuccion = 0f;
        if (!_enModoFisica)
        {
            // Cambiar a física (RB dinámico, agent off)
            SetModoIA(false, warp: false);
        }
    }

    private void SetModoIA(bool activoIA, bool warp)
    {
        _enModoFisica = !activoIA;

        if (agent)
        {
            if (activoIA)
            {
                if (!agent.enabled) agent.enabled = true;
                if (warp) agent.Warp(transform.position);
                agent.isStopped = false;
                agent.updatePosition = true;
                agent.updateRotation = true;

                if (fijarAlturaAlVolver)
                {
                    var p = transform.position; p.y = alturaFijada;
                    transform.position = p; agent.Warp(p);
                }
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
                rb.useGravity = true;   // o false si tu escena lo requiere
                rb.isKinematic = true;  // IA controla transform
            }
            else
            {
                rb.isKinematic = false; // física controla
                rb.useGravity = true;   // opcional
            }
        }
    }
}
