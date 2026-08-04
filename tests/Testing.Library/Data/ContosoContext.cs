using Microsoft.EntityFrameworkCore;
using Regira.Entities.Attachments.Models;
using Testing.Library.Contoso;

namespace Testing.Library.Data;

public class ContosoContext(DbContextOptions<ContosoContext> options) : DbContext(options)
{
    public DbSet<Attachment> Attachments { get; set; } = null!;
    public DbSet<Course> Courses { get; set; } = null!;
    public DbSet<CourseAttachment> CourseAttachments { get; set; } = null!;
    public DbSet<Department> Departments { get; set; } = null!;
    public DbSet<Enrollment> Enrollments { get; set; } = null!;
    public DbSet<Instructor> Instructors { get; set; } = null!;
    public DbSet<OfficeAssignment> OfficeAssignments { get; set; } = null!;
    public DbSet<Person> Persons { get; set; } = null!;
    public DbSet<PersonAttachment> PersonAttachments { get; set; } = null!;
    public DbSet<Student> Students { get; set; } = null!;


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Course>(entity =>
        {
            entity.HasMany(e => e.Attachments)
                .WithOne()
                .HasForeignKey(e => e.ObjectId)
                .HasPrincipalKey(e => e.Id);
        });
        modelBuilder.Entity<Person>(entity =>
        {
            entity.HasMany(e => e.Attachments)
                .WithOne()
                .HasForeignKey(e => e.ObjectId)
                .HasPrincipalKey(e => e.Id);
            entity.HasMany(e => e.Departments)
                .WithOne(e => e.Administrator)
                .HasForeignKey(e => e.AdministratorId)
                .HasPrincipalKey(e => e.Id);
        });
    }
}