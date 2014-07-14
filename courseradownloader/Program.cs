using System;
using System.Collections.Generic;
using System.Reflection;
using NDesk.Options;

namespace courseradownloader
{
    class Program
    {
        private static string username;
        private static string password;
        private static string parser;
        private static string proxy;
        private static string reverse;
        private static string[] course_names;
        private static bool gzip_courses;
        private static int mppl;
        private static string wkfilter;
        private static string dest_dir;
        private static string ignorefiles;

        private static void Main(string[] args)
        {
            // parse the commandline arguments
            OptionSet optionSet = new OptionSet();
            Console.WriteLine("Download Coursera.org course videos/docs for offline use.");
            optionSet.WriteOptionDescriptions(Console.Out);

            optionSet.Add("u=", "coursera username (.netrc used if omitted)", value => username = value);
            optionSet.Add("p=", "coursera password", value => password = value);

            optionSet.Add("d:", "destination directory where everything will be saved", value => dest_dir = value);
            optionSet.Add("n:", "comma-separated list of file extensions to skip, e.g., \"ppt,srt,pdf\"", value => ignorefiles = value);
            optionSet.Add("reverse-sections:", "download and save the sections in reverse order", value => reverse = value);
            optionSet.Add("course_names=", "one or more course names from the url (e.g., comnets-2012-001)", value => course_names = value.Split('+'));
            optionSet.Add("gz:", "Tarball courses for archival storage (folders get deleted)", value => gzip_courses = (value == "true"));
            optionSet.Add("mppl:", "Maximum length of filenames/dirs in a path", value => mppl = int.Parse(value));
            optionSet.Add("w:", "Comma separted list of week numbers to download e.g., 1,3,8", value => wkfilter = value);
            List<string> unparsedArgs = optionSet.Parse(args);

            Console.WriteLine("Coursera-dl v{0} ({1})", Assembly.GetExecutingAssembly().GetName().Version, optionSet.GetType().ToString());

            // check the parser
            // search for login credentials in .netrc file if username hasn"t been
            // provided in command-line args
            if (unparsedArgs.Contains("u"))
            {
                /*object creds = get_netrc_creds();
                if (creds == null)
                {
                    throw new Exception("No username passed and no .netrc credentials found (check a netrc file exists and has the correct permissions), unable to login");
                }
                else
                {
                    //TODO: Fix here
                    ////username1 = creds.Username;
                    ////password1 = creds.Password;
                }
                 */
            }
            else if (unparsedArgs.Contains("p")) // prompt the user for his password if not specified
            {
                Console.WriteLine("Please enter your password:");
                password = Console.ReadLine();
            }

            // should we be trimming paths?
            // TODO: this is a simple hack, something more elaborate needed
            //if mppl specified, always use that
            if (!unparsedArgs.Contains("mppl"))
            {
                // if mppl is not specified on windows set manually
                mppl = 260;
                Console.WriteLine("Maximum length of a path component set to {0}", mppl);
            }

            Coursera coursera = null;
            FutureLearn futureLearn = null;
            int i = 0;
            foreach (string courseName in course_names)
            {
                string[] courseLocationName = courseName.Split(':');
                switch (courseLocationName[0]) //which site
                {
                    case "coursera":
                        // instantiate the downloader class
                        coursera = new Coursera(username, password, proxy, parser, ignorefiles, mppl, gzip_courses, wkfilter);
                        // authenticate, only need to do this once but need a classaname to get hold
                        // of the csrf token, so simply pass the first one
                        Console.WriteLine("Logging in as \"{0}\"...", username);
                        coursera.Login(courseLocationName[1]); //the actual course name
                        Course courseContent = coursera.GetDownloadableContent(courseLocationName[1]);
                        coursera.Courses.Add(courseContent);
                        Console.WriteLine("Course {0} of {1}", i + 1, course_names.Length);
                        break;
                    case "futurelearn":
                        // instantiate the downloader class
                        futureLearn = new FutureLearn(username, password, proxy, parser, ignorefiles, mppl, gzip_courses, wkfilter);
                        // authenticate, need to get hold of the csrf token
                        Console.WriteLine("Logging in as \"{0}\"...", username);
                        futureLearn.Login();
                        Course flCourseContent = futureLearn.GetDownloadableContent(courseLocationName[1]);
                        futureLearn.Courses.Add(flCourseContent);
                        Console.WriteLine("Course {0} of {1}", i + 1, course_names.Length);
                        break;
                    case "udacity":
                        break;
                    case "edx":
                        break;
                }
                i++;
            }

            // download the content
            if (coursera != null)
            {
                foreach (Course course in coursera.Courses)
                {
                    coursera.Download(course.CourseName, dest_dir, false, gzip_courses, course);
                }
            }
            if (futureLearn != null)
            {
                foreach (Course course in futureLearn.Courses)
                {
                    futureLearn.Download(course.CourseName, dest_dir, false, gzip_courses, course);
                }
            }
        }
        /*
        private static object get_netrc_creds()
        {
            // Read username/password from the users' netrc file. Returns null if no
            // coursera credentials can be found.
            // inspired by https://github.com/jplehmann/coursera
            List<string> environmentVariablesList = new List<string>()
            {
                Environment.GetEnvironmentVariable("HOME"),
                Environment.GetEnvironmentVariable("HOMEDRIVE"),
                Environment.GetEnvironmentVariable("HOMEPATH"),
                Environment.GetEnvironmentVariable("USERPROFILE"),
                Environment.GetEnvironmentVariable("SYSTEMDRIVE"),
                Environment.CurrentDirectory,
                Environment.SystemDirectory,
                Directory.GetDirectoryRoot(@"C:\")
            };

            List<string> file_names = new List<string>()
                {
                    ".netrc",
                    "_netrc"
                };

            IEnumerable<string> paths = environmentVariablesList.Zip(file_names, string.Concat);

            // try the paths one by one and return the first one that works
            object creds = null;
            foreach (string path in paths)
            {
                try
                {
                    //auths = netrc.netrc(p).authenticators('coursera-dl');
                    //creds = (auths[0], auths[2]);
                    //print_("Credentials found in .netrc file");
                    break;
                }
                catch (Exception)
                {
                    throw;
                }
            }

            return creds;
        }
         */
    }
}
