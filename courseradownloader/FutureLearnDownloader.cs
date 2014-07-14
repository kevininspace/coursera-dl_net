using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualBasic.FileIO;
using Pechkin;
using Pechkin.Synchronized;
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

            StringBuilder csv = new StringBuilder();
            foreach (Week week in courseContent.Weeks)
            {
                foreach (ClassSegment classSegment in week.ClassSegments)
                {
                    string key = classSegment.ResourceLinks.Keys.First();
                    string val = classSegment.ResourceLinks.Values.First();
                    csv.Append("Week Number, Class Number, Class Name, Link, Name");
                    string newLine = string.Format("{0},{1},{2},{3},{4}{5}", week.WeekNum, classSegment.ClassNum, classSegment.ClassName, key, val, Environment.NewLine);
                    csv.Append(newLine);
                }
            }

            //after your loop
            File.WriteAllText(Path.Combine(courseDir, "content.csv"), csv.ToString());

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
                            download(resourceLink.Key, wkdir, resourceLink.Value);
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

            string filepath = Path.Combine(targetDir, fname);

            //ensure it respects mppl
            filepath = _futureleanCourse.TrimPathPart(filepath);

            //fname = Path.GetFileName(filepath);

            _futureleanCourse._client.DownloadFile(url, filepath);
        }
    }
}