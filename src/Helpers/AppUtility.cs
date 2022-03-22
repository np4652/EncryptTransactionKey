using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EncryptTransactionKey.Helpers
{
    public class AppUtility
    {
        private static Lazy<AppUtility> Instance = new Lazy<AppUtility>(() => new AppUtility());
        public static AppUtility O => Instance.Value;
        private AppUtility() { }
        public string DictionaryToString(Dictionary<string, string> dictionary)
        {
            string dictionaryString = "{";
            foreach (KeyValuePair<string, string> keyValues in dictionary)
            {
                dictionaryString += keyValues.Key + " : " + keyValues.Value + ", ";
            }
            return dictionaryString.TrimEnd(',', ' ') + "}";
        }
    }
}
