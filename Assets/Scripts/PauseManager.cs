using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseManager : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;
    private void Start()
    {
        pausePanel.SetActive(false);
    }

    public void OnPausePressed()
    {
        Time.timeScale = 0f;
        pausePanel.SetActive(true);
    }

    public void OnResumePressed()
    {
        Time.timeScale = 1f;
        pausePanel.SetActive(false);
    }
    public void OnMainMenu()
    {
        SceneManager.LoadScene(0);
    }
    public void OnExitPressed()
    {
        Application.Quit();
    }
}