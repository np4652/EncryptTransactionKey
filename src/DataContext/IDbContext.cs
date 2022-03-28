using APIApplication.Model;
using EncryptTransactionKey.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EncryptTransactionKey.DataContext
{
    public interface IDbContext
    {
        Task<NetworkAddress> SaveNetworkAddress(NetworkAddress binanceAddress);
        Task<NetworkAddress> GetBinanceInfoByAddress(string Address, string UserId);
        Task<NetworkAddress> IFAddressExists(string TID,string NetworkId);
        Task<bool> IsIPValid(string IP);
        Task<string> GetSalt(string requestType);
        Task<string> GetSecretKey(string requestType, IDictionary<string, string> keyCollection, string userId, string toAddress);
        Task saveLog(EncryptRequest request, string requestedUrl, APIResponse APIRes, BaseResponse<APIResponse> response);
        Task saveLog(string request, string requestedUrl, string APIRes, string response);
    }
}
