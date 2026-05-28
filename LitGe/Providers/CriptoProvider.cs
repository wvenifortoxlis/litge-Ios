using System.Security.Cryptography;
using System.Text;

namespace LitGe.Providers
{
    public class CriptoProvider
    {
        //private string _deviceId = "ddrrccebdc71a7ff6f36ced9cf78a123";
        //private string _publicKey = "-----BEGIN PUBLIC KEY-----\r\n MEwwDQYJKoZIhvcNAQEBBQADOwAwOAIxAOJu76frj86vt3lUixPdI17xPu71DtzW\r\n 6VCCWj9R7ovwtM1fdjrVgCmmf88VobgC/QIDAQAB\r\n -----END PUBLIC KEY-----\r\n ";

        private byte[] CalculateChecksum(byte[] data)
        {
            return MD5Core.GetHash(data);
        }

        /*
        public Task<byte[]> GetPrivateKeyAsync(SessionToken token)
        {
            return Task<byte[]>.Factory.StartNew(() =>
            {
                //var serial = CalculateHash2(GetDeviceId(), token.Username);
                var serial = GetDeviceId();  //CalculateHash2(GetDeviceId());
                var os = "win10"; //Environment.OSVersion.ToString();
                var model = _deviceInfo.DeviceModel();
                var manufacturer = "PC";

                Debug.WriteLine("GetPrivateKeyAsync token:" + token.Token + ", serial:" + serial + ", os:" + os + ", model:" + model + ", manafacturer:" + manufacturer);

                var hash = CalculateHash(token.Token, serial, os, model, manufacturer).ToLower();
                var get = string.Format("{0}&method=key&session={1}&serial={2}&os={3}&model={4}&manufacturer={5}&checksum={6}&version=3", StaticSession.ServerBaseUrl, token.Token, serial, System.Net.WebUtility.UrlEncode(os), model, manufacturer, hash, "ARG6");
                Debug.WriteLine("GetPrivateKeyAsync url:" + get);
                var result = _webRequest.GetResultAsync(get).Result;

                return IsSuccess(result) ? _webRequest.GetBytes(result).Result : null;
            });
        }
        */

        public byte[] DecryptPrivateKey(byte[] key, string deviceId)
        {
            if (key == null || !key.Any())
                throw new ArgumentException("Invalid Argument: key");

            string hash = BitConverter
                .ToString(this.CalculateChecksum(Encoding.ASCII.GetBytes(deviceId)))
                .Replace("-", "")
                .ToLower();
            byte[] data = Encoding.ASCII.GetBytes(hash);
            for (int i = 1; i <= 16; i++)
            {
                data[i + 8] = (byte)(data[i] + data[i + 13] - data[24 - i]);
            }

            byte[] pwd = data.Skip(11).Take(16).ToArray();
            byte[] rsaKey = Convert.FromBase64String(Encoding.ASCII.GetString(key));

            MemoryStream ms = new MemoryStream();
            Rijndael aes = RijndaelManaged.Create();
            aes.Key = pwd;
            aes.IV = pwd;
            CryptoStream stream = new CryptoStream(
                ms,
                aes.CreateDecryptor(),
                CryptoStreamMode.Write
            );
            stream.Write(rsaKey, 0, rsaKey.Length);
            return ms.ToArray();
        }

        /// <summary>
        /// Decrypts epub key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="rsa">RSA private key used to decrypt AES key.</param>
        /// <returns>An instance of EpubKey</returns>
        public EpubKey DecryptEpubKey(byte[] key, byte[] rsa)
        {
            RSACryptoServiceProvider pk = opensslkey.DecodeRSAPrivateKey(
                opensslkey.DecodeOpenSSLPrivateKey(Encoding.ASCII.GetString(rsa))
            );
            string buff = Encoding.ASCII.GetString(pk.Decrypt(key, false));
            EpubKey epubKey = new EpubKey();
            epubKey.ValidFrom = DateTime.ParseExact(buff.Substring(0, 10), "yyyy-MM-dd", null);
            epubKey.ValidTo = DateTime.ParseExact(buff.Substring(10, 10), "yyyy-MM-dd", null);
            epubKey.Key = Encoding.ASCII.GetBytes(buff.Substring(20));
            return epubKey;
        }

        /// <summary>
        /// Decrypts epub chapter.
        /// </summary>
        /// <param name="data">Encypted epub chapter.</param>
        /// <param name="key">AES key.</param>
        /// <returns>Decrypted chapter.</returns>
        public byte[] DecryptChapter(byte[] data, EpubKey key)
        {
            MemoryStream ms = new MemoryStream();
            Rijndael aes = RijndaelManaged.Create();

            aes.Padding = PaddingMode.None;

            aes.Key = key.Key;
            aes.IV = key.Key;
            using (
                CryptoStream stream = new CryptoStream(
                    ms,
                    aes.CreateDecryptor(),
                    CryptoStreamMode.Write
                )
            )
            {
                stream.Write(data, 0, data.Length);
            }
            return ms.ToArray();
        }

        /// <summary>
        /// Encrypts the specified data.
        /// </summary>
        /// <param name="data">The buffer to be encrypted.</param>
        /// <param name="key">The key used to encrypt data.</param>
        public byte[] Encrypt(byte[] data, byte[] key)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (Rijndael aes = RijndaelManaged.Create())
                {
                    aes.Key = this.CalculateChecksum(key);
                    aes.IV = this.CalculateChecksum(key);
                    using (
                        CryptoStream stream = new CryptoStream(
                            ms,
                            aes.CreateEncryptor(aes.Key, aes.IV),
                            CryptoStreamMode.Write
                        )
                    )
                    {
                        stream.Write(data, 0, data.Length);
                    }
                    return ms.ToArray();
                }
            }
        }

        /// <summary>
        /// Decrypts the specified data.
        /// </summary>
        /// <param name="data">The buffer to be decrypted.</param>
        /// <param name="key">The key used to decrypt data.</param>
        public byte[] Decrypt(byte[] data, byte[] key)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (Rijndael aes = RijndaelManaged.Create())
                {
                    aes.Key = this.CalculateChecksum(key);
                    aes.IV = this.CalculateChecksum(key);
                    using (
                        CryptoStream stream = new CryptoStream(
                            ms,
                            aes.CreateDecryptor(),
                            CryptoStreamMode.Write
                        )
                    )
                    {
                        stream.Write(data, 0, data.Length);
                    }
                    return ms.ToArray();
                }
            }
        }
    }
}
