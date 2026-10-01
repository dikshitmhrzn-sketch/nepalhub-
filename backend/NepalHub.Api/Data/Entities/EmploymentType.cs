namespace NepalHub.Api.Data.Entities;

/// <summary>
/// The contract shape a posting offers. Persisted as an int and serialised by name.
/// <para>
/// This is deliberately not derivable from <see cref="WorkMode"/>: a remote role and a
/// full-time role are independent answers, and collapsing them into one field is how a job
/// board ends up unable to say "part-time, remote". Each value is a term Nepali employers
/// already use on their own postings, which matters more than taxonomy neatness for a
/// filter people choose from.
/// </para>
/// </summary>
public enum EmploymentType
{
    FullTime = 0,
    PartTime = 1,
    Contract = 2,
    Internship = 3,
}
