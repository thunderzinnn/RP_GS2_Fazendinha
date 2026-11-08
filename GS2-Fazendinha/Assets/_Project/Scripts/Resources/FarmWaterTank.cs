using UnityEngine;

public class FarmWaterTank : MonoBehaviour
{
    [Header("Water Storage")]
    [SerializeField, Min(1f)]
    private float baseCapacity = 100f;

    [SerializeField, Min(0f)]
    private float startingWater = 70f;

    [Header("Automatic Water Regeneration")]
    [SerializeField, Min(0.1f)]
    private float regenerationInterval = 2f;

    [SerializeField, Min(0.01f)]
    private float regenerationAmount = 0.35f;

    private float regenerationTimer;

    private FarmTechnologyManager Technologies =>
        FarmTechnologyManager.Instance;

    public float CurrentWater { get; private set; }

    // O bônus usa uma fração: 0.1 representa 10% da capacidade base.
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

    // Define a água inicial sem ultrapassar a capacidade base.
    private void InitializeWater()
    {
        CurrentWater = Mathf.Clamp(
            startingWater,
            0f,
            baseCapacity);

        regenerationTimer = 0f;
    }

    // Conta os intervalos completos para regenerar a quantidade correspondente.
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

    // Recupera água sem ultrapassar a capacidade atual do tanque.
    private void RegenerateWater(int cycles)
    {
        float amount = GetRegenerationAmount() * cycles;

        CurrentWater = Mathf.Min(
            Capacity,
            CurrentWater + amount);
    }

    // O bônus de regeneração continua somando unidades de água por ciclo.
    private float GetRegenerationAmount()
    {
        float bonus = Technologies != null
            ? Technologies.GetBonus(
                TechnologyEffect.WaterCollectionBonus)
            : 0f;

        return Mathf.Max(0f, regenerationAmount + bonus);
    }

    // Aplica o desconto das tecnologias, limitado a 80% do custo original.
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

    // Confere o saldo com uma pequena tolerância para cálculos decimais.
    public bool CanWater(CropData crop)
    {
        return crop != null &&
            CurrentWater + 0.0001f >= WaterCost(crop);
    }

    // Consome água somente quando existe quantidade suficiente para regar.
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