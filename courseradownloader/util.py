import re
import unicodedata
from os import path
from six import print_, PY2
from six.moves.urllib.parse import unquote, urlparse, urlsplit







def trim_path(pathname, max_path_len=255, min_len=5):
    """
    Trim file name in given path name to fit max_path_len characters. Only file name is trimmed,
    path names are not affected to avoid creating multiple folders for the same lecture.
    """
    if len(pathname) <= max_path_len:
        return pathname

    fpath, name = path.split(pathname)
    name, ext = path.splitext(name)

    to_cut = len(pathname) - max_path_len
    to_keep = len(name) - to_cut

    if to_keep < min_len:
        print_(' Warning: Cannot trim filename "%s" to fit required path length (%d)' %
               (pathname, max_path_len))
        return pathname

    name = name[:to_keep]
    new_pathname = path.join(fpath, name + ext)
    print_(' Trimmed path name "%s" to "%s" to fit required length (%d)' %
           (pathname, new_pathname, max_path_len))

    return new_pathname
