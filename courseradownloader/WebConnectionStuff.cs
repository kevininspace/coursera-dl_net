using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;

namespace courseradownloader
{
    public class WebConnectionStuff
    {
        // how long to try to open a URL before timing out
        static int TIMEOUT = 30;

        private static CookieContainer cookiejar;


        public WebConnectionStuff()
        {
            //_courseraDownloader = courseraDownloader;
        }

        private string GetHeaders(string url, string headerName)
        {
            Dictionary<string, string> headers = GetHeaders(url);
            string headerValue;
            headers.TryGetValue(headerName, out headerValue);
            return headerValue;

        }


        /// <summary>
        /// Get the headers
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static Dictionary<string, string> GetHeaders(string url)
        {
            HttpWebResponse r = GetResponse(url, stream: true);
            WebHeaderCollection headerCollection = r.Headers;

            Dictionary<string, string> headers = headerCollection.AllKeys.ToDictionary(key => key, key => headerCollection[key]);

            r.Close();
            return headers;

        }


        //TODO: Get rid of this.
        /// <summary>
        /// Get the response
        /// </summary>
        /// <param name="url"></param>
        public static HttpWebResponse GetResponse(string url, int retries = 3, bool stream = false, Dictionary<string, string> headers = null)
        {
            HttpWebResponse httpWebResponse = null;
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    httpWebResponse = GetHttpWebResponse(url, headers);
                    if (httpWebResponse == null || httpWebResponse.StatusCode == HttpStatusCode.NotFound)
                    {
                        throw new Exception();
                    }
                    return httpWebResponse;

                }
                catch (Exception)
                {
                    Console.WriteLine(string.Format("Warning: Retrying to connect url: {0}", url));
                }
            }
            return httpWebResponse;

        }

        public int Timeout
        {
            set { TIMEOUT = value; }
            get { return TIMEOUT; }
        }

        //TODO: Get rid of this
        public static HttpWebResponse GetHttpWebResponse(string url, Dictionary<string, string> headers = null, string method = "GET", Cookie cookie = null, bool allowRedirect = true)
        //, CookieContainer cookiejar)
        {

            /* WHEN I GET IN TROUBLE, RUN THIS
            HttpWebRequest myHttpWebRequest2 = (HttpWebRequest)WebRequest.Create(url);
            myHttpWebRequest2.Connection = null;
            // Assign the response object of 'HttpWebRequest' to a 'HttpWebResponse' variable.
            HttpWebResponse myHttpWebResponse2 = (HttpWebResponse)myHttpWebRequest2.GetResponse();
            // Release the resources held by response object.
            myHttpWebResponse2.Close();
             */

            HttpWebRequest postRequest = (HttpWebRequest)WebRequest.Create(url);
            postRequest.Timeout = TIMEOUT * 10000000;
            postRequest.ContentType = "application/x-www-form-urlencoded";
            //postRequest.ContentLength = requestData.Length; //65

            postRequest.Referer = "https://www.coursera.org"; //webResponse.ResponseUri.ToString(); //?

            if (headers != null)
            {
                foreach (KeyValuePair<string, string> keyValuePair in headers)
                {
                    postRequest.Headers.Add(keyValuePair.Key, keyValuePair.Value);
                }
            }

            //Deal with cookies
            if (cookie != null)
            {
                cookiejar.Add(cookie);
            }

            postRequest.CookieContainer = cookiejar;

            postRequest.Method = method;

            postRequest.AllowAutoRedirect = allowRedirect;

            postRequest.ServicePoint.Expect100Continue = true;

            HttpWebResponse postResponse = (HttpWebResponse)postRequest.GetResponse();

            return postResponse;
        }

        private static CookieCollection IterateOverCookies(HttpWebResponse response)
        {
            string setCookie = response.Headers.Get("Set-Cookie");
            string[] strings = setCookie.Split(';');
            string name = strings.FirstOrDefault(s => s.StartsWith("name"));
            string value = strings.FirstOrDefault(s => s.StartsWith("name"));
            string path = strings.FirstOrDefault(s => s.ToLower().StartsWith("path="));
            IEnumerable<bool> enumerable = strings.Select(s => s.StartsWith("Do"));
            string domain = strings.FirstOrDefault(s => s.ToLower().StartsWith("domain="));
            cookiejar.Add(new Cookie());

            //=====
            //Cookie cookie2 = response.Cookies[0];
            //string s = response.Headers.Get("Set-Cookie");
            string[] allCookies = response.Headers.AllKeys; //.Get("cookie");
            foreach (string allCookie in allCookies)
            {
                string s1 = response.Headers.Get(allCookie);
            }
            //CookieCollection collection = cookieContainer.GetCookies(new Uri("http://www.futurelearn.com"));
            //Cookie cookie2 = collection[0];

            CookieContainer tempContainer = new CookieContainer();
            foreach (Cookie cookie1 in response.Cookies)
            {
                tempContainer.Add(cookie1);
            }
            CookieCollection cookieCollection = new CookieCollection(); //testcontainer.GetCookies(new Uri("http://www.futurelearn.com"));
            Hashtable table = (Hashtable)tempContainer.GetType().InvokeMember("m_domainTable",
                BindingFlags.NonPublic |
                BindingFlags.GetField |
                BindingFlags.Instance,
                null,
                tempContainer,
                new object[] { });


            foreach (var key in table.Keys)
            {
                foreach (Cookie cook in tempContainer.GetCookies(new Uri(string.Format("https://{0}/", key.ToString().Trim('.')))))
                {
                    cookieCollection.Add(cook);
                    Console.WriteLine("Name = {0} ; Value = {1} ; Domain = {2}", cook.Name, cook.Value, cook.Domain);
                }
            }
            //=====
            return cookieCollection;
        }

        /// <summary>
        /// Login into coursera and obtain the necessary session cookies
        /// </summary>
        /// <param name="courseName"></param>
        /// <param name="url"></param>
        /// <param name="postData"></param>
        public void Login(string classURL, string loginUrl, string postData)
        {
            //HttpWebResponse postResponse = GetHttpWebResponse(loginUrl + postData, method: "POST", cookie: cookies); //, cookiejar);
        }

        internal void SetLoginCookie(string loginUrl, string postData, Dictionary<string, string> newHeader, Cookie crsfCookie, Uri cookieAssociatedUri)
        {
            HttpWebResponse postResponse = GetHttpWebResponse(loginUrl + postData, method: "POST", headers: newHeader,
                                                              cookie: crsfCookie); //, cookiejar);
            if (postResponse.StatusCode == HttpStatusCode.Unauthorized)
            {
                postResponse.Close();
                throw new Exception("Invalid username or password");
            }

            // check if we managed to login
            CookieCollection loginCookieCollection = cookiejar.GetCookies(cookieAssociatedUri);
            crsfCookie = loginCookieCollection["CAUTH"];
            if (crsfCookie == null)
            {
                Console.WriteLine(string.Format("Failed to authenticate using {0}", postData));
                postResponse.Close();
                throw new Exception(string.Format("Failed to authenticate using {0}", postData));
            }
            postResponse.Close();
        }

        internal Cookie GetCookieToken(string loginUrl, string cookieToken)
        {
            //string url = courseraDownloader.lecture_url_from_name(courseName);
            cookiejar = new CookieContainer();
            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(loginUrl);
            webRequest.CookieContainer = cookiejar;
            webRequest.Timeout = Timeout * 1000;
            //webRequest.Proxy = new WebProxy(Proxy);

            HttpWebResponse webResponse = null;
            Cookie cookie = null;
            try
            {
                webResponse = (HttpWebResponse)webRequest.GetResponse();

                //Check 404
                if (webResponse.StatusCode == HttpStatusCode.NotFound)
                {
                    webResponse.Close();
                    //TODO: Need better exception message
                    throw new Exception(string.Format("Unknown class {0}", loginUrl));
                }

                webResponse.Close();

                CookieCollection cookieCollection = cookiejar.GetCookies(new Uri(loginUrl));
                cookie = cookieCollection[cookieToken];

                if (cookie == null)
                {
                    throw new Exception("Failed to find csrf cookie");
                }
            }
            catch (WebException e)
            {
                //TODO: What is this doing here?
                if (e.Status == WebExceptionStatus.ProtocolError)
                {
                    webResponse = (HttpWebResponse)e.Response;
                }
            }
            return cookie;
        }

        //public static CookieCollection GetCookies(CookieCollection cookies)
        //{
        //    ////cookiejar = new CookieContainer();
        //    //////HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(loginUrl);
        //    //////webRequest.CookieContainer = cookiejar;
        //    //////webRequest.Timeout = Timeout * 1000;
        //    //////webRequest.Proxy = new WebProxy(Proxy);

        //    //////HttpWebResponse webResponse = null;
        //    ////Cookie cookie = null;
        //    //////try
        //    //////{
        //    //////    webResponse = (HttpWebResponse)webRequest.GetResponse();

        //    //////    //Check 404
        //    //////    if (webResponse.StatusCode == HttpStatusCode.NotFound)
        //    //////    {
        //    //////        webResponse.Close();
        //    //////        //TODO: Need better exception message
        //    //////        throw new Exception(string.Format("Unknown class {0}", loginUrl));
        //    //////    }

        //    //////    webResponse.Close();

        //    //////    CookieCollection cookieCollection = cookiejar.GetCookies(new Uri(loginUrl));
        //    //////    cookie = cookieCollection[cookieToken];

        //    //////    if (cookie == null)
        //    //////    {
        //    //////        throw new Exception("Failed to find csrf cookie");
        //    //////    }
        //    //////}
        //    //////catch (WebException e)
        //    //////{
        //    //////    //TODO: What is this doing here?
        //    //////    if (e.Status == WebExceptionStatus.ProtocolError)
        //    //////    {
        //    //////        webResponse = (HttpWebResponse)e.Response;
        //    //////    }
        //    //////}
        //    ////return cookie;
        //    return null;
        //}

        public void MakeHttpWebCall(string url, Dictionary<string, string> headers = null, string method = "GET", CookieContainer cookies = null, bool allowRedirect = true, byte[] bytes = null)
        {
            HttpWebRequest postRequest = (HttpWebRequest)WebRequest.Create(url);
            postRequest.Timeout = TIMEOUT * 10000000;
            postRequest.ContentType = "application/x-www-form-urlencoded";
            //postRequest.ContentLength = requestData.Length; //65

            Uri referer = new Uri(url);
            string refererString = referer.AbsoluteUri; //.GetComponents(UriComponents.Host, UriFormat.Unescaped);
            postRequest.Referer = refererString; //webResponse.ResponseUri.ToString(); //?

            if (headers != null)
            {
                foreach (KeyValuePair<string, string> keyValuePair in headers)
                {
                    postRequest.Headers.Add(keyValuePair.Key, keyValuePair.Value);
                }
            }

            //Deal with cookies
            CookieContainer localCookieJar = new CookieContainer();
            if (cookies != null)
            {
                localCookieJar = cookies;
            }
            postRequest.CookieContainer = localCookieJar;

            postRequest.Method = method;
            postRequest.AllowAutoRedirect = allowRedirect;
            postRequest.ServicePoint.Expect100Continue = true;

            postRequest.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8";
            postRequest.Referer = "https://www.futurelearn.com/sign-in";
            postRequest.UserAgent =
                "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/35.0.1916.153 Safari/537.36";

            HttpWebResponse postResponse;
            if (method == "POST")
            {
                postRequest.ContentLength = bytes.Length;
                Stream requestStream = postRequest.GetRequestStream();
                requestStream.Write(bytes, 0, bytes.Length);
                postResponse = (HttpWebResponse)postRequest.GetResponse();
                requestStream.Close();

            }
            else
            {
                postResponse = (HttpWebResponse)postRequest.GetResponse();
            }

            CookieCollection allCookiesFromHeader = GetAllCookiesFromHeader(postResponse.Headers.Get("Set-Cookie"), null);
            localCookieJar.Add(allCookiesFromHeader);
            CookieJar = localCookieJar;
            //CookieCollection iterateOverCookies = IterateOverCookies(postResponse);

            //foreach (Cookie responseCookie in iterateOverCookies)
            //{
            //    localCookieJar.Add(responseCookie);
            //}

            PostResponse = postResponse;
        }

        public CookieContainer CookieJar { get; set; }
        public HttpWebResponse PostResponse { get; set; }

        private static void FindDomainCookies(WebHeaderCollection headers)
        {
            for (int i = 0; i < headers.Count; i++)
            {
                if ("Set-Cookie" == headers.Keys[i])
                {
                    string rawCookie = headers[i];
                    
                    if (rawCookie.Contains(","))
                    {
                        //regexp for Date format per RFC http://www.w3.org/Protocols/rfc2109/rfc2109 Wdy, DD-Mon-YY HH:MM:SS GMT

                        string dateRegExp = @"(?<day>expires=[A-Z,a-z]{3}),(?<date>\s\d{2}-[A-Z,a-z]{3}-\d{4}\s\d{2}:\d{2}:\d{2}\sgmt)";
                        
                        string replaceDateExp = @"${day}${date}";

                        rawCookie = Regex.Replace(rawCookie, dateRegExp, replaceDateExp, RegexOptions.IgnoreCase);
                    }
                    
                    string[] multipleCookies = rawCookie.Split(new char[] { ',' });
                    
                    for (int j = 0; j < multipleCookies.Length; j++)
                    {
                        Cookie cookie = new Cookie();
                        
                        string[] cookieValues = multipleCookies[j].Split(new char[] { ';' });
                        string[] paramNameVale;
                        
                        foreach (string param in cookieValues)
                        {
                            paramNameVale = param.Trim().Split(new char[] { '=' });
                            paramNameVale[0] = paramNameVale[0].ToLower();

                            if (paramNameVale[0] == "domain")
                            {
                                cookie.Domain = param.Split(new char[] {'='})[1];
                            }
                                
                            else if (paramNameVale[0] == "expires")
                            {
                                string date = paramNameVale[1];
                                
                                //Date format per RFC http://www.w3.org/Protocols/rfc2109/rfc2109 Wdy, DD-Mon-YY HH:MM:SS GMT
                                date = Regex.Replace(date, @"(?<day>(sun mon tue wed thu fri sat))", @"${day},", RegexOptions.IgnoreCase);
                                
                                cookie.Expires = Convert.ToDateTime(date);
                            }

                            else if (paramNameVale[0] == "path")
                            {
                                cookie.Path = paramNameVale[1];
                            }
                        }

                        cookieValues[0] = cookieValues[0].Trim();
                        cookie.Name = cookieValues[0].Split(new char[] { '=' })[0];
                        cookie.Value = cookieValues[0].Split(new char[] { '=' })[1];
                        
                        cookiejar.Add(cookie);
                        //if (cookie.Domain.ToLower().Contains("live"))
                        //    liveCookies.Add(cookie);
                        //else if (cookie.Domain.ToLower().Contains("msn"))
                        //    msnCookies.Add(cookie);
                    }
                }
            }
        }

        /// <summary>
        /// http://snipplr.com/view.php?codeview&id=4427
        /// </summary>
        /// <param name="strHeader"></param>
        /// <param name="strHost"></param>
        /// <returns></returns>
        public static CookieCollection GetAllCookiesFromHeader(string strHeader, string strHost)
        {
            ArrayList al = new ArrayList();
            CookieCollection cc = new CookieCollection();
            if (strHeader != string.Empty)
            {
                al = ConvertCookieHeaderToArrayList(strHeader);
                cc = ConvertCookieArraysToCookieCollection(al, strHost);
            }
            return cc;
        }

        /// <summary>
        /// http://snipplr.com/view.php?codeview&id=4427
        /// </summary>
        /// <param name="strCookHeader"></param>
        /// <returns></returns>
        private static ArrayList ConvertCookieHeaderToArrayList(string strCookHeader)
        {
            strCookHeader = strCookHeader.Replace("\r", "");
            strCookHeader = strCookHeader.Replace("\n", "");
            string[] strCookTemp = strCookHeader.Split(',');
            ArrayList al = new ArrayList();
            int i = 0;
            int n = strCookTemp.Length;
            while (i < n)
            {
                if (strCookTemp[i].IndexOf("expires=", StringComparison.OrdinalIgnoreCase) > 0)
                {
                    al.Add(strCookTemp[i] + "," + strCookTemp[i + 1]);
                    i = i + 1;
                }
                else
                {
                    al.Add(strCookTemp[i]);
                }
                i = i + 1;
            }
            return al;
        }

        /// <summary>
        /// http://snipplr.com/view.php?codeview&id=4427
        /// </summary>
        /// <param name="al"></param>
        /// <param name="strHost"></param>
        /// <returns></returns>
        private static CookieCollection ConvertCookieArraysToCookieCollection(ArrayList al, string strHost)
        {
            CookieCollection cc = new CookieCollection();

            int alcount = al.Count;
            string strEachCook;
            string[] strEachCookParts;
            for (int i = 0; i < alcount; i++)
            {
                strEachCook = al[i].ToString();
                strEachCookParts = strEachCook.Split(';');
                int intEachCookPartsCount = strEachCookParts.Length;
                string strCNameAndCValue = string.Empty;
                string strPNameAndPValue = string.Empty;
                string strDNameAndDValue = string.Empty;
                string[] NameValuePairTemp;
                Cookie cookTemp = new Cookie();

                for (int j = 0; j < intEachCookPartsCount; j++)
                {
                    if (j == 0)
                    {
                        strCNameAndCValue = strEachCookParts[j];
                        if (strCNameAndCValue != string.Empty)
                        {
                            int firstEqual = strCNameAndCValue.IndexOf("=");
                            string firstName = strCNameAndCValue.Substring(0, firstEqual);
                            string allValue = strCNameAndCValue.Substring(firstEqual + 1, strCNameAndCValue.Length - (firstEqual + 1));
                            cookTemp.Name = firstName;
                            cookTemp.Value = allValue;
                        }
                        continue;
                    }
                    if (strEachCookParts[j].IndexOf("path", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        strPNameAndPValue = strEachCookParts[j];
                        if (strPNameAndPValue != string.Empty)
                        {
                            NameValuePairTemp = strPNameAndPValue.Split('=');
                            if (NameValuePairTemp[1] != string.Empty)
                            {
                                cookTemp.Path = NameValuePairTemp[1];
                            }
                            else
                            {
                                cookTemp.Path = "/";
                            }
                        }
                        continue;
                    }

                    if (strEachCookParts[j].IndexOf("domain", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        strPNameAndPValue = strEachCookParts[j];
                        if (strPNameAndPValue != string.Empty)
                        {
                            NameValuePairTemp = strPNameAndPValue.Split('=');

                            if (NameValuePairTemp[1] != string.Empty)
                            {
                                cookTemp.Domain = NameValuePairTemp[1];
                            }
                            else
                            {
                                cookTemp.Domain = strHost;
                            }
                        }
                        continue;
                    }

                    if (strEachCookParts[j].IndexOf("expires", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        strPNameAndPValue = strEachCookParts[j];
                        if (strPNameAndPValue != string.Empty)
                        {
                            NameValuePairTemp = strPNameAndPValue.Split('=');

                            if (NameValuePairTemp[1] != string.Empty)
                            {
                                DateTime expiry;
                                //DateTime.ParseExact("24-okt-08 21:09:06 CEST".Replace("CEST", "+2"), "dd-MMM-yy HH:mm:ss z", culture);
                                bool tryParse = DateTime.TryParse(NameValuePairTemp[1].Replace("CEST", "+2"), out expiry);
                                cookTemp.Expires = expiry;
                            }
                            else
                            {
                                cookTemp.Domain = strHost;
                            }
                        }
                        continue;
                    }
                }

                if (cookTemp.Path == string.Empty)
                {
                    cookTemp.Path = "/";
                }
                if (cookTemp.Domain == string.Empty)
                {
                    cookTemp.Domain = strHost;
                }
                cc.Add(cookTemp);
            }
            return cc;
        }
    }
}