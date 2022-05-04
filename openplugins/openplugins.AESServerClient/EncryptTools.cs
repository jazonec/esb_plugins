using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace openplugins.AESServerClient
{
    internal class EncryptTools
    {
        private readonly RSA _publicKey = null;
        private readonly RSA _privateKey = null;
        private readonly RSAEncryptionPadding _padding;

        public EncryptTools(JObject settings)
        {
            _padding = RSAEncryptionPadding.Pkcs1;

            if (settings.ContainsKey("certificate"))
            {
                X509Certificate2 _RSACert = new X509Certificate2((string)settings["certificate"]["file"]);
                _publicKey = _RSACert.GetRSAPublicKey();
                _privateKey = _RSACert.GetRSAPrivateKey();
            }
            else if (settings.ContainsKey("public"))
            {
                var publicSettings = (JObject)settings["public"];
                if (publicSettings.ContainsKey("xml"))
                {
                    _publicKey.FromXmlString((string)publicSettings["xml"]);
                }
                else if (publicSettings.ContainsKey("modulus"))
                {
                    RSAParameters param = new RSAParameters
                    {
                        Modulus = Convert.FromBase64String((string)publicSettings["modulus"]),
                        Exponent = new byte[] { 1, 0, 1 }
                    };
                    _publicKey = RSA.Create(param);
                }
            }
        }

        public byte[] Encrypt_RSA(byte[] plainText)
        {
            return _publicKey.Encrypt(plainText, _padding);
        }

        public byte[] Decrypt_RSA(byte[] ciperText)
        {
            return _privateKey.Decrypt(ciperText, _padding);
        }

        public static string DecryptStringFromBytes_Aes(byte[] cipherText, byte[] Key)
        {
            byte[] IV = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            // Check arguments.
            if (cipherText == null || cipherText.Length <= 0)
                throw new ArgumentNullException("cipherText");
            if (Key == null || Key.Length <= 0)
                throw new ArgumentNullException("Key");

            // Declare the string used to hold
            // the decrypted text.
            string plaintext = null;

            // Create an Aes object
            // with the specified key and IV.
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = Key;
                aesAlg.IV = IV;

                // Create a decryptor to perform the stream transform.
                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                // Create the streams used for decryption.
                using (MemoryStream msDecrypt = new MemoryStream(cipherText))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                        {

                            // Read the decrypted bytes from the decrypting stream
                            // and place them in a string.
                            plaintext = srDecrypt.ReadToEnd();
                        }
                    }
                }
            }

            return plaintext;
        }

        public static byte[] EncryptStringToBytes_Aes(string plainText, byte[] key)
        {
            byte[] IV = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            byte[] encrypted;

            // Create an Aes object
            // with the specified key and IV.
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = key;
                aesAlg.IV = IV;

                // Create an encryptor to perform the stream transform.
                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                // Create the streams used for encryption.
                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            //Write all data to the stream.
                            swEncrypt.Write(plainText);
                        }
                        encrypted = msEncrypt.ToArray();
                    }
                }
            }

            // Return the encrypted bytes from the memory stream.
            return encrypted;
        }

        private static readonly Random random = new Random();

        public static byte[] RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return Encoding.UTF8.GetBytes(new string(Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray()));
        }
    }
}