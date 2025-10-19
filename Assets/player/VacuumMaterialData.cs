using UnityEngine;

[CreateAssetMenu(fileName = "NuevoMaterialDeBasura", menuName = "Vacuum/Basura Material Data")]
public class VacuumMaterialData : ScriptableObject
{
    [Header("Identidad")]
    public string id = "Basura";

    [Header("Comportamiento")]
    [Tooltip("Si es microbasura: se destruye al capturar (en lugar de almacenarse).")]
    public bool microBasura = true;

    [Tooltip("Si no es capturable, solo vibra/atrae pero no entra (requiere upgrade).")]
    public bool capturable = true;

    [Tooltip("Masa virtual para tuning (si 0, se usa rb.mass).")]
    public float masaVirtual = 0f;

    [Tooltip("Multiplica la facilidad de succión (1=normal, >1 más fácil, <1 más difícil).")]
    public float multiplicadorSuccion = 1f;

    [Header("Feedback (opcional)")]
    public ParticleSystem vfxCapturaPrefab;  // Efecto al capturar
    public AudioClip sfxCaptura;             // Sonido al capturar
    [Range(0f, 1f)] public float volumenSfx = 0.8f;
}
