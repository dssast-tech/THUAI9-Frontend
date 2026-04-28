using System;
using System.Collections.Generic;
using DG.Tweening;
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

    public Sequence PlayActions(ActionField[] actions, Action onComplete = null)
    {
        ClearEffects();
        if (actions == null || actions.Length == 0)
        {
            onComplete?.Invoke();
            return null;
        }

        Sequence roundSequence = DOTween.Sequence();

        foreach (var act in actions)
        {
            if (act == null || string.IsNullOrEmpty(act.actionType)) continue;

            string type = act.actionType.ToLower();
            int actorId = act.soldierId;

            if (type == "movement")
            {
                roundSequence.Append(BuildMovementSequence(act));
            }
            else if (type == "attack")
            {
                // roundSequence.AppendInterval(0.5f);
            }
            else if (type == "ability")
            {
                // roundSequence.AppendCallback(() =>
                // {
                //     if (act.targetPosition == null)
                //     {
                //         return;
                //     }

                //     GameObject impact = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                //     impact.transform.position = new Vector3(act.targetPosition.x, act.targetPosition.y * 0.5f, act.targetPosition.z);
                //     impact.transform.localScale = Vector3.one * 1.5f;
                //     impact.GetComponent<Renderer>().material.color = new Color(1, 0, 1, 0.5f);
                //     visualEffects.Add(impact);
                // });
                // roundSequence.AppendInterval(0.8f);
            }

            roundSequence.AppendCallback(() =>
            {
                if (actionQueueUI != null)
                {
                    actionQueueUI.MoveSoldierToQueueEnd(actorId);
                }
            });
        }

        roundSequence.AppendInterval(0.5f); // buffer before round ends visually
        roundSequence.OnComplete(() =>
        {
            ClearEffects();
            onComplete?.Invoke();
        });
        roundSequence.Play();
        return roundSequence;
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

    private Sequence BuildMovementSequence(ActionField act)
    {
        Sequence movementSequence = DOTween.Sequence();
        if (act.path == null || act.path.Length == 0)
        {
            return movementSequence;
        }

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
            Vector3 pos = new Vector3(act.path[i].x, act.path[i].y * 0.5f, act.path[i].z);
            lr.SetPosition(i, pos);
            worldPath[i] = pos;
        }

        GameObject soldier = soldiersDataRef.GetSoldierModel(act.soldierId);
        if (soldier != null && worldPath.Length > 0)
        {
            float moveSpeed = 2.0f; // Units per second
            float turnSpeedDegrees = 540f; // Degrees per second
            Transform soldierTransform = soldier.transform;
            Animator animator = soldier.GetComponentInChildren<Animator>();

            movementSequence.AppendCallback(() =>
            {
                if (animator != null)
                {
                    animator.SetBool("Moving", true);
                }
            });
            Vector3 currentPos = soldierTransform.position;
            Quaternion currentRotation = soldierTransform.rotation;
            foreach (Vector3 targetPos in worldPath)
            {
                Vector3 flatDirection = new Vector3(targetPos.x - currentPos.x, 0f, targetPos.z - currentPos.z);
                if (flatDirection.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
                    float turnAngle = Quaternion.Angle(currentRotation, targetRotation);
                    if (turnAngle > 0.1f)
                    {
                        float turnDuration = turnAngle / turnSpeedDegrees;
                        movementSequence.Append(soldierTransform.DORotateQuaternion(targetRotation, turnDuration).SetEase(Ease.OutSine));
                    }

                    currentRotation = targetRotation;
                }

                float segmentDistance = flatDirection.magnitude;
                float duration = segmentDistance / moveSpeed;

                if (duration > 0f)
                {
                    if (Mathf.Abs(flatDirection.x) > 0.01f)
                    {
                        movementSequence.Append(soldierTransform.DOMoveX(targetPos.x, duration).SetEase(Ease.Linear));
                    }
                    else if (Mathf.Abs(flatDirection.z) > 0.01f)
                    {
                        movementSequence.Append(soldierTransform.DOMoveZ(targetPos.z, duration).SetEase(Ease.Linear));
                    }
                }

                currentPos = targetPos;
            }

            movementSequence.AppendCallback(() =>
            {
                if (animator != null)
                {
                    animator.SetBool("Moving", false);
                }
            });
        }

        return movementSequence;
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

    private void ClearEffects()
    {
        foreach (var ef in visualEffects)
        {
            if (ef != null) Destroy(ef);
        }
        visualEffects.Clear();
    }
}
