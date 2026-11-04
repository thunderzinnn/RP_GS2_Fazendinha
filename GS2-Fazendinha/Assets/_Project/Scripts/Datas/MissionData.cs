using UnityEngine;

[CreateAssetMenu(fileName = "NewMission", menuName = "Golden Root Quick Farmer/Missons")]
public class MissionData : ScriptableObject
{
    public string communityName = "";
    public CropData requiredCrop;
    [Min(1)] public int requiredAmount = 5;
    [Min(0)] public int rewardCoins = 30;
    public TechnologyData rewardTechnology;
}
