using System;
using System.Reflection;
using System.Text;
using System.Web;
using System.Linq;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using System.Collections.Generic;
using Microsoft.Ajax.Utilities;
using QDLogistics.Models;
using System.Text.RegularExpressions;

namespace QDLogistics
{
    public static class HtmlHelperExtensions
    {
        private static string _displayVersion;
        private static string controller = string.Empty;

        /// <summary>
        ///     Retrieves a non-HTML encoded string containing the assembly version as a formatted string.
        ///     <para>If a project name is specified in the application configuration settings it will be prefixed to this value.</para>
        ///     <para>
        ///         e.g.
        ///         <code>1.0 (build 100)</code>
        ///     </para>
        ///     <para>
        ///         e.g.
        ///         <code>ProjectName 1.0 (build 100)</code>
        ///     </para>
        /// </summary>
        /// <param name="helper"></param>
        /// <returns></returns>
        public static IHtmlString AssemblyVersion(this HtmlHelper helper)
        {
            if (_displayVersion.IsNullOrWhiteSpace())
                SetDisplayVersion();

            return helper.Raw(_displayVersion);
        }

        /// <summary>
        ///     Compares the requested route with the given <paramref name="value" /> value, if a match is found the
        ///     <paramref name="attribute" /> value is returned.
        /// </summary>
        /// <param name="helper"></param>
        /// <param name="value">The action value to compare to the requested route action.</param>
        /// <param name="attribute">The attribute value to return in the current action matches the given action value.</param>
        /// <returns>A HtmlString containing the given attribute value; otherwise an empty string.</returns>
        public static IHtmlString RouteIf(this HtmlHelper helper, string value, string attribute)
        {
            var currentController =
                (helper.ViewContext.RequestContext.RouteData.Values["controller"] ?? string.Empty).ToString().UnDash();
            var currentAction =
                (helper.ViewContext.RequestContext.RouteData.Values["action"] ?? string.Empty).ToString().UnDash();

            currentAction = currentAction.Replace("Result", "");
            currentAction = currentAction.Replace("Add", "");
            currentAction = currentAction.Replace("Edit", "");

            var hasController = value.Equals(currentController, StringComparison.InvariantCultureIgnoreCase);
            var hasAction = value.Equals(currentAction, StringComparison.InvariantCultureIgnoreCase);

            return hasAction || hasController ? new HtmlString(attribute) : new HtmlString(string.Empty);
        }

        /// <summary>
        ///     Renders the specified partial view with the parent's view data and model if the given setting entry is found and
        ///     represents the equivalent of true.
        /// </summary>
        /// <param name="htmlHelper"></param>
        /// <param name="partialViewName">The name of the partial view.</param>
        /// <param name="appSetting">The key value of the entry point to look for.</param>
        public static void RenderPartialIf(this HtmlHelper htmlHelper, string partialViewName, string appSetting)
        {
            var setting = Settings.GetValue<bool>(appSetting);

            htmlHelper.RenderPartialIf(partialViewName, setting);
        }

        /// <summary>
        ///     Renders the specified partial view with the parent's view data and model if the given setting entry is found and
        ///     represents the equivalent of true.
        /// </summary>
        /// <param name="htmlHelper"></param>
        /// <param name="partialViewName">The name of the partial view.</param>
        /// <param name="condition">The boolean value that determines if the partial view should be rendered.</param>
        public static void RenderPartialIf(this HtmlHelper htmlHelper, string partialViewName, bool condition)
        {
            if (!condition)
                return;

            htmlHelper.RenderPartial(partialViewName);
        }

        /// <summary>
        ///     Retrieves a non-HTML encoded string containing the assembly version and the application copyright as a formatted
        ///     string.
        ///     <para>If a company name is specified in the application configuration settings it will be suffixed to this value.</para>
        ///     <para>
        ///         e.g.
        ///         <code>1.0 (build 100) © 2015</code>
        ///     </para>
        ///     <para>
        ///         e.g.
        ///         <code>1.0 (build 100) © 2015 CompanyName</code>
        ///     </para>
        /// </summary>
        /// <param name="helper"></param>
        /// <returns></returns>
        public static IHtmlString Copyright(this HtmlHelper helper)
        {
            var copyright =
                string.Format("{0} &copy; {1} {2}", helper.AssemblyVersion(), DateTime.Now.Year, Settings.Company)
                    .Trim();

            return helper.Raw(copyright);
        }

        private static void SetDisplayVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;

            _displayVersion =
                string.Format("{4} {0}.{1}.{2} (build {3})", version.Major, version.Minor, version.Build,
                    version.Revision, Settings.Project).Trim();
        }

        /// <summary>
        ///     Returns an unordered list (ul element) of validation messages that utilizes bootstrap markup and styling.
        /// </summary>
        /// <param name="htmlHelper"></param>
        /// <param name="alertType">The alert type styling rule to apply to the summary element.</param>
        /// <param name="heading">The optional value for the heading of the summary element.</param>
        /// <returns></returns>
        public static HtmlString ValidationBootstrap(this HtmlHelper htmlHelper, string alertType = "danger",
            string heading = "")
        {
            if (htmlHelper.ViewData.ModelState.IsValid)
                return new HtmlString(string.Empty);

            var sb = new StringBuilder();

            sb.AppendFormat("<div class=\"alert alert-{0} alert-block\">", alertType);
            sb.Append("<button class=\"close\" data-dismiss=\"alert\" aria-hidden=\"true\">&times;</button>");

            if (!heading.IsNullOrWhiteSpace())
            {
                sb.AppendFormat("<h4 class=\"alert-heading\">{0}</h4>", heading);
            }

            sb.Append(htmlHelper.ValidationSummary());
            sb.Append("</div>");

            return new HtmlString(sb.ToString());
        }

        public static IHtmlString RemoveHtmlTag(this HtmlHelper helper, string htmlSource, int words = 0, string keyword = null)
        {
            if(htmlSource != null && htmlSource != "")
            {
                //移除  javascript code.
                htmlSource = Regex.Replace(htmlSource, @"<script[\d\D]*?>[\d\D]*?</script>", String.Empty);

                //移除html tag.
                htmlSource = Regex.Replace(htmlSource, @"<[^>]*>", String.Empty);

                if (words > 0)
                {
                    byte[] l_byte = Encoding.Default.GetBytes(htmlSource);
                    htmlSource = Encoding.Default.GetString(l_byte, 0, words);
                }

                if (keyword != null && keyword != "")
                {
                    htmlSource += htmlSource.Replace(keyword, "<span class=\"key_word\">" + keyword + "</span>");
                }
            }

            return new HtmlString(htmlSource);
        }

        //public static MvcHtmlString SiteMenuAsUnorderedList(this HtmlHelper helper, IEnumerable<Lims> siteLinks)
        //{
        //    if (siteLinks == null) return MvcHtmlString.Empty;
        //    return MvcHtmlString.Create(buildMenuItems(helper, siteLinks, null));
        //}

        //private static string buildMenuItems(HtmlHelper html, IEnumerable<Lims> siteLinks, int? parentId, string controller = null)
        //{
        //    var parentTag = new TagBuilder("ul");

        //    var childSiteLinks = siteLinks.Where(i => i.ParentID == parentId).OrderBy(o => o.Sort);
        //    foreach (Lims siteLink in childSiteLinks)
        //    {
        //        var liTag = new TagBuilder("li");
        //        var anchorTag = new TagBuilder("a");

        //        if (siteLink.ParentID == null) //第一層時
        //        {
        //            if((int)html.ViewContext.HttpContext.Session.Contents["AdminID"] != 888)
        //            {
        //                IEnumerable<AdminLims> adminlims = (IEnumerable<AdminLims>)html.ViewContext.HttpContext.Session.Contents["AdminLims"];

        //                bool isauth = false;

        //                if(adminlims != null)
        //                {
        //                    foreach (AdminLims al in adminlims)
        //                    {
        //                        isauth = siteLink.Lims1.Any(a => a.LimID == al.LimID);
        //                        if (isauth) break;
        //                    }
        //                }
                        
        //                if (!isauth) continue;
        //            }

        //            controller = siteLink.Key;
        //            if ((string)html.ViewContext.RouteData.Values["controller"] == siteLink.Key) liTag.MergeAttribute("class", "active");
        //        }else //其他層時
        //        {
        //            if ((int)html.ViewContext.HttpContext.Session.Contents["AdminID"] != 888)
        //            {
        //                IEnumerable<AdminLims> adminlims = (IEnumerable<AdminLims>)html.ViewContext.HttpContext.Session.Contents["AdminLims"];

        //                bool isauth = false;

        //                if (adminlims != null)
        //                {
        //                    foreach (AdminLims al in adminlims)
        //                    {
        //                        if (siteLink.LimID == al.LimID) isauth = true;
        //                        if (isauth) break;
        //                    }
        //                }

        //                if (!isauth) continue;
        //            }

        //            var currentAction = (html.ViewContext.RouteData.Values["action"] ?? string.Empty).ToString().UnDash();
        //            currentAction = currentAction.Replace("Result", "");
        //            currentAction = currentAction.Replace("Add", "");
        //            currentAction = currentAction.Replace("Edit", "");

        //            if (currentAction == siteLink.Key) liTag.MergeAttribute("class", "active");
        //        }

        //        if (siteLink.Lims1.Count > 0) //如果有下一層
        //        {                    
        //            var iTag = new TagBuilder("i");
        //            var spanTag = new TagBuilder("span");

        //            iTag.MergeAttribute("class", "fa fa-lg fa-fw " + siteLink.Icon);
        //            spanTag.MergeAttribute("class", "menu-item-parent");

        //            spanTag.SetInnerText(siteLink.Value);

        //            anchorTag.MergeAttribute("href", "#");
        //            anchorTag.InnerHtml = iTag.ToString() + " " + spanTag.ToString();
        //        }
        //        else //如果沒有下一層
        //        {
        //            anchorTag.MergeAttribute("href", "/" + controller + "/" + siteLink.Key);
        //            anchorTag.SetInnerText(siteLink.Value);
        //        }

        //        liTag.InnerHtml = anchorTag.ToString();

        //        //如果有下一層
        //        if (siteLink.Lims1.Count > 0) liTag.InnerHtml += buildMenuItems(html, siteLinks, siteLink.LimID, controller);

        //        parentTag.InnerHtml += liTag;
        //    }
            
        //    return parentTag.ToString();
        //}
    }
}