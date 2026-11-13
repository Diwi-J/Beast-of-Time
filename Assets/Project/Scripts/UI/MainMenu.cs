using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private PlayerSwordSound sound;

    

    public void OnMainMenuPressed()
    {
        
        SceneManager.LoadScene(0);

    }
    public void OnPlayPressed()
    {
        PersistentMusic.Stop();
        SceneManager.LoadScene(2);
    }
    public void OnControlsPressed()
    {
        
        
        SceneManager.LoadScene(1);
    }
    public void OnApplicationQuit()
    {
       
        Application.Quit();
    }

}
