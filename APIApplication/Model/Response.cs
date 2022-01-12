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
}
