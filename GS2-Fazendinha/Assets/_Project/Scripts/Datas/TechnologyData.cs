using UnityEngine;

public enum TechnologyEffect
{
    WaterCostReduction,
    HydrationRetention,
    TankCapacityBonus,
    WaterCollectionBonus,
    ShowGrowingTime,
    WormComposter,
    GrowthSpeedBonus,
    FertilizerRegenerationBonus,
    HarvestFertilizerBonus
}

[CreateAssetMenu(
    fileName = "NewTechnology",
    menuName = "Golden Root Quick Farmer/Technologies")]
public class TechnologyData : ScriptableObject
{
    [Header("Save Settings")]
    [SerializeField] private string saveId;
    public string SaveId => string.IsNullOrEmpty(saveId) ? name : saveId;

    [Header("Technology Information")]
    [SerializeField] private string displayName = "New Tech";

    [Header("UI Icon")]
    public Sprite icon;

    [Header("Technology Effect")]
    public TechnologyEffect effect;

    // Reduções, capacidade e bônus de colheita usam frações: 0.1 significa 10%.
    // Só a regeneração de água usa unidades adicionais por ciclo.
    [Min(0f)] public float value = 0.2f;

    [Header("Purchase Settings")]
    [Min(0)] public int price = 50;

    public bool marketExclusive;

    public string DisplayName => displayName;

    public string EffectDescription
    {
        get
        {
            string percent = (value * 100f).ToString("0.#") + "%";
            switch (effect)
            {
                case TechnologyEffect.WaterCostReduction: return "Watering cost: -" + percent;
                case TechnologyEffect.HydrationRetention: return "Hydration loss: -" + percent;
                case TechnologyEffect.TankCapacityBonus: return "Tank capacity: +" + percent;
                case TechnologyEffect.WaterCollectionBonus: return "Water regeneration: +" + value.ToString("0.##") + " per cycle";
                case TechnologyEffect.GrowthSpeedBonus: return "All crop growth speed: +" + percent;
                case TechnologyEffect.FertilizerRegenerationBonus: return "Composter generation: +" + percent + "\nRequires the worm composter.";
                case TechnologyEffect.HarvestFertilizerBonus: return "Fertilizer received per harvest: +" + percent;
                case TechnologyEffect.ShowGrowingTime: return "Shows crop growth progress in the crop menu.";
                default: return "";
            }
        }
    }
}
