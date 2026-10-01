using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data.Seeding;

internal sealed record JobSeed(
    string Slug,
    string Title,
    string Organization,
    string Description,
    JobCategory Category,
    EmploymentType EmploymentType,
    WorkMode WorkMode,
    string? DistrictCode = null,
    string ApplicationUrl = "https://www.merokaipatra.com/",
    int? ExpiresInDays = null,
    int? ExpiredDaysAgo = null,
    bool IsPublished = true);

/// <summary>
/// The starter job board: sixteen postings spread across all seven provinces, all four
/// employment types and all three work modes.
///
/// This is starter content, not the long-term source, for the same reason the quiz pool and the
/// resource catalogue are: so a fresh install shows a working board rather than an empty page,
/// and so staff can see what a well-written posting looks like before they write the real
/// catalogue.
///
/// Three decisions are worth stating, because each would otherwise look like a bug:
///
/// <para>
/// <b>Deadlines are relative, not absolute.</b> Each posting stores its deadline as an offset
/// from the day it is seeded. A hard-coded date is correct on the day it was written and wrong
/// forever after — the live postings would quietly close and the board would empty itself out.
/// The cost is honest: a seeded row is inserted once and never re-dated, so it does eventually
/// expire for real, and from that point the board belongs to staff.
/// </para>
///
/// <para>
/// <b>Two postings are already closed.</b> They are inserted as published rows with a past
/// deadline, which is the real situation the expiry filter exists for: something that was
/// legitimately live and has since closed. The service refuses to publish a past deadline, so
/// only the seeder can write these rows, and that is deliberate.
/// </para>
///
/// <para>
/// <b>District codes are reference codes, not foreign keys typed by hand.</b> The seeder
/// resolves each code through the same <c>districts</c> rows the reference endpoints serve, and
/// a code that does not exist leaves the posting without a district rather than inventing one.
/// Remote postings carry no district at all, because a remote job has no address.
/// </para>
/// </summary>
internal static class JobSeedData
{
    internal static IReadOnlyList<JobSeed> All { get; } =
    [
        new(
            "frontend-developer-ktm-tech-park",
            "Frontend developer (React)",
            "Kathmandu Tech Park",
            "We are looking for a frontend developer to work on the customer-facing dashboards our "
            + "clients use every day. You will be writing React and TypeScript against a REST API, "
            + "turning designs into components that hold up on a slow connection, and sharing "
            + "ownership of the design system with the rest of the team. We care more about how you "
            + "reason about a problem than about which frameworks you have used, because the "
            + "frameworks change and the reasoning does not. A day looks like a stand-up, a pair of "
            + "hours on a feature with a designer, code review, and a test suite that runs on every "
            + "push. We mentor juniors: if you have a portfolio and you can explain your decisions, "
            + "we would like to hear from you.",
            JobCategory.InformationTechnology,
            EmploymentType.FullTime,
            WorkMode.Hybrid,
            DistrictCode: "bagmati-kathmandu",
            ApplicationUrl: "https://www.merokaipatra.com/",
            ExpiresInDays: 30),
        new(
            "junior-python-developer-remote",
            "Junior Python developer (remote)",
            "Himal Data Labs",
            "A remote role building data pipelines for agricultural research groups across the "
            + "Terai. You will work on Python services that ingest, clean and publish climate and "
            + "yield data, and you will be the person who makes sure a dataset that arrives with a "
            + "broken column does not silently become a wrong number in a research paper. There is "
            + "no office to attend and no fixed hours: the team is distributed across Nepal and "
            + "India, we overlap for a few hours in the afternoon Nepal time, and everything else "
            + "is asynchronous. We expect someone who can write clearly, ask questions early, and "
            + "say plainly when something will not work. Experience with a specific framework is "
            + "less important than being comfortable with data you cannot see.",
            JobCategory.InformationTechnology,
            EmploymentType.FullTime,
            WorkMode.Remote,
            ApplicationUrl: "https://www.merokaipatra.com/",
            ExpiresInDays: 21),
        new(
            "secondary-school-teacher-bhojpur",
            "Secondary school teacher (Mathematics)",
            "Bhojpur Janatidevi Secondary School",
            "A Grade 9-12 mathematics teaching position at a community secondary school in Bhojpur. "
            + "You will teach four classes a day and take one period of extra support for students "
            + "preparing for the Secondary School Examination. The school has a mixed class of "
            + "roughly forty students and no lab, so the teaching is board work, chalk and "
            + "homework; what makes it effective is patience with students who have fallen behind "
            + "rather than finishing the syllabus. A bachelor's degree in mathematics or a plus two "
            + "with a mathematics background is required, and we would like someone who can also "
            + "teach in Nepali, since a class that mixes both needs it. Salary is negotiable with "
            + "the management committee and follows the school's scale.",
            JobCategory.Teaching,
            EmploymentType.FullTime,
            WorkMode.OnSite,
            DistrictCode: "koshi-bhojpur",
            ExpiresInDays: 45),
        new(
            "english-language-tutor-lalitpur",
            "English language tutor",
            "Bright Path Learning Centre",
            "Part-time English tutoring for students from Grade 6 to Grade 12 preparing for the "
            + "Secondary School Examination and for the IELTS. Classes are small, usually six to "
            + "eight students, and the centre runs on weekday evenings and Saturday mornings, so "
            + "this suits someone studying or working full time. We are looking for a tutor who can "
            + "diagnose why a student is losing marks rather than drilling grammar rules at them: "
            + "most of our students can write a sentence and still lose marks for structure, "
            + "vocabulary range and punctuation. Lesson plans and study materials are provided, but "
            + "you are expected to bring your own exercises and your own feedback.",
            JobCategory.Teaching,
            EmploymentType.PartTime,
            WorkMode.OnSite,
            DistrictCode: "bagmati-lalitpur",
            ExpiresInDays: 14),
        new(
            "community-health-officer-saptari",
            "Community health officer",
            "Madhesh Rural Health Network",
            "A field role with a non-governmental health network running maternal and child health "
            + "outreach in Saptari district. You will run health sessions in village health posts, "
            + "keep the register of the mothers and children the programme follows, and report "
            + "monthly figures to the district office. Most of the work is in Nepali and the "
            + "reporting is in English, so both languages get used daily. The successful candidate "
            + "has completed a Bachelor of Science in Nursing or a Public Health degree, is "
            + "registered with the relevant council, and can ride a motorcycle over unsurfaced "
            + "roads, since two of the outreach sites are not reachable by car in the monsoon. This "
            + "is a contract appointment of one year, renewable.",
            JobCategory.Healthcare,
            EmploymentType.Contract,
            WorkMode.OnSite,
            DistrictCode: "madhesh-saptari",
            ExpiresInDays: 35),
        new(
            "staff-nurse-icu-kathmandu",
            "Staff nurse, intensive care",
            "Patan Academy of Health Sciences",
            "A full-time staff nurse post in the intensive care unit of a teaching hospital. The unit "
            + "has eight beds and a two-nurse-per-patient ratio for ventilated patients, so the post "
            + "is demanding and the rota runs twelve-hour shifts including nights. You will be "
            + "assigned to patients under the consultant team, take part in the handover round, and "
            + "help teach the nursing students who rotate through the unit. A BSc in Nursing, a valid "
            + "Nepal Nursing Council licence and at least one year of critical care experience are "
            + "required. We are looking for someone who is steady in a crisis rather than someone "
            + "who enjoys one.",
            JobCategory.Healthcare,
            EmploymentType.FullTime,
            WorkMode.OnSite,
            DistrictCode: "bagmati-kathmandu",
            ExpiresInDays: 25),
        new(
            "accounting-assistant-bhaktapur",
            "Accounting assistant",
            "Bhaktapur Traders and Agencies",
            "An accounts role in a trading company: preparing bills, entering supplier invoices into "
            + "the accounting package, matching payments against ledgers and helping with the month "
            + "end bank reconciliation. The work is repetitive enough that accuracy matters more "
            + "than speed, and the previous post holder's main fault was leaving reconciliations to "
            + "the last day. We need someone who can read a bank statement properly and who is "
            + "comfortable asking the sales team about a missing invoice instead of guessing. "
            + "Candidates who have cleared either accounting technician level of the National "
            + "Accounting Examination Board will be preferred. Office hours are fixed and the "
            + "post is on site in the old market area.",
            JobCategory.Finance,
            EmploymentType.FullTime,
            WorkMode.OnSite,
            DistrictCode: "bagmati-bhaktapur",
            ExpiresInDays: 40),
        new(
            "accounts-internship-lalitpur",
            "Accounts internship (six months)",
            "Shanker Dev and Associates",
            "A six-month paid internship in the audit and accounts department of a chartered "
            + "accountancy firm. You will prepare working papers, help with client billings and file "
            + "the working papers of junior staff for review. This is an internship, not a disguised "
            + "junior role: there is a named supervisor, a written list of what you should have "
            + "learnt by the end, and monthly feedback. Final-year undergraduates in management or "
            + "accounting can apply. We ask for one afternoon a week in the office and the rest "
            + "flexible, because most of our interns are still finishing their degree, and we expect "
            + "two days a week in practice.",
            JobCategory.Finance,
            EmploymentType.Internship,
            WorkMode.Hybrid,
            DistrictCode: "bagmati-lalitpur",
            ExpiresInDays: 18),
        new(
            "field-sales-representative-morang",
            "Field sales representative",
            "Eastern Teras Agro Industries",
            "A field sales role covering Bhojpur, Dhankuta and Morang for an agricultural inputs "
            + "distributor. You will call on the agro-vets in your territory, restock and brief them "
            + "on the season's products, collect outstanding payments and report stock movements. "
            + "Most of the day is in the field on a motorcycle; the office is in Itahari. The "
            + "tension in this job is that you are the only person your retailers have, so you will "
            + "be asked to fix problems outside the sales brief and you will have to decide what is "
            + "yours and what goes to your manager. A plus two qualification is required and a valid "
            + "driving licence with a motorcycle is essential. Base pay plus a commission on "
            + "collections, not on volume, because selling to a retailer who cannot pay is not "
            + "selling.",
            JobCategory.Sales,
            EmploymentType.FullTime,
            WorkMode.OnSite,
            DistrictCode: "koshi-morang",
            ExpiresInDays: 28),
        new(
            "civil-site-engineer-chitwan",
            "Civil site engineer",
            "Terai Infrastructure and Construction",
            "A site engineer appointment on a building project in Bharatpur, from foundation to "
            + "finishing. You will supervise the day-to-day work of the site supervisor and the "
            + "masons, check setting-out against the drawings, keep the measurement book and "
            + "attest contractor work as it is measured, and report progress and quality issues to "
            + "the project manager weekly. The site runs to Nepali standard practice and the "
            + "drawings are in AutoCAD, so you will spend time on a drawing set rather than a "
            + "tablet. A diploma or degree in civil engineering and two years of site experience "
            + "after it are required; a valid Nepal Engineering Council registration is an "
            + "advantage. This is a site post, so expect to be on site from seven.",
            JobCategory.Engineering,
            EmploymentType.FullTime,
            WorkMode.OnSite,
            DistrictCode: "bagmati-chitwan",
            ExpiresInDays: 33),
        new(
            "hydraulic-technician-kaski-contract",
            "Hydraulic technician (field service)",
            "Pokhara Irrigation Systems",
            "Service and repair of irrigation pumping stations and their pipework across the district "
            + "for a contractor working on canal maintenance. Most of the work is responding to "
            + "breakdowns: an unstaffed pump station stops delivering water to a canal reach and "
            + "the farmers downstream notice within hours, so you will be called out at short notice "
            + "and expected to diagnose whether the fault is mechanical, electrical or a burst pipe. "
            + "A diploma in mechanical or electrical engineering, a motorcycle licence and comfort "
            + "working alone at a station on a canal bank are what the job needs. This is a "
            + "contract engagement of eleven months.",
            JobCategory.Construction,
            EmploymentType.Contract,
            WorkMode.OnSite,
            DistrictCode: "gandaki-kaski",
            ExpiresInDays: 20),
        new(
            "front-desk-receptionist-lalitpur-closed",
            "Front desk receptionist",
            "Kulekhani Guest House",
            "A front desk position at a thirty-room guest house in Lalitpur, handling arrivals and "
            + "departures, reservations by phone and online, guest billing and basic inquiries from "
            + "tourists. The previous advertisement has closed and this row is retained so staff can "
            + "see how a closed posting behaves. The applicant should be presentable on the phone, "
            + "comfortable with a booking system and a spreadsheet, and able to handle a full house "
            + "at a festival weekend without losing track of who has paid. Basic English is needed to "
            + "speak with guests; Nepali is the working language of the office. This is a full-time "
            + "position with a shared night shift on rotation.",
            JobCategory.Hospitality,
            EmploymentType.FullTime,
            WorkMode.OnSite,
            DistrictCode: "bagmati-lalitpur",
            ExpiredDaysAgo: 60),
        new(
            "community-social-worker-rupandehi",
            "Community social worker",
            "Lumbini Child Development Programme",
            "A field social work post with a child protection programme based in Rupandehi district, covering "
            + "the towns and wards along the highway corridor. You will run the community meetings the programme's "
            + "case referral process depends on, keep the case notes up to date, and coordinate with "
            + "the ward office and the local police when a case needs to be escalated. The work is "
            + "people-facing more than desk-facing, and the difficult part is often the first visit "
            + "to a family that does not expect a stranger asking questions. A Bachelor in Social "
            + "Work (BBS or equivalent) is required, and Nepali is the working language of every "
            + "document and every meeting. This is a full-time post with a monthly field allowance.",
            JobCategory.NGO,
            EmploymentType.FullTime,
            WorkMode.OnSite,
            DistrictCode: "lumbini-rupandehi",
            ExpiresInDays: 26),
        new(
            "mobile-clinic-attendant-surkhet",
            "Mobile clinic attendant",
            "Karnali Rural Health Outreach",
            "An attendant post with a mobile health clinic that runs on a fixed weekly circuit "
            + "through the villages around the Surkhet valley and the hills above it. You will "
            + "prepare the clinic vehicle, register everyone who attends, keep the stock of medicines "
            + "and consumables within what the vehicle can safely carry, and record the day's figures "
            + "for the district office. The circuit reaches places with no road for part of the year, "
            + "so a motorcycle licence and a tolerance for long days in difficult terrain are the "
            + "real requirements here; the certificate is what the paperwork asks for. A completed CHW "
            + "or ANM course is preferred. Two of the circuit's four scheduled visits are overnight, "
            + "and the post is for a local candidate who can stay out on the second and third day.",
            JobCategory.Healthcare,
            EmploymentType.FullTime,
            WorkMode.OnSite,
            DistrictCode: "karnali-surkhet",
            ExpiresInDays: 24),
        new(
            "school-teacher-dadeldhura",
            "Primary school teacher",
            "Dadeldhura Municipality Primary School",
            "A teaching post at a municipality primary school in Dadeldhura, covering Grades 1 to 3. "
            + "You will take one or two grades per class rather than a single age group, because the "
            + "school is small enough that a teacher moves between rooms, and you will be the person "
            + "who keeps the attendance register, the parent meetings and the stock of teaching "
            + "materials. Nepali is required and the classroom language is Nepali; applicants from "
            + "other language groups who can teach in Nepali are welcome. A certificate in "
            + "education or a grade 11-12 pass with a teaching training is required, and the "
            + "municipal education office runs the appointment. This is a full-time post at a school "
            + "in a hill town, so there is no relocation allowance.",
            JobCategory.Teaching,
            EmploymentType.FullTime,
            WorkMode.OnSite,
            DistrictCode: "sudurpashchim-dadeldhura",
            ExpiresInDays: 38),
        new(
            "tractor-operator-dhankuta-closed",
            "Tractor operator, agricultural support",
            "Koshi Agri Support Programme",
            "A tractor operator post supporting smallholder farmers in the eastern hills, driving "
            + "between villages on field days and keeping the machine serviced. This advertisement has "
            + "closed; the row is kept so the staff board shows a posting past its deadline rather "
            + "than an empty space where one used to be. The post was based in Dhankuta and required "
            + "a valid driving licence for a tractor, a practical ability to service the machine at a "
            + "roadside stop, and the physical fitness for field work on uneven ground. The role sits "
            + "under an agricultural support programme that has since ended its recruitment round.",
            JobCategory.Agriculture,
            EmploymentType.Contract,
            WorkMode.OnSite,
            DistrictCode: "koshi-dhankuta",
            ExpiredDaysAgo: 25),
    ];
}
