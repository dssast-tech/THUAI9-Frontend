using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SoldierHoverTooltip : MonoBehaviour
{
    [SerializeField] private SoldiersData soldiersData;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Vector2 tooltipOffset = new Vector2(16f, 16f);
    [SerializeField] private Vector2 minTooltipSize = new Vector2(260f, 160f);
    [SerializeField] private float maxTooltipWidth = 360f;
    [SerializeField] private float tooltipPaddingX = 12f;
    [SerializeField] private float tooltipPaddingY = 10f;
    [SerializeField] private float tooltipFontSize = 18f;
    [SerializeField] private Font chineseFont;
    [SerializeField] private bool hideWhenPointerOnUI = false;
    [SerializeField] private bool verboseLog = false;

    private int hoveredSoldierId = -1;
    private int lastLoggedSoldierId = int.MinValue;

    private RectTransform tooltipRoot;
    private RectTransform tooltipTextRect;
    private Text tooltipText;
    private LayoutElement tooltipLayout;
    private Camera[] cameraBuffer = new Camera[16];

    private void Awake()
    {
        if (soldiersData == null)
        {
            soldiersData = FindObjectOfType<SoldiersData>();
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
            {
                targetCamera = FindObjectOfType<Camera>();
            }
        }

        EnsureTooltipUI();
    }

    public void SetSoldiersData(SoldiersData data)
    {
        soldiersData = data;
    }

    private void Update()
    {
        if (soldiersData == null)
        {
            soldiersData = FindObjectOfType<SoldiersData>();
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
            {
                targetCamera = FindObjectOfType<Camera>();
            }
        }

        if (soldiersData == null || targetCamera == null)
        {
            hoveredSoldierId = -1;
            return;
        }

        if (hideWhenPointerOnUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            hoveredSoldierId = -1;
            return;
        }

        Camera rayCamera = ResolveRaycastCamera();
        if (rayCamera == null)
        {
            hoveredSoldierId = -1;
            UpdateTooltipVisual();
            return;
        }

        Ray ray = rayCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 500f);
        if (hits != null && hits.Length > 0)
        {
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider == null)
                {
                    continue;
                }

                if (soldiersData.TryGetSoldierIdByModel(hits[i].collider.gameObject, out int soldierId))
                {
                    hoveredSoldierId = soldierId;
                    UpdateTooltipVisual();
                    return;
                }
            }
        }

        hoveredSoldierId = -1;
        UpdateTooltipVisual();
    }

    private Camera ResolveRaycastCamera()
    {
        if (targetCamera != null)
        {
            return targetCamera;
        }

        if (Camera.main != null)
        {
            return Camera.main;
        }

        int count = Camera.GetAllCameras(cameraBuffer);
        if (count <= 0)
        {
            return null;
        }

        Vector3 pointer = Input.mousePosition;
        Camera best = null;
        float bestDepth = float.NegativeInfinity;

        for (int i = 0; i < count; i++)
        {
            Camera cam = cameraBuffer[i];
            if (cam == null || !cam.enabled || !cam.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!cam.pixelRect.Contains(pointer))
            {
                continue;
            }

            if (cam.depth > bestDepth)
            {
                bestDepth = cam.depth;
                best = cam;
            }
        }

        return best;
    }

    private void EnsureTooltipUI()
    {
        if (tooltipRoot != null && tooltipText != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("SoldierHoverTooltipCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        DontDestroyOnLoad(canvasObject);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GraphicRaycaster raycaster = canvasObject.GetComponent<GraphicRaycaster>();
        raycaster.enabled = false;

        GameObject panelObject = new GameObject("TooltipPanel", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        panelObject.transform.SetParent(canvasObject.transform, false);

        tooltipRoot = panelObject.GetComponent<RectTransform>();
        tooltipRoot.anchorMin = new Vector2(0f, 1f);
        tooltipRoot.anchorMax = new Vector2(0f, 1f);
        tooltipRoot.pivot = new Vector2(0f, 1f);

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.85f);
        panelImage.raycastTarget = false;

        tooltipLayout = panelObject.GetComponent<LayoutElement>();
        tooltipLayout.minWidth = minTooltipSize.x;
        tooltipLayout.minHeight = minTooltipSize.y;

        GameObject textObject = new GameObject("TooltipText", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(panelObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(tooltipPaddingX, tooltipPaddingY);
        textRect.offsetMax = new Vector2(-tooltipPaddingX, -tooltipPaddingY);
        tooltipTextRect = textRect;

        tooltipText = textObject.GetComponent<Text>();
        tooltipText.font = ResolveChineseFont();
        tooltipText.fontSize = Mathf.RoundToInt(tooltipFontSize);
        tooltipText.lineSpacing = 1f;
        tooltipText.supportRichText = false;
        tooltipText.alignment = TextAnchor.UpperLeft;
        tooltipText.horizontalOverflow = HorizontalWrapMode.Wrap;
        tooltipText.verticalOverflow = VerticalWrapMode.Overflow;
        tooltipText.color = Color.white;
        tooltipText.raycastTarget = false;

        panelObject.SetActive(false);
    }

    private void UpdateTooltipVisual()
    {
        EnsureTooltipUI();
        if (tooltipRoot == null || tooltipText == null)
        {
            return;
        }

        if (hoveredSoldierId < 0 || soldiersData == null)
        {
            tooltipRoot.gameObject.SetActive(false);
            return;
        }

        if (!soldiersData.TryGetSoldierInfo(hoveredSoldierId, out SoldierRuntimeInfo info) || info == null)
        {
            tooltipRoot.gameObject.SetActive(false);
            return;
        }

        string content =
            $"ID: {info.id}\n" +
            $"阵营: {info.camp}\n" +
            $"类型: {info.soldierType}\n" +
            $"生命: {info.health}\n" +
            $"力量: {info.strength}\n" +
            $"智力: {info.intelligence}\n" +
            $"坐标: ({info.position.x:F1}, {info.position.y:F1}, {info.position.z:F1})\n" +
            $"状态: {(info.isAlive ? "存活" : "阵亡")}";

        tooltipText.font = ResolveChineseFont();
        tooltipText.text = content;
        tooltipRoot.gameObject.SetActive(true);

        tooltipText.fontSize = Mathf.RoundToInt(tooltipFontSize);

        float width = Mathf.Max(minTooltipSize.x, maxTooltipWidth);
        tooltipRoot.sizeDelta = new Vector2(width, minTooltipSize.y);

        Canvas.ForceUpdateCanvases();
        float height = Mathf.Max(minTooltipSize.y, tooltipText.preferredHeight + tooltipPaddingY * 2f);

        tooltipRoot.sizeDelta = new Vector2(width, height);
        if (tooltipTextRect != null)
        {
            tooltipTextRect.offsetMin = new Vector2(tooltipPaddingX, tooltipPaddingY);
            tooltipTextRect.offsetMax = new Vector2(-tooltipPaddingX, -tooltipPaddingY);
        }

        if (tooltipLayout != null)
        {
            tooltipLayout.minWidth = width;
            tooltipLayout.minHeight = height;
        }

        float mouseX = Input.mousePosition.x;
        float mouseY = Screen.height - Input.mousePosition.y;
        float x = mouseX + tooltipOffset.x;
        float y = mouseY + tooltipOffset.y;

        if (x + width > Screen.width)
        {
            x = Screen.width - width - 8f;
        }

        if (y + height > Screen.height)
        {
            y = Screen.height - height - 8f;
        }

        x = Mathf.Max(8f, x);
        y = Mathf.Max(8f, y);

        tooltipRoot.anchoredPosition = new Vector2(x, -y);

        if (verboseLog && lastLoggedSoldierId != hoveredSoldierId)
        {
            lastLoggedSoldierId = hoveredSoldierId;
            Debug.Log($"[SoldierHoverTooltip] 当前悬停士兵ID={hoveredSoldierId}", this);
        }
    }

    private Font ResolveChineseFont()
    {
        if (chineseFont != null)
        {
            return chineseFont;
        }

#if UNITY_EDITOR
        chineseFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Microsoft YaHei.ttc");
        if (chineseFont != null)
        {
            return chineseFont;
        }
#endif

        string[] osFontCandidates =
        {
            "Microsoft YaHei",
            "微软雅黑",
            "SimHei",
            "黑体",
            "SimSun",
            "宋体"
        };

        chineseFont = Font.CreateDynamicFontFromOSFont(osFontCandidates, Mathf.Max(12, Mathf.RoundToInt(tooltipFontSize)));
        if (chineseFont != null)
        {
            return chineseFont;
        }

        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
}
