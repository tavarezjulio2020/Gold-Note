using Azure.Identity;
using GoldNote.Models.Student;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace GoldNote.Data
{
	public class User {
		public int Id { get; set; }
		public string Unique_id { get; set; }
		public string Name { get; set; }
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
            string sql = @" SELECT name, profile_id, account_id
							FROM profile
							INNER JOIN account ON account.id = profile.account_id
							WHERE account.userName = @username
							  AND account.passcode = @pass;
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

                // --- MAPPING START ---
                // Map SQL "name" -> User.Name
                foundUser.Name = reader["name"].ToString();

                // Map SQL "account_id" -> User.Id
                // We use Convert.ToInt32 to ensure it is an int
                foundUser.Id = Convert.ToInt32(reader["account_id"]);

                // Map SQL "profile_id" -> User.Unique_id
                foundUser.Unique_id = reader["profile_id"].ToString();
                // --- MAPPING END ---
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
									li.instrument_id = @inst;
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
    }

}
