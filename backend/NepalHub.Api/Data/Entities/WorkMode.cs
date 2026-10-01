namespace NepalHub.Api.Data.Entities;

/// <summary>
/// Where the work is actually done. Persisted as an int and serialised by name.
/// <para>
/// Separate from <see cref="EmploymentType"/> because the two answer different questions a
/// applicant has: one is about the shape of the day, the other is about the address.
/// <c>Remote</c> postings deliberately carry no district, which is why
/// <see cref="Job.DistrictId"/> is nullable rather than always populated — a remote role has
/// no place to be.
/// </para>
/// </summary>
public enum WorkMode
{
    OnSite = 0,
    Remote = 1,
    Hybrid = 2,
}
