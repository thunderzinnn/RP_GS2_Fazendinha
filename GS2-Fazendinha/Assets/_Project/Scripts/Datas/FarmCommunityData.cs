using UnityEngine;

// Guarda as missões, o foco e os tempos de uma comunidade.
[CreateAssetMenu(
    fileName = "NewCommunity",
    menuName = "Golden Root Quick Farmer/Communities")]
public class FarmCommunityData : ScriptableObject
{
    [Header("Community Information")]
    public string communityName = "New Community";

    // Descreve o foco das tecnologias oferecidas pela comunidade.
    public string technologyFocus = "Local development";

    [Header("Mission Pool")]

    // Missões disponíveis para a rotação desta comunidade.
    public MissionData[] missions;

    [Header("Mission Timers")]

    // Tempo disponível para entregar uma missão: 17 minutos e 30 segundos.
    [Min(1f)] public float missionDurationSeconds = 1050f;

    // Espera entre as ofertas de missão: 5 minutos e 30 segundos.
    [Min(1f)] public float missionCooldownSeconds = 330f;
}