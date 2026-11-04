using UnityEngine;

public enum TechnologyEffect
{
    WaterCostReduction,
    HydrationRetention,
    TankCapacityBonus,
    WaterCollectionBonus
}

[CreateAssetMenu(fileName = "NewTechnology", menuName = "Golden Root Quick Farmer/Techology")]
public class TechnologyData : ScriptableObject
{
    [SerializeField] private string displayName = "New Tech";
    public TechnologyEffect effect;
    [Min(0f)] public float value = 0.2f;
    [Min(0)] public int price = 50;

    public string DisplayName => displayName;
}
