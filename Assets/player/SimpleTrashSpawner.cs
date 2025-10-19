using System.Collections.Generic;
using UnityEngine;

public class SimpleTrashSpawner : MonoBehaviour
{
    [Header("Prefabs de basura (elige aleatorio)")]
    public List<GameObject> prefabs;

    [Header("Cantidad a spawnear")]
    public int cantidad = 20;

    [Header("Modo de spawn")]
    public bool usarPuntos = false;
    public Transform[] puntosSpawn; // si usarPuntos = true, usa estos

    [Header("Área de spawn (si no usas puntos)")]
    public Vector3 areaCentro = Vector3.zero;      // en coordenadas de mundo
    public Vector3 areaTam = new Vector3(30, 0, 30);
    [Tooltip("Altura desde la que se hace el raycast hacia abajo (si se usa).")]
    public float alturaRaycast = 5f;

    [Header("Colocación en suelo")]
    public bool proyectarAlSuelo = true;
    public LayerMask groundMask = ~0;
    [Tooltip("Offset vertical extra tras proyectar al suelo (cm)")]
    public float yOffset = 0.02f;
    public bool alinearConNormal = false;

    [Header("Evitar solapes básicos")]
    public bool evitarSolapes = true;
    [Tooltip("Radio mínimo entre instancias (m)")]
    public float separacionMin = 0.8f;
    [Tooltip("Intentos máximos por objeto para encontrar un lugar válido")]
    public int intentosPorBasura = 20;

    [Header("Parent opcional para mantener la jerarquía limpia")]
    public Transform parentContenedor;

    [Header("Rotación aleatoria")]
    public bool rotacionAleatoriaY = true;

    // Lista interna de instanciados (útil para limpiar)
    private readonly List<GameObject> _instanciados = new List<GameObject>();

    void Start()
    {
        SpawnInicial();
    }

    public void SpawnInicial()
    {
        LimpiarInstancias();

        if (prefabs == null || prefabs.Count == 0)
        {
            Debug.LogWarning("[SimpleTrashSpawner] No hay prefabs asignados.");
            return;
        }

        if (usarPuntos)
        {
            if (puntosSpawn == null || puntosSpawn.Length == 0)
            {
                Debug.LogWarning("[SimpleTrashSpawner] usarPuntos está activo pero no hay puntos.");
                return;
            }

            int total = Mathf.Min(cantidad, puntosSpawn.Length);
            for (int i = 0; i < total; i++)
            {
                var t = puntosSpawn[i];
                Vector3 pos = t.position;
                Quaternion rot = t.rotation;

                // Opcionalmente proyectar al suelo
                if (proyectarAlSuelo && Physics.Raycast(pos + Vector3.up * alturaRaycast, Vector3.down, out var hit, alturaRaycast * 2f, groundMask, QueryTriggerInteraction.Ignore))
                {
                    pos = hit.point + Vector3.up * yOffset;
                    if (alinearConNormal) rot = Quaternion.FromToRotation(Vector3.up, hit.normal) * rot;
                }

                InstanciarBasura(pos, rot);
            }
        }
        else
        {
            // Spawnear en área rectangular
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

                    Vector3 pos = rnd;
                    Quaternion rot = rotacionAleatoriaY ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) : Quaternion.identity;

                    if (proyectarAlSuelo)
                    {
                        Vector3 rayOrigin = rnd + Vector3.up * alturaRaycast;
                        if (Physics.Raycast(rayOrigin, Vector3.down, out var hit, alturaRaycast * 2f, groundMask, QueryTriggerInteraction.Ignore))
                        {
                            pos = hit.point + Vector3.up * yOffset;
                            if (alinearConNormal)
                                rot = Quaternion.FromToRotation(Vector3.up, hit.normal) * rot;
                        }
                        else
                        {
                            // Si no encontró suelo, prueba otro intento
                            continue;
                        }
                    }

                    if (evitarSolapes && !EsPosicionValida(pos)) continue;

                    InstanciarBasura(pos, rot);
                    colocado = true;
                }
            }
        }
    }

    private void InstanciarBasura(Vector3 pos, Quaternion rot)
    {
        var prefab = prefabs[Random.Range(0, prefabs.Count)];
        if (!prefab) return;

        GameObject go = Instantiate(prefab, pos, rot, parentContenedor ? parentContenedor : null);
        _instanciados.Add(go);
    }

    private bool EsPosicionValida(Vector3 pos)
    {
        if (!evitarSolapes || _instanciados.Count == 0 || separacionMin <= 0f) return true;

        float minSqr = separacionMin * separacionMin;
        for (int i = 0; i < _instanciados.Count; i++)
        {
            var obj = _instanciados[i];
            if (!obj) continue;
            if ((obj.transform.position - pos).sqrMagnitude < minSqr)
                return false;
        }
        return true;
    }

    public void LimpiarInstancias()
    {
        for (int i = 0; i < _instanciados.Count; i++)
        {
            if (_instanciados[i] != null)
                Destroy(_instanciados[i]);
        }
        _instanciados.Clear();
    }

    // Gizmos para ver el área/puntos
    void OnDrawGizmosSelected()
    {
        if (!usarPuntos)
        {
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.15f);
            Gizmos.DrawCube(areaCentro, areaTam);
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.9f);
            Gizmos.DrawWireCube(areaCentro, areaTam);
        }
        else
        {
            if (puntosSpawn != null)
            {
                Gizmos.color = new Color(0.1f, 1f, 0.3f, 0.85f);
                foreach (var t in puntosSpawn)
                {
                    if (!t) continue;
                    Gizmos.DrawSphere(t.position, 0.2f);
                    Gizmos.DrawRay(t.position, t.up * 0.6f);
                }
            }
        }
    }
}
