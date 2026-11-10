using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public void OnMainMenuPressed()
    {
        SceneManager.LoadScene(0);
    }
    public void OnPlayPressed()
    {
        SceneManager.LoadScene(1);
    }
    public void OnControlsPressed()
    {
        SceneManager.LoadScene(2);
    }
    public void OnApplicationQuit()
    {
        Application.Quit();
    }

}
