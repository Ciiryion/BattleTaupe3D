
using BattleTaupe3D.Entities;
using Bogus;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Diagnostics;

namespace BattleTaupe3D
{
    public class GameEventLoop
    {
        private static IMongoCollection<BsonDocument> _event;
        private static readonly string[] items = ["VIDE", "TOUCHE", "DETRUIT"];

        public static Task Setup(IMongoDatabase mongo)
        {
            _event = mongo.GetCollection<BsonDocument>("events");
            return Task.CompletedTask;
        }

        public static Task ClearAll() =>
            _event.DeleteManyAsync(FilterDefinition<BsonDocument>.Empty);

        // ─── Runtime : écriture ───────────────────────────────────────────────

        /// <summary>Enregistre un événement pendant une partie en cours.</summary>
        public static async Task RecordEvent(int partieId, int joueurId, string typeAction, BsonDocument details)
        {
            var doc = new BsonDocument
            {
                { "partie_id", partieId },
                { "joueur_id", joueurId },
                { "type_action", typeAction },
                { "timestamp", DateTime.UtcNow },
                { "details", details }
            };
            await _event.InsertOneAsync(doc);
        }

        /// <summary>Raccourci pour enregistrer un tir.</summary>
        public static Task RecordTir(int partieId, int joueurId,
            int px, int py, int pz,
            float dx, float dy, float dz,
            string resultat, int degats)
        {
            var details = new BsonDocument
            {
                { "pos_depart", new BsonDocument { { "x", px }, { "y", py }, { "z", pz } } },
                { "vecteur_dir", new BsonDocument { { "x", dx }, { "y", dy }, { "z", dz } } },
                { "resultat", resultat },
                { "degats", degats }
            };
            return RecordEvent(partieId, joueurId, "TIR", details);
        }

        /// <summary>Raccourci pour enregistrer un déplacement.</summary>
        public static Task RecordDeplacement(int partieId, int joueurId,
            int px, int py, int pz, float rotationY)
        {
            var details = new BsonDocument
            {
                { "pos", new BsonDocument { { "x", px }, { "y", py }, { "z", pz } } },
                { "rotation_y", rotationY }
            };
            return RecordEvent(partieId, joueurId, "DEPLACEMENT", details);
        }

        // ─── Runtime : lecture ────────────────────────────────────────────────

        /// <summary>Tous les événements d'une partie, triés chronologiquement.</summary>
        public static async Task<List<BsonDocument>> GetEventsByPartie(int partieId)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("partie_id", partieId);
            var sort = Builders<BsonDocument>.Sort.Ascending("timestamp");
            return await _event.Find(filter).Sort(sort).ToListAsync();
        }

        /// <summary>Événements d'un joueur, optionnellement filtrés par partie.</summary>
        public static async Task<List<BsonDocument>> GetEventsByJoueur(int joueurId, int? partieId = null)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("joueur_id", joueurId);
            if (partieId.HasValue)
                filter &= Builders<BsonDocument>.Filter.Eq("partie_id", partieId.Value);
            var sort = Builders<BsonDocument>.Sort.Ascending("timestamp");
            return await _event.Find(filter).Sort(sort).ToListAsync();
        }

        /// <summary>Nombre de tirs d'un joueur dans une partie.</summary>
        public static Task<long> CountTirs(int partieId, int joueurId)
        {
            var filter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("partie_id", partieId),
                Builders<BsonDocument>.Filter.Eq("joueur_id", joueurId),
                Builders<BsonDocument>.Filter.Eq("type_action", "TIR")
            );
            return _event.CountDocumentsAsync(filter);
        }

        /// <summary>
        /// Statistiques de tir agrégées par joueur pour une partie :
        /// nombre de tirs, total de dégâts, nombre de touches.
        /// </summary>
        public static async Task<List<BsonDocument>> GetTirStatsByPartie(int partieId)
        {
            var pipeline = new[]
            {
                new BsonDocument("$match", new BsonDocument
                {
                    { "partie_id", partieId },
                    { "type_action", "TIR" }
                }),
                new BsonDocument("$group", new BsonDocument
                {
                    { "_id", "$joueur_id" },
                    { "nb_tirs",   new BsonDocument("$sum", 1) },
                    { "degats_total", new BsonDocument("$sum", "$details.degats") },
                    { "nb_touches", new BsonDocument("$sum", new BsonDocument("$cond", new BsonArray
                        {
                            new BsonDocument("$eq", new BsonArray { "$details.resultat", "TOUCHE" }),
                            1, 0
                        }))
                    }
                }),
                new BsonDocument("$sort", new BsonDocument("degats_total", -1))
            };
            return await _event.Aggregate<BsonDocument>(pipeline).ToListAsync();
        }

        /// <summary>Supprime tous les événements d'une partie (ex : reset / annulation).</summary>
        public static async Task DeleteEventsByPartie(int partieId)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("partie_id", partieId);
            await _event.DeleteManyAsync(filter);
        }

        public static List<BsonDocument> GenerateMongoEvents(List<PlayEntity> plays, List<PartieEntity> parties, Faker faker)
        {
            List<BsonDocument> events = [];
            foreach (var play in plays)
            {
                var partie = parties.First(p => p.Id == play.PartieId);
                int n = partie.Dimension;

                int numEvents = faker.Random.Int(5, 15);
                for (int j = 0; j < numEvents; j++)
                {
                    bool isTir = faker.Random.Bool();
                    var doc = new BsonDocument
                    {
                        { "partie_id", play.PartieId },
                        { "joueur_id", play.PlayerId },
                        { "type_action", isTir ? "TIR" : "DEPLACEMENT" },
                        { "timestamp", faker.Date.Between(partie.StartDate, partie.EndDate) }
                    };

                    var details = new BsonDocument();
                    if (isTir)
                    {
                        details.Add("pos_depart", new BsonDocument { { "x", faker.Random.Int(0, n) }, { "y", faker.Random.Int(0, n) }, { "z", faker.Random.Int(0, n) } });
                        details.Add("vecteur_dir", new BsonDocument { { "x", faker.Random.Float(-1, 1) }, { "y", faker.Random.Float(-1, 1) }, { "z", faker.Random.Float(-1, 1) } });
                        details.Add("resultat", faker.PickRandom(items));
                        details.Add("cible_id", ObjectId.GenerateNewId());
                        details.Add("degats", faker.Random.Int(10, 100));
                    }
                    else
                    {
                        details.Add("pos", new BsonDocument { { "x", faker.Random.Int(0, n) }, { "y", faker.Random.Int(0, n) }, { "z", faker.Random.Int(0, n) } });
                        details.Add("rotation_y", faker.Random.Float(0, 360));
                    }
                    doc.Add("details", details);
                    events.Add(doc);
                }
            }
            return events;
        }

        public static async Task InsertMongoBulk(List<BsonDocument> events)
        {
            if (events.Count <= 0 || _event == null) return;

            var stopwatch = Stopwatch.StartNew();
            await _event.InsertManyAsync(events);
            stopwatch.Stop();
            Console.WriteLine($"Inserted {events.Count} events in {stopwatch.Elapsed.TotalSeconds} seconds.");
        }
    }
}
