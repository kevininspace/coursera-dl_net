using System.Diagnostics;
using System.IO;
using System.Net;

namespace courseradownloader
{
    internal abstract class Downloader
    {
        protected static int GetContentLength(WebHeaderCollection responseHeaders)
        {
            int contentLength;
            //get the content length (if present)
            //Will return 0 if can't get value
            string clenString = responseHeaders.Get("Content-Length");
            //string clenString = response.GetResponseHeader("Content-Lengthzzz");
            int.TryParse(clenString, out contentLength);
            return contentLength;
        }

        protected bool FindRenamed(string filepath, out string shortn)
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