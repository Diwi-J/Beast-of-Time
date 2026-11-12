using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverManager : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;   // drag the player's PlayerHealth in here
    [SerializeField] private GameObject gameOverPanel;    // drag your Game Over panel in here
    [SerializeField] private GameObject pauseCanvas;      // drag your pause canvas in here

    // Pause before the panel shows, so the death animation can play first.
    [SerializeField] private float panelDelay = 5f;

    private bool gameOverShown = false;

    private void Start()
    {
        // Start with the panel hidden, and make sure time is running
        // (in case we got here by restarting from a paused/frozen game).
        Time.timeScale = 1f;
        gameOverPanel.SetActive(false);
    }

    private void OnEnable()
    {
        if (playerHealth != null)
            playerHealth.OnPlayerDied += HandlePlayerDied;
    }

    private void OnDisable()
    {
        // Always unsubscribe so the event never calls a destroyed object.
        if (playerHealth != null)
            playerHealth.OnPlayerDied -= HandlePlayerDied;
    }

    private void HandlePlayerDied()
    {
        // Guard so the panel can't be triggered twice.
        if (gameOverShown) return;
        gameOverShown = true;

        StartCoroutine(ShowGameOver());
    }

    private IEnumerator ShowGameOver()
    {
        // Realtime wait, because we freeze Time.timeScale right after this.
        // A normal WaitForSeconds would never finish once time is 0.
        yield return new WaitForSecondsRealtime(panelDelay);

        // Hide the pause menu so it can't draw on top of the game over panel.
        if (pauseCanvas != null)
            pauseCanvas.SetActive(false);

        gameOverPanel.SetActive(true);
        Time.timeScale = 0f; // freeze the game behind the panel
        

        // Free the cursor so the buttons can be clicked.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Hook these up to the buttons' On Click () events in the Inspector.
    public void OnRestartPressed()
    {

        // Reset time first, otherwise the reloaded scene starts frozen.
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void OnMainMenuPressed()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(0);
        
    }

    
}