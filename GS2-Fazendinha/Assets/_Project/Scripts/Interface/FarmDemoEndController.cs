using UnityEngine;
using UnityEngine.SceneManagement;

public class FarmDemoEndController : MonoBehaviour
{
    [Header("Demo References")]
    [SerializeField] private FarmMissionBoard missions;
    [SerializeField] private GameObject endPanel;
    [SerializeField] private FarmUIController menus;

    private bool shown;
    private float previousTimeScale = 1f;
    public bool IsOpen => endPanel != null && endPanel.activeInHierarchy;

    private void Update()
    {
        CheckDemoCompletion();
    }

    private void CheckDemoCompletion()
    {
        if (shown || missions == null || !missions.IsDemoComplete || endPanel == null)
            return;
        if (menus != null && menus.IsPaused) return;
        if (FarmSaveManager.Instance != null && !FarmSaveManager.Instance.IsReady) return;

        shown = true;
        if (FarmSaveManager.Instance != null) FarmSaveManager.Instance.SaveAutomatically();
        previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        endPanel.SetActive(true);
        endPanel.transform.SetAsLastSibling();
    }

    public void ContinueFarming()
    {
        PlayClick();
        endPanel.SetActive(false);
        Time.timeScale = previousTimeScale;
    }

    public void ReturnToMainMenu()
    {
        PlayClick();
        if (FarmSaveManager.Instance != null) FarmSaveManager.Instance.SaveGame();
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    private void PlayClick()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();
    }

    private void OnDestroy()
    {
        if (endPanel != null && endPanel.activeSelf) Time.timeScale = previousTimeScale;
    }
}
