using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ActionQueueUI : MonoBehaviour
{
    [Serializable]
    private class PortraitMapping
    {
        public int soldierId = -1;
        public string camp;
        public string soldierType;
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
    [SerializeField] private float currentScale = 1.08f;
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
    private readonly Dictionary<string, Sprite> resourcePortraitCache = new Dictionary<string, Sprite>();
    private readonly List<List<int>> precomputedRoundQueues = new List<List<int>>();
    private SoldiersData soldiersDataRef;
    private Vector2 templateSize = new Vector2(64f, 64f);

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

        ShowInitialQueue();
    }

    public void PrecomputeRoundQueues(GameRoundField[] rounds)
    {
        precomputedRoundQueues.Clear();

        if (rounds == null || rounds.Length == 0 || soldiersDataRef == null)
        {
            return;
        }

        List<int> baseQueue = soldiersDataRef.GetAllSoldierIds(true);
        if (baseQueue.Count == 0)
        {
            baseQueue = soldiersDataRef.GetAllSoldierIds(false);
        }

        if (baseQueue.Count == 0)
        {
            return;
        }

        List<int> currentQueue = new List<int>(baseQueue);

        for (int i = 0; i < rounds.Length; i++)
        {
            GameRoundField round = rounds[i];
            ActionField[] actions = round != null ? round.actions : null;

            bool hasValidAction = TryGetFirstValidActor(actions, out int firstActorId);
            List<int> roundQueue = new List<int>(currentQueue);

            // 若该回合有行动，则将首个行动士兵放到队首。
            // 若该回合无行动，则保持上一回合队首不变。
            if (hasValidAction)
            {
                EnsureIdInQueue(roundQueue, firstActorId);
                RotateQueueToFront(roundQueue, firstActorId);
            }

            precomputedRoundQueues.Add(roundQueue);

            currentQueue = new List<int>(roundQueue);
        }

        if (verboseLog)
        {
            Debug.Log($"[ActionQueueUI] 已预计算回合队列，rounds={precomputedRoundQueues.Count}", this);
        }
    }

    public void ShowRoundQueue(int roundIndex, ActionField[] actions)
    {
        ClearQueue();

        if (queueContent == null)
        {
            return;
        }

        if (itemTemplate == null)
        {
            Debug.LogWarning("ActionQueueUI 缺少 itemTemplate，无法绘制队列。", this);
            return;
        }

        if (!TryGetPrecomputedQueue(roundIndex, out List<int> queueSoldierIds))
        {
            if (verboseLog)
            {
                Debug.LogWarning($"[ActionQueueUI] 未找到预计算队列，roundIndex={roundIndex}", this);
            }
            return;
        }

        if (queueSoldierIds == null || queueSoldierIds.Count == 0)
        {
            return;
        }

        int spawned = 0;
        for (int i = 0; i < queueSoldierIds.Count; i++)
        {
            int soldierId = queueSoldierIds[i];

            if (spawned >= maxVisibleCount)
            {
                break;
            }

            GameObject item = CreateQueueItem(spawned, "ActionQueueItem");
            if (item == null)
            {
                continue;
            }

            string actionType = ResolveFirstActionTypeBySoldier(actions, soldierId);
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
            Debug.Log($"[ActionQueueUI] roundIndex={roundIndex}, actions={actionCount}, queueSoldiers={queueSoldierIds.Count}, spawned={spawned}, visibleLimit={maxVisibleCount}", this);
        }

        RefreshCurrentVisual();
    }

    private void ShowInitialQueue()
    {
        if (!showInitialQueueOnSetup || soldiersDataRef == null || queueContent == null || itemTemplate == null)
        {
            return;
        }

        List<int> soldierIds = soldiersDataRef.GetAllSoldierIds(true);
        if (soldierIds.Count == 0)
        {
            soldierIds = soldiersDataRef.GetAllSoldierIds(false);
        }

        if (soldierIds.Count == 0)
        {
            return;
        }

        ClearQueue();

        int limit = Mathf.Min(Mathf.Max(1, initialQueueCount), Mathf.Min(maxVisibleCount, soldierIds.Count));
        for (int i = 0; i < limit; i++)
        {
            GameObject item = CreateQueueItem(i, "InitQueueItem");
            if (item == null)
            {
                continue;
            }

            ActionField initAction = new ActionField
            {
                soldierId = soldierIds[i],
                actionType = "init"
            };

            ConfigureQueueItem(item, initAction);
            activeItems.Add(item);
            activeSoldierIds.Add(soldierIds[i]);
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
        if (activeItems.Count == 0)
        {
            return;
        }

        GameObject first = activeItems[0];
        activeItems.RemoveAt(0);
        activeItems.Add(first);
        first.transform.SetAsLastSibling();

        if (activeSoldierIds.Count > 0)
        {
            int firstId = activeSoldierIds[0];
            activeSoldierIds.RemoveAt(0);
            activeSoldierIds.Add(firstId);
        }

        RefreshCurrentVisual();
    }

    public void MoveSoldierToQueueEnd(int soldierId)
    {
        if (activeItems.Count == 0)
        {
            return;
        }

        int index = activeSoldierIds.IndexOf(soldierId);
        if (index < 0)
        {
            AdvanceQueue();
            return;
        }

        GameObject actedItem = activeItems[index];
        activeItems.RemoveAt(index);
        activeItems.Add(actedItem);
        actedItem.transform.SetAsLastSibling();

        int actedId = activeSoldierIds[index];
        activeSoldierIds.RemoveAt(index);
        activeSoldierIds.Add(actedId);

        RefreshCurrentVisual();
    }

    public void ClearQueue()
    {
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
        string soldierType = soldiersDataRef != null ? soldiersDataRef.GetSoldierType(action.soldierId) : string.Empty;

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

            bool isCurrent = i == 0;
            item.transform.localScale = isCurrent ? Vector3.one * currentScale : Vector3.one;

            Outline outline = item.GetComponent<Outline>();
            if (outline == null)
            {
                outline = item.AddComponent<Outline>();
                outline.effectDistance = new Vector2(2f, -2f);
            }

            outline.effectColor = currentOutlineColor;
            outline.enabled = isCurrent;
        }
    }

    private Sprite ResolvePortrait(int soldierId, string camp, string soldierType)
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
            bool typeMatch = string.IsNullOrEmpty(mapping.soldierType) || string.Equals(mapping.soldierType, soldierType, StringComparison.OrdinalIgnoreCase);

            if (campMatch && typeMatch)
            {
                return mapping.portrait;
            }
        }

        return ResolvePortraitFromResources(camp, soldierType);
    }

    private Sprite ResolvePortraitFromResources(string camp, string soldierType)
    {
        string campKey = string.IsNullOrEmpty(camp) ? string.Empty : camp.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(campKey))
        {
            return null;
        }

        string typeKey = string.IsNullOrEmpty(soldierType) ? string.Empty : soldierType.Trim().ToLowerInvariant();
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

    private bool TryGetPrecomputedQueue(int roundIndex, out List<int> queueSoldierIds)
    {
        queueSoldierIds = null;

        if (roundIndex < 0 || roundIndex >= precomputedRoundQueues.Count)
        {
            return false;
        }

        List<int> cached = precomputedRoundQueues[roundIndex];
        if (cached == null || cached.Count == 0)
        {
            return false;
        }

        queueSoldierIds = new List<int>(cached);
        return true;
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
            if (action == null || string.IsNullOrEmpty(action.actionType))
            {
                continue;
            }

            soldierId = action.soldierId;
            return true;
        }

        return false;
    }

    private void EnsureIdInQueue(List<int> queue, int soldierId)
    {
        if (queue == null)
        {
            return;
        }

        if (!queue.Contains(soldierId))
        {
            queue.Add(soldierId);
        }
    }

    private void RotateQueueToFront(List<int> queue, int soldierId)
    {
        if (queue == null || queue.Count == 0)
        {
            return;
        }

        int index = queue.IndexOf(soldierId);
        if (index <= 0)
        {
            return;
        }

        List<int> rotated = new List<int>(queue.Count);
        for (int i = index; i < queue.Count; i++)
        {
            rotated.Add(queue[i]);
        }

        for (int i = 0; i < index; i++)
        {
            rotated.Add(queue[i]);
        }

        queue.Clear();
        queue.AddRange(rotated);
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