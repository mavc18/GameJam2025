using UnityEngine;

public class TriggerPanelActivator : MonoBehaviour
{
    [Header("🎯 Panel a mostrar")]
    [Tooltip("Arrastra aquí el panel (UI) que quieres mostrar.")]
    public GameObject panelUI;

    [Header("⚙️ Configuración")]
    [Tooltip("Si está activo, el panel se oculta al salir del trigger.")]
    public bool ocultarAlSalir = true;

    [Tooltip("Mostrar sólo una vez (no vuelve a activarse).")]
    public bool soloUnaVez = false;

    private bool yaMostrado = false;

    private void Start()
    {
        if (panelUI != null)
            panelUI.SetActive(false); // asegúrate que empiece oculto
    }

    private void OnTriggerEnter(Collider other)
    {
        if (soloUnaVez && yaMostrado) return;

        // Verifica si el objeto que entra es el jugador
        if (other.CompareTag("Player"))
        {
            if (panelUI != null)
                panelUI.SetActive(true);

            yaMostrado = true;
            Debug.Log("🟢 Panel activado por entrada al trigger.");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!ocultarAlSalir) return;

        if (other.CompareTag("Player"))
        {
            if (panelUI != null)
                panelUI.SetActive(false);

            Debug.Log("🔴 Panel ocultado al salir del trigger.");
        }
    }
}
