using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class FlowerActivatorOnPickup : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("El VacuumController de la escena (escucha los eventos de recolección).")]
    public VacuumController vacuum;
    
    [Tooltip("Padre que contiene como hijos TODAS las flores a activar.")]
    public Transform contenedorFlores;

    [Header("Regla principal (porcentual n por 1)")]
    [Tooltip("Flores esperadas por cada basura recolectada. Acepta fracciones (p.ej. 3.5 => 3 seguras + 50% de una extra).")]
    public float floresPorBasura = 3.5f;

    [Header("Azar opcional extra por evento")]
    [Tooltip("Probabilidad de añadir un 'paquete' extra de flores en una recolección (0..1).")]
    [Range(0f, 1f)] public float probExtraPorEvento = 0.15f;

    [Tooltip("Cantidad extra (entera) a activar si ocurre el evento extra.")]
    public Vector2Int rangoExtra = new Vector2Int(1, 3);

    [Header("Barajado / Selección")]
    [Tooltip("Si está activo, se baraja el orden inicial de los hijos para distribuir mejor la aleatoriedad.")]
    public bool barajarAlInicio = true;

    [Tooltip("Si está activo, cada pick elige hijos aleatorios sin reemplazo (más disperso). Si no, toma en orden del buffer barajado.")]
    public bool seleccionarAleatorioCadaVez = false;

    [Header("Eventos")]
    public UnityEvent OnTodasLasFloresActivadas;

    // Internos
    private readonly List<Transform> _poolFloresInactivas = new List<Transform>();
    private System.Random _rng;

    void Reset()
    {
        if (!vacuum) vacuum = FindObjectOfType<VacuumController>();
    }

    void Awake()
    {
        if (!vacuum) vacuum = FindObjectOfType<VacuumController>();
        _rng = new System.Random();
    }

    void OnEnable()
    {
        if (vacuum != null)
        {
            vacuum.OnCapturadoMicro.AddListener(OnRecolectaBasuraMicro);
            vacuum.OnCapturadoNormal.AddListener(OnRecolectaBasuraNormal);
        }
        ReconstruirPool();
    }

    void OnDisable()
    {
        if (vacuum != null)
        {
            vacuum.OnCapturadoMicro.RemoveListener(OnRecolectaBasuraMicro);
            vacuum.OnCapturadoNormal.RemoveListener(OnRecolectaBasuraNormal);
        }
    }

    // Llama cuando se recolecta "micro" (ya te llega del Vacuum)
    private void OnRecolectaBasuraMicro(int totalMicro)
    {
        ActivarFloresPorEvento();
    }

    // Llama cuando se recolecta "normal" (guardar en contenedor)
    private void OnRecolectaBasuraNormal(GameObject _)
    {
        ActivarFloresPorEvento();
    }

    /// <summary>
    /// Aplica la regla: floor(n) activaciones + (prob de 1 extra) + paquete extra opcional.
    /// </summary>
    private void ActivarFloresPorEvento()
    {
        if (_poolFloresInactivas.Count == 0) return;

        // 1) Parte entera
        int baseEntero = Mathf.Max(0, Mathf.FloorToInt(floresPorBasura));

        // 2) Parte fraccionaria => prob de 1 extra
        float frac = Mathf.Clamp01(floresPorBasura - baseEntero);
        int extraFrac = (UnityEngine.Random.value < frac) ? 1 : 0;

        int totalAActivar = baseEntero + extraFrac;

        // 3) Paquete extra por evento (opcional)
        if (probExtraPorEvento > 0f && UnityEngine.Random.value < probExtraPorEvento)
        {
            int extraPack = Mathf.Clamp(_rng.Next(rangoExtra.x, rangoExtra.y + 1), 0, 1000);
            totalAActivar += extraPack;
        }

        if (totalAActivar <= 0) return;

        // Limita por el tamaño restante
        totalAActivar = Mathf.Min(totalAActivar, _poolFloresInactivas.Count);

        // Activa N hijos inactivos
        if (seleccionarAleatorioCadaVez)
        {
            // Elegir índices únicos aleatorios cada vez
            for (int i = 0; i < totalAActivar; i++)
            {
                int idx = _rng.Next(0, _poolFloresInactivas.Count);
                Transform t = _poolFloresInactivas[idx];
                ActivarYRemover(t, idx);
            }
        }
        else
        {
            // Tomar desde el final (o inicio) del pool ya barajado
            for (int i = 0; i < totalAActivar; i++)
            {
                int idx = _poolFloresInactivas.Count - 1;
                Transform t = _poolFloresInactivas[idx];
                ActivarYRemover(t, idx);
            }
        }

        // Si nos quedamos sin flores
        if (_poolFloresInactivas.Count == 0)
            OnTodasLasFloresActivadas?.Invoke();
    }

    private void ActivarYRemover(Transform t, int idxEnPool)
    {
        if (t == null)
        {
            _poolFloresInactivas.RemoveAt(idxEnPool);
            return;
        }
        t.gameObject.SetActive(true);
        _poolFloresInactivas.RemoveAt(idxEnPool);
    }

    /// <summary>
    /// Reconstruye la lista de hijos inactivos del contenedor.
    /// </summary>
    public void ReconstruirPool()
    {
        _poolFloresInactivas.Clear();

        if (!contenedorFlores)
        {
            Debug.LogWarning("[FlowerActivatorOnPickup] Falta asignar 'contenedorFlores'.");
            return;
        }

        int childCount = contenedorFlores.childCount;
        for (int i = 0; i < childCount; i++)
        {
            Transform child = contenedorFlores.GetChild(i);
            if (child == null) continue;

            if (!child.gameObject.activeSelf)
                _poolFloresInactivas.Add(child);
        }

        if (barajarAlInicio && _poolFloresInactivas.Count > 1)
            Mezclar(_poolFloresInactivas);
    }

    private void Mezclar(List<Transform> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = _rng.Next(n + 1);
            Transform tmp = list[k];
            list[k] = list[n];
            list[n] = tmp;
        }
    }
}
