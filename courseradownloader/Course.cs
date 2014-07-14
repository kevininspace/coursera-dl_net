using System.Collections.Generic;

namespace courseradownloader
{
    public class Course
    {
        public Course(string name)
        {
            CourseName = name;
            Weeks = new List<Week>();
        }

        public string CourseName { get; private set; }
        internal List<Week> Weeks { get; set; }
    }

    internal class Week
    {
        public Week(string name)
        {
            WeekName = name;
            ClassSegments = new List<ClassSegment>();
        }

        public string WeekName { get; private set; }
        public int WeekNum { get; set; }
        public List<ClassSegment> ClassSegments { get; set; }
    }

    internal class ClassSegment
    {
        public ClassSegment(string className)
        {
            ClassName = className;
        }

        public string ClassName { get; private set; }
        public Dictionary<string, string> ResourceLinks { get; set; }

        public int ClassNum { get; set; }
    }
}