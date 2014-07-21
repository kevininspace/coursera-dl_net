using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Reflection;

namespace courseradownloader
{
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