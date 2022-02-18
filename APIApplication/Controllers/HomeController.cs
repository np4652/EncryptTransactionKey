using APIApplication.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using APIApplication.Model;
using System.Net.Http;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using EncryptTransactionKey.Model;
using EncryptTransactionKey.Helpers;
using System.Linq;

namespace APIApplication.Controllers
{
    [ApiController]
    [Route("api/")]
    public class HomeController : ControllerBase
    {
        #region variables
        private readonly IDapper _dapper;
        private const string APIUrl = "https://teamrijent.in/admin/CoinService.aspx?TID={0}&option1={1}&option2={2}&option3={3}&option4={4}&option5={5}";
        private string generateAddress = "http://new.teamrijent.in:3004/generate_address/{0}";
        private string getBalance = "http://new.teamrijent.in:3004/get_token_balance/{0}?walletAddress={1}&contractAddress={2}";
        private string RemoteIP = string.Empty;
        private const string key = "fa0cd267144f93e9481bf0001564baf51b21";
        #endregion
        public HomeController(IDapper dapper)
        {
            _dapper = dapper;
        }

        [HttpGet(nameof(index))]
        public IActionResult index()
        {
            return Ok("Welcome");
        }

        #region APIs

        [HttpPost(nameof(Encrypt))]
        public async Task<BaseResponse<APIResponse>> Encrypt(EncryptRequest request)
        {
            string requestedUrl = string.Format(APIUrl, request.TID.ToString(), request.Option1, request.Option2, request.Option3, request.Option4, request.Option5);
            var response = new BaseResponse<APIResponse>
            {
                StatusCode = 503,
                Status = "Bad Request",
                Data = new APIResponse
                {
                    status = -1,
                    msg = "Some parameters are not supplied"
                }
            };
            var APIRes = new APIResponse();
            string plainText = string.Empty;
            try
            {
                RemoteIP = Request.HttpContext.Connection.RemoteIpAddress.ToString();
                request.IP = RemoteIP;
                bool isIPValid = await IsIPValid(request.IP);
                if (!isIPValid)
                {
                    response.Data.msg = "Invalid IP";
                    goto Finish;
                }
                /* Check if TID is valid */
                #region Validate TID
                APIRes = await callAPI(requestedUrl);
                response.Data = APIRes;
                if (APIRes.status == -1)//.Equals("false",StringComparison.OrdinalIgnoreCase)
                    goto Finish;
                #endregion
                /* End TID Validation */
                string salt = await GetSalt();
                plainText = string.Concat(salt, key);
                if (string.IsNullOrEmpty(plainText))
                {
                    response.Data.msg = "Key not found";
                    goto Finish;
                }
                response = new BaseResponse<APIResponse>
                {
                    StatusCode = 1,
                    Status = "Success",
                    Data = new APIResponse
                    {
                        status = -1,
                        msg = Crypto.O.EncryptUsingPublicKey(plainText, DocPath.PublicKey)
                    }
                };
            }
            catch (Exception ex)
            {
                response.Status = "Failed";
                response.Data.msg = ex.Message;
            }
        Finish:
            saveLog(request, requestedUrl, APIRes, response);
            return response;
        }

        [HttpPost(nameof(Decryp))]
        public BaseResponse<string> Decryp([FromBody] Request request)
        {
            var response = new BaseResponse<string>
            {
                StatusCode = 503,
                Status = "Bad Request",
                Data = "Some parameters are not supplied"
            };
            try
            {
                return new BaseResponse<string>
                {
                    StatusCode = 1,
                    Status = "Success",
                    Data = Crypto.O.DecryptUsingPrivateKey(request.PlainText, DocPath.PrivateKey),
                };
            }
            catch (Exception ex)
            {
                response.Status = "Failed";
                response.Data = ex.Message;
            }
            return response;
        }

        [HttpPost(nameof(GenrateBinanceAddress))]
        public async Task<BaseResponse<BinanceAPIResponse<AddressWithPrivateKey>>> GenrateBinanceAddress(BaseRequest request)
        {
            BaseResponse<BinanceAPIResponse<AddressWithPrivateKey>> genrateResponse(BinanceAddress binanceAddress)
            {
                return new BaseResponse<BinanceAPIResponse<AddressWithPrivateKey>>
                {
                    StatusCode = 1,
                    Status = "Success",
                    Data = new BinanceAPIResponse<AddressWithPrivateKey>
                    {
                        success = true,
                        result = new Result<AddressWithPrivateKey>
                        {
                            message = new AddressWithPrivateKey
                            {
                                address = binanceAddress.Address,
                                privateKey = binanceAddress.PrivateKey
                            }
                        }
                    }
                };
            }
            string requestedUrl = string.Format(generateAddress, request.TID.ToString());
            var deserializeResponse = new BinanceAPIResponse<AddressWithPrivateKey>();
            var response = new BaseResponse<BinanceAPIResponse<AddressWithPrivateKey>>
            {
                StatusCode = 503,
                Status = "Bad Request",
                Data = deserializeResponse
            };
            try
            {
                RemoteIP = Request.HttpContext.Connection.RemoteIpAddress.ToString();
                request.IP = RemoteIP;
                bool isIPValid = await IsIPValid(request.IP);
                if (!isIPValid)
                {
                    response.Status = "Invalid IP";
                    goto Finish;
                }
                var binanceAddress = await IFAddressExists(request.TID);
                if (binanceAddress != null && !string.IsNullOrEmpty(binanceAddress.Address))
                {
                    response = genrateResponse(binanceAddress);
                    goto Finish;
                }

                using (var httpClient = new HttpClient())
                {
                    using (var res = await httpClient.GetAsync(requestedUrl))
                    {
                        string result = await res.Content.ReadAsStringAsync();
                        deserializeResponse = JsonConvert.DeserializeObject<BinanceAPIResponse<AddressWithPrivateKey>>(result);
                    }
                }
                if (deserializeResponse != null && deserializeResponse.success)
                {
                    deserializeResponse.result.message.privateKey = Crypto.O.Encrypt(request.TID, deserializeResponse.result.message.privateKey);
                    binanceAddress = await SaveBinanceAddress(new BinanceAddress
                    {
                        TID = request.TID,
                        Address = deserializeResponse.result.message.address,
                        PrivateKey = deserializeResponse.result.message.privateKey
                    });
                    if (!string.IsNullOrEmpty(binanceAddress.Address))
                        response = genrateResponse(binanceAddress);
                }
            }
            catch (Exception ex)
            {
                response.Status = "Failed";
            }
        Finish:
            if (response.Data != null && response.Data.result != null && response.Data.result.message != null)
                response.Data.result.message.privateKey = string.Empty;
            saveLog(JsonConvert.SerializeObject(request), requestedUrl, JsonConvert.SerializeObject(deserializeResponse), JsonConvert.SerializeObject(response));
            return response;
        }

        [HttpPost(nameof(GetPrivateKey))]
        public async Task<BaseResponse<BinanceAddress>> GetPrivateKey(PrivateKeyRequest request)
        {
            var response = new BaseResponse<BinanceAddress>
            {
                StatusCode = 503,
                Status = "Bad Request"
            };
            try
            {
                RemoteIP = Request.HttpContext.Connection.RemoteIpAddress.ToString();
                bool isIPValid = await IsIPValid(RemoteIP);
                if (!isIPValid)
                {
                    response.Status = "Invalid IP";
                    goto Finish;
                }
                var sqlQuery = @"Select Id,TID,[Address],PrivateKey from BinanceAddress(nolock) where [Address] = @Address";
                var param = new DynamicParameters();
                param.Add("Address", request.Address, DbType.String);
                var binanceAddress = await _dapper.GetAsync<BinanceAddress>(sqlQuery, param, commandType: CommandType.Text);
                if (binanceAddress != null && !string.IsNullOrEmpty(binanceAddress.PrivateKey))
                {
                    binanceAddress.PrivateKey = Crypto.O.Decrypt(binanceAddress.TID, binanceAddress.PrivateKey);
                    response = new BaseResponse<BinanceAddress>
                    {
                        StatusCode = 200,
                        Status = "Success",
                        Data = binanceAddress
                    };
                }
                return response;
            }
            catch (Exception ex)
            {
                response.Status = "Failed";
            }
        Finish:
            return response;
        }

        [HttpPost(nameof(GetBalance))]
        public async Task<BaseResponse<BinanceAPIResponse<string>>> GetBalance(BallanceRequest request)
        {
            string requestedUrl = string.Format(getBalance, request.TID.ToString(), request.WalletAddress, request.ContractAddress);
            var deserializeResponse = new BinanceAPIResponse<string>();
            var response = new BaseResponse<BinanceAPIResponse<string>>
            {
                StatusCode = 503,
                Status = "Bad Request",
                Data = deserializeResponse
            };
            try
            {
                RemoteIP = Request.HttpContext.Connection.RemoteIpAddress.ToString();
                request.IP = RemoteIP;
                bool isIPValid = await IsIPValid(request.IP);
                if (!isIPValid)
                {
                    response.Status = "Invalid IP";
                    goto Finish;
                }
                using (var httpClient = new HttpClient())
                {
                    using (var res = await httpClient.GetAsync(requestedUrl))
                    {
                        string result = await res.Content.ReadAsStringAsync();
                        deserializeResponse = JsonConvert.DeserializeObject<BinanceAPIResponse<string>>(result);
                    }
                }
                if (deserializeResponse != null && deserializeResponse.success)
                {
                    response = new BaseResponse<BinanceAPIResponse<string>>
                    {
                        StatusCode = 200,
                        Status = "Success",
                        Data = new BinanceAPIResponse<string>
                        {
                            success = deserializeResponse.success,
                            result = new Result<string>
                            {
                                message = deserializeResponse.result.message
                            }
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                response.Status = "Failed";
            }
        Finish:
            saveLog(JsonConvert.SerializeObject(request), requestedUrl, JsonConvert.SerializeObject(deserializeResponse), JsonConvert.SerializeObject(response));
            return response;
        }
        
        #endregion

        #region Methods
        private async Task<BinanceAddress> SaveBinanceAddress(BinanceAddress binanceAddress)
        {
            var sqlQuery = @"IF(Select count(1) from BinanceAddress where TID = @TID) = 0
                                insert into BinanceAddress(TID,[Address],PrivateKey,EntryOn) values (@TID,@Address,@PrivateKey,GetDate())
                             Select Id,TID,[Address],PrivateKey from BinanceAddress";
            var param = new DynamicParameters();
            param.Add("TID", binanceAddress.TID, DbType.String);
            param.Add("Address", binanceAddress.Address, DbType.String);
            param.Add("PrivateKey", binanceAddress.PrivateKey, DbType.String);
            binanceAddress = await _dapper.GetAsync<BinanceAddress>(sqlQuery, param, commandType: CommandType.Text);
            //binanceAddress = await Task.FromResult(_dapper.GetAsync<BinanceAddress>(sqlQuery, param, commandType: CommandType.Text));
            return binanceAddress;
        }

        private async Task<BinanceAddress> IFAddressExists(string TID)
        {
            var sqlQuery = @"Select Id,TID,[Address],PrivateKey from BinanceAddress where TID = @TID";
            var param = new DynamicParameters();
            param.Add("TID", TID, DbType.String);
            var binanceAddress = await _dapper.GetAsync<BinanceAddress>(sqlQuery, param, commandType: CommandType.Text);
            //var binanceAddress = await Task.FromResult(_dapper.Insert<BinanceAddress>(sqlQuery, param, commandType: CommandType.Text));
            return binanceAddress ?? new BinanceAddress();
        }
        private async Task<APIResponse> callAPI(string url)
        {
            APIResponse deserializeResponse = new APIResponse();
            try
            {
                using (var httpClient = new HttpClient())
                {
                    using (var res = await httpClient.GetAsync(url))
                    {
                        string response = await res.Content.ReadAsStringAsync();
                        deserializeResponse = JsonConvert.DeserializeObject<APIResponse>(response);
                    }
                }
            }
            catch (Exception ex)
            {
                deserializeResponse.msg = ex.Message;
            }
            return deserializeResponse ?? new APIResponse();
        }
        private string ReplaceSpecialCharecter(string arg)
        {
            arg = Regex.Replace(arg ?? string.Empty, @"[^0-9a-zA-Z]+", "");
            return arg;
        }
        private BaseResponse<string> validateRequest(string apiusername, string apipassword, string requestid, string jid, string content, string messagetype, string from)
        {
            var response = new BaseResponse<string>
            {
                IsModelValid = false
            };
            if (string.IsNullOrEmpty(apiusername))
            {
                response.Status = "Please send api user name in parameter";
                goto Finish;
            }
            if (string.IsNullOrEmpty(apipassword))
            {
                response.Status = "Please send api password in parameter";
                goto Finish;
            }
            if (string.IsNullOrEmpty(requestid))
            {
                response.Status = "Please send requestid in parameter";
                goto Finish;
            }
            if (string.IsNullOrEmpty(jid))
            {
                response.Status = "Please send jid in parameter";
                goto Finish;
            }
            if (jid.Length < 12 || jid.Length > 50)
            {
                response.Status = "invalid jid";
                goto Finish;
            }
            if (string.IsNullOrEmpty(content))
            {
                response.Status = "Please send content in parameter";
                goto Finish;
            }
            if (string.IsNullOrEmpty(messagetype))
            {
                response.Status = "Please send message type in parameter";
                goto Finish;
            }
            if (string.IsNullOrEmpty(from))
            {
                response.Status = "Please send from in parameter";
                goto Finish;
            }
            response = new BaseResponse<string>
            {
                IsModelValid = true
            };
        Finish:
            return response;
        }
        private bool IsNumeric(string s) => Regex.IsMatch(s, @"^[0-9]+$") && s != "";
        private async Task saveLog(EncryptRequest request, string requestedUrl, APIResponse APIRes, BaseResponse<APIResponse> response)
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
                //await Task.FromResult(_dapper.Insert<BaseResponse<string>>("insert into RequestResponseLog(IncomingRequest,SelfResponse,OutgoingRequest,Response,EntryOn,Remark) values (@IncomingRequest,@SelfResponse,@OutgoingRequest,@Response,getDate(),@Remark)", param, commandType: CommandType.Text));
                int  i = await _dapper.ExecuteAsync("insert into RequestResponseLog(IncomingRequest,SelfResponse,OutgoingRequest,Response,EntryOn,Remark) values (@IncomingRequest,@SelfResponse,@OutgoingRequest,@Response,getDate(),@Remark)", param, commandType: CommandType.Text);
            }
            catch(Exception ex)
            {

            }
            
            //Resolve here;
        }
        private async Task saveLog(string request, string requestedUrl, string APIRes, string response)
        {
            var param = new DynamicParameters();
            param.Add("IncomingRequest", request, DbType.String);
            param.Add("SelfResponse", response, DbType.String);
            param.Add("OutgoingRequest", requestedUrl, DbType.String);
            param.Add("Response", JsonConvert.SerializeObject(APIRes), DbType.String);
            param.Add("Remark", "Encrypted Data", DbType.String);
            await _dapper.ExecuteAsync("insert into RequestResponseLog(IncomingRequest,SelfResponse,OutgoingRequest,Response,EntryOn,Remark) values (@IncomingRequest,@SelfResponse,@OutgoingRequest,@Response,getDate(),@Remark)", param, commandType: CommandType.Text);
            //resolve here;
        }
        private async Task<string> GetSalt()
        {
            string salt = string.Empty;
            try
            {
                salt = await _dapper.GetAsync<string>(@"select top 1 [Salt] from SecretKey(nolock)"
                     , null,
                     commandType: CommandType.Text);
            }
            catch(Exception ex)
            {

            }
            return salt ?? string.Empty;
        }
        private async Task<bool> IsIPValid(string IP)
        {
            var sqlParam = new DynamicParameters();
            sqlParam.Add("IP", IP, DbType.String);
            return await _dapper.GetAsync<bool>(@"select 1 from IPMaster(nolock) where [IP]=@IP"
                     , sqlParam,
                     commandType: CommandType.Text);
        }
        #endregion Methods End
    }
}
