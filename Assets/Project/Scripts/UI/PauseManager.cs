using UnityEngine;
using UnityEngine.InputSystem;      // Needed for Keyboard.current (new Input System)
using UnityEngine.SceneManagement;

public class PauseManager : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;

    // Tracks whether the game is currently paused.
    // This stops P from re-pausing and R from "resuming" a game that isn't paused.
    private bool isPaused = false;

    private void Start()
    {
        // Make sure the game starts unpaused with the panel hidden.
        Time.timeScale = 1f;
        pausePanel.SetActive(false);
    }

    private void Update()
    {
        // Keyboard.current can be null if no keyboard is connected
        // (for example on some mobile devices), so we check first
        // to avoid a NullReferenceException.
        if (Keyboard.current == null) return;

        // Update() still runs when Time.timeScale is 0,
        // so we can keep listening for key presses while paused.

        // P pauses the game (only if it isn't already paused)
        if (Keyboard.current.pKey.wasPressedThisFrame && !isPaused)
        {
            OnPausePressed();
        }

        // R resumes the game (only if it is currently paused)
        if (Keyboard.current.rKey.wasPressedThisFrame && isPaused)
        {
            OnResumePressed();
        }
    }

    public void OnPausePressed()
    {
        isPaused = true;
        Time.timeScale = 0f;          // Freezes time-based gameplay (movement, physics, etc.)
        pausePanel.SetActive(true);
    }

    public void OnResumePressed()
    {
        isPaused = false;
        Time.timeScale = 1f;          // Restores normal game speed
        pausePanel.SetActive(false);
    }

    public void OnMainMenu()
    {
        // Reset the time scale first, otherwise the main menu
        // would load with the game still frozen.
        Time.timeScale = 1f;
        SceneManager.LoadScene(0);
    }

    public void OnExitPressed()
    {
        Application.Quit();
    }
}