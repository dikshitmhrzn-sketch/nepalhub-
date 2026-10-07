using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data.Seeding;

internal sealed record StudentResourceSeed(
    string Code,
    string Slug,
    string Title,
    string Summary,
    string Body,
    ResourceType Type,
    ResourceCategory Category,
    EducationLevel Level,
    string? ExternalUrl = null,
    int? AcademicYear = null,
    string? Provider = null,
    decimal? AwardAmountNpr = null,
    string? Eligibility = null,
    int? ExpiresInDays = null,
    int? ExpiredDaysAgo = null,
    bool IsPublished = true);

/// <summary>
/// The starter catalogue for the student resource module: ten notes, seven past question
/// papers and seven scholarships.
///
/// This is starter content, not the long-term source, exactly as the quiz pool is. It exists
/// so the catalogue is not an empty page on a fresh install and so a staff member can see what
/// a good entry looks like; the real catalogue is written in the staff editor.
///
/// Two decisions in here are worth stating, because both would otherwise look like bugs:
///
/// <para>
/// <b>Deadlines are relative, not absolute.</b> A scholarship stores its deadline as an offset
/// from the day it is seeded, not as a fixed date. A hard-coded date would have been correct on
/// the day it was written and wrong forever after: the "open" scholarships would have quietly
/// expired, and the whole type would have emptied itself out. A relative offset means the
/// seeded catalogue always has something live in it. The cost is honest and worth stating — a
/// row is inserted once and is never re-dated, so a seeded scholarship does eventually expire
/// for real, and from then on the catalogue belongs to staff.
/// </para>
///
/// <para>
/// <b>External links are section pages, not deep links.</b> A question paper has to link
/// somewhere, and a made-up PDF path would 404 in front of a student. Each link here is a real
/// institution's stable landing page, so the row works when it is seeded and gives staff an
/// obvious starting point to replace with the actual paper. Award amounts are likewise
/// indicative examples rather than quoted figures — staff own the real numbers.
/// </para>
///
/// The two expired scholarships are inserted as published rows with a past deadline, which is
/// the real situation the expiry logic exists to handle: something that was legitimately live
/// and has since closed. The service would refuse to publish a past deadline, so these rows can
/// only be written by the seeder, and that is deliberate.
/// </para>
/// </summary>
internal static class StudentResourceSeedData
{
    internal static IReadOnlyList<StudentResourceSeed> All { get; } =
    [
        // ------------------------------------------------------------------ notes
        new(
            "note-001",
            "quadratic-equations-grade-9",
            "Solving quadratic equations: the four methods",
            "A worked comparison of factoring, completing the square, the quadratic formula and "
            + "the graph method, and when to reach for each one.",
            "A quadratic equation has the form ax^2 + bx + c = 0, where a, b and c are known and "
            + "a is not zero. There are four standard methods. Factoring is the fastest when the "
            + "equation splits into neat factors, and it is worth trying first because it is the "
            + "quickest to check. Completing the square turns any quadratic into a perfect square "
            + "plus a remainder, and it is also the method that leads naturally to the vertex form. "
            + "The quadratic formula, x = (-b +/- sqrt(b^2 - 4ac)) / 2a, always works, so it is the "
            + "reliable fallback when factoring does not fall out. The graph method is not for "
            + "finding exact answers; it is for reading off the shape and the approximate roots. "
            + "The discriminant b^2 - 4ac tells you what to expect before you start: if it is "
            + "positive there are two real roots, if it is zero there is one repeated root, and if "
            + "it is negative there are no real roots at all.",
            ResourceType.Note,
            ResourceCategory.Mathematics,
            EducationLevel.Grade9to10),
        new(
            "note-002",
            "trigonometric-ratios-grade-11",
            "Trigonometric ratios and the unit circle",
            "How sine, cosine and tangent are defined from a right-angled triangle, and how the "
            + "unit circle extends the same six ratios to every angle.",
            "On a right-angled triangle the ratio of each side to the hypotenuse has a fixed name: "
            + "sine is opposite over hypotenuse, cosine is adjacent over hypotenuse, and tangent is "
            + "opposite over adjacent. Those definitions are only valid for acute angles, because a "
            + "right-angled triangle needs both other angles to be acute. The unit circle removes "
            + "that restriction. Take a circle of radius one centred on the origin, and a point on "
            + "it at angle t measured from the positive x-axis. The coordinates of that point are "
            + "(cos t, sin t), which is where the two ratios come from. The same point also fixes "
            + "the tangent as the ratio of the y and x coordinates, so tan t = sin t / cos t wherever "
            + "the cosine is not zero. The quadrants are worth memorising for signs: sine and "
            + "tangent are positive in the first and second quadrants, cosine and tangent are "
            + "positive in the first and fourth. The exact values worth knowing without a calculator "
            + "are 0, 30, 45, 60 and 90 degrees, which comes from equilateral triangles and "
            + "isosceles right triangles.",
            ResourceType.Note,
            ResourceCategory.Mathematics,
            EducationLevel.Grade11to12),
        new(
            "note-003",
            "laws-of-motion-grade-11",
            "Newton's three laws of motion",
            "Inertia, action and reaction, with the everyday consequences that tend to be asked "
            + "about in exams.",
            "The first law says an object keeps doing what it was already doing unless a net force "
            + "acts on it. A passenger lurches forward when a bus brakes because the bus changes "
            + "and the passenger does not. The second law says force equals mass times acceleration, "
            + "F = ma, so the same force moves a heavy object less. The third law says every action "
            + "has an equal and opposite reaction, and the phrase that confuses people is that the "
            + "two forces act on different bodies. When you push a wall, the wall pushes back on "
            + "you, but the force pushing the wall acts on the wall while the reaction acts on you, "
            + "so they never cancel out on one object. That is also why you cannot push a boat by "
            + "standing in it and pushing the hull; the reaction just pushes the boat back. The "
            + "common exam questions ask you to identify which law explains a specific situation, so "
            + "it is worth learning a clear example for each one.",
            ResourceType.Note,
            ResourceCategory.Science,
            EducationLevel.Grade11to12),
        new(
            "note-004",
            "chemical-bonding-grade-11",
            "Ionic, covalent and metallic bonding",
            "How atoms combine into compounds, and the properties each kind of bond produces.",
            "Ionic bonding is the transfer of electrons from a metal to a non-metal, producing "
            + "positively charged cations and negatively charged anions that attract each other. "
            + "Sodium chloride is the standard example. The resulting solid lattice is very hard, "
            + "has a high melting point, and conducts electricity when molten or dissolved, because "
            + "the ions are free to move. In water the lattice comes apart, which is why these "
            + "compounds are soluble in water. Covalent bonding is a shared pair of electrons, and "
            + "it happens between non-metals. Water is the standard example. A covalent compound "
            + "generally has a lower melting point than an ionic one, and it does not conduct "
            + "electricity because no free ions are produced; it is the mobile electrons in metals "
            + "that give metals their conductivity. Metallic bonding is a lattice of positive ions "
            + "surrounded by delocalised electrons, which explains why metals conduct and why they "
            + "can be bent and hammered into shape without shattering.",
            ResourceType.Note,
            ResourceCategory.Science,
            EducationLevel.Grade11to12),
        new(
            "note-005",
            "cell-structure-grade-9",
            "Plant and animal cells: what is actually different",
            "The parts of a cell and the two differences that are worth memorising, as opposed to "
            + "the ones that are really the same.",
            "Both plant and animal cells contain a cell membrane, cytoplasm, a nucleus carrying "
            + "genetic material, mitochondria and ribosomes, and those are the parts that answer "
            + "most questions. The differences are fewer than diagrams suggest. A plant cell has a "
            + "cell wall outside the membrane, made of cellulose, which gives it a fixed rectangular "
            + "shape; an animal cell has no wall and is rounder and more flexible. A plant cell "
            + "also has a large permanent vacuole filled with cell sap, which pushes the cytoplasm "
            + "to the edges; an animal cell has only small temporary vacuoles. One more difference "
            + "worth knowing is that chloroplasts occur in plant cells and carry out photosynthesis, "
            + "while both kinds have mitochondria for respiration. Remember that chloroplasts are "
            + "only in the green parts of a plant, so a root cell is a plant cell without them. "
            + "Questions usually ask you to name the organelle from its described function, so link "
            + "each one to its job: nucleus for control, mitochondria for energy release, "
            + "chloroplasts for photosynthesis, ribosomes for protein production.",
            ResourceType.Note,
            ResourceCategory.Science,
            EducationLevel.Grade9to10),
        new(
            "note-006",
            "parts-of-speech-grade-6",
            "The eight parts of speech",
            "What each part of speech does, and the test that identifies each one in a sentence.",
            "A noun names a person, place, thing or idea. A pronoun stands in for a noun, such as "
            + "he, she, it, they and this. A verb says what happens or what a subject is, and it is "
            + "the part that carries tense. An adjective describes a noun, so red book has two "
            + "adjectives in it. An adverb describes a verb, an adjective or another adverb, and it "
            + "usually ends in -ly, though not always, because fast can be both an adjective and an "
            + "adverb. A preposition shows the relationship of a noun to another word, as in on, in, "
            + "under and between. A conjunction joins words, phrases or clauses, and it is either "
            + "coordinating, joining two equal items with and, or, but, so, yet, or subordinating, "
            + "introducing a dependent clause with because, although or while. An interjection "
            + "expresses sudden feeling, like oh or ah, and it stands apart from the rest of the "
            + "sentence. A useful exam trick is that a word can belong to more than part of speech "
            + "depending on its job in that particular sentence, which is why you have to look at "
            + "what the word is doing rather than what it is.",
            ResourceType.Note,
            ResourceCategory.English,
            EducationLevel.Grade6to8),
        new(
            "note-007",
            "nepali-present-tense-verbs",
            "Nepali present tense verbs: लिङ्, ले, लाई and बा",
            "The four present tenses and how the ending of a verb changes with the subject.",
            "Nepali present tense has four forms, distinguished by both the verb ending and the "
            + "subject particle. The familiar is लिङ्, as in म पढ्छु, I read, and it is used with the "
            + "first person. The ordinary is ले, as in म पढ्छु, तिमीले पढ्छौ, and it covers both the "
            + "first and second person. The polite is लाई, as in उहाँले पढ्नुहुन्छ, and it is what you "
            + "use for someone you owe respect to. The remote is बा, as in ऊ पढ्छ, meaning that "
            + "someone who is not present reads, or that a habitual reading happens elsewhere. The "
            + "verb root usually stays the same and the ending carries the meaning, so पढ्- is the "
            + "root throughout and छ- is what changes. Verbs ending in a nasal sound add a "
            + "nasalisation mark, so गन् becomes गन्छु rather than गछु, and लेस् becomes लेस्छु. The "
            + "irregular forms are worth learning separately, because सुन् gives सुन्छु and हुन् gives "
            + "हुन्छ, and neither follows the regular pattern.",
            ResourceType.Note,
            ResourceCategory.Nepali,
            EducationLevel.Grade6to8),
        new(
            "note-008",
            "constitution-of-nepal-2015",
            "The Constitution of Nepal 2015: how it is arranged",
            "The preamble, the five parts of the document, and the parts of the system that are "
            + "most often asked about.",
            "The Constitution was promulgated on 20 September 2015 and replaced the 2007 "
            + "constitution and the monarchy it had ended. It opens with a preamble stating the "
            + "sovereignty of the people, and it establishes Nepal as an independent, indivisible, "
            + "secular, inclusive and fully democratic federal democratic republic. The document is "
            + "divided into five parts. Part One covers the fundamental rights and duties of "
            + "citizens, which is the longest section and the one most questions are drawn from. "
            + "Part Two covers the legislative branch, the House of Representatives and the "
            + "National Assembly. Part Three covers the executive, meaning the President, the Prime "
            + "Minister, the Council of Ministers and the civil service. Part Four deals with the "
            + "judiciary and the appointments and independence of the judges. Part Five deals with "
            + "federalism, the local government structure and the constitutional amendment "
            + "process. Nepal's system is a parliamentary republic, so the executive comes from the "
            + "legislature and the President is head of state rather than head of government. The "
            + "federal structure divided the country into seven provinces, and questions often test "
            + "which province a given district or city belongs to.",
            ResourceType.Note,
            ResourceCategory.SocialStudies,
            EducationLevel.Grade11to12),
        new(
            "note-009",
            "data-structures-introduction",
            "An introduction to data structures",
            "Why the choice of structure matters more than the code that fills it, and how the "
            + "common ones compare.",
            "A data structure is a way of arranging data so that the operations you need most are "
            + "cheap. Which one to pick depends entirely on the pattern of access, so it is worth "
            + "asking what the data will be asked to do before choosing. An array stores items in "
            + "order and gives constant time access by index, but inserting or removing in the "
            + "middle means shifting everything after it, so it is the wrong choice when the order "
            + "changes often. A linked list fixes insertion and removal by relinking neighbours, but "
            + "gives up fast access by position, so searching one is linear. A stack is last in, "
            + "first out, which is exactly what you want for undo history and for evaluating "
            + "expressions with brackets, and a queue is first in, first out, which is what a "
            + "printer spool or a breadth-first search needs. A hash table gives near constant time "
            + "lookup by key at the cost of no ordering, so it cannot answer questions like what is "
            + "the smallest item. A binary search tree keeps items ordered and gives logarithmic "
            + "search, insertion and removal provided it stays balanced, and a self-balancing "
            + "variant such as an AVL tree is what keeps that guarantee when the input arrives in "
            + "sorted order.",
            ResourceType.Note,
            ResourceCategory.ComputerScience,
            EducationLevel.PlusTwo),
        new(
            "note-010",
            "nepali-numbering-and-dates",
            "Nepali numbering, dates and how to read them",
            "The lakh and crore system, the Bikram Sambat calendar, and the two ways a Nepali date "
            + "is written.",
            "Nepal uses the lakh and crore system, so a lakh is 100,000 and a crore is 10,000,000. "
            + "The words are short forms of the old hundred-thousand and ten-million, and English "
            + "writing mixes the two systems constantly, so it is worth being careful: 1,50,000 is "
            + "one and a half lakh, which is 150,000. Nepal also uses its own script of numerals "
            + "alongside the Arabic ones, and the two are read the same way. The calendar is "
            + "Bikram Sambat, abbreviated BS, which currently runs about 56 years ahead of the "
            + "Gregorian year: BS 2081 corresponds to 2024 and 2025. A date is written day, month, "
            + "year, so 2081/05/12 is the twelfth day of Baisakh 2081. Month names are not "
            + "abbreviated in the way English ones are: Baishakh, Jestha, Ashadh, Shrawan, Bhadra, "
            + "Ashwin, Kartik, Mangsir, Poush, Magh, Falgun and Chaitra, with the lunar months "
            + "Krishna and Kartik in Poush and Magh and Mangsir. Government offices, schools and "
            + "newspapers use BS, so a deadline printed in a notice is almost always in BS and will "
            + "need converting.",
            ResourceType.Note,
            ResourceCategory.General,
            EducationLevel.General),

        // -------------------------------------------------------- question papers
        new(
            "paper-001",
            "see-2081-mathematics-question-paper",
            "SEE 2081 mathematics question paper",
            "The Grade 9-10 Secondary School Examination mathematics paper for 2081, with a link "
            + "to where the paper can be read.",
            "This entry records the Grade 9-10 Secondary School Examination mathematics paper for "
            + "the year 2081 BS. NepalHub does not host the paper itself: the link below goes to the "
            + "examination body's published materials, and staff replace that link with the exact "
            + "paper PDF as soon as it is posted. A candidate sitting this paper should expect short "
            + "and long questions across the whole Grade 9-10 syllabus, including algebra, "
            + "geometry, trigonometry, measurement, statistics and the handling of data. Practice "
            + "with a paper under timed conditions is more useful than working through topic notes, "
            + "because the exam rewards pacing as much as knowledge. Working through the previous "
            + "two or three years is worth more than a single paper done repeatedly, since the "
            + "question types repeat while the specific numbers change.",
            ResourceType.QuestionPaper,
            ResourceCategory.Mathematics,
            EducationLevel.Grade9to10,
            ExternalUrl: "https://neb.gov.np/",
            AcademicYear: 2081),
        new(
            "paper-002",
            "see-2081-english-question-paper",
            "SEE 2081 English question paper",
            "The Grade 9-10 Secondary School Examination English paper for 2081, covering grammar, "
            + "comprehension and writing.",
            "This entry records the Grade 9-10 Secondary School Examination English paper for the "
            + "year 2081 BS. The link goes to the examination body's published materials and staff "
            + "replace it with the exact paper once it is available. The paper typically carries "
            + "three sections: objective grammar and vocabulary questions, a comprehension passage "
            + "with questions, and a writing task. The writing section is the one candidates lose "
            + "the most marks on, and the usual reasons are not knowing the required format, running "
            + "out of time, and leaving spelling and punctuation uncorrected. Practise the format "
            + "first, because a letter and a report have fixed headings and a question that does "
            + "not answer the set task loses marks regardless of how good the English is.",
            ResourceType.QuestionPaper,
            ResourceCategory.English,
            EducationLevel.Grade9to10,
            ExternalUrl: "https://neb.gov.np/",
            AcademicYear: 2081),
        new(
            "paper-003",
            "see-2081-science-question-paper",
            "SEE 2081 science question paper",
            "The Grade 9-10 Secondary School Examination science paper for 2081, mixing physics, "
            + "chemistry and biology.",
            "This entry records the Grade 9-10 Secondary School Examination science paper for the "
            + "year 2081 BS. The link points at the examination body's published materials and staff "
            + "replace it with the exact paper when it is posted. Unlike the separate Grade 11 "
            + "science papers, this paper is a single combined paper, so a candidate has to cover "
            + "physics, chemistry and biology within one sitting and cannot bank time from a strong "
            + "subject to rescue a weak one. The most common loss of marks comes from numerical "
            + "physics questions where the working is skipped, so write out every step even when "
            + "the answer looks obvious. Practising calculations for units is worthwhile: an answer "
            + "that is correct but carries the wrong unit loses the mark.",
            ResourceType.QuestionPaper,
            ResourceCategory.Science,
            EducationLevel.Grade9to10,
            ExternalUrl: "https://neb.gov.np/",
            AcademicYear: 2081),
        new(
            "paper-004",
            "see-2081-social-studies-question-paper",
            "SEE 2081 social studies question paper",
            "The Grade 9-10 Secondary School Examination social studies paper for 2081.",
            "This entry records the Grade 9-10 Secondary School Examination social studies paper for "
            + "the year 2081 BS. The link goes to the examination body's published materials and "
            + "staff update it to the exact paper once published. The subject covers history, "
            + "geography, civics and health, and the paper mixes short factual questions with longer "
            + "descriptive ones that ask you to explain rather than recall. The descriptive questions "
            + "reward structure: state the point, give the evidence or reason, and then link the two "
            + "with a connective. A common mistake is to write several disconnected points where "
            + "the question asked for an explanation of how one thing led to another, and a plan "
            + "written in the margin before starting the answer is the cheapest way to avoid it.",
            ResourceType.QuestionPaper,
            ResourceCategory.SocialStudies,
            EducationLevel.Grade9to10,
            ExternalUrl: "https://neb.gov.np/",
            AcademicYear: 2081),
        new(
            "paper-005",
            "grade-11-12-annual-2081-mathematics",
            "Grade 11-12 annual 2081 mathematics paper",
            "The Grade 11-12 annual examination mathematics paper for 2081, covering pure and "
            + "applied mathematics.",
            "This entry records the Grade 11-12 annual examination mathematics paper for the year "
            + "2081 BS. The link points to the examination body's published materials and staff "
            + "replace it with the exact paper when it is available. The Grade 11-12 mathematics "
            + "paper is separated by subject group, so a candidate sits either the pure mathematics "
            + "paper, which carries calculus, or an applied group paper with more modelling, and the "
            + "link should match the group being taken. The calculus sections reward knowing the "
            + "standard derivatives and integrals rather than deriving them, so time is better spent "
            + "memorising the small number of formulas that come up repeatedly and practising how to "
            + "apply them to unfamiliar shapes.",
            ResourceType.QuestionPaper,
            ResourceCategory.Mathematics,
            EducationLevel.Grade11to12,
            ExternalUrl: "https://neb.gov.np/",
            AcademicYear: 2081),
        new(
            "paper-006",
            "grade-11-12-annual-2081-biology",
            "Grade 11-12 annual 2081 biology paper",
            "The Grade 11-12 annual examination biology paper for 2081, with emphasis on genetics "
            + "and physiology.",
            "This entry records the Grade 11-12 annual examination biology paper for the year 2081 "
            + "BS. The link goes to the examination body's published materials and staff update it to "
            + "the exact paper once published. This paper assumes the Grade 9-10 foundations, so "
            + "cell biology, diversity of life and the human body come first before genetics and "
            + "ecology. The sections that cause most difficulty are the ones requiring a diagram to "
            + "be drawn and labelled correctly, particularly the stages of mitosis and meiosis, so "
            + "practise drawing them from memory rather than copying them. Genetics questions often "
            + "ask for a punnett square to be shown as working, so the reasoning is marked and not "
            + "only the final ratio.",
            ResourceType.QuestionPaper,
            ResourceCategory.Science,
            EducationLevel.Grade11to12,
            ExternalUrl: "https://neb.gov.np/",
            AcademicYear: 2081),
        new(
            "paper-007",
            "grade-11-12-annual-2081-english",
            "Grade 11-12 annual 2081 English paper",
            "The Grade 11-12 annual examination English paper for 2081, with skills-based writing.",
            "This entry records the Grade 11-12 annual examination English paper for the year 2081 "
            + "BS. The link points to the examination body's published materials and staff replace it "
            + "with the exact paper when it is posted. The paper is more demanding than the "
            + "Secondary School Examination paper, with a longer reading passage, heavier use of "
            + "inferential questions that require inference from evidence rather than a stated fact, "
            + "and a longer writing task worth more marks. Candidates lose time deciding what to "
            + "write; spending three minutes on a plan that lists the required content in order is "
            + "usually worth it. Vocabulary questions are frequently about word formation, so a "
            + "prefix or suffix changes the answer, and reading the whole word including its ending "
            + "is a good habit to build.",
            ResourceType.QuestionPaper,
            ResourceCategory.English,
            EducationLevel.Grade11to12,
            ExternalUrl: "https://neb.gov.np/",
            AcademicYear: 2081),

        // ------------------------------------------------------------ scholarships
        new(
            "scholarship-001",
            "plus-two-exam-merit-scholarship-closed",
            "Plus Two exam merit scholarship (closed)",
            "A merit award for students sitting the Grade 12 board examinations. This listing has "
            + "closed, and is kept as an example of an expired entry.",
            "This scholarship was a merit award for students sitting the Grade 12 board "
            + "examinations, awarded on the basis of the previous year's results. The application "
            + "window has now closed, so this entry no longer appears in the public catalogue and "
            + "staff can see it as a closed listing. The amount shown is indicative and staff "
            + "replace it with the official figure. It is retained deliberately rather than deleted, "
            + "because closed scholarships are a normal part of any real catalogue and it is useful "
            + "to see how an expired entry behaves once its deadline passes.",
            ResourceType.Scholarship,
            ResourceCategory.General,
            EducationLevel.PlusTwo,
            Provider: "Ministry of Education",
            AwardAmountNpr: 30000m,
            Eligibility: "Students registered for the Grade 12 board examinations, awarded on merit.",
            ExpiredDaysAgo: 150),
        new(
            "scholarship-002",
            "undergraduate-tuition-support-closed",
            "Undergraduate tuition support (closed)",
            "Tuition support for undergraduates at a public institution. This listing has closed.",
            "This scholarship provided tuition support for undergraduates at a public higher "
            + "education institution, and its application window has closed. The entry is kept so "
            + "staff can see a closed scholarship in the catalogue rather than a gap where one used "
            + "to be, which is also what a visitor following an older shared link will hit. The "
            + "amount shown is indicative; staff replace it with the official figure before "
            + "publishing a live listing of this kind.",
            ResourceType.Scholarship,
            ResourceCategory.Management,
            EducationLevel.Bachelors,
            Provider: "Tribhuvan University",
            AwardAmountNpr: 50000m,
            Eligibility: "Students admitted to a public undergraduate programme.",
            ExpiredDaysAgo: 90),
        new(
            "scholarship-003",
            "grade-11-12-merit-scholarship",
            "Grade 11-12 merit scholarship",
            "A merit scholarship for students in Grades 11 and 12, awarded on the previous year's "
            + "examination results.",
            "This scholarship supports students in Grades 11 and 12 and is awarded on merit, which "
            + "usually means the results of the preceding year. Application is through the school "
            + "rather than individually, so a student applies by submitting the form to their school "
            + "office and the school confirms the list. The award amount shown is indicative and "
            + "staff should replace it with the official figure, together with the exact deadline "
            + "and the correct application link, before treating this as an authoritative listing. "
            + "Because the deadline is what hides a scholarship from the catalogue, leaving it blank "
            + "or in the past will make the entry disappear from public view.",
            ResourceType.Scholarship,
            ResourceCategory.General,
            EducationLevel.Grade11to12,
            ExternalUrl: "https://www.moest.gov.np/",
            Provider: "Ministry of Education",
            AwardAmountNpr: 40000m,
            Eligibility: "Students enrolled in Grades 11 or 12, selected on previous year results.",
            ExpiresInDays: 60),
        new(
            "scholarship-004",
            "engineering-undergraduate-scholarship",
            "Engineering undergraduate scholarship",
            "Support for students entering a Bachelor of Engineering programme, with preference "
            + "given to students from outside the capital.",
            "This scholarship supports students entering a Bachelor of Engineering programme. "
            + "Preference is generally given to students from outside the Kathmandu Valley and to "
            + "those who could not otherwise afford the programme, which is why an application "
            + "usually asks for evidence of household income as well as academic results. "
            + "Applications go to the awarding body directly rather than through a school. The award "
            + "amount shown is indicative; staff replace it with the official figure and attach the "
            + "current application link. Anyone holding a previous year's award who wants to apply "
            + "again should check the current eligibility text rather than assuming the rules are "
            + "unchanged, since these schemes are revised regularly.",
            ResourceType.Scholarship,
            ResourceCategory.Science,
            EducationLevel.Bachelors,
            ExternalUrl: "https://nec.gov.np/",
            Provider: "Nepal Engineering Council",
            AwardAmountNpr: 120000m,
            Eligibility: "Entrants to a Bachelor of Engineering programme; preference for students "
            + "from outside the Kathmandu Valley.",
            ExpiresInDays: 45),
        new(
            "scholarship-005",
            "management-bachelors-scholarship",
            "Bachelor of Management scholarship",
            "A scholarship for students entering a Bachelor of Management or BBA programme.",
            "This scholarship supports students entering a Bachelor of Management or Bachelor of "
            + "Business Administration programme. It is usually awarded against combined academic "
            + "performance and an entrance or interview result, and applications are handled by the "
            + "institution rather than a separate body, so the application link on this entry is "
            + "the one to check first. The award amount shown is indicative and staff replace it "
            + "with the official figure. Applicants should note that a scholarship covering tuition "
            + "is not the same as one covering living costs, so the eligibility text is worth "
            + "reading carefully before assuming the award meets the full cost of the programme.",
            ResourceType.Scholarship,
            ResourceCategory.Management,
            EducationLevel.Bachelors,
            ExternalUrl: "https://www.ku.edu.np/",
            Provider: "Kathmandu University",
            AwardAmountNpr: 75000m,
            Eligibility: "Entrants to a Bachelor of Management or BBA programme.",
            ExpiresInDays: 75),
        new(
            "scholarship-006",
            "competitive-exam-coaching-support",
            "Competitive examination coaching support",
            "Financial support towards coaching for public service and competitive examinations.",
            "This scholarship covers part of the cost of coaching for public service and other "
            + "competitive examinations, which is the part of the cost most candidates cannot "
            + "otherwise meet. It is usually means-tested against family income, and awards are "
            + "typically limited to a number of candidates each year. Because the benefit is "
            + "conditional and the number of places is small, the deadline is usually early in the "
            + "year and missing it means waiting for the next cycle. The award amount shown is "
            + "indicative and staff replace it with the official figure. Applicants should confirm "
            + "the current cycle on the awarding body's own notice, since a listing like this one "
            + "is maintained by hand and can only be as current as the last staff edit.",
            ResourceType.Scholarship,
            ResourceCategory.General,
            EducationLevel.Competitive,
            ExternalUrl: "https://opmcm.gov.np/",
            Provider: "Office of the Prime Minister and Council of Ministers",
            AwardAmountNpr: 25000m,
            Eligibility: "Candidates preparing for public service or competitive examinations, "
            + "assessed against family income.",
            ExpiresInDays: 30),
        new(
            "scholarship-007",
            "plus-two-science-stream-scholarship",
            "Plus Two science stream scholarship",
            "Support for students continuing into the science stream at Plus Two level.",
            "This scholarship supports students who have completed Grade 10 and are continuing into "
            + "the science stream at Plus Two level. It exists because the science stream is the "
            + "prerequisite for the degrees that most government and private scholarships later "
            + "assume, so the cost of entry falls on families at exactly the point where the "
            + "return is furthest away. Applications are usually submitted through the school, and "
            + "awards depend on the Grade 10 result. The award amount shown is indicative and staff "
            + "replace it with the official figure. Students considering the science stream who "
            + "are still undecided should look at this alongside the Grade 11-12 merit scholarship, "
            + "since the two usually run on different cycles.",
            ResourceType.Scholarship,
            ResourceCategory.Science,
            EducationLevel.PlusTwo,
            ExternalUrl: "https://www.moest.gov.np/",
            Provider: "Ministry of Education",
            AwardAmountNpr: 20000m,
            Eligibility: "Students entering Plus Two in the science stream, awarded on Grade 10 "
            + "results.",
            ExpiresInDays: 120),
    ];
}
