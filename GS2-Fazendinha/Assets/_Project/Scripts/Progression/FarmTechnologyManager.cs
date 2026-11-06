using System.Collections.Generic;
using UnityEngine;

public class FarmTechnologyManager : MonoBehaviour
{
    public static FarmTechnologyManager Instance { get; private set; }

    private readonly List<TechnologyData> unlocked =
        new List<TechnologyData>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError(
                "Keep only one FarmTechnologyManager in the scene.");

            enabled = false;
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool Has(TechnologyData technology) =>
        technology != null && unlocked.Contains(technology);

    public bool Unlock(TechnologyData technology)
    {
        if (technology == null || Has(technology))
            return false;

        unlocked.Add(technology);
        return true;
    }

    public float GetBonus(TechnologyEffect effect)
    {
        // Soma os bonus desbloqueados para o efeito solicitado.
        float total = 0f;

        foreach (TechnologyData technology in unlocked)
        {
            if (technology.effect == effect)
                total += technology.value;
        }

        return total;
    }

    public string GetUnlockedNames()
    {
        if (unlocked.Count == 0)
            return "None";

        List<string> names = new List<string>();

        foreach (TechnologyData technology in unlocked)
            names.Add(technology.DisplayName);

        return string.Join(", ", names);
    }
}