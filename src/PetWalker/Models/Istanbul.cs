using System.Globalization;

namespace PetWalker.Models;

// The address field is a choice of Istanbul district; it is stored as "Kadıköy, İstanbul".
public static class Istanbul
{
    private static readonly string[] DistrictNames =
    [
        "Adalar", "Arnavutköy", "Ataşehir", "Avcılar", "Bağcılar", "Bahçelievler", "Bakırköy", "Başakşehir",
        "Bayrampaşa", "Beşiktaş", "Beykoz", "Beylikdüzü", "Beyoğlu", "Büyükçekmece", "Çatalca", "Çekmeköy",
        "Esenler", "Esenyurt", "Eyüpsultan", "Fatih", "Gaziosmanpaşa", "Güngören", "Kadıköy", "Kağıthane",
        "Kartal", "Küçükçekmece", "Maltepe", "Pendik", "Sancaktepe", "Sarıyer", "Silivri", "Sultanbeyli",
        "Sultangazi", "Şile", "Şişli", "Tuzla", "Ümraniye", "Üsküdar", "Zeytinburnu",
    ];

    public static IReadOnlyList<string> Districts { get; } =
        DistrictNames.Order(StringComparer.Create(CultureInfo.GetCultureInfo("tr-TR"), ignoreCase: false)).ToList();

    public static string ToAddress(string district) => $"{district}, İstanbul";

    public static bool IsValidAddress(string? address) =>
        address is not null && Districts.Any(d => ToAddress(d) == address);
}
