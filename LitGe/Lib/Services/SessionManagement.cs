using System.Security.AccessControl;
using Newtonsoft.Json;

namespace LitGe.Lib.Services
{
    public class SessionManagement
    {
        public class SessionModel
        {
            /*
                 0   1      2                  3                 4                     5   6
                "92;leqso;ajakhua@gmail.com;ალექსანდრე ჯახუა;r3r1su4jp8t43i658fosr9j915;1;false"
            */
            public string RawResponse { get; set; }
            public string Session { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
            public string Email { get; set; }
            public string DisplayName { get; set; }
            public bool IsLoggedIn { get; set; }
            public DateTime LoginTime { get; set; }
            public DateTime ExpiryTime { get; set; }

            public SessionModel(string rawResponse, string password, string? usernameFallback = null)
            {
                RawResponse = rawResponse;
                Password = password;
                Username = usernameFallback ?? "";
                
                // Safety check for empty or malformed responses
                if (string.IsNullOrWhiteSpace(rawResponse))
                {
                    IsError = true;
                    Message = "Server returned an empty session response.";
                    Console.WriteLine("DEBUG_SESSION: Empty rawResponse received in SessionModel constructor.");
                    return;
                }

                if (rawResponse.StartsWith("BYPASS|"))
                {
                    string[] parts = rawResponse.Split('|');
                    Username = parts[1];
                    Email = "bypass@lit.ge";
                    DisplayName = "Bypass User";
                    Session = parts.Length > 2 && !string.IsNullOrWhiteSpace(parts[2]) ? parts[2] : "r3r1su4jp8t43i658fosr9j915";
                    Password = "bypass-token"; // Set dummy password for bypass
                    IsLoggedIn = true;
                    LoginTime = DateTime.UtcNow;
                    ExpiryTime = LoginTime.AddDays(30); // Long expiry for bypass
                    return;
                }

                string[] splited = RawResponse.Split(';');
                if (splited.Length < 5)
                {
                    IsError = true;
                    Message = $"Invalid session format: {rawResponse}";
                    return;
                }

                if (string.IsNullOrEmpty(Username)) Username = splited[1];
                Email = splited[2];
                DisplayName = splited[3];
                Session = splited[4];
                IsLoggedIn = true;
                LoginTime = DateTime.UtcNow;
                ExpiryTime = LoginTime.AddMinutes(15);
            }

            public bool IsError { get; set; }
            public string Message { get; set; } = "";
        }

        public void InitSession(string rawResponse, string password, string? username = null)
        {
            SessionModel sessionModel = new(rawResponse, password, username);

            JsonSerializer jsonSerializer = new() { Formatting = Formatting.Indented };
            using StreamWriter sw = new(_root);
            using JsonWriter jsonWriter = new JsonTextWriter(sw);
            jsonSerializer.Serialize(jsonWriter, sessionModel);
        }

        public async Task<SessionModel?> ReadSessionAsync()
        {
            if (!File.Exists(_root))
            {
                return null;
            }

            string json = await File.ReadAllTextAsync(_root);
            SessionModel model =
                JsonConvert.DeserializeObject<SessionModel>(json)
                ?? throw new Exception($"Ver agdga sessis monacemi: {json}");
            return model;
        }

        public void DeleteSession()
        {
            if (File.Exists(_root))
            {
                File.Delete(_root);
            }
        }

        public void Update(SessionModel model)
        {
            JsonSerializer jsonSerializer = new() { Formatting = Formatting.Indented };
            using StreamWriter sw = new(_root);
            using JsonWriter jw = new JsonTextWriter(sw);
            jsonSerializer.Serialize(jw, model);
        }

        private readonly string _root = Path.Combine(
            FileSystem.Current.AppDataDirectory,
            "__session.json"
        );
    }
}
