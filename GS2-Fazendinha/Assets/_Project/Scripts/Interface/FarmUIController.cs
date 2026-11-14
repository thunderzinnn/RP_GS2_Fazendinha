using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class FarmUIController : MonoBehaviour
{
    public static bool TutorialCompleted { get; set; }

    [Header("Scene Settings")]
    [SerializeField] private bool mainMenu;
    [SerializeField] private string tutorialScene = "Tutorial";

    [Header("Menu Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject optionsPanel;
    [SerializeField] private TMP_Text pauseStatusText;
    [SerializeField] private GameObject resetConfirmationPanel;

    [Header("Audio Controls")]
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider effectsSlider;
    [SerializeField] private TMP_Text musicValueText;
    [SerializeField] private TMP_Text effectsValueText;

    private float previousTimeScale = 1f;
    public bool IsPaused { get; private set; }

    private void Start()
    {
        InitializeMenus();
    }

    private void Update()
    {
        HandleMenuInput();
    }

    private void HandleMenuInput()
    {
        var demoEnd = GetComponent<FarmDemoEndController>();
        if (demoEnd != null && demoEnd.IsOpen) return;
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        if (resetConfirmationPanel != null && resetConfirmationPanel.activeSelf) CancelResetProgress();
        else if (optionsPanel != null && optionsPanel.activeSelf) CloseOptions();
        else if (!mainMenu && IsPaused) ResumeGame();
        else if (!mainMenu) PauseGame();
    }

    private void InitializeMenus()
    {
        if (optionsPanel != null) optionsPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(mainMenu);
        if (resetConfirmationPanel != null) resetConfirmationPanel.SetActive(false);
        var audio = AudioManager.Instance;
        if (musicSlider != null) musicSlider.SetValueWithoutNotify(audio != null ? audio.MusicVolume : 0.7f);
        if (effectsSlider != null) effectsSlider.SetValueWithoutNotify(audio != null ? audio.EffectsVolume : 1f);
        RefreshVolumeLabels();
    }

    public void PlayGame()
    {
        PlayClick();
        Time.timeScale = 1f;
        TutorialCompleted = false;
        SceneManager.LoadScene(tutorialScene);
    }

    public void PauseGame()
    {
        if (mainMenu || IsPaused) return;
        PlayClick();
        PauseTime();
        if (pausePanel != null) pausePanel.SetActive(true);
    }

    private void PauseTime()
    {
        previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        IsPaused = true;
        if (pauseStatusText != null) pauseStatusText.text = "Esc to resume";
        if (FarmSaveManager.Instance != null) FarmSaveManager.Instance.SaveAutomatically();
    }

    public void ResumeGame()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayMenuClose();
        if (optionsPanel != null) optionsPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        Time.timeScale = previousTimeScale;
        IsPaused = false;
    }

    public void OpenOptions()
    {
        PlayClick();
        if (!mainMenu && !IsPaused) PauseTime();
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(true);
    }

    public void CloseOptions()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayMenuClose();
        FarmSaveManager.SaveAudioSettings();
        PlayerPrefs.Save();
        if (optionsPanel != null) optionsPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(mainMenu);
        if (!mainMenu && pausePanel != null) pausePanel.SetActive(true);
    }

    public void SetMusicVolume(float value)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.SetMusicVolume(value);
        RefreshVolumeLabels();
    }

    public void SetEffectsVolume(float value)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.SetEffectsVolume(value);
        RefreshVolumeLabels();
    }

    private void RefreshVolumeLabels()
    {
        if (musicValueText != null && musicSlider != null) musicValueText.text = Mathf.RoundToInt(musicSlider.value * 100f) + "%";
        if (effectsValueText != null && effectsSlider != null) effectsValueText.text = Mathf.RoundToInt(effectsSlider.value * 100f) + "%";
    }

    public void QuitGame()
    {
        PlayClick();
        if (FarmSaveManager.Instance != null) FarmSaveManager.Instance.SaveGame();
        Time.timeScale = 1f;
        Application.Quit();
    }

    public void ReturnToMainMenu()
    {
        PlayClick();
        if (FarmSaveManager.Instance != null) FarmSaveManager.Instance.SaveGame();
        IsPaused = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    public void ShowResetProgress()
    {
        if (!mainMenu || resetConfirmationPanel == null) return;
        PlayClick();
        if (optionsPanel != null) optionsPanel.SetActive(false);
        resetConfirmationPanel.SetActive(true);
        resetConfirmationPanel.transform.SetAsLastSibling();
    }

    public void CancelResetProgress()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayMenuClose();
        if (resetConfirmationPanel != null) resetConfirmationPanel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(true);
    }

    public void ConfirmResetProgress()
    {
        if (!mainMenu || FarmSaveManager.Instance == null) return;
        PlayClick();
        FarmSaveManager.Instance.ResetProgress();
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    public void SaveGame()
    {
        PlayClick();
        var save = FarmSaveManager.Instance;
        if (save != null && save.IsReady && save.CanSaveProgress)
        {
            save.SaveGame();
            if (pauseStatusText != null) pauseStatusText.text = "Progress saved.";
        }
    }

    private void PlayClick()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();
    }

    private void OnDestroy()
    {
        if (IsPaused) Time.timeScale = previousTimeScale;
    }
}
