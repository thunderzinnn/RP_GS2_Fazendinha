using UnityEngine;

public class FarmFertilizerStorage : MonoBehaviour
{
    [Header("Fertilizer Storage")]
    [SerializeField, Min(1f)] private float capacity = 100f;
    [SerializeField, Min(0f)] private float startingFertilizer;

    [Header("Worm Composter Reference")]
    [SerializeField] private FarmWormComposter wormComposter;

    [Header("Harvest Rewards")]
    [SerializeField, Range(0f, 1f)] private float normalHarvestFraction = 0.2f;
    [SerializeField, Range(0f, 1f)] private float fertilizedHarvestFraction = 0.05f;

    [Header("Fertilizer Application")]
    [SerializeField, Min(0.1f)] private float applicationCost = 20f;
    [SerializeField, Range(0f, 0.9f)] private float growthTimeReduction = 0.25f;

    public float CurrentFertilizer { get; private set; }
    public float StartingFertilizer => startingFertilizer;

    public void SaveProgress(string prefix)
    {
        PlayerPrefs.SetFloat(prefix + ".Fertilizer", CurrentFertilizer);
    }

    public void LoadProgress(string prefix)
    {
        CurrentFertilizer = Mathf.Clamp(PlayerPrefs.GetFloat(prefix + ".Fertilizer", startingFertilizer), 0f, Capacity);
    }
    public float Capacity => Mathf.Max(1f, capacity);
    public float ApplicationCost => Mathf.Max(0.1f, applicationCost);
    public float BaseGrowthTimeReduction => Mathf.Clamp(growthTimeReduction, 0f, 0.9f);

    public float GrowthTimeReduction => Mathf.Clamp(
        BaseGrowthTimeReduction + (wormComposter != null
            ? wormComposter.ActiveGrowthTimeReductionBonus
            : 0f), 0f, 0.9f);
    public bool CanApply => CurrentFertilizer + 0.0001f >= ApplicationCost;
    public float HarvestYieldBonus => wormComposter != null
        ? wormComposter.HarvestYieldBonus
        : 0f;

    private void Awake()
    {
        CurrentFertilizer = Mathf.Clamp(startingFertilizer, 0f, Capacity);
    }

    public float AddHarvestReward(bool wasFertilized)
    {
        float fraction = wasFertilized
            ? fertilizedHarvestFraction
            : normalHarvestFraction;

        float bonus = FarmTechnologyManager.Instance != null
            ? FarmTechnologyManager.Instance.GetBonus(TechnologyEffect.HarvestFertilizerBonus)
            : 0f;
        return AddFertilizer(Capacity * Mathf.Clamp01(fraction) * (1f + Mathf.Max(0f, bonus)));
    }

    public float AddFertilizer(float amount)
    {
        float previousAmount = CurrentFertilizer;
        CurrentFertilizer = Mathf.Min(
            Capacity,
            CurrentFertilizer + Mathf.Max(0f, amount));

        return CurrentFertilizer - previousAmount;
    }

    public bool TrySpendForApplication()
    {
        if (!CanApply)
            return false;

        CurrentFertilizer = Mathf.Max(
            0f,
            CurrentFertilizer - ApplicationCost);

        return true;
    }
}
