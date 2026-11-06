using UnityEngine;
using UnityEngine.UI;

public class CropWorldIndicator : MonoBehaviour
{
    [Header("Plot Reference")]
    [SerializeField] private FarmPlot plot;
    [SerializeField] private Camera gameCamera;

    [Header("Indicator References")]
    [SerializeField] private GameObject hydrationRoot;
    [SerializeField] private Image hydrationRing;
    [SerializeField] private Image readyMarker;
    [SerializeField] private Image dryMarker;

    [Header("Position Settings")]

    // Opcional: crie um objeto vazio como filho do terreno e arraste aqui.
    // Mova esse objeto em X, Y e Z para escolher a origem do indicador.
    [SerializeField] private Transform positionAnchor;

    // Desloca o conjunto inteiro nos eixos X, Y e Z do mundo.
    [SerializeField] private Vector3 positionOffset = Vector3.zero;

    // Usado somente quando Position Anchor estiver vazio.
    [SerializeField] private float heightAboveCrop = 0.65f;

    [Header("Individual Element Positions")]

    // Posicoes locais dentro do Canvas. Permitem separar o anel e os icones.
    [SerializeField] private Vector3 hydrationOffset = Vector3.zero;
    [SerializeField] private Vector3 readyMarkerOffset = Vector3.zero;
    [SerializeField] private Vector3 dryMarkerOffset = Vector3.zero;

    [Header("Floating Animation")]
    [SerializeField] private bool floatMarkers = true;
    [SerializeField] private float markerFloatHeight = 6f;
    [SerializeField] private float markerFloatSpeed = 3f;

    private void Awake()
    {
        if (gameCamera == null)
            gameCamera = Camera.main;
    }

    private void LateUpdate()
    {
        UpdatePosition();
        UpdateIndicator();
        UpdateElementPositions();
    }

    // Usa a origem escolhida ou calcula uma posicao acima da plantacao.
    private void UpdatePosition()
    {
        if (gameCamera != null)
            transform.rotation = gameCamera.transform.rotation;

        if (positionAnchor != null &&
            positionAnchor != transform &&
            !positionAnchor.IsChildOf(transform))
        {
            transform.position = positionAnchor.position + positionOffset;
            return;
        }

        if (plot == null || gameCamera == null)
            return;

        transform.position = GetAutomaticPosition() + positionOffset;
    }

    // Calcula uma posicao acima do contorno da planta visto pela camera.
    private Vector3 GetAutomaticPosition()
    {
        Renderer[] models = plot.GetComponentsInChildren<Renderer>();

        if (models.Length == 0)
            return plot.transform.position + Vector3.up * heightAboveCrop;

        Bounds bounds = models[0].bounds;

        foreach (Renderer model in models)
            bounds.Encapsulate(model.bounds);

        float left = float.PositiveInfinity;
        float right = float.NegativeInfinity;
        float top = float.NegativeInfinity;

        for (int x = 0; x < 2; x++)
        {
            for (int y = 0; y < 2; y++)
            {
                for (int z = 0; z < 2; z++)
                {
                    Vector3 direction = new Vector3(
                        x == 0 ? -1 : 1,
                        y == 0 ? -1 : 1,
                        z == 0 ? -1 : 1);

                    Vector3 corner = bounds.center +
                        Vector3.Scale(bounds.extents, direction);

                    Vector3 screen =
                        gameCamera.WorldToScreenPoint(corner);

                    left = Mathf.Min(left, screen.x);
                    right = Mathf.Max(right, screen.x);
                    top = Mathf.Max(top, screen.y);
                }
            }
        }

        Vector3 center =
            gameCamera.WorldToScreenPoint(bounds.center);

        float margin = gameCamera.WorldToScreenPoint(
            bounds.center +
            gameCamera.transform.up * heightAboveCrop).y - center.y;

        Vector3 screenPosition = new Vector3(
            (left + right) * 0.5f,
            top + margin,
            center.z);

        return gameCamera.ScreenToWorldPoint(screenPosition);
    }

    // Atualiza o preenchimento, as cores e os simbolos de cada estado.
    private void UpdateIndicator()
    {
        bool growing = plot != null &&
            plot.Machine != null &&
            (plot.Machine.CurrentState == plot.Planted ||
             plot.Machine.CurrentState == plot.Growing);

        bool ready = plot != null &&
            plot.Machine != null &&
            plot.Machine.CurrentState == plot.Grown;

        bool dry = growing && plot.Hydration <= 0f;

        if (hydrationRoot != null)
            hydrationRoot.SetActive(growing);

        if (readyMarker != null)
            readyMarker.gameObject.SetActive(ready);

        if (dryMarker != null)
            dryMarker.gameObject.SetActive(dry);

        if (!growing || hydrationRing == null)
            return;

        float hydration = Mathf.Clamp01(plot.Hydration / 100f);

        hydrationRing.fillAmount = hydration;

        if (hydration > 0.5f)
        {
            hydrationRing.color = new Color32(102, 225, 76, 255);
        }
        else if (hydration > 0.25f)
        {
            hydrationRing.color = new Color32(255, 202, 58, 255);
        }
        else
        {
            hydrationRing.color = new Color32(244, 75, 49, 255);
        }
    }

    // Aplica as posicoes individuais sem apagar os ajustes dos campos.
    private void UpdateElementPositions()
    {
        float floatingOffset = floatMarkers
            ? Mathf.Sin(Time.time * markerFloatSpeed) * markerFloatHeight
            : 0f;

        Vector3 animationOffset = Vector3.up * floatingOffset;

        if (hydrationRoot != null)
            hydrationRoot.transform.localPosition = hydrationOffset;

        if (readyMarker != null)
        {
            readyMarker.rectTransform.localPosition =
                readyMarkerOffset + animationOffset;
        }

        if (dryMarker != null)
        {
            dryMarker.rectTransform.localPosition =
                dryMarkerOffset + animationOffset;
        }
    }
}