using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Services
{
    public class ApplicationClaimsPrincipalFactory
        : UserClaimsPrincipalFactory<User, Role>
    {
        public ApplicationClaimsPrincipalFactory(
            UserManager<User> userManager,
            RoleManager<Role> roleManager,
            IOptions<IdentityOptions> options)
            : base(userManager, roleManager, options)
        {
        }

        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(User user)
        {
            var identity = await base.GenerateClaimsAsync(user);
            var nameClaim = identity.FindFirst(ClaimTypes.Name);
            if (nameClaim != null)
            {
                identity.RemoveClaim(nameClaim);
            }

            identity.AddClaim(new Claim(ClaimTypes.Name, user.FullName));
            identity.AddClaim(new Claim("Username", user.UserName ?? string.Empty));
            identity.AddClaim(new Claim("Post", user.Post));
            return identity;
        }
    }
}
