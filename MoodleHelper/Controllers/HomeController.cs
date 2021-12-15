using MoodleHelper.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web.Mvc;
using System.Web.Script.Serialization;

namespace MoodleHelper.Controllers
{
    public class HomeController : Controller
    {
        private static readonly int courseID = 10; // my course id
        private static readonly JavaScriptSerializer js = new JavaScriptSerializer();
        private static readonly string basePath = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string serviceUrl = System.Configuration.ConfigurationManager.AppSettings.Get("MoodleWebServiceUrl");

        public ActionResult Index()
        {
            ViewBag.CreteFile = FileCreateStatus();
            ViewBag.FormatedUsers = FileCreateStatus(1);

            ViewBag.FormatedUserCount = 0;
            ViewBag.NonFormatedUserCount = 0;

            if ((bool)ViewBag.CreteFile)
            {
                string getUserCsvFileFullPath = GetUserCsvFileFullPath();
                using (StreamReader sr = new StreamReader(getUserCsvFileFullPath))
                {
                    string currentLine;
                    var lineCount = 0;
                    while ((currentLine = sr.ReadLine()) != null)
                    {
                        var infos = currentLine.Split(';').ToList();
                        if (infos.Count > 2)
                        {
                            lineCount++;
                        }
                    }
                    ViewBag.NonFormatedUserCount = lineCount;
                }
            }
            if ((bool)ViewBag.FormatedUsers)
            {
                string getFormattedUsersFileFullPath = GetFormattedUsersFileFullPath();
                using (StreamReader sr = new StreamReader(getFormattedUsersFileFullPath))
                {
                    var jsonFileContent = sr.ReadToEnd();
                    if (!string.IsNullOrEmpty(jsonFileContent))
                    {
                        var list = js.Deserialize<List<MoodleUser>>(jsonFileContent);
                        if (list.Any())
                        {
                            ViewBag.FormatedUserCount = list.Count(t => t.id == null);
                        }
                    }
                }
            }

            return View();
        }

        /// <summary>
        /// CSV users map to moodle user list 
        /// </summary>
        /// <returns></returns>
        public ActionResult CreateFormatedFile()
        {
            ViewBag.CreteFile = FileCreateStatus();
            ViewBag.FormatedUsers = FileCreateStatus(1);
            ViewBag.FormatedUserCount = 0;
            ViewBag.NonFormatedUserCount = 0;

            string fullPath = GetUserCsvFileFullPath();

            var listMoodleUser = new List<MoodleUser>();

            using (StreamReader sr = new StreamReader(fullPath))
            {
                string currentLine;
                while ((currentLine = sr.ReadLine()) != null)
                {
                    var infos = currentLine.Split(';').ToList();
                    if (infos.Count > 2)
                    {
                        var _email = infos[2].Trim();
                        var nameSurname = infos[0]; // username@edomain.com
                        var nameList = nameSurname.Split(' ').ToList(); // ["jonas", "jonas"] 
                        var _lastname = nameList.LastOrDefault(); // "jonas"
                        var _firstname = string.Join(" ", nameList.Take(nameList.Count - 1));
                        var _password = infos[1];
                        var _username = infos[2];
                        var moodleUser = new MoodleUser()
                        {
                            email = _email,
                            firstname = _firstname,
                            lastname = _lastname,
                            password = _password,
                            username = _password
                        };
                        listMoodleUser.Add(moodleUser);
                    }
                }
            }

            string json = js.Serialize(listMoodleUser);

            //write string to file
            System.IO.File.WriteAllText(basePath + "formatedUsers.json", json);
            ViewBag.CreteFile = FileCreateStatus();
            ViewBag.FormatedUsers = FileCreateStatus(1);
            ViewBag.FormatedUserCount = listMoodleUser.Count;

            return View("Index");
        }

        /// <summary>
        /// 1- The user of the list is created one by one on the moodle.
        /// 2- Each created user is assigned the student role.
        /// 3- Each created user is enroll given the course (courseID).
        /// 4- All users are written to disk step by step
        /// </summary>
        /// <returns></returns>
        public ActionResult CreatedMoodleUser()
        {
            ViewBag.CreteFile = FileCreateStatus();
            ViewBag.FormatedUsers = FileCreateStatus(1);
            ViewBag.FormatedUserCount = 0;
            ViewBag.NonFormatedUserCount = 0;

            var userList = new List<MoodleUser>();
            string getFormattedUsersFileFullPath = GetFormattedUsersFileFullPath();
            using (StreamReader sr = new StreamReader(getFormattedUsersFileFullPath))
            {
                var jsonFileContent = sr.ReadToEnd();
                if (!string.IsNullOrEmpty(jsonFileContent))
                {
                    userList = js.Deserialize<List<MoodleUser>>(jsonFileContent);
                }
            }
            userList = userList.Where(u => u.id == null).ToList();

            if (userList.Any())
            {
                var ex = new MoodleException();
                for (int i = 0; i < userList.Count; i++)
                {
                    var result = CreateMoodleUser(userList[i], out ex);

                    // 1
                    if (result != null && result.Any())
                        userList[i].id = result.FirstOrDefault().id;

                    if (!string.IsNullOrEmpty(userList[i].id))
                    {
                        // 2
                        userList[i].isAssignRole = AddRoleMoodleUser(int.Parse(userList[i].id), userList[i].role, out ex);

                        // 3
                        userList[i].isCourseEnroll = CourseEnrolmentUser(courseID, userList[i].id, userList[i].role, out ex);
                    }
                    //4
                    if (userList[i].id != null)
                    {
                        string jsonStr = js.Serialize(userList);
                        System.IO.File.WriteAllText(basePath + "formatedUsers.json", jsonStr);
                    }
                    Response.Write(js.Serialize(userList[i]));
                }
            }
            ViewBag.CreteFile = FileCreateStatus();
            ViewBag.FormatedUsers = FileCreateStatus(1);

            ViewBag.FormatedUserCount = 0;
            ViewBag.NonFormatedUserCount = userList.Where(t => t.id == null).Count();

            return View("Index");
        }

        /// <summary>
        /// It finds and creates the user whose id is missing in the main list from the sublist, then deletes the sublist.
        /// </summary>
        /// <returns></returns>
        public ActionResult SyncALLList()
        {
            ViewBag.CreteFile = FileCreateStatus();
            ViewBag.FormatedUsers = FileCreateStatus(1);
            ViewBag.FormatedUserCount = 0;
            ViewBag.NonFormatedUserCount = 0;

            string mainListJson = "formatedUsers-all.json";
            string subListJson = "formatedUsers-1.json";
            string path1 = Path.Combine(basePath, mainListJson);
            string path4 = Path.Combine(basePath, subListJson);

            var mainList = new List<MoodleUser>();
            var subList = new List<MoodleUser>();

            if (!string.IsNullOrEmpty(path1))
            {
                var fileJson = ReadFile(path1);
                mainList = js.Deserialize<List<MoodleUser>>(fileJson);
            }

            if (!string.IsNullOrEmpty(path4))
            {
                var fileJson = ReadFile(path4);
                subList = js.Deserialize<List<MoodleUser>>(fileJson);
            }

            foreach (var item in mainList)
            {
                if (!string.IsNullOrEmpty(item.id))
                    continue;
                item.username = item.username.Replace("ç", "c").Replace("ö", "o").Replace("ş", "s").Replace("ü", "u");
                var subItem = subList.FirstOrDefault(t => t.username == item.username);
                if (subItem != null)
                {
                    item.email = subItem.email.Replace("ç", "c").Replace("ö", "o").Replace("ş", "s").Replace("ü", "u");
                    item.password = subItem.password.Replace("ç", "c").Replace("ö", "o").Replace("ş", "s").Replace("ü", "u");
                    item.id = subItem.id;
                }
            }

            var allJson = js.Serialize(mainList);
            System.IO.File.WriteAllText(basePath + "formatedUsers-all.json", allJson);

            for (int i = 0; i < mainList.Count; i++)
            {
                if (subList.Any())
                {
                    var id = mainList[i].id;
                    var subitem = subList.FirstOrDefault(t => t.id == id);
                    if (subitem != null)
                    {
                        subList.Remove(subitem);
                    }
                }
                else
                {
                    break;
                }
            }
            System.IO.File.WriteAllText(basePath + "formatedUsers-1.json", js.Serialize(subList));
            return View("Index");
        }

        /// <summary>
        /// Being able to detect users with empty ids from the total list. (ids-null.json) will create.
        /// </summary>
        /// <returns></returns>
        public ActionResult FindNullIds()
        {
            ViewBag.CreteFile = FileCreateStatus();
            ViewBag.FormatedUsers = FileCreateStatus(1);
            ViewBag.FormatedUserCount = 0;
            ViewBag.NonFormatedUserCount = 0;

            string mainListJson = "formatedUsers-all.json";

            string path1 = Path.Combine(basePath, mainListJson);

            var mainList = new List<MoodleUser>();

            if (!string.IsNullOrEmpty(path1))
            {
                var fileJson = ReadFile(path1);
                mainList = js.Deserialize<List<MoodleUser>>(fileJson);
            }
            if (mainList.Any())
            {
                var newList = mainList.Where(t => t.id == null);
                System.IO.File.WriteAllText(basePath + "ids-null.json", js.Serialize(newList));
            }

            return View("Index");
        }

        /// <summary>
        /// Provides which buttons are shown as active on the index page.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private bool FileCreateStatus(int type = 0)
        {
            bool result = false;
            switch (type)
            {
                case 0:
                    var formatedJsonFileIsExist = System.IO.File.Exists(AppDomain.CurrentDomain.BaseDirectory + "formatedUsers.json");
                    var baseCsvFileIsExist = System.IO.File.Exists(AppDomain.CurrentDomain.BaseDirectory + "users.csv");
                    result = baseCsvFileIsExist && (formatedJsonFileIsExist == false);
                    break;
                case 1:
                    result = System.IO.File.Exists(AppDomain.CurrentDomain.BaseDirectory + "formatedUsers.json");
                    break;
                default:
                    break;
            }
            return result;
        }

        private List<MoodleCreateUserResponse> CreateMoodleUser(MoodleUser user, out MoodleException exception)
        {
            var result = new List<MoodleCreateUserResponse>();
            exception = null;

            string serviceType = "core_user_create_users";
            string requestUrl = $"{serviceUrl}&wsfunction={serviceType}&moodlewsrestformat=json";

            user.username = user.username.Replace("ç", "c").Replace("ö", "o").Replace("ş", "s").Replace("ü", "u");
            user.password = user.password.Replace("ç", "c").Replace("ö", "o").Replace("ş", "s").Replace("ü", "u");
            user.email = user.email.Replace("ç", "c").Replace("ö", "o").Replace("ş", "s").Replace("ü", "u");
            // Call Moodle REST Service
            string postData = $"users[0][username]={user.username}&users[0][password]={user.password}&users[0][firstname]={user.firstname}&users[0][lastname]={user.lastname}&users[0][email]={user.email}";

            HttpStatusCode httpStatusCode;
            var serviceResult = HttpClientPostData(requestUrl, postData, out httpStatusCode);

            // Bad Result
            if (serviceResult.Contains("exception"))
            {
                // Error
                exception = new MoodleException();
                exception = js.Deserialize<MoodleException>(serviceResult);
                return null;
            }

            // Good Result
            result = js.Deserialize<List<MoodleCreateUserResponse>>(serviceResult);
            return result;
        }

        private bool AddRoleMoodleUser(int userID, int roleID, out MoodleException exception)
        {
            var result = new List<MoodleCreateUserResponse>();
            exception = null;

            string serviceType = "core_role_assign_roles";
            string requestUrl = $"{serviceUrl}&wsfunction={serviceType}&moodlewsrestformat=json";

            // Call Moodle REST Service
            string postData = $"&assignments[0][roleid]={roleID}&assignments[0][userid]={userID}";

            HttpStatusCode httpStatusCode;
            var serviceResult = HttpClientPostData((requestUrl + postData), "", out httpStatusCode);

            try
            {
                // Bad Result
                if (serviceResult.Contains("exception"))
                {
                    exception = new MoodleException();
                    exception = js.Deserialize<MoodleException>(serviceResult);
                    return false;
                }
            }
            catch
            {
                return false;
            }


            return true;
        }
        private bool CourseEnrolmentUser(int courseID, string userID, int userRole, out MoodleException exception)
        {
            /*{MOODLE_URL}/webservice/rest/server.php?wstoken={TOKEN}&moodlewsrestformat=json&wsfunction=enrol_manual_enrol_users&enrolments[0][roleid]=5&enrolments[0][userid]=51&enrolments[0][courseid]=4*/


            string serviceType = "enrol_manual_enrol_users";
            string requestUrl = $"{serviceUrl}&wsfunction={serviceType}&moodlewsrestformat=json";

            // Call Moodle REST Service
            string parameters = $"&enrolments[0][courseid]={courseID}";
            parameters += $"&enrolments[0][userid]={userID}";
            parameters += $"&enrolments[0][roleid]={(int)userRole}";

            exception = null;
            try
            {
                HttpStatusCode httpStatusCode;
                var serviceResult = HttpClientPostData((requestUrl + parameters), "", out httpStatusCode);
                if (serviceResult.Contains("exception"))
                {
                    exception = new MoodleException();
                    exception = js.Deserialize<MoodleException>(serviceResult);
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string HttpClientPostData(string url, string postData, out HttpStatusCode statusCode)
        {
            // Call Moodle REST Service
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "POST";
            req.ContentType = "application/x-www-form-urlencoded";

            // Encode the parameters as form data:
            byte[] formData = Encoding.UTF8.GetBytes(postData);
            req.ContentLength = formData.Length;

            // Write out the form Data to the request:
            using (Stream post = req.GetRequestStream())
            {
                post.Write(formData, 0, formData.Length);
            }

            // Get the Response
            using (var resp = (HttpWebResponse)req.GetResponse())
            {
                statusCode = resp.StatusCode;
                using (var resStream = resp.GetResponseStream())
                {
                    using (var reader = new StreamReader(resStream))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
        }

        private static string GetUserCsvFileFullPath()
        {
            string relativePath = "users.csv";
            string fullPath = Path.Combine(basePath, relativePath);
            return fullPath;
        }
        private static string GetFormattedUsersFileFullPath()
        {
            string relativePath = "formatedUsers.json";
            string fullPath = Path.Combine(basePath, relativePath);
            return fullPath;
        }
        private static string ReadFile(string path)
        {
            using (StreamReader sr = new StreamReader(path))
            {
                return sr.ReadToEnd();
            }
        }
    }
}