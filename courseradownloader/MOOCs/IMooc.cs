﻿namespace courseradownloader
{
    internal interface IMooc
    {
        Course GetDownloadableContent(string courseName);
        bool Login();
    }
}