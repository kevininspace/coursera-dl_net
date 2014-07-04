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
        /// <param name="url"></param>
        /// <param name="postData"></param>
        public void Login(string classURL, string loginUrl, string postData)
        {
            //string url = courseraDownloader.lecture_url_from_name(courseName);
            cookiejar = new CookieContainer();
            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(classURL);
            webRequest.CookieContainer = cookiejar;
            webRequest.Timeout = Timeout * 1000;
            //webRequest.Proxy = new WebProxy(Proxy);

            HttpWebResponse webResponse = null;
            Cookie cookie = null;
            Cookie cookie2 = null;
            try
            {
                webResponse = (HttpWebResponse)webRequest.GetResponse();

                //Check 404
                if (webResponse.StatusCode == HttpStatusCode.NotFound)
                {
                    webResponse.Close();
                    //TODO: Need better exception message
                    throw new Exception(string.Format("Unknown class {0}", classURL));
                }

                webResponse.Close();

                CookieCollection cookieCollection = cookiejar.GetCookies(new Uri(classURL));
                cookie = cookieCollection["csrf_token"];
                cookie2 = cookieCollection["_csrf_token"];
                if (cookie == null & cookie2 == null)
                {
                    throw new Exception("Failed to find csrf cookie");
                }

                //HACK: Take the non-null version
                cookie = cookie ?? cookie2;
            }
            catch (WebException e)
            {
                //TODO: What is this doing here?
                if (e.Status == WebExceptionStatus.ProtocolError)
                {
                    webResponse = (HttpWebResponse)e.Response;
                }
            }

            //byte[] requestData = Encoding.ASCII.GetBytes(postData.ToString());

            Dictionary<string, string> newHeader = new Dictionary<string, string>
            {
                {"X-CSRFToken", cookie.Value}
            };
            //CookieContainer postCookies = new CookieContainer(); //use new cookiejar
            Cookie crsfCookie = new Cookie("csrftoken", cookie.Value, "/", ".coursera.org");

            /*
             * cookie:__cfduid=d628b4437c2ce4f38d0e2f0d4855909961404230554509; _csrf_token=BAhJIjFJbDZSQ2Myb2o4bmRDUHF2aG56VlNaanA0RkRyUVdSNzlGQk9OSGh2eVVVPQY6BkVG--e06450ac38144c9316d376546e064c94cada5a84; __uvt=; _future_learn_session=S3I4QUZZNWFPS1hwdmNpVkRWeEZMaEtUbTNRcVErQnpNa3Y4Ni9MeUZOYUt6N3FTUUE0eEVWSVU3d3RRRitCUW5KY2pXRGFKYUNZRkxWUlk4ODJEVkxjYnFJOW9NVEFpZU8xQmk4ZG13d3U5S2FicXBjYitHQ2xvYlA1YTBwcnFDeE5sZTBNRWpmU2I1SnMzemxaeEpLS1JpQk5yczlJOWpDTHFlT2pxTTdReDlIbXNjNjNQS29YYkZnNUw0OFhEOXFxRDZKQ3pJRDdabk1zMWRPUCt0cVdzbzhPZC9jSVJMWHBibUtRLzZza1RCLzBmZ29tVVZ6NnptL2NkMDJyQ2xIQVcxZEVwNklmc0RNZTBFQUZmTEE9PS0tRkxJcjQxazkxd2xwQlRRSFZjWjZuZz09--5f3b46958f9b9199b6c28f2945a4d5581306b28a; _ga=GA1.2.1859077010.1404230559; uvts=1WgDdAwQaxVMkRdN
             */
            HttpWebResponse postResponse = GetHttpWebResponse(loginUrl + postData, method: "POST", headers: newHeader, cookie: crsfCookie); //, cookiejar);
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
                Console.WriteLine(string.Format("Failed to authenticate using {0}", postData));
                throw new Exception(string.Format("Failed to authenticate using {0}", postData));
            }

            /*
             * FUTURELEARN RESPONSE
             * cache-control:no-cache
cf-ray:1447a9f1151c053a-YYZ
content-type:text/html; charset=utf-8
date:Fri, 04 Jul 2014 01:49:01 GMT
location:https://www.futurelearn.com/courses/cancer-and-the-genomic-revolution/todo
server:cloudflare-nginx
set-cookie:_future_learn_session=SzZWUS9VRmtSUU5FYjQwZ0RqWmF2cEk5STd4eGdtS0I3bjdYY1NyOEhYdUxFMUFZdmZZOEhvcEM4akJzK3RuWnZNak9RNHhkTURZcW1aeGNPTUNrV3BaTUZkQVlZc3Vja2FqamxDUmNJa2Y2bUFtaWF2R3pleUI4T3lOcjJXR1ZUVmk1OXNlZjl4ZFRDVVBTKzlQVW9kVlRLS3lDb1l6dloyV1RzUGRHM2tXZFlxSnhHdXc0TzB1QlNlRy84cXJkdldrMDZONUw0ejhvMFFHQlhVM3NvVzFBZS9tUk5LSXFyczVpMVpMbmhZZFBYR013NjNpdjRhUnNpT0hmZEhYSjM5dnpsRE9EQ05mTmZ6dG1VN0wzSHc9PS0taWxMWTNjejAxOGw0dnNNUXUrSzNQZz09--02c3904cebd1a27fa010c4569a04cb28c2ed15dd; path=/; secure; HttpOnly
set-cookie:session_last_active_at=BAhsKwcVCLZT--143cab77e1d41cea4983f60126a354c0b3a658a0; path=/; expires=Sun, 06 Jul 2014 01:49:09 -0000; secure; HttpOnly
status:302 Found
status:302 Found
version:HTTP/1.1
x-bypass:1
x-content-type-options:nosniff
x-frame-options:SAMEORIGIN
x-request-id:e0d8a468-86e9-4760-8103-b25fe7771815
x-runtime:0.115527
x-ua-compatible:IE=edge
x-xss-protection:1; mode=block
             */
        }
    }
}