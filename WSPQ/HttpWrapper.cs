using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Web;
using System.Web.Script.Serialization;
using System.Security.Cryptography;
using System.Configuration;

namespace WSPQ
{
    class HttpWrapper
    {
        private static int TIMEOUT = 1000;
        private JavaScriptSerializer jsonDecode;
        private string baseUrl;
        private string checkQuotaPath;
        private string updateQuotaPath;
        private string lastUser = "";
        private int lastPageCount = 0;
        private bool lastResult = false;

        public HttpWrapper()
        {
            jsonDecode = new JavaScriptSerializer();
            baseUrl = Properties.Settings.Default.printQuotaManagerUrl;
            
            if (baseUrl.LastIndexOf('/') == baseUrl.Length - 1)
                baseUrl = baseUrl.Substring(0, baseUrl.Length - 1);
            checkQuotaPath = Properties.Settings.Default.checkQuotaPath;
            updateQuotaPath = Properties.Settings.Default.updateQuotaPath;
        }

        public bool CanPrint(string user, int pageCount)
        {
            if (lastUser == user && lastPageCount == pageCount)
                return lastResult;
            
            string rawData = Get(String.Format("{0}/{1}?user={2}&pagecount={3}",
                baseUrl, checkQuotaPath, user, pageCount));
            JsonMessage json = null;

            if (rawData != null && rawData != "")
            {
                try
                {
                    json = jsonDecode.Deserialize<JsonMessage>(rawData);
                }
                catch (Exception) { }
            }

            bool result = true;
            if (json != null)
            {
                if (json.status == "success" && json.data.can_print == false)
                    result = false;
            }
            lastResult = result;
            return result;
        }

        public void HasPrinted(int id, string user, string printer, string document, int pageCount)
        {
            String checksum = ShortMD5Hash(id.ToString() + user + pageCount.ToString() + "__printSetLog");

            GetAndForget(String.Format("{0}/{1}?id={2}&user={3}&printer={4}&doc={5}&printedcount={6}&chk={7}",
                baseUrl, updateQuotaPath, id, user, HttpUtility.UrlEncode(printer), HttpUtility.UrlEncode(document), pageCount, checksum));
        }

        private static string ShortMD5Hash(string input)
        {
            MD5 checksum = MD5.Create();
            byte[]hash = checksum.ComputeHash(System.Text.Encoding.ASCII.GetBytes(input));
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("x2"));
            }
            return sb.ToString().Substring(1, 6);
        }

        private static string ToUTF8(string str)
        {
            return Encoding.UTF8.GetString(Encoding.Default.GetBytes(str));
        }

        public string Get(string url)
        {
            HttpWebRequest wReq = (HttpWebRequest)WebRequest.Create(url);
            wReq.Timeout = TIMEOUT;
            WebResponse wRes;
            try
            {
                wRes = wReq.GetResponse();
            }
            catch (Exception)
            {
                return "";
            }

            Stream dStr = wRes.GetResponseStream();
            StreamReader reader = new StreamReader(dStr);
            string res = reader.ReadToEnd();
            reader.Close();
            dStr.Close();
            wRes.Close();
            return res;
        }

        public void GetAndForget(string url)
        {
            HttpWebRequest wReq = (HttpWebRequest)WebRequest.Create(url);
            wReq.Timeout = TIMEOUT;
            try
            {
                wReq.GetResponse().Close();
            }
            catch (Exception) { }
        }
    }

    class JsonMessage
    {
        public string status;
        public string message;
        public JsonData data;
    }
    
    class JsonData
    {
        public bool can_print;
        public int quota;
        public int maxQuota;
        public bool hasQuota;
        public string frequency;
    }
}
