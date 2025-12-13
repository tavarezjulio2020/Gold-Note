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
            string sql = @"SELECT i.inst_name, inst_id, li.learn_id
                           FROM learn_instrument li
                           JOIN profile p ON p.profile_id = li.person_id
                           JOIN instruments i ON i.inst_id = li.instrument_id
                           WHERE li.person_id = @personID";

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
            string personId = userIdString;
            string instrumentIdList = selectedInstrumentIds.Any() ? string.Join(", ", selectedInstrumentIds) : "NULL";

            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        string deleteSql = $@"DELETE FROM learn_instrument 
                                              WHERE person_id = @personID 
                                              AND instrument_id NOT IN ({instrumentIdList}) 
                                              {(selectedInstrumentIds.Any() ? "" : "AND instrument_id IS NOT NULL")};";

                        using (var deleteCmd = new SqlCommand(deleteSql, con, transaction))
                        {
                            deleteCmd.Parameters.AddWithValue("@personID", personId);
                            await deleteCmd.ExecuteNonQueryAsync();
                        }

                        string insertSql = @"IF NOT EXISTS (SELECT 1 FROM learn_instrument WHERE person_id = @personID AND instrument_id = @instID)
                                             BEGIN
                                                 INSERT INTO learn_instrument (person_id, instrument_id) VALUES (@personID, @instID);
                                             END";

                        using (var insertCmd = new SqlCommand(insertSql, con, transaction))
                        {
                            insertCmd.Parameters.AddWithValue("@personID", personId);
                            var instIdParam = new SqlParameter("@instID", System.Data.SqlDbType.Int);
                            insertCmd.Parameters.Add(instIdParam);

                            foreach (int instId in selectedInstrumentIds)
                            {
                                instIdParam.Value = instId;
                                await insertCmd.ExecuteNonQueryAsync();
                            }
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
    }
}