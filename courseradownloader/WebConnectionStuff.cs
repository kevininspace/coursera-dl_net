using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Web;

namespace courseradownloader
{
    internal class WebConnectionStuff
    {
        // how long to try to open a URL before timing out
        int TIMEOUT = 30;

        private CourseraDownloader _courseraDownloader;
        private CookieContainer cookiejar;

        public WebConnectionStuff(CourseraDownloader courseraDownloader)
        {
            _courseraDownloader = courseraDownloader;
        }

        private string get_headers(string url, string headerName)
        {
            Dictionary<string, string> headers = get_headers(url);
            string headerValue;
            headers.TryGetValue(headerName, out headerValue);
            return headerValue;

        }

        /// <summary>
        /// Get the headers
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public Dictionary<string, string> get_headers(string url)
        {
            HttpWebResponse r = GetResponse(url, stream: true);
            WebHeaderCollection headerCollection = r.Headers;

            Dictionary<string, string> headers = headerCollection.AllKeys.ToDictionary(key => key, key => headerCollection[key]);

            r.Close();
            return headers;

        }

        /// <summary>
        /// Get the response
        /// </summary>
        /// <param name="url"></param>
        public HttpWebResponse GetResponse(string url, int retries = 3, bool stream = false, Dictionary<string, string> headers = null)
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

        public HttpWebResponse GetHttpWebResponse(string url, Dictionary<string, string> headers = null, string method = "GET", Cookie cookie = null, bool allowRedirect = true)
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

        private static CookieCollection IterateOverCookies(HttpWebResponse postResponse, CookieContainer postCookies)
        {
            //=====
            //Cookie cookie2 = postResponse.Cookies[0];
            string s = postResponse.Headers.Get("Set-Cookie");
            CookieCollection collection = postCookies.GetCookies(new Uri("http://coursera.org"));
            Cookie cookie2 = collection[0];
            CookieContainer testContainer = new CookieContainer();
            foreach (Cookie cookie1 in postResponse.Cookies)
            {
                testContainer.Add(cookie1);
            }
            Hashtable table = (Hashtable)testContainer.GetType().InvokeMember("m_domainTable",
                BindingFlags.NonPublic |
                BindingFlags.GetField |
                BindingFlags.Instance,
                null,
                testContainer,
                new object[] { });


            foreach (var key in table.Keys)
            {
                foreach (
                    Cookie cook in
                        testContainer.GetCookies(new Uri(string.Format("https://{0}/", key.ToString().Trim('.')))))
                {
                    Console.WriteLine("Name = {0} ; Value = {1} ; Domain = {2}", cook.Name, cook.Value,
                        cook.Domain);
                }
            }
            //=====
            return collection;
        }

        /// <summary>
        /// Login into coursera and obtain the necessary session cookies
        /// </summary>
        /// <param name="courseName"></param>
        public void login(string courseName, CourseraDownloader courseraDownloader)
        {
            string url = courseraDownloader.lecture_url_from_name(courseName);
            cookiejar = new CookieContainer();
            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(url);
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
                    throw new Exception(string.Format("Unknown class {0}", courseName));
                }

                webResponse.Close();

                CookieCollection cookieCollection = cookiejar.GetCookies(new Uri(url));
                cookie = cookieCollection["csrf_token"];
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


            // call the authenticator url
            StringBuilder postData = new StringBuilder();
            postData.Append("?email=" + HttpUtility.UrlEncode(courseraDownloader.Username1) + "&");
            postData.Append("password=" + HttpUtility.UrlEncode(courseraDownloader.Password1));
            //byte[] requestData = Encoding.ASCII.GetBytes(postData.ToString());

            Dictionary<string, string> newHeader = new Dictionary<string, string>
            {
                {"X-CSRFToken", cookie.Value}
            };
            //CookieContainer postCookies = new CookieContainer(); //use new cookiejar
            Cookie crsfCookie = new Cookie("csrftoken", cookie.Value, "/", ".coursera.org");

            HttpWebResponse postResponse = GetHttpWebResponse(courseraDownloader.LoginUrl + postData, method: "POST", headers: newHeader, cookie: crsfCookie); //, cookiejar);
            if (postResponse.StatusCode == HttpStatusCode.Unauthorized)
            {
                postResponse.Close();
                throw new Exception("Invalid username or password");
            }

            // check if we managed to login
            CookieCollection loginCookieCollection = cookiejar.GetCookies(new Uri("https://class.coursera.org"));
            cookie = loginCookieCollection["CAUTH"];
            if (cookie == null)
            {
                Console.WriteLine(string.Format("Failed to authenticate as {0}", courseraDownloader.Username1));
                throw new Exception(string.Format("Failed to authenticate as {0}", courseraDownloader.Username1));
            }
        }
    }
}