using System.ComponentModel.DataAnnotations;

namespace EncryptTransactionKey.Model
{
    public class Request
    {
        [Required]
        public string PlainText { get; set; }
        public string Key { get; set; }
    }

    public class EncryptRequest : BaseRequest
    {
        //[Required]
        //public string TID { get; set; }
        //public string IP { get; set; }
        public string Option1 { get; set; }
        public string Option2 { get; set; }
        public string Option3 { get; set; }
        public string Option4 { get; set; }
        public string Option5 { get; set; }
    }


    public class BaseRequest
    {
        [Required]
        public string TID { get; set; }
        public string IP { get; set; }
    }

    public class PrivateKeyRequest
    {
        [Required]
        public string UserId { get; set; }
        public string TID { get; set; }
        public string Address { get; set; }
        public string ToAddress { get; set; }
        public string RequestType { get; set; }
        public string Amount { get; set; }
        public string RequestFrom { get; set; }
    }

    public class BallanceRequest : BaseRequest
    {
        public string WalletAddress { get; set; }
        public string ContractAddress { get; set; }
    }

    public class GenrateAddressReq : BaseRequest
    {
        public string NetworkId { get; set; }
    }
}
