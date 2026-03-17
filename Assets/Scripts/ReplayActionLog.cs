using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ReplayActionLog : MonoBehaviour
{
    [SerializeField] private TMP_Text logText;
    [SerializeField] private int maxRoundsToKeep = 8;
    [SerializeField] private bool clearOnPlayStart = true;
    [Header("Display Optimization")]
    [SerializeField] private bool optimizeLogView = true;
    [SerializeField] private float logFontSize = 18f;
    [SerializeField] private Vector2 logBoxSize = new Vector2(420f, 320f);
    [SerializeField] private float scrollSensitivity = 25f;
    [SerializeField] private Color boxBackgroundColor = new Color(0f, 0f, 0f, 0.35f);
    [SerializeField] private Color boxBorderColor = new Color(1f, 1f, 1f, 0.85f);

    private const string DefaultPlaceholderText = "回合行动日志（点击 Play/Next 后开始）";

    private readonly Queue<string> roundLogs = new Queue<string>();
    private ScrollRect scrollRect;
    private RectTransform logViewportRect;

    private void Awake()
    {
        if (logText == null)
        {
            Debug.LogError("ReplayActionLog 未绑定 logText，请在 Inspector 手动指定 TMP 组件。", this);
            return;
        }

        if (string.IsNullOrEmpty(logText.text))
        {
            logText.text = DefaultPlaceholderText;
        }

        if (optimizeLogView)
        {
            ConfigureLogView();
        }
    }

    public bool CanDisplayLogs()
    {
        return logText != null;
    }

    public void Setup(SoldiersData soldiersDataScript)
    {
        if (clearOnPlayStart)
        {
            ClearLog();
        }
    }

    public void ClearLog()
    {
        roundLogs.Clear();
        RefreshText();
    }

    public void ShowRoundActions(int roundNumber, IReadOnlyList<string> actionDescriptions)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"第 {roundNumber} 回合");

        if (actionDescriptions == null || actionDescriptions.Count == 0)
        {
            builder.Append("- 本回合无行动");
        }
        else
        {
            int lineIndex = 1;
            foreach (var line in actionDescriptions)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                builder.AppendLine($"{lineIndex}. {line.Trim()}");
                lineIndex++;
            }

            if (lineIndex == 1)
            {
                builder.Append("- 本回合无有效行动");
            }
        }

        roundLogs.Enqueue(builder.ToString().TrimEnd());
        while (roundLogs.Count > maxRoundsToKeep)
        {
            roundLogs.Dequeue();
        }

        RefreshText();
    }

    private void RefreshText()
    {
        if (logText == null)
        {
            return;
        }

        logText.text = roundLogs.Count == 0
            ? DefaultPlaceholderText
            : string.Join("\n\n", roundLogs.ToArray());

        if (scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }

    private void ConfigureLogView()
    {
        TextMeshProUGUI tmpUGUI = logText as TextMeshProUGUI;
        if (tmpUGUI == null)
        {
            Debug.LogWarning("ReplayActionLog 的 logText 不是 TextMeshProUGUI，无法启用滚轮与边界框优化。", this);
            return;
        }

        tmpUGUI.fontSize = Mathf.Max(8f, logFontSize);
        tmpUGUI.enableWordWrapping = true;
        tmpUGUI.overflowMode = TextOverflowModes.Overflow;
        tmpUGUI.margin = new Vector4(8f, 8f, 8f, 8f);

        RectTransform contentRect = tmpUGUI.rectTransform;
        RectTransform originalParent = contentRect.parent as RectTransform;
        if (originalParent == null)
        {
            Debug.LogWarning("ReplayActionLog 未找到可用父级，无法配置滚动视图。", this);
            return;
        }

        RectTransform viewportRect = originalParent.GetComponentInChildren<RectTransform>(true);
        if (contentRect.parent != null && contentRect.parent.name == "ReplayLogViewport")
        {
            viewportRect = contentRect.parent as RectTransform;
        }
        else
        {
            GameObject viewportObject = new GameObject("ReplayLogViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(Outline), typeof(ScrollRect));
            viewportRect = viewportObject.GetComponent<RectTransform>();
            viewportRect.SetParent(originalParent, false);
            viewportRect.SetSiblingIndex(contentRect.GetSiblingIndex());

            viewportRect.anchorMin = contentRect.anchorMin;
            viewportRect.anchorMax = contentRect.anchorMax;
            viewportRect.pivot = contentRect.pivot;
            viewportRect.anchoredPosition = contentRect.anchoredPosition;
            viewportRect.localScale = Vector3.one;

            contentRect.SetParent(viewportRect, false);
        }

        logViewportRect = viewportRect;
        logViewportRect.sizeDelta = logBoxSize;

        Image bgImage = logViewportRect.GetComponent<Image>();
        if (bgImage == null)
        {
            bgImage = logViewportRect.gameObject.AddComponent<Image>();
        }
        bgImage.color = boxBackgroundColor;

        Outline outline = logViewportRect.GetComponent<Outline>();
        if (outline == null)
        {
            outline = logViewportRect.gameObject.AddComponent<Outline>();
        }
        outline.effectColor = boxBorderColor;
        outline.effectDistance = new Vector2(1f, -1f);

        if (logViewportRect.GetComponent<RectMask2D>() == null)
        {
            logViewportRect.gameObject.AddComponent<RectMask2D>();
        }

        ContentSizeFitter fitter = contentRect.GetComponent<ContentSizeFitter>();
        if (fitter == null)
        {
            fitter = contentRect.gameObject.AddComponent<ContentSizeFitter>();
        }
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, contentRect.sizeDelta.y);

        scrollRect = logViewportRect.GetComponent<ScrollRect>();
        if (scrollRect == null)
        {
            scrollRect = logViewportRect.gameObject.AddComponent<ScrollRect>();
        }

        scrollRect.content = contentRect;
        scrollRect.viewport = logViewportRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = true;
        scrollRect.scrollSensitivity = scrollSensitivity;
    }
}