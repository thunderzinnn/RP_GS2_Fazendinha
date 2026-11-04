using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;

public enum FarmTool { Plant, Water, Harvest }

[RequireComponent(typeof(NavMeshAgent))]
public class PlayerFarmController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Camera gameCamera;
    [SerializeField] private FarmInventory inventory;
    [SerializeField] private FarmWaterTank tank;
    [SerializeField] private FarmHUD hud;

    [Header("Clique e navegacao")]
    [SerializeField] private LayerMask clickableLayers = ~0;
    [SerializeField, Min(0.1f)] private float navMeshSearchRadius = 1.5f;
    [SerializeField, Min(0.1f)] private float arrivalDistance = 0.65f;

    public NavMeshAgent Agent { get; private set; }
    public PlayerStateMachine Machine { get; private set; }
    public PlayerIdleState Idle { get; private set; }
    public PlayerMoveState Move { get; private set; }
    public PlayerInteractState Interact { get; private set; }

    public FarmTool SelectedTool { get; private set; } = FarmTool.Plant;
    public CropData SelectedCrop { get; private set; }
    public FarmPlot LastSelectedPlot { get; private set; }
    public FarmPlot TargetPlot { get; private set; }
    public Vector3 Destination { get; private set; }

    private FarmTool queuedTool;
    private CropData queuedCrop;
    private float requestedAt;

    private void Awake()
    {
        Agent = GetComponent<NavMeshAgent>();
        if (gameCamera == null) gameCamera = Camera.main;

        Machine = new PlayerStateMachine();
        Idle = new PlayerIdleState(this);
        Move = new PlayerMoveState(this);
        Interact = new PlayerInteractState(this);
    }

    private void Start() => Machine.ChangeState(Idle);

    private void Update()
    {
        if (Input.GetMouseButtonDown(0)) HandleClick();
        Machine.Tick();
    }

    private void FixedUpdate() => Machine.FixedTick();

    public void SelectTool(FarmTool tool, CropData crop = null)
    {
        SelectedTool = tool;
        if (tool == FarmTool.Plant) SelectedCrop = crop;
    }

    private void HandleClick()
    {
        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject()) return;
        if (gameCamera == null || !Agent.isOnNavMesh) return;

        Ray ray = gameCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, clickableLayers)) return;

        FarmPlot plot = hit.collider.GetComponentInParent<FarmPlot>();
        LastSelectedPlot = plot;

        if (plot != null && !plot.CanPerform(SelectedTool, SelectedCrop,
            inventory, tank, out string reason))
        {
            Show(reason);
            return;
        }

        Vector3 requested = plot != null ? plot.InteractionPosition : hit.point;
        if (!NavMesh.SamplePosition(requested, out NavMeshHit navHit,
            navMeshSearchRadius, NavMesh.AllAreas))
        {
            Show("Destino fora da area navegavel.");
            return;
        }

        if (!Agent.SetDestination(navHit.position))
        {
            Show("Nao foi possivel iniciar o caminho.");
            return;
        }

        TargetPlot = plot;
        Destination = navHit.position;
        queuedTool = SelectedTool;
        queuedCrop = SelectedCrop;
        requestedAt = Time.time;
        Agent.isStopped = false;
        Machine.ChangeState(Move);
    }

    public bool HasPathProblem()
    {
        if (!Agent.isOnNavMesh) return true;
        if (Agent.pathPending || Time.time - requestedAt < 0.15f) return false;
        if (Agent.hasPath && Agent.pathStatus != NavMeshPathStatus.PathComplete)
            return true;
        return !Agent.hasPath &&
            Vector3.Distance(transform.position, Destination) > arrivalDistance;
    }

    public bool HasArrived()
    {
        if (!Agent.isOnNavMesh || Agent.pathPending) return false;
        float tolerance = Mathf.Max(arrivalDistance, Agent.stoppingDistance);
        if (Agent.hasPath && Agent.remainingDistance > tolerance) return false;
        if (Vector3.Distance(transform.position, Destination) > tolerance) return false;
        return Agent.velocity.sqrMagnitude <= 0.02f;
    }

    public void PerformQueuedInteraction()
    {
        FarmPlot plot = TargetPlot;
        StopAndClear();
        if (plot == null) return;

        bool success = plot.Interact(queuedTool, queuedCrop, inventory, tank,
            out string message);
        Show(message);
    }

    public void StopAndClear()
    {
        TargetPlot = null;
        if (Agent.isOnNavMesh) Agent.ResetPath();
    }

    public void Show(string message)
    {
        if (hud != null) hud.ShowMessage(message);
        else Debug.Log(message);
    }
}
