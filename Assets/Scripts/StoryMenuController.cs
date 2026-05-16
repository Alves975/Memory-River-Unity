using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StoryMenuController : MonoBehaviour
{
    public string gameplaySceneName = SceneIds.Historia1;
    public string mainMenuSceneName = SceneIds.MainMenu;

    public Text chapterTitleText;
    public Text chapterDescriptionText;
    public Text progressText;
    public Text statusText;

    private void Awake()
    {
        BindUI();
        RefreshTexts();
    }

    public void StartChapterOne()
    {
        GameLaunchConfig.ConfigureStory(1);
        SceneManager.LoadScene("Historia1");
    }

    public void BackToMainMenu()
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void BindUI()
    {
        Button startButton = FindButton("Jogar Capitulo 1Button");
        if (startButton != null)
        {
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(StartChapterOne);
        }

        Button backButton = FindButton("VoltarButton");
        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(BackToMainMenu);
        }

        if (chapterTitleText == null)
            chapterTitleText = FindText("ChapterTitle");

        if (chapterDescriptionText == null)
            chapterDescriptionText = FindText("ChapterDescription");

        if (progressText == null)
            progressText = FindText("Progress");

        if (statusText == null)
            statusText = FindText("Status");
    }

    private void RefreshTexts()
    {
        if (chapterTitleText != null)
            chapterTitleText.text = "Capitulo 1";

        if (chapterDescriptionText != null)
            chapterDescriptionText.text = "Memorize as cartas, limpe o tabuleiro e avance na campanha.";

        if (progressText != null)
            progressText.text = "Fluxo de historia separado do multiplayer.";

        if (statusText != null)
            statusText.text = "Comece pelo Capitulo 1.";
    }

    private Button FindButton(string objectName)
    {
        Transform target = FindDeepChild(transform, objectName);
        return target != null ? target.GetComponent<Button>() : null;
    }

    private Text FindText(string objectName)
    {
        Transform target = FindDeepChild(transform, objectName);
        return target != null ? target.GetComponent<Text>() : null;
    }

    private Transform FindDeepChild(Transform parent, string objectName)
    {
        if (parent.name == objectName)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeepChild(parent.GetChild(i), objectName);
            if (found != null)
                return found;
        }

        return null;
    }
}