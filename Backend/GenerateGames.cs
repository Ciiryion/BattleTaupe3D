using BattleTaupe3D.Entities;
using Bogus;
using Npgsql;
using System.Diagnostics;

namespace BattleTaupe3D
{
    public class GenerateGames
    {
        private static Bogus.Faker faker = new();
        private static GameEntity? game;

        private static int playerIdCounter = 1;
        private static int gameIdCounter = 1;

        private const int batchSize = 100;
        private const int numberOfGames = 1_000;

        public async static Task Generate(NpgsqlConnection connection)
        {
            Randomizer.Seed = new Random(123456789);

            await ExecuteQuery(connection, $"DELETE FROM public.\"jouer\"");
            await ExecuteQuery(connection, $"DELETE FROM public.\"posseder\"");
            await ExecuteQuery(connection, $"DELETE FROM public.\"partie\"");
            await ExecuteQuery(connection, $"DELETE FROM public.\"joueur\"");
            await ExecuteQuery(connection, $"DELETE FROM public.\"jeu\"");

            game = new GameEntity() { Name = $"BattleTaupe3D", Id = Randomizer.Seed.Next() };

            await ExecuteQuery(connection, $"INSERT INTO public.\"jeu\" (\"id\", \"nom\") VALUES ({game.Id}, '{game.Name}')");

            List<PlayerEntity> players = GeneratePlayers();
            await InsertPlayersBulk(connection, players);

            await InsertPossederBulk(connection, players, game.Id);

            for (int i = 0; i < numberOfGames; i += batchSize)
            {
                List<PartieEntity> parties = GenerateParties();

                await InsertPartiesBulk(connection, parties);

                List<PlayEntity> plays = GeneratePlays(players, parties);

                await InsertPlayBulk(connection, plays);

                await GameEventLoop.InsertMongoBulk(GameEventLoop.GenerateMongoEvents(plays, parties, faker));
            }
        }

        private static List<PlayEntity> GeneratePlays(List<PlayerEntity> players, List<PartieEntity> parties)
        {
            List<PlayEntity> plays = [];
            foreach (var partie in parties)
            {
                int numberOfPlayers = faker.Random.Int(2, 6);
                var selectedPlayers = faker.PickRandom(players, numberOfPlayers).ToList();
                foreach (var player in selectedPlayers)
                {
                    plays.Add(new PlayEntity
                    {
                        PlayerId = player.Id,
                        PartieId = partie.Id,
                        Score = faker.Random.Int(0, 1000),
                        IsAdmin = faker.Random.Bool(0.1f),
                        IsWinner = false,
                    });
                }
                var winner = faker.PickRandom(selectedPlayers);
                var playToUpdate = plays.First(p => p.PlayerId == winner.Id && p.PartieId == partie.Id);
                playToUpdate.IsWinner = true;
            }
            return plays;
        }

        private static List<PlayerEntity> GeneratePlayers()
        {
            List<PlayerEntity> players = [];
            for (int i = 0; i < batchSize; i++)
            {
                players.Add(new PlayerEntity
                {
                    Id = playerIdCounter++,
                    Name = faker.Name.FullName(),
                    Birth = faker.Date.Past(30, DateTime.Now.AddYears(-18)),
                    Location = faker.Address.City(),
                    Email = faker.Internet.Email(),
                    Password = faker.Internet.Password(),
                });
            }
            return players;
        }

        private static List<PartieEntity> GenerateParties()
        {
            List<PartieEntity> parties = [];

            for (int i = 0; i < batchSize; i++)
            {
                var startDate = faker.Date.Past(1);
                var endDate = startDate.AddMinutes(faker.Random.Int(1, 120));
                var dimension = faker.Random.Int(3, 10);
                var maxTime = faker.Random.Int(1, 120);
                var gameTime = faker.Random.Int(1, maxTime);

                parties.Add(new PartieEntity
                {
                    Id = gameIdCounter++,
                    StartDate = startDate,
                    EndDate = endDate,
                    Dimension = dimension,
                    MaxTime = maxTime,
                    GameTime = gameTime,
                    GameId = game?.Id ?? 0,
                });
            }

            return parties;
        }

        private static async Task ExecuteQuery(NpgsqlConnection connection, string query)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = query;
            await command.ExecuteNonQueryAsync();
        }

        private static async Task InsertPartiesBulk(NpgsqlConnection connection, List<PartieEntity> parties)
        {
            var stopwatch = Stopwatch.StartNew();

            var copyCommand = $"COPY public.\"partie\" (\"id\", \"date_fin\", \"dimension\", \"date_debut\", \"temps_max\", \"temps_ecoule\", \"id_jeu\") FROM STDIN (FORMAT BINARY)";

            using var writer = connection.BeginBinaryImport(copyCommand);

            foreach (var partie in parties)
            {
                writer.StartRow();
                writer.Write(partie.Id, NpgsqlTypes.NpgsqlDbType.Integer);
                writer.Write(partie.EndDate, NpgsqlTypes.NpgsqlDbType.Timestamp);
                writer.Write(partie.Dimension, NpgsqlTypes.NpgsqlDbType.Integer);
                writer.Write(partie.StartDate, NpgsqlTypes.NpgsqlDbType.Timestamp);
                writer.Write(partie.MaxTime, NpgsqlTypes.NpgsqlDbType.Integer);
                writer.Write(partie.GameTime, NpgsqlTypes.NpgsqlDbType.Integer);
                writer.Write(partie.GameId, NpgsqlTypes.NpgsqlDbType.Integer);
            }

            await writer.CompleteAsync();

            stopwatch.Stop();
            Console.WriteLine($"Inserted {parties.Count} parties in {stopwatch.Elapsed.TotalSeconds} seconds.");
        }

        private static async Task InsertPlayersBulk(NpgsqlConnection connection, List<PlayerEntity> players)
        {
            var copyCommand = $"COPY public.\"joueur\" (\"id\", \"nom\", \"creation\", \"naissance\", \"adresse\", \"email\", \"mdp\") FROM STDIN (FORMAT BINARY)";
            using var writer = connection.BeginBinaryImport(copyCommand);
            foreach (var player in players)
            {
                writer.StartRow();
                writer.Write(player.Id, NpgsqlTypes.NpgsqlDbType.Integer);
                writer.Write(player.Name, NpgsqlTypes.NpgsqlDbType.Text);
                writer.Write(player.Creation, NpgsqlTypes.NpgsqlDbType.Timestamp);
                writer.Write(player.Birth, NpgsqlTypes.NpgsqlDbType.Date);
                writer.Write(player.Location, NpgsqlTypes.NpgsqlDbType.Text);
                writer.Write(player.Email, NpgsqlTypes.NpgsqlDbType.Text);
                writer.Write(player.Password, NpgsqlTypes.NpgsqlDbType.Text);
            }
            await writer.CompleteAsync();
        }

        private static async Task InsertPlayBulk(NpgsqlConnection connection, List<PlayEntity> plays)
        {
            var stopwatch = Stopwatch.StartNew();
            var copyCommand = $"COPY public.\"jouer\" (\"id_joueur\", \"id_partie\", \"score\", \"admin_\", \"vainqueur\") FROM STDIN (FORMAT BINARY)";
            using var writer = connection.BeginBinaryImport(copyCommand);
            foreach (var play in plays)
            {
                writer.StartRow();
                writer.Write(play.PlayerId, NpgsqlTypes.NpgsqlDbType.Integer);
                writer.Write(play.PartieId, NpgsqlTypes.NpgsqlDbType.Integer);
                writer.Write(play.Score, NpgsqlTypes.NpgsqlDbType.Integer);
                writer.Write(play.IsAdmin, NpgsqlTypes.NpgsqlDbType.Boolean);
                writer.Write(play.IsWinner, NpgsqlTypes.NpgsqlDbType.Boolean);
            }
            await writer.CompleteAsync();
            stopwatch.Stop();
            Console.WriteLine($"Inserted {plays.Count} plays in {stopwatch.Elapsed.TotalSeconds} seconds.");
        }

        private static async Task InsertPossederBulk(NpgsqlConnection connection, List<PlayerEntity> players, int gameId)
        {
            var stopwatch = Stopwatch.StartNew();
            var copyCommand = $"COPY public.\"posseder\" (\"id\", \"id_jeu\") FROM STDIN (FORMAT BINARY)";

            using var writer = connection.BeginBinaryImport(copyCommand);
            foreach (var player in players)
            {
                writer.StartRow();
                writer.Write(player.Id, NpgsqlTypes.NpgsqlDbType.Integer);
                writer.Write(gameId, NpgsqlTypes.NpgsqlDbType.Integer);
            }
            await writer.CompleteAsync();

            stopwatch.Stop();
            Console.WriteLine($"Lié {players.Count} joueurs au jeu {gameId} en {stopwatch.Elapsed.TotalSeconds}s.");
        }
    }
}
