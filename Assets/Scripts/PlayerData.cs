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
}
