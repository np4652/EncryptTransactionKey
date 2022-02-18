using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace EncryptTransactionKey.Helpers
{
    public class Crypto
    {
        private static Lazy<Crypto> Instance = new Lazy<Crypto>(() => new Crypto());
        public static Crypto O => Instance.Value;
        private Crypto() { }

        public string EncryptUsingPublicKey(string plainText, string certificatePath)
        {
            string result = string.Empty;
            try
            {

                plainText = string.Concat((char)160, plainText);
                byte[] data = Encoding.UTF8.GetBytes(plainText);

                IBufferedCipher cipher = CipherUtilities.GetCipher("RSA/ECB/PKCS1Padding");
                using (var fileStream = new FileStream(certificatePath, FileMode.Open))
                {
                    var rsa = new RSACryptoServiceProvider();
                    X509CertificateParser certParser = new X509CertificateParser();
                    X509Certificate certificate = certParser.ReadCertificate(fileStream);
                    RsaKeyParameters publicKey = (RsaKeyParameters)certificate.GetPublicKey();
                    cipher.Init(true, publicKey);
                    result = Convert.ToBase64String(cipher.DoFinal(data));
                }
            }
            catch (Exception ex)
            {
                result = "something went wrong";
            }
            return result;
        }
        public string DecryptUsingPrivateKey(string plainText, string certificatePath)
        {
            string result = string.Empty;
            try
            {
                byte[] data = Convert.FromBase64String(plainText);
                using (var fileStream = System.IO.File.OpenText(certificatePath))
                {
                    var pemReader = new PemReader(fileStream);
                    var KeyParameter = (AsymmetricKeyParameter)pemReader.ReadObject();
                    AsymmetricKeyParameter privateKey = KeyParameter;
                    RsaEngine e = new RsaEngine();
                    e.Init(false, privateKey);
                    byte[] decipheredBytes = e.ProcessBlock(data, 0, data.Length);
                    result = Encoding.UTF8.GetString(decipheredBytes, 0, decipheredBytes.Length);
                    var splitData = result.Split((char)160);
                    //var splitData = result.Split(" ");
                    if (splitData.Length >= 1)
                    {
                       var results = splitData[1];
                    }
                }
            }
            catch (Exception ex)
            {
                result = "something went wrong";
            }
            return result;
        }
        public string Encrypt(string EncryptionKey, string clearText)
        {
            byte[] clearBytes = Encoding.Unicode.GetBytes(clearText);
            using (Aes encryptor = Aes.Create())
            {
                Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(EncryptionKey, new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 });
                encryptor.Key = pdb.GetBytes(32);
                encryptor.IV = pdb.GetBytes(16);
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(clearBytes, 0, clearBytes.Length);
                        cs.Close();
                    }
                    clearText = Convert.ToBase64String(ms.ToArray());
                }
            }
            return clearText;
        }
        public string Decrypt(string EncryptionKey, string cipherText)
        {
            try
            {
                byte[] cipherBytes = Convert.FromBase64String(cipherText);
                using (Aes encryptor = Aes.Create())
                {
                    Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(EncryptionKey, new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 });
                    encryptor.Key = pdb.GetBytes(32);
                    encryptor.IV = pdb.GetBytes(16);
                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateDecryptor(), CryptoStreamMode.Write))
                        {
                            cs.Write(cipherBytes, 0, cipherBytes.Length);
                            cs.Close();
                        }
                        cipherText = Encoding.Unicode.GetString(ms.ToArray());
                    }
                }
            }
            catch (Exception ex)
            {
            }
            return cipherText;
        }
    }
}
