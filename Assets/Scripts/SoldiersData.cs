using System.Collections.Generic;
using UnityEngine;

public class SoldiersData : MonoBehaviour
{
    private Dictionary<int, GameObject> soldiersMap = new Dictionary<int, GameObject>();
    
    // Store soldier info
    private Dictionary<int, string> soldierTypes = new Dictionary<int, string>();
    private Dictionary<int, string> soldierCamps = new Dictionary<int, string>();
    private Dictionary<int, int> soldierHP = new Dictionary<int, int>();
    
    private Transform soldiersParent;

    public void InitializeSoldiers(SoldierInitDataField[] soldiersDataData)
    {
        if (soldiersParent == null)
            soldiersParent = new GameObject("SoldiersParent").transform;
        
        ClearSoldiers();

        foreach (var s in soldiersDataData)
        {
            GameObject model = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            model.transform.SetParent(soldiersParent);
            
            // Set position
            float sizeY = s.position.y; 
            // In MapData z represents row and x represents col. Position needs to match perfectly.
            // s.position is {x, y, z}. y is height, so actual map tile y=position.y, but visually model's world y should be position.y or slightly above.
            model.transform.position = new Vector3(s.position.x, sizeY + 0.5f, s.position.z);
            model.name = $"Soldier_{s.ID}_{s.soldierType}_{s.camp}";

            Renderer r = model.GetComponent<Renderer>();
            string c = s.camp.ToLower();
            if (c == "red") r.material.color = Color.red;
            else if (c == "blue") r.material.color = Color.blue;
            else r.material.color = Color.magenta;

            // Simple primitive placeholder differentiation
            if (s.soldierType.ToLower() == "warrior")
                model.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
            else if (s.soldierType.ToLower() == "archer")
                model.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            else if (s.soldierType.ToLower() == "mage")
                model.transform.localScale = new Vector3(0.6f, 1.2f, 0.6f);

            soldiersMap[s.ID] = model;
            soldierTypes[s.ID] = s.soldierType;
            soldierCamps[s.ID] = s.camp;
            soldierHP[s.ID] = s.stats != null ? s.stats.health : 0;
        }
    }

    public void UpdateSoldierStats(SoldierRoundStat[] stats)
    {
        if (stats == null) return;
        
        foreach (var stat in stats)
        {
            if (soldiersMap.TryGetValue(stat.soldierId, out GameObject go))
            {
                if (stat.survived != null && stat.survived.ToLower() == "false")
                {
                    go.SetActive(false);
                    soldierHP[stat.soldierId] = 0;
                    continue; // Skip further update for dead soldier
                }
                
                if (stat.position != null)
                {
                    float sizeY = stat.position.y;
                    go.transform.position = new Vector3(stat.position.x, sizeY + 0.5f, stat.position.z);
                }

                // Handling case sensitivity mapping between Stats and stats
                if (stat.Stats != null)
                {
                    soldierHP[stat.soldierId] = stat.Stats.health;
                }
                else if (stat.stats != null)
                {
                    soldierHP[stat.soldierId] = stat.stats.health;
                }
            }
        }
    }
    
    public void ClearSoldiers()
    {
        foreach (var kvp in soldiersMap)
        {
            Destroy(kvp.Value);
        }
        soldiersMap.Clear();
        soldierTypes.Clear();
        soldierCamps.Clear();
        soldierHP.Clear();
    }

    public GameObject GetSoldierModel(int id)
    {
        if (soldiersMap.TryGetValue(id, out GameObject go))
        {
            return go;
        }
        return null;
    }

    public string GetSoldierDisplayName(int id)
    {
        string camp = soldierCamps.ContainsKey(id) ? soldierCamps[id] : "Unknown";
        string type = soldierTypes.ContainsKey(id) ? soldierTypes[id] : "Soldier";
        return $"{camp} {type} #{id}";
    }

    public string GetSoldierCamp(int id)
    {
        return soldierCamps.ContainsKey(id) ? soldierCamps[id] : string.Empty;
    }

    public string GetSoldierType(int id)
    {
        return soldierTypes.ContainsKey(id) ? soldierTypes[id] : string.Empty;
    }

    public List<int> GetAllSoldierIds(bool onlyAlive)
    {
        List<int> ids = new List<int>();
        foreach (var kvp in soldiersMap)
        {
            int id = kvp.Key;

            if (onlyAlive)
            {
                bool isAlive = kvp.Value != null && kvp.Value.activeSelf;
                if (soldierHP.ContainsKey(id) && soldierHP[id] <= 0)
                {
                    isAlive = false;
                }

                if (!isAlive)
                {
                    continue;
                }
            }

            ids.Add(id);
        }

        ids.Sort();
        return ids;
    }

}
