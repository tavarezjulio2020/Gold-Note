using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace GoldNote.Models.Student
{
    public class Teacher
    {
        public int TeacherId { get; set; }
        public string Name { get; set; }
        public string InstName { get; set; }
        public int ClassId { get; set; }
    }

    public class InstrumentItem
    {
        public string instrumentName { get; set; }
        public int instrumentId { get; set; }
        public int learnID { get; set; }
    }

    public class PracticeSummary
    {
        public string InstrumentName { get; set; }
        public int TotalSecondsPracticed { get; set; }
    }

    public class StudentModel
    {
        private readonly string _connectionString;

        public StudentModel(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public List<Teacher> getTeachers(string userID)
        {
            List<Teacher> data = new List<Teacher>();
            string sql = @"Select t.account_id, t.name, i.inst_name, cr.classRoom_id
                           FROM profile t
                           JOIN classRoom cr ON cr.teacher_id = t.profile_id
                           JOIN studentInClass sic ON sic.classroom_id = cr.classRoom_id
                           JOIN learn_instrument li ON li.learn_id = sic.student_instrument_id
                           JOIN instruments i ON i.inst_id= li.instrument_id
                           WHERE li.person_id = @studentID";

            using (var con = new SqlConnection(_connectionString))
            {
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@studentID", userID);
                    con.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            data.Add(new Teacher
                            {
                                TeacherId = Convert.ToInt32(reader["account_id"]),
                                Name = reader["name"].ToString(),
                                InstName = reader["inst_name"].ToString(),
                                ClassId = Convert.ToInt32(reader["classRoom_id"])
                            });
                        }
                    }
                }
            }
            return data;
        }

        public List<InstrumentItem> getAllInstruments()
        {
            List<InstrumentItem> data = new List<InstrumentItem>();
            string sql = @"Select * FROM instruments";

            using (var con = new SqlConnection(_connectionString))
            {
                using (var cmd = new SqlCommand(sql, con))
                {
                    con.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            data.Add(new InstrumentItem
                            {
                                instrumentId = Convert.ToInt32(reader["inst_id"]),
                                instrumentName = reader["inst_name"].ToString()
                            });
                        }
                    }
                }
            }
            return data;
        }

        public List<InstrumentItem> getStudentInstruments(string userId)
        {
            List<InstrumentItem> data = new List<InstrumentItem>();
            string sql = @"SELECT i.inst_name, i.inst_id, li.learn_id
                   FROM learn_instrument li
                   JOIN profile p 
                        ON p.profile_id = li.person_id
                   JOIN instruments i 
                        ON i.inst_id = li.instrument_id
                   WHERE li.person_id = @personID AND li.actively_learning = 1";

            using (var con = new SqlConnection(_connectionString))
            {
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@personID", userId);
                    con.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            data.Add(new InstrumentItem
                            {
                                instrumentId = Convert.ToInt32(reader["inst_id"]),
                                instrumentName = reader["inst_name"].ToString(),
                                learnID = Convert.ToInt32(reader["learn_id"])
                            });
                        }
                    }
                }
            }
            return data;
        }

        public List<InstrumentItem> learningInst(string personId, int instId)
        {
            List<InstrumentItem> data = new List<InstrumentItem>();
            string sql = @"SELECT i.inst_name, inst_id
                           FROM learn_instrument li
                           JOIN profile p ON p.profile_id = li.person_id
                           JOIN instruments i ON i.inst_id = li.instrument_id
                           WHERE li.person_id = @personID AND inst_id = @instrument";

            using (var con = new SqlConnection(_connectionString))
            {
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@personID", personId);
                    cmd.Parameters.AddWithValue("@instrument", instId); // Fixed typo from "@intrument"
                    con.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            data.Add(new InstrumentItem
                            {
                                instrumentId = Convert.ToInt32(reader["inst_id"]),
                                instrumentName = reader["inst_name"].ToString()
                            });
                        }
                    }
                }
            }
            return data;
        }

        public async Task UpdateStudentInstruments(string userIdString, List<int> selectedInstrumentIds)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        // STEP 1: Soft-delete everything. 
                        // Set actively_learning = 0 for ALL instruments connected to this user.
                        string resetSql = "UPDATE learn_instrument SET actively_learning = 0 WHERE person_id = @personID";

                        using (var resetCmd = new SqlCommand(resetSql, con, transaction))
                        {
                            resetCmd.Parameters.AddWithValue("@personID", userIdString);
                            await resetCmd.ExecuteNonQueryAsync();
                        }

                        // STEP 2: Reactivate existing or Insert new instruments
                        // Loop through the list of checked boxes from the front end.
                        if (selectedInstrumentIds != null && selectedInstrumentIds.Any())
                        {
                            string upsertSql = @"
                        IF EXISTS (SELECT 1 FROM learn_instrument WHERE person_id = @personID AND instrument_id = @instID)
                        BEGIN
                            -- If the row already exists, just turn it back on
                            UPDATE learn_instrument SET actively_learning = 1 WHERE person_id = @personID AND instrument_id = @instID
                        END
                        ELSE
                        BEGIN
                            -- If the row doesn't exist at all, insert it and default it to on
                            INSERT INTO learn_instrument (person_id, instrument_id, actively_learning) VALUES (@personID, @instID, 1)
                        END";

                            using (var upsertCmd = new SqlCommand(upsertSql, con, transaction))
                            {
                                upsertCmd.Parameters.AddWithValue("@personID", userIdString);

                                // Define the parameter once outside the loop for better performance
                                var instIdParam = new SqlParameter("@instID", System.Data.SqlDbType.Int);
                                upsertCmd.Parameters.Add(instIdParam);

                                foreach (int instId in selectedInstrumentIds)
                                {
                                    // Update the parameter value and execute for each instrument checked
                                    instIdParam.Value = instId;
                                    await upsertCmd.ExecuteNonQueryAsync();
                                }
                            }
                        }

                        // If everything above succeeds, commit the changes to the database
                        transaction.Commit();
                    }
                    catch
                    {
                        // If anything fails, roll back so we don't end up with partial updates
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public async Task requestTeacher(string code, int studentlearn, string userIdString)
        {
            string sql = @"INSERT INTO classRoomRequest (join_Code, learn_id) VALUES (@code, @studentlearn);";

            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@code", code);
                    cmd.Parameters.AddWithValue("@studentlearn", studentlearn);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public void DropClass(string userId, int classId)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                string sql = @"DELETE sic FROM studentInClass sic
                               JOIN learn_instrument li ON li.learn_id = sic.student_instrument_id
                               WHERE sic.classroom_id = @ClassId AND li.person_id = @UserId";

                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@ClassId", classId);
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    int rows = cmd.ExecuteNonQuery();
                    if (rows == 0) throw new Exception("Class not found or you are not enrolled.");
                }
            }
        }

        public List<PracticeSummary> GetTodayPracticeTime(string userId, int instrumentId)
        {
            List<PracticeSummary> list = new List<PracticeSummary>();
            string sql = @"
                            SELECT 
                                i.inst_name, 
                                SUM(p.seconds) as TotalSeconds
                            FROM practice p
                            JOIN learn_instrument li ON p.learn_id = li.learn_id
                            JOIN instruments i ON li.instrument_id = i.inst_id
                            WHERE li.person_id = @UserId
                            AND CAST(p.startTime AS DATE) = CAST(SYSUTCDATETIME() AS DATE) 
                            AND (i.inst_id = @InstId OR @InstId = 0) 
                            GROUP BY i.inst_name";

            using (var con = new SqlConnection(_connectionString))
            {
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    cmd.Parameters.AddWithValue("@InstId", instrumentId);

                    con.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new PracticeSummary
                            {
                                InstrumentName = reader["inst_name"].ToString(),
                                TotalSecondsPracticed = Convert.ToInt32(reader["TotalSeconds"])
                            });
                        }
                    }
                }
            }
            return list;
        }

        public string GetUsernameByEmail(string email)
        {
            string username = null;

            // Join the profile and account tables to find the matching username
            string sql = @"
                SELECT a.userName 
                FROM account a
                JOIN profile p ON a.id = p.account_id
                WHERE p.email = @Email
            ";

            using (var con = new SqlConnection(_connectionString))
            {
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Email", email);
                    con.Open();

                    // ExecuteScalar returns the first column of the first row (perfect for a single string)
                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        username = result.ToString();
                    }
                }
            }
            return username;
        }
    }
}