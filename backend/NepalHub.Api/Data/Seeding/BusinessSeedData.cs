using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data.Seeding;

/// <summary>One weekday's hours, as the seeder states them.</summary>
internal sealed record BusinessHoursSeed(DayOfWeek DayOfWeek, TimeOnly OpensAt, TimeOnly ClosesAt);

internal sealed record BusinessSeed(
    string Slug,
    string Name,
    string Description,
    BusinessCategory Category,
    string DistrictCode,
    string? Address = null,
    string? Phone = null,
    string? Email = null,
    string? Website = null,
    bool IsPublished = true,
    bool IsVerified = true,
    IReadOnlyList<BusinessHoursSeed>? OpeningHours = null);

/// <summary>
/// The starter directory: twenty-five listings spread across all twelve categories and all seven
/// provinces.
///
/// This is starter content, not the long-term source, for the same reason the quiz pool and the
/// resource catalogue are: so a fresh install shows a working directory rather than an empty page,
/// and so staff can see what a well-written listing looks like before they write the real one.
///
/// Four decisions are worth stating, because each would otherwise look like a bug:
///
/// <para>
/// <b>The publication states are deliberate, not accidental.</b> A directory where every listing is
/// published and verified proves nothing about the staff screen. Three rows exist to exercise states
/// the service must handle and that staff need to see: a draft waiting to be checked, a listing that
/// was checked before it went live, and one that is published but no longer carries its badge.
/// </para>
///
/// <para>
/// <b>Bagmati is heavily weighted, on purpose.</b> Fifteen of the twenty-five listings are in
/// Kathmandu valley. That is where most Nepali users are and where a fresh install needs the
/// province filter to have something to do; the other six provinces each have at least one, so
/// every province filter returns something. A flat spread would have made the directory look
/// national while being useless for anyone actually browsing their own city.
/// </para>
///
/// <para>
/// <b>Opening hours are relative to nothing.</b> They are wall-clock times, unlike the job board's
/// relative deadlines, because a shop's nine-to-six does not expire. Nothing here needs re-seeding
/// as time passes, which is what makes this a better shape than the deadline fields the job seeder
/// has to work around.
/// </para>
///
/// <para>
/// <b>District codes are reference codes, not foreign keys typed by hand.</b> The seeder resolves
/// each code through the same <c>districts</c> rows the reference endpoints serve, so a code that
/// does not exist stops startup with a message naming the row rather than producing a foreign-key
/// error later.
/// </para>
///
/// <para>
/// <b>Every listing carries a district.</b> This is the one place the directory departs from the job
/// board, and it is deliberate: a remote job has no address, but a local business with no location
/// cannot be found by anyone browsing their own district.
/// </para>
/// </summary>
internal static class BusinessSeedData
{
    /// <summary>Monday to Saturday, the ordinary week, as a shared shape.</summary>
    private static IReadOnlyList<BusinessHoursSeed> StandardWeek { get; } =
    [
        new(DayOfWeek.Sunday, new TimeOnly(9, 0), new TimeOnly(19, 0)),
        new(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(19, 0)),
        new(DayOfWeek.Tuesday, new TimeOnly(9, 0), new TimeOnly(19, 0)),
        new(DayOfWeek.Wednesday, new TimeOnly(9, 0), new TimeOnly(19, 0)),
        new(DayOfWeek.Thursday, new TimeOnly(9, 0), new TimeOnly(19, 0)),
        new(DayOfWeek.Friday, new TimeOnly(9, 0), new TimeOnly(19, 0)),
        new(DayOfWeek.Saturday, new TimeOnly(9, 0), new TimeOnly(19, 0)),
    ];

    /// <summary>The same week with Sunday closed, for a business that keeps the sabbath.</summary>
    private static IReadOnlyList<BusinessHoursSeed> ClosedSunday { get; } =
        StandardWeek.Where(hours => hours.DayOfWeek != DayOfWeek.Sunday).ToList();

    /// <summary>
    /// The same week with Saturday closed, for a business that takes Sunday instead. Present so
    /// the closed-weekday path is visible in a fresh install rather than only in tests.
    /// </summary>
    private static IReadOnlyList<BusinessHoursSeed> ClosedSaturday { get; } =
        StandardWeek.Where(hours => hours.DayOfWeek != DayOfWeek.Saturday).ToList();

    /// <summary>
    /// A week that runs past midnight on Friday and Saturday. Present because a bar is exactly the
    /// business that would otherwise find the hours form refuses to save its schedule.
    /// </summary>
    private static IReadOnlyList<BusinessHoursSeed> LateWeek { get; } =
    [
        new(DayOfWeek.Sunday, new TimeOnly(16, 0), new TimeOnly(23, 0)),
        new(DayOfWeek.Monday, new TimeOnly(16, 0), new TimeOnly(23, 0)),
        new(DayOfWeek.Tuesday, new TimeOnly(16, 0), new TimeOnly(23, 0)),
        new(DayOfWeek.Wednesday, new TimeOnly(16, 0), new TimeOnly(23, 0)),
        new(DayOfWeek.Thursday, new TimeOnly(16, 0), new TimeOnly(23, 59)),
        new(DayOfWeek.Friday, new TimeOnly(16, 0), new TimeOnly(2, 0)),
        new(DayOfWeek.Saturday, new TimeOnly(16, 0), new TimeOnly(2, 0)),
    ];

    internal static IReadOnlyList<BusinessSeed> All { get; } =
    [
        // ---------------------------------------------------------------- restaurants
        new(
            "newa-momo-house-lalitpur",
            "Newa Momo House",
            "A family-run momo house that has been folding dumplings on a back street in Lalitpur "
            + "since 2011, and is now known well enough that people walk past two closer branches to "
            + "eat here. The steamed chicken momo is the reason to come: the filling is seasoned by "
            + "hand every morning rather than bought in, which is why the shop is small and does not "
            + "franchise. Order at the counter, take your number, and wait at the plastic tables at "
            + "the front. It gets loud at lunch and after six, which is when most of Kathmandu eats. "
            + "Cash and cards are both fine. There is no delivery, because the dumplings do not "
            + "travel well and the owner decided not to pretend otherwise.",
            BusinessCategory.Restaurants,
            "bagmati-lalitpur",
            Address: "Gwarko, opposite the municipality gate",
            Phone: "9841123456",
            Email: "hello@newamomohouse.com.np",
            Website: "https://newamomohouse.com.np/",
            OpeningHours: StandardWeek),
        new(
            "bhojan-ghar-patan",
            "Bhojan Ghar",
            "Newar cuisine in the old part of Patan, in a courtyard house that has been serving "
            + "bara, chatamari and samay baji for three generations. This is the place people bring "
            + "visitors who want to eat something they would not get at home, so the bara is the "
            + "reason to come and the rest of the menu is best approached by asking what is good "
            + "today. Lunch only, because that is when the kitchen is actually cooking. Cash "
            + "preferred. Walk in; there is no booking and there is a queue at weekends.",
            BusinessCategory.Restaurants,
            "bagmati-lalitpur",
            Address: "Kumari Ghar, Sundhara Chowk",
            Phone: "0114456789",
            OpeningHours: [.. ClosedSunday.Where(hours => hours.DayOfWeek != DayOfWeek.Saturday)]),
        new(
            "thakali-kitchen-dharan",
            "Thakali Kitchen",
            "Thakali food in the middle of Dharan, cooked by a family from Khotang who came south "
            + "for work and stayed. Dharye, khatta and the seasonal sel roti are all here, and the "
            + "portion size is the point: most of this food is meant to be shared between two or "
            + "three people. Takeaway is available if you are in a hurry, though the bhat is better "
            + "eaten hot. Open for lunch and dinner, closed on Tuesdays.",
            BusinessCategory.Restaurants,
            "koshi-sunsari",
            Address: "Line No 2, near the traffic office",
            Phone: "9812345678",
            OpeningHours: [
                new(DayOfWeek.Sunday, new TimeOnly(11, 0), new TimeOnly(21, 0)),
                new(DayOfWeek.Monday, new TimeOnly(11, 0), new TimeOnly(21, 0)),
                new(DayOfWeek.Wednesday, new TimeOnly(11, 0), new TimeOnly(21, 0)),
                new(DayOfWeek.Thursday, new TimeOnly(11, 0), new TimeOnly(21, 0)),
                new(DayOfWeek.Friday, new TimeOnly(11, 0), new TimeOnly(21, 0)),
                new(DayOfWeek.Saturday, new TimeOnly(11, 0), new TimeOnly(21, 0)),
            ]),

        // ---------------------------------------------------------------- cafes
        new(
            "sajha-coffee-lalitpur",
            "Sajha Coffee",
            "A small coffee roaster in Jawalakhel, roasting on Tuesdays and Fridays and selling "
            + "whatever came off the roaster that morning. The Nepali single-origin lots change with "
            + "the harvest, so what is on the board changes with it, and the staff will tell you what "
            + "tastes like what rather than repeating a description off a bag. It is the sort of place "
            + "people bring a book to on a Saturday morning. No table service, no bookings. The back "
            + "room is quiet in the afternoons and loud at weekends.",
            BusinessCategory.Cafes,
            "bagmati-lalitpur",
            Address: "Jawalakhel, near the bypass junction",
            Phone: "9801234567",
            Website: "https://sajhacoffee.com.np/",
            OpeningHours: [
                new(DayOfWeek.Sunday, new TimeOnly(8, 0), new TimeOnly(18, 0)),
                new(DayOfWeek.Monday, new TimeOnly(7, 0), new TimeOnly(18, 0)),
                new(DayOfWeek.Tuesday, new TimeOnly(7, 0), new TimeOnly(18, 0)),
                new(DayOfWeek.Wednesday, new TimeOnly(7, 0), new TimeOnly(18, 0)),
                new(DayOfWeek.Thursday, new TimeOnly(7, 0), new TimeOnly(18, 0)),
                new(DayOfWeek.Friday, new TimeOnly(7, 0), new TimeOnly(18, 0)),
                new(DayOfWeek.Saturday, new TimeOnly(8, 0), new TimeOnly(18, 0)),
            ]),
        new(
            "himalayan-cafe-pokhara",
            "Himalayan Beans Cafe",
            "Second-wave coffee in Pokhara, in a strip of shops that has filled up with cafes over "
            + "the last few years. What separates this one is the cold brew on a proper filter rather "
            + "in a jug, and a room that does not fill with music loud enough to make conversation "
            + "work. Free wifi with no time limit and plenty of plug points, which is why a lot of "
            + "remote workers end up here in season. Breakfast until eleven, then sandwiches. Cash "
            + "and eSewa accepted.",
            BusinessCategory.Cafes,
            "gandaki-kaski",
            Address: "Lakeside Road, opposite the peace pagoda path",
            Phone: "061456789",
            Website: "https://himalayanbeans.com.np/",
            OpeningHours: StandardWeek),

        // ---------------------------------------------------------------- hotels
        new(
            "shivapuri-lodge-nagarkot",
            "Shivapuri Lodge",
            "A lodge on the ridge above Nagarkot, built for the sunrise rather than the view of the "
            + "park, with twenty rooms and no restaurant attached. People stay for two nights and "
            + "leave early to walk to the tower, so the practical question is whether you can stand "
            + "up in the dark at five in the morning: you can. Rooms are simple and warm, with "
            + "blankets all year because it is cold on that hill from October. Book by phone, "
            + "preferably in advance for October and November when the whole ridge fills.",
            BusinessCategory.HotelsAndAccommodation,
            "bagmati-bhaktapur",
            Address: "Nagarkot ridge, past the army post",
            Phone: "9809876543",
            Website: "https://shivapurilodge.com.np/",
            OpeningHours: StandardWeek),
        new(
            "janaki-guest-house-bharatpur",
            "Janaki Guest House",
            "A family guest house in Sauraha, a few minutes' walk from the park gate, with a walled "
            + "garden that makes it worth staying in even when you are not going on a walk. Rooms are "
            + "simple, the family cooks dinner for guests on request, and the whole thing runs on "
            + "being asked rather than on a reception desk. Chitwan is not a fast drive from "
            + "Kathmandu, so most people book ahead for the busy season and accept a longer stay "
            + "outside it.",
            BusinessCategory.HotelsAndAccommodation,
            "bagmati-chitwan",
            Address: "Sauraha, near the Tiger Tops gate",
            Phone: "9803456789",
            OpeningHours: StandardWeek,

            // Checked but not published: a listing someone has verified and is waiting to put out.
            // Both flags true on this row is why they are two columns rather than one status.
            IsPublished: false,
            IsVerified: true),

        // ---------------------------------------------------------------- gyms
        new(
            "himal-gym-thamel",
            "Himal Gym and Fitness",
            "A neighbourhood gym in Thamel rather than a chain: weights, a rack of benches, a "
            + "reasonable set of dumbbells, and trainers who are there in the evening when the "
            + "office crowd comes in. Day passes are available without joining, which is the honest "
            + "way to try it. The changing rooms are small and there are only three. Locker keys are "
            + "given at the desk. Peak hours are six to eight in the evening and the racks are hard "
            + "to get at then; come at six in the morning if you can.",
            BusinessCategory.GymsAndFitness,
            "bagmati-kathmandu",
            Address: "Thamel, off the main drag",
            Phone: "9851234567",
            OpeningHours: [
                new(DayOfWeek.Sunday, new TimeOnly(6, 0), new TimeOnly(21, 0)),
                new(DayOfWeek.Monday, new TimeOnly(6, 0), new TimeOnly(21, 0)),
                new(DayOfWeek.Tuesday, new TimeOnly(6, 0), new TimeOnly(21, 0)),
                new(DayOfWeek.Wednesday, new TimeOnly(6, 0), new TimeOnly(21, 0)),
                new(DayOfWeek.Thursday, new TimeOnly(6, 0), new TimeOnly(21, 0)),
                new(DayOfWeek.Friday, new TimeOnly(6, 0), new TimeOnly(21, 0)),
                new(DayOfWeek.Saturday, new TimeOnly(7, 0), new TimeOnly(20, 0)),
            ]),

        // ---------------------------------------------------------------- salons
        new(
            "sajilo-hair-salon-baneshwor",
            "Sajilo Hair and Beauty Salon",
            "A salon in Baneshwor that has been cutting hair on the same street for fifteen years, "
            + "which in this city is a long time. Bridal packages, threading, colouring and ordinary "
            + "cuts, done by people who have been doing it long enough to have their own regulars. "
            + "Walk-ins for haircuts; book for the longer work and for weekends. Prices are posted at "
            + "the counter and are lower than the salons along the tourist strip, which is the reason "
            + "locals come here rather than to those.",
            BusinessCategory.SalonsAndBeauty,
            "bagmati-kathmandu",
            Address: "Baneshwor, near the main bazaar",
            Phone: "9843456789",
            OpeningHours: StandardWeek),
        new(
            "gorkha-tailoring-biratnagar",
            "Gorkha Tailoring",
            "A tailoring shop in Biratnagar that makes and alters sherwani, daura and suits to "
            + "measure, with the measurements kept on file so a repeat order is a week rather than a "
            + "month. Shirt and suit stitching is the bulk of the work. The owner measures rather "
            + "than trusting the numbers over the phone, so bring something you already own that "
            + "fits. Payment on collection. It gets busy for weeks around Dashain and the wedding "
            + "season, so order then.",
            BusinessCategory.SalonsAndBeauty,
            "koshi-morang",
            Address: "Koiri Bazaar",
            Phone: "9822345678",
            OpeningHours: ClosedSunday),

        // ---------------------------------------------------------------- education
        new(
            "kathmandu-tuition-centre-thapathali",
            "Kathmandu Tuition Centre",
            "A tuition centre in Thapathali running grade five to twelve in small groups, with the "
            + "grader and the medium the deciding factor rather than the marketing. Science and "
            + "mathematics are the strongest subjects, which reflects what the students who come here "
            + "are actually stuck on. Classes run in the evening after school and twice on Sunday. "
            + "There is a demo class before anyone pays, and the honest advice about whether this is "
            + "the right centre for a particular student is free.",
            BusinessCategory.EducationAndTuition,
            "bagmati-kathmandu",
            Address: "Thapathali, near the ring road",
            Phone: "9805678901",
            Website: "https://ktctuition.edu.np/",
            OpeningHours: [
                new(DayOfWeek.Sunday, new TimeOnly(10, 0), new TimeOnly(16, 0)),
                new(DayOfWeek.Monday, new TimeOnly(15, 0), new TimeOnly(20, 0)),
                new(DayOfWeek.Tuesday, new TimeOnly(15, 0), new TimeOnly(20, 0)),
                new(DayOfWeek.Wednesday, new TimeOnly(15, 0), new TimeOnly(20, 0)),
                new(DayOfWeek.Thursday, new TimeOnly(15, 0), new TimeOnly(20, 0)),
                new(DayOfWeek.Friday, new TimeOnly(15, 0), new TimeOnly(20, 0)),
            ]),
        new(
            "everest-english-school-pokhara",
            "Everest English School",
            "An English school in Pokhara teaching conversation and grammar to adults who need it for "
            + "work: nurses preparing for language exams, staff moving to a hotel, people who have "
            + "picked up some English on their own and want it to be reliable. Small classes and a lot "
            + "of speaking, which is the part most courses skip and most students need. Three intakes "
            + "a year. Times can be moved for shift workers, which is worth asking about before "
            + "enrolling.",
            BusinessCategory.EducationAndTuition,
            "gandaki-kaski",
            Address: "Prithvi Highway, near the bus park",
            Phone: "9841122334",
            OpeningHours: StandardWeek),

        // ---------------------------------------------------------------- repair
        new(
            "valley-motor-works-patan",
            "Valley Motor Works",
            "A two-bay motor workshop in Patan doing servicing, repair and two-wheeler work, "
            + "including the small electrical faults that otherwise turn into expensive ones. The "
            + "practical thing about this shop is that it will tell you when something does not need "
            + "doing, which is rarer than it should be. Two-wheelers only, no cars. Same-day work for "
            + "most repairs. Parts are charged at cost and you are welcome to see the old one.",
            BusinessCategory.RepairAndMaintenance,
            "bagmati-lalitpur",
            Address: "Mangal Bazaar, near the ring road",
            Phone: "9841234501",
            OpeningHours: ClosedSunday),
        new(
            "himalayan-craft-repair-bhaktapur",
            "Himalayan Craft Repair",
            "Repair for prayer flags, singing bowls, carved wooden figures and the brass work that "
            + "travellers buy and then break or leave in a hotel room. Fixes are done by hand and "
            + "nothing that is beyond saving is accepted, which is why the owner will turn work away. "
            + "Bring the item and wait if it is small; larger repairs take a few days. This is a "
            + "craft shop, not a factory, so it closes for the festivals when the family is away.",
            BusinessCategory.RepairAndMaintenance,
            "bagmati-bhaktapur",
            Address: "Taumadhi Square",
            Phone: "9804456789",
            OpeningHours: [
                new(DayOfWeek.Sunday, new TimeOnly(10, 0), new TimeOnly(17, 0)),
                new(DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(17, 0)),
                new(DayOfWeek.Tuesday, new TimeOnly(10, 0), new TimeOnly(17, 0)),
                new(DayOfWeek.Wednesday, new TimeOnly(10, 0), new TimeOnly(17, 0)),
                new(DayOfWeek.Thursday, new TimeOnly(10, 0), new TimeOnly(17, 0)),
                new(DayOfWeek.Friday, new TimeOnly(10, 0), new TimeOnly(17, 0)),
            ]),

        // ---------------------------------------------------------------- shops
        new(
            "himal-handicraft-emporium-bhaktapur",
            "Himal Handicraft Emporium",
            "A cooperative shop on Taumadhi Square selling paper lokta, felt, woollen goods and "
            + "carving made by member groups across the valley, rather than imported items with a Nepali "
            + "label. The prices are fixed and shown, which is unusual for lokta and is the point: "
            + "visitors who have been haggled on the way in can see here what the going rate is. The "
            + "cooperative returns a share of sales to the groups that made the goods. Fixed price, no "
            + "bargaining, and the receipt says which group made your scarf.",
            BusinessCategory.Shops,
            "bagmati-bhaktapur",
            Address: "Taumadhi, next to the pagoda",
            Phone: "9806543210",
            Website: "https://himalhandicraft.org.np/",
            OpeningHours: StandardWeek),
        new(
            "everest-hardware-dhangadhi",
            "Everest Hardware",
            "A hardware shop in Dhangadhi with the cement, nails, fittings and paint that a house or "
            + "a workshop actually needs, plus a delivery service within the city for anyone who would "
            + "rather not carry bags. It is the sort of shop where the staff know what the building "
            + "supply shops in Kathmandu charge, which is useful if you are buying in volume. Rates for "
            + "bulk cement, credit for regular tradespeople, and delivery by six in the evening.",
            BusinessCategory.Shops,
            "sudurpashchim-kailali",
            Address: "Mahakali Chowk",
            Phone: "9814567890",

            // Published and checked but with no hours stated. The card for this one must say the
            // hours are not listed rather than claiming it is shut.
            OpeningHours: null),
        new(
            "machhapuchhre-books-kathmandu",
            "Machhapuchhre Books",
            "A second-hand and academic bookshop in Pulchowk that stocks the second-hand university "
            + "textbooks the surrounding colleges cannot justify buying new every year. Science and "
            + "economics are the deepest sections; medical and engineering are thinner. Everything is "
            + "sorted by subject and priced on the shelf. They also buy books, which is worth knowing "
            + "before taking a course's worth of paper to a shop and being offered scrap price for it.",
            BusinessCategory.Shops,
            "bagmati-kathmandu",
            Address: "Pulchowk, near the college gate",
            Phone: "9807654321",
            OpeningHours: [
                new(DayOfWeek.Sunday, new TimeOnly(11, 0), new TimeOnly(19, 0)),
                new(DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(19, 0)),
                new(DayOfWeek.Tuesday, new TimeOnly(10, 0), new TimeOnly(19, 0)),
                new(DayOfWeek.Wednesday, new TimeOnly(10, 0), new TimeOnly(19, 0)),
                new(DayOfWeek.Thursday, new TimeOnly(10, 0), new TimeOnly(19, 0)),
                new(DayOfWeek.Friday, new TimeOnly(10, 0), new TimeOnly(19, 0)),
                new(DayOfWeek.Saturday, new TimeOnly(10, 0), new TimeOnly(19, 0)),
            ]),

        // ---------------------------------------------------------------- IT
        new(
            "sankhu-tech-support-baneshwor",
            "Sankhu Tech Support",
            "A computer repair and small-network shop in Baneshwor doing desktop and laptop repairs, "
            + "data recovery from failing drives, and cabling for small offices. The useful thing "
            + "about this shop is that it gives an honest assessment before starting: if a repair is "
            + "not worth the parts, they say so. On-site calls within Baneshwor and Kathmandu for "
            + "network work. Same-day for most software problems, a few days for a drive recovery.",
            BusinessCategory.InformationTechnology,
            "bagmati-kathmandu",
            Address: "Baneshwor, near the ring road",
            Phone: "9808765432",
            OpeningHours: StandardWeek,

            // A draft, waiting to be checked. Exercises the staff-only branch of the list and the
            // "why can this not be published yet" list on the editor.
            //
            // This row is the only InformationTechnology listing, and it is deliberately a draft, so
            // the second one below exists for a reason: a category whose only listing is hidden is a
            // category whose filter returns nothing to a reader, which is indistinguishable from a
            // broken search.
            IsPublished: false,
            IsVerified: false),
        new(
            "everest-web-studio-lalitpur",
            "Everest Web Studio",
            "A two-person web development shop in Lalitpur that builds and maintains sites for small "
            + "businesses, mostly the ones who need something to send a customer to rather than "
            + "something to run a company on. WordPress sites, small shops on hosted platforms, and "
            + "the hosting and domain renewals that go with them. They will tell you honestly when a "
            + "site does not need rebuilding, which is more common than the invoice implies. Fixed "
            + "quotes before work starts, and a handover document so the site does not depend on them "
            + "being reachable.",
            BusinessCategory.InformationTechnology,
            "bagmati-lalitpur",
            Address: "Gwarko, near the ring road",
            Phone: "9802234567",
            Email: "hello@everestweb.np",
            Website: "https://everestweb.np/",
            OpeningHours: [
                new(DayOfWeek.Sunday, new TimeOnly(10, 0), new TimeOnly(17, 0)),
                new(DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(18, 0)),
                new(DayOfWeek.Tuesday, new TimeOnly(10, 0), new TimeOnly(18, 0)),
                new(DayOfWeek.Wednesday, new TimeOnly(10, 0), new TimeOnly(18, 0)),
                new(DayOfWeek.Thursday, new TimeOnly(10, 0), new TimeOnly(18, 0)),
                new(DayOfWeek.Friday, new TimeOnly(10, 0), new TimeOnly(18, 0)),
                new(DayOfWeek.Saturday, new TimeOnly(10, 0), new TimeOnly(14, 0)),
            ]),

        // ---------------------------------------------------------------- professional
        new(
            "sagar-associates-kathmandu",
            "Sagar and Associates",
            "An audit and tax practice in Kamal Pokhari, small enough that the person you meet does "
            + "the work rather than passing it to a junior. Audits, tax filings for companies and "
            + "individuals, and the VAT and payroll filings that are now routine obligations. A lot "
            + "of the clients are small businesses who need someone who will also answer the question "
            + "they did not think to ask. Consultations by appointment. Fees quoted before work starts.",
            BusinessCategory.ProfessionalServices,
            "bagmati-kathmandu",
            Address: "Kamal Pokhari, near the embassy road",
            Phone: "9809876543",
            Email: "contact@sagarassociates.com.np",
            Website: "https://sagarassociates.com.np/",
            OpeningHours: [
                new(DayOfWeek.Sunday, new TimeOnly(10, 0), new TimeOnly(16, 0)),
                new(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0)),
                new(DayOfWeek.Tuesday, new TimeOnly(9, 0), new TimeOnly(17, 0)),
                new(DayOfWeek.Wednesday, new TimeOnly(9, 0), new TimeOnly(17, 0)),
                new(DayOfWeek.Thursday, new TimeOnly(9, 0), new TimeOnly(17, 0)),
                new(DayOfWeek.Friday, new TimeOnly(9, 0), new TimeOnly(17, 0)),
            ]),
        new(
            "lumbini-legal-consultancy-butwal",
            "Lumbini Legal Consultancy",
            "A legal consultancy in Butwal doing property transfer and registration, family and "
            + "inheritance matters, and the notarisation that a surprising number of errands turn out "
            + "to require. The property work is the bulk of it, and the reason to use this office "
            + "rather than a broker is that the transfer is actually done rather than handed off. Fees "
            + "quoted after seeing the documents, not before. First consultation is free.",
            BusinessCategory.ProfessionalServices,
            "lumbini-rupandehi",
            Address: "Traffic Chowk, near the bus station",
            Phone: "9815678901",
            Email: "info@lumbinilegal.com.np",
            OpeningHours: ClosedSunday),
        new(
            "karnali-highland-consultancy-birendranagar",
            "Karnali Highland Consultancy",
            "An engineering and advisory practice in Birendranagar serving the Karnali and Sudurpashchim "
            + "valleys, doing water supply and irrigation feasibility work, structural assessment of "
            + "older buildings, and the survey work that precedes a road or bridge tender. Small, and "
            + "the partners do the site visits themselves rather than delegating them. The work is "
            + "mostly outside Birendranagar, so travel costs are quoted separately and honestly "
            + "estimated before a mobilisation rather than added afterwards.",
            BusinessCategory.ProfessionalServices,
            "karnali-dailekh",
            Address: "Birendranagar, near the district council",
            Phone: "9801432109",
            Email: "office@karnalihighland.com.np",
            OpeningHours: [
                new(DayOfWeek.Sunday, new TimeOnly(10, 0), new TimeOnly(16, 0)),
                new(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0)),
                new(DayOfWeek.Tuesday, new TimeOnly(9, 0), new TimeOnly(17, 0)),
                new(DayOfWeek.Wednesday, new TimeOnly(9, 0), new TimeOnly(17, 0)),
                new(DayOfWeek.Thursday, new TimeOnly(9, 0), new TimeOnly(17, 0)),
                new(DayOfWeek.Friday, new TimeOnly(9, 0), new TimeOnly(17, 0)),
            ]),

        // ---------------------------------------------------------------- healthcare
        new(
            "kathmandu-dental-care-thapathali",
            "Kathmandu Dental Care",
            "A dental clinic in Thapathali doing check-ups, fillings, extractions and the "
            + "orthodontic work people in Kathmandu used to travel to Delhi for. Small and unhurried, "
            + "which is most of what people want from a dentist. The waiting room is not busy in the "
            + "morning and fills up after four. Appointments preferred, walk-ins accepted if they can "
            + "see you. X-rays taken on site.",
            BusinessCategory.HealthcareServices,
            "bagmati-kathmandu",
            Address: "Thapathali, near the post office",
            Phone: "9846789012",

            // Published, unchecked, and with no opening hours at all. This is the state unverifying
            // creates, and it is also why an empty hours set has to be a first-class answer: the
            // reader is told "open now: unknown", which is true, rather than shown a red closed
            // badge for a clinic that certainly opens.
            OpeningHours: null,
            IsPublished: true,
            IsVerified: false),
        new(
            "everest-pharmacy-biratnagar",
            "Evereast Pharmacy",
            "A pharmacy in Biratnagar dispensing prescriptions and selling the medicines that do not "
            + "need one, plus the usual small things: thermometers, glucose strips, masks. The "
            + "counter staff will check a prescription for interactions if you ask, which is worth "
            + "doing rather than assuming. Open late, because pharmacies in this city are used at odd "
            + "hours. No home delivery.",
            BusinessCategory.HealthcareServices,
            "koshi-morang",
            Address: "Golchha Chowk, near the market",
            Phone: "9812345098",
            OpeningHours: [
                new(DayOfWeek.Sunday, new TimeOnly(7, 0), new TimeOnly(22, 0)),
                new(DayOfWeek.Monday, new TimeOnly(7, 0), new TimeOnly(22, 0)),
                new(DayOfWeek.Tuesday, new TimeOnly(7, 0), new TimeOnly(22, 0)),
                new(DayOfWeek.Wednesday, new TimeOnly(7, 0), new TimeOnly(22, 0)),
                new(DayOfWeek.Thursday, new TimeOnly(7, 0), new TimeOnly(22, 0)),
                new(DayOfWeek.Friday, new TimeOnly(7, 0), new TimeOnly(22, 0)),
                new(DayOfWeek.Saturday, new TimeOnly(7, 0), new TimeOnly(22, 0)),
            ]),

        // ---------------------------------------------------------------- other
        new(
            "janaki-school-bardibas",
            "Janaki Secondary School",
            "A secondary school in Bardibas running grades one to ten, with the medium available in "
            + "both Nepali and English and a science stream from grade eleven. The practical detail "
            + "that matters here is the fee structure: it is published at the office, there are "
            + "scholarship places each year for students from the surrounding villages, and the "
            + "headmaster will tell you about them in an admission meeting rather than leaving "
            + "families to ask. Admission is on the published test result, not by interview alone.",
            BusinessCategory.EducationAndTuition,
            "madhesh-dhanusha",
            Address: "Bardibas, near the highway",
            Phone: "9803876541",
            OpeningHours: ClosedSaturday),
        new(
            "everest-craft-house-pokhara",
            "Everest Craft House",
            "A fair-trade craft shop in Pokhara selling felt, pashmina, paper and carved wood from "
            + "producer groups in the hills, with the group named on the tag. A worthwhile stop for "
            + "anyone buying craft in the valley who has been told the shop downstairs has better "
            + "prices, and it does: these are fixed and posted, so there is nothing to negotiate and "
            + "no need to try. The collection changes as groups bring new work, so it is worth "
            + "looking twice. Card and cash.",
            BusinessCategory.Other,
            "gandaki-kaski",
            Address: "Lakeside, near the bank of the lake",
            Phone: "9846789012",
            Website: "https://everestcraft.org.np/",
            OpeningHours: ClosedSunday),
    ];
}