using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class PatientDatum
{
    public long Id { get; set; }

    public byte[]? Uuid { get; set; }

    public string Title { get; set; } = null!;

    public string Language { get; set; } = null!;

    public string Financial { get; set; } = null!;

    public string Fname { get; set; } = null!;

    public string Lname { get; set; } = null!;

    public string Mname { get; set; } = null!;

    public DateOnly? Dob { get; set; }

    public string Street { get; set; } = null!;

    public string PostalCode { get; set; } = null!;

    public string City { get; set; } = null!;

    public string State { get; set; } = null!;

    public string CountryCode { get; set; } = null!;

    public string DriversLicense { get; set; } = null!;

    public string Ss { get; set; } = null!;

    public string? Occupation { get; set; }

    public string PhoneHome { get; set; } = null!;

    public string PhoneBiz { get; set; } = null!;

    public string PhoneContact { get; set; } = null!;

    public string PhoneCell { get; set; } = null!;

    public int PharmacyId { get; set; }

    public string Status { get; set; } = null!;

    public string ContactRelationship { get; set; } = null!;

    public DateTime? Date { get; set; }

    /// <summary>
    /// Sex at birth
    /// </summary>
    public string Sex { get; set; } = null!;

    public string Referrer { get; set; } = null!;

    public string ReferrerId { get; set; } = null!;

    public int? ProviderId { get; set; }

    public int? RefProviderId { get; set; }

    public string Email { get; set; } = null!;

    public string EmailDirect { get; set; } = null!;

    public string Ethnoracial { get; set; } = null!;

    public string Race { get; set; } = null!;

    public string Ethnicity { get; set; } = null!;

    public string Religion { get; set; } = null!;

    /// <summary>
    /// original field used for determining if patient needs an interpreter, now used for additional notes about need for interpreter
    /// </summary>
    public string Interpreter { get; set; } = null!;

    /// <summary>
    /// fk to list_options.option_id where list_id=yes_no_unknown used to determine if patient needs an interpreter
    /// </summary>
    public string? InterpreterNeeded { get; set; }

    public string Migrantseasonal { get; set; } = null!;

    public string FamilySize { get; set; } = null!;

    public string MonthlyIncome { get; set; } = null!;

    public string? BillingNote { get; set; }

    public string Homeless { get; set; } = null!;

    public DateTime? FinancialReview { get; set; }

    public string Pubpid { get; set; } = null!;

    public long Pid { get; set; }

    public string Genericname1 { get; set; } = null!;

    public string Genericval1 { get; set; } = null!;

    public string Genericname2 { get; set; } = null!;

    public string Genericval2 { get; set; } = null!;

    public string HipaaMail { get; set; } = null!;

    public string HipaaVoice { get; set; } = null!;

    public string HipaaNotice { get; set; } = null!;

    public string HipaaMessage { get; set; } = null!;

    public string HipaaAllowsms { get; set; } = null!;

    public string HipaaAllowemail { get; set; } = null!;

    public string Squad { get; set; } = null!;

    public int Fitness { get; set; }

    public string ReferralSource { get; set; } = null!;

    public string Usertext1 { get; set; } = null!;

    public string Usertext2 { get; set; } = null!;

    public string Usertext3 { get; set; } = null!;

    public string Usertext4 { get; set; } = null!;

    public string Usertext5 { get; set; } = null!;

    public string Usertext6 { get; set; } = null!;

    public string Usertext7 { get; set; } = null!;

    public string Usertext8 { get; set; } = null!;

    public string Userlist1 { get; set; } = null!;

    public string Userlist2 { get; set; } = null!;

    public string Userlist3 { get; set; } = null!;

    public string Userlist4 { get; set; } = null!;

    public string Userlist5 { get; set; } = null!;

    public string Userlist6 { get; set; } = null!;

    public string Userlist7 { get; set; } = null!;

    public string Pricelevel { get; set; } = null!;

    /// <summary>
    /// Registration Date
    /// </summary>
    public DateTime? Regdate { get; set; }

    /// <summary>
    /// Date contraceptives initially used
    /// </summary>
    public DateOnly? Contrastart { get; set; }

    public string CompletedAd { get; set; } = null!;

    /// <summary>
    /// Date and time the advance care directive was reviewed and validated by the authenticator user.
    /// </summary>
    public DateTime? AdReviewed { get; set; }

    /// <summary>
    /// fk to users.id of the user who authenticates that the advance care directive is valid.
    /// </summary>
    public long? AdvanceDirectiveUserAuthenticator { get; set; }

    public string Vfc { get; set; } = null!;

    public string Mothersname { get; set; } = null!;

    public string? Guardiansname { get; set; }

    public string AllowImmRegUse { get; set; } = null!;

    public string AllowImmInfoShare { get; set; } = null!;

    public string AllowHealthInfoEx { get; set; } = null!;

    public string AllowPatientPortal { get; set; } = null!;

    public DateTime? DeceasedDate { get; set; }

    public string DeceasedReason { get; set; } = null!;

    /// <summary>
    /// 1-Prescription Press 2-Prescription Import 3-Allergy Press 4-Allergy Import
    /// </summary>
    public sbyte? SoapImportStatus { get; set; }

    public string CmsportalLogin { get; set; } = null!;

    public string? CareTeamProvider { get; set; }

    public string? CareTeamFacility { get; set; }

    public string? CareTeamStatus { get; set; }

    public string County { get; set; } = null!;

    public string? Industry { get; set; }

    public string? ImmRegStatus { get; set; }

    public string? ImmRegStatEffdate { get; set; }

    public string? PublicityCode { get; set; }

    public string? PublCodeEffDate { get; set; }

    public string? ProtectIndicator { get; set; }

    public string? ProtIndiEffdate { get; set; }

    public string? Guardianrelationship { get; set; }

    public string? Guardiansex { get; set; }

    public string? Guardianaddress { get; set; }

    public string? Guardiancity { get; set; }

    public string? Guardianstate { get; set; }

    public string? Guardianpostalcode { get; set; }

    public string? Guardiancountry { get; set; }

    public string? Guardianphone { get; set; }

    public string? Guardianworkphone { get; set; }

    public string? Guardianemail { get; set; }

    public string? SexualOrientation { get; set; }

    public string? GenderIdentity { get; set; }

    public string? BirthFname { get; set; }

    public string? BirthLname { get; set; }

    public string? BirthMname { get; set; }

    public int Dupscore { get; set; }

    public string? NameHistory { get; set; }

    public string? Suffix { get; set; }

    public string? StreetLine2 { get; set; }

    public string? PatientGroups { get; set; }

    public string? PreventPortalApps { get; set; }

    public string? ProviderSinceDate { get; set; }

    /// <summary>
    /// users.id the user that first created this record
    /// </summary>
    public long? CreatedBy { get; set; }

    /// <summary>
    /// users.id the user that last modified this record
    /// </summary>
    public long? UpdatedBy { get; set; }

    public string? PreferredName { get; set; }

    public string? NationalityCountry { get; set; }

    public DateTime LastUpdated { get; set; }

    public string? TribalAffiliations { get; set; }

    /// <summary>
    /// Patient reported current sex
    /// </summary>
    public string? SexIdentified { get; set; }

    public string? Pronoun { get; set; }
}
