using System;
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
    class Coursera : Mooc
    {

        private string Username;
        private string Password;
        private readonly WebConnectionStuff _webConnectionStuff;
        private string Parser;
        public IEnumerable<string> Ignorefiles;
        private bool Gzip_courses;
        private string[] Wk_filter;

        string QUIZ_URL;
        string AUTH_URL;
        string ROOT_URL = "https://class.coursera.org/";
        string LOGIN_URL = "https://accounts.coursera.org/api/v1/login";
        public string ABOUT_URL = "https://www.coursera.org/maestro/api/topic/information?topic-id={0}";

        public Coursera(string username, string password, string proxy, string parser, string ignorefiles, int mppl, bool gzipCourses, string wkfilter)
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

        protected override string BASE_URL
        {
            get { return "https://class.coursera.org/{0}"; }
        }
        
        public override string HOME_URL
        {
            get { return BASE_URL + "/class/index"; }
        }
        protected override string LECTURE_URL
        {
            get { return BASE_URL + "/lecture/index"; }
        }

        public string Proxy { get; set; }

        public object Session { get; set; }

        public string Username1
        {
            set { Username = value; }
            get { return Username; }
        }

        public string Password1
        {
            set { Password = value; }
            get { return Password; }
        }

        public WebConnectionStuff WebConnectionStuff
        {
            get { return _webConnectionStuff; }
        }

        /// <summary>
        /// Given the video lecture URL of the course, return a list of all downloadable resources.
        /// </summary>
        public override Course GetDownloadableContent(string courseName)
        {
            //get the lecture url
            string course_url = LectureUrlFromName(courseName);

            Course courseContent = new Course(courseName);
            Console.WriteLine("* Collecting downloadable content from " + course_url);

            //get the course name, and redirect to the course lecture page
            string vidpage = get_page(course_url);

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
                    HtmlNodeCollection weeks = htmlDoc.DocumentNode.SelectNodes("//div[contains(concat(' ', @class, ' '), ' course-item-list-header ')]"); //"[@class='course-item-list-header']");

                    if (weeks != null)
                    {
                        // for each weekly class
                        int i = 0;
                        foreach (HtmlNode week in weeks)
                        {
                            Console.WriteLine();
                            Console.WriteLine("* Week " + i + " of " + weeks.Count);

                            HtmlNode h3 = week.SelectSingleNode("./h3");

                            // sometimes the first week are the hidden sample lectures, catch this
                            string h3txt;
                            if (h3.InnerText.Trim().StartsWith("window.onload"))
                            {
                                h3txt = "Sample Lectures";
                            }
                            else
                            {
                                h3txt = h3.InnerText.Trim();
                            }
                            string weekTopic = util.sanitise_filename(h3txt);
                            weekTopic = TrimPathPart(weekTopic);

                            Week weeklyContent = new Week(weekTopic);
                            weeklyContent.WeekNum = i++;

                            //get all the classes for the week
                            HtmlNode ul = week.NextSibling;
                            HtmlNodeCollection lis = ul.SelectNodes("li");

                            //for each class (= lecture)
                            int j = 0;
                            foreach (HtmlNode li in lis)
                            {
                                util.DrawProgressBar(j, lis.Count, 20, '=');

                                Dictionary<string, string> resourceLinks = new Dictionary<string, string>();

                                //the name of this class
                                string className = li.SelectSingleNode("a").InnerText.Trim();

                                className.RemoveColon();
                                className = util.sanitise_filename(className);
                                className = TrimPathPart(className);

                                //collect all the resources for this class (ppt, pdf, mov, ..)
                                HtmlNodeCollection classResources = li.SelectNodes("./div[contains(concat(' ', @class, ' '), ' course-lecture-item-resource ')]/a");
                                foreach (HtmlNode classResource in classResources)
                                {
                                    //get the hyperlink itself
                                    string h = util.clean_url(classResource.GetAttributeValue("href", ""));
                                    if (string.IsNullOrEmpty(h))
                                    {
                                        continue;
                                    }
                                    //Sometimes the raw, uncompresed source videos are available as
                                    //well. Don't download them as they are huge and available in
                                    //compressed form anyway.
                                    if (h.Contains("source_videos"))
                                    {
                                        Console.WriteLine("   - will skip raw source video " + h);
                                    }
                                    else
                                    {
                                        //Dont set a filename here, that will be inferred from the week titles
                                        resourceLinks.Add(h, className);
                                    }
                                }

                                //check if the video is included in the resources, if not, try do download it directly
                                bool containsMp4 = resourceLinks.Any(s => s.Key.Contains(".mp4"));
                                if (!containsMp4)
                                {
                                    HtmlNode ll = li.SelectSingleNode("./a[contains(concat(' ', @class, ' '), ' lecture-link ')]");
                                    string lurl = util.clean_url(ll.GetAttributeValue("data-modal-iframe", ""));
                                    try
                                    {
                                        //HttpWebResponse httpWebResponse = get_response(lurl);
                                        //string html = new WebClient().DownloadString(lurl);
                                        WebClient wc = new WebClient();
                                        wc.DownloadStringCompleted += WcOnDownloadStringCompleted;
                                        wc.DownloadStringAsync(new Uri(lurl));
                                        System.Threading.Thread.Sleep(3000);
                                        wc.CancelAsync();


                                        string page = get_page(lurl);
                                        HtmlDocument bb = new HtmlDocument();

                                        bb.LoadHtml(lurl);

                                        //string page = get_page(lurl);
                                        //HtmlWeb bb = new HtmlWeb();
                                        //HtmlDocument doc = bb.Load(lurl);
                                        HtmlNode selectSingleNode = bb.DocumentNode.SelectSingleNode("div"); //"[contains(concat(' ', @type, ' '), 'video/mp4')]");
                                        if (selectSingleNode.OuterHtml.Length < 1)
                                        {
                                            Console.WriteLine(string.Format(" Warning: Failed to find video for {0}", className));
                                        }
                                        else
                                        {
                                            string vurl = util.clean_url(selectSingleNode.SelectSingleNode("src").OuterHtml);

                                            //build the matching filename
                                            string fn = Path.ChangeExtension(className, "mp4");
                                            resourceLinks.Add(vurl, fn);
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        // sometimes there is a lecture without a vidio (e.g.,
                                        // genes-001) so this can happen.
                                        Console.WriteLine(string.Format(" Warning: failed to open the direct video link {0}: {1}", lurl, e));
                                    }
                                }
                                ClassSegment weekClasses = new ClassSegment(className);
                                weekClasses.ClassNum = j++;
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
            throw new NotImplementedException();
        }

        private void WcOnDownloadStringCompleted(object sender, DownloadStringCompletedEventArgs downloadStringCompletedEventArgs)
        {
            string result = downloadStringCompletedEventArgs.Result;
        }

        public override void Login(string s)
        {
            //Coursera requires a course name to get the initial cookie


            // call the authenticator url
            StringBuilder postData = new StringBuilder();
            postData.Append("?email=" + HttpUtility.UrlEncode(Username1) + "&");
            postData.Append("password=" + HttpUtility.UrlEncode(Password1) + "&");
            postData.Append("webrequest=true");
            Cookie cookieToken = _webConnectionStuff.GetCookieToken(LectureUrlFromName(s), "csrf_token");

            Dictionary<string, string> newHeader = new Dictionary<string, string>
            {
                {"X-CSRFToken", cookieToken.Value}
            };
            
            Cookie crsfCookie = new Cookie("csrftoken", cookieToken.Value, "/", ".coursera.org");

            _webConnectionStuff.SetLoginCookie(LOGIN_URL, postData.ToString(), newHeader, crsfCookie, new Uri("https://class.coursera.org"));

            _webConnectionStuff.Login(LectureUrlFromName(s), LOGIN_URL, postData.ToString());
        }

        public override void Download(string courseName, string destDir, bool b, bool gzipCourses, Course courseContent)
        {
            MakeCourseList(courseContent, Path.Combine(destDir, courseName));
            CourseraDownloader cd = new CourseraDownloader(this);
            cd.DownloadCourse(courseName, destDir, b, gzipCourses, courseContent);
            
        }

    }
}
