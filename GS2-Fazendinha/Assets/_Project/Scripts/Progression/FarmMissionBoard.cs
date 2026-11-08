using System.Collections.Generic;
using UnityEngine;

public class FarmMissionBoard : MonoBehaviour
{
    [Header("System References")]
    [SerializeField] private FarmInventory inventory;
    [SerializeField] private FarmWallet wallet;
    [SerializeField] private FarmTechnologyManager technologies;

    [Header("Community Settings")]
    [SerializeField] private FarmCommunityData[] communities;

    // Mantém a lista anterior como alternativa às comunidades configuradas.
    [SerializeField]
    private List<MissionData> missions =
        new List<MissionData>();

    [Header("Default Mission Timers")]
    [SerializeField, Min(1f)]
    private float defaultMissionDuration = 1050f;

    [SerializeField, Min(1f)]
    private float defaultMissionCooldown = 330f;

    [Header("Demo Settings")]
    [SerializeField, Min(1)]
    private int demoMissionLimit = 10;

    // Guarda o andamento de cada comunidade sem modificar seus assets.
    private class CommunityProgress
    {
        public string name;
        public string focus;

        public readonly List<MissionData> pool =
            new List<MissionData>();

        public MissionData activeMission;

        public float duration;
        public float cooldown;
        public float timeRemaining;

        public bool coolingDown;

        public int nextMissionIndex;
        public int completedDeliveries;
    }

    private readonly List<CommunityProgress> progress =
        new List<CommunityProgress>();

    private readonly HashSet<MissionData> completed =
        new HashSet<MissionData>();

    public int CompletedMissionCount => completed.Count;

    public int CommunityCount => progress.Count;

    public int DemoMissionLimit => Mathf.Max(1, demoMissionLimit);

    public bool IsDemoComplete =>
        CompletedMissionCount >= DemoMissionLimit;

    public int TotalProduceDelivered { get; private set; }

    public int CommunitiesServed
    {
        get
        {
            int count = 0;

            foreach (CommunityProgress community in progress)
            {
                if (community.completedDeliveries > 0)
                    count++;
            }

            return count;
        }
    }

    private void Awake()
    {
        InitializeCommunities();
    }

    private void Update()
    {
        TickMissionTimers(Time.deltaTime);
    }

    // Prepara as comunidades e oferece uma missão inicial para cada uma.
    private void InitializeCommunities()
    {
        if (communities != null && communities.Length > 0)
        {
            InitializeConfiguredCommunities();
        }
        else
        {
            InitializeLegacyCommunities();
        }

        foreach (CommunityProgress community in progress)
            AssignNextMission(community);
    }

    // Usa os nomes, focos, missões e tempos definidos nos novos assets.
    private void InitializeConfiguredCommunities()
    {
        foreach (FarmCommunityData data in communities)
        {
            if (data == null)
                continue;

            CommunityProgress community = CreateProgress(
                data.communityName,
                data.technologyFocus,
                data.missionDurationSeconds,
                data.missionCooldownSeconds);

            AddMissions(community, data.missions);
            progress.Add(community);
        }
    }

    // Agrupa a lista antiga pelo nome da comunidade, preservando seu uso.
    private void InitializeLegacyCommunities()
    {
        if (missions == null)
            return;

        foreach (MissionData mission in missions)
        {
            if (mission == null)
                continue;

            CommunityProgress community =
                FindCommunity(mission.communityName);

            if (community == null)
            {
                community = CreateProgress(
                    mission.communityName,
                    "Local development",
                    defaultMissionDuration,
                    defaultMissionCooldown);

                progress.Add(community);
            }

            if (!community.pool.Contains(mission))
                community.pool.Add(mission);
        }
    }

    private CommunityProgress FindCommunity(string communityName)
    {
        foreach (CommunityProgress community in progress)
        {
            if (community.name == communityName)
                return community;
        }

        return null;
    }

    // Cria os dados de execução com tempos positivos.
    private CommunityProgress CreateProgress(
        string communityName,
        string focus,
        float duration,
        float cooldown)
    {
        return new CommunityProgress
        {
            name = communityName,
            focus = focus,
            duration = Mathf.Max(1f, duration),
            cooldown = Mathf.Max(1f, cooldown)
        };
    }

    // Ignora referências vazias e evita repetir o mesmo asset no conjunto.
    private void AddMissions(
        CommunityProgress community,
        MissionData[] pool)
    {
        if (pool == null)
            return;

        foreach (MissionData mission in pool)
        {
            if (mission != null && !community.pool.Contains(mission))
                community.pool.Add(mission);
        }
    }

    // Atualiza os prazos e cooldowns com tempo de jogo, mesmo com o mapa aberto.
    public void TickMissionTimers(float seconds)
    {
        if (seconds <= 0f || IsDemoComplete)
            return;

        foreach (CommunityProgress community in progress)
        {
            if (community.activeMission == null &&
                !community.coolingDown)
            {
                AssignNextMission(community);

                if (community.activeMission == null)
                    continue;
            }

            AdvanceCommunityTimer(community, seconds);
        }
    }

    // Aproveita o tempo restante quando um intervalo termina durante a atualização.
    private void AdvanceCommunityTimer(
        CommunityProgress community,
        float seconds)
    {
        float remaining = seconds;

        while (remaining > 0f &&
               (community.activeMission != null || community.coolingDown))
        {
            float elapsed = Mathf.Min(
                remaining,
                community.timeRemaining);

            remaining -= elapsed;
            community.timeRemaining -= elapsed;

            if (community.timeRemaining > 0f)
                break;

            if (community.coolingDown)
                AssignNextMission(community);
            else
                BeginCooldown(community);
        }
    }

    // Percorre as missões em ordem circular, pulando as já concluídas.
    private void AssignNextMission(CommunityProgress community)
    {
        community.activeMission = null;
        community.coolingDown = false;
        community.timeRemaining = 0f;

        if (IsDemoComplete || community.pool.Count == 0)
            return;

        for (int offset = 0; offset < community.pool.Count; offset++)
        {
            int index = (
                community.nextMissionIndex + offset
            ) % community.pool.Count;

            MissionData mission = community.pool[index];

            if (mission == null ||
                completed.Contains(mission) ||
                IsOfferedElsewhere(mission, community))
            {
                continue;
            }

            community.activeMission = mission;
            community.nextMissionIndex =
                (index + 1) % community.pool.Count;

            community.timeRemaining = community.duration;
            return;
        }
    }

    // Impede que o mesmo asset seja oferecido em duas comunidades ao mesmo tempo.
    private bool IsOfferedElsewhere(
        MissionData mission,
        CommunityProgress owner)
    {
        foreach (CommunityProgress community in progress)
        {
            if (community != owner &&
                community.activeMission == mission)
            {
                return true;
            }
        }

        return false;
    }

    // Inicia a espera após uma entrega ou após o prazo da missão acabar.
    private void BeginCooldown(CommunityProgress community)
    {
        community.activeMission = null;
        community.coolingDown = true;
        community.timeRemaining = community.cooldown;
    }

    public MissionData GetActiveMission(int index)
    {
        return IsValidCommunity(index) && !IsDemoComplete
            ? progress[index].activeMission
            : null;
    }

    public string GetCommunityName(int index)
    {
        return IsValidCommunity(index)
            ? progress[index].name
            : "Community not configured";
    }

    public float GetTimeRemaining(int index)
    {
        return IsValidCommunity(index) && !IsDemoComplete
            ? progress[index].timeRemaining
            : 0f;
    }

    public bool IsCoolingDown(int index)
    {
        return IsValidCommunity(index) &&
            !IsDemoComplete &&
            progress[index].coolingDown;
    }

    private bool IsValidCommunity(int index)
    {
        return index >= 0 && index < progress.Count;
    }

    // Monta a descrição da missão, da espera ou do encerramento da demo.
    public string Describe(int index)
    {
        if (!IsValidCommunity(index))
            return "Community not configured";

        CommunityProgress community = progress[index];

        string heading =
            community.name +
            "\nFocus: " +
            community.focus +
            "\n\n";

        string summary =
            "\n\nCompleted: " +
            CompletedMissionCount +
            "/" +
            DemoMissionLimit +
            " | Food delivered: " +
            TotalProduceDelivered;

        if (IsDemoComplete)
        {
            return heading +
                "All demo missions completed!" +
                summary;
        }

        if (community.coolingDown)
        {
            return heading +
                "Next request in " +
                FormatTime(community.timeRemaining) +
                summary;
        }

        MissionData mission = community.activeMission;

        if (mission == null)
        {
            return heading +
                "No uncompleted requests configured." +
                summary;
        }

        string cropName = mission.requiredCrop != null
            ? mission.requiredCrop.CropName
            : "Crop not configured";

        string technologyReward = mission.rewardTechnology != null
            ? "\nTechnology: " + mission.rewardTechnology.DisplayName
            : "";

        return heading +
            "Deliver " + mission.requiredAmount + " " + cropName +
            "\nReward: " + mission.rewardCoins + " coins" +
            technologyReward +
            "\nTime left: " + FormatTime(community.timeRemaining) +
            summary;
    }

    // Permite ao HUD conferir se deve habilitar o botão de entrega.
    public bool CanDeliver(int index)
    {
        MissionData mission = GetActiveMission(index);

        if (mission == null ||
            inventory == null ||
            wallet == null ||
            mission.requiredCrop == null ||
            mission.requiredAmount <= 0)
        {
            return false;
        }

        if (!HasValidTechnologyReward(mission))
            return false;

        return inventory.ProduceCount(mission.requiredCrop) >=
            mission.requiredAmount;
    }

    // Tecnologias exclusivas do mercado não podem ser recompensas de missão.
    private bool HasValidTechnologyReward(MissionData mission)
    {
        if (mission.rewardTechnology == null)
            return true;

        return technologies != null &&
            !mission.rewardTechnology.marketExclusive;
    }

    // Valida a entrega antes de consumir os produtos e pagar a recompensa.
    public bool Deliver(int index, out string message)
    {
        if (!IsValidCommunity(index) ||
            inventory == null ||
            wallet == null)
        {
            message = "Mission systems not configured.";
            return false;
        }

        if (IsDemoComplete)
        {
            message = "All demo missions have been completed.";
            return false;
        }

        CommunityProgress community = progress[index];
        MissionData mission = community.activeMission;

        if (community.coolingDown ||
            mission == null ||
            community.timeRemaining <= 0f ||
            completed.Contains(mission))
        {
            message =
                "There is no active request to deliver in this community.";

            return false;
        }

        if (mission.requiredCrop == null ||
            mission.requiredAmount <= 0 ||
            !HasValidTechnologyReward(mission))
        {
            message =
                "Mission requirements or technology reward " +
                "are not configured correctly.";

            return false;
        }

        if (!inventory.TryRemoveProduce(
            mission.requiredCrop,
            mission.requiredAmount))
        {
            message = "Not enough produce for this delivery.";
            return false;
        }

        wallet.AddCoins(mission.rewardCoins);

        bool unlocked = mission.rewardTechnology != null &&
            technologies.Unlock(mission.rewardTechnology);

        RegisterCompletedMission(community, mission);

        message =
            "Delivery completed: +" +
            mission.rewardCoins +
            " coins";

        if (unlocked)
            message += " and " + mission.rewardTechnology.DisplayName;

        return true;
    }

    // Registra uma única conclusão e atualiza os indicadores de abastecimento.
    private void RegisterCompletedMission(
        CommunityProgress community,
        MissionData mission)
    {
        completed.Add(mission);

        community.completedDeliveries++;
        TotalProduceDelivered += mission.requiredAmount;

        BeginCooldown(community);

        if (IsDemoComplete)
            FinishDemoMissions();
    }

    // Encerra as ofertas de todas as comunidades ao atingir o limite da demo.
    private void FinishDemoMissions()
    {
        foreach (CommunityProgress community in progress)
        {
            community.activeMission = null;
            community.coolingDown = false;
            community.timeRemaining = 0f;
        }
    }

    // Converte segundos para o formato minutos:segundos.
    private string FormatTime(float seconds)
    {
        int total = Mathf.CeilToInt(Mathf.Max(0f, seconds));

        return (total / 60).ToString("00") +
            ":" +
            (total % 60).ToString("00");
    }
}