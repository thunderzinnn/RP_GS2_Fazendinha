using UnityEngine;

[CreateAssetMenu(fileName = "NewMission", menuName = "Golden Root Quick Farmer/Missions")]
public class MissionData : ScriptableObject
{
    [Header("Save Settings")]
    [SerializeField] private string saveId;
    public string SaveId => string.IsNullOrEmpty(saveId) ? name : saveId;

    public string communityName = "New Community";
    public CropData requiredCrop;

    [Min(1)] public int requiredAmount = 5;
    [Min(0)] public int rewardCoins = 30;

    public TechnologyData rewardTechnology;
}
