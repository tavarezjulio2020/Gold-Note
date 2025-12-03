using Azure.Identity;
using GoldNote.Data;
using GoldNote.Models.Student;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Data.SqlClient;
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
    }
}
