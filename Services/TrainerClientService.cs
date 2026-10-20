using HealthTrack.Data;
using HealthTrack.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Services
{
    /// <summary>
    /// Decides which clients a trainer may see and manage. A trainer works with a client
    /// when the client is assigned to them, or when they already share a plan or appointment
    /// (e.g. a nutritionist consulting on another trainer's client).
    /// </summary>
    public interface ITrainerClientService
    {
        Task<List<ApplicationUser>> GetClientsAsync(string trainerId);
        Task<bool> CanAccessClientAsync(string trainerId, string clientId);
    }

    public class TrainerClientService : ITrainerClientService
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _users;

        public TrainerClientService(ApplicationDbContext db, UserManager<ApplicationUser> users)
        {
            _db = db;
            _users = users;
        }

        public async Task<List<ApplicationUser>> GetClientsAsync(string trainerId)
        {
            var ids = new HashSet<string>();
            ids.UnionWith(await _db.Users.Where(u => u.AssignedTrainerId == trainerId).Select(u => u.Id).ToListAsync());
            ids.UnionWith(await _db.WorkoutPlans.Where(p => p.TrainerId == trainerId).Select(p => p.ClientId).ToListAsync());
            ids.UnionWith(await _db.DietPlans.Where(p => p.TrainerId == trainerId).Select(p => p.ClientId).ToListAsync());
            ids.UnionWith(await _db.Appointments.Where(a => a.TrainerId == trainerId && a.Status != AppointmentStatus.Rejected)
                                                .Select(a => a.ClientId).ToListAsync());

            // Only keep real, active client accounts.
            var clients = await _users.GetUsersInRoleAsync(Roles.Client);
            return clients.Where(c => ids.Contains(c.Id) && c.IsActive).OrderBy(c => c.FullName).ToList();
        }

        public async Task<bool> CanAccessClientAsync(string trainerId, string clientId)
        {
            if (string.IsNullOrEmpty(clientId)) return false;
            if (await _db.Users.AnyAsync(u => u.Id == clientId && u.AssignedTrainerId == trainerId)) return true;
            if (await _db.WorkoutPlans.AnyAsync(p => p.TrainerId == trainerId && p.ClientId == clientId)) return true;
            if (await _db.DietPlans.AnyAsync(p => p.TrainerId == trainerId && p.ClientId == clientId)) return true;
            return await _db.Appointments.AnyAsync(a => a.TrainerId == trainerId && a.ClientId == clientId
                                                        && a.Status != AppointmentStatus.Rejected);
        }
    }
}
