﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
#if __CORE__
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Routing;
using HttpContext = Microsoft.AspNetCore.Http.HttpContext;
#else
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
#endif
using NewLife.Reflection;
using NewLife.Web;
using XCode;
using XCode.Configuration;
using XCode.Membership;

namespace NewLife.Cube
{
    /// <summary>视图助手</summary>
    public static class ViewHelper
    {
        /// <summary>创建页面设置的委托</summary>
        public static Func<Bootstrap> CreateBootstrap = () => new Bootstrap();

        /// <summary>获取页面设置</summary>
        /// <param name="context"></param>
        /// <returns></returns>
#if __CORE__
        public static Bootstrap Bootstrap(this HttpContext context)
#else
        public static Bootstrap Bootstrap(this HttpContextBase context)
#endif
        {
            var bs = context.Items["Bootstrap"] as Bootstrap;
            if (bs == null)
            {
                bs = CreateBootstrap();
                context.Items["Bootstrap"] = bs;
            }

            return bs;
        }

#if __CORE__
#else
        /// <summary>获取页面设置</summary>
        /// <param name="page"></param>
        /// <returns></returns>
        public static Bootstrap Bootstrap(this WebViewPage page) => Bootstrap(page.Context);
#endif
        /// <summary>获取页面设置</summary>
        /// <param name="controller"></param>
        /// <returns></returns>
        public static Bootstrap Bootstrap(this Controller controller) => Bootstrap(controller.HttpContext);

        /// <summary>获取路由Key</summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public static RouteValueDictionary GetRouteKey(this IEntity entity)
        {
            var fact = EntityFactory.CreateOperate(entity.GetType());
            var pks = fact.Table.PrimaryKeys;

            var rv = new RouteValueDictionary();
            if (fact.Unique != null)
            {
                rv["id"] = entity[fact.Unique.Name];
            }
            else if (pks.Length > 0)
            {
                foreach (var item in pks)
                {
                    rv[item.Name] = "{0}".F(entity[item.Name]);
                }
            }

            return rv;
        }

        /// <summary>获取排序分页以外的参数</summary>
        /// <returns></returns>
        public static RouteValueDictionary GetRouteValue(this Pager page)
        {
            var dic = new RouteValueDictionary();
            foreach (var item in page.Params)
            {
                if (!item.Key.EqualIgnoreCase("Sort", "Desc", "PageIndex", "PageSize")) dic[item.Key] = item.Value;
            }

            return dic;
        }

        internal static Boolean MakeListView(Type entityType, String vpath, List<FieldItem> fields)
        {
            var tmp = @"@using NewLife;
@using NewLife.Web;
@using XCode;
@using XCode.Configuration;
@using XCode.Membership;
@using NewLife.Cube;
@using System.Web.Mvc;
@using System.Web.Mvc.Ajax;
@using System.Web.Mvc.Html;
@using System.Web.Routing;
@{
    var fact = ViewBag.Factory as IEntityOperate;
    var page = ViewBag.Page as Pager;
    var fields = ViewBag.Fields as IList<FieldItem>;
    var set = ViewBag.PageSetting as PageSetting;
    //var provider = ManageProvider.Provider;
}
<table class=""table table-bordered table-hover table-striped table-condensed"">
    <thead>
        <tr>
            @if (set.EnableSelect)
            {
                <th class=""text-center"" style=""width:10px;""><input type=""checkbox"" id=""chkAll"" title=""全选"" /></th>
            }
            @foreach(var item in fields)
            {
                var sortUrl = item.OriField != null ? page.GetSortUrl(item.OriField.Name) : page.GetSortUrl(item.Name);
                <th class=""text-center""><a href=""@Html.Raw(sortUrl)"">@item.DisplayName</a></th>
            }
            @if (this.Has(PermissionFlags.Detail, PermissionFlags.Update, PermissionFlags.Delete))
            {
                <th class=""text-center"">操作</th>
            }
        </tr>
    </thead>
    <tbody>
        @foreach (var entity in Model)
        {
            <tr>
                @if (set.EnableSelect)
                {
                    <td class=""text-center""><input type=""checkbox"" name=""keys"" value=""@entity.ID"" /></td>
                }
                @foreach (var item in fields)
                {
                    @Html.Partial(""_List_Data_Item"", new Pair(entity, item))
                }
                @if (this.Has(PermissionFlags.Detail, PermissionFlags.Update, PermissionFlags.Delete))
                {
                    <td class=""text-center"">
                        @Html.Partial(""_List_Data_Action"", (Object)entity)
                    </td>
                }
            </tr>
        }
        @if (page.State != null)
        {
            var entity = page.State as IEntity;
            <tr>
                @if (set.EnableSelect)
                {
                    <td></td>
                }
                @Html.Partial(""_List_Data_Stat"", page.State)
                @if (this.Has(PermissionFlags.Detail, PermissionFlags.Update, PermissionFlags.Delete))
                {
                    <td></td>
                }
            </tr>
        }
    </tbody>
</table>";
            var sb = new StringBuilder();
            var fact = EntityFactory.CreateOperate(entityType);

            sb.AppendFormat("@model IList<{0}>", entityType.FullName);
            sb.AppendLine();

            tmp = tmp.Replace("page.State as IEntity", "page.State as " + entityType.FullName);

            var str = tmp.Substring(null, "            @foreach");
            // 如果有用户字段，则启用provider
            if (fields.Any(f => f.Name.EqualIgnoreCase("CreateUserID", "UpdateUserID")))
                str = str.Replace("//var provider", "var provider");
            sb.Append(str);

            var ident = new String(' ', 4 * 3);

            foreach (var item in fields)
            {
                // 缩进
                sb.Append(ident);

                var name = item.OriField?.Name ?? item.Name;
                var des = item.DisplayName ?? item.Name;

                // 样式
                sb.Append(@"<th class=""text-center""");

                // 固定宽度
                if (item.Type == typeof(DateTime))
                {
                    var width = item.Name.EndsWithIgnoreCase("Date") ? 80 : 134;
                    sb.AppendFormat(@" style=""min-width:{0}px;""", width);
                }

                // 备注
                if (!item.Description.IsNullOrEmpty() && item.Description != des) sb.AppendFormat(@" title=""{0}""", item.Description);

                // 内容
                sb.AppendFormat(@"><a href=""@Html.Raw(page.GetSortUrl(""{1}""))"">{0}</a></th>", des, name);

                sb.AppendLine();
            }

            var ps = new Int32[2];
            str = tmp.Substring("            @if (this.Has", "                @foreach (var item in fields)", 0, ps);
            if (fact.Unique != null)
                str = str.Replace("@entity.ID", "@entity." + fact.Unique.Name);
            else
                str = str.Replace("@entity.ID", "");

            sb.Append("            @if (this.Has");
            sb.Append(str);

            ident = new String(' ', 4 * 4);
            foreach (var item in fields)
            {
                // 第二名称，去掉后面的数字，便于模式匹配
                var name2 = item.Name;
                while (name2.Length > 1 && Char.IsDigit(name2[name2.Length - 1])) name2 = name2.Substring(0, name2.Length - 1);

                // 缩进
                sb.Append(ident);
                //sb.AppendLine(@"@Html.Partial(""_List_Data_Item"", new Pair(entity, item))");
                if (item.PrimaryKey)
                    sb.AppendFormat(@"<td class=""text-center"">@entity.{0}</td>", item.Name);
                else
                {
                    switch (Type.GetTypeCode(item.Type))
                    {
                        case TypeCode.Boolean:
                            sb.AppendLine(@"<td class=""text-center"">");
                            sb.Append(ident);
                            sb.AppendFormat(@"    <i class=""glyphicon glyphicon-@(entity.{0} ? ""ok"" : ""remove"")"" style=""color: @(entity.{0} ? ""green"" : ""red"");""></i>", item.Name);
                            sb.AppendLine();
                            sb.Append(ident);
                            sb.Append(@"</td>");
                            break;
                        case TypeCode.DateTime:
                            if (name2.EndsWith("Date"))
                                sb.AppendFormat(@"<td class=""text-center"">@entity.{0}.ToString(""yyyy-MM-dd"")</td>", item.Name);
                            else
                                sb.AppendFormat(@"<td class=""text-center"">@entity.{0}.ToFullString("""")</td>", item.Name);
                            break;
                        case TypeCode.Decimal:
                            sb.AppendFormat(@"<td class=""text-right"">@entity.{0}.ToString(""n2"")</td>", item.Name);
                            break;
                        case TypeCode.Single:
                        case TypeCode.Double:
                            if (name2.EndsWith("Rate"))
                            {
                                var des = item.Description + "";
                                if (des.Contains("百分之一"))
                                    sb.AppendFormat(@"<td class=""text-center"">@((entity.{0} / 100).ToString(""p2""))</td>", item.Name);
                                else if (des.Contains("万分之一"))
                                    sb.AppendFormat(@"<td class=""text-center"">@((entity.{0} / 10000).ToString(""p2""))</td>", item.Name);
                                else
                                    sb.AppendFormat(@"<td class=""text-center"">@entity.{0}.ToString(""p2"")</td>", item.Name);
                            }
                            else
                            {
                                sb.AppendFormat(@"<td class=""text-right"">@entity.{0}.ToString(""n2"")</td>", item.Name);
                            }
                            break;
                        case TypeCode.Byte:
                        case TypeCode.Int16:
                        case TypeCode.Int32:
                        case TypeCode.Int64:
                        case TypeCode.UInt16:
                        case TypeCode.UInt32:
                        case TypeCode.UInt64:
                            // 特殊处理枚举
                            if (item.Type.IsEnum)
                                sb.AppendFormat(@"<td class=""text-center"">@entity.{0}</td>", item.Name);
                            else if (item.Name.EqualIgnoreCase("CreateUserID", "UpdateUserID"))
                                BuildUser(item, sb);
                            else if (name2.EndsWith("Rate"))
                            {
                                var des = item.Description + "";
                                if (des.Contains("百分之一"))
                                    sb.AppendFormat(@"<td class=""text-center"">@((entity.{0} / 100d).ToString(""p2""))</td>", item.Name);
                                else if (des.Contains("万分之一"))
                                    sb.AppendFormat(@"<td class=""text-center"">@((entity.{0} / 10000d).ToString(""p2""))</td>", item.Name);
                                else
                                    sb.AppendFormat(@"<td class=""text-center"">@entity.{0}.ToString(""p2"")</td>", item.Name);
                            }
                            else
                                sb.AppendFormat(@"<td class=""text-right"">@entity.{0}.ToString(""n0"")</td>", item.Name);
                            break;
                        case TypeCode.String:
                            if (item.Map != null)
                            {
                                if (item.Map.Provider != null)
                                {
                                    var prv = item.Map.Provider;
                                    sb.AppendFormat(@"<td><a href=""{1}?{2}=@entity.{3}"">@entity.{0}</a></td>", item.Name, prv.EntityType.Name, prv.Key, item.OriField?.Name);
                                }
                                else
                                {
                                    sb.AppendFormat(@"<td class=""text-center"">@entity.{0}</td>", item.Name);
                                }
                            }
                            else if (item.Name.EqualIgnoreCase("CreateIP", "UpdateIP"))
                                BuildIP(item, sb);
                            else
                                sb.AppendFormat(@"<td>@entity.{0}</td>", item.Name);
                            break;
                        default:
                            sb.AppendFormat(@"<td>@entity.{0}</td>", item.Name);
                            break;
                    }
                }
                sb.AppendLine();
            }

            // 构造统计
            str = BuildStat(fields);

            sb.Append("                @if");
            var str2 = tmp.Substring("                @if", null, ps[1]);
            str = str2.Replace("                @Html.Partial(\"_List_Data_Stat\", page.State)", str);
            sb.Append(str);

            //sb.Append("@if (page.State != null)");
            //sb.Append(tmp.Substring("@if (page.State != null)", null, ps[1]));

            File.WriteAllText(vpath.GetFullPath().EnsureDirectory(true), sb.ToString(), Encoding.UTF8);

            return true;
        }

        private static void BuildUser(FieldItem item, StringBuilder sb) => sb.AppendFormat(@"<td class=""text-center"" class=""text-right"">@provider.FindByID(entity.{0})</td>", item.Name);

        private static void BuildIP(FieldItem item, StringBuilder sb) => sb.AppendFormat(@"<td class=""text-center"" title=""@entity.{0}.IPToAddress()"">@entity.{0}</td>", item.Name);

        private static String BuildStat(IList<FieldItem> fields)
        {
            var ident = new String(' ', 4 * 4);
            var sb = new StringBuilder();
            foreach (var item in fields)
            {
                // 第二名称，去掉后面的数字，便于模式匹配
                var name2 = item.Name;
                while (name2.Length > 1 && Char.IsDigit(name2[name2.Length - 1])) name2 = name2.Substring(0, name2.Length - 1);

                // 缩进
                sb.Append(ident);
                if (item.PrimaryKey)
                    sb.AppendFormat(@"<td class=""text-center"">总计</td>");
                else
                {
                    switch (Type.GetTypeCode(item.Type))
                    {
                        case TypeCode.Boolean:
                        case TypeCode.DateTime:
                            sb.Append(@"<td></td>");
                            break;
                        case TypeCode.Decimal:
                            sb.AppendFormat(@"<td class=""text-right"">@entity.{0}.ToString(""n2"")</td>", item.Name);
                            break;
                        case TypeCode.Single:
                        case TypeCode.Double:
                            if (name2.EndsWith("Rate"))
                            {
                                var des = item.Description + "";
                                if (des.Contains("百分之一"))
                                    sb.AppendFormat(@"<td class=""text-center"">@((entity.{0} / 100).ToString(""p2""))</td>", item.Name);
                                else if (des.Contains("万分之一"))
                                    sb.AppendFormat(@"<td class=""text-center"">@((entity.{0} / 10000).ToString(""p2""))</td>", item.Name);
                                else
                                    sb.AppendFormat(@"<td class=""text-center"">@entity.{0}.ToString(""p2"")</td>", item.Name);
                            }
                            else
                            {
                                sb.AppendFormat(@"<td class=""text-right"">@entity.{0}.ToString(""n2"")</td>", item.Name);
                            }
                            //sb.AppendFormat(@"<td class=""text-right"">@entity.{0:n2}</td>", item.Name);
                            break;
                        case TypeCode.Byte:
                        case TypeCode.Int16:
                        case TypeCode.Int32:
                        case TypeCode.Int64:
                        case TypeCode.UInt16:
                        case TypeCode.UInt32:
                        case TypeCode.UInt64:
                            // 特殊处理枚举
                            if (item.Type.IsEnum)
                                sb.Append(@"<td></td>");
                            else if (item.Name.EqualIgnoreCase("CreateUserID", "UpdateUserID"))
                                sb.Append(@"<td></td>");
                            else if (name2.EndsWith("Rate"))
                            {
                                var des = item.Description + "";
                                if (des.Contains("百分之一"))
                                    sb.AppendFormat(@"<td class=""text-center"">@((entity.{0} / 100d).ToString(""p2""))</td>", item.Name);
                                else if (des.Contains("万分之一"))
                                    sb.AppendFormat(@"<td class=""text-center"">@((entity.{0} / 10000d).ToString(""p2""))</td>", item.Name);
                                else
                                    sb.AppendFormat(@"<td class=""text-center"">@entity.{0}.ToString(""p2"")</td>", item.Name);
                            }
                            else
                                sb.AppendFormat(@"<td class=""text-right"">@entity.{0}.ToString(""n0"")</td>", item.Name);
                            break;
                        case TypeCode.String:
                        default:
                            sb.Append(@"<td></td>");
                            break;
                    }
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        internal static Boolean MakeFormView(Type entityType, String vpath, List<FieldItem> fields)
        {
            var tmp = @"@using NewLife;
@using XCode;
@using XCode.Configuration;
@{
    var entity = Model;
    var fields = ViewBag.Fields as IList<FieldItem>;
    var isNew = (entity as IEntity).IsNullKey;
}
@foreach (var item in fields)
{
    if (!item.IsIdentity)
    {
        <div class=""@cls"">
            @Html.Partial(""_Form_Item"", new Pair(entity, item))
        </div>
    }
}
@Html.Partial(""_Form_Footer"", entity)
@if (this.Has(PermissionFlags.Insert, PermissionFlags.Update))
{
    <div class=""clearfix form-actions col-sm-12 col-md-12"">
        <label class=""control-label col-xs-4 col-sm-5 col-md-5""></label>
        <button type=""submit"" class=""btn btn-success btn-sm""><i class=""glyphicon glyphicon-@(isNew ? ""plus"" : ""save"")""></i><strong>@(isNew ? ""新增"" : ""保存"")</strong></button>
        <button type=""button"" class=""btn btn-danger btn-sm"" onclick=""history.go(-1);""><i class=""glyphicon glyphicon-remove""></i><strong>取消</strong></button>
    </div>
}";

            var sb = new StringBuilder();
            var fact = EntityFactory.CreateOperate(entityType);

            sb.AppendLine($"@model {entityType.FullName}");

            var str = tmp.Substring(null, "@foreach");
            sb.Append(str);

            var set = Setting.Current;
            var cls = set.FormGroupClass;
            if (cls.IsNullOrEmpty()) cls = "form-group col-xs-12 col-sm-6 col-lg-4";

            var ident = new String(' ', 4 * 1);
            foreach (var item in fields)
            {
                if (item.IsIdentity) continue;

                sb.AppendLine($"<div class=\"{cls}\">");
                BuildFormItem(item, sb, fact);
                sb.AppendLine("</div>");
            }

            var p = tmp.IndexOf(@"@Html.Partial(""_Form_Footer""");
            sb.Append(tmp.Substring(p));

            File.WriteAllText(vpath.GetFullPath().EnsureDirectory(true), sb.ToString(), Encoding.UTF8);

            return true;
        }

        private static void BuildFormItem(FieldItem field, StringBuilder sb, IEntityOperate fact)
        {
            var des = field.Description.TrimStart(field.DisplayName).TrimStart(",", ".", "，", "。");

            var err = 0;

            var total = 12;
            var label = 3;
            var span = 4;
            if (err == 0 && des.IsNullOrEmpty())
            {
                span = 0;
            }
            else if (field.Type == typeof(Boolean) || field.Type.IsEnum)
            {
                span += 1;
            }
            var input = total - label - span;
            var ident = new String(' ', 4 * 1);

            sb.AppendLine($"    <label class=\"control-label col-xs-{label} col-sm-{label}\">{field.DisplayName}</label>");
            sb.AppendLine($"    <div class=\"input-group col-xs-{total - label} col-sm-{input}\">");

            // 优先处理映射。因为映射可能是字符串
            var map = field.Map;
            if (map?.Provider != null)
            {
                var field2 = field?.OriField ?? field;
                sb.AppendLine($"        @Html.ForDropDownList(\"{field2.Name}\", {fact.EntityType.Name}.Meta.AllFields.First(e=>e.Name==\"{field.Name}\").Map.Provider.GetDataSource(), @entity.{map.Name})");
            }
            else if (field.ReadOnly)
                sb.AppendLine($"        <label class=\"form-control\">@entity.{field.Name}</label>");
            else if (field.Type == typeof(String))
                BuildStringItem(field, sb);
            else if (fact.EntityType.As<IEntityTree>() && fact.EntityType.GetValue("Setting") is IEntityTreeSetting set && set?.Parent == field.Name)
                sb.AppendLine($"        @Html.ForTreeEditor({fact.EntityType.Name}._.{field.Name}, entity)");
            else
            {
                switch (field.Type.GetTypeCode())
                {
                    case TypeCode.Boolean:
                        sb.AppendLine($"        @Html.CheckBox(\"{field.Name}\", @entity.{field.Name}, new {{ @class = \"chkSwitch\" }})");
                        break;
                    case TypeCode.DateTime:
                        //sb.AppendLine($"        @Html.ForDateTime(\"{field.Name}\", @entity.{field.Name})");
                        sb.AppendLine($"        <span class=\"input-group-addon\"><i class=\"fa fa-calendar\"></i></span>");
                        sb.AppendLine($"        @Html.TextBox(\"{field.Name}\", @entity.{field.Name}.ToFullString(\"\"), new {{ @class = \"form-control date form_datetime\" }})");
                        break;
                    case TypeCode.Decimal:
                        //sb.AppendLine($"        @Html.ForDecimal(\"{field.Name}\", @entity.{field.Name})");
                        sb.AppendLine($"        <span class=\"input-group-addon\"><i class=\"fa fa-yen\"></i></span>");
                        sb.AppendLine($"        @Html.TextBox(\"{field.Name}\", @entity.{field.Name}, new {{ @class = \"form-control\" }})");
                        break;
                    case TypeCode.Single:
                    case TypeCode.Double:
                        //sb.AppendLine($"        @Html.ForDouble(\"{field.Name}\", @entity.{field.Name})");
                        sb.AppendLine($"        @Html.TextBox(\"{field.Name}\", @entity.{field.Name}, new {{ @class = \"form-control\" }})");
                        break;
                    case TypeCode.Byte:
                    case TypeCode.SByte:
                    case TypeCode.Int16:
                    case TypeCode.Int32:
                    case TypeCode.Int64:
                    case TypeCode.UInt16:
                    case TypeCode.UInt32:
                    case TypeCode.UInt64:
                        if (field.Type.IsEnum)
                            sb.AppendLine($"        @Html.ForEnum(\"{field.Name}\", @entity.{field.Name})");
                        else
                            sb.AppendLine($"        @Html.TextBox(\"{field.Name}\", @entity.{field.Name}, new {{ @class = \"form-control\", role=\"number\" }})");
                        break;
                    case TypeCode.String:
                        BuildStringItem(field, sb);
                        break;
                    default:
                        sb.AppendLine($"        @Html.ForEditor({fact.EntityType.Name}._.{field.Name}, entity)");
                        break;
                }
            }

            sb.AppendLine(@"    </div>");

            if (!des.IsNullOrEmpty()) sb.AppendLine($"    <span class=\"hidden-xs col-sm-{span}\"><span class=\"middle\">{des}</span></span>");
        }

        private static void BuildStringItem(FieldItem field, StringBuilder sb)
        {
            var cls = "form-control";
            var type = "text";
            var name = field.Name;

            // 首先输出图标
            var ico = "";

            var txt = "";
            if (name.EqualIgnoreCase("Pass", "Password"))
            {
                type = "password";
            }
            else if (name.EqualIgnoreCase("Phone", "Mobile"))
            {
                type = "tel";
                ico = "phone";
            }
            else if (name.EqualIgnoreCase("email", "mail"))
            {
                type = "email";
                ico = "envelope";
            }
            else if (name.EndsWithIgnoreCase("url"))
            {
                type = "url";
                ico = "home";
            }
            else if (field.Length < 0 || field.Length > 300)
            {
                txt = $"<textarea class=\"{cls}\" cols=\"20\" id=\"{name}\" name=\"{name}\" rows=\"3\">@entity.{name}</textarea>";
            }

            if (txt.IsNullOrEmpty()) txt = $"<input class=\"{cls}\" id=\"{name}\" name=\"{name}\" type=\"{type}\" value=\"@entity.{name}\" />";

            if (!ico.IsNullOrEmpty())
            {
                txt = $"<div class=\"input-group\"><span class=\"input-group-addon\"><i class=\"glyphicon glyphicon-{ico}\"></i></span>{txt}</div>";
            }

            sb.AppendLine($"        {txt}");
        }

        /// <summary>是否启用多选</summary>
        /// <param name="page"></param>
        /// <returns></returns>
#if __CORE__
        public static Boolean EnableSelect(this IRazorPage page)
#else
        public static Boolean EnableSelect(this WebViewPage page)
#endif
        {
            // 是否启用多选，仅取决于更新删除权限，不要求必须有唯一主键，方便前端对多主键灵活控制
            //var fact = page.ViewContext.ViewBag.Factory as IEntityOperate;
            //var fk = fact?.Unique;
            //if (fk == null) return false;

            if (page.ViewContext.ViewData.TryGetValue("EnableSelect", out var rs)) return (Boolean)rs;

            return page.Has(PermissionFlags.Update, PermissionFlags.Delete);

            //var user = page.ViewBag.User as IUser ?? page.User.Identity as IUser;
            //if (user == null) return false;

            //var menu = page.ViewBag.Menu as IMenu;

            //return user.Has(menu, PermissionFlags.Update, PermissionFlags.Delete);
        }

        /// <summary>获取头像地址</summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public static String GetAvatarUrl(this IUser user)
        {
            if (user == null || user.Avatar.IsNullOrEmpty()) return null;

            var set = Setting.Current;
            var av = set.AvatarPath.CombinePath(user.ID + ".png").GetFullPath();

            if (File.Exists(av)) return "/Sso/Avatar/" + user.ID;

            return user.Avatar;
        }

        private static Boolean? _IsDevelop;
        /// <summary>当前是否开发环境。判断csproj文件</summary>
        /// <returns></returns>
        public static Boolean IsDevelop()
        {
            if (_IsDevelop != null) return _IsDevelop.Value;

            var fis = ".".AsDirectory().GetFiles("*.csproj", SearchOption.TopDirectoryOnly);
            _IsDevelop = fis != null && fis.Length > 0;

            return _IsDevelop.Value;
        }

        private static readonly Dictionary<String, String> _logo_cache = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
        /// <summary>获取指定名称的Logo图标</summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static String GetLogo(String name)
        {
            if (_logo_cache.TryGetValue(name, out var logo)) return logo;

            //var ico = "/Content/images/logo/{0}.png".F(mi.Name);
            var paths = new[] { "/Content/images/logo/", "/Content/Logo/" };
            foreach (var item in paths)
            {
                var ico = item.TrimStart("/").AsDirectory().GetAllFiles(name + ".*").FirstOrDefault();
                if (ico != null && ico.Exists)
                {
                    logo = item + ico.Name;
                    break;
                }
            }

            // 缓存起来
            _logo_cache[name] = logo;

            return logo;
        }
    }

    /// <summary>Bootstrap页面控制。允许继承</summary>
    public class Bootstrap
    {
        #region 属性
        /// <summary>最大列数</summary>
        public Int32 MaxColumn { get; set; } //= 2;

        /// <summary>默认标签宽度</summary>
        public Int32 LabelWidth { get; set; }// = 4;
        #endregion

        #region 当前项
        ///// <summary>当前项</summary>
        //public FieldItem Item { get; set; }

        /// <summary>名称</summary>
        public String Name { get; set; }

        /// <summary>类型</summary>
        public Type Type { get; set; }

        /// <summary>长度</summary>
        public Int32 Length { get; set; }

        /// <summary>设置项</summary>
        public void Set(FieldItem item)
        {
            Name = item.Name;
            Type = item.Type;
            Length = item.Length;
        }
        #endregion

        #region 构造
        /// <summary>实例化一个页面助手</summary>
        public Bootstrap()
        {
            MaxColumn = 2;
            LabelWidth = 4;
        }
        #endregion

        #region 方法
        /// <summary>获取分组宽度</summary>
        /// <returns></returns>
        public virtual Int32 GetGroupWidth()
        {
            if (MaxColumn > 1 && Type != null)
            {
                if (Type != typeof(String) || Length <= 100) return 12 / MaxColumn;
            }

            return 12;
        }
        #endregion
    }
}