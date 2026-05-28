using Newtonsoft.Json;
using RandomString4Net;
using System.ComponentModel.DataAnnotations;
using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Text;

namespace LitGe.Lib.Services
{
    public class KeyManagement
    {
        public record DeviceInformation
        {
            public string Model { get; set; } = "";
            public string Manufacturer { get; set; } = "";
            public string Name { get; set; } = "";
            public string DeviceId { get; set; } = "";
            public string OSVersion { get; set; } = "";
            public string Idiom { get; set; } = "";
            public string Platform { get; set; } = "";
            public bool IsVirtual { get; set; }
        }
        public record KeyNode
        {
            [Required(ErrorMessage = "Username aklia")]
            public string Username { get; set; } = "";
            [Required(ErrorMessage = "Paroli aklia")]
            public string Password { get; set; } = "";
            [Required(ErrorMessage = "DeviceId aklia")]
            public string DeviceId { get; set; } = "";
            [Required(ErrorMessage = "OS aklia")]
            public string OS { get; set; } = "";
            [Required(ErrorMessage = "Key aklia")]
            public string Key { get; set; } = "";
        }

        /// <summary>
        ///     axali nodes damateba dzvel node[]ze
        /// </summary>
        /// <param name="node">shemdgari keynode</param>
        public async Task AddKeyAsync(KeyNode node)
        {
            ValidateKeyNode(node);

            // Defensive check: Don't save dummy keys or invalid Base64
            if (node.Key == "dummy_emulator_key" || !IsValidBase64(node.Key))
            {
                throw new Exception("Invalid key format. Cannot save key.");
            }

            string? existingNodes = await SecureStorage.Default.GetAsync(_root);

            if (existingNodes == null) // jer araferia chawerili
            {
                List<KeyNode> nodes = [node]; // inaxeba rogorc masivi
                string nodesJson = JsonConvert.SerializeObject(nodes);
                await SecureStorage.Default.SetAsync(_root, nodesJson);
            }
            else //shemowmdes tu arsebobs aseti node, tuara chaiweros
            {
                List<KeyNode> nodes = JsonConvert.DeserializeObject<List<KeyNode>>(existingNodes) ?? throw new Exception($"Ver agdga monacemi: {existingNodes}");
                if (nodes.Any(n => n.Username == node.Username && n.Password == node.Password))
                    return;
                nodes.Add(node);

                string nodesJson = JsonConvert.SerializeObject(nodes);
                await SecureStorage.Default.SetAsync(_root, nodesJson);
            }
        }

        public async Task RemoveKeyAsync(string username, string password)
        {
            string? existingNodes = await SecureStorage.Default.GetAsync(_root);
            if (existingNodes == null) return;

            List<KeyNode> nodes = JsonConvert.DeserializeObject<List<KeyNode>>(existingNodes) ?? new List<KeyNode>();
            nodes.RemoveAll(n => n.Username == username && n.Password == password);

            string nodesJson = JsonConvert.SerializeObject(nodes);
            await SecureStorage.Default.SetAsync(_root, nodesJson);
        }

        /// <summary>
        ///     sistemashi shenaxuli yvela gasagebi
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<List<KeyNode>> KeysAsync()
        {
            string? existingNodes = await SecureStorage.Default.GetAsync(_root);
            if (existingNodes == null)
                return [];
            List<KeyNode> nodes = JsonConvert.DeserializeObject<List<KeyNode>>(existingNodes) ?? throw new Exception($"Ver agdga monacemebi: {existingNodes}"); ;
            return nodes;
        }

        public void RemoveAll()
        {
            SecureStorage.Default.RemoveAll();
        }

        public DeviceInformation GetDeviceInformation()
        {
            string rawId = 
#if ANDROID
                (Android.Provider.Settings.Secure.GetString(Android.App.Application.Context.ContentResolver, Android.Provider.Settings.Secure.AndroidId) ?? MimicDeviceId()).Replace("-", "").ToLower();
#elif IOS
                UIKit.UIDevice.CurrentDevice.IdentifierForVendor.AsString().Replace("-", "").ToLower();
#else
                MimicDeviceId().Replace("-", "").ToLower();
#endif
            // Hash to 32 chars for consistency with Registration API
            byte[] idData = MD5.HashData(Encoding.UTF8.GetBytes(rawId));
            string deviceId = BitConverter.ToString(idData).Replace("-", "").ToLower();

            return new DeviceInformation
            {
                Model = DeviceInfo.Current.Model,
                Manufacturer = DeviceInfo.Current.Manufacturer,
                Name = DeviceInfo.Current.Name,
                DeviceId = deviceId,
                OSVersion = DeviceInfo.Current.VersionString,
                Idiom = DeviceInfo.Current.Idiom.ToString(),
                Platform = DeviceInfo.Current.Platform.ToString(),
                IsVirtual = DeviceInfo.Current.DeviceType switch { DeviceType.Virtual => true, _ => false },
            };
        }

        /// <summary>
        ///     mexsierebashi yvela node mibmuli iqneba amaze
        ///     { _root:[node1,node2 ... ] }
        /// </summary>
        private const string _root = "EpubKeys";

        private void ValidateKeyNode(KeyNode node)
        {
            ValidationContext validation = new(node);
            List<ValidationResult> results = [];

            bool isValid = Validator.TryValidateObject(node, validation, results, true);
            if (!isValid)
            {
                string error = string.Join(",", results.Select(results => results.ErrorMessage));
                throw new Exception($"Ivalid KeyNode: {error}");
            }
        }

        public static string MimicDeviceId()
        {
            string id = Preferences.Default.Get("MimicDeviceId", string.Empty);
            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString();
                Preferences.Default.Set("MimicDeviceId", id);
            }
            return id;
        }

        public static bool IsValidBase64(string base64)
        {
            if (string.IsNullOrWhiteSpace(base64)) return false;
            try
            {
                // Basic cleanup
                string cleaned = base64.Trim();
                // Check regex for standard Base64 characters
                if (!Regex.IsMatch(cleaned, @"^[a-zA-Z0-9\+/]*={0,2}$")) return false;
                
                // Final check by attempting to convert
                Convert.FromBase64String(cleaned);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}