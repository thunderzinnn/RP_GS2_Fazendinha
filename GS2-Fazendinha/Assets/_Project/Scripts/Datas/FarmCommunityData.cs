using UnityEngine;

[CreateAssetMenu(
    fileName = "NewCommunity",
    menuName = "Golden Root Quick Farmer/Communities")]
public class FarmCommunityData : ScriptableObject
{
    [Header("Save Settings")]
    [SerializeField] private string saveId;
    public string SaveId => string.IsNullOrEmpty(saveId) ? name : saveId;

    [Header("Community Information")]
    public string communityName = "New Community";

    public string technologyFocus = "Local development";

    [Header("Mission Pool")]

    public MissionData[] missions;

    [Header("Mission Timers")]

    [Min(1f)] public float missionDurationSeconds = 1050f;

    [Min(1f)] public float missionCooldownSeconds = 330f;
}
