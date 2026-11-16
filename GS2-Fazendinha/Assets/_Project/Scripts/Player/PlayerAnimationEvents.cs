using UnityEngine;

public class PlayerAnimationEvents : MonoBehaviour
{
    private PlayerFarmController player;

    private void Awake()
    {
        player = GetComponentInParent<PlayerFarmController>();
    }

    public void ApplyAction()
    {
        if (player != null)
            player.ApplyQueuedInteraction();
    }
}
