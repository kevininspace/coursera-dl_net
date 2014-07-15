namespace courseradownloader
{
    internal interface IMooc
    {
        Course GetDownloadableContent(string courseName);
        void Login();
    }
}