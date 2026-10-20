using System.Security.Claims;
using HealthTrack.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace HealthTrack.Services
{
    /// <summary>
    /// Adds the user's full name to the authentication cookie so the layout can greet
    /// the user by name without a database query on every page.
    /// </summary>
    public class AppClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
    {
        public const string FullNameClaim = "FullName";

        public AppClaimsPrincipalFactory(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager,
                                         IOptions<IdentityOptions> options)
            : base(userManager, roleManager, options)
        {
        }

        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
        {
            var identity = await base.GenerateClaimsAsync(user);
            identity.AddClaim(new Claim(FullNameClaim, user.FullName));
            return identity;
        }
    }
}
