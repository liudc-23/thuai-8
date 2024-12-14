using System.Text.Json.Serialization;

namespace Thuai.Server.Connection;
 public record Player
    {
        [JsonPropertyName("token")]
        public string Token { get; init; } = "";

        [JsonPropertyName("weapon")]
        public List<Weapon> Weapon { get; init; } = new();

        [JsonPropertyName("armor")]
        public Armor Armor { get; init; } = new();

        [JsonPropertyName("skills")]
        public List<Skill> Skills { get; init; } = new();

        [JsonPropertyName("position")]
        public Position Position { get; init; } = new();
    }