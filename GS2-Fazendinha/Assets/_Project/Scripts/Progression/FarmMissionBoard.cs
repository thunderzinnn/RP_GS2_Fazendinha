using System.Collections.Generic;
using UnityEngine;

public class FarmMissionBoard : MonoBehaviour
{
    [SerializeField] private FarmInventory inventory;
    [SerializeField] private FarmWallet wallet;
    [SerializeField] private FarmTechnologyManager technologies;

    [SerializeField]
    private List<MissionData> missions =
        new List<MissionData>();

    private readonly HashSet<MissionData> completed =
        new HashSet<MissionData>();

    public string Describe(int index)
    {
        if (index < 0 ||
            index >= missions.Count ||
            missions[index] == null)
        {
            return "Mission not configured";
        }

        MissionData mission = missions[index];

        if (completed.Contains(mission))
            return mission.communityName + ": completed";

        string cropName = mission.requiredCrop != null
            ? mission.requiredCrop.CropName
            : "?";

        string techName = mission.rewardTechnology != null
            ? " + " + mission.rewardTechnology.DisplayName
            : "";

        return mission.communityName + ": " +
            mission.requiredAmount + " " + cropName +
            " | +" + mission.rewardCoins +
            " coins" + techName;
    }

    public bool Deliver(int index, out string message)
    {
        // Confere a entrega antes de consumir produtos e liberar recompensas.
        if (index < 0 ||
            index >= missions.Count ||
            missions[index] == null ||
            inventory == null ||
            wallet == null ||
            technologies == null)
        {
            message = "Mission not configured.";
            return false;
        }

        MissionData mission = missions[index];

        if (completed.Contains(mission))
        {
            message = "This delivery has already been completed.";
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

        bool unlocked =
            technologies.Unlock(mission.rewardTechnology);

        completed.Add(mission);

        message = "Delivery completed: +" +
            mission.rewardCoins + " coins";

        if (unlocked)
        {
            message += " and " +
                mission.rewardTechnology.DisplayName;
        }

        return true;
    }
}