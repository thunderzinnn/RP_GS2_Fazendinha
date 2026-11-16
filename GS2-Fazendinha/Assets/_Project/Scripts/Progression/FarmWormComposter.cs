using UnityEngine;

public class FarmWormComposter : MonoBehaviour
{
    [Header("System References")]
    [SerializeField] private FarmTechnologyManager technologies;
    [SerializeField] private FarmFertilizerStorage fertilizerStorage;
    [SerializeField] private TechnologyData unlockTechnology;

    [Header("Compost Visuals")]
    [SerializeField] private GameObject basicComposterModel;
    [SerializeField] private GameObject composterModel;

    [Header("Fertilizer Generation")]
    [SerializeField, Min(0.1f)] private float generationInterval = 5f;
    [SerializeField, Min(0.01f)] private float generationAmount = 1f;

    [Header("Fertilized Crop Bonuses")]
    [SerializeField, Range(0f, 0.9f)] private float additionalGrowthTimeReduction = 0.25f;

    private float generationTimer;

    public void SaveProgress(string prefix)
    {
        PlayerPrefs.SetFloat(prefix + ".ComposterTimer", generationTimer);
    }

    public void LoadProgress(string prefix)
    {
        generationTimer = Mathf.Clamp(PlayerPrefs.GetFloat(prefix + ".ComposterTimer", 0f), 0f, GenerationInterval);
        UpdateUnlockState();
    }

    public bool IsUnlocked => technologies != null &&
        unlockTechnology != null &&
        unlockTechnology.effect == TechnologyEffect.WormComposter &&
        technologies.Has(unlockTechnology);

    public float GenerationInterval => Mathf.Max(0.1f, generationInterval);
    public float GenerationAmount => Mathf.Max(0f, generationAmount) * (1f +
        (technologies != null ? Mathf.Max(0f, technologies.GetBonus(TechnologyEffect.FertilizerRegenerationBonus)) : 0f));
    public float GrowthTimeReductionBonus => Mathf.Clamp(additionalGrowthTimeReduction, 0f, 0.9f);
    public float ActiveGrowthTimeReductionBonus => IsUnlocked ? GrowthTimeReductionBonus : 0f;

    public float HarvestYieldBonus => IsUnlocked
        ? Mathf.Max(0f, unlockTechnology.value)
        : 0f;

    public float GenerationTimeRemaining =>
        !IsUnlocked || fertilizerStorage == null ||
        fertilizerStorage.CurrentFertilizer >= fertilizerStorage.Capacity
            ? 0f
            : Mathf.Max(0f, GenerationInterval - generationTimer);

    private void Awake()
    {
        UpdateUnlockState();
    }

    private void Update()
    {
        UpdateUnlockState();
        TickGeneration(Time.deltaTime);
    }

    private void UpdateUnlockState()
    {
        if (basicComposterModel != null && basicComposterModel.activeSelf == IsUnlocked)
            basicComposterModel.SetActive(!IsUnlocked);
        if (composterModel != null && composterModel.activeSelf != IsUnlocked)
            composterModel.SetActive(IsUnlocked);
    }

    public void TickGeneration(float seconds)
    {
        if (!IsUnlocked || fertilizerStorage == null ||
            fertilizerStorage.CurrentFertilizer >= fertilizerStorage.Capacity)
        {
            generationTimer = 0f;
            return;
        }

        if (seconds <= 0f)
            return;

        generationTimer += seconds;

        if (generationTimer < GenerationInterval)
            return;

        int cycles = Mathf.FloorToInt(generationTimer / GenerationInterval);
        generationTimer -= cycles * GenerationInterval;

        fertilizerStorage.AddFertilizer(GenerationAmount * cycles);

        if (fertilizerStorage.CurrentFertilizer >= fertilizerStorage.Capacity)
            generationTimer = 0f;
    }
}
