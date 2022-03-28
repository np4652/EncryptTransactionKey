using Microsoft.AspNetCore.Mvc;

namespace EncryptTransactionKey.Controllers
{
   
    public class HomeController : Controller
    {
        public IActionResult index()
        {
            return Redirect("~/swagger");
        }
    }
}
