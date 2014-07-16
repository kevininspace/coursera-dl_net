using System;
using System.Collections.Generic;
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

        protected List<string> Ignorefiles;

        protected bool IsFileNeeded(string filepath, int contentLength, string fname)
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