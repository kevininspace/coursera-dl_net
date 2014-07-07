using System.Collections.Generic;
using System.IO;
using System.Net;

namespace courseradownloader
{
    abstract class MOOC : IMooc
    {
        protected int Max_path_part_len;
        protected abstract string BASE_URL { get; }
        public abstract string HOME_URL { get; }
        protected abstract string LECTURE_URL { get; }

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

        public virtual string TrimPathPart(string weekTopic)
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

        


        public abstract Course GetDownloadableContent(string courseName);
        public abstract void Login();
        public abstract void Login(string s);
        public abstract void Download(string courseName, string destDir, bool b, bool gzipCourses, Course courseContent);
    }
}