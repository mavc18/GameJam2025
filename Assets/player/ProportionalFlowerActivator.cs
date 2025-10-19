using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class ProportionalFlowerActivator : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("El VacuumController que genera el evento de recolección de basura.")]
    public VacuumController vacuum;

    [Tooltip("Hasta 10 contenedores de flores. Cada hijo debe estar inactivo al inicio.")]
    public List<Transform> contenedoresFlores = new List<Transform>();

    [Header("Regla de aparición")]
    [Tooltip("Flores esperadas por cada basura recolectada (total global, se reparte proporcionalmente entre los contenedores).")]
    public float floresTotalesPorBasura = 10f;

    [Tooltip("Probabilidad de añadir una flor extra por evento (0..1).")]
    [Range(0f, 1f)] public float probExtraFlor = 0.3f;

    [Tooltip("Si está activo, se baraja el orden de las flores inactivas al inicio.")]
    public bool barajarAlInicio = true;

    [Tooltip("Si está activo, se eligen flores aleatorias en cada activación (más disperso).")]
    public bool seleccionarAleatorioCadaVez = false;

    [Header("Evento al terminar todas las flores")]
    public UnityEvent OnTodasLasFloresActivadas;

    // === Internos ===
    private readonly List<List<Transform>> _pools = new List<List<Transform>>();
    private System.Random _rng;
    private int _totalFloresInicial;
    private bool _sinFlores = false;

    void Reset()
    {
        if (!vacuum) vacuum = FindObjectOfType<VacuumController>();
    }

    void Awake()
    {
        _rng = new System.Random();
        if (!vacuum) vacuum = FindObjectOfType<VacuumController>();
    }

    void OnEnable()
    {
        if (vacuum)
        {
            vacuum.OnCapturadoMicro.AddListener(OnRecolectaBasura);
            vacuum.OnCapturadoNormal.AddListener(OnRecolectaBasuraNormal);
        }
        ReconstruirPools();
    }

    void OnDisable()
    {
        if (vacuum)
        {
            vacuum.OnCapturadoMicro.RemoveListener(OnRecolectaBasura);
            vacuum.OnCapturadoNormal.RemoveListener(OnRecolectaBasuraNormal);
        }
    }

    // === EVENTOS ===
    private void OnRecolectaBasura(int _) => ActivarFloresProporcional();
    private void OnRecolectaBasuraNormal(GameObject _) => ActivarFloresProporcional();

    // === LÓGICA PRINCIPAL ===
    private void ActivarFloresProporcional()
    {
        if (_sinFlores || _pools.Count == 0 || _totalFloresInicial == 0)
            return;

        // calcular total actual
        int totalRestantes = 0;
        foreach (var pool in _pools) totalRestantes += pool.Count;

        if (totalRestantes == 0)
        {
            _sinFlores = true;
            OnTodasLasFloresActivadas?.Invoke();
            return;
        }

        // calcular cantidad global a activar este evento
        int totalAActivar = Mathf.FloorToInt(floresTotalesPorBasura);
        if (Random.value < (floresTotalesPorBasura - totalAActivar)) totalAActivar++;
        if (Random.value < probExtraFlor) totalAActivar++;

        // repartir proporcionalmente entre contenedores
        int activadasTotal = 0;

        for (int i = 0; i < _pools.Count; i++)
        {
            var pool = _pools[i];
            if (pool.Count == 0) continue;

            float proporcion = (float)pool.Count / totalRestantes;
            int activarContenedor = Mathf.RoundToInt(totalAActivar * proporcion);

            // asegura al menos 1 flor en contenedores grandes
            if (activarContenedor == 0 && pool.Count > 0 && Random.value < proporcion)
                activarContenedor = 1;

            activarContenedor = Mathf.Min(activarContenedor, pool.Count);

            for (int j = 0; j < activarContenedor; j++)
            {
                int idx = seleccionarAleatorioCadaVez ? _rng.Next(pool.Count) : pool.Count - 1;
                Transform flor = pool[idx];
                if (flor != null) flor.gameObject.SetActive(true);
                pool.RemoveAt(idx);
                activadasTotal++;
            }
        }

        // si ya todas las flores están activadas
        bool todasActivas = true;
        foreach (var pool in _pools)
        {
            if (pool.Count > 0)
            {
                todasActivas = false;
                break;
            }
        }

        if (todasActivas)
        {
            _sinFlores = true;
            OnTodasLasFloresActivadas?.Invoke();
        }
    }

    // === POOLS ===
    public void ReconstruirPools()
    {
        _pools.Clear();
        _totalFloresInicial = 0;
        _sinFlores = false;

        if (contenedoresFlores == null || contenedoresFlores.Count == 0)
        {
            Debug.LogWarning("[ProportionalFlowerActivator] No hay contenedores de flores asignados.");
            return;
        }

        foreach (var cont in contenedoresFlores)
        {
            if (!cont) continue;

            var lista = new List<Transform>();
            for (int i = 0; i < cont.childCount; i++)
            {
                Transform hijo = cont.GetChild(i);
                if (!hijo.gameObject.activeSelf)
                    lista.Add(hijo);
            }

            if (barajarAlInicio && lista.Count > 1)
                Mezclar(lista);

            _pools.Add(lista);
            _totalFloresInicial += lista.Count;
        }

        if (_totalFloresInicial == 0)
            Debug.LogWarning("[ProportionalFlowerActivator] Todos los hijos ya están activos o no existen.");
    }

    private void Mezclar(List<Transform> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = _rng.Next(n + 1);
            (list[n], list[k]) = (list[k], list[n]);
        }
    }
}
