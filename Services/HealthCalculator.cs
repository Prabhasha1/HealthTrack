using HealthTrack.Models;

namespace HealthTrack.Services
{
    /// <summary>Pure health formulas, kept separate so they are easy to unit test.</summary>
    public static class HealthCalculator
    {
        /// <summary>Body Mass Index = weight(kg) / height(m)^2, rounded to 1 decimal place.</summary>
        public static double CalculateBmi(double weightKg, double heightCm)
        {
            if (weightKg <= 0 || heightCm <= 0)
                throw new ArgumentOutOfRangeException(nameof(weightKg), "Weight and height must be positive.");
            double heightM = heightCm / 100.0;
            return Math.Round(weightKg / (heightM * heightM), 1);
        }

        /// <summary>WHO adult BMI category.</summary>
        public static string BmiCategory(double bmi)
        {
            if (bmi <= 0) return "Unknown";
            if (bmi < 18.5) return "Underweight";
            if (bmi < 25) return "Normal";
            if (bmi < 30) return "Overweight";
            return "Obese";
        }

        /// <summary>Bootstrap colour class used to display a BMI category.</summary>
        public static string BmiBadgeClass(double bmi) => BmiCategory(bmi) switch
        {
            "Normal" => "bg-success",
            "Underweight" => "bg-info",
            "Overweight" => "bg-warning text-dark",
            "Obese" => "bg-danger",
            _ => "bg-secondary"
        };

        /// <summary>
        /// Estimates calories burned using MET values:
        /// kcal = MET x 3.5 x weight(kg) / 200 x minutes.
        /// </summary>
        public static int EstimateCaloriesBurned(int minutes, Intensity intensity, double weightKg)
        {
            if (minutes <= 0) return 0;
            if (weightKg <= 0) weightKg = 70; // sensible default when no weight is recorded
            double met = intensity switch
            {
                Intensity.Low => 3.5,
                Intensity.Moderate => 6.0,
                Intensity.High => 9.0,
                _ => 5.0
            };
            return (int)Math.Round(met * 3.5 * weightKg / 200.0 * minutes);
        }
    }
}
