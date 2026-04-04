using System.Collections.Generic;
using UnityEngine;
using System.IO;
using DG.Tweening;

public class GameRounds : MonoBehaviour
{
    public MapData mapDataScript;
    public PlayerData playerDataScript;
    public SoldiersData soldiersDataScript;
    public Actions actionsScript;
    public ReplayActionLog replayActionLog;
    public ActionQueueUI actionQueueUI;
    public SoldierHoverTooltip soldierHoverTooltip;

    private RootData gameData;
    private int currentRoundIndex = 0;
    private bool isPlaying = false;
    private bool isAutoPlaying = false;
    private Sequence currentActionSequence;
    private Tween roundEndPauseTween;

    void Start()
    {
        // Try to load default on start
        string path = Path.Combine(Application.streamingAssetsPath, "logNew.json");
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
        KillPlaybackTweens();
        currentRoundIndex = 0;
        isPlaying = false;
        isAutoPlaying = false;

        // Auto find components if not manually set
        if (mapDataScript == null) mapDataScript = gameObject.GetComponent<MapData>() ?? gameObject.AddComponent<MapData>();
        if (playerDataScript == null) playerDataScript = gameObject.GetComponent<PlayerData>() ?? gameObject.AddComponent<PlayerData>();
        if (soldiersDataScript == null) soldiersDataScript = gameObject.GetComponent<SoldiersData>() ?? gameObject.AddComponent<SoldiersData>();
        if (actionsScript == null) actionsScript = gameObject.GetComponent<Actions>() ?? gameObject.AddComponent<Actions>();
        if (soldierHoverTooltip == null) soldierHoverTooltip = gameObject.GetComponent<SoldierHoverTooltip>() ?? gameObject.AddComponent<SoldierHoverTooltip>();
        if (actionQueueUI == null) actionQueueUI = FindObjectOfType<ActionQueueUI>();

        mapDataScript.ClearMap();
        mapDataScript.GenerateMap(gameData.mapdata);

        playerDataScript.Initialize(gameData.playerData);
        soldiersDataScript.InitializeSoldiers(gameData.soldiersData);

        actionsScript.Setup(soldiersDataScript);
        actionsScript.SetActionQueueUI(actionQueueUI);
        if (soldierHoverTooltip != null)
        {
            soldierHoverTooltip.SetSoldiersData(soldiersDataScript);
        }

        if (actionQueueUI != null)
        {
            actionQueueUI.Setup(soldiersDataScript);
            actionQueueUI.PrecomputeRoundQueues(gameData.gameRounds);
        }
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
        if (isAutoPlaying)
        {
            Debug.LogWarning("当前处于自动播放中，NextRound 已忽略。", this);
            return;
        }

        if (!CanPlayNextRound(logWhenBlocked: true))
        {
            return;
        }

        PlayNextRound();
    }

    // 供 UI Button(OnClick) 绑定：自动播放剩余所有回合
    public void AutoPlay()
    {
        if (!HasRoundsToPlay(logWhenBlocked: true))
        {
            return;
        }

        if (isAutoPlaying)
        {
            Debug.Log("自动播放已在进行中。", this);
            return;
        }

        isAutoPlaying = true;
        if (!isPlaying)
        {
            PlayNextRound();
        }
    }

    // 可选：供 UI Button(OnClick) 绑定，手动停止自动播放
    public void StopAutoPlay()
    {
        if (!isAutoPlaying)
        {
            return;
        }

        isAutoPlaying = false;
        Debug.Log("已停止自动播放。", this);
    }

    private bool HasRoundsToPlay(bool logWhenBlocked)
    {
        if (gameData == null || gameData.gameRounds == null || gameData.gameRounds.Length == 0)
        {
            if (logWhenBlocked)
            {
                Debug.LogWarning("没有可播放的回合数据。", this);
            }
            return false;
        }

        if (currentRoundIndex >= gameData.gameRounds.Length)
        {
            if (logWhenBlocked)
            {
                Debug.Log("所有回合已播放完毕。", this);
            }
            return false;
        }

        return true;
    }

    private bool CanPlayNextRound(bool logWhenBlocked)
    {
        if (isPlaying)
        {
            if (logWhenBlocked)
            {
                Debug.LogWarning("当前回合仍在播放中，请稍候再点击 NextRound。", this);
            }
            return false;
        }

        return HasRoundsToPlay(logWhenBlocked);
    }

    private void PlayNextRound()
    {
        if (!CanPlayNextRound(logWhenBlocked: false))
        {
            return;
        }

        isPlaying = true;

        var round = gameData.gameRounds[currentRoundIndex];
        if (actionQueueUI != null)
        {
            actionQueueUI.ShowRoundQueue(currentRoundIndex, round.actions);
        }

        if (replayActionLog != null)
        {
            int roundNumber = round.roundNumber > 0 ? round.roundNumber : currentRoundIndex + 1;
            var actionDescriptions = actionsScript.BuildRoundActionDescriptions(round.actions);
            replayActionLog.ShowRoundActions(roundNumber, actionDescriptions);
        }

        currentActionSequence = actionsScript.PlayActions(round.actions, () =>
        {
            currentActionSequence = null;

            soldiersDataScript.UpdateSoldierStats(round.stats);
            playerDataScript.UpdateRoundInfo(round.score, round.end);

            currentRoundIndex++;

            roundEndPauseTween?.Kill();
            roundEndPauseTween = DOVirtual.DelayedCall(1.0f, OnRoundCompleteDelayFinished).SetTarget(this);
        });
    }

    private void OnRoundCompleteDelayFinished()
    {
        roundEndPauseTween = null;
        isPlaying = false;

        if (currentRoundIndex >= gameData.gameRounds.Length)
        {
            if (isAutoPlaying)
            {
                Debug.Log("自动播放结束：所有回合已播放完毕。", this);
            }

            isAutoPlaying = false;
            return;
        }

        if (isAutoPlaying)
        {
            PlayNextRound();
        }
    }

    private void KillPlaybackTweens()
    {
        currentActionSequence?.Kill();
        currentActionSequence = null;

        roundEndPauseTween?.Kill();
        roundEndPauseTween = null;
    }
}
