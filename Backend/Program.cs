using BattleTaupe3D;
using BattleTaupe3D.Entities;
using MongoDB.Driver;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

var dataSource = new NpgsqlDataSourceBuilder(cfg.GetConnectionString("Postgres")).Build();
var mongo = new MongoClient(cfg.GetConnectionString("MongoDB")).GetDatabase(cfg["MongoDB:Database"]);

await GameEventLoop.Setup(mongo);

builder.Services.AddSingleton(dataSource);
builder.Services.AddCors();

var app = builder.Build();
app.UseCors(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

Console.WriteLine("""
     /$$$$$$$              /$$     /$$     /$$                               
    | $$__  $$            | $$    | $$    | $$                               
    | $$  \ $$  /$$$$$$  /$$$$$$ /$$$$$$  | $$  /$$$$$$                      
    | $$$$$$$  |____  $$|_  $$_/|_  $$_/  | $$ /$$__  $$                     
    | $$__  $$  /$$$$$$$  | $$    | $$    | $$| $$$$$$$$                     
    | $$  \ $$ /$$__  $$  | $$ /$$| $$ /$$| $$| $$_____/                     
    | $$$$$$$/|  $$$$$$$  |  $$$$/|  $$$$/| $$|  $$$$$$$                     
    |_______/  \_______/   \___/   \___/  |__/ \_______/                     

     /$$$$$$$$                                             /$$$$$$  /$$$$$$$
    |__  $$__/                                            /$$__  $$| $$__  $$
       | $$  /$$$$$$  /$$   /$$  /$$$$$$   /$$$$$$       |__/  \ $$| $$  \ $$
       | $$ |____  $$| $$  | $$ /$$__  $$ /$$__  $$         /$$$$$/| $$  | $$
       | $$  /$$$$$$$| $$  | $$| $$  \ $$| $$$$$$$$        |___  $$| $$  | $$
       | $$ /$$__  $$| $$  | $$| $$  | $$| $$_____/       /$$  \ $$| $$  | $$
       | $$|  $$$$$$$|  $$$$$$/| $$$$$$$/|  $$$$$$$      |  $$$$$$/| $$$$$$$/
       |__/ \_______/ \______/ | $$____/  \_______/       \______/ |_______/
                               | $$
                               | $$
                               |__/  copyright © 2026 BattleTaupe3D, all rights reserved.
    """);

app.MapPost("/api/seed", async (NpgsqlDataSource ds) =>
{
    await GameEventLoop.ClearAll();
    await using var conn = await ds.OpenConnectionAsync();
    await GenerateGames.Generate(conn);
    return Results.Ok("Données générées.");
});

app.MapGet("/api/games", async (NpgsqlDataSource ds) =>
{
    await using var conn = await ds.OpenConnectionAsync();
    return Results.Ok(await new PlatformRepository(conn).GetJeux());
});

app.MapPost("/api/auth/register", async (RegisterDto req, NpgsqlDataSource ds) =>
{
    if (!DateTime.TryParse(req.Birth, out var birthDate))
        return Results.BadRequest("Format de date invalide. Utilisez YYYY-MM-DD.");
    await using var conn = await ds.OpenConnectionAsync();
    var repo = new PlatformRepository(conn);
    if (await repo.GetJoueurByEmail(req.Email) != null)
        return Results.Conflict("Email déjà utilisé.");
    var player = new PlayerEntity
    {
        Id = IdGen.Next(),
        Name = req.Name,
        Email = req.Email,
        Password = req.Password,
        Birth = birthDate,
        Location = req.Location ?? string.Empty,
    };
    await repo.CreateJoueur(player);
    return Results.Ok(new { player.Id, player.Name });
});

app.MapPost("/api/auth/login", async (LoginDto req, NpgsqlDataSource ds) =>
{
    await using var conn = await ds.OpenConnectionAsync();
    var player = await new PlatformRepository(conn).GetJoueurByEmail(req.Email);
    if (player == null || player.Password != req.Password)
        return Results.Unauthorized();
    return Results.Ok(new { playerId = player.Id, player.Name });
});

app.MapPost("/api/parties", async (CreatePartieDto req, NpgsqlDataSource ds) =>
{
    await using var conn = await ds.OpenConnectionAsync();
    var repo = new PlatformRepository(conn);
    var partie = new PartieEntity
    {
        Id = IdGen.Next(),
        StartDate = DateTime.Now,
        EndDate = DateTime.Now,
        Dimension = req.Dimension,
        MaxTime = req.MaxTime,
        GameTime = 0,
        GameId = req.JeuId,
    };
    await repo.CreatePartie(partie);
    await repo.AddJoueurToPartie(new PlayEntity
    { PlayerId = req.JoueurId, PartieId = partie.Id, Score = 0, IsAdmin = true });
    return Results.Ok(new { id = partie.Id });
});

app.MapPost("/api/parties/{id}/join", async (int id, JoinDto req, NpgsqlDataSource ds) =>
{
    await using var conn = await ds.OpenConnectionAsync();
    await new PlatformRepository(conn).AddJoueurToPartie(
        new PlayEntity { PlayerId = req.JoueurId, PartieId = id, Score = 0 });
    return Results.Ok();
});

app.MapPost("/api/parties/{id}/end", async (int id, EndPartieDto req, NpgsqlDataSource ds) =>
{
    await using var conn = await ds.OpenConnectionAsync();
    var repo = new PlatformRepository(conn);
    await repo.EndPartie(id, DateTime.Now, req.GameTime);
    await repo.SetWinner(req.VainqueurId, id);
    return Results.Ok();
});

app.MapPut("/api/parties/{id}/score/{joueurId}", async (int id, int joueurId, ScoreDto req, NpgsqlDataSource ds) =>
{
    await using var conn = await ds.OpenConnectionAsync();
    await new PlatformRepository(conn).UpdateScore(joueurId, id, req.Score);
    return Results.Ok();
});

app.MapGet("/api/parties/{id}/leaderboard", async (int id, NpgsqlDataSource ds) =>
{
    await using var conn = await ds.OpenConnectionAsync();
    return Results.Ok(await new PlatformRepository(conn).GetLeaderboard(id));
});

app.MapPost("/api/events/tir", async (TirDto req) =>
{
    await GameEventLoop.RecordTir(req.PartieId, req.JoueurId,
        req.Px, req.Py, req.Pz, req.Dx, req.Dy, req.Dz, req.Resultat, req.Degats);
    return Results.Ok();
});

app.MapPost("/api/events/deplacement", async (DeplacementDto req) =>
{
    await GameEventLoop.RecordDeplacement(req.PartieId, req.JoueurId,
        req.Px, req.Py, req.Pz, req.RotationY);
    return Results.Ok();
});

app.MapGet("/api/events/{partieId}/stats", async (int partieId) =>
    Results.Ok(await GameEventLoop.GetTirStatsByPartie(partieId)));


app.MapDelete("/api/joueurs/{id}", async (int id, NpgsqlDataSource ds) =>
{
    await using var conn = await ds.OpenConnectionAsync();
    await new PlatformRepository(conn).DeleteJoueur(id);
    return Results.NoContent();
});


app.MapPatch("/api/parties/{id}/pause", async (int id, PauseDto req, NpgsqlDataSource ds) =>
{
    await using var conn = await ds.OpenConnectionAsync();
    await new PlatformRepository(conn).PausePartie(id, req.TempsEcoule);
    return Results.Ok(new { message = "Partie mise en pause.", tempsEcoule = req.TempsEcoule });
});

app.MapGet("/api/parties/{id}", async (int id, NpgsqlDataSource ds) =>
{
    await using var conn = await ds.OpenConnectionAsync();
    var partie = await new PlatformRepository(conn).GetPartieById(id);
    if (partie == null) return Results.NotFound();
    return Results.Ok(new
    {
        partie.Id,
        partie.Dimension,
        partie.MaxTime,
        partie.GameTime,
        tempsRestant = partie.MaxTime - partie.GameTime
    });
});

app.MapGet("/api/parties/{id}/record", async (int id) =>
    Results.Ok(await GameEventLoop.GetEventsByPartie(id)));

app.MapGet("/api/parties/{id}/joueurs", async (int id, NpgsqlDataSource ds) =>
{
    await using var conn = await ds.OpenConnectionAsync();
    return Results.Ok(await new PlatformRepository(conn).GetJoueursParPartie(id));
});


app.MapGet("/api/stats/joueur-plus-parties/{jeuId}", async (int jeuId, NpgsqlDataSource ds) =>
{
    await using var conn = await ds.OpenConnectionAsync();
    var result = await new PlatformRepository(conn).GetJoueurPlusParties(jeuId);
    return result == null ? Results.NotFound() : Results.Ok(result);
});

app.MapGet("/api/stats/age-moyen/{jeuId}", async (int jeuId, NpgsqlDataSource ds) =>
{
    await using var conn = await ds.OpenConnectionAsync();
    var age = await new PlatformRepository(conn).GetAgeMoyenJoueurs(jeuId);
    return Results.Ok(new { ageMoyen = age });
});

app.MapGet("/api/stats/meilleur-vainqueur", async (NpgsqlDataSource ds) =>
{
    await using var conn = await ds.OpenConnectionAsync();
    var result = await new PlatformRepository(conn).GetMeilleurVainqueur();
    return result == null ? Results.NotFound() : Results.Ok(result);
});

app.MapGet("/api/stats/jeux-classement", async (NpgsqlDataSource ds) =>
{
    await using var conn = await ds.OpenConnectionAsync();
    return Results.Ok(await new PlatformRepository(conn).GetJeuxClassement());
});

app.MapGet("/api/stats/joueurs-exclusifs/{jeuId}", async (int jeuId, NpgsqlDataSource ds) =>
{
    await using var conn = await ds.OpenConnectionAsync();
    var nb = await new PlatformRepository(conn).GetNbJoueursExclusifs(jeuId);
    return Results.Ok(new { joueurs_exclusifs = nb });
});

app.MapGet("/api/stats/evolution-semaine/{jeuId}", async (int jeuId, NpgsqlDataSource ds) =>
{
    await using var conn = await ds.OpenConnectionAsync();
    return Results.Ok(await new PlatformRepository(conn).GetEvolutionParSemaine(jeuId));
});

app.MapGet("/api/events/{partieId}/deplacements/{joueurId}", async (int partieId, int joueurId) =>
    Results.Ok(await GameEventLoop.GetDeplacementsJoueur(partieId, joueurId)));

app.MapGet("/api/events/classement-degats", async () =>
{
    var docs = await GameEventLoop.GetClassementDegats();
    return Results.Ok(docs.Select(d => new
    {
        joueurId     = d.Contains("_id")          ? d["_id"].AsInt32          : 0,
        degats_total = d.Contains("degats_total") ? d["degats_total"].AsInt32 : 0,
        nb_tirs      = d.Contains("nb_tirs")      ? d["nb_tirs"].AsInt32      : 0,
    }));
});

{
    await using var initConn = await dataSource.OpenConnectionAsync();
    await using var cmd = initConn.CreateCommand();
    cmd.CommandText = """
        SELECT GREATEST(
            COALESCE((SELECT MAX(id) FROM public."joueur"), 100000),
            COALESCE((SELECT MAX(id) FROM public."partie"), 100000)
        )
        """;
    var maxId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
    IdGen.Initialize(maxId);
}

app.Run();

record RegisterDto(string Name, string Email, string Password, string Birth, string? Location);
record LoginDto(string Email, string Password);
record CreatePartieDto(int JeuId, int Dimension, int MaxTime, int JoueurId);
record JoinDto(int JoueurId);
record EndPartieDto(int GameTime, int VainqueurId);
record ScoreDto(int Score);
record TirDto(int PartieId, int JoueurId, int Px, int Py, int Pz, float Dx, float Dy, float Dz, string Resultat, int Degats);
record DeplacementDto(int PartieId, int JoueurId, int Px, int Py, int Pz, float RotationY);
record PauseDto(int TempsEcoule);

static class IdGen
{
    private static int _counter = 100_000;
    public static void Initialize(int maxExisting) =>
        Interlocked.Exchange(ref _counter, Math.Max(_counter, maxExisting));
    public static int Next() => Interlocked.Increment(ref _counter);
}
