using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class ApiService : MonoBehaviour
{
    public static ApiService Instance { get; private set; }

    [SerializeField] private string baseUrl = "http://localhost:5000";

    public int CurrentPlayerId { get; private set; }
    public string CurrentPlayerName { get; private set; } = "";
    public int CurrentPartieId { get; private set; }
    public int DefaultJeuId { get; private set; } = 0;
    private bool _jeuIdFetched = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        _ = FetchDefaultJeuId();
    }

    private Task<string> Post(string path, string json)
    {
        var tcs = new TaskCompletionSource<string>();
        var req = new UnityWebRequest(baseUrl + path, "POST")
        {
            uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
            downloadHandler = new DownloadHandlerBuffer()
        };

        req.SetRequestHeader("Content-Type", "application/json");
        req.SendWebRequest().completed += _ =>
        {
            if (req.result != UnityWebRequest.Result.Success)
                tcs.SetException(new Exception($"[Api] {req.responseCode} - {req.downloadHandler.text}"));
            else
                tcs.SetResult(req.downloadHandler.text);
            req.Dispose();
        };

        return tcs.Task;
    }

    private Task<(string body, long code)> PostRaw(string path, string json)
    {
        var tcs = new TaskCompletionSource<(string, long)>();
        var req = new UnityWebRequest(baseUrl + path, "POST")
        {
            uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
            downloadHandler = new DownloadHandlerBuffer()
        };

        req.SetRequestHeader("Content-Type", "application/json");
        req.SendWebRequest().completed += _ =>
        {
            tcs.SetResult((req.downloadHandler.text, req.responseCode));
            req.Dispose();
        };

        return tcs.Task;
    }

    private Task<string> Get(string path)
    {
        var tcs = new TaskCompletionSource<string>();
        var req = UnityWebRequest.Get(baseUrl + path);

        req.SendWebRequest().completed += _ =>
        {
            if (req.result != UnityWebRequest.Result.Success)
                tcs.SetException(new Exception($"[Api] {req.responseCode} - {req.error}"));
            else
                tcs.SetResult(req.downloadHandler.text);
            req.Dispose();
        };

        return tcs.Task;
    }

    private Task<string> Put(string path, string json)
    {
        var tcs = new TaskCompletionSource<string>();
        var req = new UnityWebRequest(baseUrl + path, "PUT")
        {
            uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
            downloadHandler = new DownloadHandlerBuffer()
        };

        req.SetRequestHeader("Content-Type", "application/json");
        req.SendWebRequest().completed += _ =>
        {
            if (req.result != UnityWebRequest.Result.Success)
                tcs.SetException(new Exception($"[Api] {req.responseCode} - {req.error}"));
            else
                tcs.SetResult(req.downloadHandler.text);
            req.Dispose();
        };

        return tcs.Task;
    }

    public async Task FetchDefaultJeuId()
    {
        try
        {
            var json = await Get("/api/games");
            var list = JsonUtility.FromJson<GameInfoList>("{\"items\":" + json + "}");
            if (list.items is { Length: > 0 })
            {
                DefaultJeuId = list.items[0].id;
                _jeuIdFetched = true;
            }
        }
        catch (Exception e) { Debug.LogWarning("[ApiService] Impossible de récupérer le jeu : " + e.Message); }
    }

    public Task Register(string name, string email, string password, string birth, string location) =>
        Post("/api/auth/register", JsonUtility.ToJson(new RegisterReq
        { name = name, email = email, password = password, birth = birth, location = location, createdAt = DateTime.UtcNow.ToString("o") }));

    public async Task<bool> Login(string email, string password)
    {
        var (json, code) = await PostRaw("/api/auth/login",
            JsonUtility.ToJson(new LoginReq { email = email, password = password }));

        if (code == 401) return false;
        if (code != 200) throw new Exception($"[Api] Login error {code}");

        var r = JsonUtility.FromJson<LoginResponse>(json);
        CurrentPlayerId = r.playerId;
        CurrentPlayerName = r.name;
        return true;
    }

    public async Task CreatePartie(int dimension = 5, int maxTime = 60)
    {
        if (!_jeuIdFetched)
            await FetchDefaultJeuId();
        if (DefaultJeuId == 0)
            throw new Exception("Aucun jeu disponible sur le serveur.");
        var json = await Post("/api/parties", JsonUtility.ToJson(new CreatePartieReq
        { jeuId = DefaultJeuId, dimension = dimension, maxTime = maxTime, joueurId = CurrentPlayerId }));
        CurrentPartieId = JsonUtility.FromJson<IdResponse>(json).id;
    }

    public Task JoinPartie(int partieId) =>
        Post($"/api/parties/{partieId}/join",
            JsonUtility.ToJson(new JoinReq { joueurId = CurrentPlayerId }));

    public Task EndPartie(int vainqueurId, int gameTime) =>
        Post($"/api/parties/{CurrentPartieId}/end",
            JsonUtility.ToJson(new EndPartieReq { vainqueurId = vainqueurId, gameTime = gameTime }));

    public Task UpdateScore(int score) =>
        Put($"/api/parties/{CurrentPartieId}/score/{CurrentPlayerId}",
            JsonUtility.ToJson(new ScoreReq { score = score }));

    public async Task<PlayResult[]> GetLeaderboard()
    {
        var json = await Get($"/api/parties/{CurrentPartieId}/leaderboard");
        return JsonUtility.FromJson<PlayResultList>("{\"items\":" + json + "}").items;
    }

    public Task RecordTir(Vector3Int pos, Vector3 dir, string resultat, int degats) =>
        Post("/api/events/tir", JsonUtility.ToJson(new TirReq
        {
            partieId = CurrentPartieId,
            joueurId = CurrentPlayerId,
            px = pos.x,
            py = pos.y,
            pz = pos.z,
            dx = dir.x,
            dy = dir.y,
            dz = dir.z,
            resultat = resultat,
            degats = degats,
        }));

    public Task RecordDeplacement(Vector3Int pos, float rotationY) =>
        Post("/api/events/deplacement", JsonUtility.ToJson(new DeplacementReq
        {
            partieId = CurrentPartieId,
            joueurId = CurrentPlayerId,
            px = pos.x,
            py = pos.y,
            pz = pos.z,
            rotationY = rotationY,
        }));
    public async Task<DegatsEntry[]> GetClassementDegats()
    {
        var json = await Get("/api/events/classement-degats");
        return JsonUtility.FromJson<DegatsEntryList>("{\"items\":" + json + "}").items;
    }

    public async Task<MeilleurVainqueurResp> GetMeilleurVainqueur()
    {
        var json = await Get("/api/stats/meilleur-vainqueur");
        return JsonUtility.FromJson<MeilleurVainqueurResp>(json);
    }

    public async Task<float> GetAgeMoyen()
    {
        if (DefaultJeuId == 0) await FetchDefaultJeuId();
        var json = await Get($"/api/stats/age-moyen/{DefaultJeuId}");
        return JsonUtility.FromJson<AgeMoyenResp>(json).ageMoyen;
    }

    public async Task<JeuClassement[]> GetJeuxClassement()
    {
        var json = await Get("/api/stats/jeux-classement");
        return JsonUtility.FromJson<JeuClassementList>("{\"items\":" + json + "}").items;
    }
}

[Serializable] class RegisterReq { public string name = "", email = "", password = "", birth = "", location = "", createdAt = ""; }
[Serializable] class LoginReq { public string email = "", password = ""; }
[Serializable] class LoginResponse { public int playerId; public string name = ""; }
[Serializable] class IdResponse { public int id; }
[Serializable] class CreatePartieReq { public int jeuId, dimension, maxTime, joueurId; }
[Serializable] class JoinReq { public int joueurId; }
[Serializable] class EndPartieReq { public int vainqueurId, gameTime; }
[Serializable] class ScoreReq { public int score; }
[Serializable] class TirReq { public int partieId, joueurId, px, py, pz, degats; public float dx, dy, dz; public string resultat = ""; }
[Serializable] class DeplacementReq { public int partieId, joueurId, px, py, pz; public float rotationY; }
[Serializable] class GameInfo { public int id; public string name = ""; }
[Serializable] class GameInfoList { public GameInfo[] items = Array.Empty<GameInfo>(); }
[Serializable] public class PlayResult { public int playerId, partieId, score; public bool isAdmin, isWinner; }
[Serializable] class PlayResultList { public PlayResult[] items = Array.Empty<PlayResult>(); }
[Serializable] public class DegatsEntry { public int joueurId; public int degats_total; public int nb_tirs; }
[Serializable] class DegatsEntryList { public DegatsEntry[] items = Array.Empty<DegatsEntry>(); }
[Serializable] public class MeilleurVainqueurResp { public string nom = ""; public int nb_victoires; }
[Serializable] public class AgeMoyenResp { public float ageMoyen; }
[Serializable] public class JeuClassement { public string nom = ""; public int nb_parties; }
[Serializable] class JeuClassementList { public JeuClassement[] items = Array.Empty<JeuClassement>(); }
