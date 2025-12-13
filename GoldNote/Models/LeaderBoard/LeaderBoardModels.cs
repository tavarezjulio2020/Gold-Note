using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System;

namespace GoldNote.Models.LeaderBoard
{
    // --- Data Models ---
    public class ClassLeaderboardViewModel
    {
        public string ClassName { get; set; }
        public string ClassCode { get; set; }
        public List<LeaderboardEntry> Entries { get; set; }
    }

    public class LeaderboardEntry
    {
        public int Rank { get; set; }
        public string StudentName { get; set; }
        public int TotalSeconds { get; set; }
        public bool IsCurrentUser { get; set; }
    }

    // --- Logic / Repository ---
    // REMOVED ": Controller" - This is just a standard class now
    public class LeaderBoardRepository
    {
        private readonly string _connectionString;

        public LeaderBoardRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public List<ClassLeaderboardViewModel> GetLeaderboards(string studentProfileId)
        {
            var leaderboards = new List<ClassLeaderboardViewModel>();

            // 1. Logic to get dates
            DateTime today = DateTime.Today;
            int diff = (7 + (today.DayOfWeek - DayOfWeek.Sunday)) % 7;
            DateTime sundayStart = today.AddDays(-1 * diff);
            DateTime saturdayEnd = sundayStart.AddDays(7);

            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();

                // 2. Get Classes
                string sqlClasses = @"
                    SELECT c.classRoom_id, c.classRoom_Name, c.join_Code
                    FROM studentInClass sic
                    JOIN learn_instrument li ON li.learn_id = sic.student_instrument_id
                    JOIN classRoom c ON c.classRoom_id = sic.classroom_id
                    WHERE li.person_id = @StudentId";

                var classes = new List<dynamic>();
                using (var cmd = new SqlCommand(sqlClasses, con))
                {
                    cmd.Parameters.AddWithValue("@StudentId", studentProfileId);
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            classes.Add(new { Id = (int)r["classRoom_id"], Name = r["classRoom_Name"].ToString(), Code = r["join_Code"].ToString() });
                        }
                    }
                }

                // 3. Get Rankings per class
                foreach (var cls in classes)
                {
                    var lb = new ClassLeaderboardViewModel
                    {
                        ClassName = cls.Name,
                        ClassCode = cls.Code,
                        Entries = new List<LeaderboardEntry>()
                    };

                    string sqlRank = @"
                        SELECT p.name AS StudentName, p.profile_id, ISNULL(SUM(pr.seconds), 0) as TotalTime
                        FROM studentInClass sic
                        JOIN learn_instrument li ON li.learn_id = sic.student_instrument_id
                        JOIN profile p ON p.profile_id = li.person_id
                        LEFT JOIN practice pr ON pr.learn_id = li.learn_id AND pr.startTime >= @Start AND pr.startTime < @End
                        WHERE sic.classroom_id = @ClassId
                        GROUP BY p.name, p.profile_id
                        ORDER BY TotalTime DESC";

                    using (var cmdRank = new SqlCommand(sqlRank, con))
                    {
                        cmdRank.Parameters.AddWithValue("@ClassId", cls.Id);
                        cmdRank.Parameters.AddWithValue("@Start", sundayStart);
                        cmdRank.Parameters.AddWithValue("@End", saturdayEnd);

                        using (var r = cmdRank.ExecuteReader())
                        {
                            int rank = 1;
                            while (r.Read())
                            {
                                string pid = r["profile_id"].ToString();
                                lb.Entries.Add(new LeaderboardEntry
                                {
                                    Rank = rank++,
                                    StudentName = r["StudentName"].ToString(),
                                    TotalSeconds = Convert.ToInt32(r["TotalTime"]),
                                    IsCurrentUser = (pid == studentProfileId)
                                });
                            }
                        }
                    }
                    leaderboards.Add(lb);
                }
            }
            return leaderboards;
        }
    }
}