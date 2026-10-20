using System.Security.Claims;

namespace PetWalker.Services;

public static class CurrentUser
{
    public static int GetCustomerId(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? throw new InvalidOperationException("User is not signed in."));
}
