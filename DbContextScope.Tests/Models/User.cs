namespace DbContextScope.Tests.Models
{
    internal class User
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;

        // Navigation properties
        public ICollection<CourseUser> CoursesUsers { get; set; } = default!;
    }
}