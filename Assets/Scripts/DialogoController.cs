using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

[System.Serializable]
public class DialogueLine
{
    public string characterName;

    [TextArea(2,5)]
    public string dialogueText;
}


public class DialogoController : MonoBehaviour
{
    public TMP_Text nomePersonagem;
    public TMP_Text textoDialogo;

    public DialogueLine[] falas;

    private int indice = 0;

    public System.Action onDialogueFinished;

    public bool terminou = false;

    void Start()
    {
        MostrarFala();
    }

    void MostrarFala()
    {
        nomePersonagem.text = falas[indice].characterName;
        textoDialogo.text = falas[indice].dialogueText;
    }

   
    public void ProximaFala()
{
    indice++;

    if(indice < falas.Length)
    {
        MostrarFala();
    }
   else
    {
        terminou = true;

        if (onDialogueFinished != null)
            onDialogueFinished.Invoke();
        else
            LoadNextScene();
        Debug.Log("Evento de fim do diálogo disparado");
    }
}

    private void LoadNextScene()
    {
        string currentScene = SceneManager.GetActiveScene().name;

        if (currentScene == SceneIds.Historia1)
            SceneManager.LoadScene(SceneIds.Level1);
        else if (currentScene == SceneIds.Historia2)
            SceneManager.LoadScene(SceneIds.Level2);
        else if (currentScene == SceneIds.Historia3)
            SceneManager.LoadScene(SceneIds.Level3);
        else if (currentScene == SceneIds.Historia4)
            SceneManager.LoadScene(SceneIds.MainMenu);
    }

}
