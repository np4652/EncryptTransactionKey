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
        public string TID { get; set; }
        public string IP { get; set; }
    }
}
