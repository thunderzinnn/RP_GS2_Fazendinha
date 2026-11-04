public interface IPlotState
{
    void Enter();
    void Tick();
    void FixedTick();
    void Exit();
    bool Interact(FarmTool tool, CropData crop, FarmInventory inventory,
                  FarmWaterTank tank, out string message);
}

public class PlotStateMachine
{
    public IPlotState CurrentState { get; private set; }

    public void ChangeState(IPlotState newState)
    {
        CurrentState?.Exit();
        CurrentState = newState;
        CurrentState?.Enter();
    }

    public void Tick() => CurrentState?.Tick();
    public void FixedTick() => CurrentState?.FixedTick();
}
