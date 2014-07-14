using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SevenZip;

namespace courseradownloader
{
    internal class FutureLearnDownloader : IDownloader
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

            download(string.Format(_futureleanCourse.HOME_URL, courseName), courseDir, "index.html");
            download(string.Format(_futureleanCourse.LectureUrlFromName(courseName)), courseDir, "lectures.html");

            //try
            //{
            //    DownloadAbout(courseName, courseDir, _courseraCourse.ABOUT_URL);
            //}
            //catch (Exception e)
            //{
            //    Console.WriteLine("Warning: failed to download about file: {0}", e.Message);
            //}

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
                            download(resourceLink.Key, clsdir, resourceLink.Value);
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

        public void download(string url, string targetDir, string targetFname)
        {
            string fname = targetFname.RemoveColon();

            //ensure it respects mppl
            fname = _futureleanCourse.TrimPathPart(fname);

            string filepath = Path.Combine(targetDir, fname);
            
            //fname = Path.GetFileName(filepath);

            _futureleanCourse._client.DownloadFile(url, filepath);
        }
    }
}