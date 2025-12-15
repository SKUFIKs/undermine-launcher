using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization; 
using System.Threading.Tasks;

namespace UnderMine.Network.NetworkStatus
{
    public class CheckServerStatus
    {
        private static readonly HttpClient client = new HttpClient();

        public async Task<ServerStatus> GetServerStatus()
        {
            var url = "https://api.mcsrvstat.us/3/play.mc-undermine.ru";

            try
            {
                HttpResponseMessage response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    string jsonString = await response.Content.ReadAsStringAsync();

                    var data = JsonSerializer.Deserialize<ServerStatus>(jsonString);
                    return data;
                }
                else
                {
                    Console.WriteLine($"Ошибка API: {response.StatusCode}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка сети: {ex.Message}");
                return null;
            }
        }
    }

    public class ServerStatus
    {
        [JsonPropertyName("online")]
        public bool IsOnline { get; set; }

        [JsonPropertyName("players")]
        public PlayersInfo Players { get; set; }

        [JsonPropertyName("debug")]
        public DebugInfo Debug { get; set; }
    }

    public class PlayersInfo
    {
        [JsonPropertyName("online")]
        public int OnlineCount { get; set; } 
    }

    public class DebugInfo
    {
        [JsonPropertyName("ping")]
        public bool Ping { get; set; }
    }
}


/*
var checker = new CheckServerStatus();
var status = await checker.GetServerStatus();

if (status != null)
{
    Console.WriteLine($"Сервер работает: {status.IsOnline}");
    Console.WriteLine($"Игроков онлайн: {status.Players?.OnlineCount}");
    Console.WriteLine($"Пинг доступен: {status.Debug?.Ping}");
}
*/