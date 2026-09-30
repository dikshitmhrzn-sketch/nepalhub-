using System.Text;

namespace NepalHub.Api.Data.Seeding;

internal sealed record ProvinceSeed(Guid Id, string Code, string Name, int SortOrder);

internal sealed record DistrictSeed(Guid Id, Guid ProvinceId, string Code, string Name, int SortOrder);

internal sealed record ProvinceDefinition(string Code, string Name, string[] Districts);

internal static class NepalReferenceData
{
    /*
     * The seven provinces and 77 districts, in the order the Constitution numbers
     * them (Province 1 to Province 7) and alphabetically within each province.
     *
     * "Rukum East" belongs to Lumbini and "Rukum West" to Karnali; the province
     * therefore has 14 districts and Lumbini has 12.
     */
    private static readonly ProvinceDefinition[] Definitions =
    [
        new("koshi", "Koshi",
        [
            "Bhojpur",
            "Dhankuta",
            "Ilam",
            "Jhapa",
            "Khotang",
            "Morang",
            "Okhaldhunga",
            "Panchthar",
            "Sankhuwasabha",
            "Solukhumbu",
            "Sunsari",
            "Taplejung",
            "Tehrathum",
            "Udayapur",
        ]),
        new("madhesh", "Madhesh",
        [
            "Bara",
            "Dhanusha",
            "Mahottari",
            "Parsa",
            "Rautahat",
            "Saptari",
            "Sarlahi",
            "Siraha",
        ]),
        new("bagmati", "Bagmati",
        [
            "Bhaktapur",
            "Chitwan",
            "Dhading",
            "Dolakha",
            "Kathmandu",
            "Kavrepalanchok",
            "Lalitpur",
            "Makwanpur",
            "Nuwakot",
            "Ramechhap",
            "Rasuwa",
            "Sindhuli",
            "Sindhupalchok",
        ]),
        new("gandaki", "Gandaki",
        [
            "Baglung",
            "Gorkha",
            "Kaski",
            "Lamjung",
            "Manang",
            "Mustang",
            "Myagdi",
            "Nawalpur",
            "Parbat",
            "Syangja",
            "Tanahun",
        ]),
        new("lumbini", "Lumbini",
        [
            "Arghakhanchi",
            "Banke",
            "Bardiya",
            "Dang",
            "Gulmi",
            "Kapilvastu",
            "Nawalparasi West",
            "Palpa",
            "Pyuthan",
            "Rolpa",
            "Rukum East",
            "Rupandehi",
        ]),
        new("karnali", "Karnali",
        [
            "Dailekh",
            "Dolpa",
            "Humla",
            "Jajarkot",
            "Jumla",
            "Kalikot",
            "Mugu",
            "Rukum West",
            "Salyan",
            "Surkhet",
        ]),
        new("sudurpashchim", "Sudurpashchim",
        [
            "Achham",
            "Baitadi",
            "Bajhang",
            "Bajura",
            "Dadeldhura",
            "Darchula",
            "Doti",
            "Kailali",
            "Kanchanpur",
        ]),
    ];

    internal static IReadOnlyList<ProvinceSeed> Provinces { get; } =
        Definitions
            .Select((definition, index) => new ProvinceSeed(
                ReferenceDataIds.Province(definition.Code),
                definition.Code,
                definition.Name,
                index + 1))
            .ToArray();

    internal static IReadOnlyList<DistrictSeed> Districts { get; } =
        Definitions
            .SelectMany((definition, provinceIndex) => definition.Districts
                .Order(StringComparer.Ordinal)
                .Select((name, districtIndex) =>
                {
                    var code = $"{definition.Code}-{Slug(name)}";

                    return new DistrictSeed(
                        ReferenceDataIds.District(code),
                        ReferenceDataIds.Province(definition.Code),
                        code,
                        name,
                        districtIndex + 1);
                }))
            .ToArray();

    private static string Slug(string name)
    {
        var builder = new StringBuilder(name.Length);
        var previousWasSeparator = false;

        foreach (var character in name)
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                previousWasSeparator = false;
            }
            else if (!previousWasSeparator)
            {
                builder.Append('-');
                previousWasSeparator = true;
            }
        }

        return builder.ToString().Trim('-');
    }
}
