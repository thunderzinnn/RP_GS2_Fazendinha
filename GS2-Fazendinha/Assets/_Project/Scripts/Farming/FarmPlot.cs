using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class FarmPlot : MonoBehaviour
{
    [Header("Posicao de interacao")]
    [SerializeField] private Transform interactionPoint;
    [Header("Modelos (filhos do terreno)")]
    [SerializeField] private GameObject plantedVisual;
    [SerializeField] private GameObject growingVisual;
    [SerializeField] private GameObject grownVisual;
    [SerializeField] private Transform cropVisualAnchor;

    private GameObject currentModelInstance;

    public CropData CurrentCrop { get; private set; }
    public float Hydration { get; private set; }
    public float StageTimer { get; set; }
    public Vector3 InteractionPosition => interactionPoint != null
        ? interactionPoint.position : transform.position;

    public PlotStateMachine Machine { get; private set; }
    public PlotEmptyState Empty { get; private set; }
    public PlotPlantedState Planted { get; private set; }
    public PlotGrowingState Growing { get; private set; }
    public PlotGrownState Grown { get; private set; }

    public string StateName => Machine.CurrentState == Empty ? "Vazio" :
        Machine.CurrentState == Planted ? "Plantado" :
        Machine.CurrentState == Growing ? "Crescendo" : "Crescido";

    private void Awake()
    {
        Machine = new PlotStateMachine();
        Empty = new PlotEmptyState(this);
        Planted = new PlotPlantedState(this);
        Growing = new PlotGrowingState(this);
        Grown = new PlotGrownState(this);
    }

    private void Start() => Machine.ChangeState(Empty);
    private void Update() => Machine.Tick();
    private void FixedUpdate() => Machine.FixedTick();

    private void ShowStage(GameObject fallbackVisual, GameObject cropPrefab)
    {
        if (currentModelInstance != null)
            Destroy(currentModelInstance);

        if (plantedVisual != null) plantedVisual.SetActive(false);
        if (growingVisual != null) growingVisual.SetActive(false);
        if (grownVisual != null) grownVisual.SetActive(false);

        if (cropPrefab != null && cropVisualAnchor != null)
            currentModelInstance = Instantiate(cropPrefab, cropVisualAnchor);
        else if (fallbackVisual != null)
            fallbackVisual.SetActive(true);
    }

    public void ShowEmpty() => ShowStage(null, null);
    public void ShowPlanted() => ShowStage(plantedVisual,
        CurrentCrop != null ? CurrentCrop.plantedPrefab : null);
    public void ShowGrowing() => ShowStage(growingVisual,
        CurrentCrop != null ? CurrentCrop.growingPrefab : null);
    public void ShowGrown() => ShowStage(grownVisual,
        CurrentCrop != null ? CurrentCrop.grownPrefab : null);

    public bool CanPerform(FarmTool tool, CropData crop, FarmInventory inventory,
                           FarmWaterTank tank, out string message)
    {
        message = "";
        if (Machine.CurrentState == Empty && tool == FarmTool.Plant)
        {
            if (crop == null || inventory == null || inventory.SeedCount(crop) < 1)
            {
                message = "Voce precisa de sementes dessa cultura.";
                return false;
            }
            return true;
        }

        if ((Machine.CurrentState == Planted || Machine.CurrentState == Growing)
            && tool == FarmTool.Water)
        {
            if (Hydration >= 99.9f) { message = "A planta ja esta hidratada."; return false; }
            if (tank == null || !tank.CanWater(CurrentCrop))
            {
                message = "Agua insuficiente no reservatorio.";
                return false;
            }
            return true;
        }

        if (Machine.CurrentState == Grown && tool == FarmTool.Harvest)
        {
            if (inventory == null) { message = "Inventario nao configurado."; return false; }
            return true;
        }

        message = "Ferramenta incorreta para o estado desta planta.";
        return false;
    }

    public bool Interact(FarmTool tool, CropData crop, FarmInventory inventory,
                         FarmWaterTank tank, out string message)
    {
        if (!CanPerform(tool, crop, inventory, tank, out message)) return false;
        return Machine.CurrentState.Interact(tool, crop, inventory, tank, out message);
    }

    public bool TryPlant(CropData crop, FarmInventory inventory, out string message)
    {
        if (crop == null || inventory == null || !inventory.TryUseSeed(crop))
        {
            message = "Sem sementes para plantar.";
            return false;
        }
        CurrentCrop = crop;
        Hydration = crop.initialHydration;
        Machine.ChangeState(Planted);
        message = crop.CropName + " plantado!";
        return true;
    }

    public bool TryWater(FarmWaterTank tank, out string message)
    {
        if (tank == null || CurrentCrop == null || !tank.TrySpendFor(CurrentCrop))
        {
            message = "Agua insuficiente.";
            return false;
        }
        Hydration = Mathf.Min(100f, Hydration + CurrentCrop.hydrationPerWatering);
        message = "Regado! Hidratacao: " + Hydration.ToString("0") + "%";
        return true;
    }

    public bool TryHarvest(FarmInventory inventory, out string message)
    {
        if (CurrentCrop == null || inventory == null)
        {
            message = "Nao foi possivel colher.";
            return false;
        }
        CropData harvestedCrop = CurrentCrop;
        inventory.AddProduce(harvestedCrop, harvestedCrop.harvestAmount);
        CurrentCrop = null;
        Hydration = 0f;
        Machine.ChangeState(Empty);
        message = "+" + harvestedCrop.harvestAmount + " " + harvestedCrop.CropName;
        return true;
    }

    public void TickGrowth(float seconds, float stageDuration, IPlotState nextState)
    {
        if (CurrentCrop == null) return;

        float reduction = FarmTechnologyManager.Instance != null
            ? FarmTechnologyManager.Instance.GetBonus(TechnologyEffect.HydrationRetention)
            : 0f;
        float lossMultiplier = 1f - Mathf.Clamp(reduction, 0f, 0.8f);
        Hydration = Mathf.Max(0f, Hydration -
            CurrentCrop.hydrationLossPerSecond * lossMultiplier * seconds);

        if (Hydration <= 0f) return;
        StageTimer += seconds;
        if (StageTimer >= stageDuration) Machine.ChangeState(nextState);
    }
}
