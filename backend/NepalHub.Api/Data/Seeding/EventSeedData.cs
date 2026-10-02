using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data.Seeding;

/// <summary>
/// A starter row for <c>events</c>. Not the long-term source of listings, for the same reason the job
/// board and the business directory are not: they exist so a fresh install shows a working calendar,
/// and so staff can see what a well-written listing looks like before they write the real one.
/// </summary>
/// <param name="Slug">Stable key, matched on so a staff edit is never undone on restart.</param>
/// <param name="Title">The event as a reader would search for it.</param>
/// <param name="Summary">One line for the listing cards.</param>
/// <param name="Description">The full details, plain text.</param>
/// <param name="Category">One of the fixed set, so the calendar can be filtered without a join.</param>
/// <param name="Organiser">Whoever is running it, named in the row rather than linked.</param>
/// <param name="BusinessSlug">The listed business running or hosting it, when it has one.</param>
/// <param name="DistrictCode">
/// Where it happens. Required on an event, unlike on a deal: a reader cannot attend something with no
/// location.
/// </param>
/// <param name="Venue">The building, ground or hall.</param>
/// <param name="StartsInDays">
/// Offset from the Nepal date on the day it is seeded, counted in days and then set to a fixed hour so
/// the listing lands at a plausible time of day.
/// </param>
/// <param name="DurationHours">How long it runs, which is what an event that is on now is derived from.</param>
/// <param name="ExpiredDaysAgo">
/// Set instead of <paramref name="StartsInDays"/> for an event that already happened, so the public
/// filter's finished path is exercised by seeded data as well as by tests.
/// </param>
/// <param name="IsPublished">Whether readers can see it.</param>
internal sealed record EventSeed(
    string Slug,
    string Title,
    string Summary,
    string Description,
    EventCategory Category,
    string DistrictCode,
    string? Organiser = null,
    string? BusinessSlug = null,
    string? Venue = null,
    int StartsInDays = 7,
    int DurationHours = 2,
    int? ExpiredDaysAgo = null,
    bool IsPublished = true);

/// <summary>
/// Fifteen events, deliberately small, spread across all seven provinces and all eight categories, with
/// two that have already finished so the expiry filter is exercised by data rather than only by tests.
///
/// <para>
/// <b>Dates are relative, not absolute.</b> Every row stores an offset from the day it is seeded,
/// because a hard-coded date is correct on the day it was written and wrong forever after: the
/// calendar would empty itself out and fill up with events that had already finished. The cost is
/// honest — a seeded row is inserted once and never re-dated, so the upcoming ones do eventually pass
/// and the calendar belongs to staff from then on.
/// </para>
///
/// <para>
/// <b>Content is Nepali and non-political.</b> Festivals, sport, literature, school days, health
/// camps and treks, because that is what a community calendar is for and because a seeded listing
/// that took a side on a political question would be a poor thing to ship in a first install.
/// </para>
/// </summary>
internal static class EventSeedData
{
    internal static IReadOnlyList<EventSeed> All { get; } =
    [
        new(
            "newa-street-food-festival-kathmandu",
            "Newa street food festival",
            "Two days of Newari food along the main chowk, from morning tea to late evening.",
            "A weekend of Newari cooking in the old city: sel roti, yomari, samay baji, bara and "
            + "the sweet shops that only open for festivals. Stalls are set along the chowk and "
            + "there is seating at the far end. Most stalls take cash only, and a few take "
            + "eSewa. Entry is free and there is no need to register. Come before noon if you want "
            + "the sel roti, because it sells out by early afternoon.",
            EventCategory.Cultural,
            DistrictCode: "bagmati-kathmandu",
            Organiser: "Newa Guthi",
            Venue: "Kumari chowk and the lanes either side",
            StartsInDays: 12),
        new(
            "pokhara-paragliding-safety-briefing",
            "Paragliding safety briefing",
            "Free two-hour briefing for anyone about to fly, whether or not you booked with us.",
            "Covers air conditions, launch and landing procedure, and what to do if the canopy "
            + "behaves unexpectedly. Taught by our pilots and free to anyone, whether you are "
            + "flying with us or with someone else. You must be at least sixteen and able to run "
            + "with the wing. No flying happens on the day; this is the classroom session. Come "
            + "fifteen minutes early to be checked in.",
            EventCategory.Sports,
            DistrictCode: "gandaki-kaski",
            Organiser: "Lakeside Paragliding Association",
            Venue: "Lakeside Sports Centre, Pokhara",
            StartsInDays: 9,
            DurationHours: 2),
        new(
            "biratnagar-literature-reading",
            "Nepali literature reading",
            "An evening of readings and discussion in Nepali, with tea afterwards.",
            "Six writers read from work published in the last two years, and there is an open "
            + "discussion afterwards. Readings are in Nepali; there is no translation. Seating is "
            + "limited and there is no need to book, though we would rather you arrived early than "
            + "stood at the back. Entry is free.",
            EventCategory.Cultural,
            DistrictCode: "koshi-morang",
            Organiser: "Morang Sahitya Parishad",
            Venue: "Biratnagar Central Library hall",
            StartsInDays: 21),
        new(
            "tihar-celebration-baneshwor",
            "Tihar celebration",
            "Marigold garlands, deusi bhailo and a rangoli competition for the neighbourhood.",
            "Open to everyone in the neighbourhood. Garlands are made from four in the morning and "
            + "the deusi procession goes along the main lane about four in the afternoon. There is a "
            + "rangoli competition for children, with the winners announced before sunset — bring "
            + "your own colours if you want to enter. Food is provided at long tables. No cover "
            + "charge.",
            EventCategory.Cultural,
            DistrictCode: "bagmati-kathmandu",
            Organiser: "Baneshwor Tole Panchayat",
            Venue: "Baneshwor chowk and the adjoining lanes",
            StartsInDays: 34),
        new(
            "butwal-marathon",
            "Butwal road marathon",
            "5 km and 10 km races on open roads, with a separate walk for the same distance.",
            "Two races, both starting and finishing at the traffic Chowk, and a walk that is neither "
            + "timed nor ranked. Registration closes when the field is full or the day before, "
            + "whichever comes first. Entry includes a finisher's shirt. The roads close from about "
            + "six in the morning until the last runner is back, so expect traffic to be diverted. "
            + "Bring water; the aid stations have cups but not much else.",
            EventCategory.Sports,
            DistrictCode: "lumbini-rupandehi",
            Organiser: "Butwal District Athletics Association",
            Venue: "Traffic Chowk, Butwal",
            StartsInDays: 18,
            DurationHours: 4),
        new(
            "it-career-workshop-kathmandu",
            "IT career workshop for students",
            "A free half-day on getting into tech work: CVs, interviews, and what the first year is like.",
            "For students and anyone in the first two years of a job. We go through what a CV is "
            + "actually read for, how a technical interview works, and what to expect in the first "
            + "six months of a job. There is a short practical session on writing a small program, so "
            + "come on a laptop if you have one — there are twenty spare machines in the hall. Free, "
            + "but register at the desk on the day as places fill quickly.",
            EventCategory.Education,
            DistrictCode: "bagmati-kathmandu",
            Organiser: "Sankhu Tech Support",
            Venue: "Machhapuchhre Books, Sundhara",
            StartsInDays: 5,
            DurationHours: 4),
        new(
            "janaki-school-sports-day-bardibas",
            "Janaki Secondary School sports day",
            "The school's annual athletics day, open to parents and former students.",
            "Track and field from seven in the morning: running, shot put, long jump and a relay. "
            + "Parents and old students are welcome; there is seating in the stand. The last relay "
            + "is at about three in the afternoon and the prizes are given immediately after. Entry "
            + "is free, and the school car park is closed on the day, so walk or come early.",
            EventCategory.Sports,
            DistrictCode: "madhesh-dhanusha",
            Organiser: "Janaki Secondary School",
            Venue: "Janaki Secondary School ground, Bardibas",
            StartsInDays: 27),
        new(
            "bhaktapur-craft-fair",
            "Bhaktapur craft fair",
            "Three days of handloom, paper and carved wood from workshops around the valley.",
            "Around forty stalls run by the workshops themselves, so prices are the makers' own and "
            + "negotiable. There is a demonstration of lokta paper-making each afternoon at three, "
            + "and a short talk on Saturday morning about how the trade has changed over thirty "
            + "years. The fair is indoors and does not stop for rain. Entry is free; cash is what "
            + "most stalls take.",
            EventCategory.Exhibition,
            DistrictCode: "bagmati-bhaktapur",
            Organiser: "Bhaktapur Craft Development Committee",
            Venue: "Dattatreya Square and the adjacent halls",
            StartsInDays: 15,
            DurationHours: 9),
        new(
            "dhangadhi-health-camp",
            "Free health camp",
            "General check-ups, eye tests and blood pressure screening, all free.",
            "Run with the district health office. General outpatient, eye testing, blood pressure "
            + "and blood sugar screening, and counselling on nutrition for children. Consultation is "
            + "free and medicine is charged at cost, so bring what you are already taking. There are "
            + "eight doctors and it is busy before eleven. No appointment is needed; registration "
            + "runs from seven.",
            EventCategory.Community,
            DistrictCode: "sudurpashchim-kailali",
            Organiser: "Kailali District Health Office",
            Venue: "Biraj Nagar community hall, Dhangadhi",
            StartsInDays: 7,
            DurationHours: 7),
        new(
            "annapurna-trek-briefing-kathmandu",
            "Annapurna trek briefing",
            "For anyone walking the Annapurna circuit this season: route, permits, and what to carry.",
            "A briefing rather than a briefing course — two hours covering the route options, which "
            + "permits are needed, what the weather is doing this month, and what people most often "
            + "get wrong on the first few days. Led by someone who has walked it four times. There "
            + "is no charge and no booking, but arrive ten minutes early as the room fills. Bring "
            + "any questions about gear and we will answer them properly rather than sell you "
            + "anything.",
            EventCategory.Sports,
            DistrictCode: "bagmati-kathmandu",
            Organiser: "Sundhara trekking cooperative",
            Venue: "Thamel community hall",
            StartsInDays: 3,
            DurationHours: 2),
        new(
            "small-business-registration-clinic-lalitpur",
            "Business registration clinic",
            "Free help with registering a business, with staff from the office in the room.",
            "For anyone starting a small business: what registration involves, what it costs, what "
            + "tax registration means, and which forms are needed. Staff from the ward office will "
            + "be there all day to check paperwork on the spot. Free, no booking, and no obligation "
            + "to register on the day. Bring whatever you already have, even if it is incomplete.",
            EventCategory.Business,
            DistrictCode: "bagmati-lalitpur",
            Organiser: "Lalitpur Metropolitan City ward office",
            Venue: "Gwarko ward office, Lalitpur",
            StartsInDays: 11,
            DurationHours: 6),
        new(
            "bhajan-sandhya-birendranagar-surkhet",
            "Bhajan sandhya at the district hall",
            "An evening of devotional songs in the Karnali bhasha, with chai afterwards.",
            "A bhajan sandhya in the Karnali bhasha, sung by local groups in turn, with no fixed "
            + "order and no rehearsal. Anyone can sit in at the back and nobody will mind. Chai is "
            + "served afterwards and there is a collection for the district flood relief fund, which "
            + "is optional and nobody will ask you twice. Entry is free and there is no need to "
            + "register. The hall is open from five and the singing starts at six.",
            EventCategory.Religious,
            DistrictCode: "karnali-surkhet",
            Organiser: "Karnali Bhajan Samuh",
            Venue: "Karnali district hall, Birendranagar",
            StartsInDays: 13,
            DurationHours: 3),
        new(
            "itahari-cycle-repair-day",
            "Free cycle repair day",
            "Free repairs, safety checks and a spare tube for anyone who rides to work.",
            "Volunteer mechanics at the bazaar, all day, for anyone who turns up on a bicycle or a "
            + "motorbike: brakes, chains, punctures, gears, and a free safety check whether or not "
            + "anything needs doing. Spare tubes and brake blocks are given away until they run "
            + "out, and we would rather somebody came and left with a working brake. Bring your own "
            + "bike and a plug if you have one. No booking, no charge, no obligation.",
            EventCategory.Other,
            DistrictCode: "koshi-sunsari",
            Organiser: "Itahari Bicycle Repair Association",
            Venue: "Itahari bazaar, Sunsari",
            StartsInDays: 9,
            DurationHours: 5),
        new(
            "bhaktapur-pottery-workshop-dharamshala-expired",
            "Pottery workshop at the craft fair",
            "A one-day introduction to throwing pots, run alongside the fair.",
            "Run last year alongside the craft fair and finished with it. The row is kept so the "
            + "public filter's finished path is exercised by seeded data: an event whose end has "
            + "passed must drop off the calendar on its own, without anybody remembering to retract "
            + "it.",
            EventCategory.Exhibition,
            DistrictCode: "bagmati-bhaktapur",
            Organiser: "Bhaktapur Craft Development Committee",
            Venue: "Pottery workshop, Suryabinayak",
            StartsInDays: -40,
            ExpiredDaysAgo: 40),
        new(
            "janaki-school-annual-exam-week",
            "Annual examination week",
            "A holding row so the calendar has an event that has already finished.",
            "Kept for the same reason as the other finished event: the public calendar filters on the "
            + "end date, and it should be demonstrably correct on data rather than only on a test "
            + "double.",
            EventCategory.Education,
            DistrictCode: "madhesh-dhanusha",
            Organiser: "Janaki Secondary School",
            Venue: "Janaki Secondary School, Bardibas",
            StartsInDays: -30,
            ExpiredDaysAgo: 30),
    ];
}