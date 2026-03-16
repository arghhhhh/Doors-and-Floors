using UnityEngine;

public class WinTrigger : MonoBehaviour
{
    void OnTriggerStay(Collider other)
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.Playing)
            return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        // Only trigger win when player has landed on the floor (not mid-teleport)
        if (!player.IsGrounded) return;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.PlayerWon(player.playerNumber);
        }
    }
}
