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
    public TMP_Text textoBasura;    // ej: "Basura: 7 / 20"

    [Header("Timer")]
    [Tooltip("Si está activo, el tiempo cuenta hacia atrás desde 'tiempoInicialSegundos'. Si no, cuenta hacia arriba desde 0.")]
    public bool cuentaRegresiva = true;

    [Tooltip("Tiempo inicial (segundos) para cuenta regresiva, o valor hasta el que quieres contar (opcional).")]
    public float tiempoInicialSegundos = 180f; // 3 minutos por defecto

    [Tooltip("¿El timer empieza automáticamente al iniciar?")]
    public bool iniciarAutomaticamente = true;

    [Header("Basura recolectada")]
    [Tooltip("Cantidad total de basura en el mapa (para mostrar en el contador).")]
    public int totalBasura = 20;

    // Estado interno
    private bool _timerActivo = false;
    private float _tiempo; // segundos
    private int _contadorBasura = 0;

    void Reset()
    {
        if (!vacuum) vacuum = FindObjectOfType<VacuumController>();
    }

    void Awake()
    {
        if (!vacuum) vacuum = FindObjectOfType<VacuumController>();
    }

    void OnEnable()
    {
        if (vacuum != null)
        {
            vacuum.OnEnergiaCambiada.AddListener(OnEnergiaCambiada);
            vacuum.OnCapturadoMicro.AddListener(OnCapturadoMicro);
            vacuum.OnCapturadoNormal.AddListener(OnCapturadoNormalGO);
        }
    }

    void Start()
    {
        // Inicializa UI
        if (textoEnergia) textoEnergia.text = "0";
        if (textoBasura)  textoBasura.text  = $"0 / {totalBasura}";

        if (cuentaRegresiva)
            _tiempo = Mathf.Max(0f, tiempoInicialSegundos);
        else
            _tiempo = 0f;

        if (textoTiempo) textoTiempo.text = FormatearTiempo(_tiempo);

        _timerActivo = iniciarAutomaticamente;
    }

    void OnDisable()
    {
        if (vacuum != null)
        {
            vacuum.OnEnergiaCambiada.RemoveListener(OnEnergiaCambiada);
            vacuum.OnCapturadoMicro.RemoveListener(OnCapturadoMicro);
            vacuum.OnCapturadoNormal.RemoveListener(OnCapturadoNormalGO);
        }
    }

    void Update()
    {
        // Actualización del timer
        if (_timerActivo)
        {
            if (cuentaRegresiva)
            {
                _tiempo -= Time.deltaTime;
                if (_tiempo <= 0f)
                {
                    _tiempo = 0f;
                    _timerActivo = false; // detiene al llegar a cero
                }
            }
            else
            {
                _tiempo += Time.deltaTime;
            }

            if (textoTiempo) textoTiempo.text = FormatearTiempo(_tiempo);
        }
    }

    // ===== Eventos del VacuumController =====

    private void OnEnergiaCambiada(float energiaNorm01)
    {
        if (textoEnergia)
        {
            int pct = Mathf.RoundToInt(Mathf.Clamp01(energiaNorm01) * 100f);
            textoEnergia.text = pct.ToString();
        }
    }

    private void OnCapturadoMicro(int totalMicro)
    {
        _contadorBasura++;
        ActualizarTextoBasura();
    }

    private void OnCapturadoNormalGO(GameObject go)
    {
        _contadorBasura++;
        ActualizarTextoBasura();
    }

    // ===== Métodos públicos =====

    public void IniciarTimer() => _timerActivo = true;
    public void PausarTimer()  => _timerActivo = false;

    public void ReiniciarTimer()
    {
        _timerActivo = false;
        _tiempo = cuentaRegresiva ? tiempoInicialSegundos : 0f;
        if (textoTiempo) textoTiempo.text = FormatearTiempo(_tiempo);
    }

    public void SetTiempo(float segundos, bool arrancar = false)
    {
        _tiempo = Mathf.Max(0f, segundos);
        if (textoTiempo) textoTiempo.text = FormatearTiempo(_tiempo);
        _timerActivo = arrancar;
    }

    public void ReiniciarContadorBasura()
    {
        _contadorBasura = 0;
        ActualizarTextoBasura();
    }

    public void SetTotalBasura(int total)
    {
        totalBasura = Mathf.Max(0, total);
        ActualizarTextoBasura();
    }

    // ===== Helpers =====

    private void ActualizarTextoBasura()
    {
        if (textoBasura)
            textoBasura.text = $"{_contadorBasura} / {totalBasura}";
    }

    private string FormatearTiempo(float segundos)
    {
        int s = Mathf.Max(0, Mathf.FloorToInt(segundos + 0.5f));
        int min = s / 60;
        int sec = s % 60;
        return string.Format("{0:00}:{1:00}", min, sec);
    }
}
