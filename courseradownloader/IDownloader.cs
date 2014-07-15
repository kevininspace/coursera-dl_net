namespace courseradownloader
{
    internal interface IDownloader
    {
        void Download(string format, string targetDir, string targetFname);
    }
}