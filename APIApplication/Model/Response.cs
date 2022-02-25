using System;
using System.Text.Json.Serialization;

namespace APIApplication.Model
{
    public class BaseResponse<T>
    {
        public int StatusCode { get; set; }
        public string Status { get; set; }
        public T Data { get; set; }
        public Exception Exception { get; set; }
        [JsonIgnore]
        public bool IsModelValid { get; set; }
    }

    public class APIResponse
    {
        public int status { get; set; }
        public string msg { get; set; }
    }

    public class AddressWithPrivateKey
    {
        public string address { get; set; }
        public string privateKey { get; set; }
    }

    public class Result<T>
    {
        public T message { get; set; }
    }

    public class BinanceAPIResponse<T>
    {
        public bool success { get; set; }
        public Result<T> result { get; set; }
    }
    public class NetworkAddress
    {
        public int Id { get; set; }
        public string TID { get; set; }
        public string Address { get; set; }
        public string PrivateKey { get; set; }
        public string EntryOn { get; set; }
        public string NetworkId { get; set; }
    }
}
