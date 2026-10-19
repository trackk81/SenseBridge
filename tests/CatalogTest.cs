using System;using System.IO;using SenseBridge;
class CatalogTest {
 static void Reject(Action action){try{action();}catch(InvalidDataException){return;}throw new Exception("Invalid catalog accepted");}
 static void Main(string[] args){var index=Store.Json.Deserialize<GameDataIndex>(File.ReadAllText(args[0]));GameDataCatalog.Validate(index);var g=index.Games[0];string url=g.Url;g.Url="https://example.com/payload.sensebridge.json";Reject(()=>GameDataCatalog.Validate(index));g.Url=url+"/../payload.sensebridge.json";Reject(()=>GameDataCatalog.Validate(index));g.Url=url;index.Games.Add(g);Reject(()=>GameDataCatalog.Validate(index));index.Games.RemoveAt(1);g.Size=200*1024*1024;Reject(()=>GameDataCatalog.Validate(index));Console.WriteLine("PASS: catalog validation, trusted library origin, path bounds, duplicates and size limits");}
}
