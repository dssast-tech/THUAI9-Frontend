using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class ReplayActionLog : MonoBehaviour
{
    [SerializeField] private TMP_Text logText;
    [SerializeField] private int maxRoundsToKeep = 8;
    [SerializeField] private bool clearOnPlayStart = true;

    private readonly Queue<string> roundLogs = new Queue<string>();
    private SoldiersData soldiersData;

    private void Awake()
    {
        if (logText == null)
        {
            logText = GetComponent<TMP_Text>();
        }

        if (logText != null && string.IsNullOrEmpty(logText.text))
        {
            logText.text = "回合行动日志（点击 Play/Next 后开始）";
        }
    }

    public void Setup(SoldiersData soldiersDataScript)
    {
        soldiersData = soldiersDataScript;
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

    public void ShowRoundActions(int roundNumber, ActionField[] actions)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"第 {roundNumber} 回合");

        if (actions == null || actions.Length == 0)
        {
            builder.Append("- 本回合无行动");
        }
        else
        {
            int lineIndex = 1;
            foreach (var action in actions)
            {
                if (action == null)
                {
                    continue;
                }

                builder.AppendLine($"{lineIndex}. {FormatAction(action)}");
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

        logText.text = string.Join("\n\n", roundLogs.ToArray());
    }

    private string FormatAction(ActionField action)
    {
        string soldierName = soldiersData != null ? soldiersData.GetSoldierDisplayName(action.soldierId) : $"角色 #{action.soldierId}";
        string actionType = string.IsNullOrEmpty(action.actionType) ? "unknown" : action.actionType.ToLower();

        switch (actionType)
        {
            case "movement":
                return FormatMovement(soldierName, action);
            case "attack":
                return FormatAttack(soldierName, action);
            case "ability":
                return FormatAbility(soldierName, action);
            default:
                return $"{soldierName} 执行了未知行动：{action.actionType}";
        }
    }

    private string FormatMovement(string soldierName, ActionField action)
    {
        if (action.path == null || action.path.Length == 0)
        {
            return $"{soldierName} 尝试移动，但没有路径信息";
        }

        PositionField start = action.path[0];
        PositionField end = action.path[action.path.Length - 1];
        return $"{soldierName} 从 {FormatPosition(start)} 移动到 {FormatPosition(end)}，共 {action.path.Length - 1} 步";
    }

    private string FormatAttack(string soldierName, ActionField action)
    {
        string damageInfo = FormatDamage(action.damageDealt);
        return string.IsNullOrEmpty(damageInfo)
            ? $"{soldierName} 发动了攻击"
            : $"{soldierName} 发动攻击，{damageInfo}";
    }

    private string FormatAbility(string soldierName, ActionField action)
    {
        string abilityName = string.IsNullOrEmpty(action.ability) ? "技能" : action.ability;
        string targetInfo = action.targetPosition != null ? $"，目标点 {FormatPosition(action.targetPosition)}" : string.Empty;
        string damageInfo = FormatDamage(action.damageDealt);

        if (string.IsNullOrEmpty(damageInfo))
        {
            return $"{soldierName} 使用了 {abilityName}{targetInfo}";
        }

        return $"{soldierName} 使用了 {abilityName}{targetInfo}，{damageInfo}";
    }

    private string FormatDamage(TargetDamageField[] damages)
    {
        if (damages == null || damages.Length == 0)
        {
            return string.Empty;
        }

        List<string> entries = new List<string>();
        foreach (var damage in damages)
        {
            if (damage == null)
            {
                continue;
            }

            string targetName = soldiersData != null ? soldiersData.GetSoldierDisplayName(damage.targetId) : $"角色 #{damage.targetId}";
            entries.Add($"对 {targetName} 造成 {damage.damage} 点伤害");
        }

        return string.Join("；", entries);
    }

    private string FormatPosition(PositionField position)
    {
        return $"({position.x}, {position.y}, {position.z})";
    }
}