using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class TrashSpawner : MonoBehaviour
{
    [Header("Prefabs de basura (aleatorio)")]
    public List<GameObject> prefabs;

    [Header("Cantidad a spawnear al iniciar")]
    public int cantidad = 20;

    [Header("Modo de spawn")]
    public bool usarPuntos = false;
    public Transform[] puntosSpawn;

    [Header("Área de spawn (si no usas puntos)")]
    public Vector3 areaCentro = Vector3.zero;
    public Vector3 areaTam = new Vector3(30, 0, 30);
    public float alturaRaycast = 4f;

    [Header("NavMesh")]
    public float maxDesvioNavMesh = 2.0f; // radio para SamplePosition
    public LayerMask groundMask = ~0;
    public Transform player; // opcional: para asignar a la IA

    [Header("Separación mínima entre basuras (m)")]
    public float separacionMin = 1.0f;
    public int intentosPorBasura = 20;

    private readonly List<GameObject> _instanciados = new List<GameObject>();

    void Start()
    {
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }

        if (usarPuntos && (puntosSpawn == null || puntosSpawn.Length == 0))
        {
            Debug.LogWarning("[TrashSpawner] usarPuntos está activo pero no hay puntos asignados.");
            usarPuntos = false;
        }

        SpawnInicial();
    }

    public void SpawnInicial()
    {
        LimpiarInstancias();

        if (usarPuntos)
        {
            int total = Mathf.Min(cantidad, puntosSpawn.Length);
            for (int i = 0; i < total; i++)
            {
                var pos = puntosSpawn[i].position;
                var rot = puntosSpawn[i].rotation;
                InstanciarEnNavMesh(pos, rot);
            }
        }
        else
        {
            for (int i = 0; i < cantidad; i++)
            {
                bool colocado = false;
                for (int k = 0; k < intentosPorBasura && !colocado; k++)
                {
                    Vector3 rnd = areaCentro + new Vector3(
                        Random.Range(-areaTam.x * 0.5f, areaTam.x * 0.5f),
                        0f,
                        Random.Range(-areaTam.z * 0.5f, areaTam.z * 0.5f)
                    );

                    Vector3 posRay = rnd + Vector3.up * alturaRaycast;
                    if (Physics.Raycast(posRay, Vector3.down, out var hit, alturaRaycast * 2f, groundMask, QueryTriggerInteraction.Ignore))
                    {
                        Vector3 pos = hit.point;
                        if (NavMesh.SamplePosition(pos, out var nmHit, maxDesvioNavMesh, NavMesh.AllAreas))
                        {
                            if (EsPosValida(nmHit.position))
                            {
                                InstanciarEnNavMesh(nmHit.position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                                colocado = true;
                            }
                        }
                    }
                }
            }
        }
    }

    private bool EsPosValida(Vector3 pos)
    {
        if (separacionMin <= 0f || _instanciados.Count == 0) return true;
        float minSqr = separacionMin * separacionMin;
        foreach (var go in _instanciados)
        {
            if (!go) continue;
            if ((go.transform.position - pos).sqrMagnitude < minSqr) return false;
        }
        return true;
    }

    private void InstanciarEnNavMesh(Vector3 pos, Quaternion rot)
    {
        if (prefabs == null || prefabs.Count == 0) return;

        GameObject prefab = prefabs[Random.Range(0, prefabs.Count)];
        if (!prefab) return;

        GameObject go = Instantiate(prefab, pos, rot);
        _instanciados.Add(go);

        // Asignar Player a la IA si está
        var ai = go.GetComponent<TrashAI>();
        if (ai && player) ai.player = player;

        // Estado inicial: Agent activo, RB kinematic (NavAgentSuctionLink lo ajusta)
        var rb = go.GetComponent<Rigidbody>();
        var agent = go.GetComponent<NavMeshAgent>();
        if (agent && rb)
        {
            rb.isKinematic = true;
            rb.useGravity = true;
            agent.enabled = true;
        }
    }

    public void LimpiarInstancias()
    {
        for (int i = 0; i < _instanciados.Count; i++)
            if (_instanciados[i]) Destroy(_instanciados[i]);
        _instanciados.Clear();
    }

    void OnDrawGizmosSelected()
    {
        if (!usarPuntos)
        {
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.2f);
            Gizmos.DrawCube(areaCentro, areaTam);
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.9f);
            Gizmos.DrawWireCube(areaCentro, areaTam);
        }
    }
}
