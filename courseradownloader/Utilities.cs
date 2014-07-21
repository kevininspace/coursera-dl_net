using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace courseradownloader
{
    public static class Utilities
    {
        internal static void DrawProgressBar(int complete, int maxVal, int barSize, char progressCharacter)
        {
            Console.CursorVisible = false;
            int left = Console.CursorLeft;
            decimal perc = (decimal)complete / (decimal)maxVal;
            int chars = (int)Math.Floor(perc / ((decimal)1 / (decimal)barSize));
            string p1 = String.Empty, p2 = String.Empty;

            for (int i = 0; i < chars; i++) p1 += progressCharacter;
            for (int i = 0; i < barSize - chars; i++) p2 += progressCharacter;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(p1);
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.Write(p2);

            Console.ResetColor();
            Console.Write(" {0}%", (perc * 100).ToString("N2"));
            Console.CursorLeft = left;
        }

        internal static string filename_from_header(WebHeaderCollection headers)
        {
            try
            {
                string cd = headers["Content-Disposition"];
                Match m = Regex.Match(cd, "attachment; filename=\"(.*?)\"");
                Group g = m.Groups[1];
                string gDecode = g.Value;
                if (gDecode.Contains("%"))
                {
                    gDecode = HttpUtility.UrlDecode(g.Value);
                }
                return sanitise_filename(gDecode);
            }
            catch (Exception e)
            {
                return "";
            }
        }


        /// <summary>
        /// ensure a clean, valid filename (arg may be both str and unicode)
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static string sanitise_filename(string filename)
        {
            string s;
            
            //ensure a unicode string, problematic ascii chars will get removed
            //normalize it
            string normalize = filename.Normalize(NormalizationForm.FormKD);

            normalize = HtmlAgilityPack.HtmlEntity.DeEntitize(normalize);
            
            //remove any characters not in the whitelist
            normalize = Regex.Replace(normalize, @"[^\w\-\(\)\., ]", @"").Trim(); //[^\w\-\(\)\[\]\., \:]
            normalize = normalize.RemoveColon();
            /*TODO
             * # ensure it is within a sane maximum
             * max = 250
             *     
             * # split off extension, trim, and re-add the extension
             * fn, ext = path.splitext(s)
             * s = fn[:max - len(ext)] + ext
             */

            s = normalize;

            return s;
        }

        public static string clean_url(string resourceUrl)
        {
            if (string.IsNullOrEmpty(resourceUrl))
            {
                return null;
            }

            Uri url = new Uri(resourceUrl.Trim());

            UriBuilder uriBuilder = new UriBuilder(url); //{Scheme = Uri.UriSchemeHttp}};

            return uriBuilder.Uri.AbsoluteUri;
        }

        public static string filename_from_url(string url)
        {
            // parse the url into its components
            Uri u = new Uri(url);

            NameValueCollection nameValueCollection = HttpUtility.ParseQueryString(url);
            string ext = string.Empty;
            if (nameValueCollection.HasKeys() && nameValueCollection.GetValues("format") != null)
            {
                ext = nameValueCollection.GetValues("format").FirstOrDefault();
            }


            // split the path into parts and unquote
            string[] strings = u.AbsolutePath.Split('/');
            List<string> parts = new List<string>();
            foreach (string x in strings)
            {
                parts.Add(HttpUtility.UrlDecode(x));
            }
            
            // take the last component as filename
            string fname = parts.Last();

            // if empty, url ended with a trailing slash
            // so join up the hostnam/path  and use that as a filename
            if (fname.Length < 1)
            {
                string s = u.Host + u.AbsolutePath;
                fname = s.Replace('/', '_');
            }
            else
            {
                // unquoting could have cuased slashes to appear again
                // split and take the last element if so
                fname = fname.Split('/').Last();
            }

            
            // add an extension if none
            if (string.IsNullOrEmpty(ext) || string.IsNullOrWhiteSpace(ext))
            {
                ext = Path.GetExtension(fname);
            }

            if( ext.Length < 1 || ext.Length > 5)
            {
                fname += ".html";
            }
            else
            {
                fname = Path.ChangeExtension(fname, ext);
            }

            // remove any illegal chars and return
            return sanitise_filename(fname);
        }

       public static string TrimPathPart(string weekTopic, int Max_path_part_len)
        {
            weekTopic = Regex.Replace(weekTopic, @"\s+", " ");
            //TODO: simple hack, something more elaborate needed
            if (Max_path_part_len != 0 && weekTopic.Length > Max_path_part_len)
            {
                if (Path.HasExtension(weekTopic))
                {
                    string extension = Path.GetExtension(weekTopic);
                    string substring = weekTopic.Substring(0, Max_path_part_len - extension.Length - 2);
                    return Path.ChangeExtension(substring, extension);
                }

                return weekTopic.Substring(0, Max_path_part_len);
            }
            else
            {
                return weekTopic;
            }
        }
    }
}
