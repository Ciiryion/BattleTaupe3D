using BattleTaupe3D;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using Npgsql;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

// postgres driver
var dataSource = new NpgsqlDataSourceBuilder(config.GetConnectionString("Postgres")).Build();

await using var connection = await dataSource.OpenConnectionAsync();

await GenerateGames.Generate(connection);
await connection.CloseAsync();

// mongo driver
var client = new MongoClient(config.GetConnectionString("MongoDB"));
var database = client.GetDatabase(config["MongoDB:Database"]);
await GameEventLoop.Setup(database);

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