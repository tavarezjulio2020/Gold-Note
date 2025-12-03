using GoldNote.Models.Student;

namespace GoldNote.Models.Teacher
{
    public class Teacher
    {
        public int Id { get; set; } // Primary Key
        public string Name { get; set; } // The teacher's full name
        // ... other properties (e.g., Email, Phone, Bio)

        // Navigation property for the many-to-many relationship
        public ICollection<StudentTeacherInstrument> StudentAssignments { get; set; }
    }
    public class Instrument
    {
        public int Id { get; set; }  
        public string Name { get; set; }  
        public ICollection<StudentTeacherInstrument> Assignments { get; set; }

    }

    public class StudentTeacherInstrument
    { 
        public int Id { get; set; }

         public string StudentId { get; set; }
        public StudentModel Student { get; set; }  

        public int TeacherId { get; set; }
        public Teacher Teacher { get; set; } 
         public int InstrumentId { get; set; }
        public Instrument Instrument { get; set; }  
    }
}
