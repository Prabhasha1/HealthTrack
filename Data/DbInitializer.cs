using HealthTrack.Models;
using HealthTrack.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Data
{
    /// <summary>
    /// Creates the database on first run, adds the three roles and the administrator,
    /// and (optionally) a realistic set of demo trainers, clients and 60 days of history
    /// so every dashboard and report has data to show in the demonstration.
    /// </summary>
    public static class DbInitializer
    {
        public static async Task InitializeAsync(IServiceProvider services)
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            var config = services.GetRequiredService<IConfiguration>();
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");

            // Creates HealthTrackDb in SQL Server LocalDB with all tables if it does not exist.
            await db.Database.EnsureCreatedAsync();

            foreach (var role in Roles.All)
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            string adminEmail = config["SeedData:AdminEmail"] ?? "admin@healthtrack.lk";
            if (await userManager.FindByEmailAsync(adminEmail) == null)
            {
                var admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    FullName = "System Administrator",
                    PrivacyConsentAt = DateTime.Now
                };
                await CreateUserAsync(userManager, admin, config["SeedData:AdminPassword"] ?? "Admin@123", Roles.Admin, logger);
            }

            bool createDemo = config.GetValue("SeedData:CreateDemoData", true);
            if (createDemo && !await db.WorkoutPlans.AnyAsync())
            {
                await SeedDemoDataAsync(db, userManager, config["SeedData:DemoPassword"] ?? "Demo@123", logger);
            }
        }

        private static async Task<ApplicationUser> CreateUserAsync(UserManager<ApplicationUser> userManager,
            ApplicationUser user, string password, string role, ILogger logger)
        {
            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                string errors = string.Join("; ", result.Errors.Select(e => e.Description));
                logger.LogError("Could not seed user {Email}: {Errors}", user.Email, errors);
                throw new InvalidOperationException("Seeding failed: " + errors);
            }
            await userManager.AddToRoleAsync(user, role);
            return user;
        }

        private static async Task SeedDemoDataAsync(ApplicationDbContext db, UserManager<ApplicationUser> userManager,
                                                    string password, ILogger logger)
        {
            var random = new Random(2026); // fixed seed => the same demo data every time
            const int HistoryDays = 90;  // days of demo history to generate
            DateTime today = DateTime.Today;

            // ---------- Trainers / professionals ----------
            async Task<ApplicationUser> Trainer(string email, string name, ProfessionalType type, string spec, string bio) =>
                await userManager.FindByEmailAsync(email) ?? await CreateUserAsync(userManager, new ApplicationUser
                {
                    UserName = email, Email = email, EmailConfirmed = true, FullName = name,
                    ProfessionalType = type, Specialization = spec, Bio = bio,
                    CreatedAt = today.AddDays(-120), PrivacyConsentAt = today.AddDays(-120)
                }, password, Roles.Trainer, logger);

            var kasun = await Trainer("kasun@healthtrack.lk", "Kasun Jayasinghe", ProfessionalType.FitnessTrainer,
                                      "Strength & Conditioning", "Certified personal trainer with 8 years' experience.");
            var nimali = await Trainer("nimali@healthtrack.lk", "Nimali Fernando", ProfessionalType.Nutritionist,
                                       "Weight management nutrition", "Registered nutritionist focusing on sustainable diets.");
            var perera = await Trainer("dr.perera@healthtrack.lk", "Dr. Ruwan Perera", ProfessionalType.Doctor,
                                       "Sports medicine", "Consultant in sports and exercise medicine.");

            // ---------- Clients ----------
            async Task<ApplicationUser> Client(string email, string name, Gender g, int age, double height, string goal,
                                               ApplicationUser? trainer, int joinedDaysAgo) =>
                await userManager.FindByEmailAsync(email) ?? await CreateUserAsync(userManager, new ApplicationUser
                {
                    UserName = email, Email = email, EmailConfirmed = true, FullName = name, Gender = g,
                    DateOfBirth = today.AddYears(-age).AddDays(-random.Next(0, 300)), HeightCm = height,
                    FitnessGoal = goal, AssignedTrainerId = trainer?.Id,
                    CreatedAt = today.AddDays(-joinedDaysAgo), PrivacyConsentAt = today.AddDays(-joinedDaysAgo),
                    LastLoginAt = today.AddDays(-random.Next(0, 3)).AddHours(8 + random.Next(0, 10))
                }, password, Roles.Client, logger);

            var amal = await Client("amal@healthtrack.lk", "Amal Silva", Gender.Male, 32, 174, "Lose 8 kg and improve stamina", kasun, 95);
            var sanduni = await Client("sanduni@healthtrack.lk", "Sanduni Wickramasinghe", Gender.Female, 27, 162, "Tone up and run a 10K", kasun, 90);
            var ravindu = await Client("ravindu@healthtrack.lk", "Ravindu Bandara", Gender.Male, 41, 179, "Lower blood pressure, lose weight", nimali, 85);
            var tharushi = await Client("tharushi@healthtrack.lk", "Tharushi Gunawardena", Gender.Female, 35, 158, "Healthy eating habits", nimali, 65);
            await Client("dilan@healthtrack.lk", "Dilan Rathnayake", Gender.Male, 24, 181, "Build muscle", null, 5);

            // ---------- Workout plans ----------
            var amalPlan = new WorkoutPlan
            {
                Title = "Fat-loss circuit (12 weeks)", Goal = "Weight loss", TrainerId = kasun.Id, ClientId = amal.Id,
                Description = "Three cardio + strength sessions per week, progressive intensity.",
                StartDate = today.AddDays(-90), EndDate = today.AddDays(30), CreatedAt = today.AddDays(-91),
                Exercises =
                {
                    new WorkoutExercise { Name = "Treadmill intervals", Day = DayOfWeek.Monday, DurationMinutes = 30, Intensity = Intensity.High },
                    new WorkoutExercise { Name = "Goblet squats", Day = DayOfWeek.Monday, Sets = 4, Reps = 12, DurationMinutes = 15 },
                    new WorkoutExercise { Name = "Cycling", Day = DayOfWeek.Wednesday, DurationMinutes = 40, Intensity = Intensity.Moderate },
                    new WorkoutExercise { Name = "Push-ups", Day = DayOfWeek.Wednesday, Sets = 3, Reps = 15, DurationMinutes = 10 },
                    new WorkoutExercise { Name = "HIIT circuit", Day = DayOfWeek.Friday, DurationMinutes = 25, Intensity = Intensity.High },
                    new WorkoutExercise { Name = "Plank", Day = DayOfWeek.Friday, Sets = 3, Reps = 1, DurationMinutes = 5, Notes = "Hold 60 seconds" }
                }
            };
            var sanduniPlan = new WorkoutPlan
            {
                Title = "10K running programme", Goal = "Endurance", TrainerId = kasun.Id, ClientId = sanduni.Id,
                Description = "Build up to a continuous 10K run with supporting strength work.",
                StartDate = today.AddDays(-85), CreatedAt = today.AddDays(-86),
                Exercises =
                {
                    new WorkoutExercise { Name = "Easy run", Day = DayOfWeek.Tuesday, DurationMinutes = 35, Intensity = Intensity.Moderate },
                    new WorkoutExercise { Name = "Yoga flow", Day = DayOfWeek.Wednesday, DurationMinutes = 30, Intensity = Intensity.Low },
                    new WorkoutExercise { Name = "Tempo run", Day = DayOfWeek.Thursday, DurationMinutes = 30, Intensity = Intensity.High },
                    new WorkoutExercise { Name = "Lunges", Day = DayOfWeek.Thursday, Sets = 3, Reps = 12, DurationMinutes = 10 },
                    new WorkoutExercise { Name = "Long run", Day = DayOfWeek.Sunday, DurationMinutes = 60, Intensity = Intensity.Moderate }
                }
            };
            var ravinduPlan = new WorkoutPlan
            {
                Title = "Low-impact cardio", Goal = "Heart health", TrainerId = nimali.Id, ClientId = ravindu.Id,
                Description = "Brisk walking and swimming to reduce blood pressure.",
                StartDate = today.AddDays(-80), CreatedAt = today.AddDays(-81),
                Exercises =
                {
                    new WorkoutExercise { Name = "Brisk walk", Day = DayOfWeek.Monday, DurationMinutes = 45, Intensity = Intensity.Low },
                    new WorkoutExercise { Name = "Swimming", Day = DayOfWeek.Thursday, DurationMinutes = 30, Intensity = Intensity.Moderate },
                    new WorkoutExercise { Name = "Brisk walk", Day = DayOfWeek.Saturday, DurationMinutes = 45, Intensity = Intensity.Low },
                    new WorkoutExercise { Name = "Cycling", Day = DayOfWeek.Saturday, DurationMinutes = 20, Intensity = Intensity.Low }
                }
            };
            var tharushiPlan = new WorkoutPlan
            {
                Title = "Beginner full-body", Goal = "General fitness", TrainerId = nimali.Id, ClientId = tharushi.Id,
                StartDate = today.AddDays(-60), CreatedAt = today.AddDays(-61),
                Exercises =
                {
                    new WorkoutExercise { Name = "Yoga flow", Day = DayOfWeek.Monday, DurationMinutes = 30, Intensity = Intensity.Low },
                    new WorkoutExercise { Name = "Goblet squats", Day = DayOfWeek.Wednesday, Sets = 3, Reps = 10, DurationMinutes = 15 },
                    new WorkoutExercise { Name = "Brisk walk", Day = DayOfWeek.Friday, DurationMinutes = 40, Intensity = Intensity.Low }
                }
            };
            db.WorkoutPlans.AddRange(amalPlan, sanduniPlan, ravinduPlan, tharushiPlan);

            // ---------- Diet plans ----------
            DietPlan Diet(string title, ApplicationUser trainer, ApplicationUser client, int kcal, int daysAgo) => new DietPlan
            {
                Title = title, TrainerId = trainer.Id, ClientId = client.Id, DailyCalorieTarget = kcal,
                Description = "Balanced plate approach: half vegetables, quarter protein, quarter complex carbs.",
                StartDate = today.AddDays(-daysAgo), CreatedAt = today.AddDays(-daysAgo - 1),
                Meals =
                {
                    new DietMeal { MealType = MealType.Breakfast, Time = new TimeSpan(7, 0, 0), Description = "Oats with banana and milk", Calories = 380 },
                    new DietMeal { MealType = MealType.MorningSnack, Time = new TimeSpan(10, 30, 0), Description = "Greek yoghurt and nuts", Calories = 200 },
                    new DietMeal { MealType = MealType.Lunch, Time = new TimeSpan(13, 0, 0), Description = "Red rice, dhal, chicken curry, gotukola sambol", Calories = 600 },
                    new DietMeal { MealType = MealType.AfternoonSnack, Time = new TimeSpan(16, 0, 0), Description = "Fruit bowl", Calories = 150 },
                    new DietMeal { MealType = MealType.Dinner, Time = new TimeSpan(19, 30, 0), Description = "String hoppers with fish curry and salad", Calories = 500 }
                }
            };
            db.DietPlans.AddRange(
                Diet("Calorie-deficit plan", nimali, amal, 1900, 88),
                Diet("Runner's fuel plan", nimali, sanduni, 2100, 80),
                Diet("Low-sodium DASH plan", nimali, ravindu, 2000, 78),
                Diet("Balanced beginner plan", nimali, tharushi, 1800, 58));
            await db.SaveChangesAsync();

            // ---------- History: measurements, workouts and meals ----------
            var history = new[]
            {
                (Client: amal, Plan: amalPlan, StartWeight: 88.0, Trend: -0.065, Adherence: 0.85, Kcal: 1950),
                (Client: sanduni, Plan: sanduniPlan, StartWeight: 61.0, Trend: -0.02, Adherence: 0.9, Kcal: 2050),
                (Client: ravindu, Plan: ravinduPlan, StartWeight: 95.0, Trend: -0.05, Adherence: 0.6, Kcal: 2150),
                (Client: tharushi, Plan: tharushiPlan, StartWeight: 64.0, Trend: -0.015, Adherence: 0.7, Kcal: 1850)
            };

            foreach (var h in history)
            {
                double weight = h.StartWeight;
                for (int daysAgo = HistoryDays; daysAgo >= 0; daysAgo--)
                {
                    var date = today.AddDays(-daysAgo);
                    weight += h.Trend + (random.NextDouble() - 0.5) * 0.25;

                    // A measurement roughly twice a week
                    if (daysAgo % 3 == 0)
                    {
                        double w = Math.Round(weight, 1);
                        int systolic = h.Client == ravindu ? 152 - (HistoryDays - daysAgo) / 5 + random.Next(-3, 4) : 118 + random.Next(-5, 6);
                        db.HealthMeasurements.Add(new HealthMeasurement
                        {
                            ClientId = h.Client.Id, Date = date, WeightKg = w, HeightCm = h.Client.HeightCm!.Value,
                            Bmi = HealthCalculator.CalculateBmi(w, h.Client.HeightCm.Value),
                            BodyFatPercent = Math.Round((h.Client.Gender == Gender.Female ? 30 : 25) - (HistoryDays - daysAgo) * 0.04 + random.NextDouble(), 1),
                            RestingHeartRate = 78 - (HistoryDays - daysAgo) / 12 + random.Next(-1, 2),
                            SystolicBp = systolic, DiastolicBp = systolic - 40 + random.Next(-3, 4),
                            Notes = daysAgo == 0 ? "Feeling more energetic this week." : null
                        });
                    }

                    // Workouts on the plan's scheduled days, if the plan had started
                    if (date >= h.Plan.StartDate)
                    {
                        foreach (var ex in h.Plan.Exercises.Where(e => e.Day == date.DayOfWeek))
                        {
                            if (random.NextDouble() > h.Adherence) continue;
                            int minutes = Math.Max(5, ex.DurationMinutes + random.Next(-5, 6));
                            db.WorkoutLogs.Add(new WorkoutLog
                            {
                                ClientId = h.Client.Id, WorkoutPlanId = h.Plan.Id, ExerciseName = ex.Name, Date = date,
                                DurationMinutes = minutes, Intensity = ex.Intensity,
                                CaloriesBurned = HealthCalculator.EstimateCaloriesBurned(minutes, ex.Intensity, weight)
                            });
                        }
                    }

                    // Three or four meals logged most days
                    if (random.NextDouble() < 0.85)
                    {
                        var meals = new (MealType Type, string Desc, int Share)[]
                        {
                            (MealType.Breakfast, "Oats with banana", 22),
                            (MealType.Lunch, "Rice and curry", 38),
                            (MealType.AfternoonSnack, "Fruit and tea", 10),
                            (MealType.Dinner, "Hoppers with dhal", 30)
                        };
                        int dayTotal = h.Kcal + random.Next(-250, 300);
                        foreach (var m in meals)
                        {
                            if (m.Type == MealType.AfternoonSnack && random.NextDouble() < 0.4) continue;
                            int kcal = dayTotal * m.Share / 100;
                            db.MealLogs.Add(new MealLog
                            {
                                ClientId = h.Client.Id, Date = date, MealType = m.Type, Description = m.Desc, Calories = kcal,
                                ProteinGrams = Math.Round(kcal * 0.25 / 4, 1), CarbsGrams = Math.Round(kcal * 0.5 / 4, 1),
                                FatGrams = Math.Round(kcal * 0.25 / 9, 1)
                            });
                        }
                    }

                    // Logins for engagement statistics
                    if (random.NextDouble() < h.Adherence)
                    {
                        db.ActivityLogs.Add(new ActivityLog
                        {
                            UserId = h.Client.Id, Action = "Login", Details = "Demo data",
                            IpAddress = "127.0.0.1", Timestamp = date.AddHours(7 + random.Next(0, 12))
                        });
                    }
                }
            }

            // ---------- Appointments ----------
            DateTime At(int daysFromToday, int hour) => today.AddDays(daysFromToday).AddHours(hour);
            db.Appointments.AddRange(
                new Appointment { ClientId = amal.Id, TrainerId = kasun.Id, Type = AppointmentType.TrainingSession, StartTime = At(-14, 9), Status = AppointmentStatus.Completed, ClientNotes = "Form check on squats", TrainerNotes = "Good depth, keep chest up." , CreatedAt = today.AddDays(-20)},
                new Appointment { ClientId = amal.Id, TrainerId = nimali.Id, Type = AppointmentType.NutritionConsultation, StartTime = At(1, 10), Status = AppointmentStatus.Approved, ClientNotes = "Review calorie target", CreatedAt = today.AddDays(-3) },
                new Appointment { ClientId = sanduni.Id, TrainerId = kasun.Id, Type = AppointmentType.TrainingSession, StartTime = At(2, 17), Status = AppointmentStatus.Pending, ClientNotes = "Pacing strategy for race day", CreatedAt = today.AddDays(-1) },
                new Appointment { ClientId = ravindu.Id, TrainerId = perera.Id, Type = AppointmentType.HealthCheck, StartTime = At(-7, 11), Status = AppointmentStatus.Completed, ClientNotes = "Blood pressure follow-up", TrainerNotes = "BP improving; continue plan.", CreatedAt = today.AddDays(-12) },
                new Appointment { ClientId = ravindu.Id, TrainerId = perera.Id, Type = AppointmentType.HealthCheck, StartTime = At(5, 11), Status = AppointmentStatus.Pending, ClientNotes = "Monthly check", CreatedAt = today },
                new Appointment { ClientId = tharushi.Id, TrainerId = nimali.Id, Type = AppointmentType.NutritionConsultation, StartTime = At(3, 15), Status = AppointmentStatus.Approved, CreatedAt = today.AddDays(-2) },
                new Appointment { ClientId = tharushi.Id, TrainerId = kasun.Id, Type = AppointmentType.TrainingSession, StartTime = At(-3, 16), Status = AppointmentStatus.Cancelled, TrainerNotes = "Trainer unavailable", CreatedAt = today.AddDays(-6) });

            // ---------- Feedback & notifications ----------
            db.TrainerFeedbacks.AddRange(
                new TrainerFeedback { TrainerId = kasun.Id, ClientId = amal.Id, Rating = 5, Message = "Great consistency this month - you've lost over 4 kg. Let's add one more interval round on Mondays.", CreatedAt = today.AddDays(-4) },
                new TrainerFeedback { TrainerId = kasun.Id, ClientId = sanduni.Id, Rating = 4, Message = "Long runs look strong. Remember to warm up properly before tempo runs.", CreatedAt = today.AddDays(-6) },
                new TrainerFeedback { TrainerId = nimali.Id, ClientId = ravindu.Id, Rating = 3, Message = "Try to log your meals every day so we can track sodium intake.", CreatedAt = today.AddDays(-2) });

            db.Notifications.AddRange(
                new Notification { UserId = amal.Id, Title = "New feedback", Message = "Kasun Jayasinghe left you feedback.", Type = NotificationType.Feedback, Link = "/Client/Dashboard", CreatedAt = today.AddDays(-4) },
                new Notification { UserId = amal.Id, Title = "Appointment approved", Message = "Your nutrition consultation with Nimali Fernando was approved.", Type = NotificationType.AppointmentUpdate, Link = "/Appointments", CreatedAt = today.AddDays(-2) },
                new Notification { UserId = kasun.Id, Title = "New appointment request", Message = "Sanduni Wickramasinghe requested a training session.", Type = NotificationType.AppointmentUpdate, Link = "/Appointments", CreatedAt = today.AddDays(-1) });

            await db.SaveChangesAsync();
            logger.LogInformation("Demo data created.");
        }
    }
}
