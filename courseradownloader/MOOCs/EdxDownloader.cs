using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SevenZip;
using YoutubeExtractor;

namespace courseradownloader.MOOCs
{
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
                    clsdir = Utilities.TrimPathPart(clsdir, _edxCourse.Max_path_part_len - 15);
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