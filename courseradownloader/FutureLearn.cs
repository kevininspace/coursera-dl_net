﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using HtmlAgilityPack;

namespace courseradownloader
{
    class FutureLearn : MOOC
    {
        string LOGIN_URL = "https://www.futurelearn.com/sign-in";
        private string AUTH_URL;
        private string QUIZ_URL;
        private string Username;
        private string Password;
        private string Parser;
        private object Session;
        private string Proxy;
        private bool Gzip_courses;
        private WebConnectionStuff _webConnectionStuff;
        public CookieAwareWebClient _client;

        public FutureLearn(string username, string password, string proxy, string parser, string ignorefiles, int mppl, bool gzipCourses, string wkfilter)
        {
            AUTH_URL = BASE_URL + "/auth/auth_redirector?type=login&subtype=normal";
            QUIZ_URL = BASE_URL + "/quiz/index";

            Username = username;
            Password = password;
            Parser = parser;

            // Split "ignorefiles" argument on commas, strip, remove prefixing dot
            // if there is one, and filter out empty tokens.
            char[] charsToTrim = { '.', ' ' };
            if (ignorefiles != null) Ignorefiles = ignorefiles.Split(',').Select(s => s.Trim(charsToTrim));

            Session = null;
            Proxy = proxy;
            Max_path_part_len = mppl;
            Gzip_courses = gzipCourses;
            try
            {
                if (string.IsNullOrEmpty(wkfilter))
                {
                    Wk_filter = null;
                }
                else
                {
                    Wk_filter = wkfilter.Split(',');
                }
                _webConnectionStuff = new WebConnectionStuff();
            }
            catch (Exception e)
            {
                Console.WriteLine("Invalid week filter, should be a comma separated list of integers.");
                Console.WriteLine(e.Message);
            }

            Courses = new List<Course>();
        }

        public List<Course> Courses { get; set; }

        protected string[] Wk_filter { get; set; }

        protected IEnumerable<string> Ignorefiles { get; set; }

        protected override string BASE_URL
        {
            get { return "https://www.futurelearn.com/"; }
        }

        public override string HOME_URL
        {
            get { return BASE_URL + "courses/{0}"; }
        }

        protected override string LECTURE_URL
        {
            get { return HOME_URL + "/todo"; }
        }

        public override string LectureUrlFromName(string courseName)
        {
            //https://www.futurelearn.com/courses/cancer-and-the-genomic-revolution/todo/150
            string lecutreUrl = string.Format(LECTURE_URL, courseName);
            return lecutreUrl;
        }

        public override Course GetDownloadableContent(string courseName)
        {

            //get the lecture url
            string course_url = LectureUrlFromName(courseName);

            Course courseContent = new Course(courseName);
            Console.WriteLine("* Collecting downloadable content from " + course_url);

            //get the course name, and redirect to the course lecture page
            //string vidpage = get_page(course_url);
            string vidpage = _client.DownloadString(course_url);

            HtmlDocument htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(vidpage);

            // ParseErrors is an ArrayList containing any errors from the Load statement
            if (htmlDoc.ParseErrors != null && htmlDoc.ParseErrors.Any())
            {
                // Handle any parse errors as required
            }
            else
            {
                if (htmlDoc.DocumentNode != null)
                {
                    //# extract the weekly classes
                    HtmlNodeCollection weeks = htmlDoc.DocumentNode.SelectNodes("//li[contains(concat(' ', @class, ' '), ' todonav_item week ')]"); //"[@class='course-item-list-header']");

                    if (weeks != null)
                    {
                        // for each weekly class, go to the page and find the actual content there.
                        int i = 0;
                        foreach (HtmlNode week in weeks)
                        {
                            HtmlNode a = week.SelectSingleNode("a");

                            string weekLink = a.Attributes["href"].Value; //.InnerText.Trim();
                            string weekPage = _client.DownloadString(BASE_URL + weekLink);

                            HtmlDocument weekDoc = new HtmlDocument();
                            weekDoc.LoadHtml(weekPage);

                            HtmlNode h3txt = weekDoc.DocumentNode.SelectSingleNode("//h3[contains(concat(' ', @class, ' '), ' headline ')]");
                            string weekTopic = util.sanitise_filename(h3txt.InnerText.Trim());
                            weekTopic = TrimPathPart(weekTopic);
                            
                            Week weeklyContent = new Week(weekTopic);
                            weeklyContent.WeekNum = i++;

                            HtmlNodeCollection weekSteps = weekDoc.DocumentNode.SelectNodes("//li[contains(concat(' ', @class, ' '), ' step ')]");
                            foreach (HtmlNode weekStep in weekSteps)
                            {
                                Dictionary<string, string> resourceLinks = new Dictionary<string, string>();

                                HtmlNode weekStepAnchor = weekStep.SelectSingleNode("a");

                                string stepNumber = weekStepAnchor.SelectSingleNode("span/div").InnerText;
                                string stepName = weekStepAnchor.SelectSingleNode("div/div/h5").InnerText;
                                string stepType = weekStepAnchor.SelectSingleNode("div/div/span").InnerText;
                                string weekNumber = stepNumber.Trim().Split('.')[0].PadLeft(2, '0');
                                string videoNumber = stepNumber.Trim().Split('.')[1].PadLeft(2, '0');

                                stepName.RemoveColon();
                                stepName = util.sanitise_filename(stepName);
                                stepName = TrimPathPart(stepName);

                                string classname = string.Join("-", weekNumber, videoNumber, stepName);

                                string weekStepAnchorHref = weekStepAnchor.Attributes["href"].Value;




                                if (stepType == "video")
                                {
                                    string weekStepVideoPage = _client.DownloadString(BASE_URL + weekStepAnchorHref);
                                    HtmlDocument weekStepVideoDoc = new HtmlDocument();
                                    weekStepVideoDoc.LoadHtml(weekStepVideoPage);
                                    HtmlNode videoObject = weekStepVideoDoc.DocumentNode.SelectSingleNode("//source");
                                        //"[contains(concat(' ', @name, ' '), ' flashvars ')]");
                                    string vidUrl = videoObject.Attributes["src"].Value;

                                    string fn = Path.ChangeExtension(classname, "mp4");
                                    resourceLinks.Add("http:" + vidUrl, fn);
                                }
                                else
                                {
                                    resourceLinks.Add(BASE_URL + weekStepAnchorHref, "index.html");
                                }

                                ClassSegment weekClasses = new ClassSegment(classname);
                                weekClasses.ClassNum = i++;
                                weekClasses.ResourceLinks = resourceLinks;

                                weeklyContent.ClassSegments.Add(weekClasses);

                            }

                            courseContent.Weeks.Add(weeklyContent);

                        }
                        return courseContent;
                    }
                }
            }
            return null;
        }

        public override void Login()
        {
            //_webConnectionStuff.MakeHttpWebCall(LOGIN_URL);
            //HttpWebResponse httpWebResponse = _webConnectionStuff.PostResponse;
            //CookieContainer cookieContainer = _webConnectionStuff.CookieJar;

            //string myStr;
            //using (StreamReader reader = new StreamReader(httpWebResponse.GetResponseStream()))
            //{
            //    StreamReader sw = new StreamReader(reader.BaseStream);
            //    myStr = sw.ReadToEnd();
            //    // The string is currently stored in the 
            //    // StreamWriters buffer. Flushing the stream will 
            //    // force the string into the MemoryStream.
            //    //sw.Flush();

            //    // If we dispose the StreamWriter now, it will close 
            //    // the BaseStream (which is our MemoryStream) which 
            //    // will prevent us from reading from our MemoryStream
            //    //DON'T DO THIS - sw.Dispose();

            //    // The StreamReader will read from the current 
            //    // position of the MemoryStream which is currently 
            //    // set at the end of the string we just wrote to it. 
            //    // We need to set the position to 0 in order to read 
            //    // from the beginning.
            //    //ms.Position = 0;
            //    //var sr = new StreamReader(ms);
            //    //myStr = sr.ReadToEnd();
            //    //Console.WriteLine(myStr);
            //}

            //string tempFileName = Path.GetTempFileName();


            ////Stream stringStream = new FileStream(tempFileName, FileMode.CreateNew);
            ////httpWebResponse.GetResponseStream().CopyTo(stringStream);
            ////stringStream.ToString();
            ////string loginPage = get_page(LOGIN_URL);
            //HtmlDocument htmlDoc = new HtmlDocument();
            //htmlDoc.LoadHtml(myStr);

            //string authenticity_token = "";
            //if (htmlDoc.DocumentNode != null)
            //{
            //    //get the authenticity token from the login page
            //    HtmlNode token = htmlDoc.DocumentNode.SelectNodes("//input[contains(concat(' ', @name, ' '), ' authenticity_token ')]").FirstOrDefault();
            //    authenticity_token = token.Attributes["value"].Value;
            //}

            ////utf8=%E2%9C%93&authenticity_token=Il6RCc2oj8ndCPqvhnzVSZjp4FDrQWR79FBONHhvyUU%3D&uv_login=&return=&email=kevin.bourque%40gmail.com&password=%21rUkUS65w%24&button=
            //// call the authenticator url
            //StringBuilder postData = new StringBuilder();
            //postData.Append("/utf8=%E2%9C%93&"); //UTF-8 checkmark
            //postData.Append("authenticity_token=" + HttpUtility.UrlEncode(authenticity_token) + "&");
            //postData.Append("uv_login=&return=&");
            //postData.Append("email=" + HttpUtility.UrlEncode(Username) + "&");
            //postData.Append("password=" + HttpUtility.UrlEncode(Password) + "&");
            //postData.Append("remember_me=1&button=");

            //UTF8Encoding utf8 = new UTF8Encoding();
            //byte[] bytes = utf8.GetBytes(postData.ToString());


            /*TEST
             */
            CookieContainer cookieJar = new CookieContainer();
            _client = new CookieAwareWebClient(cookieJar);
            _client.Referer = LOGIN_URL;

            // the website sets some cookie that is needed for login, and as well the 'authenticity_token' is always different
            string response = _client.DownloadString(LOGIN_URL);

            // parse the 'authenticity_token' and cookie is auto handled by the cookieContainer
            string token1 = Regex.Match(response, "authenticity_token.+?value=\"(.+?)\"").Groups[1].Value;
            StringBuilder postData1 = new StringBuilder();
            postData1.Append("/utf8=%E2%9C%93&"); //UTF-8 checkmark
            postData1.Append("authenticity_token=" + HttpUtility.UrlEncode(token1) + "&");
            postData1.Append("uv_login=&return=&");
            postData1.Append("email=" + HttpUtility.UrlEncode(Username) + "&");
            postData1.Append("password=" + HttpUtility.UrlEncode(Password) + "&");
            postData1.Append("remember_me=1&button=");
            //string postData1 = string.Format("utf8=%E2%9C%93&authenticity_token={0}&user%5Blogin%5D=USERNAME&user%5Bpassword%5D=PASSWORD&user%5Boffset%5D=5.5&user%5Bremember_me%5D=0&button=", token);


            //WebClient.UploadValues is equivalent of Http url-encode type post
            _client.Method = "POST";
            response = _client.UploadString("https://www.futurelearn.com/sign-in", postData1.ToString());

            //Now that we've logged in, set the Method back to "GET"
            _client.Method = "GET";

            //Now get the goods (cookies should be set!


            //i am getting invalid user/pass, but i am sure it will work fine with normal user/password


            /*END TEST
             */

            /*
             * FUTURELEARN RESPONSE
             * cache-control:no-cache
             * cf-ray:1447a9f1151c053a-YYZ
             * content-type:text/html; charset=utf-8
             * date:Fri, 04 Jul 2014 01:49:01 GMT
             * location:https://www.futurelearn.com/courses/cancer-and-the-genomic-revolution/todo
             * server:cloudflare-nginx
             * set-cookie:_future_learn_session=SzZWUS9VRmtSUU5FYjQwZ0RqWmF2cEk5STd4eGdtS0I3bjdYY1NyOEhYdUxFMUFZdmZZOEhvcEM4akJzK3RuWnZNak9RNHhkTURZcW1aeGNPTUNrV3BaTUZkQVlZc3Vja2FqamxDUmNJa2Y2bUFtaWF2R3pleUI4T3lOcjJXR1ZUVmk1OXNlZjl4ZFRDVVBTKzlQVW9kVlRLS3lDb1l6dloyV1RzUGRHM2tXZFlxSnhHdXc0TzB1QlNlRy84cXJkdldrMDZONUw0ejhvMFFHQlhVM3NvVzFBZS9tUk5LSXFyczVpMVpMbmhZZFBYR013NjNpdjRhUnNpT0hmZEhYSjM5dnpsRE9EQ05mTmZ6dG1VN0wzSHc9PS0taWxMWTNjejAxOGw0dnNNUXUrSzNQZz09--02c3904cebd1a27fa010c4569a04cb28c2ed15dd; path=/; secure; HttpOnly
             * set-cookie:session_last_active_at=BAhsKwcVCLZT--143cab77e1d41cea4983f60126a354c0b3a658a0; path=/; expires=Sun, 06 Jul 2014 01:49:09 -0000; secure; HttpOnly
             * status:302 Found
             * status:302 Found
             * version:HTTP/1.1
             * x-bypass:1
             * x-content-type-options:nosniff
             * x-frame-options:SAMEORIGIN
             * x-request-id:e0d8a468-86e9-4760-8103-b25fe7771815
             * x-runtime:0.115527
             * x-ua-compatible:IE=edge
             * x-xss-protection:1; mode=block
             */

            //Dictionary<string, string> newHeader = new Dictionary<string, string>
            //{
            //    {"set-cookie", csrfToken.Value}

            //};

            //Cookie crsfCookie = new Cookie("_csrf_token", csrfToken.Value, "/", ".futurelearn.com");
            //Cookie sessionCookie = new Cookie("_future_learn_session", session.Value, "/", ".futurelearn.com");

            //_webConnectionStuff.SetLoginCookie(LOGIN_URL, postData.ToString(), newHeader, csrfToken, new Uri("https://www.futurelearn.com"));


            //File.Delete(tempFileName);

            //_webConnectionStuff.MakeHttpWebCall(LOGIN_URL + postData, null, "POST", cookieContainer, true, bytes);
        }

        public override void Login(string s)
        {
            Login();
        }

        public override void Download(string courseName, string destDir, bool reverse, bool gzipCourses, Course courseContent)
        {
            FutureLearnDownloader cd = new FutureLearnDownloader(this);
            cd.DownloadCourse(courseName, destDir, reverse, gzipCourses, courseContent);
        }
    }
}
