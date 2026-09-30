using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data.Seeding;

internal sealed record QuizSeedOption(string Text, bool IsCorrect);

internal sealed record QuizSeedQuestion(
    string Code,
    string Text,
    string Explanation,
    QuizCategory Category,
    IReadOnlyList<QuizSeedOption> Options);

/// <summary>
/// The bootstrap question pool.
///
/// This is starter content, not the long-term source. Thirty questions is roughly three
/// days of rotation at ten a day, so the pool has to be replenished through the staff
/// question editor well before the site opens. The facts here were chosen because they are
/// settled and uncontroversial; anything a subject-matter reviewer would want to check
/// belongs in the editor, not in a seeder.
///
/// Every question is deliberately written so exactly one option is unambiguously right.
/// A trivia question with a debatable answer produces a wrong mark for a correct player,
/// which is the fastest way to make a quiz feel broken.
/// </summary>
internal static class QuizSeedData
{
    internal static IReadOnlyList<QuizSeedQuestion> All { get; } =
    [
        // ----------------------------------------------------------------- Nepal
        new(
            "nep-001",
            "What is the capital city of Nepal?",
            "Kathmandu is the capital and largest city of Nepal. It sits in the Kathmandu Valley "
            + "alongside Lalitpur and Bhaktapur.",
            QuizCategory.Nepal,
            [
                new("Kathmandu", true),
                new("Pokhara", false),
                new("Biratnagar", false),
                new("Hetauda", false),
            ]),
        new(
            "nep-002",
            "How many provinces does Nepal have?",
            "Nepal was reorganised into seven provinces in 2015, replacing the older five "
            + "development regions.",
            QuizCategory.Nepal,
            [
                new("Five", false),
                new("Six", false),
                new("Seven", true),
                new("Eight", false),
            ]),
        new(
            "nep-003",
            "What is the official currency of Nepal?",
            "The currency is the Nepalese rupee, written NPR or Rs, and subdivided into 100 paisa.",
            QuizCategory.Nepal,
            [
                new("Nepalese rupee", true),
                new("Bangladeshi taka", false),
                new("Bhutanese ngultrum", false),
                new("Indian rupee", false),
            ]),
        new(
            "nep-004",
            "What is the national flower of Nepal?",
            "The rhododendron, called lali gurans in Nepali, was adopted as the national flower.",
            QuizCategory.Nepal,
            [
                new("Rhododendron", true),
                new("Lotus", false),
                new("Himalayan jasmine", false),
                new("Marigold", false),
            ]),
        new(
            "nep-005",
            "What is the national bird of Nepal?",
            "The danphe, or Himalayan monal, is the national bird. It is a large, vividly "
            + "coloured pheasant found in the Himalayan foothills.",
            QuizCategory.Nepal,
            [
                new("Danphe (Himalayan monal)", true),
                new("Lophura (Himalayan tragopan)", false),
                new("Snow partridge", false),
                new("Golden eagle", false),
            ]),
        new(
            "nep-006",
            "Nepal Standard Time is how far ahead of UTC?",
            "Nepal Standard Time is UTC+05:45, an unusual offset of 5 hours 45 minutes. It is one "
            + "of the few modern time zones that is not a whole number of hours ahead of UTC.",
            QuizCategory.Nepal,
            [
                new("5 hours 45 minutes ahead", true),
                new("6 hours ahead", false),
                new("5 hours 30 minutes ahead", false),
                new("4 hours 45 minutes ahead", false),
            ]),

        // ------------------------------------------------------------ Geography
        new(
            "geo-001",
            "What is the highest mountain in the world?",
            "Mount Everest stands at 8,848.86 metres, a height fixed by a joint Nepal-China survey "
            + "in 2020. It lies on the Nepal-Tibet border in Sagarmatha National Park.",
            QuizCategory.Geography,
            [
                new("Mount Everest", true),
                new("Kanchenjunga", false),
                new("Makalu", false),
                new("Dhaulagiri", false),
            ]),
        new(
            "geo-002",
            "What is the longest river in Nepal?",
               "The Karnali rises in the Himalayas and runs roughly 920 kilometres before joining the "
               + "Ghaghara, making it the longest river in the country. It is not confined to Nepal: it "
               + "crosses into northern India on its way to the Ganges.",
               QuizCategory.Geography,
            [
                new("Koshi", false),
                new("Bagmati", false),
                new("Karnali", true),
                new("Marsyangdi", false),
            ]),
        new(
            "geo-003",
            "How many districts does Nepal have?",
            "Nepal is divided into 77 districts, spread across its seven provinces.",
            QuizCategory.Geography,
            [
                new("70", false),
                new("75", false),
                new("77", true),
                new("82", false),
            ]),
        new(
            "geo-004",
            "The Bagmati river flows through which valley?",
            "The Bagmati runs through the Kathmandu Valley, the densely settled basin that contains "
            + "Kathmandu, Lalitpur and Bhaktapur.",
            QuizCategory.Geography,
            [
                new("Pokhara Valley", false),
                new("Surkhet Valley", false),
                new("Kathmandu Valley", true),
                new("Dang Valley", false),
            ]),
        new(
            "geo-005",
            "Rara Lake, the largest lake in Nepal by surface area, is in which district?",
            "Rara sits at about 2,990 metres in Mugu district, in the far west of Nepal, and is the "
            + "country's largest lake.",
            QuizCategory.Geography,
            [
                new("Mugu", true),
                new("Kaski", false),
                new("Sindhupalchowk", false),
                new("Manang", false),
            ]),

        // -------------------------------------------------------------- History
        new(
            "his-001",
            "In which year was Mount Everest first climbed?",
            "Edmund Hillary and Tenzing Norgay reached the summit on 29 May 1953, the first confirmed "
            + "ascent.",
            QuizCategory.History,
            [
                new("1950", false),
                new("1953", true),
                new("1957", false),
                new("1963", false),
            ]),
        new(
            "his-002",
            "In which place in Nepal was Gautama Buddha born?",
            "Buddha was born at Lumbini in the Terai, in what is now Rupandehi district, a site now "
            + "protected as a UNESCO World Heritage Site.",
            QuizCategory.History,
            [
                new("Lumbini", true),
                new("Bodh Gaya", false),
                new("Kushinagar", false),
                new("Sarnath", false),
            ]),
        new(
            "his-003",
            "Prithvi Narayan Shah, who united Nepal into one kingdom, was born in which district?",
            "He was born in Gorkha in 1723, and the Gorkha kingdom he built gave the modern state its "
            + "name.",
            QuizCategory.History,
            [
                new("Gorkha", true),
                new("Kaski", false),
                new("Kathmandu", false),
                new("Jhapa", false),
            ]),
        new(
            "his-004",
            "Which was the first national park established in Nepal?",
            "Chitwan National Park was established in 1973 as Nepal's first national park, and is "
            + "famous for its one-horned rhinoceros.",
            QuizCategory.History,
            [
                new("Chitwan", true),
                new("Bardia", false),
                new("Manaslu", false),
                new("Langtang", false),
            ]),
        new(
            "his-005",
            "In which year did Nepal's current constitution come into force?",
            "The Constitution of Nepal was promulgated in September 2015, replacing the monarchy with a "
            + "federal republic and establishing the seven provinces.",
            QuizCategory.History,
            [
                new("1990", false),
                new("2006", false),
                new("2015", true),
                new("2020", false),
            ]),

        // -------------------------------------------------------------- Science
        new(
            "sci-001",
            "What is the chemical symbol for gold?",
            "Gold's symbol is Au, from the Latin aurum. Silver is Ag and lead is Pb.",
            QuizCategory.Science,
            [
                new("Ag", false),
                new("Au", true),
                new("Gd", false),
                new("Go", false),
            ]),
        new(
            "sci-002",
            "Which gas do green plants take in during photosynthesis?",
            "Plants take in carbon dioxide and release oxygen while using sunlight to make sugars from "
            + "water and carbon dioxide.",
            QuizCategory.Science,
            [
                new("Oxygen", false),
                new("Nitrogen", false),
                new("Carbon dioxide", true),
                new("Hydrogen", false),
            ]),
        new(
            "sci-003",
            "What is the largest planet in the solar system?",
            "Jupiter is the largest, a gas giant with a mass more than twice that of all the other "
            + "planets combined.",
            QuizCategory.Science,
            [
                new("Saturn", false),
                new("Neptune", false),
                new("Jupiter", true),
                new("Earth", false),
            ]),
        new(
            "sci-004",
            "How many bones are there in the adult human body?",
            "An adult has 206 bones. Newborns have about 270, and many of the skull bones fuse "
            + "together during growth.",
            QuizCategory.Science,
            [
                new("186", false),
                new("206", true),
                new("226", false),
                new("246", false),
            ]),
        new(
            "sci-005",
            "What is the pH of pure water at 25 degrees Celsius?",
            "Pure water is neutral, with a pH of 7. Below 7 is acidic, above 7 is alkaline.",
            QuizCategory.Science,
            [
                new("0", false),
                new("7", true),
                new("10", false),
                new("14", false),
            ]),

        // ----------------------------------------------------------- Technology
        new(
            "tec-001",
            "Who is credited with inventing the World Wide Web?",
            "Tim Berners-Lee proposed the web at CERN in 1989 and released the first web server and "
            + "browser in 1990.",
            QuizCategory.Technology,
            [
                new("Vint Cerf", false),
                new("Tim Berners-Lee", true),
                new("Alan Turing", false),
                new("Linus Torvalds", false),
            ]),
        new(
            "tec-002",
            "What does HTTP stand for?",
            "HTTP is the Hypertext Transfer Protocol, the request and response protocol underlying the "
            + "web. Adding TLS to it gives HTTPS.",
            QuizCategory.Technology,
            [
                new("Hypertext Transfer Protocol", true),
                new("Hypertext Transmission Process", false),
                new("High Transfer Text Protocol", false),
                new("Hyperlink Text Transport Program", false),
            ]),
        new(
            "tec-003",
            "In binary, how is the decimal number 10 written?",
            "Decimal 10 is 1010 in binary: 8 + 2. Binary uses only 0 and 1, counting in powers of two.",
            QuizCategory.Technology,
            [
                new("1001", false),
                new("1010", true),
                new("1100", false),
                new("1110", false),
            ]),
        new(
            "tec-004",
            "What does CPU stand for?",
            "The CPU is the Central Processing Unit, the component that executes a program's "
            + "instructions.",
            QuizCategory.Technology,
            [
                new("Central Processing Unit", true),
                new("Computer Personal Unit", false),
                new("Central Program Utility", false),
                new("Core Processing Unit", false),
            ]),
        new(
            "tec-005",
            "Which protocol is used to serve web pages over an encrypted connection?",
            "HTTPS is HTTP carried inside TLS encryption, so traffic between the browser and the "
            + "server cannot be read in transit.",
            QuizCategory.Technology,
            [
                new("FTP", false),
                new("SMTP", false),
                new("HTTPS", true),
                new("Telnet", false),
            ]),

        // ---------------------------------------------------- General knowledge
        new(
            "gen-001",
            "How many continents are there in the conventional seven-continent model?",
            "The seven are Africa, Antarctica, Asia, Europe, North America, Oceania and South America. "
            + "Some models merge North and South America into six.",
            QuizCategory.GeneralKnowledge,
            [
                new("Five", false),
                new("Six", false),
                new("Seven", true),
                new("Eight", false),
            ]),
        new(
            "gen-002",
            "How many players from one team are on the pitch in a football match?",
            "Eleven players per team, one of whom is the goalkeeper. A football squad itself is "
            + "larger, with substitutes named on the bench.",
            QuizCategory.GeneralKnowledge,
            [
                new("Nine", false),
                new("Ten", false),
                new("Eleven", true),
                new("Twelve", false),
            ]),
        new(
            "gen-003",
            "Which is the largest ocean on Earth?",
            "The Pacific Ocean covers more than a third of the Earth's surface and is larger than all "
            + "the land areas combined.",
            QuizCategory.GeneralKnowledge,
            [
                new("Atlantic", false),
                new("Indian", false),
                new("Pacific", true),
                new("Arctic", false),
            ]),
        new(
            "gen-004",
            "How many sides does a hexagon have?",
            "A hexagon has six sides. The prefix comes from the Greek hexa, meaning six.",
            QuizCategory.GeneralKnowledge,
            [
                new("Five", false),
                new("Six", true),
                new("Seven", false),
                new("Eight", false),
            ]),
        new(
            "gen-005",
            "How many minutes are there in a full day?",
            "A day is 24 hours of 60 minutes, so 1,440 minutes.",
            QuizCategory.GeneralKnowledge,
            [
                new("1,220", false),
                new("1,360", false),
                new("1,440", true),
                new("1,520", false),
            ]),
    ];
}
