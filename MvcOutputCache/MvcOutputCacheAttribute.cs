namespace MvcOutputCaching
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Reflection;
	using System.Text;
	using System.Web;
	using System.Web.Caching;
	using System.Web.Mvc;
	using System.Web.UI;

	public class MvcOutputCacheAttribute : ActionFilterAttribute
	{
		public MvcOutputCacheAttribute()
		{
			AnonymousDuration = Duration = 60;
			AnonymousOnly = false;
			VaryByCookie = null;
			VaryByUser = true;
			VaryByHost = true;
			VaryByAjax = true;
			VaryByQueryString = "*";
		}

		public MvcOutputCacheAttribute(int duration)
			: this()
		{
			Duration = duration;
		}

		public int Duration { get; set; }
		public int AnonymousDuration { get; set; }
		public string VaryByCookie { get; set; }
		public bool VaryByUser { get; set; }											// some scenarios don't care if the user is logged in; most do
		public string VaryByQueryString { get; set; }
		public bool VaryByHost { get; set; }											// in case web site served on multiple domains
		public bool VaryByAjax { get; set; }
		public bool AnonymousOnly { get; set; }
		public bool InvalidateUserCacheOnPost { get; set; }

		public static readonly string SessionKey = "MvcOutputCache.LastModifiedDate";

		private TextWriter _originalWriter;
		private string _cacheKey;
		private bool _nocache;
		
		private bool DetermineNoCache(ActionExecutingContext filterContext)
		{
			if (filterContext.HttpContext.Request.HttpMethod != "GET") return true;		// only for GET requests
			if (AnonymousOnly && !IsAnonymous(filterContext)) return true;
			DateTime? lastModifiedTime = GetUserLastModifiedTime(filterContext);
			if (lastModifiedTime != null) {
				var limit = DateTime.UtcNow.AddSeconds(-(GetDuration(filterContext) + 10));			// minor fudge factor in case web farm machine clocks are not exactly in sync
				if (lastModifiedTime >= limit) return true;
			}

			return false;
		}

		protected virtual DateTime? GetUserLastModifiedTime(ActionExecutingContext filterContext)
		{
			if (filterContext.HttpContext.Session != null)
				return filterContext.HttpContext.Session[SessionKey] as DateTime?;
			
			return null;
		}

		private int GetDuration(ControllerContext filterContext)
		{
			if (AnonymousDuration > 0 && IsAnonymous(filterContext)) return AnonymousDuration;
			return Duration;
		}

		protected virtual bool IsAnonymous(ControllerContext filterContext)
		{
			return !filterContext.HttpContext.Request.IsAuthenticated;
		}

		protected virtual string GetVaryByCustomString(ControllerContext filterContext)
		{
			return null;
		}

		public override void OnActionExecuting(ActionExecutingContext filterContext)
		{
			if (InvalidateUserCacheOnPost && filterContext.HttpContext.Request.HttpMethod == "POST")
				InvalidateUserOutputCacheAttribute.InvalidateUserCache(filterContext);

			_nocache = DetermineNoCache(filterContext);
			if (_nocache) return;

			_cacheKey = ComputeCacheKey(filterContext);

			string cachedOutput = (string)filterContext.HttpContext.Cache[_cacheKey];
			if (cachedOutput != null)
				filterContext.Result = new ContentResult { Content = cachedOutput };
			else
				_originalWriter = (TextWriter)_switchWriterMethod.Invoke(HttpContext.Current.Response, new object[] { new HtmlTextWriter(new StringWriter()) });
		}

		public override void OnResultExecuted(ResultExecutedContext filterContext)
		{
			if (_nocache) return;

			if (_originalWriter != null) // Must complete the caching
			{
				HtmlTextWriter cacheWriter = (HtmlTextWriter)_switchWriterMethod.Invoke(HttpContext.Current.Response, new object[] { _originalWriter });

				string textWritten = ((StringWriter)cacheWriter.InnerWriter).ToString();
				filterContext.HttpContext.Response.Write(textWritten);

				filterContext.HttpContext.Cache.Add(_cacheKey, textWritten, null, DateTime.Now.AddSeconds(GetDuration(filterContext)), Cache.NoSlidingExpiration, CacheItemPriority.Normal, null);
			}
		}

		private string ComputeCacheKey(ActionExecutingContext filterContext)
		{
			// Assumptions: empty param values & order of params are irrelevant; optimize by sorting and removing empties

			var context = filterContext.HttpContext;
			var request = context.Request;
			var url = request.Url;

			var keyBuilder = new StringBuilder();

			if (VaryByHost) keyBuilder.AppendFormat("host_{0}", url.Authority);

			var userKey = IsAnonymous(filterContext) ? "-1" : context.User.Identity.Name.GetHashCode().ToString();
			if (VaryByUser) keyBuilder.AppendFormat("u_{0}", userKey);
			if (VaryByAjax) keyBuilder.AppendFormat("ajax_{0}", filterContext.HttpContext.Request.IsAjaxRequest().GetHashCode());

			foreach (var pair in filterContext.RouteData.Values.Where(p => p.Value != null).OrderBy(p => p.Key))
				keyBuilder.AppendFormat("rd{0}_{1}_", pair.Key.GetHashCode(), pair.Value.GetHashCode());

			foreach (var pair in filterContext.ActionParameters.Where(p => p.Value != null).OrderBy(p => p.Key))
				keyBuilder.AppendFormat("ap{0}_{1}_", pair.Key.GetHashCode(), pair.Value.GetHashCode());

			if (!String.IsNullOrEmpty(VaryByCookie) && VaryByCookie != "none") {
				string[] cookienames = { };

				if (VaryByCookie == "*")
					cookienames = request.Cookies.AllKeys;
				else if (!String.IsNullOrEmpty(VaryByCookie))
					cookienames = VaryByCookie.Split(", ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

				IEnumerable<HttpCookie> cookies = cookienames
					.Where(c => !String.IsNullOrEmpty(c) && request.Cookies[c] != null && !String.IsNullOrEmpty(request.Cookies[c].Value))
					.Select(c => request.Cookies[c])
					.OrderBy(c => c.Name);

				foreach (var cookie in cookies)
					keyBuilder.AppendFormat("c{0}_{1}_", cookie.Name.GetHashCode(), cookie.Value.GetHashCode());
			}

			if (!String.IsNullOrEmpty(VaryByQueryString) && VaryByQueryString != "none") {
				string[] paramnames = { };

				if (VaryByQueryString == "*")
					paramnames = request.QueryString.AllKeys;
				else if (!String.IsNullOrEmpty(VaryByQueryString))
					paramnames = VaryByQueryString.Split(", ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

				paramnames = paramnames
					.Where(p => !String.IsNullOrEmpty(p) && request.QueryString[p] != null && !String.IsNullOrEmpty(request.QueryString[p]))
					.OrderBy(p => p).ToArray();

				foreach (var paramname in paramnames)
					keyBuilder.AppendFormat("qs{0}_{1}_", paramname.GetHashCode(), request.QueryString[paramname].GetHashCode());
			}

			var custom = GetVaryByCustomString(filterContext);

			if (!String.IsNullOrEmpty(custom))
				keyBuilder.AppendFormat("custom_{0}_", custom.GetHashCode());
		
			return keyBuilder.ToString();
		}
		
		// This hack is optional; I'll explain it later in the blog post
		private static MethodInfo _switchWriterMethod = typeof(HttpResponse).GetMethod("SwitchWriter", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

	}

	/// <summary>
	/// Scenario: a user updates information; they should see their changes immediately; therefore, invalidate cache for that user
	/// This is done by storing (in Session) the last time the user modified data. MvcOutputCache will not use cache if last modified time is within cache duration.
	/// Note: in web farm scenarios, make sure Session is shared across machines, eg, by using SQL Session provider
	/// </summary>
	public class InvalidateUserOutputCacheAttribute : ActionFilterAttribute
	{
		public override void OnResultExecuted(ResultExecutedContext filterContext)
		{
			InvalidateUserCache(filterContext);
			base.OnResultExecuted(filterContext);
		}

		public static void InvalidateUserCache(ControllerContext filterContext)
		{
			if (filterContext.HttpContext.Session != null)
				filterContext.HttpContext.Session[MvcOutputCacheAttribute.SessionKey] = DateTime.UtcNow;		// always use UTC!
		}
	}
}