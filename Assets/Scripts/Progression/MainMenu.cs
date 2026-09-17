using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    [Header("Scene panels")]
    [SerializeField] private GameObject homePanel;
    [SerializeField] private GameObject levelsPanel;
    [SerializeField] private GameObject profilePanel;
    [SerializeField] private GameObject resetConfirmation;
    [Header("Profile and progress")]
    [SerializeField] private Text profileLabel;
    [SerializeField] private Text progressLabel;
    [SerializeField] private Text status;
    [SerializeField] private InputField playerName;
    [SerializeField] private Button playButton;
    [SerializeField] private Text playLabel;
    [SerializeField] private Button levelSelectButton;
    [Header("Dynamic level list")]
    [SerializeField] private Transform levelContent;
    [SerializeField] private Button levelButtonPrefab;
    private readonly List<GameObject> levelButtons = new List<GameObject>();

    private void Start() => ShowHome();

    private void ShowPanel(GameObject panel)
    {
        homePanel.SetActive(panel == homePanel);
        levelsPanel.SetActive(panel == levelsPanel);
        profilePanel.SetActive(panel == profilePanel);
        resetConfirmation.SetActive(false);
        status.text = "";
        var catalog = GameProgress.Catalog;
        int total = catalog == null ? 0 : catalog.levels.Count;
        int completed = catalog == null ? 0 : catalog.levels.FindAll(l => GameProgress.Profile.HasCompleted(l.id)).Count;
        profileLabel.text = "PROFILE  /  " + GameProgress.Profile.displayName;
        progressLabel.text = completed + " / " + total + " levels completed";
        playButton.interactable = levelSelectButton.interactable = total > 0;
        playLabel.text = completed == 0 ? "Play" : completed == total ? "Replay" : "Continue";
        if (total == 0) status.text = "No levels configured.";
        var first = panel.GetComponentInChildren<Selectable>();
        if (first != null) first.Select();
    }

    public void ShowHome() => ShowPanel(homePanel);

    public void ShowLevels()
    {
        foreach (var button in levelButtons) { button.SetActive(false); Destroy(button); }
        levelButtons.Clear();
        var catalog = GameProgress.Catalog;
        if (catalog != null)
            for (int i = 0; i < catalog.levels.Count; i++)
            {
                int index = i;
                var level = catalog.levels[i];
                var button = Instantiate(levelButtonPrefab, levelContent);
                button.name = "Level " + (i + 1);
                button.interactable = catalog.IsUnlocked(GameProgress.Profile, i);
                string state = GameProgress.Profile.HasCompleted(level.id) ? "COMPLETED" : button.interactable ? "AVAILABLE" : "LOCKED";
                button.GetComponentInChildren<Text>().text = (i + 1).ToString("00") + "   " + level.title + "  /  " + state;
                button.onClick.AddListener(() => Play(index));
                levelButtons.Add(button.gameObject);
            }
        ShowPanel(levelsPanel);
        Canvas.ForceUpdateCanvases();
        var scroll = levelContent.GetComponentInParent<ScrollRect>();
        if (scroll != null) scroll.verticalNormalizedPosition = 1;
    }

    public void ShowProfile()
    {
        ShowPanel(profilePanel);
        playerName.text = GameProgress.Profile.displayName;
        playerName.Select();
    }

    public void ContinueGame()
    {
        if (GameProgress.Catalog != null) Play(GameProgress.Catalog.ContinueIndex(GameProgress.Profile));
    }

    private void Play(int index)
    {
        if (!GameProgress.LoadLevel(index)) status.text = "Unable to load level. Check the build scene list.";
    }

    public void SaveName()
    {
        string name = playerName.text.Trim();
        if (name.Length == 0) { status.text = "Enter a player name."; return; }
        string oldName = GameProgress.Profile.displayName;
        GameProgress.Profile.displayName = name;
        if (ProfileStore.Save(GameProgress.Profile))
        {
            profileLabel.text = "PROFILE  /  " + name;
            status.text = "Profile saved.";
        }
        else
        {
            GameProgress.Profile.displayName = oldName;
            status.text = "Could not save profile. Please try again.";
        }
    }

    public void RequestReset() { resetConfirmation.SetActive(true); resetConfirmation.GetComponentInChildren<Button>().Select(); }
    public void CancelReset() { resetConfirmation.SetActive(false); playerName.Select(); }
    public void ConfirmReset()
    {
        var old = GameProgress.Profile.completedLevelIds;
        GameProgress.Profile.completedLevelIds = new List<string>();
        if (ProfileStore.Save(GameProgress.Profile)) ShowHome();
        else { GameProgress.Profile.completedLevelIds = old; status.text = "Reset could not be saved."; }
    }
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
