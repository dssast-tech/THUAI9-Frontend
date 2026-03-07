using UnityEngine;

public class PlayerData : MonoBehaviour
{
    private string player1Id;
    private string player2Id;
    private int p1Score = 0;
    private int p2Score = 0;
    private bool gameOver = false;
    private string endReason = "";
    
    // Simple UI reference strings to draw with OnGUI
    // If you prefer full UGUI, this can be mapped to UnityEngine.UI.Text or TMPro.TextMeshProUGUI components.
    
    public void Initialize(PlayerDataField ptField)
    {
        player1Id = ptField.player1;
        player2Id = ptField.player2;
        p1Score = 0;
        p2Score = 0;
        gameOver = false;
        endReason = "";
    }

    public void UpdateRoundInfo(ScoreField score, string endData)
    {
        if (score != null)
        {
            p1Score = score.redScore;
            p2Score = score.blueScore;
        }

        if (!string.IsNullOrEmpty(endData) && endData.ToLower() != "false")
        {
            gameOver = true;
            endReason = endData;
        }
    }
    
    private void OnGUI()
    {
        if (string.IsNullOrEmpty(player1Id)) return;
        
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 24;
        style.normal.textColor = Color.red;
        
        GUI.Label(new Rect(10, 10, 300, 30), $"[Red] {player1Id} Score: {p1Score}", style);
        
        style.normal.textColor = Color.blue;
        style.alignment = TextAnchor.UpperRight;
        GUI.Label(new Rect(Screen.width - 310, 10, 300, 30), $"[Blue] {player2Id} Score: {p2Score}", style);
        
        if (gameOver)
        {
            style.fontSize = 40;
            style.alignment = TextAnchor.MiddleCenter;
            style.normal.textColor = Color.yellow;
            GUI.Label(new Rect(0, 0, Screen.width, Screen.height), $"GAME OVER: {endReason}", style);
        }
    }
}
