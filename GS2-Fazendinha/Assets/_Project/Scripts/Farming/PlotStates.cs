public class PlotEmptyState : IPlotState
{
    private readonly FarmPlot plot;

    public PlotEmptyState(FarmPlot plot)
    {
        this.plot = plot;
    }

    public void Enter()
    {
        plot.ShowEmpty();
    }

    public void Tick() { }

    public void FixedTick() { }

    public void Exit() { }

    public bool Interact(
        FarmTool tool,
        CropData crop,
        FarmInventory inventory,
        FarmWaterTank tank,
        out string message)
    {
        return plot.TryPlant(crop, inventory, out message);
    }
}

public class PlotPlantedState : IPlotState
{
    private readonly FarmPlot plot;

    public PlotPlantedState(FarmPlot plot)
    {
        this.plot = plot;
    }

    public void Enter()
    {
        // Reinicia o tempo e exibe o modelo da etapa de plantio.
        plot.StageTimer = 0f;
        plot.ShowPlanted();
    }

    public void Tick()
    {
        plot.TickGrowth(
            UnityEngine.Time.deltaTime,
            plot.CurrentCrop.plantedSeconds,
            plot.Growing);
    }

    public void FixedTick() { }

    public void Exit() { }

    public bool Interact(
        FarmTool tool,
        CropData crop,
        FarmInventory inventory,
        FarmWaterTank tank,
        out string message)
    {
        return plot.TryWater(tank, out message);
    }
}

public class PlotGrowingState : IPlotState
{
    private readonly FarmPlot plot;

    public PlotGrowingState(FarmPlot plot)
    {
        this.plot = plot;
    }

    public void Enter()
    {
        plot.StageTimer = 0f;
        plot.ShowGrowing();
    }

    public void Tick()
    {
        plot.TickGrowth(
            UnityEngine.Time.deltaTime,
            plot.CurrentCrop.growingSeconds,
            plot.Grown);
    }

    public void FixedTick() { }

    public void Exit() { }

    public bool Interact(
        FarmTool tool,
        CropData crop,
        FarmInventory inventory,
        FarmWaterTank tank,
        out string message)
    {
        return plot.TryWater(tank, out message);
    }
}

public class PlotGrownState : IPlotState
{
    private readonly FarmPlot plot;

    public PlotGrownState(FarmPlot plot)
    {
        this.plot = plot;
    }

    public void Enter()
    {
        plot.ShowGrown();
    }

    public void Tick() { }

    public void FixedTick() { }

    public void Exit() { }

    public bool Interact(
        FarmTool tool,
        CropData crop,
        FarmInventory inventory,
        FarmWaterTank tank,
        out string message)
    {
        return plot.TryHarvest(inventory, out message);
    }
}