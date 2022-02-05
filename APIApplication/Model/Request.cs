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
        public string Option1 { get; set; }
        public string Option2 { get; set; }
        public string Option3 { get; set; }
        public string Option4 { get; set; }
        public string Option5 { get; set; }
    }
}
