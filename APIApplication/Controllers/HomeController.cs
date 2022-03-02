using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using APIApplication.Model;
using System.Net.Http;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using EncryptTransactionKey.Model;
using EncryptTransactionKey.Helpers;
using EncryptTransactionKey.DataContext;
using System.Collections.Generic;
using System.Linq;

namespace APIApplication.Controllers
{
    [ApiController]
    [Route("api/")]
    public class HomeController : ControllerBase
    {
        #region variables
        private string APIUrl;
        private string generateAddress;
        private string getBalance;
        private string RemoteIP = string.Empty;
        private IDbContext _dbContext;
        private readonly Dictionary<string, string> keyCollection = new Dictionary<string, string>
        {
            { "key","fa0cd267144f93e9481bf0001564baf51b21"},
            { "Withdrawalkey","fa0cd267144f93e9481bf0001564baf51b21"}
        };
        #endregion
        public HomeController(IDbContext dbContext, List<API> apis)
        {
            _dbContext = dbContext;
            InitializeVariable(apis);
        }

        [HttpGet(nameof(index))]
        public IActionResult index()
        {
            return Ok("Welcome");
        }

        #region APIs

        private async Task<BaseResponse<APIResponse>> ValidateTID(EncryptRequest request)
        {
            string requestedUrl = string.Format(APIUrl, request.TID.ToString(), request.Option1, request.Option2, request.Option3, request.Option4, request.Option5);
            var response = new BaseResponse<APIResponse>
            {
                StatusCode = 503,
                Status = "Bad Request",
                Data = new APIResponse
                {
                    status = -1,
                    msg = "Invalid IP"
                }
            };
            try
            {
                RemoteIP = Request.HttpContext.Connection.RemoteIpAddress.ToString();
                request.IP = RemoteIP;
                bool isIPValid = await _dbContext.IsIPValid(request.IP);
                if (!isIPValid)
                    goto Finish;
                var APIRes = await callAPI(requestedUrl);
                if (APIRes.status == -1)
                    goto Finish;
                response.Status = requestedUrl;
                response.Data = APIRes;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        Finish:
            return response;
        }

        [HttpPost(nameof(Encrypt))]
        public async Task<BaseResponse<APIResponse>> Encrypt(EncryptRequest request)
        {
            //string requestedUrl = string.Format(APIUrl, request.TID.ToString(), request.Option1, request.Option2, request.Option3, request.Option4, request.Option5);
            string requestedUrl = string.Empty;
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
                response = await ValidateTID(request);
                APIRes = response.Data ?? new APIResponse();
                requestedUrl = response.Status;
                if (APIRes.status == -1)
                {
                    //response.Data = APIRes;
                    goto Finish;
                }

                //APIRes = response.Data;

                plainText = await _dbContext.GetSecretKey(request.Option3 ?? string.Empty, keyCollection);

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
                        status = 1,
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
            _dbContext.saveLog(request, requestedUrl, APIRes, response);
            return response;
        }

        [HttpPost(nameof(Decryp))]
        public BaseResponse<string> Decryp(Request request)
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
                    Data = string.IsNullOrEmpty(request.Key) ? Crypto.O.DecryptUsingPrivateKey(request.PlainText, DocPath.PrivateKey) : Crypto.O.Decrypt(request.Key, request.PlainText),
                };
            }
            catch (Exception ex)
            {
                response.Status = "Failed";
                response.Data = ex.Message;
            }
            return response;
        }

        [HttpPost("GenrateBinanceAddress")]
        [HttpPost(nameof(GenrateNetworkAddress))]
        public async Task<BaseResponse<BinanceAPIResponse<AddressWithPrivateKey>>> GenrateNetworkAddress(GenrateAddressReq request)
        {
            BaseResponse<BinanceAPIResponse<AddressWithPrivateKey>> genrateResponse(NetworkAddress binanceAddress)
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
                bool isIPValid = await _dbContext.IsIPValid(request.IP);
                if (!isIPValid)
                {
                    response.Status = "Invalid IP";
                    goto Finish;
                }
                var binanceAddress = await _dbContext.IFAddressExists(request.TID, request.NetworkId);
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
                if (deserializeResponse != null)
                {
                    if (!deserializeResponse.success)
                    {
                        response.Data.result = deserializeResponse.result;
                        goto Finish;
                    }
                    deserializeResponse.result.message.privateKey = Crypto.O.Encrypt(request.TID, deserializeResponse.result.message.privateKey);
                    binanceAddress = await _dbContext.SaveNetworkAddress(new NetworkAddress
                    {
                        TID = request.TID,
                        Address = deserializeResponse.result.message.address,
                        PrivateKey = deserializeResponse.result.message.privateKey,
                        NetworkId = request.NetworkId
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
            _dbContext.saveLog(JsonConvert.SerializeObject(request), requestedUrl, JsonConvert.SerializeObject(deserializeResponse), JsonConvert.SerializeObject(response));
            return response;
        }

        [HttpPost(nameof(GetPrivateKey))]
        public async Task<BaseResponse<NetworkAddress>> GetPrivateKey(PrivateKeyRequest request)
        {
            var response = new BaseResponse<NetworkAddress>
            {
                StatusCode = 503,
                Status = "Bad Request",
            };
            try
            {
                request.RequestType = string.IsNullOrEmpty(request.RequestType) ? string.Empty : request.RequestType;
                RemoteIP = Request.HttpContext.Connection.RemoteIpAddress.ToString();
                if (request.RequestType.Equals("Withdrawal", StringComparison.OrdinalIgnoreCase))
                {
                    var _ = await Encrypt(new EncryptRequest
                    {
                        TID = request.TID,
                        Option1 = request.Address,
                        Option2 = request.Amount,
                        Option3 = request.RequestType,
                    });
                    if (_.StatusCode == 1)
                    {
                        response = new BaseResponse<NetworkAddress>
                        {
                            StatusCode = 200,
                            Status = "Success",
                            Data = new NetworkAddress
                            {
                                TID = request.TID,
                                Address = request.Address,
                                PrivateKey = _.Data.msg
                            }
                        };
                        goto Finish;
                    }
                    response.StatusCode = _.Data.status;
                    response.Status = _?.Data.msg;
                }

                var validateTID = await ValidateTID(new EncryptRequest
                {
                    TID = request.TID,
                    Option1 = request.Address,
                    Option2 = request.Amount,
                    Option3 = request.RequestType,
                    Option4 = request.UserId,
                });
                if (validateTID.StatusCode == -1)
                    goto Finish;
                var binanceAddress = await _dbContext.GetBinanceInfoByAddress(request.Address, request.UserId);
                if (binanceAddress != null && !string.IsNullOrEmpty(binanceAddress.PrivateKey))
                {
                    binanceAddress.PrivateKey = Crypto.O.Decrypt(binanceAddress.TID, binanceAddress.PrivateKey);
                    response = new BaseResponse<NetworkAddress>
                    {
                        StatusCode = 200,
                        Status = "Success",
                        Data = binanceAddress
                    };
                }
                goto Finish;
            }
            catch (Exception ex)
            {
                response.Status = "Failed";
            }
        Finish:
            _dbContext.saveLog(JsonConvert.SerializeObject(request), "/api/GetPrivateKey", JsonConvert.SerializeObject(response), JsonConvert.SerializeObject(response));
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
                bool isIPValid = await _dbContext.IsIPValid(request.IP);
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
                if (deserializeResponse != null)
                {
                    if (!deserializeResponse.success)
                    {
                        response.Data.result = deserializeResponse.result;
                        goto Finish;
                    }
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
            _dbContext.saveLog(JsonConvert.SerializeObject(request), requestedUrl, JsonConvert.SerializeObject(deserializeResponse), JsonConvert.SerializeObject(response));
            return response;
        }

        #endregion

        #region Methods
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

        private void InitializeVariable(List<API> apis)
        {
            IEnumerable<APIConfig> APIConfig = apis.Where(x => x.Provider.Equals("teamrijent", StringComparison.OrdinalIgnoreCase)).Select(x => x.APIConfig).FirstOrDefault();
            APIUrl = APIConfig.Where(x => x.Name.Equals("ValidateTID", StringComparison.OrdinalIgnoreCase)).Select(x => x.Url)?.FirstOrDefault();
            generateAddress = APIConfig.Where(x => x.Name.Equals("generateAddress", StringComparison.OrdinalIgnoreCase)).Select(x => x.Url)?.FirstOrDefault();
            getBalance = APIConfig.Where(x => x.Name.Equals("getBalance", StringComparison.OrdinalIgnoreCase)).Select(x => x.Url)?.FirstOrDefault();
        }
        #endregion Methods End
    }
}