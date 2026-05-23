using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviour
{
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
        }
        catch (Exception e) { Debug.LogError("[Register] " + e.Message); }
    }

    private async Task DoLogin()
    {
        try
        {
            bool ok = await ApiService.Instance.Login(loginEmail.text, loginPassword.text);
            if (ok)
                Debug.Log($"Connecté : {ApiService.Instance.CurrentPlayerName}");
            else
                Debug.LogWarning("Email ou mot de passe incorrect.");
        }
        catch (Exception e) { Debug.LogError("[Login] " + e.Message); }
    }

    private async Task DoStartGame()
    {
        try
        {
            int dim = UnityEngine.Random.Range(5, 11);
            int time = UnityEngine.Random.Range(30, 91);
            await ApiService.Instance.CreatePartie(dimension: dim, maxTime: time);
            Debug.Log($"Partie {ApiService.Instance.CurrentPartieId} créée (Dimension: {dim}, Temps: {time}) !");
        }
        catch (Exception e) { Debug.LogError("[StartGame] " + e.Message); }
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
