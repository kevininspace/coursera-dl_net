using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace courseradownloader
{
    public static class util
    {

        private static string filename_from_header(Dictionary<string, string> headers)
        {
            try
            {
                string cd = headers["Content-Disposition"];
                Regex.Match(cd, "atachment/; filename=/"(.*?)/"")
                ;
            }
            catch (Exception)
            {
                
                throw;
            }
            /*
             * def filename_from_header(header):
     try:
         cd = header['Content-Disposition']
         pattern = 'attachment; filename="(.*?)"'
         m = re.search(pattern, cd)
         g = m.group(1)
         if "%" in g:
             g = unquote(g)
         return sanitise_filename(g)
     except Exception:
         return ''
             */
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
            normalize = Regex.Replace(normalize, @"[^\w\-\(\)\[\]\., ]", @"").Trim();

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

            UriBuilder uriBuilder = new UriBuilder(url){Scheme = Uri.UriSchemeHttp};

            return uriBuilder.Uri.AbsoluteUri;
        }
    }
}
