using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    [Header("UI Panels")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject registerPanel;
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private GameObject startGamePanel;
    [SerializeField] private GameObject gamePanel;
    [SerializeField] private GameObject playerObject;
    [SerializeField] private Camera menuCamera;

    [Header("Game")]
    [SerializeField] private ArenaTimer arenaTimer;
    [SerializeField] private Transform floorPlane;
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private int enemyMin = 2;
    [SerializeField] private int enemyMax = 6;

    [Header("HUD")]
    [SerializeField] private TMP_Text scoreText;

    [Header("Leaderboard")]
    [SerializeField] private GameObject leaderboardPanel;
    [SerializeField] private TMP_Text leaderboardText;

    [Header("Stats")]
    [SerializeField] private GameObject statsPanel;
    [SerializeField] private TMP_Text   statsText;

    private float _arenaHalf;
    private int _score;
    private float wallMargin => 1f;
    private float playerMargin => 3f;


    private void Start()
    {
        ShowMainMenu();
        if (menuCamera != null) menuCamera.gameObject.SetActive(true);
        if (gamePanel != null) gamePanel.SetActive(false);
        if (playerObject != null) playerObject.SetActive(false);        if (statsPanel   != null) statsPanel.SetActive(false);        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ShowMainMenu() => SetMenuPanel(mainMenuPanel);
    public void ShowRegister() => SetMenuPanel(registerPanel);
    public void ShowLogin() => SetMenuPanel(loginPanel);
    public void ShowStartGame() => SetMenuPanel(startGamePanel);

    private void SetMenuPanel(GameObject target)
    {
        if (mainMenuPanel  != null) mainMenuPanel.SetActive(false);
        if (registerPanel  != null) registerPanel.SetActive(false);
        if (loginPanel     != null) loginPanel.SetActive(false);
        if (startGamePanel != null) startGamePanel.SetActive(false);
        if (statsPanel     != null) statsPanel.SetActive(false);
        if (target         != null) target.SetActive(true);
        menuCamera.gameObject.SetActive(target != null);
    }

    [Header("Inscription")]
    [SerializeField] private TMP_InputField registerName;
    [SerializeField] private TMP_InputField registerEmail;
    [SerializeField] private TMP_InputField registerPassword;
    [SerializeField] private TMP_InputField registerBirth;
    [SerializeField] private TMP_InputField registerLocation;

    [Header("Connexion")]
    [SerializeField] private TMP_InputField loginEmail;
    [SerializeField] private TMP_InputField loginPassword;

    public void RegisterClick() => _ = DoRegister();
    public void ConnectionClick() => _ = DoLogin();
    public void StartGame() => _ = DoStartGame();

    private async Task DoRegister()
    {
        try
        {
            await ApiService.Instance.Register(
                registerName.text, registerEmail.text, registerPassword.text, registerBirth.text, registerLocation.text);

            Debug.Log("Inscription réussie !");
            ShowMainMenu();
        }
        catch (Exception e) { Debug.LogError("[Register] " + e.Message); }
    }

    private async Task DoLogin()
    {
        try
        {
            bool ok = await ApiService.Instance.Login(loginEmail.text, loginPassword.text);
            if (ok)
            {
                Debug.Log($"Connecté : {ApiService.Instance.CurrentPlayerName}");
                ShowStartGame();
            }
            else
                Debug.LogWarning("Email ou mot de passe incorrect.");
        }
        catch (Exception e) { Debug.LogError("[Login] " + e.Message); }
    }

    private async Task DoStartGame()
    {
        try
        {
            if (ApiService.Instance.CurrentPlayerId == 0)
            {
                Debug.LogWarning("Vous devez vous connecter avant de jouer !");
                ShowLogin();
                return;
            }
            menuCamera.gameObject.SetActive(false);
            menuPanel.SetActive(false);

            _score = 0;
            if (scoreText != null) scoreText.text = "Score: 0";

            int dim = UnityEngine.Random.Range(5, 11);
            int time = UnityEngine.Random.Range(15, 35);

            _arenaHalf = floorPlane != null ? floorPlane.localScale.x * 5f : dim / 2f;

            await ApiService.Instance.CreatePartie(dimension: dim, maxTime: time);
            arenaTimer.StartTimer(time);
            SpawnEnemies();

            SetMenuPanel(null);
            if (menuCamera != null) menuCamera.gameObject.SetActive(false);
            if (gamePanel != null) gamePanel.SetActive(true);
            if (playerObject != null) playerObject.SetActive(true);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            Debug.Log($"Partie {ApiService.Instance.CurrentPartieId} créée (Dimension: {dim}, Temps: {time}) !");
        }
        catch (Exception e) { Debug.LogError("[StartGame] " + e.Message); }
    }

    private void SpawnEnemies()
    {
        if (enemyPrefab == null) { Debug.LogWarning("[GameManager] enemyPrefab non assigné !"); return; }

        foreach (var e in GameObject.FindGameObjectsWithTag("Enemy"))
            Destroy(e);

        int count = UnityEngine.Random.Range(enemyMin, enemyMax + 1);

        for (int i = 0; i < count; i++)
        {
            Vector3 pos;
            int tries = 0;
            do
            {
                float x = UnityEngine.Random.Range(-_arenaHalf + wallMargin, _arenaHalf - wallMargin);
                float z = UnityEngine.Random.Range(-_arenaHalf + wallMargin, _arenaHalf - wallMargin);
                pos = new Vector3(x, 1f, z);
                tries++;
            }
            while (new Vector2(pos.x, pos.z).magnitude < playerMargin && tries < 20);

            Instantiate(enemyPrefab, pos, Quaternion.identity);
        }
    }

    public void AddScore(int points)
    {
        _score += points;
        if (scoreText != null) scoreText.text = $"Score : {_score}";
        _ = ApiService.Instance.UpdateScore(_score);
    }

    public void TriggerGameOver() => _ = DoGameOver();

    private async Task DoGameOver()
    {
        int elapsed = arenaTimer.GetElapsed();

        if (playerObject != null) playerObject.SetActive(false);
        if (gamePanel != null) gamePanel.SetActive(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        try { await ApiService.Instance.EndPartie(ApiService.Instance.CurrentPlayerId, elapsed); }
        catch (Exception e) { Debug.LogWarning("[EndPartie] " + e.Message); }

        try { await ShowLeaderboard(elapsed); }
        catch (Exception e) { Debug.LogError("[ShowLeaderboard] " + e.Message); }
    }

    private async Task ShowLeaderboard(int elapsed)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Temps joué : {elapsed}s\n");

        try
        {
            var results = await ApiService.Instance.GetLeaderboard();
            foreach (var r in results)
            {
                string name = r.playerId == ApiService.Instance.CurrentPlayerId
                    ? ApiService.Instance.CurrentPlayerName
                    : $"Joueur #{r.playerId}";
                sb.AppendLine($"{name}  :  {r.score} pts");
            }
        }
        catch
        {
            sb.AppendLine($"{ApiService.Instance.CurrentPlayerName}  :  {_score} pts");
        }

        menuCamera.gameObject.SetActive(true);
        menuPanel.SetActive(true);
        if (leaderboardText != null) leaderboardText.text = sb.ToString();
        if (leaderboardPanel != null) leaderboardPanel.SetActive(true);
    }

    public void BackToMenu()
    {
        if (leaderboardPanel != null) leaderboardPanel.SetActive(false);
        ShowMainMenu();
    }

    public void ShowStats() => _ = DoShowStats();
    public void BackFromStats() => ShowMainMenu();

    private async Task DoShowStats()
    {
        SetMenuPanel(null);
        if (statsPanel != null) { statsPanel.SetActive(true); menuCamera.gameObject.SetActive(true); }
        if (statsText == null) return;

        statsText.text = "Chargement...";
        var sb = new System.Text.StringBuilder();

        try
        {
            var v = await ApiService.Instance.GetMeilleurVainqueur();
            sb.AppendLine($"Meilleur joueur : {v.nom}  ({v.nb_victoires} victoires)");
        }
        catch { sb.AppendLine("Meilleur joueur : N/A"); }

        try
        {
            float age = await ApiService.Instance.GetAgeMoyen();
            sb.AppendLine($"Age moyen des joueurs : {age:F1} ans");
        }
        catch { sb.AppendLine("Age moyen : N/A"); }

        sb.AppendLine();
        sb.AppendLine("-- Classement par degats --");
        try
        {
            var classement = await ApiService.Instance.GetClassementDegats();
            if (classement.Length == 0)
                sb.AppendLine("Aucune donnee");
            for (int i = 0; i < classement.Length; i++)
            {
                var e = classement[i];
                string name = e.joueurId == ApiService.Instance.CurrentPlayerId
                    ? ApiService.Instance.CurrentPlayerName
                    : $"Joueur #{e.joueurId}";
                sb.AppendLine($"#{i + 1}  {name}  -  {e.degats_total} pts  ({e.nb_tirs} tirs)");
            }
        }
        catch { sb.AppendLine("Classement indisponible"); }

        sb.AppendLine();
        sb.AppendLine("-- Jeux (nb parties) --");
        try
        {
            var jeux = await ApiService.Instance.GetJeuxClassement();
            foreach (var j in jeux)
                sb.AppendLine($"{j.nom}  :  {j.nb_parties} parties");
        }
        catch { sb.AppendLine("Classement jeux indisponible"); }

        statsText.text = sb.ToString();
    }

    public void OnPlayerShoot(Vector3Int position, Vector3 direction, string resultat, int degats)
        => _ = ApiService.Instance.RecordTir(position, direction, resultat, degats);

    public void OnPlayerMove(Vector3Int position, float rotationY)
        => _ = ApiService.Instance.RecordDeplacement(position, rotationY);

    public void OnScoreChanged(int newScore)
        => _ = ApiService.Instance.UpdateScore(newScore);

    public void OnGameOver(int vainqueurId, int gameTime)
        => _ = ApiService.Instance.EndPartie(vainqueurId, gameTime);
}
