using UnityEngine;
using UnityEngine.UI;

public class CropWorldIndicator : MonoBehaviour
{


    [SerializeField] Color32 colorRed, colorBlue, colorGreen, colorYellow;
    [Header("Plot Reference")]
    [SerializeField] private FarmPlot plot;
    [SerializeField] private Camera gameCamera;

    [Header("Indicator References")]
    [SerializeField] private GameObject hydrationRoot;
    [SerializeField] private Image hydrationRing, readyMarker, dryMarker;

    [Header("Position Settings")]
    [SerializeField] private Transform positionAnchor;
    [SerializeField] private Vector3 positionOffset = Vector3.zero;
    [SerializeField] private float heightAboveCrop = 0.65f;

    private void Awake()
    {
        if (gameCamera == null)
            gameCamera = Camera.main;
    }

    private void LateUpdate()
    {
        UpdatePosition();
        UpdateIndicator();
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
            hydrationRing.color = colorGreen;
        }
        else if (hydration > 0.25f)
        {
            hydrationRing.color = colorYellow;
        }
        else
        {
            hydrationRing.color = colorRed;
        }
    }
}