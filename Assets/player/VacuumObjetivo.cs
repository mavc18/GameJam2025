using UnityEngine;

public class VacuumObjetivo : MonoBehaviour
{
    [Header("Material (opcional)")]
    public VacuumMaterialData material;

    [Header("Overrides (opcionales)")]
    [Tooltip("Si > 0, reemplaza la masa del RB para el cálculo de succión.")]
    public float masaVirtualOverride = 0f;

    [Tooltip("Multiplica la facilidad de succión extra (se combina con el material).")]
    public float multiplicadorSuccionExtra = 1f;

    [Header("Destrucción visual")]
    [Tooltip("Si lo asignas, este objeto será el que se desactive/destruya al capturar. Si no, se usa el root del Rigidbody.")]
    public GameObject raizParaDestruir;

    // --------- NUEVO: Resistencia / Lucha ----------
    [Header("Resistencia / Lucha")]
    [Tooltip("Resistencia total que hay que vencer. Piensa en 'segundos' de succión a intensidad llena.")]
    public float resistencia = 3.0f;     // mayor = más difícil de atrapar
    [Tooltip("Se recupera si dejas de aspirarlo (p.ej. 1–2 por segundo).")]
    public float regeneracionResistencia = 1.0f; 
    [Tooltip("Multiplicador adicional solo para la fase de lucha (0.5–2).")]
    public float multiplicadorResistencia = 1.0f;

    [Tooltip("Estado interno (0..resistencia).")]
    public float progresoActual = 0f;

    [Tooltip("Indica si actualmente está en lucha (si está bajo succión y dentro de distancia de lucha).")]
    public bool enLucha = false;

    // Helpers
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
        => multiplicadorResistencia * (material ? 1f : 1f);
}
