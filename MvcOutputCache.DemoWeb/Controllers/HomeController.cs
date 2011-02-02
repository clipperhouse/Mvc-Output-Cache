using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using MvcOutputCaching;

namespace MvcOutputCache.DemoWeb.Controllers
{
	public class HomeController : Controller
	{
		//
		// GET: /Home/

		[MvcOutputCache(300)]
		public ActionResult Index()
		{
			return View();
		}

	}
}
