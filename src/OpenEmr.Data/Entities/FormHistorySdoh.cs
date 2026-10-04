using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class FormHistorySdoh
{
    public ulong Id { get; set; }

    public byte[]? Uuid { get; set; }

    public uint Pid { get; set; }

    public uint? Encounter { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// fk to users.id user that created this record
    /// </summary>
    public uint? CreatedBy { get; set; }

    /// <summary>
    /// fk to users.id user that last modified this record
    /// </summary>
    public uint? UpdatedBy { get; set; }

    public DateOnly? AssessmentDate { get; set; }

    /// <summary>
    /// fk to list_options.option_id WHERE list_id=sdoh_instruments represents the assessment tool used to administer this assessment
    /// </summary>
    public string? ScreeningTool { get; set; }

    /// <summary>
    /// fk to users.username the user that administered the assessment
    /// </summary>
    public string? Assessor { get; set; }

    /// <summary>
    /// fk to list_options.option_id WHERE list_id=sdoh_food_insecurity_risk
    /// </summary>
    public string? FoodInsecurity { get; set; }

    public string? FoodInsecurityNotes { get; set; }

    /// <summary>
    /// fk to list_options.option_id WHERE list_id=sdoh_housing_worry
    /// </summary>
    public string? HousingInstability { get; set; }

    public string? HousingInstabilityNotes { get; set; }

    /// <summary>
    /// fk to list_options.option_id WHERE list_id=sdoh_transportation_barrier
    /// </summary>
    public string? TransportationInsecurity { get; set; }

    public string? TransportationInsecurityNotes { get; set; }

    /// <summary>
    /// fk to list_options.option_id WHERE list_id=sdoh_utilities_shutoff
    /// </summary>
    public string? UtilitiesInsecurity { get; set; }

    public string? UtilitiesInsecurityNotes { get; set; }

    /// <summary>
    /// fk to list_options.option_id WHERE list_id=sdoh_financial_strain
    /// </summary>
    public string? InterpersonalSafety { get; set; }

    public string? InterpersonalSafetyNotes { get; set; }

    /// <summary>
    /// fk to list_options.option_id WHERE list_id=sdoh_financial_strain
    /// </summary>
    public string? FinancialStrain { get; set; }

    public string? FinancialStrainNotes { get; set; }

    /// <summary>
    /// fk to list_options.option_id WHERE list_id=sdoh_social_isolation_freq
    /// </summary>
    public string? SocialIsolation { get; set; }

    public string? SocialIsolationNotes { get; set; }

    /// <summary>
    /// fk to list_options.option_id WHERE list_id=sdoh_childcare_needs
    /// </summary>
    public string? ChildcareNeeds { get; set; }

    public string? ChildcareNeedsNotes { get; set; }

    /// <summary>
    /// fk to list_options.option_id WHERE list_id=sdoh_digital_access
    /// </summary>
    public string? DigitalAccess { get; set; }

    public string? DigitalAccessNotes { get; set; }

    /// <summary>
    /// fk to list_options.option_id WHERE list_id=sdoh_food_insecurity_risk
    /// </summary>
    public string? EmploymentStatus { get; set; }

    /// <summary>
    /// fk to list_options.option_id WHERE list_id=sdoh_education_level
    /// </summary>
    public string? EducationLevel { get; set; }

    /// <summary>
    /// fk to list_options.option_id WHERE list_id=sdoh_food_insecurity_risk
    /// </summary>
    public string? CaregiverStatus { get; set; }

    /// <summary>
    /// fk to list_options.option_id WHERE list_id=sdoh_food_insecurity_risk
    /// </summary>
    public string? VeteranStatus { get; set; }

    /// <summary>
    /// fk to list_options.option_id WHERE list_id=pregnancy_status
    /// </summary>
    public string? PregnancyStatus { get; set; }

    /// <summary>
    /// Estimated due date for pregnancy
    /// </summary>
    public DateOnly? PregnancyEdd { get; set; }

    /// <summary>
    /// fk to list_options.option_id WHERE list_id=pregnancy_intent Pregnancy Intent Over Next Year (codes from PregnancyIntent list)
    /// </summary>
    public string? PregnancyIntent { get; set; }

    /// <summary>
    /// fk to list_options.option_id WHERE list_id=postpartum_status
    /// </summary>
    public string? PostpartumStatus { get; set; }

    /// <summary>
    /// PostPartum end date
    /// </summary>
    public DateOnly? PostpartumEnd { get; set; }

    public string? Goals { get; set; }

    public string? Interventions { get; set; }

    public int? InstrumentScore { get; set; }

    public int? PositiveDomainCount { get; set; }

    public bool? DeclinedFlag { get; set; }

    /// <summary>
    /// fk to list_options.option_id WHERE list_id=disability_status
    /// </summary>
    public string? DisabilityStatus { get; set; }

    public string? DisabilityStatusNotes { get; set; }

    public string? DisabilityScale { get; set; }

    /// <summary>
    /// fk to list_options.option_id WHERE list_id=vital_signs_answers
    /// </summary>
    public string? HungerQ1 { get; set; }

    /// <summary>
    /// fk to list_options.option_id WHERE list_id=vital_signs_answers
    /// </summary>
    public string? HungerQ2 { get; set; }

    /// <summary>
    /// Calculated HVS score
    /// </summary>
    public int? HungerScore { get; set; }
}
