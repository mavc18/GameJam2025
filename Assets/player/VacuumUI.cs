using UnityEngine;
using TMPro;

public class VacuumUI : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Arrastra aquí tu VacuumController de la escena.")]
    public VacuumController vacuum;

    [Header("TextMeshPro (UI)")]
    public TMP_Text textoEnergia;   // ej: "85 %"
    public TMP_Text textoTiempo;    // ej: "02:59"
    public TMP_Text textoBasura;    // ej: "Basura: 7"

    [Header("Timer")]
    [Tooltip("Si está activo, el tiempo cuenta hacia atrás desde 'tiempoInicialSegundos'. Si no, cuenta hacia arriba desde 0.")]
    public bool cuentaRegresiva = true;

    [Tooltip("Tiempo inicial (segundos) para cuenta regresiva, o valor hasta el que quieres contar (opcional).")]
    public float tiempoInicialSegundos = 180f; // 3 min por defecto

    [Tooltip("¿El timer está corriendo desde el inicio?")]
    public bool iniciarAutomaticamente = true;

    // Estado interno del timer
    private bool _timerActivo = false;
    private float _tiempo; // usa segundos (restantes si cuentaRegresiva; transcurridos si no)

    // Contador de basura recolectada
    private int _contadorBasura = 0;

    void Reset()
    {
        // Intenta autoconfigurar referencias
        if (!vacuum) vacuum = FindObjectOfType<VacuumController>();
    }

    void Awake()
    {
        if (!vacuum) vacuum = FindObjectOfType<VacuumController>();
    }

    void OnEnable()
    {
        // Suscripción a eventos del VacuumController (si existe)
        if (vacuum != null)
        {
            vacuum.OnEnergiaCambiada.AddListener(OnEnergiaCambiada);
            vacuum.OnCapturadoMicro.AddListener(OnCapturadoMicro);
            vacuum.OnCapturadoNormal.AddListener(OnCapturadoNormalGO);
        }
    }

    void Start()
    {
        // Inicializar UI
        if (textoEnergia)   textoEnergia.text = "0";
        if (textoBasura)    textoBasura.text  = "0";

        if (cuentaRegresiva)
        {
            _tiempo = Mathf.Max(0f, tiempoInicialSegundos);
        }
        else
        {
            _tiempo = 0f;
        }

        if (textoTiempo) textoTiempo.text = FormatearTiempo(_tiempo);

        _timerActivo = iniciarAutomaticamente;
    }

    void OnDisable()
    {
        // Limpia suscripciones
        if (vacuum != null)
        {
            vacuum.OnEnergiaCambiada.RemoveListener(OnEnergiaCambiada);
            vacuum.OnCapturadoMicro.RemoveListener(OnCapturadoMicro);
            vacuum.OnCapturadoNormal.RemoveListener(OnCapturadoNormalGO);
        }
    }

    void Update()
    {
        // Avance del timer
        if (_timerActivo)
        {
            if (cuentaRegresiva)
            {
                _tiempo -= Time.deltaTime;
                if (_tiempo <= 0f)
                {
                    _tiempo = 0f;
                    _timerActivo = false; // se detiene al llegar a cero
                }
            }
            else
            {
                _tiempo += Time.deltaTime;
            }

            if (textoTiempo) textoTiempo.text = FormatearTiempo(_tiempo);
        }
    }

    // ===== Eventos de VacuumController =====
    private void OnEnergiaCambiada(float energiaNorm01)
    {
        // energiaNorm01: valor 0..1
        if (textoEnergia)
        {
            int pct = Mathf.RoundToInt(Mathf.Clamp01(energiaNorm01) * 100f);
            textoEnergia.text = pct.ToString() + "";
        }
    }

    private void OnCapturadoMicro(int totalMicro)
    {
        // Sumamos 1 a nuestro contador total de basura
        _contadorBasura++;
        if (textoBasura) textoBasura.text = $"{_contadorBasura}";
    }

    private void OnCapturadoNormalGO(GameObject go)
    {
        // También cuenta como basura recolectada
        _contadorBasura++;
        if (textoBasura) textoBasura.text = $"{_contadorBasura}";
    }

    // ===== Utilidades públicas =====

    /// <summary>Inicia o reanuda el timer.</summary>
    public void IniciarTimer()
    {
        _timerActivo = true;
    }

    /// <summary>Pausa el timer (conserva el tiempo actual).</summary>
    public void PausarTimer()
    {
        _timerActivo = false;
    }

    /// <summary>Reinicia el timer (si cuenta regresiva, vuelve al tiempoInicial; si no, a 0).</summary>
    public void ReiniciarTimer()
    {
        _timerActivo = false;
        _tiempo = cuentaRegresiva ? Mathf.Max(0f, tiempoInicialSegundos) : 0f;
        if (textoTiempo) textoTiempo.text = FormatearTiempo(_tiempo);
    }

    /// <summary>Define un tiempo (en segundos) por código y refresca la UI.</summary>
    public void SetTiempo(float segundos, bool arrancar = false)
    {
        _tiempo = Mathf.Max(0f, segundos);
        if (textoTiempo) textoTiempo.text = FormatearTiempo(_tiempo);
        _timerActivo = arrancar;
    }

    /// <summary>Reinicia el contador de basura a 0 y refresca la UI.</summary>
    public void ReiniciarContadorBasura()
    {
        _contadorBasura = 0;
        if (textoBasura) textoBasura.text = "0";
    }

    // ===== Helpers =====
    private string FormatearTiempo(float segundos)
    {
        // Para cuenta regresiva, mostramos el tiempo restante (ya viene en _tiempo)
        // Para cronómetro, mostramos el tiempo transcurrido _tiempo
        int s = Mathf.Max(0, Mathf.FloorToInt(segundos + 0.5f));
        int min = s / 60;
        int sec = s % 60;
        return string.Format("{0:00}:{1:00}", min, sec);
    }
}
