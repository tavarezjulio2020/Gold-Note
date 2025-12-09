using Microsoft.Extensions.Configuration;
using System.Linq; 
using System;    
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using NuGet.Protocol.Plugins;
using Azure.Core;
using GoldNote.Data;
using static System.Runtime.InteropServices.JavaScript.JSType;

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

    public class ClassroomRequest
    {
        public int RequestId { get; set; } // Matches crr.crid
        public string StudentName { get; set; } // Matches s.name
        public string InstrumentName { get; set; }
        public int LearnId { get; set; }
        public int ClassroomId { get; set; }
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

        // Inside GoldNote.Models.Teacher class

        // Helper method to generate the random 9-character string
        private string GenerateRandomCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();

            // Generate 9 random characters
            var code = new string(Enumerable.Repeat(chars, 9)
              .Select(s => s[random.Next(s.Length)]).ToArray());

            return code;
        }

        // New method to generate a unique code using a database check loop
        private string GenerateUniqueJoinCode()
        {
            string joinCode;
            bool codeExists = true;

            // Loop until we generate a code that doesn't exist in the database
            while (codeExists)
            {
                joinCode = GenerateRandomCode();

                // Check the database
                string sql = "SELECT COUNT(*) FROM classRoom WHERE join_Code = @JoinCode";

                using var con = new SqlConnection(_connectionString);
                using var cmd = new SqlCommand(sql, con);

                cmd.Parameters.AddWithValue("@JoinCode", joinCode);

                con.Open();

                // ExecuteScalar returns the first column of the first row (the COUNT)
                int count = (int)cmd.ExecuteScalar();

                if (count == 0)
                {
                    // The code is unique! Exit the loop.
                    codeExists = false;
                    return joinCode;
                }
                // If count > 0, codeExists remains true, and the loop repeats.
            }

            // This part of the code should technically be unreachable if the loop is correct, 
            // but a compiler might require a return statement outside the loop.
            throw new InvalidOperationException("Failed to generate a unique join code after multiple attempts.");
        }

        // Updated CreateClassroom method to use the unique code generator
        public string CreateClassroom(string teacherId, string classroomName, string selectedPlan = "Promotional")
        {
            string joinCode = GenerateUniqueJoinCode();

            if (!Guid.TryParse(teacherId, out Guid teacherGuid))
            {
                throw new ArgumentException("Invalid teacher ID format.");
            }

            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();

                // 🛑 NEW STEP: Get the INT account_id from the profile table
                int accountIdInt = 0;
                string sqlGetAccountId = "SELECT account_id FROM profile WHERE profile_id = @ProfileGuid";
                using (var cmdLookup = new SqlCommand(sqlGetAccountId, con)) // No transaction needed yet
                {
                    cmdLookup.Parameters.Add("@ProfileGuid", System.Data.SqlDbType.UniqueIdentifier).Value = teacherGuid;
                    // ExecuteScalar retrieves the single INT value
                    object result = cmdLookup.ExecuteScalar();
                    if (result != null)
                    {
                        accountIdInt = Convert.ToInt32(result);
                    }
                    else
                    {
                        throw new InvalidOperationException("Profile ID not found in the database.");
                    }
                }
                // 🛑 accountIdInt now holds the correct INT ID for the profile_Role table

                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        // --- STEP A: Create the Classroom (Uses GUID) ---
                        string sqlClassroom = @" 
                    INSERT INTO classRoom (classRoom_Name, join_Code, teacher_id)
                    VALUES (@ClassroomName, @JoinCode, @TeacherId);
                ";
                        using (var cmdClass = new SqlCommand(sqlClassroom, con, transaction))
                        {
                            cmdClass.Parameters.AddWithValue("@ClassroomName", classroomName);
                            cmdClass.Parameters.AddWithValue("@JoinCode", joinCode);
                            cmdClass.Parameters.Add("@TeacherId", System.Data.SqlDbType.UniqueIdentifier).Value = teacherGuid;
                            cmdClass.ExecuteNonQuery();
                        }

                        // --- STEP B: Assign Role ID 2 (Teacher) - NOW USES INT ID ---
                        string sqlRole = @"
                    IF NOT EXISTS (SELECT 1 FROM profile_Role WHERE account_id = @AccountId AND role_id = 2)
                    BEGIN
                        INSERT INTO profile_Role (account_id, role_id)
                        VALUES (@AccountId, 2);
                    END";

                        using (var cmdRole = new SqlCommand(sqlRole, con, transaction))
                        {
                            // **FIXED HERE: Passing the INT ID retrieved from the profile table**
                            cmdRole.Parameters.AddWithValue("@AccountId", accountIdInt);
                            cmdRole.ExecuteNonQuery();
                        }

                        // --- STEP C: Insert into the Subscriptions Table (Uses GUID) ---
                        string sqlSub = @" INSERT INTO subscriptions (profile_id, plan_type, start_date, is_active)
                                     VALUES (@ProfileId, @PlanType, GETDATE(), 1);";

                        using (var cmdSub = new SqlCommand(sqlSub, con, transaction))
                        {
                            cmdSub.Parameters.Add("@ProfileId", System.Data.SqlDbType.UniqueIdentifier).Value = teacherGuid;
                            cmdSub.Parameters.AddWithValue("@PlanType", selectedPlan);
                            cmdSub.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        // Log ex.Message here
                        throw;
                    }
                }
            } 
            return joinCode;
        }

        public List<ClassroomRequest> GetPendingRequests(string teacherId)
        {
            List<ClassroomRequest> data = new();
            string sql = @"SELECT  crr.crrID AS RequestId, 
                                    s.name AS StudentName, 
                                    i.inst_name AS InstrumentName,
                                    li.learn_id AS LearnId,
                                    c.classRoom_id AS ClassroomId 
                                FROM classRoomRequest crr
                                JOIN learn_instrument li ON li.learn_id = crr.learn_id
                                JOIN profile s ON li.person_id = s.profile_id
                                JOIN instruments i ON i.inst_id = li.instrument_id
                                JOIN classRoom c ON c.join_Code = crr.join_Code
                                WHERE c.teacher_id =  @teacherId
            ";
            using var con = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, con);

            cmd.Parameters.AddWithValue("teacherId", teacherId);

            con.Open();
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                data.Add(new ClassroomRequest()
                {
                    RequestId = Convert.ToInt32(reader["RequestId"]),
                    StudentName = reader["StudentName"].ToString(),
                    InstrumentName = reader["InstrumentName"].ToString(),
                    LearnId = Convert.ToInt32(reader["LearnId"]),
                    ClassroomId = Convert.ToInt32(reader["ClassroomId"])
                });
            }
            return data;
        }

    }
}
