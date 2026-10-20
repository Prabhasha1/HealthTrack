using HealthTrack.Models;
using HealthTrack.Services;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HealthTrack.Data
{
    /// <summary>
    /// Entity Framework Core database context. Inherits the Identity tables
    /// (AspNetUsers, AspNetRoles, ...) and adds the HealthTrack tables.
    /// </summary>
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        private readonly ISensitiveDataProtector _protector;

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options,
                                    ISensitiveDataProtector protector)
            : base(options)
        {
            _protector = protector;
        }

        public DbSet<WorkoutPlan> WorkoutPlans => Set<WorkoutPlan>();
        public DbSet<WorkoutExercise> WorkoutExercises => Set<WorkoutExercise>();
        public DbSet<DietPlan> DietPlans => Set<DietPlan>();
        public DbSet<DietMeal> DietMeals => Set<DietMeal>();
        public DbSet<WorkoutLog> WorkoutLogs => Set<WorkoutLog>();
        public DbSet<MealLog> MealLogs => Set<MealLog>();
        public DbSet<HealthMeasurement> HealthMeasurements => Set<HealthMeasurement>();
        public DbSet<Appointment> Appointments => Set<Appointment>();
        public DbSet<TrainerFeedback> TrainerFeedbacks => Set<TrainerFeedback>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Free-text medical notes are encrypted before they are written to SQL Server
            // and decrypted when read back (ASP.NET Core Data Protection, AES-256).
            var encrypt = new ValueConverter<string, string>(
                plain => _protector.Protect(plain),
                cipher => _protector.Unprotect(cipher));

            builder.Entity<ApplicationUser>(user =>
            {
                user.HasOne(u => u.AssignedTrainer)
                    .WithMany()
                    .HasForeignKey(u => u.AssignedTrainerId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // A plan belongs to one trainer and one client. SQL Server does not allow two
            // cascading paths from AspNetUsers, so deletes of users are restricted and
            // handled explicitly by the application.
            builder.Entity<WorkoutPlan>(plan =>
            {
                plan.HasOne(p => p.Trainer).WithMany().HasForeignKey(p => p.TrainerId).OnDelete(DeleteBehavior.Restrict);
                plan.HasOne(p => p.Client).WithMany().HasForeignKey(p => p.ClientId).OnDelete(DeleteBehavior.Restrict);
                plan.HasMany(p => p.Exercises).WithOne(e => e.WorkoutPlan!).HasForeignKey(e => e.WorkoutPlanId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<DietPlan>(plan =>
            {
                plan.HasOne(p => p.Trainer).WithMany().HasForeignKey(p => p.TrainerId).OnDelete(DeleteBehavior.Restrict);
                plan.HasOne(p => p.Client).WithMany().HasForeignKey(p => p.ClientId).OnDelete(DeleteBehavior.Restrict);
                plan.HasMany(p => p.Meals).WithOne(m => m.DietPlan!).HasForeignKey(m => m.DietPlanId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<WorkoutLog>(log =>
            {
                log.HasOne(l => l.Client).WithMany().HasForeignKey(l => l.ClientId).OnDelete(DeleteBehavior.Restrict);
                log.HasOne(l => l.WorkoutPlan).WithMany().HasForeignKey(l => l.WorkoutPlanId).OnDelete(DeleteBehavior.SetNull);
                log.HasIndex(l => new { l.ClientId, l.Date });
            });

            builder.Entity<MealLog>(log =>
            {
                log.HasOne(l => l.Client).WithMany().HasForeignKey(l => l.ClientId).OnDelete(DeleteBehavior.Restrict);
                log.HasIndex(l => new { l.ClientId, l.Date });
            });

            builder.Entity<HealthMeasurement>(m =>
            {
                m.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
                m.Property(x => x.Notes).HasConversion(encrypt).HasMaxLength(2000);
                m.HasIndex(x => new { x.ClientId, x.Date });
            });

            builder.Entity<Appointment>(a =>
            {
                a.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
                a.HasOne(x => x.Trainer).WithMany().HasForeignKey(x => x.TrainerId).OnDelete(DeleteBehavior.Restrict);
                a.Property(x => x.ClientNotes).HasConversion(encrypt).HasMaxLength(2000);
                a.Property(x => x.TrainerNotes).HasConversion(encrypt).HasMaxLength(2000);
                a.HasIndex(x => new { x.TrainerId, x.StartTime });
            });

            builder.Entity<TrainerFeedback>(f =>
            {
                f.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
                f.HasOne(x => x.Trainer).WithMany().HasForeignKey(x => x.TrainerId).OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Notification>(n =>
            {
                n.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
                n.HasIndex(x => new { x.UserId, x.IsRead });
                n.HasIndex(x => x.DedupKey);
            });

            builder.Entity<ActivityLog>(l =>
            {
                l.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
                l.HasIndex(x => x.Timestamp);
            });
        }
    }
}
