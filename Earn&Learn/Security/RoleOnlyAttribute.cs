using Earn_Learn.Models;
using Earn_Learn.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Identity;

namespace Earn_Learn.Security
{
    public class RoleOnlyAttribute : ActionFilterAttribute
    {
        private readonly Uloga _dozvoljenaUloga;

        public RoleOnlyAttribute(Uloga uloga)
        {
            _dozvoljenaUloga = uloga;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var userManager = context.HttpContext.RequestServices
                .GetRequiredService<UserManager<Korisnik>>();

            var user = userManager.GetUserAsync(context.HttpContext.User).Result;

            if (user == null || user.Uloga != _dozvoljenaUloga)
            {
                context.Result = new RedirectToActionResult(
                    "PristupOdbijen",
                    "Home",
                    null);
            }

            base.OnActionExecuting(context);
        }
    }
}