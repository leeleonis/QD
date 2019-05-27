using Newtonsoft.Json;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using QDLogistics.Commons;
using QDLogistics.Models;
using QDLogistics.Models.Repositiry;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.WebPages;

public static class MyHelp
{
    private static QDLogisticsEntities db = new QDLogisticsEntities();
    private static IRepository<ActionLog> ActionLog = new GenericRepository<ActionLog>(db);

    public static DateTime DateTimeWithZone(DateTime dateTime, bool local = true) // local = true 換算成台北時間 ， local = false 換算成紐約時間
    {
        TimeZoneInfo EstTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        TimeZoneInfo TstTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Taipei Standard Time");

        DateTime utcDateTime = TimeZoneInfo.ConvertTimeToUtc(dateTime, local ? EstTimeZone : TimeZoneInfo.Local);
        return TimeZoneInfo.ConvertTime(utcDateTime, (local ? TstTimeZone : EstTimeZone));
    }

    public static IEnumerable<Country> GetCountries()
    {
        var result = CultureInfo.GetCultures(CultureTypes.SpecificCultures).Where(x => x.LCID != 4096).Select(x => new Country(x.LCID)).GroupBy(c => c.ID).Select(c => c.First()).OrderBy(x => x.Name);

        return result;
    }

    public static EnumData.TimeZone GetTimeZone(int zoneID)
    {
        foreach (EnumData.TimeZone timeZone in Enum.GetValues(typeof(EnumData.TimeZone)))
        {
            if (zoneID == (int)timeZone) return timeZone;
        }

        return EnumData.TimeZone.TST;
    }

    public static bool CheckAuth(string controller, string action, EnumData.AuthType check, Menu menu = null)
    {
        if (Convert.ToBoolean(HttpContext.Current.Session["IsManager"])) return true;

        if (menu == null)
        {
            IRepository<Menu> Menu = new GenericRepository<Menu>(new QDLogisticsEntities());
            menu = Menu.GetAll().First(m => m.IsEnable == true && m.Controller == controller && m.Action == action);
            if (menu == null) return false;
        }

        string auth = (string)HttpContext.Current.Session["auth"];
        if (auth.IsEmpty()) return false;

        List<bool> Auth;
        Dictionary<int, List<bool>> AdminAuth = JsonConvert.DeserializeObject<Dictionary<int, List<bool>>>(auth);

        if (!AdminAuth.TryGetValue(menu.MenuId, out Auth)) return false;

        return Auth[(int)check];
    }

    public static string Encrypt(string strPwd)
    {
        string str = "";

        MD5 md5 = new MD5CryptoServiceProvider();
        byte[] data = Encoding.Default.GetBytes(strPwd);
        byte[] md5Data = md5.ComputeHash(data);
        md5.Clear();
        for (int i = 0; i < md5Data.Length - 1; i++)
        {
            str += md5Data[i].ToString("x").PadLeft(2, '0');
        }

        return str;
    }

    /// <summary>
    /// 完整的寄信功能
    /// </summary>
    /// <param name="MailFrom">寄信人E-mail Address</param>
    /// <param name="MailTos">收信人E-mail Address</param>
    /// <param name="Ccs">副本E-mail Address</param>
    /// <param name="MailSub">主旨</param>
    /// <param name="MailBody">信件內容</param>
    /// <param name="isBodyHtml">是否採用HTML格式</param>
    /// <param name="filePaths">附檔在WebServer檔案總管路徑</param>
    /// <param name="deleteFileAttachment">是否刪除在WebServer上的附件</param>
    /// <returns>是否成功</returns>
    public static bool Mail_Send(string MailFrom, string[] MailTos, string[] Ccs, string MailSub, string MailBody, bool isBodyHtml, string[] filePaths, List<Tuple<Stream, string>> filePaths2, bool deleteFileAttachment)
    {
        string smtpServer = "smtp.sendgrid.net";
        int smtpPort = 587;
        string mailAccount = "azure_d6e7b8a9c9606ce581770e3a9db11a27@azure.com";
        string mailPwd = "6S2OHhiXob9Q";
        //string mailAccount = "nexeve";
        //string mailPwd = "96yVfFZ9a81GqnDYTUebjncIsZe5Rvhnbv1j";

        try
        {
            //防呆
            if (string.IsNullOrEmpty(MailFrom))
            {//※有些公司的Mail Server會規定寄信人的Domain Name要是該Mail Server的Domain Name
                MailFrom = "dispatch-qd@hotmail.com";
            }

            //建立MailMessage物件
            MailMessage mms = new MailMessage();
            //指定一位寄信人MailAddress
            mms.From = new MailAddress(MailFrom);
            //信件主旨
            mms.Subject = MailSub;
            //信件內容
            mms.Body = MailBody;
            //信件內容 是否採用Html格式
            mms.IsBodyHtml = isBodyHtml;

            if (MailTos != null)//防呆
            {
                for (int i = 0; i < MailTos.Length; i++)
                {
                    //加入信件的收信人(們)address
                    if (!string.IsNullOrEmpty(MailTos[i].Trim()))
                    {
                        mms.To.Add(new MailAddress(MailTos[i].Trim()));
                    }
                }
            }//End if (MailTos !=null)//防呆

            if (Ccs != null) //防呆
            {
                for (int i = 0; i < Ccs.Length; i++)
                {
                    if (!string.IsNullOrEmpty(Ccs[i].Trim()))
                    {
                        //加入信件的副本(們)address
                        mms.CC.Add(new MailAddress(Ccs[i].Trim()));
                    }
                }
            }//End if (Ccs!=null) //防呆

            if (filePaths != null)//防呆
            {//有夾帶檔案
                for (int i = 0; i < filePaths.Length; i++)
                {
                    if (!string.IsNullOrEmpty(filePaths[i].Trim()))
                    {
                        Attachment file = new Attachment(filePaths[i].Trim());
                        //加入信件的夾帶檔案
                        mms.Attachments.Add(file);
                    }
                }
            }//End if (filePaths!=null)//防呆

            if (filePaths2 != null)
            {
                foreach (var item in filePaths2)
                {
                    Attachment file = new Attachment(item.Item1, item.Item2);
                    //加入信件的夾帶檔案
                    mms.Attachments.Add(file);
                }
            }


            using (SmtpClient client = new SmtpClient(smtpServer, smtpPort))//或公司、客戶的smtp_server
            {
                if (!string.IsNullOrEmpty(mailAccount) && !string.IsNullOrEmpty(mailPwd))//.config有帳密的話
                {
                    client.Credentials = new NetworkCredential(mailAccount, mailPwd);//寄信帳密
                }
                client.Send(mms);//寄出一封信
            }//end using 

            //釋放每個附件，才不會Lock住
            if (mms.Attachments != null && mms.Attachments.Count > 0)
            {
                for (int i = 0; i < mms.Attachments.Count; i++)
                {
                    mms.Attachments[i].Dispose();
                    //mms.Attachments[i] = null;
                }
            }

            //是否要刪除附檔
            if (deleteFileAttachment && filePaths != null && filePaths.Length > 0)
            {

                foreach (string filePath in filePaths)
                {
                    File.Delete(filePath.Trim());
                }

            }

            return true;//成功
        }
        catch (Exception ex)
        {
            Log("SendMail", null, ex.InnerException != null && !string.IsNullOrEmpty(ex.InnerException.Message) ? ex.InnerException.Message : ex.Message);
            return false;//寄失敗
        }
    }

    public static string RenderViewToString(ControllerContext controllerContext, string viewName, object model, ViewDataDictionary viewData = null, TempDataDictionary tempData = null)
    {
        if (viewData == null) viewData = new ViewDataDictionary();

        if (tempData == null) tempData = new TempDataDictionary();

        // assing model to the viewdata
        viewData.Model = model;

        using (var sw = new StringWriter())
        {
            // try to find the specified view
            ViewEngineResult viewResult = ViewEngines.Engines.FindPartialView(controllerContext, viewName);
            // create the associated context
            ViewContext viewContext = new ViewContext(controllerContext, viewResult.View, viewData, tempData, sw);
            // write the render view with the given context to the stringwriter
            viewResult.View.Render(viewContext, sw);

            viewResult.ViewEngine.ReleaseView(controllerContext, viewResult.View);
            return sw.GetStringBuilder().ToString();
        }
    }

    public static void Log(string tableName, object targetID, string actionName, HttpSessionStateBase session = null)
    {
        lock (ActionLog)
        {
            ActionLog.Create(new ActionLog()
            {
                AdminID = (int)get_session("AdminID", session, -1),
                AdminName = (string)get_session("AdminName", session, ""),
                TableName = !string.IsNullOrEmpty(tableName) ? tableName : "",
                TargetID = targetID != null ? targetID.ToString() : "",
                ActionName = !string.IsNullOrEmpty(actionName) ? actionName : "",
                CreateDate = DateTime.UtcNow
            });

            ActionLog.SaveChanges();
        }
    }

    public static void ErrorLog(Exception e, string actionName, string orderID = null)
    {
        Log("Orders", orderID, actionName);

        string filePath = System.Web.Hosting.HostingEnvironment.MapPath("~/ErrorLog");
        string fileName = DateTime.UtcNow.ToString("yyyy-MM-dd") + ".txt";

        FileStream File = new FileStream(Path.Combine(filePath, fileName), FileMode.Append, FileAccess.Write);
        using (StreamWriter writer = new StreamWriter(File))
        {
            writer.WriteLine("ActionName : " + actionName);
            writer.WriteLine("Message : " + e.Message.Trim() + Environment.NewLine + "StackTrace : " + e.StackTrace.Trim() + Environment.NewLine + "Date : " + DateTime.Now.ToString());
            if (e.InnerException != null) writer.WriteLine("Inner Message : " + e.InnerException.ToString());
            writer.WriteLine(Environment.NewLine + "-----------------------------------------------------------------------------" + Environment.NewLine);
        }
    }

    public static object get_session(string col, HttpSessionStateBase session, object value = null)
    {
        if (session != null && session[col] != null) return session[col];

        if (HttpContext.Current != null && HttpContext.Current.Session[col] != null) return HttpContext.Current.Session[col];

        return value;
    }

    public static void CopyRow(ref HSSFSheet worksheet, int sourceRowNum, int destinationRowNum, bool IsCoverRow = false, bool IsRemoveSrcRow = false, bool copyRowHeight = true, bool resetOriginalRowHeight = true)
    {
        HSSFRow newRow = worksheet.GetRow(destinationRowNum) as HSSFRow;
        HSSFRow sourceRow = worksheet.GetRow(sourceRowNum) as HSSFRow;
        HSSFCell oldCell, newCell;
        int i;

        if (newRow == null)
            newRow = worksheet.CreateRow(destinationRowNum) as HSSFRow;
        else
        {
            if (!IsCoverRow)
                ShiftRows(ref worksheet, destinationRowNum, worksheet.LastRowNum, 1);
        }

        // Loop through source columns to add to new row
        for (i = 0; i < sourceRow.LastCellNum; i++)
        {
            // Grab a copy of the old/new cell
            oldCell = sourceRow.GetCell(i) as HSSFCell;
            newCell = newRow.GetCell(i) as HSSFCell;

            if (newCell == null)
                newCell = newRow.CreateCell(i) as HSSFCell;

            // If the old cell is null jump to next cell
            if (oldCell == null)
            {
                newCell = null;
                continue;
            }

            // Copy style from old cell and apply to new cell
            newCell.CellStyle = oldCell.CellStyle;

            // If there is a cell comment, copy
            if (newCell.CellComment != null) newCell.CellComment = oldCell.CellComment;

            // If there is a cell hyperlink, copy
            if (oldCell.Hyperlink != null) newCell.Hyperlink = oldCell.Hyperlink;

            // Set the cell data value
            switch (oldCell.CellType)
            {
                case CellType.Blank:
                    newCell.SetCellValue(oldCell.StringCellValue);
                    break;
                case CellType.Boolean:
                    newCell.SetCellValue(oldCell.BooleanCellValue);
                    break;
                case CellType.Error:
                    newCell.SetCellErrorValue(oldCell.ErrorCellValue);
                    break;
                case CellType.Formula:
                    newCell.CellFormula = oldCell.CellFormula;
                    break;
                case CellType.Numeric:
                    newCell.SetCellValue(oldCell.NumericCellValue);
                    break;
                case CellType.String:
                    newCell.SetCellValue(oldCell.RichStringCellValue);
                    break;
                case CellType.Unknown:
                    newCell.SetCellValue(oldCell.StringCellValue);
                    break;
            }
        }

        // If there are are any merged regions in the source row, copy to new row
        List<NPOI.SS.Util.CellRangeAddress> cellRangeList = new List<NPOI.SS.Util.CellRangeAddress>();
        NPOI.SS.Util.CellRangeAddress cellRangeAddress = null, newCellRangeAddress = null;
        for (i = 0; i < worksheet.NumMergedRegions; i++)
        {
            cellRangeAddress = worksheet.GetMergedRegion(i);

            if (cellRangeAddress.FirstRow == sourceRow.RowNum)
                cellRangeList.Add(cellRangeAddress);
        }

        if (cellRangeList.Any())
        {
            foreach (var cellRange in cellRangeList)
            {
                newCellRangeAddress = new NPOI.SS.Util.CellRangeAddress(newRow.RowNum, (newRow.RowNum + (cellRange.LastRow - cellRange.FirstRow)), cellRange.FirstColumn, cellRange.LastColumn);
                worksheet.AddMergedRegion(newCellRangeAddress);
            }
        }

        //複製行高到新列
        if (copyRowHeight)
            newRow.Height = sourceRow.Height;
        //重製原始列行高
        if (resetOriginalRowHeight)
            sourceRow.Height = worksheet.DefaultRowHeight;
        //清掉原列
        if (IsRemoveSrcRow == true)
        {
            worksheet.RemoveRow(sourceRow);

            bool next = true;
            do
            {
                next = false;
                for (i = 0; i < worksheet.NumMergedRegions; i++)
                {
                    if (worksheet.GetMergedRegion(i).FirstRow == sourceRow.RowNum)
                    {
                        worksheet.RemoveMergedRegion(i);
                        next = true;
                    }
                }
            } while (next);
        }
    }

    public static void CopyRow(ref XSSFSheet worksheet, int sourceRowNum, int destinationRowNum, bool IsCoverRow = false, bool IsRemoveSrcRow = false, bool copyRowHeight = true, bool resetOriginalRowHeight = true)
    {
        XSSFRow newRow = worksheet.GetRow(destinationRowNum) as XSSFRow;
        XSSFRow sourceRow = worksheet.GetRow(sourceRowNum) as XSSFRow;
        XSSFCell oldCell, newCell;
        int i;

        if (newRow == null)
            newRow = worksheet.CreateRow(destinationRowNum) as XSSFRow;
        else
        {
            if (!IsCoverRow)
                ShiftRows(ref worksheet, destinationRowNum, worksheet.LastRowNum, 1);
        }

        // Loop through source columns to add to new row
        for (i = 0; i < sourceRow.LastCellNum; i++)
        {
            // Grab a copy of the old/new cell
            oldCell = sourceRow.GetCell(i) as XSSFCell;
            newCell = newRow.GetCell(i) as XSSFCell;

            if (newCell == null)
                newCell = newRow.CreateCell(i) as XSSFCell;

            // If the old cell is null jump to next cell
            if (oldCell == null)
            {
                newCell = null;
                continue;
            }

            // Copy style from old cell and apply to new cell
            newCell.CellStyle = oldCell.CellStyle;

            // If there is a cell comment, copy
            if (newCell.CellComment != null) newCell.CellComment = oldCell.CellComment;

            // If there is a cell hyperlink, copy
            if (oldCell.Hyperlink != null) newCell.Hyperlink = oldCell.Hyperlink;

            // Set the cell data value
            switch (oldCell.CellType)
            {
                case CellType.Blank:
                    newCell.SetCellValue(oldCell.StringCellValue);
                    break;
                case CellType.Boolean:
                    newCell.SetCellValue(oldCell.BooleanCellValue);
                    break;
                case CellType.Error:
                    newCell.SetCellErrorValue(oldCell.ErrorCellValue);
                    break;
                case CellType.Formula:
                    newCell.CellFormula = oldCell.CellFormula;
                    break;
                case CellType.Numeric:
                    newCell.SetCellValue(oldCell.NumericCellValue);
                    break;
                case CellType.String:
                    newCell.SetCellValue(oldCell.RichStringCellValue);
                    break;
                case CellType.Unknown:
                    newCell.SetCellValue(oldCell.StringCellValue);
                    break;
            }
        }

        // If there are are any merged regions in the source row, copy to new row
        List<NPOI.SS.Util.CellRangeAddress> cellRangeList = new List<NPOI.SS.Util.CellRangeAddress>();
        NPOI.SS.Util.CellRangeAddress cellRangeAddress = null, newCellRangeAddress = null;
        for (i = 0; i < worksheet.NumMergedRegions; i++)
        {
            cellRangeAddress = worksheet.GetMergedRegion(i);

            if (cellRangeAddress.FirstRow == sourceRow.RowNum)
                cellRangeList.Add(cellRangeAddress);
        }

        if (cellRangeList.Any())
        {
            foreach (var cellRange in cellRangeList)
            {
                newCellRangeAddress = new NPOI.SS.Util.CellRangeAddress(newRow.RowNum, (newRow.RowNum + (cellRange.LastRow - cellRange.FirstRow)), cellRange.FirstColumn, cellRange.LastColumn);
                worksheet.AddMergedRegion(newCellRangeAddress);
            }
        }

        //複製行高到新列
        if (copyRowHeight)
            newRow.Height = sourceRow.Height;
        //重製原始列行高
        if (resetOriginalRowHeight)
            sourceRow.Height = worksheet.DefaultRowHeight;
        //清掉原列
        if (IsRemoveSrcRow == true)
        {
            worksheet.RemoveRow(sourceRow);

            bool next = true;
            do
            {
                next = false;
                for (i = 0; i < worksheet.NumMergedRegions; i++)
                {
                    if (worksheet.GetMergedRegion(i).FirstRow == sourceRow.RowNum)
                    {
                        worksheet.RemoveMergedRegion(i);
                        next = true;
                    }
                }
            } while (next);
        }
    }

    public static void ShiftRows(ref HSSFSheet sheet, int startrow, int endrow, int n, bool copyRowHeight = true, bool resetOriginalRowHeight = true)
    {
        if (n == 0) return;

        int i;

        if (n > 0)
        {
            for (i = endrow; i >= startrow; i--)
                if (sheet.GetRow(i) != null)
                    CopyRow(ref sheet, i, i + n, true, true, copyRowHeight, resetOriginalRowHeight);
        }
        else
        {
            for (i = startrow; i <= endrow; i++)
                if (sheet.GetRow(i) != null)
                    CopyRow(ref sheet, i, i + n, true, true, copyRowHeight, resetOriginalRowHeight);
        }
    }

    public static void ShiftRows(ref XSSFSheet sheet, int startrow, int endrow, int n, bool copyRowHeight = true, bool resetOriginalRowHeight = true)
    {
        if (n == 0) return;

        int i;

        if (n > 0)
        {
            for (i = endrow; i >= startrow; i--)
                if (sheet.GetRow(i) != null)
                    CopyRow(ref sheet, i, i + n, true, true, copyRowHeight, resetOriginalRowHeight);
        }
        else
        {
            for (i = startrow; i <= endrow; i++)
                if (sheet.GetRow(i) != null)
                    CopyRow(ref sheet, i, i + n, true, true, copyRowHeight, resetOriginalRowHeight);
        }
    }

    public static DateTime SkipWeekend(DateTime checkDate, int skipDaysOfSat = 2, int skipDaysOfSun = 1)
    {
        if (checkDate.DayOfWeek == DayOfWeek.Saturday) checkDate = checkDate.AddDays(skipDaysOfSat);
        if (checkDate.DayOfWeek == DayOfWeek.Sunday) checkDate = checkDate.AddDays(skipDaysOfSun);

        return checkDate;
    }

    public static IQueryable<TSource> SetOrder<TSource, TKey>(this IQueryable<TSource> source, System.Linq.Expressions.Expression<Func<TSource, TKey>> keySelector, string dir)
    {
        if (dir == "asc")
            return source.OrderBy(keySelector);
        else
            return source.OrderByDescending(keySelector);
    }
}

public class Country
{
    private RegionInfo info;

    public string ID { get { return info.TwoLetterISORegionName; } }
    public string Name { get { return info.EnglishName; } }
    public string ChtName { get { return info.DisplayName; } }
    public string TwoCode { get { return info.TwoLetterISORegionName; } }
    public string ThreeCode { get { return info.ThreeLetterISORegionName; } }

    public Country(int LCID)
    {
        info = new RegionInfo(LCID);
    }

    public Country(string Name)
    {
        info = new RegionInfo(Name);
    }

    public string OriginName
    {
        get
        {
            switch (ID)
            {
                case "CN":
                    return "China";
                case "TW":
                    return "Taiwan";
                case "US":
                    return "USA";
                default:
                    return Name;
            }
        }
    }
}

public class TimeZoneConvert
{
    private TimeZoneInfo TimeZoneInfo;

    public DateTime Utc { get; private set; }
    public double TimeStamp { get { return (Utc.Subtract(new DateTime(1970, 1, 1, 0, 0, 0))).TotalSeconds; } }

    private Dictionary<string, EnumData.TimeZone> TimeZoneList = new Dictionary<string, EnumData.TimeZone>()
    { { "USD", EnumData.TimeZone.PST }, { "GBP", EnumData.TimeZone.GMT }, { "AUD", EnumData.TimeZone.AEST }, { "JPY", EnumData.TimeZone.JST } };

    private Dictionary<EnumData.TimeZone, string> TimeZoneId = new Dictionary<EnumData.TimeZone, string>()
    {
        { EnumData.TimeZone.EST, "Eastern Standard Time" }, { EnumData.TimeZone.TST, "Taipei Standard Time" }, { EnumData.TimeZone.PST, "Pacific Standard Time" },
        { EnumData.TimeZone.GMT, "Greenwich Mean Time" }, { EnumData.TimeZone.AEST, "AUS Eastern Standard Time" }, { EnumData.TimeZone.JST, "Tokyo Standard Time" },
        { EnumData.TimeZone.UTC, "UTC" }
    };

    public TimeZoneConvert()
    {
        InitDateTime(DateTime.UtcNow);
    }

    public TimeZoneConvert(DateTime originDateTime, EnumData.TimeZone originTimeZone)
    {
        InitDateTime(originDateTime, originTimeZone);
    }

    public TimeZoneConvert InitDateTime(DateTime originDateTime, EnumData.TimeZone originTimeZone = EnumData.TimeZone.UTC)
    {
        originDateTime = DateTime.SpecifyKind(originDateTime, DateTimeKind.Unspecified);
        string timeZoneId = GetTimeZoneId(originTimeZone);
        TimeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        Utc = TimeZoneInfo.ConvertTimeToUtc(originDateTime, TimeZoneInfo);

        return this;
    }

    public DateTime ConvertDateTime(EnumData.TimeZone targetTimeZone)
    {
        string timeZoneId = GetTimeZoneId(targetTimeZone);
        TimeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return TimeZoneInfo.ConvertTime(Utc, TimeZoneInfo);
    }

    private string GetTimeZoneId(EnumData.TimeZone timeZone)
    {
        return TimeZoneId[timeZone];
    }

    private EnumData.TimeZone ConvertCurrencyToTimeZone(string currency)
    {
        return TimeZoneList[currency];
    }

    public string DateTimeToString(DateTime? dateTime, EnumData.TimeZone orginTimeZone = EnumData.TimeZone.UTC, EnumData.TimeZone timeZone = EnumData.TimeZone.UTC)
    {
        string dateTimeString = "";
        if (dateTime.HasValue && !dateTime.Equals(DateTime.MinValue))
        {
            return InitDateTime(dateTime.Value, orginTimeZone).ConvertDateTime(timeZone).ToString("MM/dd/yyyy<br />hh:mm tt");

        }
        return dateTimeString;
    }
}