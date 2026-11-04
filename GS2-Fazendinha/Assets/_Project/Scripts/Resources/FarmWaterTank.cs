using UnityEngine;

public class FarmWaterTank : MonoBehaviour
{
    [Header("Reservatorio")]
    [SerializeField, Min(1f)] private float baseCapacity = 100f;
    [SerializeField, Min(0f)] private float startingWater = 70f;
    [Header("Captacao de agua (teste)")]
    [SerializeField, Min(0.1f)] private float collectionCooldown = 30f;
    [SerializeField, Min(0.1f)] private float collectionAmount = 25f;

    private float nextCollectionTime;
    public float CurrentWater { get; private set; }
    private FarmTechnologyManager Tech => FarmTechnologyManager.Instance;

    public float Capacity => baseCapacity +
        (Tech != null ? Tech.GetBonus(TechnologyEffect.TankCapacityBonus) : 0f);

    public float CollectionCooldownRemaining =>
        Mathf.Max(0f, nextCollectionTime - Time.time);

    private void Awake()
    {
        CurrentWater = Mathf.Clamp(startingWater, 0f, baseCapacity);
    }

    public float WaterCost(CropData crop)
    {
        if (crop == null) return 0f;
        float reduction = Tech != null
            ? Tech.GetBonus(TechnologyEffect.WaterCostReduction) : 0f;
        return crop.waterCost * (1f - Mathf.Clamp(reduction, 0f, 0.8f));
    }

    public bool CanWater(CropData crop) =>
        crop != null && CurrentWater + 0.0001f >= WaterCost(crop);

    public bool TrySpendFor(CropData crop)
    {
        if (!CanWater(crop)) return false;
        CurrentWater = Mathf.Max(0f, CurrentWater - WaterCost(crop));
        return true;
    }

    public float CollectWater()
    {
        if (CollectionCooldownRemaining > 0f || CurrentWater >= Capacity)
            return 0f;

        float bonus = Tech != null
            ? Tech.GetBonus(TechnologyEffect.WaterCollectionBonus) : 0f;
        float collected = Mathf.Min(Capacity - CurrentWater, collectionAmount + bonus);
        CurrentWater += collected;
        nextCollectionTime = Time.time + collectionCooldown;
        return collected;
    }
}
