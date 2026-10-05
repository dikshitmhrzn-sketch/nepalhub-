using System.Text;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data.Seeding;

internal sealed record ProvinceSeed(Guid Id, string Code, string Name, int SortOrder);

internal sealed record DistrictSeed(Guid Id, Guid ProvinceId, string Code, string Name, int SortOrder);

internal sealed record ProvinceDefinition(string Code, string Name, string[] Districts);

internal sealed record BadgeDefinitionSeed(
    Guid Id,
    string Code,
    string Name,
    string Description,
    string Icon,
    BadgeCriteria Criteria,
    int Threshold,
    int SortOrder);

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

    /*
     * The three badges this phase awards. Each row is a declaration of a threshold the
     * quiz already tracks, never a new fact about the user: the attempt count comes from
     * the quiz_attempts rows and the streak from the value the quiz just wrote to the
     * user. That is why there is no XP column here. The quiz decides XP, once per
     * attempt, and a badge that quietly paid out a second time would make the total on
     * the account page disagree with the sum of the quiz results behind it.
     */
    private static readonly BadgeDefinitionSeed[] Badges =
    [
        new(
            ReferenceDataIds.Badge("first-quiz"),
            "first-quiz",
            "First Quiz",
            "Finish your first daily quiz.",
            "trophy",
            BadgeCriteria.QuizzesCompleted,
            1,
            1),
        new(
            ReferenceDataIds.Badge("seven-day-streak"),
            "seven-day-streak",
            "7-Day Streak",
            "Finish a quiz on seven days in a row.",
            "flame",
            BadgeCriteria.ConsecutiveDayStreak,
            7,
            2),
        new(
            ReferenceDataIds.Badge("quiz-enthusiast"),
            "quiz-enthusiast",
            "Quiz Enthusiast",
            "Finish ten daily quizzes.",
            "book",
            BadgeCriteria.QuizzesCompleted,
            10,
            3),
    ];

    internal static IReadOnlyList<BadgeDefinitionSeed> BadgeDefinitions { get; } = Badges;

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
