using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Script.Serialization;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace courseradownloader
{
    internal class CourseraDownloader : IDownloader
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

        

        // see
        // http://www.crummy.com/software/BeautifulSoup/bs4/doc/#installing-a-parser
        string DEFAULT_PARSER = "html.parser";
        private Coursera _courseraCourse;


        public CourseraDownloader(Coursera coursera)
        {
            _courseraCourse = coursera;
        }


        /// <summary>
        /// Download the 'about' json file
        /// </summary>
        /// <param name="cname"></param>
        /// <param name="courseDir"></param>
        /// <param name="abouturl"></param>
        private void download_about(string cname, string courseDir, string abouturl)
        {
            string fn = Path.Combine(courseDir, cname) + "-about.json";

            //get the base course name (without the -00x suffix)
            string[] strings = Regex.Split(cname, "(-[0-9]+)");
            string base_name = strings[0];

            //get the json
            string about_url = string.Format(abouturl, base_name);
            JObject jObject = get_json(about_url);

            //pretty print to file
            List<string> jsonList = new List<string>();
            try
            {
                foreach (KeyValuePair<string, JToken> keyValuePair in jObject)
                {
                    jsonList.Add(keyValuePair.Key + "," + keyValuePair.Value.ToString());
                }

                File.WriteAllLines(fn, jsonList);
            }
            catch (Exception e)
            {
                throw e;
            }

        }

        /// <summary>
        /// Get the json data
        /// </summary>
        /// <param name="aboutUrl"></param>
        private JObject get_json(string url)
        {
            JObject jObject;
            using (HttpWebResponse response = WebConnectionStuff.GetResponse(url))
            {
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    string readToEnd = reader.ReadToEnd();
                    jObject = JObject.Parse(readToEnd);
                    reader.Close();
                }
                response.Close();
            }

            return jObject;
        }

        /// <summary>
        /// Download the url to the given filename
        /// </summary>
        public void download(string url, string target_dir = ".", string target_fname = null)
        {
            //get the headers
            Dictionary<string, string> headers = WebConnectionStuff.GetHeaders(url);

            //get the content length (if present)
            //Will return 0 if can't get value
            string clenString;
            headers.TryGetValue("Content-Length", out clenString);


            int clen;
            int.TryParse(clenString, out clen);

            //build the absolute path we are going to write to
            string fname;

            if (!string.IsNullOrEmpty(util.filename_from_header(headers)))
            {
                fname = util.filename_from_header(headers);
            }
            else
            {
                fname = util.filename_from_url(url);
            }

            //split off the extension
            string basename = Path.GetFileNameWithoutExtension(fname);
            string ext = Path.GetExtension(fname);

            //ensure it respects mppl
            fname = _courseraCourse.TrimPathPart(fname);

            //check if we should skip it (remember to remove the leading .)
            if (!string.IsNullOrEmpty(ext) && _courseraCourse.Ignorefiles != null && _courseraCourse.Ignorefiles.Contains(ext))
            {
                Console.WriteLine("    - skipping \"{0}\" (extension ignored)", fname);
                return;
            }

            string filepath = Path.Combine(target_dir, fname);

            bool dl = true;
            if (File.Exists(filepath))
            {
                if (clen > 0)
                {
                    FileInfo fileInfo = new FileInfo(filepath);
                    long fs = fileInfo.Length;
                    long delta = Math.Abs(clen - fs);

                    // there are cases when a file was not completely downloaded or
                    // something went wront that meant the file on disk is
                    // unreadable. The file on disk my be smaller or larger (!) than
                    // the reported content length in those cases.
                    // Hence we overwrite the file if the reported content length is
                    // different than what we have already by at least k bytes (arbitrary)

                    //TODO: this is still not foolproof as the fundamental problem is that the content length cannot be trusted
                    // so this really needs to be avoided and replaced by something
                    // else, eg., explicitly storing what downloaded correctly

                    if (delta > 10)
                    {
                        Console.WriteLine("    - \"{0}\" seems corrupt, downloading again", fname);
                    }
                    else
                    {
                        Console.WriteLine("    - \"{0}\" already exists, skipping", fname);
                        dl = false;
                    }

                }
                else
                {
                    // missing or invalid content length
                    // assume all is ok...
                    dl = false;
                }
            }
            else
            {
                //Detect renamed files
                string shortn;
                bool existing = find_renamed(filepath, clen, out shortn);

                if (existing)
                {
                    Console.WriteLine("    - \"{0}\" seems to be a copy of \"{1}\", renaming existing file", fname, shortn);
                    File.Move(shortn, filepath);
                    dl = false;
                }
                /*
            # Detect renamed files
            existing, short = find_renamed(filepath, clen)
            if existing:
                print_('    - "%s" seems to be a copy of "%s", renaming existing file' %
                       (fname, short))
                os.rename(existing, filepath)
                dl = False
                 */

            }

            try
            {
                if (dl)
                {
                    Console.WriteLine("     - Downloading {0}", fname);
                    //HttpWebResponse response = WebConnectionStuff.GetResponse(url, stream: true);
                    int full_size = clen;
                    int done_size = 0;
                    int slice_size = 524288; //512 kB buffer
                    DateTime last_time = DateTime.Now;

                    using (HttpWebResponse response = WebConnectionStuff.GetResponse(url, stream: true))
                    {
                        using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                        {
                            //if (ext == ".mp4" || )
                            //{
                                /*
                                FileStream fs = new FileStream(fname, FileMode.Create);
                                BinaryWriter bn = new BinaryWriter(fs);
                                bn.Write(reader.ReadToEnd());
                                bn.Close();
                                fs.Close();*/
                            using (Stream s = File.Create(filepath))
                            {
                                reader.BaseStream.CopyTo(s);
                            }
                            //FileStream fs = new FileStream(fname, FileMode.Create);
                            //    Encoding encode = System.Text.Encoding.GetEncoding("utf-8");
                            //    // Pipes the stream to a higher level stream reader with the required encoding format. 
                            //    //StreamReader readStream = new StreamReader(receiveStream, encode);
                            //    Console.WriteLine("\r\nResponse stream received.");
                            //    Char[] read = new Char[256];
                            //    // Reads 256 characters at a time.    
                            //    int count = reader.Read(read, 0, 256);
                            //    Console.WriteLine("HTML...\r\n");
                            //    while (count > 0)
                            //    {
                            //        fs.Write(new byte[0]{}, count, 256);
                            //        // Dumps the 256 characters on a string and displays the string to the console.
                            //        String str = new String(read, 0, count);
                            //        Console.Write(str);
                            //        count = reader.Read(read, 0, 256);
                            //    }
                            //}
                            //else
                            //{
                                
                            //    string readToEnd = reader.ReadToEnd();
                            //    File.WriteAllText(fname, readToEnd);
                            //    //jObject = JObject.Parse(readToEnd);
                            //}
                            reader.Close();
                        }
                        response.Close();
                    }

                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to download url {0} to {1}: {2}", url, filepath, e.Message);
            }


            /*"""
        
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

            download(string.Format(_courseraCourse.HOME_URL, cname), course_dir, "index.html");
            download(string.Format(_courseraCourse.lecture_url_from_name(cname)), course_dir, "lectures.html");

            try
            {
                download_about(cname, course_dir, _courseraCourse.ABOUT_URL);
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

        private bool find_renamed(string filepath, int clen, out string shortn)
        {
            if (Directory.Exists(filepath))
            {
                shortn = string.Empty;
                return false;
            }

            string name = normalize_string(Path.GetFileName(filepath));


            //TODO
            //Temp
            shortn = string.Empty;
            return false;

            /*
def find_renamed(filename, size):
    fpath, name = path.split(filename)
    name, ext = path.splitext(name)
    name = normalize_string(name)

    if not path.exists(fpath):
        return None, None

    files = os.listdir(fpath)
    if files:
        for f in files:
            fname, fext = path.splitext(f)
            fname = normalize_string(fname)
            if fname == name and fext == ext:
                fullname = os.path.join(fpath, f)
                if path.getsize(fullname) == size:
                    return fullname, f

    return None, None
             */
        }

        private string normalize_string(string str)
        {
            foreach (char invalidFileNameChar in Path.GetInvalidFileNameChars())
            {
                str.Replace(invalidFileNameChar.ToString(), "");
            }
            return str.ToLower();
        }










    }
}