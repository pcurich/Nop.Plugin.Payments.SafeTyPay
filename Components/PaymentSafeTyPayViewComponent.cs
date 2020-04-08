using Microsoft.AspNetCore.Mvc;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.SafeTyPay.Components
{
    [ViewComponent(Name = "PaymentSafeTyPay")]
    public class PaymentSafeTyPayViewComponent : NopViewComponent
    {
        public IViewComponentResult Invoke()
        {
            return View("~/Plugins/Payments.SafeTyPay/Views/PaymentInfo.cshtml");
        }
    }
}