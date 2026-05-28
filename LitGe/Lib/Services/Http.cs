using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace LitGe.Lib.Services
{
    public class Http
    {
        public async Task<Stream> Downloadbook(
            string session,
            string serial,
            string? item,
            string? productId = null,
            int? productType = null
        )
        {
            string cleanSerial = serial.Replace("-", "");
            if (cleanSerial.Length > 32) cleanSerial = cleanSerial.Substring(0, 32);

            string checksum = Checksum([session, cleanSerial, item]);
            
            string encodedSession = Uri.EscapeDataString(session);
            string encodedSerial = Uri.EscapeDataString(cleanSerial);
            string encodedItem = Uri.EscapeDataString(item ?? "");
            
            string url =
                $"{_downloadUrl}&session={encodedSession}&serial={encodedSerial}&item={encodedItem}&checksum={checksum}&version=3";

            using HttpClient client = new();
            HttpResponseMessage response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStreamAsync();
        }

        public async Task<string[]> EveryChapterInBook(
            string session,
            string id,
            string productType
        )
        {
            string checksum = Checksum([session, id, productType]);
            
            string encodedSession = Uri.EscapeDataString(session);
            string encodedId = Uri.EscapeDataString(id);
            string encodedType = Uri.EscapeDataString(productType);
            
            string url =
                $"{_serviceUrl}&method=chapters&session={encodedSession}&id={encodedId}&locale=1&checksum={checksum}&type={encodedType}&version=3";
            using HttpClient http = new HttpClient();
            HttpResponseMessage response = await http.GetAsync(url);
            response.EnsureSuccessStatusCode();
            string content = await response.Content.ReadAsStringAsync();

            return content.Split(';').Where((t, i) => (i + 3) % 4 == 0).ToArray();
        }

        public async Task<string?[]> AudioChaptersForDownload(string item, string session)
        {
            string checksum = Checksum([session, item]);
            
            string encodedSession = Uri.EscapeDataString(session);
            string encodedItem = Uri.EscapeDataString(item);
            
            string url =
                $"{_serviceUrl}&method=mp3items&session={encodedSession}&id={encodedItem}&checksum={checksum}";
            using HttpClient http = new HttpClient();
            HttpResponseMessage response = await http.GetAsync(url);
            response.EnsureSuccessStatusCode();
            string content = await response.Content.ReadAsStringAsync();
            return content.Split("[split]")[1].Split(';', StringSplitOptions.RemoveEmptyEntries);
        }

        public async Task<string> Session(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                Console.WriteLine("DEBUG_LOGIN_ERROR: Username or password is null/empty.");
                return string.Empty;
            }

            string checksum = Checksum([username, password]);
            
            // Try POST if GET is being blocked/returning empty
            var contentForm = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("application", "iotareader"),
                new KeyValuePair<string, string>("method", "login"),
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("password", password),
                new KeyValuePair<string, string>("checksum", checksum)
            });

            Console.WriteLine($"DEBUG_LOGIN_ATTEMPT: POST to literacy.ge/service.php");
            
            using HttpClient httpClient = new();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "LitGe-iOS/1.0");
            
            HttpResponseMessage responseMessage = await httpClient.PostAsync("https://literacy.ge/service.php", contentForm);
            string content = await responseMessage.Content.ReadAsStringAsync();
            
            Console.WriteLine($"DEBUG_LOGIN_STATUS: {(int)responseMessage.StatusCode}");
            Console.WriteLine($"DEBUG_LOGIN_RESPONSE: {content}");
            
            if (string.IsNullOrWhiteSpace(content))
            {
                // Fallback to GET if POST failed to return content, but use the old format
                string url = $"{_serviceUrl}&method=login&username={Uri.EscapeDataString(username)}&password={Uri.EscapeDataString(password)}&checksum={checksum}";
                Console.WriteLine($"DEBUG_LOGIN_FALLBACK_GET: {url}");
                responseMessage = await httpClient.GetAsync(url);
                content = await responseMessage.Content.ReadAsStringAsync();
                Console.WriteLine($"DEBUG_LOGIN_FALLBACK_STATUS: {(int)responseMessage.StatusCode}");
                Console.WriteLine($"DEBUG_LOGIN_FALLBACK_RESPONSE: {content}");
            }

            responseMessage.EnsureSuccessStatusCode();
            return content;
        }

        public async Task<string> Key(
            string session,
            string deviceId,
            string os,
            string model,
            string manufacturer
        )
        {
            if (string.IsNullOrEmpty(session) || string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(os) || string.IsNullOrEmpty(model) || string.IsNullOrEmpty(manufacturer))
            {
                Console.WriteLine("DEBUG_KEY_ERROR: One or more parameters are null/empty.");
                return string.Empty;
            }

            // Ensure serial is 32 characters (consistent with iOS UDID / GUID)
            string cleanSerial = deviceId.Replace("-", "").ToLower();
            if (cleanSerial.Length != 32)
            {
                byte[] serialData = MD5.HashData(Encoding.UTF8.GetBytes(cleanSerial));
                cleanSerial = BitConverter.ToString(serialData).Replace("-", "").ToLower();
            }

            string checksum = Checksum([session, cleanSerial, os, $"{model}-litge", manufacturer]);
            
            string eSession = Uri.EscapeDataString(session);
            string eSerial = Uri.EscapeDataString(cleanSerial);
            string eOs = Uri.EscapeDataString(os);
            string eModel = Uri.EscapeDataString($"{model}-litge");
            string eManufacturer = Uri.EscapeDataString(manufacturer);
            
            // Removing version=3 as it was not in the backup code for this specific method
            string url =
                $"{_serviceUrl}&method=key&session={eSession}&serial={eSerial}&os={eOs}&model={eModel}&manufacturer={eManufacturer}&checksum={checksum}";
            
            Console.WriteLine($"DEBUG_KEY_URL: {url}");
            
            using HttpClient client = new();
            client.DefaultRequestHeaders.Add("User-Agent", "LitGe-iOS/1.0");
            HttpResponseMessage responseMessage = await client.GetAsync(url);
            
            if (!responseMessage.IsSuccessStatusCode)
            {
                string errorBody = await responseMessage.Content.ReadAsStringAsync();
                Console.WriteLine($"DEBUG_KEY_ERROR_BODY: {errorBody}");
            }
            
            responseMessage.EnsureSuccessStatusCode();
            string keyResponse = await responseMessage.Content.ReadAsStringAsync();
            Console.WriteLine($"DEBUG_KEY_RESPONSE: {keyResponse}");
            return keyResponse;
        }

        public async Task<string> Collection(string session)
        {
            if (string.IsNullOrEmpty(session))
            {
                Console.WriteLine("DEBUG_COLLECTION: Session is null or empty. Cannot fetch collection.");
                return string.Empty;
            }

            string checksum = Checksum([session]);
            string encodedSession = Uri.EscapeDataString(session);
            
            string url =
                $"{_serviceUrl}&version=3&method=collection&session={encodedSession}&checksum={checksum}";
            using HttpClient client = new();
            HttpResponseMessage responseMessage = await client.GetAsync(url);
            responseMessage.EnsureSuccessStatusCode();
            return await responseMessage.Content.ReadAsStringAsync();
        }

        private const string _serviceUrl = "https://literacy.ge/service.php?application=iotareader";

        private const string _downloadUrl =
            "https://literacy.ge/download.php?application=iotareader";

        private const string _salt = "Dsadas#$#@%32Fsds$%$#%$#6$%$#^$#";

        private string Checksum(string[] args)
        {
            using MD5 md5 = MD5.Create();
            string raw = $"{string.Join("", args)}{_salt}";
            byte[] inputBytes = Encoding.UTF8.GetBytes(raw);
            byte[] hashBytes = md5.ComputeHash(inputBytes);
            string hash = Convert.ToHexString(hashBytes).ToLower();
            
            Console.WriteLine($"DEBUG_CHECKSUM: RAW={raw} HASH={hash}");
            
            return hash;
        }
    }
}
