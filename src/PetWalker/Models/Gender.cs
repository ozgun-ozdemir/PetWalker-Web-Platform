namespace PetWalker.Models;

public enum Gender
{
    Female = 1,
    Male = 2,
}

public enum PetSex
{
    Male = 1,
    Female = 2,
}

public enum PetType
{
    Dog = 1,
    Cat = 2,
}

public static class GenderExtensions
{
    public static string ToTurkish(this PetType type) => type switch
    {
        PetType.Dog => "Köpek",
        PetType.Cat => "Kedi",
        _ => "",
    };

    public static string ToTurkish(this PetSex sex) => sex switch
    {
        PetSex.Male => "Erkek",
        PetSex.Female => "Dişi",
        _ => "",
    };

    public static string ToTurkish(this Gender gender) => gender switch
    {
        Gender.Female => "Kadın",
        Gender.Male => "Erkek",
        _ => "",
    };
}
