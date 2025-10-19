using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class TrashAI : MonoBehaviour
{
    [Header("Referencias")]
    public Transform player; // si no se asigna, busca por tag "Player"

    [Header("Percepción / Huida")]
    public float radioPercepcion = 10f;     // si el player entra aquí, huye
    public float distanciaHuida = 8f;       // cuánto quiere alejarse en cada escape
    public float cooldownRecalculo = 0.6f;  // evita recalcular cada frame

    [Header("Vagar cuando lejos")]
    public float radioVagar = 6f;
    public float tiempoEntreVagar = 3f;

    [Header("NavMeshAgent ajustes")]
    public float velocidadCorrer = 5.5f;
    public float velocidadVagar = 2.2f;
    public float alturaSample = 1.0f;       // para proyectar en NavMesh

    private NavMeshAgent agent;
    private float tRecalculo;
    private float tVagar;
    private Vector3 ultimoDestino;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    void Start()
    {
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }

        // Primera meta “vagar” para que no se queden quietos al inicio
        SetDestinoVagar();
    }

    void Update()
    {
        if (!player || !agent || !agent.isOnNavMesh) return;

        float dt = Time.deltaTime;
        tRecalculo -= dt;
        tVagar -= dt;

        float distPlayer = Vector3.Distance(player.position, transform.position);
        bool huir = distPlayer <= radioPercepcion;

        if (huir)
        {
            if (tRecalculo <= 0f || Vector3.Distance(agent.destination, transform.position) < 0.8f)
            {
                HuirDelPlayer();
                tRecalculo = cooldownRecalculo;
            }

            if (agent.speed != velocidadCorrer) agent.speed = velocidadCorrer;
        }
        else
        {
            if (tVagar <= 0f || Vector3.Distance(agent.destination, transform.position) < 0.5f)
            {
                SetDestinoVagar();
                tVagar = tiempoEntreVagar;
            }

            if (agent.speed != velocidadVagar) agent.speed = velocidadVagar;
        }
    }

    private void HuirDelPlayer()
    {
        Vector3 dir = (transform.position - player.position);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = Random.insideUnitSphere; // por si están encima

        dir.Normalize();
        Vector3 candidato = transform.position + dir * distanciaHuida;

        if (NavMesh.SamplePosition(candidato, out var hit, alturaSample + distanciaHuida, NavMesh.AllAreas))
        {
            SetDestino(hit.position);
        }
        else
        {
            // fallback: algún punto aleatorio
            SetDestinoVagar();
        }
    }

    private void SetDestinoVagar()
    {
        Vector3 rnd = transform.position + Random.insideUnitSphere * radioVagar;
        rnd.y = transform.position.y;

        if (NavMesh.SamplePosition(rnd, out var hit, alturaSample + radioVagar, NavMesh.AllAreas))
            SetDestino(hit.position);
    }

    private void SetDestino(Vector3 pos)
    {
        ultimoDestino = pos;
        agent.isStopped = false;
        agent.SetDestination(pos);
    }
}
