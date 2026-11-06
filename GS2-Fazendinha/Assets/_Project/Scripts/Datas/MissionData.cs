using UnityEngine;

[CreateAssetMenu(fileName = "NewMission", menuName = "Golden Root Quick Farmer/Missions")]
// Define a entrega e suas recompensas usando uma referencia de CropData.
public class MissionData : ScriptableObject
{
    public string communityName = "New Community";
    public CropData requiredCrop;

    [Min(1)] public int requiredAmount = 5;
    [Min(0)] public int rewardCoins = 30;

    public TechnologyData rewardTechnology;
}