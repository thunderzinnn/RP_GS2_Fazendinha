using UnityEngine;

public class FarmWaterTank : MonoBehaviour
{
    [Header("Water Storage")]
    [SerializeField, Min(1f)]
    private float baseCapacity = 100f;

    [SerializeField, Min(0f)]
    private float startingWater = 50f;

    [Header("Automatic Water Regeneration")]
    [SerializeField, Min(0.1f)]
    private float regenerationInterval = 3f;

    [SerializeField, Min(0.01f)]
    private float regenerationAmount = 0.35f;

    private float regenerationTimer;

    private FarmTechnologyManager Technologies =>
        FarmTechnologyManager.Instance;

    public float CurrentWater { get; private set; }
    public float StartingWater => startingWater;

    public void SaveProgress(string prefix)
    {
        PlayerPrefs.SetFloat(prefix + ".Water", CurrentWater);
        PlayerPrefs.SetFloat(prefix + ".WaterTimer", regenerationTimer);
    }

    public void LoadProgress(string prefix)
    {
        CurrentWater = Mathf.Clamp(PlayerPrefs.GetFloat(prefix + ".Water", startingWater), 0f, Capacity);
        regenerationTimer = Mathf.Clamp(PlayerPrefs.GetFloat(prefix + ".WaterTimer", 0f), 0f, regenerationInterval);
    }

    public float Capacity => baseCapacity * (1f +
        (Technologies != null
            ? Technologies.GetBonus(TechnologyEffect.TankCapacityBonus)
            : 0f));

    public float RegenerationTimeRemaining =>
        CurrentWater >= Capacity
            ? 0f
            : Mathf.Max(0f, regenerationInterval - regenerationTimer);

    private void Awake()
    {
        InitializeWater();
    }

    private void Update()
    {
        UpdateRegeneration();
    }

    private void InitializeWater()
    {
        CurrentWater = Mathf.Clamp(
            startingWater,
            0f,
            baseCapacity);

        regenerationTimer = 0f;
    }

    private void UpdateRegeneration()
    {
        if (CurrentWater >= Capacity)
        {
            regenerationTimer = 0f;
            return;
        }

        regenerationTimer += Time.deltaTime;

        if (regenerationTimer < regenerationInterval)
            return;

        int cycles = Mathf.FloorToInt(
            regenerationTimer / regenerationInterval);

        regenerationTimer -= cycles * regenerationInterval;

        RegenerateWater(cycles);
    }

    private void RegenerateWater(int cycles)
    {
        float amount = GetRegenerationAmount() * cycles;

        CurrentWater = Mathf.Min(
            Capacity,
            CurrentWater + amount);
    }

    private float GetRegenerationAmount()
    {
        float bonus = Technologies != null
            ? Technologies.GetBonus(
                TechnologyEffect.WaterCollectionBonus)
            : 0f;

        return Mathf.Max(0f, regenerationAmount + bonus);
    }

    public float WaterCost(CropData crop)
    {
        if (crop == null)
            return 0f;

        float reduction = Technologies != null
            ? Technologies.GetBonus(
                TechnologyEffect.WaterCostReduction)
            : 0f;

        return crop.waterCost *
            (1f - Mathf.Clamp(reduction, 0f, 0.8f));
    }

    public bool CanWater(CropData crop)
    {
        return crop != null &&
            CurrentWater + 0.0001f >= WaterCost(crop);
    }

    public bool TrySpendFor(CropData crop)
    {
        if (!CanWater(crop))
            return false;

        CurrentWater = Mathf.Max(
            0f,
            CurrentWater - WaterCost(crop));

        return true;
    }
}
