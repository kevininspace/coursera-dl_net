using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using HtmlAgilityPack;

namespace courseradownloader
{
    internal class CourseraDownloader
    {

        /*
        Class to download content (videos, lecture notes, ...) from coursera.org for
        use offline.

        https://github.com/dgorissen/coursera-dl

        :param username: username
        :param password: password
        :keyword proxy: http proxy, eg: foo.bar.com:1234
        :keyword parser: xml parser
        :keyword ignorefiles: comma separated list of file extensions to skip (e.g., "ppt,srt")
        */

        private static string BASE_URL = "https://class.coursera.org/{0}";
        string HOME_URL = BASE_URL + "/class/index";
        string LECTURE_URL = BASE_URL + "/lecture/index";
        string QUIZ_URL = BASE_URL + "/quiz/index";
        string AUTH_URL = BASE_URL + "/auth/auth_redirector?type=login&subtype=normal";
        string LOGIN_URL = "https://accounts.coursera.org/api/v1/login";
        string ABOUT_URL = "https://www.coursera.org/maestro/api/topic/information?topic-id={0}";

        // see
        // http://www.crummy.com/software/BeautifulSoup/bs4/doc/#installing-a-parser
        string DEFAULT_PARSER = "html.parser";

        // how long to try to open a URL before timing out
        int TIMEOUT = 30;
        private string Username;
        private string Password;
        private string Parser;
        private IEnumerable<string> Ignorefiles;
        private int Max_path_part_len;
        private string Gzip_courses;
        private string[] Wk_filter;
        private CookieContainer cookiejar;

        public CourseraDownloader(string username, string password, string proxy, string parser, string ignorefiles, int mppl, string gzipCourses, string wkfilter)
        {
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
            }
            catch (Exception e)
            {
                Console.WriteLine("Invalid week filter, should be a comma separated list of integers.");
                Console.WriteLine(e.Message);
            }
        }

        public string Proxy { get; set; }

        public object Session { get; set; }

        /// <summary>
        /// Download all the contents (quizzes, videos, lecture notes, ...) of the course to the given destination directory (defaults to .)
        /// </summary>
        /// <param name="courseName"></param>
        /// <param name="destDir"></param>
        /// <param name="reverse"></param>
        /// <param name="gzipCourses"></param>
        public void download_course(string cname, string destDir, bool reverse, string gzipCourses)
        {
            //get the lecture url
            string course_url = lecture_url_from_name(cname);
            Dictionary<string, Dictionary<string, List<string>>> weeklyTopics = get_downloadable_content(course_url);

            if (!weeklyTopics.Any())
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
                //TODO: Horrible
                IEnumerable<KeyValuePair<string, Dictionary<string, List<string>>>> reversedWeeklyTopics = weeklyTopics.Reverse();
                weeklyTopics.Clear();
                foreach (KeyValuePair<string, Dictionary<string, List<string>>> reversedWeeklyTopic in reversedWeeklyTopics)
                {
                    weeklyTopics.Add(reversedWeeklyTopic.Key, reversedWeeklyTopic.Value);
                }
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

            download(string.Format(HOME_URL, cname), target_dir: course_dir, target_fname: "index.html");

            /*
        self.download(self.HOME_URL %
                      cname, target_dir=course_dir, target_fname="index.html")
        self.download(course_url,
                      target_dir=course_dir, target_fname="lectures.html")
        try:
            self.download_about(cname, course_dir)
        except Exception as e:
            print_("Warning: failed to download about file", e)

        # now download the actual content (video's, lecture notes, ...)
        for j, (weeklyTopic, weekClasses) in enumerate(weeklyTopics, start=1):

            if self.wk_filter and j not in self.wk_filter:
                print_(" - skipping %s (idx = %s), as it is not in the week filter" %
                       (weeklyTopic, j))
                continue

            # add a numeric prefix to the week directory name to ensure
            # chronological ordering
            wkdirname = str(j).zfill(2) + " - " + weeklyTopic

            # ensure the week dir exists
            wkdir = path.join(course_dir, wkdirname)
            if not path.exists(wkdir):
                os.makedirs(wkdir)

            print_(" - " + weeklyTopic)

            for i, (className, classResources) in enumerate(weekClasses, start=1):

                # ensure chronological ordering
                clsdirname = str(i).zfill(2) + " - " + className

                # ensure the class dir exists
                clsdir = path.join(wkdir, clsdirname)

                if not path.exists(clsdir):
                    os.makedirs(clsdir)

                print_("  - Downloading resources for " + className)

                # download each resource
                for classResource, tfname in classResources:
                    try:
                        self.download(
                            classResource, target_dir=clsdir, target_fname=tfname)
                    except Exception as e:
                        print_("    - failed: ", classResource, e)

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

        /// <summary>
        /// Download the url to the given filename
        /// </summary>
        private void download(string url, string target_dir = ".", string target_fname = null)
        {
            //get the headers
            Dictionary<string, string> headers = get_headers(url);

            string clenString;
            headers.TryGetValue("Content-Length", out clenString);
            //get the content length (if present)
            //Will return 0 if can't get value

            int clen;
            int.TryParse(clenString, out clen);

            string fname;
            if (!headers.Any())
            {
                //fname = headers);
            }
            else if (!string.IsNullOrEmpty(util.filename_from_header(headers)))
            {
                fname = util.filename_from_header(headers);
            }
            else
            {
                fname = util.filename_from_url(url);
            }

            /*"""

        
        # build the absolute path we are going to write to
        fname = target_fname or filename_from_header(
            headers) or filename_from_url(url)

        # split off the extension
        basename, ext = path.splitext(fname)

        # ensure it respects mppl
        fname = self.trim_path_part(basename) + ext

        # check if we should skip it (remember to remove the leading .)
        if ext and ext[1:] in self.ignorefiles:
            print_('    - skipping "%s" (extension ignored)' % fname)
            return
        
        filepath = path.join(target_dir, fname)

        dl = True
        if path.exists(filepath):
            if clen > 0:
                fs = path.getsize(filepath)
                delta = math.fabs(clen - fs)

                # there are cases when a file was not completely downloaded or
                # something went wront that meant the file on disk is
                # unreadable. The file on disk my be smaller or larger (!) than
                # the reported content length in those cases.
                # Hence we overwrite the file if the reported content length is
                # different than what we have already by at least k bytes (arbitrary)

                # TODO this is still not foolproof as the fundamental problem is that the content length cannot be trusted
                # so this really needs to be avoided and replaced by something
                # else, eg., explicitly storing what downloaded correctly
                if delta > 10:
                    print_(
                        '    - "%s" seems corrupt, downloading again' % fname)
                else:
                    print_('    - "%s" already exists, skipping' % fname)
                    dl = False
            else:
                # missing or invalid content length
                # assume all is ok...
                dl = False
        else:
            # Detect renamed files
            existing, short = find_renamed(filepath, clen)
            if existing:
                print_('    - "%s" seems to be a copy of "%s", renaming existing file' %
                       (fname, short))
                os.rename(existing, filepath)
                dl = False

        try:
            if dl:
                print_('    - Downloading', fname)
                response = self.get_response(url, stream=True)
                full_size = clen
                done_size = 0
                slice_size = 524288  # 512KB buffer
                last_time = time.time()
                with open(filepath, 'wb') as f:
                    for data in response.iter_content(chunk_size=slice_size):
                        f.write(data)
                        try:
                            percent = int(float(done_size) / full_size * 100)
                        except:
                            percent = 0
                        try:
                            cur_time = time.time()
                            speed = float(slice_size) / float(
                                cur_time - last_time)
                            last_time = cur_time
                        except:
                            speed = 0
                        if speed < 1024:
                            speed_str = '{:.1f} B/s'.format(speed)
                        elif speed < 1048576:
                            speed_str = '{:.1f} KB/s'.format(speed / 1024)
                        else:
                            speed_str = '{:.1f} MB/s'.format(speed / 1048576)
                        status_str = 'status: {:2d}% {}'.format(
                            percent, speed_str)
                        sys.stdout.write(
                            status_str + ' ' * (25 - len(status_str)) + '\r')
                        sys.stdout.flush()
                        done_size += slice_size
                response.close()
                sys.stdout.write(' ' * 25 + '\r')
                sys.stdout.flush()
        except Exception as e:
            print_("Failed to download url %s to %s: %s" % (url, filepath, e))
             */
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
        private Dictionary<string, string> get_headers(string url)
        {
            HttpWebResponse r = get_response(url, stream: true);
            WebHeaderCollection headerCollection = r.Headers;

            Dictionary<string, string> headers = headerCollection.AllKeys.ToDictionary(key => key, key => headerCollection[key]);

            r.Close();
            return headers;

        }

        /// <summary>
        /// Given the video lecture URL of the course, return a list of all downloadable resources.
        /// </summary>
        /// <param name="course_url"></param>
        private Dictionary<string, Dictionary<string, List<string>>> get_downloadable_content(string course_url)
        {
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
                        //# for each weekly class
                        Dictionary<string, Dictionary<string, List<string>>> weeklyTopics = new Dictionary<string, Dictionary<string, List<string>>>();
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

                            //get all the classes for the week
                            HtmlNode ul = week.NextSibling;
                            HtmlNodeCollection lis = ul.SelectNodes("li");

                            //for each class (= lecture)
                            Dictionary<string, List<string>> weekClasses = new Dictionary<string, List<string>>();
                            foreach (HtmlNode li in lis)
                            {
                                List<string> resourceLinks = new List<string>();

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

                                        resourceLinks.Add(h);
                                    }
                                }

                                //check if the video is included in the resources, if not, try do download it directly
                                string containsMp4 = resourceLinks.FirstOrDefault(s => s.Contains(".mp4"));
                                if (string.IsNullOrEmpty(containsMp4))
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
                                            //                                            vurl = clean_url(vobj['src'])
                                            //# build the matching filename
                                            //fn = className + ".mp4"
                                            //resourceLinks.append((vurl, fn))
                                        }
                                        /*try:


                    except requests.exceptions.HTTPError as e:
                        # sometimes there is a lecture without a vidio (e.g.,
                        # genes-001) so this can happen.
                        print_(
                            " Warning: failed to open the direct video link %s: %s" % (lurl, e))
                                         */
                                    }
                                    catch (Exception e)
                                    {
                                        // sometimes there is a lecture without a vidio (e.g.,
                                        // genes-001) so this can happen.
                                        Console.WriteLine(string.Format(" Warning: failed to open the direct video link {0}: {1}", lurl, e));
                                    }
                                }
                                weekClasses.Add(className, resourceLinks);
                            }
                            weeklyTopics.Add(weekTopic, weekClasses);
                        }
                        return weeklyTopics;
                    }
                }
            }
            return null;
        }

        private void WcOnDownloadStringCompleted(object sender, DownloadStringCompletedEventArgs downloadStringCompletedEventArgs)
        {
            string result = downloadStringCompletedEventArgs.Result;
        }

        private string get_page(string courseUrl, Dictionary<string, string> headers = null)
        {
            HttpWebResponse r = get_response(url: courseUrl, headers: headers);
            Stream responseStream = r.GetResponseStream();
            //Encoding encoding = System.Text.Encoding.GetEncoding(r.ContentEncoding);
            StreamReader reader = new StreamReader(responseStream);
            string page = reader.ReadToEnd();
            reader.Close();
            responseStream.Close();
            r.Close();
            return page;
        }

        private string trim_path_part(string weekTopic)
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
        /// Get the response
        /// </summary>
        /// <param name="url"></param>
        private HttpWebResponse get_response(string url, int retries = 3, bool stream = false, Dictionary<string, string> headers = null)
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


        /// <summary>
        /// Login into coursera and obtain the necessary session cookies
        /// </summary>
        /// <param name="courseName"></param>
        public void login(string courseName)
        {
            string url = lecture_url_from_name(courseName);
            cookiejar = new CookieContainer();
            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(url);
            webRequest.CookieContainer = cookiejar;
            webRequest.Timeout = TIMEOUT * 1000;
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
            postData.Append("?email=" + HttpUtility.UrlEncode(Username) + "&");
            postData.Append("password=" + HttpUtility.UrlEncode(Password));
            //byte[] requestData = Encoding.ASCII.GetBytes(postData.ToString());

            Dictionary<string, string> newHeader = new Dictionary<string, string>
            {
                {"X-CSRFToken", cookie.Value}
            };
            //CookieContainer postCookies = new CookieContainer(); //use new cookiejar
            Cookie crsfCookie = new Cookie("csrftoken", cookie.Value, "/", ".coursera.org");

            HttpWebResponse postResponse = GetHttpWebResponse(LOGIN_URL + postData, method: "POST", headers: newHeader, cookie: crsfCookie); //, cookiejar);
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
                Console.WriteLine(string.Format("Failed to authenticate as {0}", Username));
                throw new Exception(string.Format("Failed to authenticate as {0}", Username));
            }
        }

        private HttpWebResponse GetHttpWebResponse(string url, Dictionary<string, string> headers = null, string method = "GET", Cookie cookie = null, bool allowRedirect = true)
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
        /// Given the name of a course, return the video lecture url
        /// </summary>
        /// <param name="courseName"></param>
        /// <returns></returns>
        private string lecture_url_from_name(string courseName)
        {
            return string.Format(LECTURE_URL, courseName);
        }
    }
}