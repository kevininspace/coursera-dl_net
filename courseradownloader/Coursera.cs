using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using HtmlAgilityPack;

namespace courseradownloader
{
    class Coursera : MOOC
    {

        private string Username;
        private string Password;
        private readonly WebConnectionStuff _webConnectionStuff;
        private string Parser;
        private IEnumerable<string> Ignorefiles;
        private int Max_path_part_len;
        private bool Gzip_courses;
        private string[] Wk_filter;

        string QUIZ_URL;
        string AUTH_URL;
        string LOGIN_URL = "https://accounts.coursera.org/api/v1/login";
        string ABOUT_URL = "https://www.coursera.org/maestro/api/topic/information?topic-id={0}";

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
        }

        public List<Course> Courses { get; set; }

        protected override string BASE_URL
        {
            get { return "https://class.coursera.org/{0}"; }
        }
        protected override string HOME_URL
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

        protected virtual string trim_path_part(string weekTopic)
        {
            //TODO: simple hack, something more elaborate needed
            if (Max_path_part_len != 0 && weekTopic.Length > Max_path_part_len)
            {
                return weekTopic.Substring(0, Max_path_part_len);
            }
            else
            {
                return weekTopic;
            }
        }

        /// <summary>
        /// Given the video lecture URL of the course, return a list of all downloadable resources.
        /// </summary>
        /// <param name="course_url"></param>
        public override Course GetDownloadableContent(string cname)
        {
            //get the lecture url
            string course_url = lecture_url_from_name(cname);

            Course courseContent = new Course(cname);
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
                            weekTopic = trim_path_part(weekTopic);

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

                                //Many class names have the following format:
                                //"Something really cool (12:34)"
                                //If the class name has this format, replace the colon in the
                                //time with a hyphen.
                                if (Regex.IsMatch(className, @".+\(\d?\d:\d\d\)$"))
                                {
                                    className = className.Replace(":", "-");
                                }
                                className = util.sanitise_filename(className);
                                className = trim_path_part(className);

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

        private void WcOnDownloadStringCompleted(object sender, DownloadStringCompletedEventArgs downloadStringCompletedEventArgs)
        {
            string result = downloadStringCompletedEventArgs.Result;
        }

        public override void Login()
        {
            // call the authenticator url
            StringBuilder postData = new StringBuilder();
            postData.Append("?email=" + HttpUtility.UrlEncode(Username1) + "&");
            postData.Append("password=" + HttpUtility.UrlEncode(Password1));
            _webConnectionStuff.Login(LOGIN_URL, postData.ToString());
        }

        public override void Download()
        {
            CourseraDownloader cd = new CourseraDownloader();
            
        }
    }

    abstract class MOOC : IMooc
    {
        protected abstract string BASE_URL { get; }
        protected abstract string HOME_URL { get; }
        protected abstract string LECTURE_URL { get; }

        /// <summary>
        /// Given the name of a course, return the video lecture url
        /// </summary>
        /// <param name="courseName"></param>
        /// <returns></returns>
        public virtual string lecture_url_from_name(string courseName)
        {
            return string.Format(LECTURE_URL, courseName);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="courseUrl"></param>
        /// <param name="headers"></param>
        /// <returns></returns>
        public virtual string get_page(string courseUrl, Dictionary<string, string> headers = null)
        {
            HttpWebResponse r = WebConnectionStuff.GetResponse(url: courseUrl, headers: headers);
            Stream responseStream = r.GetResponseStream();
            //Encoding encoding = System.Text.Encoding.GetEncoding(r.ContentEncoding);
            StreamReader reader = new StreamReader(responseStream);
            string page = reader.ReadToEnd();
            reader.Close();
            responseStream.Close();
            r.Close();
            return page;
        }


        /// <summary>
        /// Download all the contents (quizzes, videos, lecture notes, ...) of the course to the given destination directory (defaults to .)
        /// </summary>
        /// <param name="courseName"></param>
        /// <param name="destDir"></param>
        /// <param name="reverse"></param>
        /// <param name="gzipCourses"></param>
        public virtual void download_course(string cname, string destDir, bool reverse, bool gzipCourses, Course weeklyTopics)
        {

            if (!weeklyTopics.Weeks.Any())
            {
                Console.WriteLine(
                    string.Format(" Warning: no downloadable content found for {0}, did you accept the honour code?",
                        cname));
            }
            else
            {
                Console.WriteLine(
                    string.Format(" * Got all downloadable content for {0} ", cname));
            }

            if (reverse)
            {
                weeklyTopics.Weeks.Reverse();
            }

            //where the course will be downloaded to
            string course_dir = Path.Combine(destDir, cname);
            if (!Directory.Exists(course_dir))
            {
                DirectoryInfo directoryInfo = Directory.CreateDirectory(course_dir);
            }

            Console.WriteLine("* " + cname + " will be downloaded to " + course_dir);
            //download the standard pages
            Console.WriteLine(" - Downloading lecture/syllabus pages");

            download(string.Format(HOME_URL, cname), course_dir, "index.html");
            download(string.Format(lecture_url_from_name(cname)), course_dir, "lectures.html");

            try
            {
                download_about(cname, course_dir);
            }
            catch (Exception e)
            {
                Console.WriteLine("Warning: failed to download about file: {0}", e.Message);
            }

            //now download the actual content (video's, lecture notes, ...)
            foreach (Week week in weeklyTopics.Weeks)
            {
                //TODO: filter
                /*if (Wk_filter && week.Key)
                {
                    
                }
                 * 
                 *             if self.wk_filter and j not in self.wk_filter:
                print_(" - skipping %s (idx = %s), as it is not in the week filter" %
                       (weeklyTopic, j))
                continue
                 */

                // add a numeric prefix to the week directory name to ensure
                // chronological ordering

                string wkdirname = week.WeekNum.ToString().PadLeft(2, '0') + " - " + week.WeekName;

                //ensure the week dir exists
                Console.WriteLine(" - " + week.WeekName);
                string wkdir = Path.Combine(course_dir, wkdirname);
                Directory.CreateDirectory(wkdir);

                foreach (ClassSegment classSegment in week.ClassSegments)
                {
                    //ensure chronological ordering
                    string clsdirname = classSegment.ClassNum.ToString().PadLeft(2, '0') + " - " + classSegment.ClassName;

                    //ensure the class dir exists
                    string clsdir = Path.Combine(wkdir, clsdirname);
                    Directory.CreateDirectory(clsdir);

                    Console.WriteLine(" - Downloading resources for " + classSegment.ClassName);

                    //download each resource
                    foreach (KeyValuePair<string, string> resourceLink in classSegment.ResourceLinks)
                    {
                        try
                        {
                            download(resourceLink.Key, clsdir, resourceLink.Value);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(string.Format("   - failed: {0}, {1}", resourceLink.Key, e.Message));
                            throw e;
                        }
                    }

                }
            }

            if (gzipCourses)
            {
                ZipFile.CreateFromDirectory(destDir, cname + ".zip");
            }
            /*

        if gzip_courses:
            tar_file_name = cname + ".tar.gz"
            print_("Compressing and storing as " + tar_file_name)
            tar = tarfile.open(os.path.join(dest_dir, tar_file_name), 'w:gz')
            tar.add(os.path.join(dest_dir, cname), arcname=cname)
            tar.close()
            print_("Compression complete. Cleaning up.")
            shutil.rmtree(os.path.join(dest_dir, cname))
             */
        }


        public abstract Course GetDownloadableContent(string cname);
        public abstract void Login();
        public abstract void Download();
    }

    internal interface IMooc
    {
        Course GetDownloadableContent(string cname);
        void Login();
    }
}
