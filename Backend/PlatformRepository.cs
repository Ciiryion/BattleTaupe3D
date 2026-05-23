using BattleTaupe3D.Entities;
using Npgsql;
using NpgsqlTypes;

namespace BattleTaupe3D
{
    public class PlatformRepository(NpgsqlConnection connection)
    {
        private readonly NpgsqlConnection _connection = connection;

        // ─── Joueur ───────────────────────────────────────────────────────────

        public async Task<PlayerEntity?> GetJoueurById(int id)
        {
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                SELECT id, nom, creation, naissance, adresse, email, mdp
                FROM public."joueur"
                WHERE id = @id
                """;
            cmd.Parameters.AddWithValue("id", NpgsqlDbType.Integer, id);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;
            return MapJoueur(reader);
        }

        public async Task<PlayerEntity?> GetJoueurByEmail(string email)
        {
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                SELECT id, nom, creation, naissance, adresse, email, mdp
                FROM public."joueur"
                WHERE email = @email
                """;
            cmd.Parameters.AddWithValue("email", NpgsqlDbType.Text, email);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;
            return MapJoueur(reader);
        }

        public async Task CreateJoueur(PlayerEntity player)
        {
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO public."joueur" (id, nom, creation, naissance, adresse, email, mdp)
                VALUES (@id, @nom, @creation, @naissance, @adresse, @email, @mdp)
                """;
            cmd.Parameters.AddWithValue("id", NpgsqlDbType.Integer, player.Id);
            cmd.Parameters.AddWithValue("nom", NpgsqlDbType.Text, player.Name);
            cmd.Parameters.AddWithValue("creation", NpgsqlDbType.Timestamp, player.Creation);
            cmd.Parameters.AddWithValue("naissance", NpgsqlDbType.Date, player.Birth);
            cmd.Parameters.AddWithValue("adresse", NpgsqlDbType.Text, player.Location);
            cmd.Parameters.AddWithValue("email", NpgsqlDbType.Text, player.Email);
            cmd.Parameters.AddWithValue("mdp", NpgsqlDbType.Text, player.Password);
            await cmd.ExecuteNonQueryAsync();
        }

        private static PlayerEntity MapJoueur(NpgsqlDataReader r) => new()
        {
            Id = r.GetInt32(0),
            Name = r.GetString(1),
            Creation = r.GetDateTime(2),
            Birth = r.GetDateTime(3),
            Location = r.GetString(4),
            Email = r.GetString(5),
            Password = r.GetString(6),
        };

        // ─── Jeu ─────────────────────────────────────────────────────────────

        public async Task<GameEntity?> GetJeuById(int id)
        {
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = """SELECT id, nom FROM public."jeu" WHERE id = @id""";
            cmd.Parameters.AddWithValue("id", NpgsqlDbType.Integer, id);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;
            return new GameEntity { Id = reader.GetInt32(0), Name = reader.GetString(1) };
        }

        public async Task<List<GameEntity>> GetJeux()
        {
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = """SELECT id, nom FROM public."jeu" ORDER BY nom""";

            var result = new List<GameEntity>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                result.Add(new GameEntity { Id = reader.GetInt32(0), Name = reader.GetString(1) });
            return result;
        }

        // ─── Partie ───────────────────────────────────────────────────────────

        public async Task CreatePartie(PartieEntity partie)
        {
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO public."partie" (id, date_debut, date_fin, dimension, temps_max, temps_ecoule, id_jeu)
                VALUES (@id, @dateDebut, @dateFin, @dimension, @tempsMax, @tempsEcoule, @idJeu)
                """;
            cmd.Parameters.AddWithValue("id", NpgsqlDbType.Integer, partie.Id);
            cmd.Parameters.AddWithValue("dateDebut", NpgsqlDbType.Timestamp, partie.StartDate);
            cmd.Parameters.AddWithValue("dateFin", NpgsqlDbType.Timestamp, partie.EndDate);
            cmd.Parameters.AddWithValue("dimension", NpgsqlDbType.Integer, partie.Dimension);
            cmd.Parameters.AddWithValue("tempsMax", NpgsqlDbType.Integer, partie.MaxTime);
            cmd.Parameters.AddWithValue("tempsEcoule", NpgsqlDbType.Integer, partie.GameTime);
            cmd.Parameters.AddWithValue("idJeu", NpgsqlDbType.Integer, partie.GameId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task EndPartie(int partieId, DateTime endDate, int gameTime)
        {
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                UPDATE public."partie"
                SET date_fin = @dateFin, temps_ecoule = @tempsEcoule
                WHERE id = @id
                """;
            cmd.Parameters.AddWithValue("dateFin", NpgsqlDbType.Timestamp, endDate);
            cmd.Parameters.AddWithValue("tempsEcoule", NpgsqlDbType.Integer, gameTime);
            cmd.Parameters.AddWithValue("id", NpgsqlDbType.Integer, partieId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<PartieEntity?> GetPartieById(int id)
        {
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                SELECT id, date_debut, date_fin, dimension, temps_max, temps_ecoule, id_jeu
                FROM public."partie"
                WHERE id = @id
                """;
            cmd.Parameters.AddWithValue("id", NpgsqlDbType.Integer, id);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;
            return MapPartie(reader);
        }

        public async Task<List<PartieEntity>> GetPartiesByJeu(int jeuId)
        {
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                SELECT id, date_debut, date_fin, dimension, temps_max, temps_ecoule, id_jeu
                FROM public."partie"
                WHERE id_jeu = @jeuId
                ORDER BY date_debut DESC
                """;
            cmd.Parameters.AddWithValue("jeuId", NpgsqlDbType.Integer, jeuId);

            var result = new List<PartieEntity>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                result.Add(MapPartie(reader));
            return result;
        }

        private static PartieEntity MapPartie(NpgsqlDataReader r) => new()
        {
            Id = r.GetInt32(0),
            StartDate = r.GetDateTime(1),
            EndDate = r.GetDateTime(2),
            Dimension = r.GetInt32(3),
            MaxTime = r.GetInt32(4),
            GameTime = r.GetInt32(5),
            GameId = r.GetInt32(6),
        };

        // ─── Jouer ────────────────────────────────────────────────────────────

        public async Task AddJoueurToPartie(PlayEntity play)
        {
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO public."jouer" (id_joueur, id_partie, score, admin_, vainqueur)
                VALUES (@joueurId, @partieId, @score, @admin, @vainqueur)
                """;
            cmd.Parameters.AddWithValue("joueurId", NpgsqlDbType.Integer, play.PlayerId);
            cmd.Parameters.AddWithValue("partieId", NpgsqlDbType.Integer, play.PartieId);
            cmd.Parameters.AddWithValue("score", NpgsqlDbType.Integer, play.Score);
            cmd.Parameters.AddWithValue("admin", NpgsqlDbType.Boolean, play.IsAdmin);
            cmd.Parameters.AddWithValue("vainqueur", NpgsqlDbType.Boolean, play.IsWinner);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UpdateScore(int joueurId, int partieId, int score)
        {
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                UPDATE public."jouer"
                SET score = @score
                WHERE id_joueur = @joueurId AND id_partie = @partieId
                """;
            cmd.Parameters.AddWithValue("score", NpgsqlDbType.Integer, score);
            cmd.Parameters.AddWithValue("joueurId", NpgsqlDbType.Integer, joueurId);
            cmd.Parameters.AddWithValue("partieId", NpgsqlDbType.Integer, partieId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task SetWinner(int joueurId, int partieId)
        {
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                UPDATE public."jouer"
                SET vainqueur = true
                WHERE id_joueur = @joueurId AND id_partie = @partieId
                """;
            cmd.Parameters.AddWithValue("joueurId", NpgsqlDbType.Integer, joueurId);
            cmd.Parameters.AddWithValue("partieId", NpgsqlDbType.Integer, partieId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<List<PlayEntity>> GetLeaderboard(int partieId)
        {
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                SELECT id_joueur, id_partie, score, admin_, vainqueur
                FROM public."jouer"
                WHERE id_partie = @partieId
                ORDER BY score DESC
                """;
            cmd.Parameters.AddWithValue("partieId", NpgsqlDbType.Integer, partieId);

            return await ReadPlays(cmd);
        }

        public async Task<List<PlayEntity>> GetPlayerStats(int joueurId)
        {
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                SELECT id_joueur, id_partie, score, admin_, vainqueur
                FROM public."jouer"
                WHERE id_joueur = @joueurId
                ORDER BY id_partie DESC
                """;
            cmd.Parameters.AddWithValue("joueurId", NpgsqlDbType.Integer, joueurId);

            return await ReadPlays(cmd);
        }

        private static async Task<List<PlayEntity>> ReadPlays(NpgsqlCommand cmd)
        {
            var result = new List<PlayEntity>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                result.Add(new PlayEntity
                {
                    PlayerId = reader.GetInt32(0),
                    PartieId = reader.GetInt32(1),
                    Score = reader.GetInt32(2),
                    IsAdmin = reader.GetBoolean(3),
                    IsWinner = reader.GetBoolean(4),
                });
            return result;
        }

        // ─── Posseder ─────────────────────────────────────────────────────────

        public async Task AddPosseder(int joueurId, int jeuId)
        {
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO public."posseder" (id, id_jeu)
                VALUES (@joueurId, @jeuId)
                ON CONFLICT DO NOTHING
                """;
            cmd.Parameters.AddWithValue("joueurId", NpgsqlDbType.Integer, joueurId);
            cmd.Parameters.AddWithValue("jeuId", NpgsqlDbType.Integer, jeuId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<bool> JoueurPossedJeu(int joueurId, int jeuId)
        {
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                SELECT COUNT(1) FROM public."posseder"
                WHERE id = @joueurId AND id_jeu = @jeuId
                """;
            cmd.Parameters.AddWithValue("joueurId", NpgsqlDbType.Integer, joueurId);
            cmd.Parameters.AddWithValue("jeuId", NpgsqlDbType.Integer, jeuId);
            return (long)(await cmd.ExecuteScalarAsync())! > 0;
        }
    }
}
