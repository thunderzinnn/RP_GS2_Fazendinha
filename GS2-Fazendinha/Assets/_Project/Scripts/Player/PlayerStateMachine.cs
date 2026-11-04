public interface IPlayerState
{
    void Enter();
    void Tick();
    void FixedTick();
    void Exit();
}

public class PlayerStateMachine
{
    public IPlayerState CurrentState { get; private set; }

    public void ChangeState(IPlayerState next)
    {
        CurrentState?.Exit();
        CurrentState = next;
        CurrentState?.Enter();
    }

    public void Tick() => CurrentState?.Tick();
    public void FixedTick() => CurrentState?.FixedTick();
}
