using System.Diagnostics;
using Logistica_y_transporte.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Logistica_y_transporte.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        [AllowAnonymous]
        public IActionResult Blog()=>View();

        [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
        public IActionResult Idioma(string idioma,string? volver)
        {
            var code=idioma=="en"?"en":"es";
            Response.Cookies.Append(Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.DefaultCookieName,
                Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.MakeCookieValue(new Microsoft.AspNetCore.Localization.RequestCulture(code=="en"?"en-US":"es-GT",code)),
                new CookieOptions{HttpOnly=true,Secure=Request.IsHttps,SameSite=SameSiteMode.Lax,Path=Request.PathBase.HasValue?Request.PathBase.Value:"/",MaxAge=TimeSpan.FromDays(365)});
            return LocalRedirect(Url.IsLocalUrl(volver)&&volver!.StartsWith(Request.PathBase+"/",StringComparison.Ordinal)?volver:Url.Action("Index","Home")!);
        }

        [AllowAnonymous]
        public IActionResult Privacy()
        {
            return View();
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
       // hola mundo
    }
}