namespace MoodleHelper.Models
{
    public class MoodleUser
    {
        public string id { get; set; }
        public string username { get; set; }
        public string password { get; set; }
        public string firstname { get; set; }
        public string lastname { get; set; }
        public string email { get; set; }
        public int role { get; set; } = 5; // student = 5 moodle role
        public bool isCourseEnroll { get; set; } = false;
        public bool isAssignRole { get; set; } = false;
    }
    public class MoodleCreateUserResponse
    {
        public string id { get; set; }
        public string username { get; set; }
    }
    public class MoodleException
    {
        public string exception { get; set; }
        public string errorcode { get; set; }
        public string message { get; set; }
        public string debuginfo { get; set; }
    }
}