using System;
using System.IO;
using System.Text.Json;

namespace GroupyV.Services
{
    public static class AppSettings
    {
        private static readonly Lazy<string> _connectionString = new(ReadConnectionString);

        public static string ConnectionString => _connectionString.Value;

        private static string ReadConnectionString()
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(configPath))
                throw new FileNotFoundException(
                    "appsettings.json introuvable. Copiez appsettings.example.json en appsettings.json et configurez vos identifiants.",
                    configPath);

            var json = File.ReadAllText(configPath);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement
                .GetProperty("ConnectionStrings")
                .GetProperty("DefaultConnection")
                .GetString()
                ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection manquant dans appsettings.json");
        }
    }
}
