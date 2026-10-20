namespace HealthTrack.Models
{
    /// <summary>Names of the three security roles used throughout the system.</summary>
    public static class Roles
    {
        public const string Admin = "Admin";
        public const string Trainer = "Trainer";
        public const string Client = "Client";

        /// <summary>All roles, used when seeding the database.</summary>
        public static readonly string[] All = { Admin, Trainer, Client };
    }

    public enum Gender
    {
        Male,
        Female,
        Other,
        PreferNotToSay
    }

    /// <summary>The kind of professional a "Trainer" account represents.</summary>
    public enum ProfessionalType
    {
        FitnessTrainer,
        Nutritionist,
        Physiotherapist,
        Doctor
    }

    public enum MealType
    {
        Breakfast,
        MorningSnack,
        Lunch,
        AfternoonSnack,
        Dinner
    }

    public enum Intensity
    {
        Low,
        Moderate,
        High
    }

    public enum AppointmentType
    {
        TrainingSession,
        NutritionConsultation,
        HealthCheck,
        Physiotherapy,
        GeneralConsultation
    }

    /// <summary>Life-cycle of an appointment (see the state diagram in the report).</summary>
    public enum AppointmentStatus
    {
        Pending,
        Approved,
        Rescheduled,
        Completed,
        Cancelled,
        Rejected
    }

    public enum NotificationType
    {
        General,
        WorkoutReminder,
        MealReminder,
        AppointmentReminder,
        AppointmentUpdate,
        PlanAssigned,
        Feedback
    }
}
