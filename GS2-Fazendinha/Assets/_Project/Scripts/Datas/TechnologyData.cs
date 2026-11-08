using UnityEngine;

public enum TechnologyEffect
{
    WaterCostReduction,
    HydrationRetention,
    TankCapacityBonus,
    WaterCollectionBonus,
    ShowGrowingTime
}

// Guarda o nome, o efeito, o valor e o custo da tecnologia.
[CreateAssetMenu(
    fileName = "NewTechnology",
    menuName = "Golden Root Quick Farmer/Technologies")]
public class TechnologyData : ScriptableObject
{
    [Header("Technology Information")]
    [SerializeField] private string displayName = "New Tech";

    [Header("Technology Effect")]
    public TechnologyEffect effect;

    // Reduções e capacidade usam frações: 0.1 significa 10%.
    // Regeneração usa unidades adicionais de água por ciclo.
    [Min(0f)] public float value = 0.2f;

    [Header("Purchase Settings")]
    [Min(0)] public int price = 50;

    // Quando marcado, só permite a compra durante uma oferta ativa do mercado.
    public bool marketExclusive;

    public string DisplayName => displayName;
}