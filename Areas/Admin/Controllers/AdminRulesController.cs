using System.Web.Mvc;

namespace shcool_event_management.Areas.Admin.Controllers
{
    public class AdminRulesController : BaseAdminController
    {
        public ActionResult Index()
        {
            ViewBag.ActiveMenu = "rules";
            return View();
        }
    }
}
