using System;
using System.Reflection;
using System.Text.Json;
using FF5MonsterSprites.Serialization;
using ImageMagitek;

namespace FF5MonsterSpritesCLI;

public class Program
{
    enum AppAction { Import, Export }

    public static async Task<int> Main(string[] args)
    {
        if (args.Length != 3)
        {
            var executableName = Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location);
            Console.WriteLine($"{executableName} <Import|Export> <Directory> <ff5.smc>");
            return -1;
        }

        var configContents = await File.ReadAllTextAsync("config.json");
        var jsonOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
        jsonOptions.Converters.Add(new HexadecimalJsonConverter());
        var config = JsonSerializer.Deserialize<Config>(configContents, jsonOptions);

        AppAction action;
        if (string.Equals(args[0], "Import", StringComparison.OrdinalIgnoreCase))
        {
            action = AppAction.Import;
        }
        else if (string.Equals(args[0], "Export", StringComparison.OrdinalIgnoreCase))
        {
            action = AppAction.Export;
        }
        else
        {
            Console.WriteLine($"Unrecognized action {args[0]}");
            return -2;
        }

        if (!Directory.Exists(args[1]) && action == AppAction.Import)
        {
            Console.WriteLine($"Directory '{args[1]}' does not exist so there are no files to import");
            return -3;
        }

        if (!Directory.Exists(args[1]) && action == AppAction.Export)
        {
            var info = Directory.CreateDirectory(args[1]);
            if (info.Exists)
                Console.WriteLine($"Created '{args[1]}'");
        }

        if (!File.Exists(args[2]))
        {
            Console.WriteLine($"File '{args[2]}' does not exist");
        }

        if (action == AppAction.Import)
        {

        }
        else if (action == AppAction.Export)
        {
            var serializer = new MonsterSerializer();
            var monsters = await serializer.DeserializeMonsters(args[2]);

            int i = 0;
            foreach (var monster in monsters)
            {
                var sprite = await serializer.DeserializeSprite(args[2], monster);
                var image = new IndexedImage(sprite.Arranger);
                image.Render();
                var path = Path.Combine(args[1], $"Enemy{i:D3}.png");
                image.ExportImage(path, new ImageSharpFileAdapter());
                sprite.DataFile.Dispose();
                i++;
            }
        }

        return 0;
    }
}