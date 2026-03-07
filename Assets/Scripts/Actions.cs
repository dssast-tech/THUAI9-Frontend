using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Actions : MonoBehaviour
{
    // A simple representation of an action being played out
    // Since UI and animations must be clear, we will visualize paths with LineRenderer and attacks with projectiles or floating text.

    private SoldiersData soldiersDataRef;
    private List<GameObject> visualEffects = new List<GameObject>();

    public void Setup(SoldiersData sd)
    {
        soldiersDataRef = sd;
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
        }
        
        yield return new WaitForSeconds(0.5f); // buffer before round ends visually
        ClearEffects();
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
                ShowFloatingText($"Target {dmg.targetId} takes {dmg.damage} raw DMG", Color.red, new Vector3(0, 1, 0)); // We don't have perfect positions here, it's just placeholder info
                Debug.Log($"Soldier {act.soldierId} attacked {dmg.targetId} for {dmg.damage} damage");
            }
        }
        
        yield return new WaitForSeconds(0.5f);
    }

    private IEnumerator HandleAbility(ActionField act)
    {
        // Show ability name
        if (!string.IsNullOrEmpty(act.ability))
        {
            Debug.Log($"Soldier {act.soldierId} used ability {act.ability}");
            ShowFloatingText($"Ability: {act.ability}", Color.magenta, new Vector3(0, 2, 0));
        }

        if (act.damageDealt != null)
        {
            foreach (var dmg in act.damageDealt)
            {
                ShowFloatingText($"Target {dmg.targetId} takes {dmg.damage} magic DMG", Color.magenta, new Vector3(0, 1.5f, 0));
            }
        }

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

    private void ShowFloatingText(string text, Color color, Vector3 offset)
    {
        // Simplistic GUI floating text is handled in OnGUI, but we can just use Debug.Log for placeholder 
        // We'll queue it as a temporary effect.
        GameObject textObj = new GameObject("FloatingTextData");
        FloatingText ft = textObj.AddComponent<FloatingText>();
        ft.text = text;
        ft.color = color;
        ft.offset = offset;
        visualEffects.Add(textObj);
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

public class FloatingText : MonoBehaviour
{
    public string text;
    public Color color;
    public Vector3 offset;
    private float birthTime;

    void Start()
    {
        birthTime = Time.time;
    }

    void OnGUI()
    {
        if (Time.time - birthTime > 2.0f) return; // Expire after 2s
        
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 20;
        style.normal.textColor = color;
        
        // Draw in center of screen
        GUI.Label(new Rect(Screen.width/2 - 100, Screen.height/2 + (offset.y * 30), 400, 40), text, style);
    }
}
