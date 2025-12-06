using Azure.Identity;
using GoldNote.Data;
using GoldNote.Models.Student;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace GoldNote.Models.Student
{

    public class Teacher
	{
		public int TeacherId {  get; set; }
		public string Name {  get; set; }
		public string InstName {  get; set; } 

	} 
    public class InstrumentItem
    {
        public string instrumentName { get; set; }
        public int instrumentId { get; set; }
        public int learnID { get; set; }
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
			string sql = @"Select	t.account_id,
									t.name,
									i.inst_name
						   FROM		profile t
						   JOIN		classRoom cr
						   		ON	cr.teacher_id = t.profile_id
						   JOIN studentInClass sic 
						   		ON	sic.classroom_id = cr.classRoom_id
						   JOIN		learn_instrument li
						   		ON	li.learn_id = sic.student_instrument_id
						   JOIN instruments i
						   		ON	i.inst_id= li.instrument_id
						   WHERE li.person_id = @studentID
			";

            using var con = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, con);

            // 3. Add parameters to prevent SQL injection
            // Ensure the "@parameterName" matches what is inside your string sql above
            cmd.Parameters.AddWithValue("@studentID", userID);

            con.Open();
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                data.Add(new Teacher
                {
                    TeacherId = Convert.ToInt32(reader["account_id"]),
                    Name = reader["name"].ToString(),
                    InstName = reader["inst_name"].ToString()
                });
            }
            return data;

        }
        public List<InstrumentItem> getAllInstruments()
        {
            List<InstrumentItem> data = new List<InstrumentItem>();
            string sql = @"Select * FROM instruments";


            using var con = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, con);

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

        public List<InstrumentItem> getStudentInstruments(string userId)
        {
            List<InstrumentItem> data = new List<InstrumentItem>();
            string sql = @"	SELECT i.inst_name, inst_id, li.learn_id
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
                    instrumentName = reader["inst_name"].ToString(),
                    learnID = Convert.ToInt32(reader["learn_id"])
                });
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
							WHERE li.person_id = @personID
                            AND inst_id = @instrument;
            ";

            using var con = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, con);

            // 3. Add parameters to prevent SQL injection
            // Ensure the "@parameterName" matches what is inside your string sql above
            cmd.Parameters.AddWithValue("@personID", personId);
            cmd.Parameters.AddWithValue("@intrument", instId);

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

        // Inside StudentModel.cs

        // ... (Your existing methods)

        public async Task UpdateStudentInstruments(string userIdString, List<int> selectedInstrumentIds)
        {
            // 1. Convert the user ID string to the correct type (string, based on your other methods)
            string personId = userIdString;

            // Use a StringBuilder to create a comma-separated list of instrument IDs for the SQL 'IN' clause
            string instrumentIdList = selectedInstrumentIds.Any()
                                        ? string.Join(", ", selectedInstrumentIds)
                                        : "NULL"; // Handle case where no instruments are selected

            using var con = new SqlConnection(_connectionString);
            await con.OpenAsync();

            // Use a transaction to ensure all changes (DELETE and INSERTs) succeed or fail together
            using var transaction = con.BeginTransaction();
            try
            {
                // 1. DELETE: Remove all instruments the user is CURRENTLY learning that are NOT in the new list.
                string deleteSql = $@"
            DELETE FROM learn_instrument 
            WHERE person_id = @personID 
            AND instrument_id NOT IN ({instrumentIdList}) 
            {(selectedInstrumentIds.Any() ? "" : "AND instrument_id IS NOT NULL")};
        ";
                using (var deleteCmd = new SqlCommand(deleteSql, con, transaction))
                {
                    deleteCmd.Parameters.AddWithValue("@personID", personId);
                    // Note: @personID is used, instrumentIdList is interpolated since SQL IN clauses don't work well with parameters
                    await deleteCmd.ExecuteNonQueryAsync();
                }

                // 2. INSERT: Add instruments from the new list that the user is NOT already learning.
                // This is where we iterate over the selected IDs and add them if they don't exist.

                // SQL query to check existence and insert if not found (prevents duplicates)
                string insertSql = @"
            IF NOT EXISTS (
                SELECT 1 FROM learn_instrument 
                WHERE person_id = @personID AND instrument_id = @instID
            )
            BEGIN
                INSERT INTO learn_instrument (person_id, instrument_id)
                VALUES (@personID, @instID);
            END
        ";

                using (var insertCmd = new SqlCommand(insertSql, con, transaction))
                {
                    insertCmd.Parameters.AddWithValue("@personID", personId);
                    var instIdParam = new SqlParameter("@instID", System.Data.SqlDbType.Int);
                    insertCmd.Parameters.Add(instIdParam);

                    // Execute the insert query for each selected instrument
                    foreach (int instId in selectedInstrumentIds)
                    {
                        instIdParam.Value = instId;
                        await insertCmd.ExecuteNonQueryAsync();
                    }
                }

                // 3. COMMIT: If all DELETEs and INSERTs succeeded, commit the transaction.
                transaction.Commit();
            }
            catch (Exception ex)
            {
                // Rollback the transaction if any query failed
                transaction.Rollback();
                // Re-throw the exception so the HomeController can catch it and return an error JSON
                throw new Exception("Error during instrument update transaction.", ex);
            }
        }

        // Inside StudentModel.cs

        public async Task requestTeacher(string code, int studentlearn, string userIdString)
        {
            // SQL to insert the request. 
            // Note: This assumes your table 'classRoomRequest' takes the raw string code and the learn_id.
            string sql = @"INSERT INTO classRoomRequest (join_Code, learn_id)
                   VALUES (@code, @studentlearn);";

            using var con = new SqlConnection(_connectionString);
            await con.OpenAsync();

            using var cmd = new SqlCommand(sql, con);

            // Add parameters to prevent SQL injection
            cmd.Parameters.AddWithValue("@code", code);
            cmd.Parameters.AddWithValue("@studentlearn", studentlearn);

            // Execute the query
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
