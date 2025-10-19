using UnityEngine;

public class VacuumObjetivo : MonoBehaviour
{
    [Header("Material (datos por tipo de basura)")]
    public VacuumMaterialData material;

    [Header("Overrides (opcionales)")]
    [Tooltip("Si > 0, reemplaza la masa del RB para el cálculo de succión.")]
    public float masaVirtualOverride = 0f;

    [Tooltip("Multiplica la facilidad de succión extra (se combina con el material).")]
    public float multiplicadorSuccionExtra = 1f;

    [Header("Destrucción visual")]
    [Tooltip("Si lo asignas, este objeto será el que se reduzca/destruya al capturar. Si no, se usa el root del Rigidbody.")]
    public GameObject raizParaDestruir;

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
}
