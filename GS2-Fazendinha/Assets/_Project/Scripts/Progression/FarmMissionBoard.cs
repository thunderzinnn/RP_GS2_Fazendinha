using System.Collections.Generic;
using UnityEngine;

public class FarmMissionBoard : MonoBehaviour
{
    [SerializeField] private FarmInventory inventory;
    [SerializeField] private FarmWallet wallet;
    [SerializeField] private FarmTechnologyManager technologies;
    [SerializeField] private List<MissionData> missions = new List<MissionData>();

    private readonly HashSet<MissionData> completed = new HashSet<MissionData>();

    public string Describe(int index)
    {
        if (index < 0 || index >= missions.Count || missions[index] == null)
            return "Missao nao configurada";

        MissionData mission = missions[index];
        if (completed.Contains(mission)) return mission.communityName + ": concluida";

        string cropName = mission.requiredCrop != null ? mission.requiredCrop.CropName : "?";
        string techName = mission.rewardTechnology != null
            ? " + " + mission.rewardTechnology.DisplayName : "";
        return mission.communityName + ": " + mission.requiredAmount + " " + cropName +
            " | +" + mission.rewardCoins + " moedas" + techName;
    }

    public bool Deliver(int index, out string message)
    {
        if (index < 0 || index >= missions.Count || missions[index] == null ||
            inventory == null || wallet == null || technologies == null)
        {
            message = "Missao nao configurada.";
            return false;
        }

        MissionData mission = missions[index];
        if (completed.Contains(mission))
        {
            message = "Essa entrega ja foi concluida.";
            return false;
        }
        if (!inventory.TryRemoveProduce(mission.requiredCrop, mission.requiredAmount))
        {
            message = "Voce ainda nao tem produtos suficientes.";
            return false;
        }

        wallet.AddCoins(mission.rewardCoins);
        bool unlocked = technologies.Unlock(mission.rewardTechnology);
        completed.Add(mission);

        message = "Entrega realizada: +" + mission.rewardCoins + " moedas";
        if (unlocked) message += " e " + mission.rewardTechnology.DisplayName;
        return true;
    }
}
