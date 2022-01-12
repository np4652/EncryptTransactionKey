using System.ComponentModel.DataAnnotations;

namespace EncryptTransactionKey.Model
{
    public class Request
    {
        [Required]
        public string PlainText { get; set; }
    }

    public class EncryptRequest
    {
        [Required]
        public int TID { get; set; }
    }
}
