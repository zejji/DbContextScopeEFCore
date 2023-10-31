using System.Collections.Generic;

namespace Zejji.Tests.Models
{
    internal class Course
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;

        // Navigation properties
        public ICollection<CourseUser> CoursesUsers { get; set; } = default!;
    }
}
