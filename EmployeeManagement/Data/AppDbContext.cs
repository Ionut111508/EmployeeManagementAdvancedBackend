using Microsoft.EntityFrameworkCore;
using EmployeeManagement.Entities;

namespace EmployeeManagement.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Project> Projects { get; set; }
        public DbSet<WorkNorm> WorkNorms { get; set; }
        public DbSet<Skill> Skills { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Account> Accounts { get; set; }
        public DbSet<TaskDescription> Descriptions { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<TaskItem> TaskItems { get; set; }
        public DbSet<TaskComment> TaskComments { get; set; }
        public DbSet<Period> Periods { get; set; }
        public DbSet<Timesheet> Timesheets { get; set; }
        public DbSet<EmployeeSkill> EmployeeSkills { get; set; }
        public DbSet<Allocation> Allocations { get; set; }
        public DbSet<EmployeeDepartment> EmployeeDepartments { get; set; }
        public DbSet<ProjectManager> ProjectManagers { get; set; }
        public DbSet<TaskPeriod> TaskPeriods { get; set; }
        public DbSet<ProjectPeriod> ProjectPeriods { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Project>(entity =>
            {
                entity.ToTable("Project");
                entity.HasKey(x => x.ProjectId);
                entity.Property(x => x.ProjectId).HasMaxLength(50);
                entity.Property(x => x.ProjectName).HasMaxLength(100).IsRequired();
            });

            modelBuilder.Entity<WorkNorm>(entity =>
            {
                entity.ToTable("WorkNorm");
                entity.HasKey(x => x.WorkNormId);
                entity.Property(x => x.WorkNormId).HasMaxLength(50);
                entity.Property(x => x.WorkNormName).HasMaxLength(50).IsRequired();
                entity.Property(x => x.WorkHours).HasColumnName("HoursPerDay").HasColumnType("decimal(3,1)");
            });

            modelBuilder.Entity<Skill>(entity =>
            {
                entity.ToTable("Skill");
                entity.HasKey(x => x.SkillId);
                entity.Property(x => x.SkillId).HasMaxLength(50).IsUnicode(false);
                entity.Property(x => x.SkillName).HasMaxLength(50).IsRequired();
                entity.Property(x => x.SkillLevel).HasMaxLength(50);
            });

            modelBuilder.Entity<Department>(entity =>
            {
                entity.ToTable("Department");
                entity.HasKey(x => x.DepartmentId);
                entity.Property(x => x.DepartmentId).HasMaxLength(50);
                entity.Property(x => x.DepartmentName).HasMaxLength(100).IsRequired();
            });

            modelBuilder.Entity<Account>(entity =>
            {
                entity.ToTable("Account");
                entity.HasKey(x => x.AccountId);
                entity.Property(x => x.AccountId).HasMaxLength(50);
                entity.Property(x => x.Username).HasMaxLength(50).IsRequired();
                entity.Property(x => x.Password).HasColumnName("PasswordHash").HasMaxLength(255).IsRequired();
                entity.Property(x => x.Role).HasMaxLength(30).HasDefaultValue("Employee").IsRequired();
            });

            modelBuilder.Entity<TaskDescription>(entity =>
            {
                entity.ToTable("TaskDescription");
                entity.HasKey(x => x.DescriptionId);
                entity.Property(x => x.DescriptionId).HasColumnName("TaskDescriptionId").HasMaxLength(50);
                entity.Property(x => x.TaskDescriptionText).HasColumnName("DescriptionText").HasMaxLength(500);
            });

            modelBuilder.Entity<Employee>(entity =>
            {
                entity.ToTable("Employee");
                entity.HasKey(x => x.EmployeeId);
                entity.HasIndex(x => x.AccountId).IsUnique();
                entity.Property(x => x.EmployeeId).HasMaxLength(50);
                entity.Property(x => x.LastName).HasMaxLength(50).IsRequired();
                entity.Property(x => x.FirstName).HasMaxLength(50).IsRequired();
                entity.Property(x => x.Email).HasMaxLength(100).IsRequired();
                entity.Property(x => x.PhoneNumber).HasMaxLength(50).IsRequired();
                entity.Property(x => x.AccountId).HasMaxLength(50).IsRequired();
                entity.Property(x => x.WorkNormId).HasMaxLength(50).IsRequired();
                entity.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.WorkNorm).WithMany().HasForeignKey(x => x.WorkNormId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<TaskItem>(entity =>
            {
                entity.ToTable("TaskItem");
                entity.HasKey(x => new { x.ProjectId, x.TaskId });
                entity.HasIndex(x => x.DescriptionId).IsUnique();
                entity.Property(x => x.ProjectId).HasMaxLength(50);
                entity.Property(x => x.TaskId).HasMaxLength(50);
                entity.Property(x => x.TaskName).HasMaxLength(100).IsRequired();
                entity.Property(x => x.EstimatedHours).HasColumnType("decimal(10,2)");
                entity.Property(x => x.DescriptionId).HasColumnName("TaskDescriptionId").HasMaxLength(50).IsRequired();
                entity.Property(x => x.RequiredSkillId).HasMaxLength(50).IsUnicode(false);
                entity.HasOne(x => x.Project).WithMany(x => x.TaskItems).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(x => x.Description).WithMany().HasForeignKey(x => x.DescriptionId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.RequiredSkill).WithMany().HasForeignKey(x => x.RequiredSkillId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<TaskComment>(entity =>
            {
                entity.ToTable("TaskComment");
                entity.HasKey(x => x.TaskCommentId);
                entity.Property(x => x.TaskCommentId).HasMaxLength(50);
                entity.Property(x => x.CommentText).HasMaxLength(500).IsRequired();
                entity.Property(x => x.CommentDate).HasColumnType("date");
                entity.HasOne(x => x.TaskItem).WithMany(x => x.TaskComments).HasForeignKey(x => new { x.ProjectId, x.TaskId }).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(x => x.Employee).WithMany(x => x.TaskComments).HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Period>(entity =>
            {
                entity.ToTable("Period");
                entity.HasKey(x => x.PeriodId);
                entity.Property(x => x.PeriodId).HasMaxLength(50);
                entity.Property(x => x.Year).HasMaxLength(10);
                entity.Property(x => x.Month).HasMaxLength(10);
                entity.Property(x => x.Day).HasMaxLength(10);
                entity.Property(x => x.PeriodType).HasMaxLength(50).IsRequired();
                entity.Property(x => x.EmployeeId).HasMaxLength(50).IsRequired();
                entity.HasOne(x => x.Employee).WithMany(x => x.Periods).HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Timesheet>(entity =>
            {
                entity.ToTable("Timesheet");
                entity.HasKey(x => new { x.ProjectId, x.TaskId, x.EmployeeId, x.WorkDate });
                entity.Property(x => x.WorkDate).HasColumnName("EntryDate").HasColumnType("date");
                entity.Property(x => x.WorkedHours).HasColumnName("HoursWorked").HasColumnType("decimal(4,2)");
                entity.HasOne(x => x.TaskItem).WithMany(x => x.Timesheets).HasForeignKey(x => new { x.ProjectId, x.TaskId }).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(x => x.Employee).WithMany(x => x.Timesheets).HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<EmployeeSkill>(entity =>
            {
                entity.ToTable("EmployeeSkill");
                entity.HasKey(x => new { x.EmployeeId, x.SkillId });
                entity.Property(x => x.AcquiredDate).HasColumnType("date");
                entity.HasOne(x => x.Employee).WithMany(x => x.EmployeeSkills).HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(x => x.Skill).WithMany(x => x.EmployeeSkills).HasForeignKey(x => x.SkillId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Allocation>(entity =>
            {
                entity.ToTable("Allocation");
                entity.HasKey(x => new { x.EmployeeId, x.ProjectId, x.TaskId });
                entity.Property(x => x.AllocationStartDate).HasColumnType("date").IsRequired();
                entity.Property(x => x.AllocationEndDate).HasColumnType("date");
                entity.Property(x => x.AllocatedHours).HasColumnName("HoursPerDay").HasColumnType("decimal(2,1)");
                entity.HasOne(x => x.Employee).WithMany(x => x.Allocations).HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(x => x.TaskItem).WithMany(x => x.Allocations).HasForeignKey(x => new { x.ProjectId, x.TaskId }).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<EmployeeDepartment>(entity =>
            {
                entity.ToTable("EmployeeDepartment");
                entity.HasKey(x => new { x.EmployeeId, x.DepartmentId });
                entity.Property(x => x.StartDate).HasColumnType("date").IsRequired();
                entity.Property(x => x.EndDate).HasColumnType("date");
                entity.HasOne(x => x.Employee).WithMany(x => x.EmployeeDepartments).HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(x => x.Department).WithMany(x => x.EmployeeDepartments).HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ProjectManager>(entity =>
            {
                entity.ToTable("ProjectManager");
                entity.HasKey(x => new { x.EmployeeId, x.ProjectId });
                entity.Property(x => x.StartDate).HasColumnType("date");
                entity.Property(x => x.EndDate).HasColumnType("date");
                entity.HasOne(x => x.Employee).WithMany(x => x.ProjectManagers).HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(x => x.Project).WithMany(x => x.ProjectManagers).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<TaskPeriod>(entity =>
            {
                entity.ToTable("TaskPeriod");
                entity.HasKey(x => new { x.ProjectId, x.TaskId, x.PeriodId });
                entity.HasOne(x => x.TaskItem).WithMany(x => x.TaskPeriods).HasForeignKey(x => new { x.ProjectId, x.TaskId }).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(x => x.Period).WithMany(x => x.TaskPeriods).HasForeignKey(x => x.PeriodId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ProjectPeriod>(entity =>
            {
                entity.ToTable("ProjectPeriod");
                entity.HasKey(x => new { x.ProjectId, x.PeriodId });
                entity.HasOne(x => x.Project).WithMany(x => x.ProjectPeriods).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(x => x.Period).WithMany(x => x.ProjectPeriods).HasForeignKey(x => x.PeriodId).OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
