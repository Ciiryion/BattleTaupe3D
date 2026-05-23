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

// ─── Seed ────────────────────────────────────────────────────────────────────
app.MapPost("/api/seed", async (NpgsqlDataSource ds) =>
{
    await GameEventLoop.ClearAll();
    await using var conn = await ds.OpenConnectionAsync();
    await GenerateGames.Generate(conn);
    return Results.Ok("Données générées.");
});

// ─── Jeux ────────────────────────────────────────────────────────────────────
app.MapGet("/api/games", async (NpgsqlDataSource ds) =>
{
    await using var conn = await ds.OpenConnectionAsync();
    return Results.Ok(await new PlatformRepository(conn).GetJeux());
});

// ─── Auth ────────────────────────────────────────────────────────────────────
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
        Id = IdGen.Next(), Name = req.Name, Email = req.Email,
        Password = req.Password, Birth = birthDate,
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

// ─── Parties ─────────────────────────────────────────────────────────────────
app.MapPost("/api/parties", async (CreatePartieDto req, NpgsqlDataSource ds) =>
{
    await using var conn = await ds.OpenConnectionAsync();
    var repo = new PlatformRepository(conn);
    var partie = new PartieEntity
    {
        Id = IdGen.Next(), StartDate = DateTime.Now, EndDate = DateTime.Now,
        Dimension = req.Dimension, MaxTime = req.MaxTime, GameTime = 0, GameId = req.JeuId,
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

// ─── Events ──────────────────────────────────────────────────────────────────
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

app.Run();

// ─── DTOs ────────────────────────────────────────────────────────────────────
record RegisterDto(string Name, string Email, string Password, string Birth, string? Location);
record LoginDto(string Email, string Password);
record CreatePartieDto(int JeuId, int Dimension, int MaxTime, int JoueurId);
record JoinDto(int JoueurId);
record EndPartieDto(int GameTime, int VainqueurId);
record ScoreDto(int Score);
record TirDto(int PartieId, int JoueurId, int Px, int Py, int Pz, float Dx, float Dy, float Dz, string Resultat, int Degats);
record DeplacementDto(int PartieId, int JoueurId, int Px, int Py, int Pz, float RotationY);

// ─── Génération d'IDs (évite les conflits avec le seed) ──────────────────────
static class IdGen
{
    private static int _counter = 100_000;
    public static int Next() => Interlocked.Increment(ref _counter);
}
