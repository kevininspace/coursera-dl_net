using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using NReadability;
using SevenZip;

namespace courseradownloader
{
    internal class FutureLearnDownloader : Downloader, IDownloader
    {
        private FutureLearn _futureleanCourse;

        public FutureLearnDownloader(FutureLearn futureLearn)
        {
            _futureleanCourse = futureLearn;

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

            Download(string.Format(_futureleanCourse.HOME_URL, courseName), courseDir, "index.html");
            Download(string.Format(_futureleanCourse.LectureUrlFromName(courseName)), courseDir, "lectures.html");

            /*
            // TextFieldParser is in the Microsoft.VisualBasic.FileIO namespace.
            using (TextFieldParser parser = new TextFieldParser(Path.Combine(courseDir, "content.csv")))
            {
                parser.CommentTokens = new string[] { "#" };
                parser.SetDelimiters(new string[] { "," });
                parser.HasFieldsEnclosedInQuotes = true;

                // Skip over header line.
                parser.ReadLine();

                while (!parser.EndOfData)
                {
                    string[] fields = parser.ReadFields();
                    string file = fields[8];
                    string url = fields[7];
                    //if (Path.HasExtension(file) && Path.GetExtension(file) == ".html")
                    //{
                    //    string s = _futureleanCourse._client.DownloadString(url);
                    //    byte[] pdfBuf = new SynchronizedPechkin(new GlobalConfig()).Convert(s);
                        
                    //    File.WriteAllBytes(file, pdfBuf);
                    //    //FileStream fs = new FileStream(file, FileMode.Create);
                    //    //fs.Write(pdfBuf, 0, pdfBuf.Length);
                    //    //api/content/v1/parser?url=http://blog.readability.com/2011/02/step-up-be-heard-readability-ideas/&token=1b830931777ac7c2ac954e9f0d67df437175e66e
                    //    //35aa55213619367d18118598984a4647a3d073dc
                    //    string token = "35aa55213619367d18118598984a4647a3d073dc";
                    //    string format = string.Format("http://www.readability.com/api/content/v1/parser?url={0}&token={1}", url, token);
                    //    string downloadString = _futureleanCourse._client.DownloadString(format);
                    //}
                }
            }
             */

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
                    string clsdirname = classSegment.ClassName;

                    //ensure the class dir exists
                    //string clsdir = Path.Combine(wkdir, clsdirname);
                    //Directory.CreateDirectory(clsdir);

                    Console.WriteLine(" - Downloading resources for " + clsdirname);

                    //download each resource
                    foreach (KeyValuePair<string, string> resourceLink in classSegment.ResourceLinks)
                    {
                        //Filter here


                        try
                        {
                            Download(resourceLink.Key, wkdir, resourceLink.Value);
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

            //
        }



        public void Download(string url, string targetDir, string targetFname)
        {
            string fname = targetFname.RemoveColon();

            string filepath = Path.Combine(targetDir, fname);

            //ensure it respects mppl
            filepath = _futureleanCourse.TrimPathPart(filepath);

            WebHeaderCollection responseHeaders = _futureleanCourse._client.ResponseHeaders;
            int contentLength = GetContentLength(responseHeaders);
            bool isFileNeeded = IsFileNeeded(filepath, contentLength, fname);

            if (isFileNeeded)
            {

                if (Path.GetExtension(filepath) == ".html")
                {
                    string content = _futureleanCourse._client.DownloadString(url);
                    NReadabilityTranscoder transcoder = new NReadabilityTranscoder();
                    TranscodingInput tiInput = new TranscodingInput(content);
                    TranscodingResult transcodedContent = transcoder.Transcode(tiInput);
                        //.Transcode(content, out success);
                    File.WriteAllText(filepath, transcodedContent.ExtractedContent);
                }
                else
                {
                    _futureleanCourse._client.DownloadFile(url, filepath);
                }
            }

        }

        private bool IsFileNeeded(string filepath, int contentLength, string fname)
        {
            //split off the extension and check if we should skip it (remember to remove the leading .)
            string ext = Path.GetExtension(fname);
            if (!string.IsNullOrEmpty(ext) && _futureleanCourse.Ignorefiles != null && _futureleanCourse.Ignorefiles.Contains(ext))
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

    }
}