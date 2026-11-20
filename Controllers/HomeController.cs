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

        [AllowAnonymous]
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

        public IActionResult Ajustes()=>View();
        [Authorize(Policy=Logistica_y_transporte.Seguridad.PermisosPersonas.Leer),HttpGet,ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
        public async Task<IActionResult> Cifras([FromServices] AppDbContext db,[FromServices] Microsoft.AspNetCore.Authorization.IAuthorizationService auth,CancellationToken ct)
        {
            var todos=(await auth.AuthorizeAsync(User,Logistica_y_transporte.Seguridad.PermisosPersonas.Todos)).Succeeded;
            var id=User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if(id is null)return Forbid();
            var consulta=db.Personas.AsQueryable();if(!todos)consulta=consulta.Where(p=>p.UserId==id);
            return Json(new{count=await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(consulta,ct),allAuthorized=todos});
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