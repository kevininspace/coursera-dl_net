using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using SevenZip;

namespace courseradownloader
{
    /// <summary>
    /// Class to download content (videos, lecture notes, ...) from coursera.org for use offline.
    /// Inspired by https://github.com/dgorissen/coursera-dl
    /// </summary>
    internal class CourseraDownloader : Downloader, IDownloader
    {
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
        private void DownloadAbout(string cname, string courseDir, string abouturl)
        {
            string fn = Path.Combine(courseDir, cname) + "-about.json";

            //get the base course name (without the -00x suffix)
            string[] strings = Regex.Split(cname, "(-[0-9]+)");
            string baseName = strings[0];

            //get the json
            string aboutUrl = string.Format(abouturl, baseName);
            JObject jObject = GetJson(aboutUrl);

            //pretty print to file
            List<string> jsonList = new List<string>();
            try
            {
                foreach (KeyValuePair<string, JToken> keyValuePair in jObject)
                {
                    jsonList.Add(keyValuePair.Key + "," + keyValuePair.Value);
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
        private static JObject GetJson(string url)
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
        public void Download(string url, string targetDir = ".", string targetFname = null)
        {
            using (HttpWebResponse response = WebConnectionStuff.GetResponse(url, stream: true))
            {
                WebHeaderCollection responseHeaders = response.Headers;

                int contentLength = GetContentLength(responseHeaders);
                string filepath = GetFilePath(url, targetDir, responseHeaders);

                string fname = Path.GetFileName(filepath);

                bool dl = IsFileNeeded(filepath, contentLength, fname);

                if (dl)
                {
                    try
                    {
                        Console.WriteLine("     - Downloading {0}", fname);
                        int full_size = contentLength;
                        int done_size = 0;
                        int slice_size = 524288; //512 kB buffer
                        DateTime last_time = DateTime.Now;

                        using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                        {
                            using (Stream s = File.Create(filepath))
                            {
                                reader.BaseStream.CopyTo(s);
                            }
                            reader.Close();
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Failed to download url {0} to {1}: {2}", url, filepath, e.Message);
                    }
                }
                response.Close();
            }
        }

        private bool IsFileNeeded(string filepath, int contentLength, string fname)
        {
            //split off the extension and check if we should skip it (remember to remove the leading .)
            string ext = Path.GetExtension(fname);
            if (!string.IsNullOrEmpty(ext) && _courseraCourse.Ignorefiles != null && _courseraCourse.Ignorefiles.Contains(ext))
            {
                Console.WriteLine("    - skipping \"{0}\" (extension ignored)", fname);
                return false;
            }

            //Next check if it already exists and is unchanged
            if (File.Exists(filepath))
            {
                if (contentLength > 0)
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

                    if (delta > 10)
                    {
                        Console.WriteLine("    - \"{0}\" seems corrupt, downloading again", fname);
                    }
                    else
                    {
                        Console.WriteLine("    - \"{0}\" already exists, skipping", fname);
                        return false;
                    }
                }
                else
                {
                    // missing or invalid content length
                    // assume all is ok... not much we can do
                    return false;
                }
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

        private string GetFilePath(string url, string targetDir, WebHeaderCollection responseHeaders)
        {
            //build the absolute path we are going to write to
            string fname = null;
            try
            {
                fname = util.filename_from_header(responseHeaders);
            }
            finally
            {
                if (string.IsNullOrEmpty(fname))
                {
                    fname = util.filename_from_url(url);
                }
            }

            fname = fname.RemoveColon();

            //ensure it respects mppl
            fname = _courseraCourse.TrimPathPart(fname);

            string filepath = Path.Combine(targetDir, fname);

            return filepath;
        }

        private static int GetContentLength(WebHeaderCollection responseHeaders)
        {
            int contentLength;
            //get the content length (if present)
            //Will return 0 if can't get value
            string clenString = responseHeaders.Get("Content-Length");
            //string clenString = response.GetResponseHeader("Content-Lengthzzz");
            int.TryParse(clenString, out contentLength);
            return contentLength;
        }

        /// <summary>
        /// Download all the contents (quizzes, videos, lecture notes, ...) of the course to the given destination directory (defaults to .)
        /// </summary>
        /// <param name="courseName"> </param>
        /// <param name="destDir"></param>
        /// <param name="reverse"></param>
        /// <param name="gzipCourses"></param>
        /// <param name="weeklyTopics"> </param>
        public virtual void DownloadCourse(string courseName, string destDir, bool reverse, bool gzipCourses, Course weeklyTopics)
        {

            if (!weeklyTopics.Weeks.Any())
            {
                Console.WriteLine(" Warning: no downloadable content found for {0}, did you accept the honour code?", courseName);
            }
            else
            {
                Console.WriteLine(" * Got all downloadable content for {0} ", courseName);
            }

            if (reverse)
            {
                weeklyTopics.Weeks.Reverse();
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

            Download(string.Format(_courseraCourse.HOME_URL, courseName), courseDir, "index.html");
            Download(string.Format(_courseraCourse.LectureUrlFromName(courseName)), courseDir, "lectures.html");

            try
            {
                DownloadAbout(courseName, courseDir, _courseraCourse.ABOUT_URL);
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
                string wkdir = Path.Combine(courseDir, wkdirname);
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
            /*

        if gzip_courses:
            tar_file_name = courseName + ".tar.gz"
            print_("Compressing and storing as " + tar_file_name)
            tar = tarfile.open(os.path.join(dest_dir, tar_file_name), 'w:gz')
            tar.add(os.path.join(dest_dir, courseName), arcname=courseName)
            tar.close()
            print_("Compression complete. Cleaning up.")
            shutil.rmtree(os.path.join(dest_dir, courseName))
             */
        }

        private bool FindRenamed(string filepath, out string shortn)
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
            string replace = null;
            foreach (char invalidFileNameChar in Path.GetInvalidFileNameChars())
            {
                replace = str.Replace(invalidFileNameChar.ToString(), "");
            }
            Debug.Assert(replace != null, "replace != null");
            return replace.ToLower();
        }

    }
}