using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class FarmPlot : MonoBehaviour
{
    [Header("Interaction Position")]
    [SerializeField] private Transform interactionPoint;

    [Header("Crop Visuals")]
    [SerializeField] private GameObject plantedVisual;
    [SerializeField] private GameObject growingVisual;
    [SerializeField] private GameObject grownVisual;

    // Define a origem da planta; os valores locais do prefab sao preservados.
    [SerializeField] private Transform cropVisualAnchor;

    private GameObject currentModelInstance;

    [Header("Crop Survival")]
    [SerializeField, Min(0.1f)] private float drySurvivalSeconds = 10f;
    [SerializeField, Min(0.1f)] private float deadVisualSeconds = 3f;
    private float dryTimer;
    public float DryTimeRemaining => Mathf.Max(0f, drySurvivalSeconds - dryTimer);
    public float DeadVisualSeconds => deadVisualSeconds;

    public CropData CurrentCrop { get; private set; }
    public float Hydration { get; private set; }
    public float StageTimer { get; set; }

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

    private void Start() => Machine.ChangeState(Empty);

    private void Update() => Machine.Tick();

    private void FixedUpdate() => Machine.FixedTick();

    // Usa o prefab da cultura ou o modelo ja conectado ao terreno.
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

            // Mantem a posicao, rotacao e escala locais salvas no prefab.
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

    // Impede que uma referencia incorreta desative o proprio terreno.
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

    // Confere se existe um modelo para a primeira etapa da cultura.
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
        null, CurrentCrop != null ? CurrentCrop.deadPrefab : null);

    // Libera o terreno depois da colheita ou da exibicao da planta morta.
    public void ClearCrop()
    {
        CurrentCrop = null;
        Hydration = 0f;
        dryTimer = 0f;
        StageTimer = 0f;
        Machine.ChangeState(Empty);
    }

    // Confere se a ferramenta pode ser usada no estado atual.
    public bool CanPerform(
        FarmTool tool,
        CropData crop,
        FarmInventory inventory,
        FarmWaterTank tank,
        out string message)
    {
        message = "";

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

    // Encaminha a interacao para o estado atual do terreno.
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

    // Consome uma semente e inicia o estado plantado.
    public bool TryPlant(
        CropData crop,
        FarmInventory inventory,
        out string message)
    {
        // Valida novamente ao chegar, antes de consumir a semente.
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

        Machine.ChangeState(Planted);

        message = crop.CropName + " planted!";
        return true;
    }

    // Consome agua do reservatorio e aumenta a hidratacao.
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

        return true;
    }

    // Guarda a colheita no inventario e esvazia o terreno.
    public bool TryHarvest(
        FarmInventory inventory,
        out string message)
    {
        if (CurrentCrop == null || inventory == null)
        {
            message = "Unable to harvest.";
            return false;
        }

        CropData harvestedCrop = CurrentCrop;

        inventory.AddProduce(
            harvestedCrop,
            harvestedCrop.harvestAmount);

        ClearCrop();

        message = "+" + harvestedCrop.harvestAmount +
            " " + harvestedCrop.CropName;

        return true;
    }

    // Atualiza a hidratacao e avanca o crescimento enquanto houver agua.
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

        if (seconds <= 0f) return;
        float lossPerSecond = CurrentCrop.hydrationLossPerSecond * lossMultiplier;
        float hydratedSeconds = Hydration > 0f
            ? (lossPerSecond > 0f ? Mathf.Min(seconds, Hydration / lossPerSecond) : seconds)
            : 0f;
        Hydration = Mathf.Max(0f, Hydration - lossPerSecond * seconds);

        // Sem agua, o crescimento para e comeca a contagem para morrer.
        if (Hydration <= 0f)
            dryTimer += seconds - hydratedSeconds;
        else
            dryTimer = 0f;

        if (dryTimer >= drySurvivalSeconds)
        {
            Machine.ChangeState(Dead);
            return;
        }

        StageTimer += hydratedSeconds;

        if (StageTimer >= stageDuration)
            Machine.ChangeState(nextState);
    }
}
