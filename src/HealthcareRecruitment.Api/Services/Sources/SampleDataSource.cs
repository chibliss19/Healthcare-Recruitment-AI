using HealthcareRecruitment.Core.Models;
using Microsoft.Extensions.Options;

namespace HealthcareRecruitment.Api.Services.Sources;

public class SampleDataOptions
{
    /// <summary>Serve built-in demo listings when no real API source is configured.</summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Built-in NI healthcare listings used when neither Adzuna nor Careerjet keys are configured,
/// so the dashboard and AI wizard can be demonstrated offline.
/// </summary>
public class SampleDataSource(IOptions<SampleDataOptions> options) : IVacancySource
{
    public string Name => "Sample";

    public bool IsConfigured => options.Value.Enabled;

    public Task<IReadOnlyList<Vacancy>> FetchAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var raw = new (string Title, string Employer, string Location, decimal? Min, decimal? Max, int DaysAgo, string Description)[]
        {
            ("Staff Nurse - Medical Ward", "Belfast Health and Social Care Trust", "Belfast", 28407, 34581, 2,
                "Band 5 registered nurse post on a busy acute medical ward. NMC registration required. Rotational shifts including nights and weekends."),
            ("Health Care Assistant", "Western Health and Social Care Trust", "Derry / Londonderry", 22383, 24336, 1,
                "Band 2/3 healthcare assistant supporting nursing teams with personal care, observations and patient comfort. NVQ Level 2 in Health and Social Care desirable."),
            ("Specialist Physiotherapist - Stroke", "Northern Health and Social Care Trust", "Antrim", 35392, 42618, 5,
                "Band 6 physiotherapist within the stroke rehabilitation pathway. HCPC registration and post-graduate neuro experience required."),
            ("Social Worker - Family Intervention", "South Eastern Health and Social Care Trust", "Lisburn", 33115, 40476, 3,
                "Band 6 social worker in the family and childcare programme. NISCC registration and AYE completion required."),
            ("Consultant Psychiatrist - General Adult", "Southern Health and Social Care Trust", "Craigavon", 88799, 119723, 8,
                "Full-time consultant post in general adult psychiatry. Full GMC registration with a licence to practise and MRCPsych or equivalent."),
            ("Pharmacist - Rotational", "Belfast Health and Social Care Trust", "Belfast", 35392, 42618, 4,
                "Band 6 rotational hospital pharmacist covering dispensary, wards and aseptic services. PSNI registration required."),
            ("Occupational Therapist - Community", "Northern Health and Social Care Trust", "Ballymena", 28407, 34581, 6,
                "Band 5 community occupational therapist supporting hospital discharge and reablement. HCPC registration essential. Access to a car required."),
            ("Paramedic", "Northern Ireland Ambulance Service", "Region-wide (NI)", 33115, 40476, 7,
                "Registered paramedic delivering emergency pre-hospital care across Northern Ireland. HCPC paramedic registration and C1 driving licence required."),
            ("Dental Nurse", "Ulster Independent Clinic", "Belfast", 23000, 27000, 9,
                "Qualified dental nurse for a busy private clinic. GDC registration required; sedation experience an advantage."),
            ("Midwife - Delivery Suite", "Western Health and Social Care Trust", "Enniskillen", 28407, 34581, 2,
                "Band 5/6 midwife for the South West Acute Hospital delivery suite. NMC registration (midwifery) essential."),
            ("Radiographer - Diagnostic", "South Eastern Health and Social Care Trust", "Dundonald", 28407, 34581, 10,
                "Band 5 diagnostic radiographer covering general X-ray, theatre and mobile imaging. HCPC registration required."),
            ("Speech and Language Therapist - Paediatrics", "Southern Health and Social Care Trust", "Newry", 28407, 34581, 12,
                "Band 5 SLT within community paediatric services. HCPC registration and experience with early years communication needs."),
            ("Mental Health Nurse - CAMHS", "Belfast Health and Social Care Trust", "Belfast", 35392, 42618, 1,
                "Band 6 registered mental health nurse in child and adolescent mental health services. NMC (RNMH) registration required."),
            ("Care Home Manager", "Four Seasons Health Care", "Newtownabbey", 45000, 55000, 14,
                "Registered manager for a 60-bed nursing home. RQIA registration requirements apply. Strong leadership and compliance track record essential."),
            ("General Practitioner - Salaried", "Federation of Family Practices NI", "Omagh", 75000, 95000, 11,
                "Salaried GP, 6-8 sessions per week. Full GMC registration, MRCGP and inclusion on the NI performers list required."),
        };

        IReadOnlyList<Vacancy> vacancies = raw.Select((job, i) => new Vacancy
        {
            ExternalId = $"sample-{i + 1:D3}",
            Source = Name,
            Title = job.Title,
            Employer = job.Employer,
            Location = job.Location,
            SalaryMin = job.Min,
            SalaryMax = job.Max,
            Description = job.Description,
            Url = "https://jobs.hscni.net/",
            PostedDate = now.AddDays(-job.DaysAgo),
            DedupeKey = VacancyNormalizer.BuildDedupeKey(job.Title, job.Employer, job.Location),
        }).ToList();

        return Task.FromResult(vacancies);
    }
}
