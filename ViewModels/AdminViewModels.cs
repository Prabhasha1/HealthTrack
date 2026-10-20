using System.ComponentModel.DataAnnotations;
using HealthTrack.Models;

namespace HealthTrack.ViewModels
{
    public class UserListItem
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsLockedOut { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public string? AssignedTrainerName { get; set; }
        public string? Specialization { get; set; }
    }

    public class UserListViewModel
    {
        public List<UserListItem> Users { get; set; } = new();
        public string? Search { get; set; }
        public string? RoleFilter { get; set; }
    }

    /// <summary>Used by the admin to create or edit any account.</summary>
    public class UserEditViewModel
    {
        public string? Id { get; set; }

        [Required, StringLength(100, MinimumLength = 3), Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Phone, Display(Name = "Phone number")]
        public string? PhoneNumber { get; set; }

        [Required]
        public string Role { get; set; } = Roles.Client;

        [Display(Name = "Professional type")]
        public ProfessionalType? ProfessionalType { get; set; }

        [StringLength(100)]
        public string? Specialization { get; set; }

        [Display(Name = "Assigned trainer")]
        public string? AssignedTrainerId { get; set; }

        [Display(Name = "Account active")]
        public bool IsActive { get; set; } = true;

        /// <summary>Required when creating; optional when editing (leave blank to keep).</summary>
        [DataType(DataType.Password), StringLength(100, MinimumLength = 8)]
        public string? Password { get; set; }
    }

    public class AssignTrainerViewModel
    {
        [Required]
        public string ClientId { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;

        [Display(Name = "Trainer")]
        public string? TrainerId { get; set; }
    }

    public class TrainerLoadItem
    {
        public string TrainerId { get; set; } = string.Empty;
        public string TrainerName { get; set; } = string.Empty;
        public string? Specialization { get; set; }
        public int ClientCount { get; set; }
        public int ActivePlans { get; set; }
        public int UpcomingAppointments { get; set; }
    }

    public class AdminDashboardViewModel
    {
        public int TotalClients { get; set; }
        public int TotalTrainers { get; set; }
        public int TotalAdmins { get; set; }
        public int ActiveUsersLast7Days { get; set; }
        public int NewUsersLast30Days { get; set; }
        public int UnassignedClients { get; set; }
        public int LogsToday { get; set; }
        public int PendingAppointments { get; set; }

        public List<string> ActivityDays { get; set; } = new();
        public List<int> LoginsPerDay { get; set; } = new();
        public List<int> LogEntriesPerDay { get; set; } = new();

        public List<TrainerLoadItem> TrainerAssignments { get; set; } = new();
        public List<ActivityLog> RecentActivity { get; set; } = new();
        public Dictionary<string, int> AppointmentsByStatus { get; set; } = new();
    }
}
