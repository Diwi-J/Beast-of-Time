using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class VictoryManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Health bossHealth;           // the boss's Health component
    [SerializeField] private GameObject victoryPanel;     // "You Win" panel
    [SerializeField] private BossHealthBar bossHealthBar; // so we can hide it on win
    [SerializeField] private GameObject pauseCanvas;      // drag your pause canvas in here

    // How long to wait after the boss dies before showing the panel.
    [SerializeField] private float panelDelay = 5f;

    private bool hasWon = false;

    private void Start()
    {
        // Panel starts hidden so it only appears on victory
        if (victoryPanel != null) victoryPanel.SetActive(false);
    }

    // Subscribe in OnEnable / unsubscribe in OnDisable, same pattern as BossHealthBar,
    // so we never get called after this object is gone (that causes null errors).
    private void OnEnable()
    {
        if (bossHealth != null) bossHealth.OnDeath += Win;
    }

    private void OnDisable()
    {
        if (bossHealth != null) bossHealth.OnDeath -= Win;
    }

    private void Win()
    {
        if (hasWon) return; // guard in case OnDeath ever fires twice
        hasWon = true;

        if (bossHealthBar != null)
        {
            bossHealthBar.Hide();
            // Disabling the component stops the F key from toggling the bar
            // back on, since BossHealthBar.Update checks for F every frame.
            bossHealthBar.enabled = false;
        }

        // The panel part runs in a coroutine so we can wait first.
        StartCoroutine(ShowVictory());
    }

    private IEnumerator ShowVictory()
    {
        // Realtime wait, because we freeze Time.timeScale right after this.
        yield return new WaitForSecondsRealtime(panelDelay);

        if (pauseCanvas != null) pauseCanvas.SetActive(false);

        if (victoryPanel != null) victoryPanel.SetActive(true);

        // Freeze the game so the player can't keep fighting behind the panel
        Time.timeScale = 0f;

        // Free the mouse so the buttons are clickable
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Hook these up to the panel's buttons via the OnClick event
    public void RestartGame()
    {
        Time.timeScale = 1f; // reset, otherwise the reloaded scene stays frozen
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(0); // assumes the main menu is scene 0
    }
}