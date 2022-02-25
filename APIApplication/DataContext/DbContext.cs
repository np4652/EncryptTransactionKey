using APIApplication.Model;
using APIApplication.Services;
using Dapper;
using EncryptTransactionKey.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace EncryptTransactionKey.DataContext
{
    public class DbContext : IDbContext
    {
        private readonly IDapper _dapper;
        public DbContext(IDapper dapper)
        {
            _dapper = dapper;
        }
        public async Task<NetworkAddress> SaveNetworkAddress(NetworkAddress binanceAddress)
        {
            var sqlQuery = @"IF(Select count(1) from NetworkAddress where TID = @TID and ISNULL(NetworkId,'')=@NetworkId) = 0
                                insert into NetworkAddress(TID,[Address],PrivateKey,EntryOn,NetworkId) 
                                                    values(@TID,@Address,@PrivateKey,GetDate(),@NetworkId)
                             Select Id,TID,[Address],PrivateKey from NetworkAddress";
            var param = new DynamicParameters();
            param.Add("TID", binanceAddress.TID, DbType.String);
            param.Add("Address", binanceAddress.Address, DbType.String);
            param.Add("PrivateKey", binanceAddress.PrivateKey, DbType.String);
            param.Add("NetworkId", binanceAddress.NetworkId ?? "BEP20", DbType.String);
            binanceAddress = await _dapper.GetAsync<NetworkAddress>(sqlQuery, param, commandType: CommandType.Text);
            return binanceAddress;
        }

        public async Task<NetworkAddress> GetBinanceInfoByAddress(string Address)
        {
            var sqlQuery = @"Select Id,TID,[Address],PrivateKey from NetworkAddress(nolock) where [Address] = @Address";
            var param = new DynamicParameters();
            param.Add("Address", Address, DbType.String);
            var binanceAddress = await _dapper.GetAsync<NetworkAddress>(sqlQuery, param, commandType: CommandType.Text);
            return binanceAddress;
        }
        public async Task<NetworkAddress> IFAddressExists(string TID)
        {
            var sqlQuery = @"Select Id,TID,[Address],PrivateKey from NetworkAddress where TID = @TID";
            var param = new DynamicParameters();
            param.Add("TID", TID, DbType.String);
            var binanceAddress = await _dapper.GetAsync<NetworkAddress>(sqlQuery, param, commandType: CommandType.Text);
            return binanceAddress ?? new NetworkAddress();
        }

        public async Task<bool> IsIPValid(string IP)
        {
            var sqlParam = new DynamicParameters();
            sqlParam.Add("IP", IP, DbType.String);
            return await _dapper.GetAsync<bool>(@"select 1 from IPMaster(nolock) where [IP]=@IP"
                     , sqlParam,
                     commandType: CommandType.Text);
        }

        public async Task<string> GetSalt(string requestType)
        {
            string salt = string.Empty;
            string sqlQuery = "select top 1 [Salt] from SecretKey(nolock)";
            if (!string.IsNullOrEmpty(requestType) && requestType.Equals("Withdrawal", StringComparison.OrdinalIgnoreCase))
                sqlQuery = "select top 1 [WithdrawalSalt] from SecretKey(nolock)";
            try
            {
                salt = await _dapper.GetAsync<string>(sqlQuery, null, commandType: CommandType.Text);
            }
            catch (Exception ex)
            {

            }
            return salt ?? string.Empty;
        }

        public async Task<string> GetSecretKey(string requestType, IDictionary<string, string> keyCollection)
        {
            string key = string.Empty;
            try
            {
                if (!string.IsNullOrEmpty(requestType) && requestType.Equals("Withdrawal", StringComparison.OrdinalIgnoreCase))
                {
                    string k = keyCollection["Withdrawalkey"];
                    string salt = await _dapper.GetAsync<string>(@"select top 1 [WithdrawalSalt] from SecretKey(nolock)", null, commandType: CommandType.Text);
                    key = string.Concat(salt, k);
                }
                else
                {
                    string k = keyCollection["key"];
                    string salt = await _dapper.GetAsync<string>(@"select top 1 [Salt] from SecretKey(nolock)", null, commandType: CommandType.Text);
                    key = string.Concat(salt, k);
                }
            }
            catch (Exception ex)
            {

            }
            return key ?? string.Empty;
        }
        public async Task saveLog(EncryptRequest request, string requestedUrl, APIResponse APIRes, BaseResponse<APIResponse> response)
        {
            BaseResponse<APIResponse> newResponse = new BaseResponse<APIResponse>
            {
                Status = response.Status,
                StatusCode = response.StatusCode,
                Data = new APIResponse
                {
                    status = response.Data.status,
                    msg = response.Data.msg
                }
            };
            //newResponse = response;
            /* Save log */
            if (newResponse.StatusCode == 1 && !string.IsNullOrEmpty(newResponse.Data.msg))
            {
                Random _random = new Random();
                var arr = newResponse.Data.msg.ToList();
                int start = _random.Next(0, 30), end = _random.Next(31, 60);
                if (end < arr.Count)
                    arr.RemoveRange(start, end);
                else
                    arr.RemoveRange(1, 20);
                newResponse.Data.msg = string.Join("", arr);
            }
            var param = new DynamicParameters();
            param.Add("IncomingRequest", JsonConvert.SerializeObject(request), DbType.String);
            param.Add("SelfResponse", JsonConvert.SerializeObject(newResponse), DbType.String);
            param.Add("OutgoingRequest", requestedUrl, DbType.String);
            param.Add("Response", JsonConvert.SerializeObject(APIRes), DbType.String);
            param.Add("Remark", "Encrypted Data", DbType.String);
            try
            {
                int i = await _dapper.ExecuteAsync("insert into RequestResponseLog(IncomingRequest,SelfResponse,OutgoingRequest,Response,EntryOn,Remark) values (@IncomingRequest,@SelfResponse,@OutgoingRequest,@Response,getDate(),@Remark)", param, commandType: CommandType.Text);
            }
            catch (Exception ex)
            {

            }
        }

        public async Task saveLog(string request, string requestedUrl, string APIRes, string response)
        {
            var param = new DynamicParameters();
            param.Add("IncomingRequest", request, DbType.String);
            param.Add("SelfResponse", response, DbType.String);
            param.Add("OutgoingRequest", requestedUrl, DbType.String);
            param.Add("Response", JsonConvert.SerializeObject(APIRes), DbType.String);
            param.Add("Remark", "Encrypted Data", DbType.String);
            await _dapper.ExecuteAsync("insert into RequestResponseLog(IncomingRequest,SelfResponse,OutgoingRequest,Response,EntryOn,Remark) values (@IncomingRequest,@SelfResponse,@OutgoingRequest,@Response,getDate(),@Remark)", param, commandType: CommandType.Text);
        }
    }
}