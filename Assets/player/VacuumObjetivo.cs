using UnityEngine;

public class VacuumObjetivo : MonoBehaviour
{
    [Header("Material (opcional)")]
    public VacuumMaterialData material;

    [Header("Overrides (opcionales)")]
    [Tooltip("Si > 0, reemplaza la masa del Rigidbody para el cálculo de succión.")]
    public float masaVirtualOverride = 0f;

    [Tooltip("Multiplica la facilidad de succión extra (se combina con el material).")]
    public float multiplicadorSuccionExtra = 1f;

    [Header("Destrucción visual")]
    [Tooltip("Si lo asignas, este objeto será el que se desactive/destruya al capturar. Si no, se usa el root del Rigidbody.")]
    public GameObject raizParaDestruir;

    // ---------------------------
    // RESISTENCIA / LUCHA (por objeto)
    // ---------------------------
    [Header("Resistencia / Lucha (por objeto)")]
    [Tooltip("Resistencia total a vencer (u). Piensa en 'segundos' de succión a intensidad 1.")]
    public float resistencia = 3.0f;

    [Tooltip("Se recupera si dejas de aspirarlo (u/s).")]
    public float regeneracionResistencia = 1.0f;

    [Tooltip("Factor extra de dificultad para este objeto (1 = normal).")]
    public float multiplicadorResistencia = 1.0f;

    [Tooltip("Estado interno (0..resistencia).")]
    public float progresoActual = 0f;

    [Tooltip("Indica si actualmente está en lucha (si está siendo succionado y aún no se despega).")]
    public bool enLucha = false;

    // ---------------------------
    // ADHERENCIA AL SUELO (por objeto)
    // ---------------------------
    [Header("Adherencia al suelo (por objeto)")]
    [Tooltip("Si está activado, este objeto solo lucha mientras toque suelo.")]
    public bool resistirSoloEnSuelo = true;

    [Tooltip("Aceleración hacia abajo aplicada mientras lucha para simular pegamento/fricción (m/s^2).")]
    public float adhesionAceleracion = 12f;

    [Tooltip("Si se despega del suelo, termina la lucha y puede capturarse sin freno en la boca.")]
    public bool capturarInmediatoAlDespegar = true;

    // ---------- Helpers ----------
    public bool EsCapturable => material ? material.capturable : true;
    public bool EsMicroBasura => material ? material.microBasura : true;

    public float MasaEfectiva(Rigidbody rb)
    {
        if (masaVirtualOverride > 0f) return masaVirtualOverride;
        if (material && material.masaVirtual > 0f) return material.masaVirtual;
        return rb ? rb.mass : 1f;
    }

    public float MultiplicadorSuccionTotal
        => (material ? material.multiplicadorSuccion : 1f) * Mathf.Max(0.01f, multiplicadorSuccionExtra);

    public float MultiplicadorResistenciaTotal
        => Mathf.Max(0.01f, multiplicadorResistencia);
}
