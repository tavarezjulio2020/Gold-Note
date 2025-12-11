using Microsoft.Extensions.Configuration;
using System.Linq; 
using System;    
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using NuGet.Protocol.Plugins;
using Azure.Core;
using GoldNote.Data;
using static System.Runtime.InteropServices.JavaScript.JSType;
using GoldNote.Models.Student;

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
                                  -- Logic: If they practiced in last 7 days, sum it. Otherwise 0.
                                  ISNULL(SUM(CASE WHEN practice.startTime > DATEADD(DAY, -7, GETDATE()) 
                                                  THEN practice.seconds 
                                                  ELSE 0 
                                             END), 0) AS 'PracticedTime'
                              FROM profile s
                              JOIN learn_instrument li ON li.person_id = s.profile_id
                              JOIN instruments i ON i.inst_id = li.instrument_id
                              JOIN studentInClass sic ON sic.student_instrument_id = li.learn_id
                              JOIN classRoom cri ON cri.classRoom_id = sic.classroom_id
                              JOIN profile t ON t.profile_id = cri.teacher_id
                              
                              -- CHANGED TO LEFT JOIN: Keeps students even if they haven't practiced
                              LEFT JOIN practice ON practice.learn_id = li.learn_id

                              WHERE t.profile_id = @userId
                              
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
                        string sqlClassroom = @"  INSERT INTO classRoom (classRoom_Name, join_Code, teacher_id)
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
                        string sqlRole = @" IF NOT EXISTS (SELECT 1 FROM profile_Role WHERE account_id = @AccountId AND role_id = 2)
                                            BEGIN
                                                INSERT INTO profile_Role (account_id, role_id)
                                                VALUES (@AccountId, 2);
                                            END
                        ";

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

        public int GetClassID(string teacherID)
        {
            string sql = @"SELECT classRoom_id
                   FROM classRoom
                   Where teacher_id = @teacherID
            ";

            using var con = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, con);

            cmd.Parameters.AddWithValue("@teacherID", teacherID);

            con.Open();

            object result = cmd.ExecuteScalar();

            if (result != null)
            {
                int classId = Convert.ToInt32(result);
                return classId;
            }
            else
            {
                return 0;
            }
        }

        // Recommended changes for your utility/repository methods:

        public int AcceptStudentWithInstrumnet(int classRoomId, int studentLearnId)
        {
            string sql = @"INSERT INTO studentInClass (classroom_id, student_instrument_id)
                       VALUES (@classRoomId, @student)";

            using var con = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, con);

            cmd.Parameters.AddWithValue("@classRoomId", classRoomId);
            cmd.Parameters.AddWithValue("@student", studentLearnId);

            con.Open();

            // Executes the command and returns the number of rows affected (should be 1 on success)
            return cmd.ExecuteNonQuery();
        }

        public int DeleteRequest(int requestId)
        {
            string sql = @"DELETE FROM classRoomrequest
                       WHERE crrID = @requestId";

            using var con = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, con);

            cmd.Parameters.AddWithValue("@requestId", requestId);

            con.Open();

            // Executes the command and returns the number of rows affected (should be 1 on success)
            return cmd.ExecuteNonQuery();
        }








        //cesar work HERE
        // 1. GET Assignments (Using your specific SQL script)
        public List<AssignmentDetails> GetAssignments(int studentLearnId)
        {
            List<AssignmentDetails> list = new List<AssignmentDetails>();

            // Note: I corrected 'desctription' to 'description' assuming it was a typo, 
            // but if your column is actually named 'desctription', please change it back below!
            string sql = @"
        SELECT 
            a.assignment_id,
            a.title,
            a.desctription, -- Ensure this matches your DB column name
            (SELECT COUNT(*) FROM practiced_assignments pa 
             JOIN practice p ON p.practice_id = pa.practice_id
             WHERE pa.assignment_id = a.assignment_id 
             AND p.startTime > DATEADD(DAY, -7, GETDATE())) as PracticeCount
        FROM assignment a
        JOIN assigned_assignments aa ON aa.assignment_id = a.assignment_id
        JOIN learn_instrument li ON li.learn_id = aa.learn_id
        WHERE li.learn_id = @LearnId
        ORDER BY a.title DESC";

            using (var con = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@LearnId", studentLearnId);
                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new AssignmentDetails
                        {
                            AssignmentId = Convert.ToInt32(reader["assignment_id"]),
                            Title = reader["title"].ToString(),
                            // Mapping 'desctription' column to TeacherNotes property
                            TeacherNotes = reader["desctription"] != DBNull.Value ? reader["desctription"].ToString() : "",
                            TimesPracticedThisWeek = reader["PracticeCount"] != DBNull.Value ? Convert.ToInt32(reader["PracticeCount"]) : 0
                        });
                    }
                }
            }
            return list;
        }

        // 2. ADD Assignment (Needs to insert into TWO tables now)
        public void AddAssignment(int studentLearnId, string title, string notes)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        // Step 1: Insert the Assignment and get the new ID
                        string insertAssignSql = @"
                    INSERT INTO assignment (title, desctription) 
                    OUTPUT INSERTED.assignment_id
                    VALUES (@Title, @Notes)";

                        int newAssignmentId;

                        using (var cmd = new SqlCommand(insertAssignSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Title", title);
                            cmd.Parameters.AddWithValue("@Notes", notes ?? (object)DBNull.Value);
                            newAssignmentId = (int)cmd.ExecuteScalar();
                        }

                        // Step 2: Link it to the student in assigned_assignments
                        string insertLinkSql = @"
                    INSERT INTO assigned_assignments (assignment_id, learn_id) 
                    VALUES (@AssignId, @LearnId)";

                        using (var cmd = new SqlCommand(insertLinkSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@AssignId", newAssignmentId);
                            cmd.Parameters.AddWithValue("@LearnId", studentLearnId);
                            cmd.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw; // Re-throw to handle in controller
                    }
                }
            }
        }

        // 3. EDIT Assignment (Updates the main assignment table)
        public int UpdateAssignment(int assignmentId, string title, string notes)
        {
            string sql = @"UPDATE assignment 
                   SET title = @Title, desctription = @Notes 
                   WHERE assignment_id = @AssignmentId";

            using var con = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, con);

            cmd.Parameters.AddWithValue("@AssignmentId", assignmentId);
            cmd.Parameters.AddWithValue("@Title", title);
            cmd.Parameters.AddWithValue("@Notes", notes ?? (object)DBNull.Value);

            con.Open();
            return cmd.ExecuteNonQuery();
        }

        // 4. DELETE Assignment (Must delete links first to avoid foreign key errors)
        public void DeleteAssignment(int assignmentId)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        // Step 1: Delete from practiced_assignments (if history exists)
                        string delPracticeSql = "DELETE FROM practiced_assignments WHERE assignment_id = @Id";
                        using (var cmd = new SqlCommand(delPracticeSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Id", assignmentId);
                            cmd.ExecuteNonQuery();
                        }

                        // Step 2: Delete from assigned_assignments (the link to student)
                        string delLinkSql = "DELETE FROM assigned_assignments WHERE assignment_id = @Id";
                        using (var cmd = new SqlCommand(delLinkSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Id", assignmentId);
                            cmd.ExecuteNonQuery();
                        }

                        // Step 3: Delete the actual assignment
                        string delAssignSql = "DELETE FROM assignment WHERE assignment_id = @Id";
                        using (var cmd = new SqlCommand(delAssignSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Id", assignmentId);
                            cmd.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }













    }
}
