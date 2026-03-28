using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Actions : MonoBehaviour
{
    // A simple representation of an action being played out
    // Since UI and animations must be clear, we will visualize paths with LineRenderer and attacks with projectiles or floating text.

    private SoldiersData soldiersDataRef;
    private List<GameObject> visualEffects = new List<GameObject>();
    private ActionQueueUI actionQueueUI;

    public void Setup(SoldiersData sd)
    {
        soldiersDataRef = sd;
    }

    public void SetActionQueueUI(ActionQueueUI queueUI)
    {
        actionQueueUI = queueUI;
    }

    public IEnumerator PlayActions(ActionField[] actions)
    {
        ClearEffects();
        if (actions == null || actions.Length == 0) yield break;

        foreach (var act in actions)
        {
            if (act == null || string.IsNullOrEmpty(act.actionType)) continue;

            string type = act.actionType.ToLower();

            if (type == "movement")
            {
                yield return StartCoroutine(HandleMovement(act));
            }
            else if (type == "attack")
            {
                yield return StartCoroutine(HandleAttack(act));
            }
            else if (type == "ability")
            {
                yield return StartCoroutine(HandleAbility(act));
            }

            if (actionQueueUI != null)
            {
                actionQueueUI.MoveSoldierToQueueEnd(act.soldierId);
            }
        }

        yield return new WaitForSeconds(0.5f); // buffer before round ends visually
        ClearEffects();
    }

    public List<string> BuildRoundActionDescriptions(ActionField[] actions)
    {
        List<string> lines = new List<string>();
        if (actions == null || actions.Length == 0)
        {
            return lines;
        }

        foreach (var act in actions)
        {
            if (act == null)
            {
                continue;
            }

            lines.AddRange(BuildActionDescriptionLines(act));
        }

        return lines;
    }

    private IEnumerator HandleMovement(ActionField act)
    {
        if (act.path == null || act.path.Length == 0) yield break;

        // Draw path with a LineRenderer
        GameObject pathObj = new GameObject($"Path_{act.soldierId}");
        visualEffects.Add(pathObj);

        LineRenderer lr = pathObj.AddComponent<LineRenderer>();
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = Color.green;
        lr.endColor = Color.cyan;
        lr.startWidth = 0.2f;
        lr.endWidth = 0.2f;

        lr.positionCount = act.path.Length;
        Vector3[] worldPath = new Vector3[act.path.Length];
        for (int i = 0; i < act.path.Length; i++)
        {
            // Position on top of the tile
            Vector3 pos = new Vector3(act.path[i].x, act.path[i].y + 0.5f, act.path[i].z);
            lr.SetPosition(i, pos);
            worldPath[i] = pos;
        }

        GameObject soldier = soldiersDataRef.GetSoldierModel(act.soldierId);
        if (soldier != null && worldPath.Length > 0)
        {
            float moveSpeed = 5.0f; // Units per second
            foreach (Vector3 targetPos in worldPath)
            {
                while (Vector3.Distance(soldier.transform.position, targetPos) > 0.05f)
                {
                    soldier.transform.position = Vector3.MoveTowards(soldier.transform.position, targetPos, moveSpeed * Time.deltaTime);
                    yield return null; // Wait for next frame
                }
                soldier.transform.position = targetPos; // Ensure exact snap
            }
        }
        else
        {
            // Wait a bit to show the path if soldier not found (fallback)
            yield return new WaitForSeconds(0.5f);
        }
    }

    private IEnumerator HandleAttack(ActionField act)
    {
        if (act.damageDealt != null)
        {
            foreach (var dmg in act.damageDealt)
            {
            }
        }

        yield return new WaitForSeconds(0.5f);
    }

    private List<string> BuildActionDescriptionLines(ActionField act)
    {
        List<string> lines = new List<string>();
        string actorName = GetSoldierDisplayName(act.soldierId);
        string type = string.IsNullOrEmpty(act.actionType) ? "unknown" : act.actionType.ToLower();

        if (type == "movement")
        {
            if (act.path == null || act.path.Length == 0)
            {
                lines.Add($"{actorName} 尝试移动，但没有路径信息");
                return lines;
            }

            PositionField start = act.path[0];
            PositionField end = act.path[act.path.Length - 1];
            lines.Add($"{actorName} 从 {FormatPosition(start)} 移动到 {FormatPosition(end)}，共 {act.path.Length - 1} 步");
            return lines;
        }

        if (type == "attack")
        {
            if (act.damageDealt == null || act.damageDealt.Length == 0)
            {
                lines.Add($"{actorName} 发动了攻击");
                return lines;
            }

            foreach (var dmg in act.damageDealt)
            {
                if (dmg == null)
                {
                    continue;
                }

                string targetName = GetSoldierDisplayName(dmg.targetId);
                lines.Add($"{actorName} 攻击 {targetName}，造成 {dmg.damage} 点伤害");
            }

            if (lines.Count == 0)
            {
                lines.Add($"{actorName} 发动了攻击");
            }

            return lines;
        }

        if (type == "ability")
        {
            string abilityName = string.IsNullOrEmpty(act.ability) ? "技能" : act.ability;
            string targetInfo = act.targetPosition == null ? string.Empty : $"，目标点 {FormatPosition(act.targetPosition)}";
            lines.Add($"{actorName} 使用了 {abilityName}{targetInfo}");

            if (act.damageDealt != null)
            {
                foreach (var dmg in act.damageDealt)
                {
                    if (dmg == null)
                    {
                        continue;
                    }

                    string targetName = GetSoldierDisplayName(dmg.targetId);
                    lines.Add($"{abilityName} 对 {targetName} 造成 {dmg.damage} 点伤害");
                }
            }

            return lines;
        }

        lines.Add($"{actorName} 执行了未知行动：{act.actionType}");
        return lines;
    }

    private string GetSoldierDisplayName(int soldierId)
    {
        return soldiersDataRef != null
            ? soldiersDataRef.GetSoldierDisplayName(soldierId)
            : $"角色 #{soldierId}";
    }

    private string FormatPosition(PositionField position)
    {
        return $"({position.x}, {position.y}, {position.z})";
    }

    private IEnumerator HandleAbility(ActionField act)
    {
        if (act.targetPosition != null)
        {
            // Draw a temporary impact sphere
            GameObject impact = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            impact.transform.position = new Vector3(act.targetPosition.x, act.targetPosition.y + 0.5f, act.targetPosition.z);
            impact.transform.localScale = Vector3.one * 1.5f;
            impact.GetComponent<Renderer>().material.color = new Color(1, 0, 1, 0.5f);
            visualEffects.Add(impact);
        }

        yield return new WaitForSeconds(0.8f);
    }

    private void ClearEffects()
    {
        foreach (var ef in visualEffects)
        {
            if (ef != null) Destroy(ef);
        }
        visualEffects.Clear();
    }
}
