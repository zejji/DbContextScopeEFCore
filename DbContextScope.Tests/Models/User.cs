using System.ComponentModel.DataAnnotations;

namespace Zejji.Tests.Models
{
    internal class User
    {
        public int Id { get; set; }

        [ConcurrencyCheck]
        public string Name { get; set; } = default!;

        // Navigation properties
        public ICollection<CourseUser> CoursesUsers { get; set; } = default!;
    }
}