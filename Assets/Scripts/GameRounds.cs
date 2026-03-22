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
    public ReplayActionLog replayActionLog;

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
        if (replayActionLog != null)
        {
            replayActionLog.Setup(soldiersDataScript);
        }
        else
        {
            Debug.LogError("GameRounds 未绑定 replayActionLog，请在 Inspector 手动指定 ReplayActionLog 组件。", this);
        }

        Debug.Log("GameRounds 初始化完成，等待 NextRound 按钮触发下一回合。", this);
    }

    // 供 UI Button(OnClick) 绑定：每次点击推进一个回合
    public void NextRound()
    {
        if (isPlaying)
        {
            Debug.LogWarning("当前回合仍在播放中，请稍候再点击 NextRound。", this);
            return;
        }

        if (gameData == null || gameData.gameRounds == null || gameData.gameRounds.Length == 0)
        {
            Debug.LogWarning("没有可播放的回合数据。", this);
            return;
        }

        if (currentRoundIndex >= gameData.gameRounds.Length)
        {
            Debug.Log("所有回合已播放完毕。", this);
            return;
        }

        StartCoroutine(PlayNextRound());
    }

    private IEnumerator PlayNextRound()
    {
        isPlaying = true;

        if (currentRoundIndex < gameData.gameRounds.Length)
        {
            var round = gameData.gameRounds[currentRoundIndex];
            if (replayActionLog != null)
            {
                int roundNumber = round.roundNumber > 0 ? round.roundNumber : currentRoundIndex + 1;
                var actionDescriptions = actionsScript.BuildRoundActionDescriptions(round.actions);
                replayActionLog.ShowRoundActions(roundNumber, actionDescriptions);
            }

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
    }
}
