using Microsoft.AspNetCore.Identity;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;

namespace NepalHub.Api.Data.Seeding;

internal static class RoleSeeder
{
    internal static async Task SeedAsync(RoleManager<ApplicationRole> roleManager, CancellationToken cancellationToken)
    {
        foreach (var definition in Roles.All)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await roleManager.RoleExistsAsync(definition.Name))
            {
                continue;
            }

            var role = new ApplicationRole
            {
                Id = ReferenceDataIds.Role(definition.Name),
                Name = definition.Name,
                Description = definition.Description,
            };

            var result = await roleManager.CreateAsync(role);
            if (!result.Succeeded)
            {
                var failures = string.Join("; ", result.Errors.Select(error => error.Description));
                throw new InvalidOperationException($"Could not seed role '{definition.Name}': {failures}");
            }
        }
    }
}
