using UnityEngine;

public class FarmPanelLayout : MonoBehaviour
{
    [SerializeField] private RectTransform canvasRect;
    [SerializeField] private RectTransform[] panels = new RectTransform[0];
    [SerializeField] private float safeMargin = 24f;
    private Vector2 previousSize;

    private void LateUpdate()
    {
        FitPanelsIfNeeded();
    }

    private void FitPanelsIfNeeded()
    {
        if (canvasRect == null || canvasRect.rect.size == previousSize) return;
        previousSize = canvasRect.rect.size;
        foreach (var panel in panels)
        {
            if (panel == null || panel.rect.width <= 0f || panel.rect.height <= 0f) continue;
            float scale = Mathf.Min(1f, (previousSize.x - safeMargin * 2f) / panel.rect.width,
                (previousSize.y - safeMargin * 2f) / panel.rect.height);
            panel.localScale = Vector3.one * Mathf.Max(0.1f, scale);
        }
    }
}
