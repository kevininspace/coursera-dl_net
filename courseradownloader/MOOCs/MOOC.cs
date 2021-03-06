using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace courseradownloader
{
    abstract class Mooc : IMooc
    {
        protected internal int Max_path_part_len;
        protected abstract string BASE_URL { get; }
        public abstract string HOME_URL { get; }
        protected abstract string LECTURE_URL { get; }
        protected internal IEnumerable<string> Ignorefiles { get; set; }


        /// <summary>
        /// Given the name of a course, return the video lecture url
        /// </summary>
        /// <param name="courseName"></param>
        /// <returns></returns>
        public virtual string LectureUrlFromName(string courseName)
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



        internal static void MakeCourseList(Course courseContent, string courseDir)
        {
            StringBuilder csv = new StringBuilder();
            csv.Append("Week Number, Class Number, Class Name, Link, Name");
            csv.Append(Environment.NewLine);
            foreach (Week week in courseContent.Weeks)
            {
                foreach (ClassSegment classSegment in week.ClassSegments)
                {
                    string key = classSegment.ResourceLinks.Keys.First();
                    string val = classSegment.ResourceLinks.Values.First();

                    string newLine = string.Format("{0},{1},{2},{3},{4}{5}", week.WeekNum, classSegment.ClassNum,
                        classSegment.ClassName, key, val, Environment.NewLine);
                    csv.Append(newLine);
                }
            }
            Directory.CreateDirectory(courseDir);

            //File.Create(Path.Combine(courseDir, "content.csv")).Close();

            File.WriteAllText(Path.Combine(courseDir, "content.csv"), csv.ToString());
        }


        public abstract Course GetDownloadableContent(string courseName);
        //public abstract void Login();
        public abstract bool Login();
        public abstract void Login(string s);
        public abstract void Download(string courseName, string destDir, bool b, bool gzipCourses, Course courseContent);
    }

    public static class Extensions
    {
        /// <summary>
        /// Many class names have the following format: "Something really cool (12:34)"
        /// If the class name has this format, replace the colon in the time with a hyphen.
        /// </summary>
        /// <param name="str">The string you want to remove the colon (":") from.</param>
        /// <returns>The string with the colon replaced by a hyphen. ":" => "-"</returns>
        public static string RemoveColon(this string str)
        {

            if (Regex.IsMatch(str, @".+\(\d?\d:\d\d\)"))
            {
                str = str.Replace(":", "-");
            }

            string invalid = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());

            foreach (char c in invalid)
            {
                str = str.Replace(c.ToString(), "");
            }
            if (Regex.IsMatch(str, ":"))
            {
                str = str.Replace(":", "-");
            }

            return str;
        }

    }
}