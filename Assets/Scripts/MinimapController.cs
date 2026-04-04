using UnityEngine;
using UnityEngine.UI;

public class MinimapController : MonoBehaviour
{
    [Header("Minimap UI")]
    [SerializeField] private Vector2 minimapSize = new Vector2(220f, 220f);
    [SerializeField] private Vector2 minimapMargin = new Vector2(20f, 20f);
    [SerializeField] private Color frameColor = new Color(0f, 0f, 0f, 0.75f);

    [Header("Minimap Camera")]
    [SerializeField] private float heightPadding = 12f;
    [SerializeField] private float orthographicPadding = 1.5f;
    [SerializeField] private Color minimapBackgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f);

    [Header("Target")]
    [SerializeField] private int targetSoldierId = -1;
    [SerializeField] private bool autoPickRedCamp = true;
    [SerializeField] private bool fallbackToFirstAlive = true;

    private MapData mapData;
    private SoldiersData soldiersData;
    private Camera minimapCamera;
    private RenderTexture minimapTexture;
    private RectTransform minimapRect;
    private RectTransform markerRect;
    private Transform currentTarget;
    private int currentTargetId = -1;

    public void Setup(MapData mapDataScript, SoldiersData soldiersDataScript)
    {
        mapData = mapDataScript;
        soldiersData = soldiersDataScript;

        EnsureUI();
        EnsureCamera();
        RebuildMinimapView();
        RefreshTarget();
    }

    private void LateUpdate()
    {
        if (currentTarget == null || !currentTarget.gameObject.activeInHierarchy)
        {
            RefreshTarget();
        }

        UpdateMarkerPosition();
    }

    public void SetTargetSoldierId(int soldierId)
    {
        targetSoldierId = soldierId;
        RefreshTarget();
    }

    public void RefreshTarget()
    {
        currentTarget = null;
        currentTargetId = -1;

        if (targetSoldierId >= 0)
        {
            GameObject fixedTarget = soldiersData.GetSoldierModel(targetSoldierId);
            if (fixedTarget != null && fixedTarget.activeInHierarchy)
            {
                currentTarget = fixedTarget.transform;
                currentTargetId = targetSoldierId;
                return;
            }
        }

        if (autoPickRedCamp)
        {
            var ids = soldiersData.GetAllSoldierIds(true);
            for (int i = 0; i < ids.Count; i++)
            {
                int id = ids[i];
                string camp = soldiersData.GetSoldierCamp(id);
                if (!string.IsNullOrEmpty(camp) && camp.ToLower() == "red")
                {
                    GameObject model = soldiersData.GetSoldierModel(id);
                    if (model != null && model.activeInHierarchy)
                    {
                        currentTarget = model.transform;
                        currentTargetId = id;
                        return;
                    }
                }
            }
        }

        if (fallbackToFirstAlive)
        {
            var ids = soldiersData.GetAllSoldierIds(true);
            if (ids.Count > 0)
            {
                GameObject model = soldiersData.GetSoldierModel(ids[0]);
                if (model != null && model.activeInHierarchy)
                {
                    currentTarget = model.transform;
                    currentTargetId = ids[0];
                }
            }
        }
    }

    public void RebuildMinimapView()
    {
        if (!mapData.TryGetMapBounds(out Bounds bounds))
        {
            return;
        }

        Vector3 center = bounds.center;
        float size = Mathf.Max(bounds.extents.x, bounds.extents.z) + orthographicPadding;

        minimapCamera.orthographic = true;
        minimapCamera.orthographicSize = Mathf.Max(1f, size);
        minimapCamera.transform.position = new Vector3(center.x, bounds.max.y + heightPadding, center.z);
        minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    private void UpdateMarkerPosition()
    {
        if (currentTarget == null)
        {
            markerRect.gameObject.SetActive(false);
            return;
        }

        Vector3 viewport = minimapCamera.WorldToViewportPoint(currentTarget.position);
        bool inFront = viewport.z >= 0f;
        bool inRange = viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;

        if (!inFront || !inRange)
        {
            markerRect.gameObject.SetActive(false);
            return;
        }

        markerRect.gameObject.SetActive(true);

        Vector2 size = minimapRect.rect.size;
        float x = (viewport.x - 0.5f) * size.x;
        float y = (viewport.y - 0.5f) * size.y;
        markerRect.anchoredPosition = new Vector2(x, y);
    }

    private void EnsureUI()
    {
        if (minimapRect != null)
        {
            return;
        }

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("MinimapCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        GameObject panel = new GameObject("MinimapPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);
        minimapRect = panel.GetComponent<RectTransform>();
        minimapRect.anchorMin = new Vector2(1f, 1f);
        minimapRect.anchorMax = new Vector2(1f, 1f);
        minimapRect.pivot = new Vector2(1f, 1f);
        minimapRect.sizeDelta = minimapSize;
        minimapRect.anchoredPosition = new Vector2(-minimapMargin.x, -minimapMargin.y);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = frameColor;
        panelImage.raycastTarget = false;

        GameObject rawImageObject = new GameObject("MinimapImage", typeof(RectTransform), typeof(RawImage));
        rawImageObject.transform.SetParent(panel.transform, false);

        RectTransform rawRect = rawImageObject.GetComponent<RectTransform>();
        rawRect.anchorMin = new Vector2(0f, 0f);
        rawRect.anchorMax = new Vector2(1f, 1f);
        rawRect.offsetMin = new Vector2(4f, 4f);
        rawRect.offsetMax = new Vector2(-4f, -4f);

        RawImage rawImage = rawImageObject.GetComponent<RawImage>();
        rawImage.raycastTarget = false;

        GameObject marker = new GameObject("TargetMarker", typeof(RectTransform), typeof(Image));
        marker.transform.SetParent(rawImageObject.transform, false);
        markerRect = marker.GetComponent<RectTransform>();
        markerRect.anchorMin = new Vector2(0.5f, 0.5f);
        markerRect.anchorMax = new Vector2(0.5f, 0.5f);
        markerRect.pivot = new Vector2(0.5f, 0.5f);
        markerRect.sizeDelta = new Vector2(10f, 10f);

        Image markerImage = marker.GetComponent<Image>();
        markerImage.color = Color.yellow;
        markerImage.raycastTarget = false;

        minimapRect = rawRect;
    }

    private void EnsureCamera()
    {
        if (minimapCamera == null)
        {
            GameObject cameraObject = new GameObject("MinimapCamera");
            minimapCamera = cameraObject.AddComponent<Camera>();
        }

        if (minimapTexture == null || minimapTexture.width != Mathf.RoundToInt(minimapSize.x) || minimapTexture.height != Mathf.RoundToInt(minimapSize.y))
        {
            if (minimapTexture != null)
            {
                minimapTexture.Release();
            }

            minimapTexture = new RenderTexture(Mathf.RoundToInt(minimapSize.x), Mathf.RoundToInt(minimapSize.y), 16, RenderTextureFormat.ARGB32);
            minimapTexture.name = "MinimapRT";
            minimapTexture.Create();
        }

        minimapCamera.targetTexture = minimapTexture;
        minimapCamera.clearFlags = CameraClearFlags.SolidColor;
        minimapCamera.backgroundColor = minimapBackgroundColor;
        minimapCamera.nearClipPlane = 0.1f;
        minimapCamera.farClipPlane = 500f;
        minimapCamera.depth = -50f;

        RawImage rawImage = minimapRect.GetComponent<RawImage>();
        if (rawImage != null)
        {
            rawImage.texture = minimapTexture;
        }
    }

    private void OnDestroy()
    {
        if (minimapTexture != null)
        {
            minimapTexture.Release();
            Destroy(minimapTexture);
            minimapTexture = null;
        }

        if (minimapCamera != null)
        {
            Destroy(minimapCamera.gameObject);
        }
    }
}
