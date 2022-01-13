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
        private readonly IDapper _dapper;
        private const string APIUrl = "https://teamrijent.in/admin/CoinService.aspx?TID={0}";
        private string RemoteIP = string.Empty;
        private const string key = "78956";
        public HomeController(IDapper dapper)
        {
            _dapper = dapper;
        }

        [HttpGet(nameof(index))]
        public IActionResult index()
        {
            return Ok("Welcome");
        }

        [HttpPost(nameof(Encrypt))]
        public async Task<BaseResponse<APIResponse>> Encrypt(EncryptRequest request)
        {
            string requestedUrl = string.Format(APIUrl, request.TID.ToString());
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
                var sqlParam = new DynamicParameters();
                sqlParam.Add("IP", RemoteIP, DbType.String);
                bool isIPValid = await Task.FromResult(_dapper.Insert<bool>(@"select 1 from IPMaster where [IP]=@IP"
                     , sqlParam,
                     commandType: CommandType.Text));
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

                var salt = await Task.FromResult(_dapper.Insert<string>(@"select top 1 [Salt] from SecretKey(nolock)"
                     , null,
                     commandType: CommandType.Text));

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
        public async Task<BaseResponse<string>> Decryp([FromBody] Request request)
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

        private async Task saveLog(EncryptRequest request,string requestedUrl,APIResponse APIRes, BaseResponse<APIResponse> response)
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
            if (newResponse.StatusCode==1 && !string.IsNullOrEmpty(newResponse.Data.msg))
            {
                Random _random = new Random();
                var arr = newResponse.Data.msg.ToList();
                int start = _random.Next(0, 30), end = _random.Next(31, 60);
                if(end<arr.Count)
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
            await Task.FromResult(_dapper.Insert<BaseResponse<string>>("insert into RequestResponseLog(IncomingRequest,SelfResponse,OutgoingRequest,Response,EntryOn,Remark) values (@IncomingRequest,@SelfResponse,@OutgoingRequest,@Response,getDate(),@Remark)", param, commandType: CommandType.Text));
        }
    }
}
