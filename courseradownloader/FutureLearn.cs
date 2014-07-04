using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
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
            get { throw new NotImplementedException(); }
        }

        public override Course GetDownloadableContent(string courseName)
        {
            //get the lecture url
            string course_url = lecture_url_from_name(courseName);

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
                                        resourceLinks.Add(h, string.Empty);
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

        private void WcOnDownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            throw new NotImplementedException();
        }

        public override void Login()
        {
            //Coursera requires a course name to get the initial cookie


            // call the authenticator url
            StringBuilder postData = new StringBuilder();
            postData.Append("?email=" + HttpUtility.UrlEncode(Username) + "&");
            postData.Append("password=" + HttpUtility.UrlEncode(Password));
            _webConnectionStuff.Login(LOGIN_URL, LOGIN_URL, postData.ToString());
        }

        public override void Login(string s)
        {

        }

        public override void Download(string courseName, string destDir, bool b, bool gzipCourses, Course courseContent)
        {
            FutureLearnDownloader cd = new FutureLearnDownloader(this);
            cd.DownloadCourse(courseName, destDir, b, gzipCourses, courseContent);
        }
    }

    internal class FutureLearnDownloader
    {
        public FutureLearnDownloader(FutureLearn futureLearn)
        {
            throw new NotImplementedException();
        }

        public void DownloadCourse(string courseName, string destDir, bool b, bool gzipCourses, Course courseContent)
        {
            throw new NotImplementedException();
        }
    }
}
