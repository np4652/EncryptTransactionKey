using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EncryptTransactionKey.Model
{
    public class APIList
    {
        public IEnumerable<API> APIs { get; set; }
    }
    public class API
    {
        public string Provider { get; set; }
        public IEnumerable<APIConfig> APIConfig { get; set; }
    }

    public class APIConfig
    {
        public string Name { get; set; }
        public string Url { get; set; }
    }
}
