using UnityEngine;

public class LevelState : MonoBehaviour
{
    [Header("Objetivos")]
    [Tooltip("Basura necesaria para ganar (total a depositar).")]
    public int goalTotalTrash = 20;

    [Header("UI Panels")]
    public GameObject winPanel;
    public GameObject losePanel;

    [Header("Estado (solo lectura)")]
    [SerializeField] private int deposited = 0;
    public int Deposited => deposited;

    public bool IsEnded { get; private set; } = false;
    public bool IsWon   { get; private set; } = false;

    void Start()
    {
        if (winPanel) winPanel.SetActive(false);
        if (losePanel) losePanel.SetActive(false);
    }

    /// <summary>Suma depósito. Si llega a la meta, gana.</summary>
    public void AddDeposited(int amount)
    {
        if (IsEnded || amount <= 0) return;
        deposited += amount;

        if (deposited >= goalTotalTrash)
        {
            Win();
        }
    }

    public void Win()
    {
        if (IsEnded) return;
        IsEnded = true;
        IsWon = true;
        if (winPanel) winPanel.SetActive(true);
        // Opcional: Time.timeScale = 0f;
    }

    public void Lose()
    {
        // Solo perder si no se ganó antes
        if (IsEnded) return;
        IsEnded = true;
        IsWon = false;
        if (losePanel) losePanel.SetActive(true);
        // Opcional: Time.timeScale = 0f;
    }
}
