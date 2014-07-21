using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using SevenZip;
using YoutubeExtractor;

namespace courseradownloader.MOOCs
{
    class Edx : Mooc
    {
        private Dictionary<string, string> OPENEDX_SITES = new Dictionary<string, string>()
            {
                {"edx", "https://courses.edx.org"},
                {"stanford", "https://class.stanford.edu"},
                {"usyd-sit", "http://online.it.usyd.edu.au"}
            };

        string LOGIN_API;
        string DASHBOARD;
        string USER_AGENT;
        string COURSEWARE_SEL = "('nav', {'aria-label':'Course Navigation'})";

        private Dictionary<string, string> DEFAULT_USER_AGENTS = new Dictionary<string, string>()
            {
                {
                    "chrome", "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.31 (KHTML, like Gecko) Chrome/26.0.1410.63 Safari/537.31"
                },
                {
                    "firefox", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10.8; rv:24.0) Gecko/20100101 Firefox/24.0"
                },
                {
                    "edx", "edX-downloader/0.01"
                }
            };

        internal CookieAwareWebClient _client;
        private int YOUTUBE_VIDEO_ID_LENGTH = 11;


        public Edx(string username, string password, string proxy, string parser, string ignorefiles, int mppl, bool gzipCourses, string wkfilter)
        {
            USER_AGENT = DEFAULT_USER_AGENTS["chrome"];
            DASHBOARD = BASE_URL + "/dashboard";
            LOGIN_API = BASE_URL + "/login_ajax";

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
                //WebConnectionStuff = new WebConnectionStuff();
            }
            catch (Exception e)
            {
                Console.WriteLine("Invalid week filter, should be a comma separated list of integers.");
                Console.WriteLine(e.Message);
            }

            Courses = new List<Course>();
        }

        //protected WebConnectionStuff WebConnectionStuff { get; private set; }

        protected string[] Wk_filter { get; set; }

        protected bool Gzip_courses { get; set; }

        protected string Proxy { get; set; }

        protected object Session { get; set; }

        protected string Parser { get; set; }

        protected string Password { get; set; }

        protected string Username { get; set; }

        public List<Course> Courses { get; set; }

        protected override string BASE_URL
        {
            get { return OPENEDX_SITES["edx"]; }
        }

        public override string HOME_URL
        {
            //EDX_HOMEPAGE
            get { return BASE_URL + "/login_ajax"; }
        }

        protected override string LECTURE_URL
        {
            get { return BASE_URL + "/courses/{0}/courseware"; }
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
                    HtmlNodeCollection weeks = htmlDoc.DocumentNode.SelectNodes("//div[contains(concat(' ', @class, ' '), ' chapter ')]");

                    if (weeks != null)
                    {
                        Regex regexpSubs = new Regex("data-transcript-translation-url=(?:&#34;|\")([^\"&]*)(?:&#34;|\")");
                        Regex splitter = new Regex("data-streams=(?:&#34;|\").*1.0[0]*:");
                        Regex extra_youtube = new Regex("//w{0,3}.youtube.com/embed/([^ ?&]*)[?& ]");

                          
                        // for each weekly class, go to the page and find the actual content there.
                        int i = 1;
                        foreach (HtmlNode week in weeks)
                        {
                            Console.WriteLine();
                            Console.WriteLine("* Week " + i + " of " + weeks.Count);

                            //HtmlNode a = week.SelectSingleNode("a");

                            //string weekLink = a.Attributes["href"].Value; //.InnerText.Trim();
                            //string weekPage = _client.DownloadString(BASE_URL + weekLink);

                            //HtmlDocument weekDoc = new HtmlDocument();
                            //weekDoc.LoadHtml(weekPage);

                            //HtmlNode h3txt = weekDoc.DocumentNode.SelectSingleNode("//h3[contains(concat(' ', @class, ' '), ' headline ')]");
                            string h3txt = week.SelectSingleNode(".//h3/a").InnerText.Trim();
                            string weekTopic = Utilities.sanitise_filename(h3txt);
                            weekTopic = Utilities.TrimPathPart(weekTopic, Max_path_part_len);

                            Week weeklyContent = new Week(weekTopic);
                            weeklyContent.WeekNum = i++;

                            //HtmlNodeCollection weekSteps = weekDoc.DocumentNode.SelectNodes("//li[contains(concat(' ', @class, ' '), ' step ')]");
                            HtmlNodeCollection weekSteps = week.SelectNodes(".//ul//a");
                            int j = 1;
                            foreach (HtmlNode weekStep in weekSteps)
                            {
                                Utilities.DrawProgressBar(j, weekSteps.Count, 20, '=');

                                Dictionary<string, string> resourceLinks = new Dictionary<string, string>();

                                string weekStepAnchorHref = weekStep.Attributes["href"].Value;
                                
                                //string stepNumber = weekStepAnchor.SelectSingleNode("span/div").InnerText;
                                string stepName = weekStep.InnerText; // weekStepAnchor.SelectSingleNode("div/div/h5").InnerText;
                                //string stepType = weekStepAnchor.SelectSingleNode("div/div/span").InnerText;
                                string stepType = null;
                                string weekNumber = weeklyContent.WeekNum.ToString().PadLeft(2, '0');
                                string videoNumber = j.ToString().PadLeft(2, '0'); //stepNumber.Trim().Split('.')[1].PadLeft(2, '0');

                                stepName.RemoveColon();
                                stepName = Utilities.sanitise_filename(stepName);
                                stepName = Utilities.TrimPathPart(stepName, Max_path_part_len);

                                string classname = string.Join("-", weekNumber, videoNumber, stepName);

                                //string weekStepAnchorHref = weekStepAnchor.Attributes["href"].Value;

                                List<string> video_id = new List<string>();

                                //TODO: Downloading non-video content is hard. It's handled by JavaScript and changes the page content on-the-fly.
                                string weekStepPage = _client.DownloadString(BASE_URL + weekStepAnchorHref);
                                /*HtmlDocument weekDoc = new HtmlDocument();
                                  weekDoc.LoadHtml(weekStepPage);

                                  HtmlNodeCollection weekSectionContentTabs = weekDoc.DocumentNode.SelectNodes("//div[contains(concat(' ', @id, ' '), ' seq_contents')]");

                                  string test = weekSectionContentTabs.First().InnerText;
                                  string decoded = HttpUtility.HtmlDecode(test);
                                 */
                                MatchCollection matchCollection = splitter.Matches(weekStepPage);
                                foreach (Match match in matchCollection)
                                {
                                    video_id.Add(weekStepPage.Substring(match.Index + match.Length, YOUTUBE_VIDEO_ID_LENGTH));
                                }

                                /*Deal with Subtitles
                                 *         subsUrls += [BASE_URL + regexpSubs.search(container).group(2) + "?videoId=" + id + "&language=en"
                                 *         if regexpSubs.search(container) is not None else ''
                                 *         for id, container in zip(video_id[-len(id_container):], id_container)]
                                 */

                                //Find other YouTube videos embeded
                                MatchCollection collection = extra_youtube.Matches(weekStepPage);
                                foreach (Match match in collection)
                                {
                                    video_id.Add(weekStepPage.Substring(match.Index + match.Length, YOUTUBE_VIDEO_ID_LENGTH));
                                }

                                List<string> video_links = new List<string>();
                                if (video_id.Count < 1)
                                {
                                    //string id_container = splitter.Split(weekStepPage)[0];
                                    //if (string.Equals(weekStepPage, id_container, StringComparison.OrdinalIgnoreCase))
                                    //{
                                    //RegEx.Split will return the original string if nothing found, so if they are the same, there is no video
                                    stepType = "html";
                                }
                                else
                                {
                                    video_links = video_id.Select(v => "http://youtube.com/watch?v=" + v).ToList();
                                    stepType = "video";
                                }


                                if (stepType == "video")
                                {
                                    foreach (string videoLink in video_links)
                                    {
                                        resourceLinks.Add(videoLink, null);
                                    }

                                }
                                else
                                {
                                    //TODO: For now, we skip non-video content. Another day. :)
                                    resourceLinks.Add(BASE_URL + weekStepAnchorHref, Path.ChangeExtension(classname, "html")); // "index.html");
                                }

                                ClassSegment weekClasses = new ClassSegment(classname);
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

        public override bool Login()
        {
            CookieContainer cookieJar = new CookieContainer();
            _client = new CookieAwareWebClient(cookieJar);
            _client.Referer = HOME_URL;

            // the website sets some cookie that is needed for login
            string response = _client.DownloadString(HOME_URL);

            Cookie tokenCookie = cookieJar.List().FirstOrDefault(c => c.Name == "csrftoken");

            StringBuilder postData = new StringBuilder();
            postData.Append("email=" + HttpUtility.UrlEncode(Username) + "&");
            postData.Append("password=" + HttpUtility.UrlEncode(Password) + "&");
            postData.Append("remember=False=");

            // the csrf token is sent in the hader
            _client.Headers.Add("User-Agent", USER_AGENT);
            _client.Headers.Add("Accept", "application/json, text/javascript, */*; q=0.01");
            _client.Headers.Add("Content-Type", "application/x-www-form-urlencoded;charset=utf-8");
            _client.Headers.Add("Referer", HOME_URL);
            _client.Headers.Add("X-Requested-With", "XMLHttpRequest");
            _client.Headers.Add("X-CSRFToken", tokenCookie.Value);

            response = _client.UploadString(LOGIN_API, postData.ToString());
            JObject jObject = JObject.Parse(response);
            JToken jToken = jObject.GetValue("success");
            if (!jToken.Value<bool>())
            {
                //The query returned false, either not authenticated or forbiddent (403)
                Console.WriteLine("Wrong email or password logging into Edx.");
                return false;
            }

            //Now get the goods (cookies should be set!)

            return true;
        }

        public override void Login(string s)
        {
            throw new NotImplementedException();
        }

        public override void Download(string courseName, string destDir, bool reverse, bool gzipCourses, Course courseContent)
        {
            MakeCourseList(courseContent, Path.Combine(destDir, courseName));
            EdxDownloader edxd = new EdxDownloader(this);
            edxd.DownloadCourse(courseName, destDir, reverse, gzipCourses, courseContent);
        }
    }

    internal class EdxDownloader : Downloader, IDownloader
    {
        private Edx _edxCourse;

        public EdxDownloader(Edx edx)
        {
            _edxCourse = edx;
            Ignorefiles = _edxCourse.Ignorefiles as List<string>;
        }

        public void DownloadCourse(string courseName, string destDir, bool reverse, bool gzipCourses, Course courseContent)
        {
            if (!courseContent.Weeks.Any())
            {
                Console.WriteLine(" Warning: no downloadable content found for {0}, did you accept the honour code?", courseName);
            }
            else
            {
                Console.WriteLine(" * Got all downloadable content for {0} ", courseName);
            }

            if (reverse)
            {
                courseContent.Weeks.Reverse();
            }

            //where the course will be downloaded to
            string courseDir = Path.Combine(destDir, courseName);
            //if (!Directory.Exists(courseDir))
            //{
            //    Directory.CreateDirectory(courseDir);
            //}

            Console.WriteLine("* " + courseName + " will be downloaded to " + courseDir);
            //download the standard pages
            Console.WriteLine(" - Downloading lecture/syllabus pages");

            //Download(string.Format(_edxCourse.HOME_URL, courseName), courseDir, "index.html");
            //Download(string.Format(_edxCourse.LectureUrlFromName(courseName)), courseDir, "lectures.html");

            //now download the actual content (video's, lecture notes, ...)
            foreach (Week week in courseContent.Weeks)
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

                //Filter the text stuff only


                // add a numeric prefix to the week directory name to ensure chronological ordering

                string wkdirname = week.WeekNum.ToString().PadLeft(2, '0') + " - " + week.WeekName;

                //ensure the week dir exists
                Console.WriteLine(" - " + week.WeekName);
                string wkdir = Path.Combine(courseDir, wkdirname);
                Directory.CreateDirectory(wkdir);

                foreach (ClassSegment classSegment in week.ClassSegments)
                {
                    //ensure chronological ordering
                    string clsdirname = classSegment.ClassName;

                    //ensure the class dir exists
                    string clsdir = Path.Combine(wkdir, clsdirname);
                    Directory.CreateDirectory(clsdir);

                    Console.WriteLine(" - Downloading resources for " + clsdirname);

                    //download each resource
                    foreach (KeyValuePair<string, string> resourceLink in classSegment.ResourceLinks)
                    {
                        //Filter here


                        try
                        {
                            Download(resourceLink.Key, clsdir, resourceLink.Value);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("   - failed: {0}, {1}", resourceLink.Key, e.Message);
                            throw e;
                        }
                    }

                }
            }

            if (gzipCourses)
            {
                SevenZipCompressor zipCompressor = new SevenZipCompressor();
                zipCompressor.CompressDirectory(destDir, courseName + ".7z");

            }
        }

        public void Download(string link, string targetDir, string targetFname)
        {
            if (link.Contains("youtube"))
            {
                IEnumerable<VideoInfo> videoInfos = DownloadUrlResolver.GetDownloadUrls(link);
                VideoInfo video = videoInfos.Where(t => t.VideoType == VideoType.Mp4).First(r => r.Resolution == 360);

                /*
                 * Create the video downloader.
                 * The first argument is the video to download.
                 * The second argument is the path to save the video file.
                 */

                string filePathName = video.Title.RemoveColon() + video.VideoExtension;
                if (string.IsNullOrEmpty(filePathName))
                {
                    filePathName = Path.GetRandomFileName();
                }

                string fname = filePathName.RemoveColon();

                string filepath = Path.Combine(targetDir, fname);

                //ensure it respects mppl
                filepath = Utilities.TrimPathPart(filepath, _edxCourse.Max_path_part_len);

                VideoDownloader videoDownloader = new VideoDownloader(video, filepath);
                
                //WebHeaderCollection responseHeaders = _edxCourse._client.ResponseHeaders;
                int contentLength = videoDownloader.BytesToDownload ?? 0; // GetContentLength(responseHeaders);
                bool isFileNeeded = IsFileNeeded(filepath, contentLength, fname);

                if (isFileNeeded)
                {

                    // Register the ProgressChanged event and print the current progress
                    videoDownloader.DownloadProgressChanged +=
                        (sender, args) =>
                        Utilities.DrawProgressBar(Convert.ToInt32(args.ProgressPercentage), 100, 40, '=');

                    /*
                     * Execute the video downloader.
                     * For GUI applications note, that this method runs synchronously.
                     */
                    videoDownloader.Execute();
                }
            }
        }

        protected new bool IsFileNeeded(string filepath, int contentLength, string fname)
        {
            //split off the extension and check if we should skip it (remember to remove the leading .)
            string ext = Path.GetExtension(fname);

            if (!string.IsNullOrEmpty(ext) && Ignorefiles != null && Ignorefiles.Contains(ext))
            {
                Console.WriteLine("    - skipping \"{0}\" (extension ignored)", fname);
                return false;
            }

            //Next check if it already exists and is unchanged
            if (File.Exists(filepath))
            {
                
                    FileInfo fileInfo = new FileInfo(filepath);
                    long fs = fileInfo.Length;
                    long delta = Math.Abs(contentLength - fs);

                    // there are cases when a file was not completely downloaded or
                    // something went wront that meant the file on disk is
                    // unreadable. The file on disk my be smaller or larger (!) than
                    // the reported content length in those cases.
                    // Hence we overwrite the file if the reported content length is
                    // different than what we have already by at least k bytes (arbitrary)

                    //TODO: this is still not foolproof as the fundamental problem is that the content length cannot be trusted
                    // so this really needs to be avoided and replaced by something
                    // else, eg., explicitly storing what downloaded correctly

                    //if (delta > 10)
                    //{
                    //    Console.WriteLine("    - \"{0}\" seems corrupt, downloading again", fname);
                    //}
                    //else
                    //{
                        Console.WriteLine("    - \"{0}\" already exists, skipping", fname);
                        return false;
                    //}
                
            }

            //Detect renamed files
            string shortn;
            bool existing = FindRenamed(filepath, out shortn);

            if (existing)
            {
                Console.WriteLine("    - \"{0}\" seems to be a copy of \"{1}\", renaming existing file", fname, shortn);
                File.Move(shortn, filepath);
                return false;
            }

            return true;
        }

    }
}
