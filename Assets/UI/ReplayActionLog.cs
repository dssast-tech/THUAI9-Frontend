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

    private const string DefaultPlaceholderText = "回合行动日志（点击 Play/Next 后开始）";

    private readonly Queue<string> roundLogs = new Queue<string>();
    private ScrollRect scrollRect;

    private void Awake()
    {
        if (logText == null)
        {
            Debug.LogError("ReplayActionLog 未绑定 logText，请在 Inspector 手动指定 TMP 组件。", this);
            return;
        }

        scrollRect = logText.GetComponentInParent<ScrollRect>(true);

        if (string.IsNullOrEmpty(logText.text))
        {
            logText.text = DefaultPlaceholderText;
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
            ScrollToBottom();
        }
    }

    private void ScrollToBottom()
    {
        scrollRect.StopMovement();
        Canvas.ForceUpdateCanvases();

        if (scrollRect.content != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
        }

        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;
    }
}