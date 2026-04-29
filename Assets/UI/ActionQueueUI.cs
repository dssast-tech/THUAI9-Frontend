using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;

public class ActionQueueUI : MonoBehaviour
{
    [Serializable]
    private class PortraitMapping
    {
        public int soldierId = -1;
        public string camp;
        public int soldierType = -1;
        public Sprite portrait;
    }

    [Serializable]
    private class ActionIconMapping
    {
        public string actionType;
        public Sprite icon;
    }

    [Header("References")]
    [SerializeField] private RectTransform queueContent;
    [SerializeField] private GameObject itemTemplate;

    [Header("Style")]
    [SerializeField] private Color redCampTint = new Color(1f, 0.55f, 0.55f, 1f);
    [SerializeField] private Color blueCampTint = new Color(0.55f, 0.75f, 1f, 1f);
    [SerializeField] private Color unknownCampTint = new Color(0.9f, 0.9f, 0.9f, 1f);
    [SerializeField] private Color currentOutlineColor = new Color(1f, 0.95f, 0.25f, 1f);
    [SerializeField] private float currentScale = 1.05f;
    [SerializeField] private int maxVisibleCount = 12;
    [SerializeField] private bool disableRaycastOnQueueItems = true;
    [SerializeField] private bool verboseLog = true;
    [SerializeField] private bool tintPortraitByCamp = false;

    [Header("Init Queue")]
    [SerializeField] private bool showInitialQueueOnSetup = true;
    [SerializeField] private int initialQueueCount = 4;
    [SerializeField] private bool autoFitItemSizeToQueueWidth = true;
    [SerializeField] private int fitItemCount = 4;
    [SerializeField] private float minItemSize = 48f;
    [SerializeField] private float maxItemSize = 120f;

    [Header("Optional Asset Mapping")]
    [SerializeField] private List<PortraitMapping> portraitMappings = new List<PortraitMapping>();
    [SerializeField] private List<ActionIconMapping> actionIconMappings = new List<ActionIconMapping>();

    private readonly List<GameObject> activeItems = new List<GameObject>();
    private readonly List<int> activeSoldierIds = new List<int>();
    private List<int> sortedSoldierIds = new List<int>();
    private readonly Dictionary<string, Sprite> resourcePortraitCache = new Dictionary<string, Sprite>();
    private readonly HashSet<int> currentActedSoldierIds = new HashSet<int>();
    private bool useActionHighlighting = false;
    private SoldiersData soldiersDataRef;
    private Vector2 templateSize = new Vector2(64f, 64f);

    private float ResolveHighlightScale()
    {
        return Mathf.Clamp(currentScale, 1f, 1.05f);
    }

    public void Setup(SoldiersData soldiersData)
    {
        soldiersDataRef = soldiersData;

        if (queueContent == null)
        {
            Debug.LogWarning("ActionQueueUI 未绑定 queueContent。", this);
            return;
        }

        if (itemTemplate == null && queueContent.childCount > 0)
        {
            itemTemplate = queueContent.GetChild(0).gameObject;
        }

        if (itemTemplate != null)
        {
            itemTemplate.SetActive(false);

            RectTransform templateRect = itemTemplate.GetComponent<RectTransform>();
            if (templateRect != null)
            {
                templateSize = templateRect.sizeDelta;
                if (templateSize.x <= 0f || templateSize.y <= 0f)
                {
                    templateSize = new Vector2(64f, 64f);
                }
            }

            // Keep only one hidden template under QueueContent to avoid duplicate static items.
            for (int i = queueContent.childCount - 1; i >= 0; i--)
            {
                Transform child = queueContent.GetChild(i);
                if (child.gameObject == itemTemplate)
                {
                    continue;
                }

                Destroy(child.gameObject);
            }
        }

        EnsureQueueLayoutSettings();

        sortedSoldierIds = soldiersDataRef.GetAllSoldierIds(true);
        if (sortedSoldierIds.Count == 0)
        {
            sortedSoldierIds = soldiersDataRef.GetAllSoldierIds(false);
        }
        sortedSoldierIds.Sort();

        ShowInitialQueue();
    }

    public void ShowRoundQueue(int roundIndex, ActionField[] actions)
    {
        ClearQueue();
        useActionHighlighting = true;
        currentActedSoldierIds.Clear();

        if (queueContent == null)
        {
            return;
        }

        if (itemTemplate == null)
        {
            Debug.LogWarning("ActionQueueUI 缺少 itemTemplate，无法绘制队列。", this);
            return;
        }

        if (sortedSoldierIds == null || sortedSoldierIds.Count == 0)
        {
            return;
        }

        HashSet<int> actedSoldierIds = new HashSet<int>();
        if (actions != null)
        {
            for (int i = 0; i < actions.Length; i++)
            {
                ActionField action = actions[i];
                if (!IsValidAction(action) || action.soldierId < 0)
                {
                    continue;
                }

                actedSoldierIds.Add(action.soldierId);
                currentActedSoldierIds.Add(action.soldierId);
            }
        }

        int spawned = 0;
        int visibleCount = Mathf.Min(maxVisibleCount, sortedSoldierIds.Count);
        for (int i = 0; i < visibleCount; i++)
        {
            int soldierId = sortedSoldierIds[i];

            GameObject item = CreateQueueItem(spawned, "ActionQueueItem");
            if (item == null)
            {
                continue;
            }

            string actionType = actedSoldierIds.Contains(soldierId) ? ResolveFirstActionTypeBySoldier(actions, soldierId) : string.Empty;
            ActionField queueEntry = new ActionField
            {
                soldierId = soldierId,
                actionType = actionType
            };

            ConfigureQueueItem(item, queueEntry);

            activeItems.Add(item);
            activeSoldierIds.Add(soldierId);
            spawned++;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(queueContent);
        Canvas.ForceUpdateCanvases();

        if (verboseLog)
        {
            int actionCount = actions != null ? actions.Length : 0;
            Debug.Log($"[ActionQueueUI] roundIndex={roundIndex}, actions={actionCount}, spawned={spawned}, visibleLimit={maxVisibleCount}", this);
        }

        RefreshCurrentVisual();
    }

    private void ShowInitialQueue()
    {
        if (!showInitialQueueOnSetup || soldiersDataRef == null || queueContent == null || itemTemplate == null)
        {
            return;
        }

        useActionHighlighting = false;
        currentActedSoldierIds.Clear();

        if (sortedSoldierIds == null || sortedSoldierIds.Count == 0)
        {
            return;
        }

        ClearQueue();

        int limit = Mathf.Min(Mathf.Max(1, initialQueueCount), Mathf.Min(maxVisibleCount, sortedSoldierIds.Count));
        for (int i = 0; i < limit; i++)
        {
            GameObject item = CreateQueueItem(i, "InitQueueItem");
            if (item == null)
                continue;

            ActionField initAction = new ActionField
            {
                soldierId = sortedSoldierIds[i],
                actionType = "init"
            };

            ConfigureQueueItem(item, initAction);
            activeItems.Add(item);
            activeSoldierIds.Add(sortedSoldierIds[i]);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(queueContent);
        Canvas.ForceUpdateCanvases();
        RefreshCurrentVisual();

        if (verboseLog)
        {
            Debug.Log($"[ActionQueueUI] 初始队列已生成，count={activeItems.Count}", this);
        }
    }

    private GameObject CreateQueueItem(int index, string namePrefix)
    {
        if (itemTemplate == null || queueContent == null)
        {
            return null;
        }

        GameObject item = Instantiate(itemTemplate, queueContent);
        item.name = $"{namePrefix}_{index + 1}";
        item.SetActive(true);
        item.transform.localScale = Vector3.one;

        RectTransform itemRect = item.GetComponent<RectTransform>();
        Vector2 runtimeSize = ResolveRuntimeItemSize();
        if (itemRect != null)
        {
            itemRect.anchorMin = new Vector2(0f, 0.5f);
            itemRect.anchorMax = new Vector2(0f, 0.5f);
            itemRect.pivot = new Vector2(0.5f, 0.5f);
            itemRect.sizeDelta = runtimeSize;
        }

        LayoutElement layoutElement = item.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = item.AddComponent<LayoutElement>();
        }

        layoutElement.ignoreLayout = false;
        layoutElement.preferredWidth = runtimeSize.x;
        layoutElement.preferredHeight = runtimeSize.y;
        return item;
    }

    private void EnsureQueueLayoutSettings()
    {
        if (queueContent == null)
        {
            return;
        }

        HorizontalLayoutGroup horizontalLayoutGroup = queueContent.GetComponent<HorizontalLayoutGroup>();
        if (horizontalLayoutGroup != null)
        {
            horizontalLayoutGroup.childControlWidth = false;
            horizontalLayoutGroup.childControlHeight = false;
            horizontalLayoutGroup.childForceExpandWidth = false;
            horizontalLayoutGroup.childForceExpandHeight = false;
            if (horizontalLayoutGroup.spacing < 6f)
            {
                horizontalLayoutGroup.spacing = 6f;
            }
        }
    }

    private Vector2 ResolveRuntimeItemSize()
    {
        if (!autoFitItemSizeToQueueWidth || queueContent == null)
        {
            return templateSize;
        }

        float contentWidth = queueContent.rect.width;
        if (contentWidth <= 1f)
        {
            return templateSize;
        }

        int desiredCount = Mathf.Max(1, fitItemCount);
        float spacing = 0f;
        float padding = 0f;

        HorizontalLayoutGroup horizontalLayoutGroup = queueContent.GetComponent<HorizontalLayoutGroup>();
        if (horizontalLayoutGroup != null)
        {
            spacing = horizontalLayoutGroup.spacing;
            padding = horizontalLayoutGroup.padding.left + horizontalLayoutGroup.padding.right;
        }

        float available = Mathf.Max(1f, contentWidth - padding - spacing * (desiredCount - 1));
        float side = available / desiredCount;
        side = Mathf.Clamp(side, minItemSize, maxItemSize);

        return new Vector2(side, side);
    }

    public void AdvanceQueue()
    {
        RefreshCurrentVisual();
    }

    public void MoveSoldierToQueueEnd(int soldierId)
    {
        if (soldierId < 0)
        {
            return;
        }

        currentActedSoldierIds.Add(soldierId);
        useActionHighlighting = true;
        RefreshCurrentVisual();
    }

    public void ClearQueue()
    {
        useActionHighlighting = false;
        currentActedSoldierIds.Clear();

        for (int i = 0; i < activeItems.Count; i++)
        {
            if (activeItems[i] != null)
            {
                Destroy(activeItems[i]);
            }
        }

        activeItems.Clear();
        activeSoldierIds.Clear();
    }

    private string ResolveFirstActionTypeBySoldier(ActionField[] actions, int soldierId)
    {
        if (soldierId < 0)
        {
            return "empty";
        }

        if (actions == null)
        {
            return "init";
        }

        for (int i = 0; i < actions.Length; i++)
        {
            ActionField action = actions[i];
            if (action == null || string.IsNullOrEmpty(action.actionType))
            {
                continue;
            }

            if (action.soldierId == soldierId)
            {
                return action.actionType;
            }
        }

        return "init";
    }

    private void ConfigureQueueItem(GameObject item, ActionField action)
    {
        Image rootImage = item.GetComponent<Image>();
        Image actionBadge = FindChildImageByName(item.transform, "ActionBadge");
        Image campBar = FindChildImageByName(item.transform, "CampBar");

        string camp = soldiersDataRef != null ? soldiersDataRef.GetSoldierCamp(action.soldierId) : string.Empty;
        int soldierType = soldiersDataRef != null ? soldiersDataRef.GetSoldierType(action.soldierId) : -1;

        Color campColor = ResolveCampColor(camp);
        Sprite portrait = ResolvePortrait(action.soldierId, camp, soldierType);
        Sprite actionIcon = ResolveActionIcon(action.actionType);

        if (rootImage != null)
        {
            rootImage.color = tintPortraitByCamp ? campColor : Color.white;
            if (portrait != null)
            {
                rootImage.sprite = portrait;
                rootImage.preserveAspect = true;
            }
        }

        TMP_Text label = FindChildTextByName(item.transform, "Label");
        if (label != null)
        {
            label.text = $"#{action.soldierId}";
        }

        if (actionBadge != null)
        {
            if (actionIcon != null)
            {
                actionBadge.sprite = actionIcon;
                actionBadge.enabled = true;
            }
            else
            {
                actionBadge.enabled = false;
            }
        }

        if (campBar != null)
        {
            campBar.color = campColor;
        }

        if (disableRaycastOnQueueItems)
        {
            Graphic[] graphics = item.GetComponentsInChildren<Graphic>(true);
            foreach (Graphic graphic in graphics)
            {
                graphic.raycastTarget = false;
            }
        }
    }

    private void RefreshCurrentVisual()
    {
        for (int i = 0; i < activeItems.Count; i++)
        {
            GameObject item = activeItems[i];
            if (item == null)
            {
                continue;
            }

            if (useActionHighlighting)
            {
                bool acted = i < activeSoldierIds.Count && currentActedSoldierIds.Contains(activeSoldierIds[i]);

                item.transform.localScale = Vector3.one;

                Image rootImg = item.GetComponent<Image>();
                if (rootImg != null)
                {
                    rootImg.rectTransform.DOKill();
                    rootImg.DOKill();

                    if (acted)
                    {
                        float highlightScale = ResolveHighlightScale();
                        rootImg.rectTransform.DOScale(Vector3.one * highlightScale, 0.18f).SetEase(Ease.OutBack);
                        rootImg.DOColor(Color.white, 0.12f);
                    }
                    else
                    {
                        rootImg.rectTransform.DOScale(Vector3.one, 0.12f).SetEase(Ease.OutSine);
                        rootImg.DOColor(Color.white, 0.12f);
                    }
                }

                Outline outline = item.GetComponent<Outline>();
                if (outline == null)
                {
                    outline = item.AddComponent<Outline>();
                    outline.effectDistance = new Vector2(2f, -2f);
                }

                outline.effectColor = currentOutlineColor;
                outline.effectDistance = acted ? new Vector2(3f, -3f) : new Vector2(2f, -2f);
                outline.enabled = acted;
            }
            else
            {
                bool isCurrent = i == 0;

                item.transform.localScale = Vector3.one;

                Image rootImg = item.GetComponent<Image>();
                if (rootImg != null)
                {
                    rootImg.rectTransform.DOKill();
                    rootImg.DOKill();

                    if (isCurrent)
                    {
                        float highlightScale = ResolveHighlightScale();
                        rootImg.rectTransform.DOScale(Vector3.one * highlightScale, 0.18f).SetEase(Ease.OutBack);
                        rootImg.DOColor(Color.white, 0.12f);
                    }
                    else
                    {
                        rootImg.rectTransform.DOScale(Vector3.one, 0.12f).SetEase(Ease.OutSine);
                        Color dim = new Color(1f, 1f, 1f, 0.6f);
                        rootImg.DOColor(dim, 0.12f);
                    }
                }

                Outline outline = item.GetComponent<Outline>();
                if (outline == null)
                {
                    outline = item.AddComponent<Outline>();
                    outline.effectDistance = new Vector2(2f, -2f);
                }

                outline.effectColor = currentOutlineColor;
                outline.effectDistance = isCurrent ? new Vector2(3f, -3f) : new Vector2(2f, -2f);
                outline.enabled = isCurrent;
            }
        }
    }

    private Sprite ResolvePortrait(int soldierId, string camp, int soldierType)
    {
        for (int i = 0; i < portraitMappings.Count; i++)
        {
            PortraitMapping mapping = portraitMappings[i];
            if (mapping == null || mapping.portrait == null)
            {
                continue;
            }

            if (mapping.soldierId >= 0 && mapping.soldierId == soldierId)
            {
                return mapping.portrait;
            }
        }

        for (int i = 0; i < portraitMappings.Count; i++)
        {
            PortraitMapping mapping = portraitMappings[i];
            if (mapping == null || mapping.portrait == null)
            {
                continue;
            }

            bool campMatch = string.IsNullOrEmpty(mapping.camp) || string.Equals(mapping.camp, camp, StringComparison.OrdinalIgnoreCase);
            bool typeMatch = mapping.soldierType == -1 || mapping.soldierType == soldierType;

            if (campMatch && typeMatch)
            {
                return mapping.portrait;
            }
        }

        return ResolvePortraitFromResources(camp, soldierType);
    }

    private Sprite ResolvePortraitFromResources(string camp, int soldierType)
    {
        string campKey = string.IsNullOrEmpty(camp) ? string.Empty : camp.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(campKey))
        {
            return null;
        }

        string typeKey = soldierType == -1 ? string.Empty : soldierType.ToString();
        string shortType = ResolvePortraitTypeSuffix(typeKey);

        List<string> candidates = new List<string>();
        if (!string.IsNullOrEmpty(shortType))
        {
            candidates.Add($"img/{campKey}{shortType}");
        }

        if (!string.IsNullOrEmpty(typeKey))
        {
            candidates.Add($"img/{campKey}{typeKey}");
        }

        candidates.Add($"img/{campKey}");

        for (int i = 0; i < candidates.Count; i++)
        {
            string path = candidates[i];
            if (resourcePortraitCache.TryGetValue(path, out Sprite cached))
            {
                if (cached != null)
                {
                    return cached;
                }

                continue;
            }

            Sprite loaded = Resources.Load<Sprite>(path);
            resourcePortraitCache[path] = loaded;
            if (loaded != null)
            {
                return loaded;
            }
        }

        return null;
    }

    private string ResolvePortraitTypeSuffix(string soldierTypeLower)
    {
        if (string.IsNullOrEmpty(soldierTypeLower))
        {
            return string.Empty;
        }

        if (soldierTypeLower.Contains("warrior") || soldierTypeLower.Contains("zhan"))
        {
            return "zhan";
        }

        if (soldierTypeLower.Contains("archer") || soldierTypeLower.Contains("mage") || soldierTypeLower.Contains("fa"))
        {
            return "fa";
        }

        return string.Empty;
    }

    private Sprite ResolveActionIcon(string actionType)
    {
        if (string.IsNullOrEmpty(actionType))
        {
            return null;
        }

        for (int i = 0; i < actionIconMappings.Count; i++)
        {
            ActionIconMapping mapping = actionIconMappings[i];
            if (mapping == null || mapping.icon == null)
            {
                continue;
            }

            if (string.Equals(mapping.actionType, actionType, StringComparison.OrdinalIgnoreCase))
            {
                return mapping.icon;
            }
        }

        return null;
    }

    private Color ResolveCampColor(string camp)
    {
        if (string.IsNullOrEmpty(camp))
        {
            return unknownCampTint;
        }

        if (string.Equals(camp, "red", StringComparison.OrdinalIgnoreCase))
        {
            return redCampTint;
        }

        if (string.Equals(camp, "blue", StringComparison.OrdinalIgnoreCase))
        {
            return blueCampTint;
        }

        return unknownCampTint;
    }

    private bool IsValidAction(ActionField action)
    {
        return action != null && !string.IsNullOrEmpty(action.actionType);
    }

    private bool TryGetFirstValidActor(ActionField[] actions, out int soldierId)
    {
        soldierId = -1;
        if (actions == null)
        {
            return false;
        }

        for (int i = 0; i < actions.Length; i++)
        {
            ActionField action = actions[i];
            if (!IsValidAction(action))
            {
                continue;
            }

            soldierId = action.soldierId;
            return true;
        }

        return false;
    }

    private Image FindChildImageByName(Transform root, string name)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (string.Equals(child.name, name, StringComparison.OrdinalIgnoreCase))
            {
                return child.GetComponent<Image>();
            }
        }

        return null;
    }

    private TMP_Text FindChildTextByName(Transform root, string name)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (string.Equals(child.name, name, StringComparison.OrdinalIgnoreCase))
            {
                return child.GetComponent<TMP_Text>();
            }
        }

        return null;
    }
}