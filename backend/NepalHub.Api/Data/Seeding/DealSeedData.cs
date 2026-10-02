using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data.Seeding;

/// <summary>
/// A starter row for <c>deals</c>. Not the long-term source of offers, for the same reason the job
/// board and the business directory are not: they exist so a fresh install shows a working offers
/// page, and so staff can see what a well-written offer looks like before they write the real one.
/// </summary>
/// <param name="Slug">Stable key, matched on so a staff edit is never undone on restart.</param>
/// <param name="Title">The offer as a reader would search for it.</param>
/// <param name="Summary">One line for the listing cards.</param>
/// <param name="Description">The full terms, plain text.</param>
/// <param name="DiscountText">
/// The offer itself in the business's own words. Text rather than a number because that is what a
/// real offer is: "free chiya with any thakali set" has no percentage in it.
/// </param>
/// <param name="BusinessSlug">The listed business this belongs to, when it has one.</param>
/// <param name="DistrictCode">
/// Where the offer applies. Null for a national offer, which is the case that separates a deal from
/// a business listing.
/// </param>
/// <param name="StartsInDays">Offset from the day it is seeded. Negative for an offer already running.</param>
/// <param name="EndsInDays">Offset from the day it is seeded, inclusive.</param>
/// <param name="ExpiredDaysAgo">
/// Set instead of <paramref name="EndsInDays"/> for an offer that already ran. The seeder writes it
/// directly because it is the only place that can create a published row past its end date, and that
/// is exactly the state the public expiry filter has to cope with; the service would refuse to publish
/// it.
/// </param>
/// <param name="IsPublished">Whether readers can see it. One draft ships so the staff board has one.</param>
internal sealed record DealSeed(
    string Slug,
    string Title,
    string Summary,
    string Description,
    string DiscountText,
    string? BusinessSlug = null,
    string? DistrictCode = null,
    int StartsInDays = 0,
    int? EndsInDays = null,
    int? ExpiredDaysAgo = null,
    bool IsPublished = true);

/// <summary>
/// Fifteen offers, deliberately small. They exist to exercise every filter path the page has —
/// six business categories, districts in four provinces, a national offer, four expired ones and a
/// draft — rather than to look like a real high street.
///
/// <para>
/// <b>Dates are relative, not absolute.</b> Every row stores offsets from the day it is seeded,
/// because a hard-coded end date is correct on the day it was written and wrong forever after: the
/// offers page would quietly empty itself out. The cost is honest. A seeded row is inserted once and
/// never re-dated, so the active ones do eventually expire for real, and from that point the page
/// belongs to staff.
/// </para>
///
/// <para>
/// <b>The businesses are the fictional ones already shipped in the directory.</b> Naming a real shop
/// in a seeded discount would be a claim about a real trading relationship, so every row points at a
/// listing from <see cref="BusinessSeedData"/> — or at nothing at all, which is what the national
/// offer does.
/// </para>
/// </summary>
internal static class DealSeedData
{
    internal static IReadOnlyList<DealSeed> All { get; } =
    [
        new(
            "free-chiya-with-thakali-set-dharan",
            "Free chiya with any thakali set",
            "Any thakali set comes with a chiya on the house at Thakali Kitchen in Dharan.",
            "Order any thakali set from our full menu and a chiya is included at no extra charge. "
            + "One chiya per set, dine-in only. The offer applies to the whole menu, not just the "
            + "daily special, and you do not need to mention the offer when you order — the counter "
            + "will apply it. We cannot honour it on takeaway orders or with the lunch combo, and it "
            + "cannot be combined with any other discount.",
            "Free chiya with any thakali set",
            BusinessSlug: "thakali-kitchen-dharan",
            DistrictCode: "koshi-sunsari",
            StartsInDays: -5,
            EndsInDays: 20),
        new(
            "lunch-combo-250-bhojan-ghar",
            "Lunch combo at Rs. 250",
            "A rotating Nepali thali with rice, dal, tarkari and a sweet, for Rs. 250 a plate.",
            "Lunch is served from 11:30 to 14:30 and the menu changes every day, so what you get "
            + "depends on what the kitchen has. Rs. 250 a plate, and there is no extra charge for "
            + "a second helping. Children under six eat free. Dine-in only; we do not offer this "
            + "on delivery because the price assumes the restaurant has space for you to sit.",
            "Thali lunch for Rs. 250",
            BusinessSlug: "bhojan-ghar-patan",
            DistrictCode: "bagmati-lalitpur",
            StartsInDays: -10,
            EndsInDays: 8),
        new(
            "two-samosa-for-one-newa-momo",
            "Two samosa for the price of one",
            "Buy one samosa, get one free, every day until the end of the month.",
            "Order at the counter and the second samosa is free. Any filling, mixed is fine. This is "
            + "a plain everyday offer and there is nothing to enter or mention — it applies "
            + "automatically. We cannot also apply it to a plate of ten, and it is not combined with "
            + "other offers.",
            "Two samosa for the price of one",
            BusinessSlug: "newa-momo-house-lalitpur",
            DistrictCode: "bagmati-lalitpur",
            StartsInDays: -2,
            EndsInDays: 12),
        new(
            "student-tuition-discount-thapathali",
            "Student discount on one-to-one tuition",
            "Twenty percent off one-to-one English classes for students showing a college ID.",
            "Our tuition is one teacher to one student, and this offer applies to the English "
            + "classes only. Bring your college ID to the first session and the discount is applied "
            + "to the whole course rather than to one month. It cannot be combined with the "
            + "sibling discount, and we ask that you tell us before the course starts rather than "
            + "at the end of it.",
            "20% off for students",
            BusinessSlug: "kathmandu-tuition-centre-thapathali",
            DistrictCode: "bagmati-kathmandu",
            StartsInDays: -3,
            EndsInDays: 30),
        new(
            "first-month-free-everest-web-studio",
            "First month free on a website package",
            "Any website package starts with a month at no charge, no obligation to continue.",
            "We build small websites for businesses and clubs, and this offer covers the first "
            + "month of any package. If you do not want to continue after that month, you walk away "
            + "having paid nothing and you keep whatever has been built so far. There is no minimum "
            + "term attached to this offer. It applies to new clients only.",
            "First month free",
            BusinessSlug: "everest-web-studio-lalitpur",
            DistrictCode: "bagmati-lalitpur",
            StartsInDays: -1,
            EndsInDays: 45),
        new(
            "weekend-gym-pass-thamel",
            "Weekend day pass at Himal Gym",
            "Saturday and Sunday entry for Rs. 400 instead of the Rs. 600 daily rate.",
            "Rs. 400 for a full day on a Saturday or Sunday, which covers the floor, the weights and "
            + "the classes running that day. Towels are extra. This is a walk-in offer and does not "
            + "need booking, but it is limited to twenty passes a day, so come early on a Sunday. "
            + "Members are not affected and the offer does not stack with the student rate.",
            "Rs. 400 weekend day pass",
            BusinessSlug: "himal-gym-thamel",
            DistrictCode: "bagmati-kathmandu",
            StartsInDays: 0,
            EndsInDays: 25),
        new(
            "free-delivery-over-1500-kathmandu",
            "Free delivery on orders over Rs. 1,500",
            "Free delivery inside Kathmandu valley on any order over Rs. 1,500, seven days a week.",
            "This is a nationwide offer rather than one tied to a single shop, so there is no "
            + "business behind it to name. Any order delivered inside the ring road of Kathmandu "
            + "valley qualifies, and the delivery charge is waived automatically at checkout. Orders "
            + "over Rs. 5,000 get free delivery anywhere in Nepal. Outside the valley, delivery is "
            + "charged at the usual rate.",
            "Free delivery over Rs. 1,500",
            DistrictCode: null,
            StartsInDays: -7,
            EndsInDays: 60),
        new(
            "handicraft-bargain-bhaktapur",
            "Bargain season at the craft emporium",
            "Ten percent off handloom and carved-wood pieces bought in a single visit.",
            "Our Bhaktapur showroom carries small handloom and carved-wood pieces from workshops we "
            + "buy from directly. Everything on the floor is negotiable and this offer sets the "
            + "starting point: ten percent off the marked price if you buy in one visit. It does not "
            + "apply to commission pieces, and it cannot be combined with the end-of-season sale we "
            + "run twice a year.",
            "10% off in a single visit",
            BusinessSlug: "himal-handicraft-emporium-bhaktapur",
            DistrictCode: "bagmati-bhaktapur",
            StartsInDays: -4,
            EndsInDays: 14),
        new(
            "haircut-and-beard-500-baneshwor",
            "Haircut and beard trim for Rs. 500",
            "Cut, beard trim and a wash at Rs. 500 instead of Rs. 750.",
            "The Rs. 500 covers a haircut, a beard trim and a wash. Choose your barber at the door "
            + "and the price is the same. It is a walk-in offer, no appointment needed, and the "
            + "waiting time is usually short on weekday afternoons. Not valid with any other "
            + "treatment, and the price is for the standard length — a very short back and sides "
            + "takes longer and we will tell you before we start.",
            "Cut and trim for Rs. 500",
            BusinessSlug: "sajilo-hair-salon-baneshwor",
            DistrictCode: "bagmati-kathmandu",
            StartsInDays: -1,
            EndsInDays: 18),
        new(
            "freight-free-over-50k-dhangadhi",
            "Free freight on orders over Rs. 50,000",
            "Hardware orders above Rs. 50,000 ship anywhere in Sudurpashchim without a freight charge.",
            "For builders and contractors ordering in volume. Rs. 50,000 is the order value before "
            + "delivery, not after. We deliver to any district in Sudurpashchim province and to "
            + "Biratnagar. Bulk pricing is already low and this offer cannot be combined with a "
            + "trade account discount.",
            "Free freight over Rs. 50,000",
            BusinessSlug: "everest-hardware-dhangadhi",
            DistrictCode: "sudurpashchim-kailali",
            StartsInDays: -6,
            EndsInDays: 40),
        new(
            "monsoon-set-dal-bhat-expired",
            "Monsoon dal bhat set",
            "A set dal bhat with a second helping, gone as quickly as the rain.",
            "This ran through the monsoon and is over. It is kept in the database so the offers page "
            + "can be seen handling an expired row, which is the case that matters: an offer past "
            + "its end date must disappear from the public list on its own without anybody "
            + "remembering to retract it.",
            "Dal bhat set with a second helping",
            BusinessSlug: "thakali-kitchen-dharan",
            DistrictCode: "koshi-sunsari",
            StartsInDays: -60,
            ExpiredDaysAgo: 9),
        new(
            "festival-gift-hamper-lalitpur-expired",
            "Festival gift hamper",
            "A hamper of local sweets, tea and dried fruit, wrapped for Tihar.",
            "Offered through the festival and finished with it. The row stays so that the expired "
            + "path in the public filter is exercised by real data rather than only by tests.",
            "Wrapped hamper at Rs. 1,800",
            BusinessSlug: "sajha-coffee-lalitpur",
            DistrictCode: "bagmati-lalitpur",
            StartsInDays: -75,
            ExpiredDaysAgo: 30),
        new(
            "pokhara-adventure-season-deal-expired",
            "Pokhara adventure season",
            "Discounted lodge rates for the trekking season.",
            "Lodge rates agreed for the trekking season, now over. Kept for the same reason as the "
            + "other expired rows: the expiry filter needs published data that has genuinely ended.",
            "Lodge rates at Rs. 2,200 a night",
            BusinessSlug: "shivapuri-lodge-nagarkot",
            DistrictCode: "gandaki-kaski",
            StartsInDays: -120,
            ExpiredDaysAgo: 55),
        new(
            "cafe-brunch-combination-lalitpur-expired",
            "Brunch and coffee combination",
            "Any breakfast with a filter coffee, for the mornings only.",
            "A short breakfast offer that ran at weekends and is now finished. Kept so the expired "
            + "branch of the public filter is covered by seeded data.",
            "Breakfast plus filter coffee",
            BusinessSlug: "sajha-coffee-lalitpur",
            DistrictCode: "bagmati-lalitpur",
            StartsInDays: -90,
            ExpiredDaysAgo: 40),
        new(
            "school-admissions-open-draft",
            "Admissions open for the new session",
            "A draft: written ahead of the admissions window and not yet ready to publish.",
            "Held as a draft on purpose. Admissions open in the middle of the year, so seeding this "
            + "as live would put an offer on the public page months before it was true. It exists so "
            + "the staff board always has one row to show what a draft looks like.",
            "Early-bird admissions rate",
            BusinessSlug: "everest-english-school-pokhara",
            DistrictCode: "gandaki-kaski",
            StartsInDays: 30,
            EndsInDays: 90,
            IsPublished: false),
    ];
}