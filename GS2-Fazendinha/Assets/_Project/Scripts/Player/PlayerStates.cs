public class PlayerIdleState : IPlayerState
{
    private readonly PlayerFarmController player;
    public PlayerIdleState(PlayerFarmController player) { this.player = player; }
    public void Enter() => player.StopAndClear();
    public void Tick() { }
    public void FixedTick() { }
    public void Exit() { }
}

public class PlayerMoveState : IPlayerState
{
    private readonly PlayerFarmController player;
    public PlayerMoveState(PlayerFarmController player) { this.player = player; }
    public void Enter() { }

    public void Tick()
    {
        if (player.Agent.pathPending) return;
        if (player.HasPathProblem())
        {
            player.Show("Nao foi possivel chegar ao destino.");
            player.Machine.ChangeState(player.Idle);
            return;
        }
        if (!player.HasArrived()) return;

        if (player.TargetPlot != null)
            player.Machine.ChangeState(player.Interact);
        else
            player.Machine.ChangeState(player.Idle);
    }

    public void FixedTick() { }
    public void Exit() { }
}

public class PlayerInteractState : IPlayerState
{
    private readonly PlayerFarmController player;
    public PlayerInteractState(PlayerFarmController player) { this.player = player; }
    public void Enter() => player.PerformQueuedInteraction();
    public void Tick() => player.Machine.ChangeState(player.Idle);
    public void FixedTick() { }
    public void Exit() { }
}
