
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

        public async static Task Setup(IMongoDatabase mongo)
        {
            _event = mongo.GetCollection<BsonDocument>("events");

            _event.DeleteMany(FilterDefinition<BsonDocument>.Empty);
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
