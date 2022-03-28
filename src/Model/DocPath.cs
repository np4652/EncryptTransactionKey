using System.IO;

namespace EncryptTransactionKey.Model
{
    public class DocPath
    {
        public static string PublicKey = Path.Combine(Directory.GetCurrentDirectory(), "Certificate/public.cer");
        public static string PrivateKey = Path.Combine(Directory.GetCurrentDirectory(), "Certificate/private.cer");
    }
}
