using Microsoft.AspNetCore.Mvc;

namespace P1F_TPM360_HUB.Function
{
    public static class SessionCheck
    {
        public static IActionResult CheckSession(this Controller controller, Func<IActionResult> action)
        {
            var session = controller.HttpContext.Session;

            string controllerName = controller.GetType().Name.Replace("Controller", "").ToLower();
            string sessionLevel = (session.GetString("level") ?? "").ToLower();
            string sessionRole = (session.GetString("role") ?? "").ToLower();

            return action();
        }
    }
}