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

namespace APIApplication.Controllers
{
    [ApiController]
    [Route("api/")]
    public class HomeController : ControllerBase
    {
        private readonly IDapper _dapper;
        private const string APIUrl = "https://alerthub.in/wapi/v1/Send/wappsms?apiusername={0}";
        private readonly string RemoteIP = string.Empty;
        private const string key = "78956";
        public HomeController(IDapper dapper)
        {
            _dapper = dapper;
            RemoteIP = Request.HttpContext.Connection.RemoteIpAddress.ToString();
        }
        [HttpGet(nameof(index))]
        public IActionResult index()
        {
            return Ok("Welcome");
        }

        [HttpGet(nameof(Encrypt))]
        public async Task<BaseResponse<string>> Encrypt(EncryptRequest request)
        {
            var response = new BaseResponse<string>
            {
                StatusCode = 503,
                Status = "Bad Request",
                Data = "Some parameters are not supplied"
            };
            string plainText = string.Empty;
            try
            {
                var sqlParam = new DynamicParameters();
                sqlParam.Add("IP", RemoteIP, DbType.String);
                bool isIPValid = await Task.FromResult(_dapper.Insert<bool>(@"select 1 from IPMaster where [IP]=@IP"
                     , sqlParam,
                     commandType: CommandType.Text));
                if (!isIPValid)
                {
                    response.Data = "Invalid IP";
                    goto Finish;
                }
                /* Check if TID is valid */
                string requestedUrl = string.Format(APIUrl, request.TID);
                var res = await callAPI(requestedUrl);
                /* End TID Validation */
                
                var salt = await Task.FromResult(_dapper.Insert<string>(@"select top 1 [Salt] from SecretKey(nolock)"
                     , null,
                     commandType: CommandType.Text));

                plainText = string.Concat(salt, key);
                if (string.IsNullOrEmpty(plainText))
                {
                    response.Data = "Please enter plain text";
                    goto Finish;
                }
                response = new BaseResponse<string>
                {
                    StatusCode = 1,
                    Status = "Success",
                    Data = Crypto.O.EncryptUsingPublicKey(plainText, DocPath.PublicKey),
                };
            }
            catch (Exception ex)
            {
                response.Status = "Failed";
                response.Data = ex.Message;
            }

        Finish:
            /* Save log */
            var param = new DynamicParameters();
            param.Add("Request", plainText, DbType.String);
            param.Add("Response", response.Status, DbType.String);
            param.Add("Remark", "Encrypted Data", DbType.String);
            _dapper.Insert<BaseResponse<string>>("insert into RequestResponseLog(Request,Response,EntryOn,Remark) values (@Request,@Response,getDate(),@Remark)", param, commandType: CommandType.Text);
            return response;
        }



        [HttpPost(nameof(Decryp))]
        public async Task<BaseResponse<string>> Decryp([FromBody]Request request)
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

        private async Task<BaseResponse<string>> callAPI(string url)
        {
            BaseResponse<string> deserializeResponse = new BaseResponse<string>();
            using (var httpClient = new HttpClient())
            {
                using (var res = await httpClient.GetAsync(url))
                {
                    string apiResponse = await res.Content.ReadAsStringAsync();
                    deserializeResponse = JsonConvert.DeserializeObject<BaseResponse<string>>(apiResponse);
                }
            }
            return deserializeResponse ?? new BaseResponse<string>();
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
    }
}
