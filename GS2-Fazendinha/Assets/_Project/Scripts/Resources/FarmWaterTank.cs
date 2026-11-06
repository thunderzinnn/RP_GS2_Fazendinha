using UnityEngine;


public class FarmWaterTank : MonoBehaviour
{
    [Header("Water Storage")]
    [SerializeField, Min(1f)] private float baseCapacity = 100f;
    [SerializeField, Min(0f)] private float startingWater = 70f;

    [Header("Automatic Water Regeneration")]
    [SerializeField, Min(0.1f)]
    private float regenerationInterval = 2f;
    [SerializeField, Min(0.01f)]
    private float regenerationAmount = 0.35f;

    private float regenerationTimer;

    private FarmTechnologyManager Technologies =>
        FarmTechnologyManager.Instance;

    public float CurrentWater { get; private set; }

    public float Capacity => baseCapacity +
        (Technologies != null
            ? Technologies.GetBonus(TechnologyEffect.TankCapacityBonus)
            : 0f);

    public float RegenerationTimeRemaining =>
        CurrentWater >= Capacity
            ? 0f
            : Mathf.Max(0f, regenerationInterval - regenerationTimer);

    // Initialization

    private void Awake()
    {
        InitializeWater();
    }

    private void Update()
    {
        UpdateRegeneration();
    }

    // Sets the initial water amount.
    private void InitializeWater()
    {
        CurrentWater = Mathf.Clamp(startingWater, 0f, baseCapacity);
        regenerationTimer = 0f;
    }

    // Automatic Regeneration

    // Tracks time and triggers water regeneration.
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

    // Restores water without exceeding tank capacity.
    private void RegenerateWater(int cycles)
    {
        float amount = GetRegenerationAmount() * cycles;

        CurrentWater = Mathf.Min(
            Capacity,
            CurrentWater + amount);
    }

    // Applies technology bonuses to regeneration.
    private float GetRegenerationAmount()
    {
        float bonus = Technologies != null
            ? Technologies.GetBonus(
                TechnologyEffect.WaterCollectionBonus)
            : 0f;

        return Mathf.Max(0f, regenerationAmount + bonus);
    }

    // Water Consumption

    // Calculates watering cost with technology discounts.
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

    // Checks whether the tank has enough water.
    public bool CanWater(CropData crop)
    {
        return crop != null &&
            CurrentWater + 0.0001f >= WaterCost(crop);
    }

    // Spends water only when enough is available.
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