using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;

public enum FarmTool
{
    Plant,
    Water,
    Harvest
}

[RequireComponent(typeof(NavMeshAgent))]
public class PlayerFarmController : MonoBehaviour
{
    [Header("System References")]
    [SerializeField] private Camera gameCamera;
    [SerializeField] private FarmInventory inventory;
    [SerializeField] private FarmWaterTank tank;
    [SerializeField] private FarmHUD hud;
    [SerializeField] private FarmPlotContextMenu contextMenu;

    [Header("Click and Navigation")]
    [SerializeField] private LayerMask clickableLayers = ~0;

    [SerializeField, Min(0.1f)]
    private float navMeshSearchRadius = 1.5f;

    [SerializeField, Min(0.1f)]
    private float arrivalDistance = 0.65f;

    public NavMeshAgent Agent { get; private set; }
    public FarmInventory Inventory => inventory;
    public FarmWaterTank Tank => tank;

    public FarmPlot LastSelectedPlot { get; private set; }
    public FarmPlot TargetPlot { get; private set; }
    public Vector3 Destination { get; private set; }

    public PlayerStateMachine Machine { get; private set; }
    public PlayerIdleState Idle { get; private set; }
    public PlayerMoveState Move { get; private set; }
    public PlayerInteractState Interact { get; private set; }

    private FarmTool queuedTool;
    private CropData queuedCrop;
    private float requestedAt;

    private void Awake()
    {
        Agent = GetComponent<NavMeshAgent>();

        if (gameCamera == null)
            gameCamera = Camera.main;

        Machine = new PlayerStateMachine();

        Idle = new PlayerIdleState(this);
        Move = new PlayerMoveState(this);
        Interact = new PlayerInteractState(this);
    }

    private void Start()
    {
        Machine.ChangeState(Idle);
    }

    private void Update()
    {
        HandleInput();
        Machine.Tick();
    }

    // Separa a leitura de entrada da atualizacao dos estados.
    private void HandleInput()
    {
        if (Input.GetMouseButtonDown(0))
            HandleClick();
    }

    private void FixedUpdate()
    {
        Machine.FixedTick();
    }

    private void HandleClick()
    {
        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (gameCamera == null)
            return;

        Ray ray = gameCamera.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(
            ray,
            out RaycastHit hit,
            1000f,
            clickableLayers))
        {
            if (contextMenu != null)
                contextMenu.Close();

            return;
        }

        FarmPlot plot =
            hit.collider.GetComponentInParent<FarmPlot>();

        LastSelectedPlot = plot;

        if (plot != null)
        {
            // Selecionar o terreno nao executa a acao imediatamente.
            Machine.ChangeState(Idle);

            if (contextMenu != null)
                contextMenu.Show(plot, Input.mousePosition);
            else
                Show("Context menu not configured.");

            return;
        }

        if (contextMenu != null)
            contextMenu.Close();

        MoveTo(hit.point, null);
    }

    public bool RequestAction(
        FarmPlot plot,
        FarmTool tool,
        CropData crop = null)
    {
        if (plot == null)
            return false;

        LastSelectedPlot = plot;

        if (!plot.CanPerform(
            tool,
            crop,
            inventory,
            tank,
            out string reason))
        {
            Show(reason);
            return false;
        }

        if (tool == FarmTool.Plant && hud != null)
            hud.SetCrop(crop);

        if (!MoveTo(plot.InteractionPosition, plot))
            return false;

        queuedTool = tool;
        queuedCrop = crop;

        return true;
    }

    private bool MoveTo(Vector3 position, FarmPlot plot)
    {
        if (!Agent.isActiveAndEnabled || !Agent.isOnNavMesh)
        {
            Show("Character is not on the NavMesh.");
            return false;
        }

        if (!NavMesh.SamplePosition(
            position,
            out NavMeshHit navHit,
            navMeshSearchRadius,
            Agent.areaMask))
        {
            Show("Destination is outside the navigable area.");
            return false;
        }

        Agent.isStopped = false;

        if (!Agent.SetDestination(navHit.position))
        {
            Show("Unable to start the path.");
            return false;
        }

        TargetPlot = plot;
        Destination = navHit.position;
        requestedAt = Time.time;

        Machine.ChangeState(Move);

        return true;
    }

    public bool HasPathProblem()
    {
        if (!Agent.isActiveAndEnabled || !Agent.isOnNavMesh)
            return true;

        if (Agent.pathPending ||
            Time.time - requestedAt < 0.15f)
        {
            return false;
        }

        if (Agent.hasPath &&
            Agent.pathStatus != NavMeshPathStatus.PathComplete)
        {
            return true;
        }

        return !Agent.hasPath && !HasArrived();
    }

    // A altura do pivot e a velocidade residual nao devem bloquear a interacao.
    public bool HasArrived()
    {
        if (!Agent.isActiveAndEnabled ||
            !Agent.isOnNavMesh ||
            Agent.pathPending)
        {
            return false;
        }

        float tolerance = Mathf.Max(
            arrivalDistance,
            Agent.stoppingDistance);

        if (Agent.hasPath)
        {
            return Agent.pathStatus == NavMeshPathStatus.PathComplete &&
                !float.IsInfinity(Agent.remainingDistance) &&
                !float.IsNaN(Agent.remainingDistance) &&
                Agent.remainingDistance <= tolerance;
        }

        Vector3 offset = Agent.nextPosition - Destination;
        offset.y = 0f;

        return offset.sqrMagnitude <= tolerance * tolerance;
    }

    public void PerformQueuedInteraction()
    {
        FarmPlot plot = TargetPlot;
        FarmTool tool = queuedTool;
        CropData crop = queuedCrop;

        StopAndClear();

        if (plot == null)
            return;

        plot.Interact(
            tool,
            crop,
            inventory,
            tank,
            out string message);

        Show(message);
    }

    public void StopAndClear()
    {
        TargetPlot = null;
        queuedCrop = null;
        queuedTool = default;

        if (Agent.isActiveAndEnabled && Agent.isOnNavMesh)
        {
            Agent.isStopped = true;
            Agent.ResetPath();
        }
    }

    public void Show(string message)
    {
        if (hud != null)
            hud.ShowMessage(message);
        else
            Debug.Log(message);
    }
}