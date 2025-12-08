using GoldNote.Models.Student;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using NuGet.Protocol.Plugins;

namespace GoldNote.Models.Teacher
{

    public class AssignmentDetails
    {
        public int AssignmentId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string TeacherNotes { get; set; }
        public int TimesPracticedThisWeek { get; set; }
    }

    // --- 2. Model for a Single Student Row (The main expandable div) ---
    public class StudentDashboardItem
    {
        public string ProfileId { get; set; }
        public string StudentName { get; set; }
        public string InstrumentName { get; set; }
        public int secondsPracticed { get; set; }
    }
    public class classCode
    {
        public string ClassCode { get; set; }
    }
    public class Teacher
    {
        private readonly string _connectionString;
        public Teacher(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public List<StudentDashboardItem> getMyStudents(string userId)
        {
            List<StudentDashboardItem> data = new List<StudentDashboardItem>();
            string sql = @"Select s.profile_id,
                                  s.name,
                                  i.inst_name,
                                  SUM(practice.seconds) AS 'PracticedTime'
                           from profile s
                           join learn_instrument li 
                                on li.person_id = s.profile_id
                           join instruments i 
                                on i.inst_id = li.instrument_id
                           join practice 
                                ON practice.learn_id = li.learn_id
                           join studentInClass sic 
                                ON sic.student_instrument_id= li.learn_id
                           join classRoom cri 
                                ON cri.classRoom_id = sic.classroom_id
                           join profile t 
                                ON t.profile_id = cri.teacher_id
                           Where t.profile_id = @userId and practice.startTime > DATEADD(DAY, -7, GETDATE())
                           GROUP BY s.profile_id, s.name, i.inst_name
            ";
            using var con = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, con);

            cmd.Parameters.AddWithValue("userId", userId);

            con.Open();
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                data.Add(new StudentDashboardItem()
                {
                    ProfileId = reader["profile_id"].ToString(),
                    StudentName = reader["name"].ToString(),
                    InstrumentName = reader["inst_name"].ToString(), // <-- ADDED THIS LINE
                    secondsPracticed = Convert.ToInt32(reader["PracticedTime"])
                });
            }
            return data;
        }

        public classCode GetClassCode(string userId)
        {
            classCode data = null;

            string sql = @" SELECT join_Code 
                            FROM classRoom 
                            WHERE teacher_id = @teacherID
            ";

            using var con = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, con);

            cmd.Parameters.AddWithValue("@teacherID", userId);

            con.Open();
            using var reader = cmd.ExecuteReader();

            if (reader.Read()) // only read one row
            {
                data = new classCode()
                {
                    ClassCode = reader["join_Code"].ToString()
                };
            }
            return data; // returns null if no row found
        }

    }
}
