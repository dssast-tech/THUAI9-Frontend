using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class GameRounds : MonoBehaviour
{
    public MapData mapDataScript;
    public PlayerData playerDataScript;
    public SoldiersData soldiersDataScript;
    public Actions actionsScript;

    private RootData gameData;
    private int currentRoundIndex = 0;
    private bool isPlaying = false;

    void Start()
    {
        // Try to load default on start
        string path = Path.Combine(Application.streamingAssetsPath, "final_example.json");
        LoadAndPlay(path);
    }

    public void LoadAndPlay(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError("File not found: " + filePath);
            return;
        }

        string jsonContent = File.ReadAllText(filePath);
        gameData = ParseJsonWithNewtonsoft(jsonContent);

        if (gameData == null || gameData.mapdata == null)
        {
            Debug.LogError("Failed to parse JSON.");
            return;
        }

        InitializeGame();
    }

    private RootData ParseJsonWithNewtonsoft(string jsonContent)
    {
        // The project has com.unity.modules.jsonserialize which means we can use JsonUtility
        // But JsonUtility needs Wrapper class for standard arrays.
        // We'll use JsonUtility and assume standard structure.
        return JsonUtility.FromJson<RootData>(jsonContent);
    }

    void OnGUI()
    {
        if (GUI.Button(new Rect(10, Screen.height - 50, 150, 40), "Load final_example.json"))
        {
            LoadAndPlay(Path.Combine(Application.streamingAssetsPath, "final_example.json"));
        }
        
        if (GUI.Button(new Rect(170, Screen.height - 50, 150, 40), "Load log.json"))
        {
            LoadAndPlay(Path.Combine(Application.streamingAssetsPath, "log.json"));
        }
        
        if (GUI.Button(new Rect(330, Screen.height - 50, 150, 40), isPlaying ? "Pause" : "Play/Next"))
        {
            if (gameData != null && !isPlaying && currentRoundIndex < gameData.gameRounds.Length)
            {
                StartCoroutine(PlayNextRound());
            }
        }
        
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 20;
        style.normal.textColor = Color.white;
        
        if (gameData != null)
        {
            string roundText = currentRoundIndex < gameData.gameRounds.Length ? (currentRoundIndex + 1).ToString() : "End";
            GUI.Label(new Rect(10, 50, 200, 30), $"Round: {roundText} / {gameData.gameRounds.Length}", style);
        }
    }

    private void InitializeGame()
    {
        StopAllCoroutines();
        currentRoundIndex = 0;
        isPlaying = false;

        // Auto find components if not manually set
        if (mapDataScript == null) mapDataScript = gameObject.GetComponent<MapData>() ?? gameObject.AddComponent<MapData>();
        if (playerDataScript == null) playerDataScript = gameObject.GetComponent<PlayerData>() ?? gameObject.AddComponent<PlayerData>();
        if (soldiersDataScript == null) soldiersDataScript = gameObject.GetComponent<SoldiersData>() ?? gameObject.AddComponent<SoldiersData>();
        if (actionsScript == null) actionsScript = gameObject.GetComponent<Actions>() ?? gameObject.AddComponent<Actions>();

        mapDataScript.ClearMap();
        mapDataScript.GenerateMap(gameData.mapdata);

        playerDataScript.Initialize(gameData.playerData);
        soldiersDataScript.InitializeSoldiers(gameData.soldiersData);
        
        actionsScript.Setup(soldiersDataScript);
    }

    private IEnumerator PlayNextRound()
    {
        isPlaying = true;
        
        if (currentRoundIndex < gameData.gameRounds.Length)
        {
            var round = gameData.gameRounds[currentRoundIndex];
            
            // Play Actions First
            yield return StartCoroutine(actionsScript.PlayActions(round.actions));
            
            // Update Stats
            soldiersDataScript.UpdateSoldierStats(round.stats);
            
            // Update Player Data
            playerDataScript.UpdateRoundInfo(round.score, round.end);
            
            currentRoundIndex++;
            
            yield return new WaitForSeconds(1.0f); // Round end pause
        }
        
        isPlaying = false;
        
        // Auto play loop ?
        if (currentRoundIndex < gameData.gameRounds.Length)
        {
            StartCoroutine(PlayNextRound());
        }
    }
}
