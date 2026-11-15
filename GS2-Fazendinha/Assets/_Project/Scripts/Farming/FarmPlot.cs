using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class FarmPlot : MonoBehaviour
{
    [Header("Save Settings")]
    [SerializeField] private string saveId;
    public string SaveId => string.IsNullOrEmpty(saveId) ? name : saveId;

    [Header("Interaction Position")]
    [SerializeField] private Transform interactionPoint;

    [Header("Fertilizer Reference")]
    [SerializeField] private FarmFertilizerStorage fertilizerStorage;

    [Header("Crop Visuals")]
    [SerializeField] private GameObject plantedVisual;
    [SerializeField] private GameObject growingVisual;
    [SerializeField] private GameObject grownVisual;

    [SerializeField] private Transform cropVisualAnchor;

    [Header("Action Effects")]
    [SerializeField] private GameObject wateringEffectPrefab;
    [SerializeField] private GameObject fertilizerEffectPrefab;
    [SerializeField] private GameObject harvestEffectPrefab;

    [Header("Crop Survival")]
    [SerializeField, Min(0.1f)] private float drySurvivalSeconds = 10f;
    [SerializeField, Min(0.1f)] private float deadVisualSeconds = 3f;

    private GameObject currentModelInstance;
    private float dryTimer;

    public float DryTimeRemaining =>
        Mathf.Max(0f, drySurvivalSeconds - dryTimer);

    public float DeadVisualSeconds => deadVisualSeconds;

    public CropData CurrentCrop { get; private set; }
    public float Hydration { get; private set; }
    public float StageTimer { get; set; }
    public bool IsFertilized { get; private set; }

    public float GrowthPercentage => GetGrowthPercent();

    public Vector3 InteractionPosition => interactionPoint != null
        ? interactionPoint.position
        : transform.position;

    public PlotStateMachine Machine { get; private set; }
    public PlotEmptyState Empty { get; private set; }
    public PlotPlantedState Planted { get; private set; }
    public PlotGrowingState Growing { get; private set; }
    public PlotGrownState Grown { get; private set; }
    public PlotDeadState Dead { get; private set; }

    public string StateName => Machine.CurrentState == Empty ? "Empty" :
        Machine.CurrentState == Planted ? "Planted" :
        Machine.CurrentState == Growing ? "Growing" :
        Machine.CurrentState == Dead ? "Dead" : "Grown";

    private void Awake()
    {
        Machine = new PlotStateMachine();

        Empty = new PlotEmptyState(this);
        Planted = new PlotPlantedState(this);
        Growing = new PlotGrowingState(this);
        Grown = new PlotGrownState(this);
        Dead = new PlotDeadState(this);
    }

    private void Start()
    {
        if (Machine.CurrentState == null) Machine.ChangeState(Empty);
    }

    public void SaveProgress(string prefix)
    {
        string key = prefix + ".Plot." + SaveId;
        PlayerPrefs.SetString(key + ".Crop", CurrentCrop != null ? CurrentCrop.SaveId : "");
        PlayerPrefs.SetString(key + ".State", Machine != null && Machine.CurrentState != null ? StateName : "Empty");
        PlayerPrefs.SetFloat(key + ".StageTimer", StageTimer);
        PlayerPrefs.SetFloat(key + ".Hydration", Hydration);
        PlayerPrefs.SetFloat(key + ".DryTimer", dryTimer);
        PlayerPrefs.SetInt(key + ".Fertilized", IsFertilized ? 1 : 0);
    }

    // Reconstrói o estado existente sem gastar sementes, água ou adubo novamente.
    public void LoadProgress(string prefix, CropData[] crops)
    {
        if (!gameObject.activeInHierarchy || Machine == null) return;
        string key = prefix + ".Plot." + SaveId;
        string cropId = PlayerPrefs.GetString(key + ".Crop", "");
        CropData savedCrop = null;
        foreach (var crop in crops)
            if (crop != null && crop.SaveId == cropId) { savedCrop = crop; break; }
        string state = PlayerPrefs.GetString(key + ".State", "Empty");
        if (savedCrop == null || state == "Empty") { ClearCrop(); return; }
        CurrentCrop = savedCrop;
        Hydration = Mathf.Clamp(PlayerPrefs.GetFloat(key + ".Hydration", savedCrop.initialHydration), 0f, 100f);
        IsFertilized = PlayerPrefs.GetInt(key + ".Fertilized", 0) == 1;
        dryTimer = Mathf.Clamp(PlayerPrefs.GetFloat(key + ".DryTimer", 0f), 0f, drySurvivalSeconds);
        if (state == "Planted") Machine.ChangeState(Planted);
        else if (state == "Growing") Machine.ChangeState(Growing);
        else if (state == "Grown") Machine.ChangeState(Grown);
        else if (state == "Dead") Machine.ChangeState(Dead);
        else { ClearCrop(); return; }
        StageTimer = Mathf.Max(0f, PlayerPrefs.GetFloat(key + ".StageTimer", 0f));
    }

    private void Update() => Machine.Tick();

    private void FixedUpdate() => Machine.FixedTick();

    private float GetGrowthPercent()
    {
        if (CurrentCrop == null || Machine == null)
            return 0f;

        if (Machine.CurrentState == Grown)
            return 100f;

        bool planted = Machine.CurrentState == Planted;
        bool growing = Machine.CurrentState == Growing;

        if (!planted && !growing)
            return 0f;

        float plantedDuration = Mathf.Max(
            0f,
            CurrentCrop.plantedSeconds);

        float growingDuration = Mathf.Max(
            0f,
            CurrentCrop.growingSeconds);

        float totalDuration = plantedDuration + growingDuration;

        if (totalDuration <= 0f)
            return 0f;

        float elapsedTime;

        if (planted)
        {
            elapsedTime = Mathf.Clamp(
                StageTimer,
                0f,
                plantedDuration);
        }
        else
        {
            // O contador reinicia ao trocar de estado; soma a etapa concluída.
            elapsedTime = plantedDuration + Mathf.Clamp(
                StageTimer,
                0f,
                growingDuration);
        }

        return Mathf.Clamp01(elapsedTime / totalDuration) * 100f;
    }

    private void ShowStage(GameObject fallbackVisual, GameObject cropPrefab)
    {
        if (currentModelInstance != null)
        {
            currentModelInstance.SetActive(false);
            Destroy(currentModelInstance);
            currentModelInstance = null;
        }

        SetVisualActive(plantedVisual, false);
        SetVisualActive(growingVisual, false);
        SetVisualActive(grownVisual, false);

        if (cropPrefab != null)
        {
            Transform anchor = cropVisualAnchor != null
                ? cropVisualAnchor
                : transform;

            currentModelInstance = Instantiate(cropPrefab, anchor, false);
            currentModelInstance.SetActive(true);

            if (!currentModelInstance.activeInHierarchy)
            {
                Debug.LogWarning(
                    "Crop anchor has an inactive parent. Check the plot hierarchy.",
                    this);
            }
        }
        else if (fallbackVisual != null)
        {
            SetVisualActive(fallbackVisual, true);

            if (!fallbackVisual.activeInHierarchy)
            {
                Debug.LogWarning(
                    "Crop visual has an inactive parent. Check the plot hierarchy.",
                    this);
            }
        }
        else if (CurrentCrop != null)
        {
            Debug.LogWarning(
                "No visual configured for this crop stage.",
                this);
        }
    }

    // Impede que uma referência incorreta desative o próprio terreno.
    private void SetVisualActive(GameObject visual, bool active)
    {
        if (visual == null)
            return;

        if (transform.IsChildOf(visual.transform))
        {
            Debug.LogWarning(
                "Crop visuals must not reference the plot or its ancestors.",
                this);

            return;
        }

        visual.SetActive(active);
    }

    private bool HasPlantingVisual(CropData crop)
    {
        if (crop == null)
            return false;

        if (crop.plantedPrefab != null)
            return true;

        return plantedVisual != null &&
            !transform.IsChildOf(plantedVisual.transform);
    }

    public void ShowEmpty() => ShowStage(null, null);

    public void ShowPlanted() => ShowStage(
        plantedVisual,
        CurrentCrop != null ? CurrentCrop.plantedPrefab : null);

    public void ShowGrowing() => ShowStage(
        growingVisual,
        CurrentCrop != null ? CurrentCrop.growingPrefab : null);

    public void ShowGrown() => ShowStage(
        grownVisual,
        CurrentCrop != null ? CurrentCrop.grownPrefab : null);

    public void ShowDead() => ShowStage(
        null,
        CurrentCrop != null ? CurrentCrop.deadPrefab : null);

    public void ClearCrop()
    {
        CurrentCrop = null;
        Hydration = 0f;
        dryTimer = 0f;
        StageTimer = 0f;
        IsFertilized = false;

        Machine.ChangeState(Empty);
    }

    public bool CanPerform(
        FarmTool tool,
        CropData crop,
        FarmInventory inventory,
        FarmWaterTank tank,
        out string message)
    {
        message = "";

        if (tool == FarmTool.Fertilize)
            return CanFertilize(out message);

        if (Machine.CurrentState == Empty && tool == FarmTool.Plant)
        {
            if (!HasPlantingVisual(crop))
            {
                message =
                    "Configure CropData.Planted Prefab or FarmPlot.Planted Visual before planting.";

                return false;
            }

            if (crop == null ||
                inventory == null ||
                inventory.SeedCount(crop) < 1)
            {
                message = "You need seeds for this crop.";
                return false;
            }

            return true;
        }

        if ((Machine.CurrentState == Planted ||
             Machine.CurrentState == Growing) &&
            tool == FarmTool.Water)
        {
            if (Hydration >= 99.9f)
            {
                message = "The crop is already hydrated.";
                return false;
            }

            if (tank == null || !tank.CanWater(CurrentCrop))
            {
                message = "Not enough water in the tank.";
                return false;
            }

            return true;
        }

        if (Machine.CurrentState == Grown && tool == FarmTool.Harvest)
        {
            if (inventory == null)
            {
                message = "Inventory not configured.";
                return false;
            }

            return true;
        }

        message = "Incorrect tool for the current crop stage.";
        return false;
    }

    public bool Interact(
        FarmTool tool,
        CropData crop,
        FarmInventory inventory,
        FarmWaterTank tank,
        out string message)
    {
        if (!CanPerform(tool, crop, inventory, tank, out message))
            return false;

        return Machine.CurrentState.Interact(
            tool,
            crop,
            inventory,
            tank,
            out message);
    }

    public bool TryPlant(
        CropData crop,
        FarmInventory inventory,
        out string message)
    {
        if (!CanPerform(
            FarmTool.Plant,
            crop,
            inventory,
            null,
            out message))
        {
            return false;
        }

        if (crop == null ||
            inventory == null ||
            !inventory.TryUseSeed(crop))
        {
            message = "No seeds available for planting.";
            return false;
        }

        CurrentCrop = crop;
        Hydration = crop.initialHydration;
        dryTimer = 0f;
        IsFertilized = false;

        Machine.ChangeState(Planted);

        message = crop.CropName + " planted!";
        return true;
    }

    public bool TryWater(FarmWaterTank tank, out string message)
    {
        if (tank == null ||
            CurrentCrop == null ||
            !tank.TrySpendFor(CurrentCrop))
        {
            message = "Not enough water.";
            return false;
        }

        Hydration = Mathf.Min(
            100f,
            Hydration + CurrentCrop.hydrationPerWatering);

        dryTimer = 0f;

        message = "Watered! Hydration: " +
            Hydration.ToString("0") + "%";

        if (AudioManager.Instance != null) AudioManager.Instance.PlayWatering();
        SpawnActionEffect(wateringEffectPrefab);

        return true;
    }

    public bool CanFertilize(out string message)
    {
        if (CurrentCrop == null || Machine == null ||
            (Machine.CurrentState != Planted && Machine.CurrentState != Growing))
        {
            message = "Only growing crops can be fertilized.";
            return false;
        }

        if (IsFertilized)
        {
            message = "This crop has already been fertilized.";
            return false;
        }

        if (fertilizerStorage == null)
        {
            message = "Fertilizer storage not configured.";
            return false;
        }

        if (!fertilizerStorage.CanApply)
        {
            message = "Not enough fertilizer.";
            return false;
        }

        message = "";
        return true;
    }

    public bool TryFertilize(out string message)
    {
        if (!CanFertilize(out message))
            return false;

        if (!fertilizerStorage.TrySpendForApplication())
        {
            message = "Not enough fertilizer.";
            return false;
        }

        IsFertilized = true;

        if (AudioManager.Instance != null) AudioManager.Instance.PlayFertilizer();
        SpawnActionEffect(fertilizerEffectPrefab);

        message = "Crop fertilized! Remaining growth time reduced by " +
            (fertilizerStorage.GrowthTimeReduction * 100f).ToString("0") + "%!";

        return true;
    }

    private void SpawnActionEffect(GameObject prefab)
    {
        if (prefab == null) return;
        Vector3 position = cropVisualAnchor != null ? cropVisualAnchor.position : transform.position;
        var effect = Instantiate(prefab, position, Quaternion.identity);
        var particles = effect.GetComponent<ParticleSystem>();
        if (particles != null) particles.Play(true);
    }

    public bool TryHarvest(
        FarmInventory inventory,
        out string message)
    {
        if (CurrentCrop == null || inventory == null ||
            Machine == null || Machine.CurrentState != Grown)
        {
            message = "Unable to harvest.";
            return false;
        }

        CropData harvestedCrop = CurrentCrop;
        int harvestAmount = GetHarvestAmount();

        inventory.AddProduce(
            harvestedCrop,
            harvestAmount);

        float fertilizerGained = fertilizerStorage != null
            ? fertilizerStorage.AddHarvestReward(IsFertilized)
            : 0f;

        ClearCrop();

        message = "+" + harvestAmount +
            " " + harvestedCrop.CropName;

        if (fertilizerStorage != null)
            message += " | Fertilizer: +" + fertilizerGained.ToString("0.#");

        if (AudioManager.Instance != null) AudioManager.Instance.PlayHarvest();
        SpawnActionEffect(harvestEffectPrefab);

        return true;
    }

    public int GetHarvestAmount()
    {
        if (CurrentCrop == null)
            return 0;

        float bonus = IsFertilized && fertilizerStorage != null
            ? fertilizerStorage.HarvestYieldBonus
            : 0f;

        return Mathf.CeilToInt(CurrentCrop.harvestAmount * (1f + bonus));
    }

    public void TickGrowth(
        float seconds,
        float stageDuration,
        IPlotState nextState)
    {
        if (CurrentCrop == null)
            return;

        float reduction = FarmTechnologyManager.Instance != null
            ? FarmTechnologyManager.Instance.GetBonus(
                TechnologyEffect.HydrationRetention)
            : 0f;

        float lossMultiplier =
            1f - Mathf.Clamp(reduction, 0f, 0.8f);

        if (seconds <= 0f)
            return;

        float lossPerSecond =
            CurrentCrop.hydrationLossPerSecond * lossMultiplier;

        float hydratedSeconds = Hydration > 0f
            ? (lossPerSecond > 0f
                ? Mathf.Min(seconds, Hydration / lossPerSecond)
                : seconds)
            : 0f;

        Hydration = Mathf.Max(
            0f,
            Hydration - lossPerSecond * seconds);

        if (Hydration <= 0f)
            dryTimer += seconds - hydratedSeconds;
        else
            dryTimer = 0f;

        if (dryTimer >= drySurvivalSeconds)
        {
            Machine.ChangeState(Dead);
            return;
        }

        AdvanceGrowth(hydratedSeconds * GetGrowthRate(), stageDuration, nextState);
    }

    private float GetGrowthRate()
    {
        float technologyBonus = FarmTechnologyManager.Instance != null
            ? FarmTechnologyManager.Instance.GetBonus(TechnologyEffect.GrowthSpeedBonus)
            : 0f;
        float fertilizerMultiplier = IsFertilized && fertilizerStorage != null
            ? 1f / (1f - fertilizerStorage.GrowthTimeReduction)
            : 1f;
        return (1f + Mathf.Max(0f, technologyBonus)) * fertilizerMultiplier;
    }

    // Aproveita a sobra de crescimento ao passar do plantio para a próxima etapa.
    private void AdvanceGrowth(float growthSeconds, float stageDuration, IPlotState nextState)
    {
        StageTimer += growthSeconds;

        if (StageTimer < stageDuration)
            return;

        float remainingGrowth = Mathf.Max(0f, StageTimer - stageDuration);
        Machine.ChangeState(nextState);

        if (Machine.CurrentState != Growing || remainingGrowth <= 0f)
            return;

        StageTimer = remainingGrowth;

        if (StageTimer >= CurrentCrop.growingSeconds)
            Machine.ChangeState(Grown);
    }
}
