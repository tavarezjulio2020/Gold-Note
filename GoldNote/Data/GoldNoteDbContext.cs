using Azure.Identity;
using GoldNote.Models.Student;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Metrics;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace GoldNote.Data
{
    // Inside GoldNoteDbContext.cs

    public class User
    {
        public int Id { get; set; }
        public string Unique_id { get; set; }
        public string Name { get; set; }
        public bool IsTeacher { get; set; }

        public string Email { get; set; }
        public string Username { get; set; }
    }

    public class InstrumentItem 
    {
        public string instrumentName { get; set; }
		public int instrumentId { get; set; }
    }

	public class PracticeSessionData
	{
		public string InstrumentId { get; set; }
		public int DurationInSeconds { get; set; }
	}

	public class Assignments
	{
		public int id { get; set; }
		public string title { get; set; }
		public string description { get; set; }
	}

    public class PracticeSessionViewModel
    {
        // These property names MUST match the camelCase keys in your JavaScript `practiceData` object.
        public int InstrumentId { get; set; }
        public DateTime StartTime { get; set; }      // Will automatically parse the ISO string from JS
        public int DurationSeconds { get; set; }
        public List<int> CompletedAssignmentIds { get; set; }
    }

    public class InstrumentPracticeTime
    {
        public int InstrumentId { get; set; }
        public string InstrumentName { get; set; }
        public int TotalSecondsPracticed { get; set; }
    }

    public class UserInstrumentDTO
    {
        public string InstrumentName { get; set; }
    }

    public class UserTeacherDTO
    {
        public string TeacherName { get; set; }
        public string InstrumentName { get; set; }
    }

    public class UserRecoveryModel
    {
        public string Name { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
    }

    public class DailyPracticeStat
    {
        public string date { get; set; }
        public double minutes { get; set; }
    }

    public class AssignmentPracticeStat
    {
        public string title { get; set; }
        public int count { get; set; }
    }

    public class LearnTag
    {
        public int learn_id { get; set; }
        public int tag_id { get; set; }
        public DateTime assigned_date { get; set; } = DateTime.UtcNow;
    }

    public class Tag
    {
        public int tag_id { get; set; }
        public string tag_name { get; set; }
        public string profile_id { get; set; }
    }

    public class GoldNoteDbContext
    {
        private readonly string _connectionString;
        public GoldNoteDbContext(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }
        public void TestConnection()
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            Console.WriteLine("Connected to MySQL successfully!");
        }

        public User getUser(string username, string password)
        {
            // 1. Create a variable to hold the result (starts as null)
            User foundUser = null;

            // 2. Your SQL Query
            string sql = @" SELECT  p.name, 
                                    p.profile_id, 
                                    p.account_id, 
                                    COUNT(r.role_id) AS IsTeacher
                            FROM    profile p
                            INNER JOIN account a 
                                  ON a.id = p.account_id
                            LEFT JOIN profile_Role r 
                                  ON r.account_id = p.account_id
                            WHERE a.userName = @username
                                  AND a.passcode = @pass
                            GROUP BY p.name, p.profile_id, p.account_id;
						";

            using var con = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, con);

            // 3. Add parameters to prevent SQL injection
            // Ensure the "@parameterName" matches what is inside your string sql above
            cmd.Parameters.AddWithValue("@username", username);
            cmd.Parameters.AddWithValue("@pass", password);

            con.Open();

            // 4. Execute the reader
            using var reader = cmd.ExecuteReader();

            // 5. Check if a row was found
            if (reader.Read())
            {
                // Create the User object
                foundUser = new User();
                foundUser.Name = reader["name"].ToString();
                foundUser.Id = Convert.ToInt32(reader["account_id"]);
                foundUser.Unique_id = reader["profile_id"].ToString();
                foundUser.IsTeacher = Convert.ToInt32(reader["IsTeacher"]) > 0;
            }

            // 6. Return the found user (or null if login failed)
            return foundUser;
        }

        public List<InstrumentItem> getStudentInstruments(string userId)
        {
            List<InstrumentItem> data = new List<InstrumentItem>();
            string sql = @"	SELECT i.inst_name, inst_id
							FROM learn_instrument li
							JOIN profile p ON p.profile_id = li.person_id
							JOIN instruments i ON i.inst_id = li.instrument_id
							WHERE li.person_id = @personID;
		    ";

            using var con = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, con);

            // 3. Add parameters to prevent SQL injection
            // Ensure the "@parameterName" matches what is inside your string sql above
            cmd.Parameters.AddWithValue("@personID", userId);

            con.Open();
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                data.Add(new InstrumentItem
                {
                    instrumentId = Convert.ToInt32(reader["inst_id"]),
                    instrumentName = reader["inst_name"].ToString()
                });
            }
            return data;
        }

        public List<Assignments> getAssignmentsForStudent(string userId, int instrument)
        {
            List<Assignments> data = new List<Assignments>();
            string sql = @"	SELECT  a.assignment_id,
									a.title,
								    a.desctription
							FROM	assignment a
							JOIN	assigned_assignments aa 
								  ON  a.assignment_id = aa.assignment_id
							JOIN	learn_instrument li
								  ON	aa.learn_id = li.learn_id
							WHERE	li.person_id = @person AND
									li.instrument_id = @inst AND
                                    aa.activeLearning = 1;
							";

            using var con = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, con);

            // 3. Add parameters to prevent SQL injection
            // Ensure the "@parameterName" matches what is inside your string sql above
            cmd.Parameters.AddWithValue("@person", userId);
            cmd.Parameters.AddWithValue("@inst", instrument);

            con.Open();
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                data.Add(new Assignments
                {
                    id = Convert.ToInt32(reader["assignment_id"]),
                    title = reader["title"].ToString(),
                    description = reader["desctription"].ToString()
                });
            }
            return data;
        }

        public List<InstrumentPracticeTime> getTodaysPracticeTimes(string userId,int instrument = 0)
        {
            List<InstrumentPracticeTime> data = new List<InstrumentPracticeTime>();
            string sql = @"	SELECT
                                    SUM(p.seconds) AS TotalSeconds,
                                    i.inst_id,
                                    i.inst_name
                                FROM practice p
                                JOIN learn_instrument li ON p.learn_id = li.learn_id
                                JOIN instruments i ON li.instrument_id = i.inst_id
                                WHERE li.person_id = @person
                                  AND CAST(p.startTime AS DATE) = '2025-12-01'";
            if(instrument != 0)
            {
                sql += " AND i.inst_id = @instrument";
            }
                sql +=  @"                GROUP BY i.inst_id, i.inst_name;
				        ";

            using var con = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, con);

            // 3. Add parameters to prevent SQL injection
            // Ensure the "@parameterName" matches what is inside your string sql above
            cmd.Parameters.AddWithValue("@person", userId);
            if(instrument != 0)
            {
                cmd.Parameters.AddWithValue("@instrument", instrument);
            }

            con.Open();
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                data.Add(new InstrumentPracticeTime
                {
                    InstrumentId = Convert.ToInt32(reader["inst_id"]),
                    InstrumentName = reader["inst_name"].ToString(),
                    TotalSecondsPracticed = Convert.ToInt32(reader["TotalSeconds"].ToString())
                });
            }
            return data;
        }
        public void SavePracticeSession(string personId, PracticeSessionViewModel model)
        {
            // Use a transaction to ensure all database operations succeed together.
            using var con = new SqlConnection(_connectionString);
            con.Open();

            using var transaction = con.BeginTransaction();
            try
            {
                // 1. Insert the main practice session record and get the new ID.
                string sessionSql = @"
                       -- 1a. Find the correct learn_id based on person and instrument
                       DECLARE @LearnID INT;
                       SELECT @LearnID = li.learn_id FROM learn_instrument li
                       WHERE li.instrument_id = @inst AND li.person_id = @person;

                       -- 1b. Insert the main practice record
                       INSERT INTO practice (seconds, startTime, learn_id)
                       VALUES (@duration, @start, @LearnID);
                       
                       -- 1c. Return the ID of the newly inserted practice record
                       SELECT SCOPE_IDENTITY();
                ";

                int practiceSessionId;
                using (var cmdSession = new SqlCommand(sessionSql, con, transaction))
                {
                    // Set parameters
                    cmdSession.Parameters.AddWithValue("@person", personId);
                    cmdSession.Parameters.AddWithValue("@inst", model.InstrumentId);
                    cmdSession.Parameters.AddWithValue("@start", model.StartTime);
                    cmdSession.Parameters.AddWithValue("@duration", model.DurationSeconds);

                    // ExecuteScalar retrieves the ID returned by SCOPE_IDENTITY()
                    // NOTE: SCOPE_IDENTITY returns a decimal type, so we convert it to an int.
                    practiceSessionId = Convert.ToInt32(cmdSession.ExecuteScalar());
                }

                // 2. Insert records for completed assignments using the new practiceSessionId.
                if (model.CompletedAssignmentIds != null && model.CompletedAssignmentIds.Count > 0)
                {
                    string assignmentSql = @"
                INSERT INTO practiced_assignments (assignment_id, practice_id)
                VALUES (@assignmentId, @sessionId);
            ";

                    foreach (int assignmentId in model.CompletedAssignmentIds)
                    {
                        using (var cmdAssignment = new SqlCommand(assignmentSql, con, transaction))
                        {
                            cmdAssignment.Parameters.AddWithValue("@sessionId", practiceSessionId);
                            cmdAssignment.Parameters.AddWithValue("@assignmentId", assignmentId);
                            cmdAssignment.ExecuteNonQuery();
                        }
                    }
                }

                // Commit the transaction if all commands succeed
                transaction.Commit();
            }
            catch (Exception ex)
            {
                // Rollback on any error
                transaction.Rollback();
                // Console.WriteLine($"Error saving practice session: {ex.Message}"); // Optional: for debugging
                throw ex; // Re-throw the exception to notify the controller and front-end
            }

        }

        public string CreateUser(string username, string password, string email, string name, string phone)
        {
            using var con = new SqlConnection(_connectionString);
            con.Open();
            using var transaction = con.BeginTransaction();

            try
            {
                // 1. CHECK (Same as before)
                string checkSql = @"
            SELECT COUNT(*) 
            FROM account a
            JOIN profile p ON a.id = p.account_id
            WHERE a.userName = @user OR p.email = @email";

                using (var cmdCheck = new SqlCommand(checkSql, con, transaction))
                {
                    cmdCheck.Parameters.AddWithValue("@user", username);
                    cmdCheck.Parameters.AddWithValue("@email", email);
                    int count = Convert.ToInt32(cmdCheck.ExecuteScalar());
                    if (count > 0) return "Username or Email already exists.";
                }

                // 2. INSERT ACCOUNT (Same as before)
                string accountSql = @"
            INSERT INTO account (userName, passcode) 
            VALUES (@user, @pass);
            SELECT SCOPE_IDENTITY();";

                int newAccountId;
                using (var cmdAccount = new SqlCommand(accountSql, con, transaction))
                {
                    cmdAccount.Parameters.AddWithValue("@user", username);
                    cmdAccount.Parameters.AddWithValue("@pass", password);
                    newAccountId = Convert.ToInt32(cmdAccount.ExecuteScalar());
                }

                // 3. INSERT PROFILE (UPDATED)
                // Added phone_number column and parameter
                string profileSql = @"
            INSERT INTO profile (profile_id, account_id, name, email, phone_number)
            VALUES (@pid, @accId, @name, @email, @phone);";

                using (var cmdProfile = new SqlCommand(profileSql, con, transaction))
                {
                    string newProfileId = Guid.NewGuid().ToString();

                    cmdProfile.Parameters.AddWithValue("@pid", newProfileId);
                    cmdProfile.Parameters.AddWithValue("@accId", newAccountId);

                    // Use the Real Name from the form
                    cmdProfile.Parameters.AddWithValue("@name", name);
                    cmdProfile.Parameters.AddWithValue("@email", email);

                    // Handle Optional Phone (Send DBNull if empty)
                    if (string.IsNullOrEmpty(phone))
                    {
                        cmdProfile.Parameters.AddWithValue("@phone", DBNull.Value);
                    }
                    else
                    {
                        cmdProfile.Parameters.AddWithValue("@phone", phone);
                    }

                    cmdProfile.ExecuteNonQuery();
                }

                transaction.Commit();
                return null;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return "Database Error: " + ex.Message;
            }
        }
        // 1. Finds a user by their email address
        public User GetUserByEmailForRecovery(string email)
        {
            User foundUser = null;
            string sql = @"
                SELECT p.name, p.email, a.userName
                FROM profile p
                JOIN account a ON p.account_id = a.id
                WHERE p.email = @email";

            using var con = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@email", email);

            con.Open();
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                foundUser = new User
                {
                    Name = reader["name"].ToString(),
                    Email = reader["email"].ToString(),
                    Username = reader["userName"].ToString()
                };
            }
            return foundUser;
        }

        // 1.5 Finds a user by their username (For Forgot Password)
        public User GetUserByUsernameForRecovery(string username)
        {
            User foundUser = null;
            string sql = @"
                SELECT p.name, p.email, a.userName
                FROM profile p
                JOIN account a ON p.account_id = a.id
                WHERE a.userName = @username";

            using var con = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@username", username);

            con.Open();
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                foundUser = new User
                {
                    Name = reader["name"].ToString(),
                    Email = reader["email"].ToString(),
                    Username = reader["userName"].ToString()
                };
            }
            return foundUser;
        }

        // 2. Saves the password reset token to the database
        public void SavePasswordResetToken(string email, string token, DateTime expiry)
        {
            string sql = @"
        UPDATE account
        SET reset_token = @token, reset_token_expiry = @expiry
        WHERE id = (SELECT account_id FROM profile WHERE email = @email)";

            using var con = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@email", email);
            cmd.Parameters.AddWithValue("@token", token);
            cmd.Parameters.AddWithValue("@expiry", expiry);

            con.Open();
            cmd.ExecuteNonQuery();
        }

        // 3. Verifies the token and updates the password
        public bool ResetPasswordWithToken(string token, string newPassword)
        {
            string sql = @"
        UPDATE account
        SET passcode = @pass, reset_token = NULL, reset_token_expiry = NULL
        WHERE reset_token = @token AND reset_token_expiry > @now";

            using var con = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@token", token);
            cmd.Parameters.AddWithValue("@pass", newPassword);
            cmd.Parameters.AddWithValue("@now", DateTime.Now);

            con.Open();
            int rowsAffected = cmd.ExecuteNonQuery();

            // Returns true if the password was successfully updated
            return rowsAffected > 0;
        }
        public List<UserInstrumentDTO> GetInstrumentsByUserId(int userId)
        {
            // This query assumes a table 'user_instrument' links the profile/account to the 'instrument' table
            string sql = @"
                            SELECT i.instrumentName 
                            FROM instrument i
                            JOIN user_instrument ui ON i.instrumentId = ui.instrumentId
                            JOIN profile p ON ui.profile_id = p.profile_id
                            WHERE p.account_id = @userId";

            // (Implementation: Execute query and map results to UserInstrumentDTO)
            // ...
            // Placeholder return:
            return new List<UserInstrumentDTO>();
        }
        public List<UserTeacherDTO> GetTeachersByUserId(int userId)
        {
            // This query assumes a table 'student_teacher' links the student profile to the teacher profile, 
            // and another table provides the instrument taught in that relationship.
            string sql = @"
                            SELECT 
                                t_p.name AS TeacherName, 
                                i.instrumentName 
                            FROM profile s_p -- Student Profile
                            JOIN student_teacher st ON s_p.profile_id = st.student_profile_id
                            JOIN profile t_p ON st.teacher_profile_id = t_p.profile_id -- Teacher Profile
                            JOIN instrument i ON st.instrumentId = i.instrumentId -- The Instrument they teach the student
                            WHERE s_p.account_id = @userId";

            // (Implementation: Execute query and map results to UserTeacherDTO)
            // ...
            // Placeholder return:
            return new List<UserTeacherDTO>();
        }

        public object GetStudentStatsData(int learnId, DateTime startDate, DateTime endDate)
        {
            var dailyData = new List<DailyPracticeStat>();
            var assignmentData = new List<AssignmentPracticeStat>();

            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();

                // -------------------------------------------------------------
                // QUERY 1: Daily Practice Minutes filtered by Date Range
                // -------------------------------------------------------------
                string sqlDaily = @"
                    SELECT CAST(startTime AS DATE) AS PracticeDate, SUM(seconds) AS TotalSeconds
                    FROM practice
                    WHERE learn_id = @learnId
                      AND CAST(startTime AS DATE) >= CAST(@start AS DATE) 
                      AND CAST(startTime AS DATE) <= CAST(@end AS DATE)
                    GROUP BY CAST(startTime AS DATE)
                    ORDER BY PracticeDate;";

                using (var cmdDaily = new SqlCommand(sqlDaily, con))
                {
                    cmdDaily.Parameters.AddWithValue("@learnId", learnId);
                    cmdDaily.Parameters.AddWithValue("@start", startDate);
                    cmdDaily.Parameters.AddWithValue("@end", endDate);

                    using (var reader = cmdDaily.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            dailyData.Add(new DailyPracticeStat
                            {
                                date = Convert.ToDateTime(reader["PracticeDate"]).ToString("yyyy-MM-dd"),
                                minutes = Math.Round(Convert.ToDouble(reader["TotalSeconds"]) / 60.0, 1)
                            });
                        }
                    }
                }

                // -------------------------------------------------------------
                // QUERY 2: Assignments Practiced filtered by Date Range
                // -------------------------------------------------------------
                string sqlAssign = @"
                    SELECT 
                        a.title, 
                        (
                            -- This subquery counts the practices just for this specific assignment, student, and date range
                            SELECT COUNT(pa.ID) 
                            FROM practiced_assignments pa
                            JOIN practice p ON pa.practice_id = p.practice_id
                            WHERE pa.assignment_id = a.assignment_id
                              AND p.learn_id = @learnId
                              AND CAST(p.startTime AS DATE) >= CAST(@start AS DATE) 
                              AND CAST(p.startTime AS DATE) <= CAST(@end AS DATE)
                        ) as PracticeCount
                    FROM assigned_assignments aa
                    JOIN assignment a ON aa.assignment_id = a.assignment_id
                    WHERE aa.learn_id = @learnId
                      AND aa.activeLearning = 1;";

                using (var cmdAssign = new SqlCommand(sqlAssign, con))
                {
                    cmdAssign.Parameters.AddWithValue("@learnId", learnId);
                    cmdAssign.Parameters.AddWithValue("@start", startDate);
                    cmdAssign.Parameters.AddWithValue("@end", endDate);
                    Console.WriteLine(sqlAssign);
                    using (var reader2 = cmdAssign.ExecuteReader())
                    {
                        while (reader2.Read())
                        {
                            assignmentData.Add(new AssignmentPracticeStat
                            {
                                title = reader2["title"].ToString(),
                                count = Convert.ToInt32(reader2["PracticeCount"])
                            });
                        }
                    }
                }
            }

            return new { success = true, dailyData = dailyData, assignmentData = assignmentData };
        }

        // --- NEW TAG AND BULK ASSIGNMENT METHODS ---

        public List<Tag> GetTags(string profileId)
        {
            List<Tag> tags = new List<Tag>();
            string sql = "SELECT tag_id, tag_name FROM tags WHERE profile_id = @pid";

            using var con = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@pid", profileId);

            con.Open();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                tags.Add(new Tag
                {
                    tag_id = Convert.ToInt32(reader["tag_id"]),
                    tag_name = reader["tag_name"].ToString()
                });
            }
            return tags;
        }

        public void CreateTag(string name, string profileId)
        {
            string sql = "INSERT INTO tags (tag_name, profile_id) VALUES (@name, @pid)";
            using var con = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@pid", profileId);

            con.Open();
            cmd.ExecuteNonQuery();
        }

        public bool AssignTag(int learnId, int tagId)
        {
            using var con = new SqlConnection(_connectionString);
            con.Open();

            // Check for duplicates first
            string checkSql = "SELECT COUNT(*) FROM learn_tags WHERE learn_id = @lid AND tag_id = @tid";
            using var checkCmd = new SqlCommand(checkSql, con);
            checkCmd.Parameters.AddWithValue("@lid", learnId);
            checkCmd.Parameters.AddWithValue("@tid", tagId);

            if (Convert.ToInt32(checkCmd.ExecuteScalar()) > 0)
            {
                return false; // Already tagged
            }

            // Insert new tag
            string insertSql = "INSERT INTO learn_tags (learn_id, tag_id) VALUES (@lid, @tid)";
            using var insertCmd = new SqlCommand(insertSql, con);
            insertCmd.Parameters.AddWithValue("@lid", learnId);
            insertCmd.Parameters.AddWithValue("@tid", tagId);
            insertCmd.ExecuteNonQuery();

            return true;
        }

        public void BulkAddAssignment(string targetId, string title, string description, string creatorId)
        {
            using var con = new SqlConnection(_connectionString);
            con.Open();
            using var transaction = con.BeginTransaction();

            try
            {
                // 1. Create the Assignment (Note: your DB uses 'desctription' with a typo based on your previous queries!)
                string assignSql = @"INSERT INTO assignment (title, desctription, creator_id, creationDate) 
                                     VALUES (@title, @desc, @creator, @date);
                                     SELECT SCOPE_IDENTITY();";
                int newAssignmentId;
                using (var cmd = new SqlCommand(assignSql, con, transaction))
                {
                    cmd.Parameters.AddWithValue("@title", title);
                    cmd.Parameters.AddWithValue("@desc", description);
                    cmd.Parameters.AddWithValue("@creator", creatorId);
                    cmd.Parameters.AddWithValue("@date", DateTime.UtcNow);
                    newAssignmentId = Convert.ToInt32(cmd.ExecuteScalar());
                }

                // 2. Figure out who gets the assignment
                List<int> learnIds = new List<int>();

                if (targetId == "ALL")
                {
                    string allSql = @"SELECT sic.student_instrument_id 
                                      FROM studentInClass sic
                                      JOIN classRoom c ON sic.classroom_id = c.classRoom_id
                                      WHERE c.teacher_id = @teacher";
                    using (var cmd = new SqlCommand(allSql, con, transaction))
                    {
                        cmd.Parameters.AddWithValue("@teacher", creatorId);
                        using var reader = cmd.ExecuteReader();
                        while (reader.Read()) learnIds.Add(Convert.ToInt32(reader[0]));
                    }
                }
                else if (int.TryParse(targetId, out int parsedTagId))
                {
                    string tagSql = "SELECT learn_id FROM learn_tags WHERE tag_id = @tid";
                    using (var cmd = new SqlCommand(tagSql, con, transaction))
                    {
                        cmd.Parameters.AddWithValue("@tid", parsedTagId);
                        using var reader = cmd.ExecuteReader();
                        while (reader.Read()) learnIds.Add(Convert.ToInt32(reader[0]));
                    }
                }

                // 3. Assign to students
                if (learnIds.Count > 0)
                {
                    string insertSql = "INSERT INTO assigned_assignments (assignment_id, learn_id, activeLearning) VALUES (@aid, @lid, 1)";
                    using (var cmd = new SqlCommand(insertSql, con, transaction))
                    {
                        cmd.Parameters.Add("@aid", System.Data.SqlDbType.Int);
                        cmd.Parameters.Add("@lid", System.Data.SqlDbType.Int);

                        foreach (var lid in learnIds)
                        {
                            cmd.Parameters["@aid"].Value = newAssignmentId;
                            cmd.Parameters["@lid"].Value = lid;
                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                throw ex;
            }
        }
    }

}
