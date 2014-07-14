using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;

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
            //=====
            //Cookie cookie2 = response.Cookies[0];
            string s = response.Headers.Get("Set-Cookie");
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

            CookieCollection iterateOverCookies = IterateOverCookies(postResponse);

            foreach (Cookie responseCookie in iterateOverCookies)
            {
                localCookieJar.Add(responseCookie);
            }



            CookieJar = localCookieJar;
            PostResponse = postResponse;
        }

        public CookieContainer CookieJar { get; set; }
        public HttpWebResponse PostResponse { get; set; }
    }

    public class CookieAwareWebClient : WebClient
    {
        public string Method;
        public CookieContainer CookieContainer { get; set; }
        public Uri Uri { get; set; }
        public string Referer { get; set; }

        public CookieAwareWebClient()
            : this(new CookieContainer())
        {
        }

        public CookieAwareWebClient(CookieContainer cookies)
        {
            this.CookieContainer = cookies;
        }

        protected override WebRequest GetWebRequest(Uri address)
        {
            WebRequest request = base.GetWebRequest(address);
            if (request is HttpWebRequest)
            {
                (request as HttpWebRequest).AllowAutoRedirect = true;
                (request as HttpWebRequest).CookieContainer = this.CookieContainer;
                (request as HttpWebRequest).ServicePoint.Expect100Continue = false;
                (request as HttpWebRequest).UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64; rv:18.0) Gecko/20100101 Firefox/18.0";
                (request as HttpWebRequest).Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
                (request as HttpWebRequest).Headers.Add(HttpRequestHeader.AcceptLanguage, "en-US,en;q=0.5");
                (request as HttpWebRequest).Referer = Referer;
                (request as HttpWebRequest).KeepAlive = true;
                (request as HttpWebRequest).AutomaticDecompression = DecompressionMethods.Deflate |
                                                                     DecompressionMethods.GZip;
                if (Method == "POST")
                {
                    (request as HttpWebRequest).ContentType = "application/x-www-form-urlencoded";
                }

            }

            if (request.GetType() == typeof(FileWebRequest))
            {
                FileWebRequest fileRequest = (FileWebRequest)request;
                return fileRequest;
            }
            else
            {
                HttpWebRequest httpRequest = (HttpWebRequest)request;
                httpRequest.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                return httpRequest;
            }
        }

        protected override WebResponse GetWebResponse(WebRequest request)
        {
            WebResponse response = base.GetWebResponse(request);

            String setCookieHeader = response.Headers[HttpResponseHeader.SetCookie];

            if (setCookieHeader != null)
            {
                //do something if needed to parse out the cookie.
                try
                {

                    Cookie cookie = new Cookie();
                    //create cookie
                    this.CookieContainer.Add(cookie);

                }
                catch (Exception)
                {

                }
            }
            return response;

        }
    }
}