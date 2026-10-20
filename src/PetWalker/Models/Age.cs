namespace PetWalker.Models;

public static class Age
{
    public static int From(DateOnly dateOfBirth) => From(dateOfBirth, DateOnly.FromDateTime(DateTime.Today));

    public static int From(DateOnly dateOfBirth, DateOnly today)
    {
        var age = today.Year - dateOfBirth.Year;
        return dateOfBirth > today.AddYears(-age) ? age - 1 : age;
    }
}
