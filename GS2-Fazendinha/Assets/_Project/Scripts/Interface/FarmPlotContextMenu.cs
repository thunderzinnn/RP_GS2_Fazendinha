using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class FarmPlotContextMenu : MonoBehaviour
{
    [Header("System References")]
    [SerializeField] private PlayerFarmController player;
    [SerializeField] private CropData[] availableCrops;

    [Header("Growth Information")]
    [SerializeField] private TechnologyData growthTimeTechnology;

    [Header("Panel and Text")]
    [SerializeField] private RectTransform menuPanel;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text statusText;

    [Header("Planting")]
    [SerializeField] private GameObject plantRow;
    [SerializeField] private Button plantButton;
    [SerializeField] private TMP_Dropdown cropDropdown;
    [SerializeField] private TMP_Text seedCountText;

    [Header("Other Actions")]
    [SerializeField] private Button waterButton;
    [SerializeField] private Button harvestButton;
    [SerializeField] private TMP_Text waterButtonText;

    private readonly List<CropData> cropsInDropdown =
        new List<CropData>();

    private FarmPlot selectedPlot;
    private Canvas canvas;
    private int openedFrame = -1;

    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();

        PopulateDropdown();

        if (cropDropdown != null)
            cropDropdown.onValueChanged.AddListener(OnCropChanged);

        if (menuPanel != null)
            menuPanel.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (cropDropdown != null)
            cropDropdown.onValueChanged.RemoveListener(OnCropChanged);
    }

    private void Update()
    {
        HandleDismissInput();
        RefreshSelectedPlot();
    }

    // Atualiza as informações enquanto o menu do terreno estiver aberto.
    private void RefreshSelectedPlot()
    {
        if (selectedPlot == null ||
            selectedPlot.Machine == null ||
            player == null ||
            menuPanel == null ||
            !menuPanel.gameObject.activeSelf)
        {
            return;
        }

        bool growing =
            selectedPlot.Machine.CurrentState == selectedPlot.Planted ||
            selectedPlot.Machine.CurrentState == selectedPlot.Growing;

        bool empty =
            selectedPlot.Machine.CurrentState == selectedPlot.Empty;

        bool grown =
            selectedPlot.Machine.CurrentState == selectedPlot.Grown;

        UpdatePlotText(empty, growing, grown);
        UpdateActionButtons(empty, growing, grown);
    }

    // Reúne os textos do terreno e mostra o progresso quando permitido.
    private void UpdatePlotText(bool empty, bool growing, bool grown)
    {
        if (titleText != null)
        {
            titleText.text = selectedPlot.CurrentCrop != null
                ? selectedPlot.CurrentCrop.CropName
                : "Empty plot";
        }

        if (statusText == null)
            return;

        string description = "State: " + selectedPlot.StateName;

        if (!empty)
        {
            description += " | Water: " +
                selectedPlot.Hydration.ToString("0") + "%";
        }

        if ((growing || grown) && CanShowGrowth())
        {
            // Arredonda para baixo para mostrar 100% somente ao concluir.
            int percentage = Mathf.FloorToInt(
                selectedPlot.GrowthPercentage);

            description += "\nGrowth: " + percentage + "%";
        }

        if (growing && selectedPlot.Hydration <= 0f)
        {
            description += "\nDry! Water within " +
                selectedPlot.DryTimeRemaining.ToString("0.0") + "s";
        }

        statusText.text = description;
    }

    // Confere se o mesmo asset de tecnologia já foi desbloqueado.
    private bool CanShowGrowth()
    {
        return growthTimeTechnology != null &&
            growthTimeTechnology.effect == TechnologyEffect.ShowGrowingTime &&
            FarmTechnologyManager.Instance != null &&
            FarmTechnologyManager.Instance.Has(growthTimeTechnology);
    }

    // Atualiza os botões conforme o estado e os recursos disponíveis.
    private void UpdateActionButtons(bool empty, bool growing, bool grown)
    {
        if (plantRow != null)
            plantRow.SetActive(empty);

        if (seedCountText != null)
            seedCountText.gameObject.SetActive(empty);

        UpdatePlantOptions();

        ConfigureButton(
            waterButton,
            growing,
            growing && selectedPlot.CanPerform(
                FarmTool.Water,
                null,
                player.Inventory,
                player.Tank,
                out _));

        ConfigureButton(
            harvestButton,
            grown,
            grown && selectedPlot.CanPerform(
                FarmTool.Harvest,
                null,
                player.Inventory,
                player.Tank,
                out _));

        if (waterButtonText != null &&
            growing &&
            selectedPlot.CurrentCrop != null &&
            player.Tank != null)
        {
            waterButtonText.text = "Water (" +
                player.Tank.WaterCost(
                    selectedPlot.CurrentCrop).ToString("0.#") +
                " water)";
        }
    }

    // Fecha o menu por Escape ou clique fora da interface.
    private void HandleDismissInput()
    {
        if (menuPanel == null ||
            !menuPanel.gameObject.activeSelf)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
            return;
        }

        if (Input.GetMouseButtonDown(0) &&
            Time.frameCount != openedFrame)
        {
            // Ignora cliques na interface.
            if (EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (!RectTransformUtility.RectangleContainsScreenPoint(
                menuPanel,
                Input.mousePosition,
                GetUICamera()))
            {
                Close();
            }
        }
    }

    // Preenche a seleção sem repetir culturas ou incluir referências vazias.
    private void PopulateDropdown()
    {
        cropsInDropdown.Clear();

        if (cropDropdown == null)
            return;

        List<string> options = new List<string>();

        if (availableCrops != null)
        {
            foreach (CropData crop in availableCrops)
            {
                if (crop == null || cropsInDropdown.Contains(crop))
                    continue;

                cropsInDropdown.Add(crop);
                options.Add(crop.CropName);
            }
        }

        cropDropdown.ClearOptions();
        cropDropdown.AddOptions(options);
        cropDropdown.SetValueWithoutNotify(0);
        cropDropdown.RefreshShownValue();
    }

    private CropData SelectedCrop()
    {
        if (cropDropdown == null || cropsInDropdown.Count == 0)
            return null;

        int index = cropDropdown.value;

        return index >= 0 && index < cropsInDropdown.Count
            ? cropsInDropdown[index]
            : null;
    }

    private void OnCropChanged(int index)
    {
        UpdatePlantOptions();
    }

    // Confere as sementes e informa por que o plantio está bloqueado.
    private void UpdatePlantOptions()
    {
        CropData crop = SelectedCrop();

        bool empty = selectedPlot != null &&
            selectedPlot.Machine != null &&
            selectedPlot.Machine.CurrentState == selectedPlot.Empty;

        string reason = "";

        bool canPlant = empty && crop != null && player != null &&
            selectedPlot.CanPerform(
                FarmTool.Plant,
                crop,
                player.Inventory,
                player.Tank,
                out reason);

        if (plantButton != null)
            plantButton.interactable = canPlant;

        if (seedCountText != null)
        {
            if (crop == null)
            {
                seedCountText.text = "No crop configured";
            }
            else if (player == null || player.Inventory == null)
            {
                seedCountText.text = "Inventory not configured";
            }
            else
            {
                seedCountText.text = "Seeds for " +
                    crop.CropName + ": " +
                    player.Inventory.SeedCount(crop);
            }

            if (!canPlant && !string.IsNullOrEmpty(reason))
                seedCountText.text += "\n" + reason;
        }
    }

    public void Show(FarmPlot plot, Vector2 screenPosition)
    {
        if (plot == null ||
            plot.Machine == null ||
            player == null ||
            menuPanel == null)
        {
            Debug.LogWarning(
                "Context menu is missing required references.");

            return;
        }

        selectedPlot = plot;
        openedFrame = Time.frameCount;

        menuPanel.gameObject.SetActive(true);
        menuPanel.SetAsLastSibling();

        RefreshSelectedPlot();

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(menuPanel);

        PositionMenu(screenPosition);
    }

    private void ConfigureButton(
        Button button,
        bool visible,
        bool enabled)
    {
        if (button == null)
            return;

        button.gameObject.SetActive(visible);
        button.interactable = enabled;
    }

    private Camera GetUICamera()
    {
        if (canvas == null ||
            canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return canvas.worldCamera;
    }

    // Posiciona o menu próximo ao clique e limita sua posição ao painel pai.
    private void PositionMenu(Vector2 screenPosition)
    {
        RectTransform parent =
            menuPanel.parent as RectTransform;

        if (parent == null)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parent,
            screenPosition,
            GetUICamera(),
            out Vector2 localPoint))
        {
            return;
        }

        float x = localPoint.x +
            parent.rect.width * parent.pivot.x + 12f;

        float y = localPoint.y -
            parent.rect.height * (1f - parent.pivot.y) - 12f;

        float maxX = Mathf.Max(
            0f,
            parent.rect.width - menuPanel.rect.width);

        float minY = Mathf.Min(
            0f,
            -parent.rect.height + menuPanel.rect.height);

        menuPanel.anchoredPosition = new Vector2(
            Mathf.Clamp(x, 0f, maxX),
            Mathf.Clamp(y, minY, 0f));
    }

    // Solicita a ação ao jogador antes de fechar o menu.
    private void Confirm(FarmTool tool, CropData crop = null)
    {
        if (selectedPlot == null || player == null)
            return;

        if (player.RequestAction(selectedPlot, tool, crop))
            Close();
    }

    public void PlantSelected()
    {
        Confirm(FarmTool.Plant, SelectedCrop());
    }

    public void Water()
    {
        Confirm(FarmTool.Water);
    }

    public void Harvest()
    {
        Confirm(FarmTool.Harvest);
    }

    public void Close()
    {
        selectedPlot = null;

        if (menuPanel != null)
            menuPanel.gameObject.SetActive(false);
    }
}