using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using OpenEmr.Data.Entities;

namespace OpenEmr.Data.Context;

public partial class OpenEmrDbContext : DbContext
{
    public OpenEmrDbContext(DbContextOptions<OpenEmrDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Address> Addresses { get; set; }

    public virtual DbSet<AmcMiscDatum> AmcMiscData { get; set; }

    public virtual DbSet<Amendment> Amendments { get; set; }

    public virtual DbSet<AmendmentsHistory> AmendmentsHistories { get; set; }

    public virtual DbSet<ApiLog> ApiLogs { get; set; }

    public virtual DbSet<ApiRefreshToken> ApiRefreshTokens { get; set; }

    public virtual DbSet<ApiToken> ApiTokens { get; set; }

    public virtual DbSet<ArActivity> ArActivities { get; set; }

    public virtual DbSet<ArSession> ArSessions { get; set; }

    public virtual DbSet<AuditDetail> AuditDetails { get; set; }

    public virtual DbSet<AuditMaster> AuditMasters { get; set; }

    public virtual DbSet<AutomaticNotification> AutomaticNotifications { get; set; }

    public virtual DbSet<BackgroundService> BackgroundServices { get; set; }

    public virtual DbSet<Batchcom> Batchcoms { get; set; }

    public virtual DbSet<BenefitEligibility> BenefitEligibilities { get; set; }

    public virtual DbSet<Billing> Billings { get; set; }

    public virtual DbSet<CalendarExternal> CalendarExternals { get; set; }

    public virtual DbSet<CareTeam> CareTeams { get; set; }

    public virtual DbSet<CareTeamMember> CareTeamMembers { get; set; }

    public virtual DbSet<CategoriesSeq> CategoriesSeqs { get; set; }

    public virtual DbSet<CategoriesToDocument> CategoriesToDocuments { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<CcdaComponent> CcdaComponents { get; set; }

    public virtual DbSet<CcdaFieldMapping> CcdaFieldMappings { get; set; }

    public virtual DbSet<CcdaSection> CcdaSections { get; set; }

    public virtual DbSet<CcdaTableMapping> CcdaTableMappings { get; set; }

    public virtual DbSet<Ccdum> Ccda { get; set; }

    public virtual DbSet<ChartTracker> ChartTrackers { get; set; }

    public virtual DbSet<Claim> Claims { get; set; }

    public virtual DbSet<ClinicalNotesDocument> ClinicalNotesDocuments { get; set; }

    public virtual DbSet<ClinicalNotesProcedureResult> ClinicalNotesProcedureResults { get; set; }

    public virtual DbSet<ClinicalPlan> ClinicalPlans { get; set; }

    public virtual DbSet<ClinicalPlansRule> ClinicalPlansRules { get; set; }

    public virtual DbSet<ClinicalRule> ClinicalRules { get; set; }

    public virtual DbSet<ClinicalRulesLog> ClinicalRulesLogs { get; set; }

    public virtual DbSet<Code> Codes { get; set; }

    public virtual DbSet<CodeType> CodeTypes { get; set; }

    public virtual DbSet<CodesHistory> CodesHistories { get; set; }

    public virtual DbSet<Contact> Contacts { get; set; }

    public virtual DbSet<ContactAddress> ContactAddresses { get; set; }

    public virtual DbSet<ContactRelation> ContactRelations { get; set; }

    public virtual DbSet<ContactTelecom> ContactTelecoms { get; set; }

    public virtual DbSet<Customlist> Customlists { get; set; }

    public virtual DbSet<DatedReminder> DatedReminders { get; set; }

    public virtual DbSet<DatedRemindersLink> DatedRemindersLinks { get; set; }

    public virtual DbSet<DirectMessageLog> DirectMessageLogs { get; set; }

    public virtual DbSet<Document> Documents { get; set; }

    public virtual DbSet<DocumentTemplate> DocumentTemplates { get; set; }

    public virtual DbSet<DocumentTemplateProfile> DocumentTemplateProfiles { get; set; }

    public virtual DbSet<DocumentsLegalCategory> DocumentsLegalCategories { get; set; }

    public virtual DbSet<DocumentsLegalDetail> DocumentsLegalDetails { get; set; }

    public virtual DbSet<DocumentsLegalMaster> DocumentsLegalMasters { get; set; }

    public virtual DbSet<Drug> Drugs { get; set; }

    public virtual DbSet<DrugInventory> DrugInventories { get; set; }

    public virtual DbSet<DrugSale> DrugSales { get; set; }

    public virtual DbSet<DrugTemplate> DrugTemplates { get; set; }

    public virtual DbSet<DsiSourceAttribute> DsiSourceAttributes { get; set; }

    public virtual DbSet<EdiSequence> EdiSequences { get; set; }

    public virtual DbSet<EligibilityVerification> EligibilityVerifications { get; set; }

    public virtual DbSet<EmailQueue> EmailQueues { get; set; }

    public virtual DbSet<EmployerDatum> EmployerData { get; set; }

    public virtual DbSet<EncCategoryMap> EncCategoryMaps { get; set; }

    public virtual DbSet<ErxNarcotic> ErxNarcotics { get; set; }

    public virtual DbSet<ErxRxLog> ErxRxLogs { get; set; }

    public virtual DbSet<ErxTtlTouch> ErxTtlTouches { get; set; }

    public virtual DbSet<EsignSignature> EsignSignatures { get; set; }

    public virtual DbSet<ExportJob> ExportJobs { get; set; }

    public virtual DbSet<ExtendedLog> ExtendedLogs { get; set; }

    public virtual DbSet<ExternalEncounter> ExternalEncounters { get; set; }

    public virtual DbSet<ExternalProcedure> ExternalProcedures { get; set; }

    public virtual DbSet<Facility> Facilities { get; set; }

    public virtual DbSet<FacilityUserId> FacilityUserIds { get; set; }

    public virtual DbSet<FeeSchedule> FeeSchedules { get; set; }

    public virtual DbSet<FeeSheetOption> FeeSheetOptions { get; set; }

    public virtual DbSet<Form> Forms { get; set; }

    public virtual DbSet<FormCarePlan> FormCarePlans { get; set; }

    public virtual DbSet<FormClinicalInstruction> FormClinicalInstructions { get; set; }

    public virtual DbSet<FormClinicalNote> FormClinicalNotes { get; set; }

    public virtual DbSet<FormDictation> FormDictations { get; set; }

    public virtual DbSet<FormEncounter> FormEncounters { get; set; }

    public virtual DbSet<FormEyeAcuity> FormEyeAcuities { get; set; }

    public virtual DbSet<FormEyeAntseg> FormEyeAntsegs { get; set; }

    public virtual DbSet<FormEyeBase> FormEyeBases { get; set; }

    public virtual DbSet<FormEyeBiometric> FormEyeBiometrics { get; set; }

    public virtual DbSet<FormEyeExternal> FormEyeExternals { get; set; }

    public virtual DbSet<FormEyeHpi> FormEyeHpis { get; set; }

    public virtual DbSet<FormEyeLocking> FormEyeLockings { get; set; }

    public virtual DbSet<FormEyeMagDispense> FormEyeMagDispenses { get; set; }

    public virtual DbSet<FormEyeMagImpplan> FormEyeMagImpplans { get; set; }

    public virtual DbSet<FormEyeMagOrder> FormEyeMagOrders { get; set; }

    public virtual DbSet<FormEyeMagPref> FormEyeMagPrefs { get; set; }

    public virtual DbSet<FormEyeMagWearing> FormEyeMagWearings { get; set; }

    public virtual DbSet<FormEyeNeuro> FormEyeNeuros { get; set; }

    public virtual DbSet<FormEyePostseg> FormEyePostsegs { get; set; }

    public virtual DbSet<FormEyeRefraction> FormEyeRefractions { get; set; }

    public virtual DbSet<FormEyeRo> FormEyeRos { get; set; }

    public virtual DbSet<FormEyeVital> FormEyeVitals { get; set; }

    public virtual DbSet<FormFunctionalCognitiveStatus> FormFunctionalCognitiveStatuses { get; set; }

    public virtual DbSet<FormGroupAttendance> FormGroupAttendances { get; set; }

    public virtual DbSet<FormGroupsEncounter> FormGroupsEncounters { get; set; }

    public virtual DbSet<FormHistorySdoh> FormHistorySdohs { get; set; }

    public virtual DbSet<FormHistorySdohHealthConcern> FormHistorySdohHealthConcerns { get; set; }

    public virtual DbSet<FormMiscBillingOption> FormMiscBillingOptions { get; set; }

    public virtual DbSet<FormObservation> FormObservations { get; set; }

    public virtual DbSet<FormQuestionnaireAssessment> FormQuestionnaireAssessments { get; set; }

    public virtual DbSet<FormReviewof> FormReviewofs { get; set; }

    public virtual DbSet<FormRo> FormRos { get; set; }

    public virtual DbSet<FormSoap> FormSoaps { get; set; }

    public virtual DbSet<FormTaskman> FormTaskmen { get; set; }

    public virtual DbSet<FormVital> FormVitals { get; set; }

    public virtual DbSet<FormVitalDetail> FormVitalDetails { get; set; }

    public virtual DbSet<FormVitalsCalculation> FormVitalsCalculations { get; set; }

    public virtual DbSet<FormVitalsCalculationComponent> FormVitalsCalculationComponents { get; set; }

    public virtual DbSet<FormVitalsCalculationFormVital> FormVitalsCalculationFormVitals { get; set; }

    public virtual DbSet<GaclAcl> GaclAcls { get; set; }

    public virtual DbSet<GaclAclSection> GaclAclSections { get; set; }

    public virtual DbSet<GaclAclSeq> GaclAclSeqs { get; set; }

    public virtual DbSet<GaclAco> GaclAcos { get; set; }

    public virtual DbSet<GaclAcoMap> GaclAcoMaps { get; set; }

    public virtual DbSet<GaclAcoSection> GaclAcoSections { get; set; }

    public virtual DbSet<GaclAcoSectionsSeq> GaclAcoSectionsSeqs { get; set; }

    public virtual DbSet<GaclAcoSeq> GaclAcoSeqs { get; set; }

    public virtual DbSet<GaclAro> GaclAros { get; set; }

    public virtual DbSet<GaclAroGroup> GaclAroGroups { get; set; }

    public virtual DbSet<GaclAroGroupsIdSeq> GaclAroGroupsIdSeqs { get; set; }

    public virtual DbSet<GaclAroGroupsMap> GaclAroGroupsMaps { get; set; }

    public virtual DbSet<GaclAroMap> GaclAroMaps { get; set; }

    public virtual DbSet<GaclAroSection> GaclAroSections { get; set; }

    public virtual DbSet<GaclAroSectionsSeq> GaclAroSectionsSeqs { get; set; }

    public virtual DbSet<GaclAroSeq> GaclAroSeqs { get; set; }

    public virtual DbSet<GaclAxo> GaclAxos { get; set; }

    public virtual DbSet<GaclAxoGroup> GaclAxoGroups { get; set; }

    public virtual DbSet<GaclAxoGroupsMap> GaclAxoGroupsMaps { get; set; }

    public virtual DbSet<GaclAxoMap> GaclAxoMaps { get; set; }

    public virtual DbSet<GaclAxoSection> GaclAxoSections { get; set; }

    public virtual DbSet<GaclGroupsAroMap> GaclGroupsAroMaps { get; set; }

    public virtual DbSet<GaclGroupsAxoMap> GaclGroupsAxoMaps { get; set; }

    public virtual DbSet<GaclPhpgacl> GaclPhpgacls { get; set; }

    public virtual DbSet<Global> Globals { get; set; }

    public virtual DbSet<Gprelation> Gprelations { get; set; }

    public virtual DbSet<Group> Groups { get; set; }

    public virtual DbSet<HistoryDatum> HistoryData { get; set; }

    public virtual DbSet<Icd10DxOrderCode> Icd10DxOrderCodes { get; set; }

    public virtual DbSet<Icd10GemDx109> Icd10GemDx109s { get; set; }

    public virtual DbSet<Icd10GemDx910> Icd10GemDx910s { get; set; }

    public virtual DbSet<Icd10GemPcs109> Icd10GemPcs109s { get; set; }

    public virtual DbSet<Icd10GemPcs910> Icd10GemPcs910s { get; set; }

    public virtual DbSet<Icd10PcsOrderCode> Icd10PcsOrderCodes { get; set; }

    public virtual DbSet<Icd10ReimbrDx910> Icd10ReimbrDx910s { get; set; }

    public virtual DbSet<Icd10ReimbrPcs910> Icd10ReimbrPcs910s { get; set; }

    public virtual DbSet<Icd9DxCode> Icd9DxCodes { get; set; }

    public virtual DbSet<Icd9DxLongCode> Icd9DxLongCodes { get; set; }

    public virtual DbSet<Icd9SgCode> Icd9SgCodes { get; set; }

    public virtual DbSet<Icd9SgLongCode> Icd9SgLongCodes { get; set; }

    public virtual DbSet<Immunization> Immunizations { get; set; }

    public virtual DbSet<ImmunizationObservation> ImmunizationObservations { get; set; }

    public virtual DbSet<InsuranceCompany> InsuranceCompanies { get; set; }

    public virtual DbSet<InsuranceDatum> InsuranceData { get; set; }

    public virtual DbSet<InsuranceNumber> InsuranceNumbers { get; set; }

    public virtual DbSet<InsuranceTypeCode> InsuranceTypeCodes { get; set; }

    public virtual DbSet<IpTracking> IpTrackings { get; set; }

    public virtual DbSet<IssueEncounter> IssueEncounters { get; set; }

    public virtual DbSet<IssueType> IssueTypes { get; set; }

    public virtual DbSet<JwtGrantHistory> JwtGrantHistories { get; set; }

    public virtual DbSet<Key> Keys { get; set; }

    public virtual DbSet<LangConstant> LangConstants { get; set; }

    public virtual DbSet<LangCustom> LangCustoms { get; set; }

    public virtual DbSet<LangDefinition> LangDefinitions { get; set; }

    public virtual DbSet<LangLanguage> LangLanguages { get; set; }

    public virtual DbSet<LayoutGroupProperty> LayoutGroupProperties { get; set; }

    public virtual DbSet<LayoutOption> LayoutOptions { get; set; }

    public virtual DbSet<LbfDatum> LbfData { get; set; }

    public virtual DbSet<LbtDatum> LbtData { get; set; }

    public virtual DbSet<List> Lists { get; set; }

    public virtual DbSet<ListOption> ListOptions { get; set; }

    public virtual DbSet<ListsMedication> ListsMedications { get; set; }

    public virtual DbSet<ListsTouch> ListsTouches { get; set; }

    public virtual DbSet<Log> Logs { get; set; }

    public virtual DbSet<LogCommentEncrypt> LogCommentEncrypts { get; set; }

    public virtual DbSet<LoginMfaRegistration> LoginMfaRegistrations { get; set; }

    public virtual DbSet<MedexIcon> MedexIcons { get; set; }

    public virtual DbSet<MedexOutgoing> MedexOutgoings { get; set; }

    public virtual DbSet<MedexPref> MedexPrefs { get; set; }

    public virtual DbSet<MedexRecall> MedexRecalls { get; set; }

    public virtual DbSet<Migration> Migrations { get; set; }

    public virtual DbSet<MiscAddressBook> MiscAddressBooks { get; set; }

    public virtual DbSet<Module> Modules { get; set; }

    public virtual DbSet<ModuleAclGroupSetting> ModuleAclGroupSettings { get; set; }

    public virtual DbSet<ModuleAclSection> ModuleAclSections { get; set; }

    public virtual DbSet<ModuleAclUserSetting> ModuleAclUserSettings { get; set; }

    public virtual DbSet<ModuleConfiguration> ModuleConfigurations { get; set; }

    public virtual DbSet<ModulesHooksSetting> ModulesHooksSettings { get; set; }

    public virtual DbSet<ModulesSetting> ModulesSettings { get; set; }

    public virtual DbSet<Note> Notes { get; set; }

    public virtual DbSet<NotificationLog> NotificationLogs { get; set; }

    public virtual DbSet<NotificationSetting> NotificationSettings { get; set; }

    public virtual DbSet<OauthClient> OauthClients { get; set; }

    public virtual DbSet<OauthTrustedUser> OauthTrustedUsers { get; set; }

    public virtual DbSet<OnetimeAuth> OnetimeAuths { get; set; }

    public virtual DbSet<Onote> Onotes { get; set; }

    public virtual DbSet<OnsiteDocument> OnsiteDocuments { get; set; }

    public virtual DbSet<OnsiteMail> OnsiteMails { get; set; }

    public virtual DbSet<OnsiteMessage> OnsiteMessages { get; set; }

    public virtual DbSet<OnsiteOnline> OnsiteOnlines { get; set; }

    public virtual DbSet<OnsitePortalActivity> OnsitePortalActivities { get; set; }

    public virtual DbSet<OnsiteSignature> OnsiteSignatures { get; set; }

    public virtual DbSet<OpenemrModule> OpenemrModules { get; set; }

    public virtual DbSet<OpenemrModuleVar> OpenemrModuleVars { get; set; }

    public virtual DbSet<OpenemrPostcalendarCategory> OpenemrPostcalendarCategories { get; set; }

    public virtual DbSet<OpenemrPostcalendarEvent> OpenemrPostcalendarEvents { get; set; }

    public virtual DbSet<PatientAccessOnsite> PatientAccessOnsites { get; set; }

    public virtual DbSet<PatientBirthdayAlert> PatientBirthdayAlerts { get; set; }

    public virtual DbSet<PatientCareExperiencePreference> PatientCareExperiencePreferences { get; set; }

    public virtual DbSet<PatientDatum> PatientData { get; set; }

    public virtual DbSet<PatientHistory> PatientHistories { get; set; }

    public virtual DbSet<PatientPortalMenu> PatientPortalMenus { get; set; }

    public virtual DbSet<PatientReminder> PatientReminders { get; set; }

    public virtual DbSet<PatientSetting> PatientSettings { get; set; }

    public virtual DbSet<PatientTracker> PatientTrackers { get; set; }

    public virtual DbSet<PatientTrackerElement> PatientTrackerElements { get; set; }

    public virtual DbSet<PatientTreatmentInterventionPreference> PatientTreatmentInterventionPreferences { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<PaymentGatewayDetail> PaymentGatewayDetails { get; set; }

    public virtual DbSet<PaymentProcessingAudit> PaymentProcessingAudits { get; set; }

    public virtual DbSet<Person> People { get; set; }

    public virtual DbSet<PersonPatientLink> PersonPatientLinks { get; set; }

    public virtual DbSet<Pharmacy> Pharmacies { get; set; }

    public virtual DbSet<PhoneNumber> PhoneNumbers { get; set; }

    public virtual DbSet<Pnote> Pnotes { get; set; }

    public virtual DbSet<PreferenceValueSet> PreferenceValueSets { get; set; }

    public virtual DbSet<Prescription> Prescriptions { get; set; }

    public virtual DbSet<Price> Prices { get; set; }

    public virtual DbSet<ProAssessment> ProAssessments { get; set; }

    public virtual DbSet<ProcedureAnswer> ProcedureAnswers { get; set; }

    public virtual DbSet<ProcedureOrder> ProcedureOrders { get; set; }

    public virtual DbSet<ProcedureOrderCode> ProcedureOrderCodes { get; set; }

    public virtual DbSet<ProcedureOrderRelationship> ProcedureOrderRelationships { get; set; }

    public virtual DbSet<ProcedureProvider> ProcedureProviders { get; set; }

    public virtual DbSet<ProcedureQuestion> ProcedureQuestions { get; set; }

    public virtual DbSet<ProcedureReport> ProcedureReports { get; set; }

    public virtual DbSet<ProcedureResult> ProcedureResults { get; set; }

    public virtual DbSet<ProcedureSpeciman> ProcedureSpecimen { get; set; }

    public virtual DbSet<ProcedureType> ProcedureTypes { get; set; }

    public virtual DbSet<ProductRegistration> ProductRegistrations { get; set; }

    public virtual DbSet<ProductWarehouse> ProductWarehouses { get; set; }

    public virtual DbSet<QuestionnaireRepository> QuestionnaireRepositories { get; set; }

    public virtual DbSet<QuestionnaireResponse> QuestionnaireResponses { get; set; }

    public virtual DbSet<RecentPatient> RecentPatients { get; set; }

    public virtual DbSet<Registry> Registries { get; set; }

    public virtual DbSet<ReportItemized> ReportItemizeds { get; set; }

    public virtual DbSet<ReportResult> ReportResults { get; set; }

    public virtual DbSet<RuleAction> RuleActions { get; set; }

    public virtual DbSet<RuleActionItem> RuleActionItems { get; set; }

    public virtual DbSet<RuleFilter> RuleFilters { get; set; }

    public virtual DbSet<RulePatientDatum> RulePatientData { get; set; }

    public virtual DbSet<RuleReminder> RuleReminders { get; set; }

    public virtual DbSet<RuleTarget> RuleTargets { get; set; }

    public virtual DbSet<Sequence> Sequences { get; set; }

    public virtual DbSet<SessionTracker> SessionTrackers { get; set; }

    public virtual DbSet<SharedAttribute> SharedAttributes { get; set; }

    public virtual DbSet<StandardizedTablesTrack> StandardizedTablesTracks { get; set; }

    public virtual DbSet<SupportedExternalDataload> SupportedExternalDataloads { get; set; }

    public virtual DbSet<SyndromicSurveillance> SyndromicSurveillances { get; set; }

    public virtual DbSet<TemplateUser> TemplateUsers { get; set; }

    public virtual DbSet<TherapyGroup> TherapyGroups { get; set; }

    public virtual DbSet<TherapyGroupsCounselor> TherapyGroupsCounselors { get; set; }

    public virtual DbSet<TherapyGroupsParticipant> TherapyGroupsParticipants { get; set; }

    public virtual DbSet<TherapyGroupsParticipantAttendance> TherapyGroupsParticipantAttendances { get; set; }

    public virtual DbSet<TrackEvent> TrackEvents { get; set; }

    public virtual DbSet<Transaction> Transactions { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserSetting> UserSettings { get; set; }

    public virtual DbSet<UsersFacility> UsersFacilities { get; set; }

    public virtual DbSet<UsersSecure> UsersSecures { get; set; }

    public virtual DbSet<UuidMapping> UuidMappings { get; set; }

    public virtual DbSet<UuidRegistry> UuidRegistries { get; set; }

    public virtual DbSet<Valueset> Valuesets { get; set; }

    public virtual DbSet<ValuesetOid> ValuesetOids { get; set; }

    public virtual DbSet<VerifyEmail> VerifyEmails { get; set; }

    public virtual DbSet<Version> Versions { get; set; }

    public virtual DbSet<Void> Voids { get; set; }

    public virtual DbSet<X12Partner> X12Partners { get; set; }

    public virtual DbSet<X12RemoteTracker> X12RemoteTrackers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_general_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<Address>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("addresses");

            entity.HasIndex(e => e.ForeignId, "foreign_id");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.City)
                .HasMaxLength(255)
                .HasColumnName("city");
            entity.Property(e => e.Country)
                .HasMaxLength(255)
                .HasColumnName("country");
            entity.Property(e => e.District)
                .HasMaxLength(255)
                .HasComment("The county or district of the address")
                .HasColumnName("district");
            entity.Property(e => e.ForeignId)
                .HasColumnType("int(11)")
                .HasColumnName("foreign_id");
            entity.Property(e => e.Line1)
                .HasMaxLength(255)
                .HasColumnName("line1");
            entity.Property(e => e.Line2)
                .HasMaxLength(255)
                .HasColumnName("line2");
            entity.Property(e => e.PlusFour)
                .HasMaxLength(4)
                .HasColumnName("plus_four");
            entity.Property(e => e.State)
                .HasMaxLength(35)
                .HasColumnName("state");
            entity.Property(e => e.Zip)
                .HasMaxLength(10)
                .HasColumnName("zip");
        });

        modelBuilder.Entity<AmcMiscDatum>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("amc_misc_data");

            entity.HasIndex(e => new { e.AmcId, e.Pid, e.MapId }, "amc_id");

            entity.Property(e => e.AmcId)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("Unique and maps to list_options list clinical_rules")
                .HasColumnName("amc_id");
            entity.Property(e => e.DateCompleted)
                .HasColumnType("datetime")
                .HasColumnName("date_completed");
            entity.Property(e => e.DateCreated)
                .HasColumnType("datetime")
                .HasColumnName("date_created");
            entity.Property(e => e.MapCategory)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasComment("Maps to an object category (such as prescriptions etc.)")
                .HasColumnName("map_category");
            entity.Property(e => e.MapId)
                .HasComment("Maps to an object id (such as prescription id etc.)")
                .HasColumnType("bigint(20)")
                .HasColumnName("map_id");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.SocProvided)
                .HasColumnType("datetime")
                .HasColumnName("soc_provided");
        });

        modelBuilder.Entity<Amendment>(entity =>
        {
            entity.HasKey(e => e.AmendmentId).HasName("PRIMARY");

            entity.ToTable("amendments");

            entity.HasIndex(e => e.Pid, "amendment_pid");

            entity.Property(e => e.AmendmentId)
                .HasComment("Amendment ID")
                .HasColumnType("int(11)")
                .HasColumnName("amendment_id");
            entity.Property(e => e.AmendmentBy)
                .HasMaxLength(50)
                .HasComment("Amendment requested from")
                .HasColumnName("amendment_by");
            entity.Property(e => e.AmendmentDate)
                .HasComment("Amendement request date")
                .HasColumnName("amendment_date");
            entity.Property(e => e.AmendmentDesc)
                .HasComment("Amendment Details")
                .HasColumnType("text")
                .HasColumnName("amendment_desc");
            entity.Property(e => e.AmendmentStatus)
                .HasMaxLength(50)
                .HasComment("Amendment status accepted/rejected/null")
                .HasColumnName("amendment_status");
            entity.Property(e => e.CreatedBy)
                .HasComment("references users.id for session owner")
                .HasColumnType("int(11)")
                .HasColumnName("created_by");
            entity.Property(e => e.CreatedTime)
                .HasComment("created time")
                .HasColumnType("timestamp")
                .HasColumnName("created_time");
            entity.Property(e => e.ModifiedBy)
                .HasComment("references users.id for session owner")
                .HasColumnType("int(11)")
                .HasColumnName("modified_by");
            entity.Property(e => e.ModifiedTime)
                .HasComment("modified time")
                .HasColumnType("timestamp")
                .HasColumnName("modified_time");
            entity.Property(e => e.Pid)
                .HasComment("Patient ID from patient_data")
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
        });

        modelBuilder.Entity<AmendmentsHistory>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("amendments_history");

            entity.HasIndex(e => e.AmendmentId, "amendment_history_id");

            entity.Property(e => e.AmendmentId)
                .ValueGeneratedOnAdd()
                .HasComment("Amendment ID")
                .HasColumnType("int(11)")
                .HasColumnName("amendment_id");
            entity.Property(e => e.AmendmentNote)
                .HasComment("Amendment requested from")
                .HasColumnType("text")
                .HasColumnName("amendment_note");
            entity.Property(e => e.AmendmentStatus)
                .HasMaxLength(50)
                .HasComment("Amendment Request Status")
                .HasColumnName("amendment_status");
            entity.Property(e => e.CreatedBy)
                .HasComment("references users.id for session owner")
                .HasColumnType("int(11)")
                .HasColumnName("created_by");
            entity.Property(e => e.CreatedTime)
                .HasComment("created time")
                .HasColumnType("timestamp")
                .HasColumnName("created_time");
        });

        modelBuilder.Entity<ApiLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("api_log");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.ClientId)
                .HasMaxLength(80)
                .HasDefaultValueSql("''")
                .HasComment("oauth_clients.client_id of the API client that made the request")
                .HasColumnName("client_id");
            entity.Property(e => e.CreatedTime)
                .HasColumnType("timestamp")
                .HasColumnName("created_time");
            entity.Property(e => e.IpAddress)
                .HasMaxLength(255)
                .HasColumnName("ip_address");
            entity.Property(e => e.LogId)
                .HasColumnType("int(11)")
                .HasColumnName("log_id");
            entity.Property(e => e.Method)
                .HasMaxLength(20)
                .HasColumnName("method");
            entity.Property(e => e.PatientId)
                .HasColumnType("bigint(20)")
                .HasColumnName("patient_id");
            entity.Property(e => e.Request)
                .HasMaxLength(255)
                .HasColumnName("request");
            entity.Property(e => e.RequestBody).HasColumnName("request_body");
            entity.Property(e => e.RequestUrl)
                .HasColumnType("text")
                .HasColumnName("request_url");
            entity.Property(e => e.Response).HasColumnName("response");
            entity.Property(e => e.UserId)
                .HasColumnType("bigint(20)")
                .HasColumnName("user_id");
        });

        modelBuilder.Entity<ApiRefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("api_refresh_token", tb => tb.HasComment("Holds information about api refresh tokens."));

            entity.HasIndex(e => new { e.ClientId, e.UserId }, "api_refresh_token_usr_client_idx");

            entity.HasIndex(e => e.Token, "token").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.ClientId)
                .HasMaxLength(80)
                .HasColumnName("client_id");
            entity.Property(e => e.Expiry)
                .HasColumnType("datetime")
                .HasColumnName("expiry");
            entity.Property(e => e.Revoked)
                .HasComment("1=revoked,0=not revoked")
                .HasColumnName("revoked");
            entity.Property(e => e.Token)
                .HasMaxLength(128)
                .HasColumnName("token");
            entity.Property(e => e.UserId)
                .HasMaxLength(40)
                .HasColumnName("user_id");
        });

        modelBuilder.Entity<ApiToken>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("api_token");

            entity.HasIndex(e => e.Token, "token").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.ClientId)
                .HasMaxLength(80)
                .HasColumnName("client_id");
            entity.Property(e => e.Context)
                .HasComment("context values that change/govern how access token are used")
                .HasColumnType("text")
                .HasColumnName("context");
            entity.Property(e => e.Expiry)
                .HasColumnType("datetime")
                .HasColumnName("expiry");
            entity.Property(e => e.Revoked)
                .HasComment("1=revoked,0=not revoked")
                .HasColumnName("revoked");
            entity.Property(e => e.Scope)
                .HasComment("json encoded")
                .HasColumnType("text")
                .HasColumnName("scope");
            entity.Property(e => e.Token)
                .HasMaxLength(128)
                .HasColumnName("token");
            entity.Property(e => e.UserId)
                .HasMaxLength(40)
                .HasColumnName("user_id");
        });

        modelBuilder.Entity<ArActivity>(entity =>
        {
            entity.HasKey(e => new { e.Pid, e.Encounter, e.SequenceNo })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0, 0 });

            entity.ToTable("ar_activity");

            entity.HasIndex(e => e.SessionId, "session_id");

            entity.Property(e => e.Pid)
                .HasColumnType("int(11)")
                .HasColumnName("pid");
            entity.Property(e => e.Encounter)
                .HasColumnType("int(11)")
                .HasColumnName("encounter");
            entity.Property(e => e.SequenceNo)
                .HasComment("Ar_activity sequence_no, incremented in code")
                .HasColumnType("int(10) unsigned")
                .HasColumnName("sequence_no");
            entity.Property(e => e.AccountCode)
                .HasMaxLength(15)
                .HasColumnName("account_code");
            entity.Property(e => e.AdjAmount)
                .HasPrecision(12, 2)
                .HasColumnName("adj_amount");
            entity.Property(e => e.Code)
                .HasMaxLength(20)
                .HasComment("empty means claim level")
                .HasColumnName("code");
            entity.Property(e => e.CodeType)
                .HasMaxLength(12)
                .HasDefaultValueSql("''")
                .HasColumnName("code_type");
            entity.Property(e => e.Deleted)
                .HasComment("NULL if active, otherwise when voided")
                .HasColumnType("datetime")
                .HasColumnName("deleted");
            entity.Property(e => e.FollowUp)
                .HasMaxLength(1)
                .IsFixedLength()
                .HasColumnName("follow_up");
            entity.Property(e => e.FollowUpNote)
                .HasColumnType("text")
                .HasColumnName("follow_up_note");
            entity.Property(e => e.Memo)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasComment("adjustment reasons go here")
                .HasColumnName("memo");
            entity.Property(e => e.ModifiedTime)
                .HasColumnType("datetime")
                .HasColumnName("modified_time");
            entity.Property(e => e.Modifier)
                .HasMaxLength(12)
                .HasDefaultValueSql("''")
                .HasColumnName("modifier");
            entity.Property(e => e.PayAmount)
                .HasPrecision(12, 2)
                .HasComment("either pay or adj will always be 0")
                .HasColumnName("pay_amount");
            entity.Property(e => e.PayerClaimNumber)
                .HasMaxLength(50)
                .HasComment("CLP07 from the payer 835")
                .HasColumnName("payer_claim_number");
            entity.Property(e => e.PayerType)
                .HasComment("0=pt, 1=ins1, 2=ins2, etc")
                .HasColumnType("int(11)")
                .HasColumnName("payer_type");
            entity.Property(e => e.PostDate)
                .HasComment("Posting date if specified at payment time")
                .HasColumnName("post_date");
            entity.Property(e => e.PostTime)
                .HasColumnType("datetime")
                .HasColumnName("post_time");
            entity.Property(e => e.PostUser)
                .HasComment("references users.id")
                .HasColumnType("int(11)")
                .HasColumnName("post_user");
            entity.Property(e => e.ReasonCode)
                .HasMaxLength(255)
                .HasComment("Use as needed to show the primary payer adjustment reason code")
                .HasColumnName("reason_code");
            entity.Property(e => e.SessionId)
                .HasComment("references ar_session.session_id")
                .HasColumnType("int(10) unsigned")
                .HasColumnName("session_id");
        });

        modelBuilder.Entity<ArSession>(entity =>
        {
            entity.HasKey(e => e.SessionId).HasName("PRIMARY");

            entity.ToTable("ar_session");

            entity.HasIndex(e => e.DepositDate, "deposit_date");

            entity.HasIndex(e => new { e.UserId, e.Closed }, "user_closed");

            entity.Property(e => e.SessionId)
                .HasColumnType("int(10) unsigned")
                .HasColumnName("session_id");
            entity.Property(e => e.AdjustmentCode)
                .HasMaxLength(50)
                .HasColumnName("adjustment_code");
            entity.Property(e => e.CheckDate).HasColumnName("check_date");
            entity.Property(e => e.Closed)
                .HasComment("0=no, 1=yes")
                .HasColumnName("closed");
            entity.Property(e => e.CreatedTime)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("timestamp")
                .HasColumnName("created_time");
            entity.Property(e => e.DepositDate).HasColumnName("deposit_date");
            entity.Property(e => e.Description)
                .HasColumnType("text")
                .HasColumnName("description");
            entity.Property(e => e.GlobalAmount)
                .HasPrecision(12, 2)
                .HasColumnName("global_amount");
            entity.Property(e => e.ModifiedTime)
                .HasColumnType("datetime")
                .HasColumnName("modified_time");
            entity.Property(e => e.PatientId)
                .HasColumnType("bigint(20)")
                .HasColumnName("patient_id");
            entity.Property(e => e.PayTotal)
                .HasPrecision(12, 2)
                .HasColumnName("pay_total");
            entity.Property(e => e.PayerId)
                .HasComment("0=pt else references insurance_companies.id")
                .HasColumnType("int(11)")
                .HasColumnName("payer_id");
            entity.Property(e => e.PaymentMethod)
                .HasMaxLength(25)
                .HasColumnName("payment_method");
            entity.Property(e => e.PaymentType)
                .HasMaxLength(50)
                .HasColumnName("payment_type");
            entity.Property(e => e.PostToDate).HasColumnName("post_to_date");
            entity.Property(e => e.Reference)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasComment("check or EOB number")
                .HasColumnName("reference");
            entity.Property(e => e.UserId)
                .HasComment("references users.id for session owner")
                .HasColumnType("int(11)")
                .HasColumnName("user_id");
        });

        modelBuilder.Entity<AuditDetail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("audit_details");

            entity.HasIndex(e => e.AuditMasterId, "audit_master_id");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.AuditMasterId)
                .HasComment("Id of the audit_master table")
                .HasColumnType("bigint(20)")
                .HasColumnName("audit_master_id");
            entity.Property(e => e.EntryIdentification)
                .HasMaxLength(255)
                .HasDefaultValueSql("'1'")
                .HasComment("Used when multiple entry occurs from the same table.1 means no multiple entry")
                .HasColumnName("entry_identification");
            entity.Property(e => e.FieldName)
                .HasMaxLength(100)
                .HasComment("openemr table's field name")
                .HasColumnName("field_name");
            entity.Property(e => e.FieldValue)
                .HasComment("openemr table's field value")
                .HasColumnName("field_value");
            entity.Property(e => e.TableName)
                .HasMaxLength(100)
                .HasComment("openemr table name")
                .HasColumnName("table_name");
        });

        modelBuilder.Entity<AuditMaster>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("audit_master");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.ApprovalStatus)
                .HasComment("1-Pending,2-Approved,3-Denied,4-Appointment directly updated to calendar table,5-Cancelled appointment")
                .HasColumnType("tinyint(4)")
                .HasColumnName("approval_status");
            entity.Property(e => e.Comments)
                .HasColumnType("text")
                .HasColumnName("comments");
            entity.Property(e => e.CreatedTime)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("timestamp")
                .HasColumnName("created_time");
            entity.Property(e => e.IpAddress)
                .HasMaxLength(100)
                .HasColumnName("ip_address");
            entity.Property(e => e.IsQrdaDocument)
                .HasDefaultValueSql("'0'")
                .HasColumnName("is_qrda_document");
            entity.Property(e => e.IsUnstructuredDocument)
                .HasDefaultValueSql("'0'")
                .HasColumnName("is_unstructured_document");
            entity.Property(e => e.ModifiedTime)
                .HasColumnType("datetime")
                .HasColumnName("modified_time");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.Type)
                .HasComment("1-new patient,2-existing patient,3-change is only in the document,4-Patient upload,5-random key,10-Appointment")
                .HasColumnType("tinyint(4)")
                .HasColumnName("type");
            entity.Property(e => e.UserId)
                .HasComment("The Id of the user who approves or denies")
                .HasColumnType("bigint(20)")
                .HasColumnName("user_id");
        });

        modelBuilder.Entity<AutomaticNotification>(entity =>
        {
            entity.HasKey(e => e.NotificationId).HasName("PRIMARY");

            entity.ToTable("automatic_notification");

            entity.Property(e => e.NotificationId)
                .HasColumnType("int(5)")
                .HasColumnName("notification_id");
            entity.Property(e => e.EmailSender)
                .HasMaxLength(100)
                .HasColumnName("email_sender");
            entity.Property(e => e.EmailSubject)
                .HasMaxLength(100)
                .HasColumnName("email_subject");
            entity.Property(e => e.Message)
                .HasColumnType("text")
                .HasColumnName("message");
            entity.Property(e => e.ProviderName)
                .HasMaxLength(100)
                .HasColumnName("provider_name");
            entity.Property(e => e.SmsGatewayType)
                .HasMaxLength(255)
                .HasColumnName("sms_gateway_type");
            entity.Property(e => e.Type)
                .HasDefaultValueSql("'SMS'")
                .HasColumnType("enum('SMS','Email')")
                .HasColumnName("type");
        });

        modelBuilder.Entity<BackgroundService>(entity =>
        {
            entity.HasKey(e => e.Name).HasName("PRIMARY");

            entity.ToTable("background_services");

            entity.Property(e => e.Name)
                .HasMaxLength(31)
                .HasColumnName("name");
            entity.Property(e => e.Active).HasColumnName("active");
            entity.Property(e => e.ExecuteInterval)
                .HasComment("minimum number of minutes between function calls,0=manual mode")
                .HasColumnType("int(11)")
                .HasColumnName("execute_interval");
            entity.Property(e => e.Function)
                .HasMaxLength(127)
                .HasComment("name of background service function")
                .HasColumnName("function");
            entity.Property(e => e.LockExpiresAt)
                .HasComment("Lease expiration. Compared with NOW() on acquire, so the stored value uses whatever session timezone is in effect (OpenEMR syncs it to gbl_time_zone). Set on acquire, cleared on release. Expired leases are automatically stolen by the next worker.")
                .HasColumnType("datetime")
                .HasColumnName("lock_expires_at");
            entity.Property(e => e.NextRun)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("timestamp")
                .HasColumnName("next_run");
            entity.Property(e => e.RequireOnce)
                .HasMaxLength(255)
                .HasComment("include file (if necessary)")
                .HasColumnName("require_once");
            entity.Property(e => e.Running)
                .IsRequired()
                .HasDefaultValueSql("-1")
                .HasComment("True indicates managed service is busy. Skip this interval")
                .HasColumnName("running");
            entity.Property(e => e.SortOrder)
                .HasDefaultValueSql("'100'")
                .HasComment("lower numbers will be run first")
                .HasColumnType("int(11)")
                .HasColumnName("sort_order");
            entity.Property(e => e.Title)
                .HasMaxLength(127)
                .HasComment("name for reports")
                .HasColumnName("title");
        });

        modelBuilder.Entity<Batchcom>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("batchcom");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.MsgDateSent)
                .HasColumnType("datetime")
                .HasColumnName("msg_date_sent");
            entity.Property(e => e.MsgSubject)
                .HasMaxLength(255)
                .HasColumnName("msg_subject");
            entity.Property(e => e.MsgText)
                .HasColumnType("mediumtext")
                .HasColumnName("msg_text");
            entity.Property(e => e.MsgType)
                .HasMaxLength(60)
                .HasColumnName("msg_type");
            entity.Property(e => e.PatientId)
                .HasColumnType("bigint(20)")
                .HasColumnName("patient_id");
            entity.Property(e => e.SentBy)
                .HasColumnType("bigint(20)")
                .HasColumnName("sent_by");
        });

        modelBuilder.Entity<BenefitEligibility>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("benefit_eligibility");

            entity.Property(e => e.Amount)
                .HasPrecision(5, 2)
                .HasColumnName("amount");
            entity.Property(e => e.BenefitType)
                .HasMaxLength(255)
                .HasColumnName("benefit_type");
            entity.Property(e => e.CoverageLevel)
                .HasMaxLength(255)
                .HasColumnName("coverage_level");
            entity.Property(e => e.CoveragePeriod)
                .HasMaxLength(255)
                .HasColumnName("coverage_period");
            entity.Property(e => e.CoverageType)
                .HasMaxLength(512)
                .HasColumnName("coverage_type");
            entity.Property(e => e.EndDate).HasColumnName("end_date");
            entity.Property(e => e.Message)
                .HasMaxLength(512)
                .HasColumnName("message");
            entity.Property(e => e.NetworkInd)
                .HasMaxLength(2)
                .HasColumnName("network_ind");
            entity.Property(e => e.Percent)
                .HasPrecision(3, 2)
                .HasColumnName("percent");
            entity.Property(e => e.PlanDescription)
                .HasMaxLength(255)
                .HasColumnName("plan_description");
            entity.Property(e => e.PlanType)
                .HasMaxLength(255)
                .HasColumnName("plan_type");
            entity.Property(e => e.ResponseCreateDate).HasColumnName("response_create_date");
            entity.Property(e => e.ResponseId)
                .HasColumnType("bigint(20)")
                .HasColumnName("response_id");
            entity.Property(e => e.ResponseModifyDate).HasColumnName("response_modify_date");
            entity.Property(e => e.ResponseStatus)
                .HasDefaultValueSql("'A'")
                .HasColumnType("enum('A','D')")
                .HasColumnName("response_status");
            entity.Property(e => e.StartDate).HasColumnName("start_date");
            entity.Property(e => e.Type)
                .HasMaxLength(4)
                .HasColumnName("type");
            entity.Property(e => e.VerificationId)
                .HasColumnType("bigint(20)")
                .HasColumnName("verification_id");
        });

        modelBuilder.Entity<Billing>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("billing");

            entity.HasIndex(e => e.Pid, "pid");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.Activity).HasColumnName("activity");
            entity.Property(e => e.Authorized).HasColumnName("authorized");
            entity.Property(e => e.BillDate)
                .HasColumnType("datetime")
                .HasColumnName("bill_date");
            entity.Property(e => e.BillProcess)
                .HasColumnType("tinyint(2)")
                .HasColumnName("bill_process");
            entity.Property(e => e.Billed).HasColumnName("billed");
            entity.Property(e => e.Chargecat)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("Charge category or customer")
                .HasColumnName("chargecat");
            entity.Property(e => e.Code)
                .HasMaxLength(20)
                .HasColumnName("code");
            entity.Property(e => e.CodeText).HasColumnName("code_text");
            entity.Property(e => e.CodeType)
                .HasMaxLength(15)
                .HasColumnName("code_type");
            entity.Property(e => e.Date)
                .HasColumnType("datetime")
                .HasColumnName("date");
            entity.Property(e => e.Encounter)
                .HasColumnType("int(11)")
                .HasColumnName("encounter");
            entity.Property(e => e.ExternalId)
                .HasMaxLength(20)
                .HasColumnName("external_id");
            entity.Property(e => e.Fee)
                .HasPrecision(12, 2)
                .HasColumnName("fee");
            entity.Property(e => e.Groupname)
                .HasMaxLength(255)
                .HasColumnName("groupname");
            entity.Property(e => e.Justify)
                .HasMaxLength(255)
                .HasColumnName("justify");
            entity.Property(e => e.Modifier)
                .HasMaxLength(12)
                .HasColumnName("modifier");
            entity.Property(e => e.NdcInfo)
                .HasMaxLength(255)
                .HasColumnName("ndc_info");
            entity.Property(e => e.Notecodes)
                .HasMaxLength(25)
                .HasDefaultValueSql("''")
                .HasColumnName("notecodes");
            entity.Property(e => e.PayerId)
                .HasColumnType("int(11)")
                .HasColumnName("payer_id");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.Pricelevel)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasColumnName("pricelevel");
            entity.Property(e => e.ProcessDate)
                .HasColumnType("datetime")
                .HasColumnName("process_date");
            entity.Property(e => e.ProcessFile)
                .HasMaxLength(255)
                .HasColumnName("process_file");
            entity.Property(e => e.ProviderId)
                .HasColumnType("int(11)")
                .HasColumnName("provider_id");
            entity.Property(e => e.RevenueCode)
                .HasMaxLength(6)
                .HasDefaultValueSql("''")
                .HasComment("Item revenue code")
                .HasColumnName("revenue_code");
            entity.Property(e => e.Target)
                .HasMaxLength(30)
                .HasColumnName("target");
            entity.Property(e => e.Units)
                .HasColumnType("int(11)")
                .HasColumnName("units");
            entity.Property(e => e.User)
                .HasColumnType("int(11)")
                .HasColumnName("user");
            entity.Property(e => e.X12PartnerId)
                .HasColumnType("int(11)")
                .HasColumnName("x12_partner_id");
        });

        modelBuilder.Entity<CalendarExternal>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("calendar_external");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.Date).HasColumnName("date");
            entity.Property(e => e.Description)
                .HasMaxLength(45)
                .HasColumnName("description");
            entity.Property(e => e.Source)
                .HasMaxLength(45)
                .HasColumnName("source");
        });

        modelBuilder.Entity<CareTeam>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("care_teams");

            entity.HasIndex(e => e.Uuid, "uuid").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.CreatedBy)
                .HasComment("fk to users.id for user who created this record")
                .HasColumnType("bigint(20)")
                .HasColumnName("created_by");
            entity.Property(e => e.DateCreated)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("date_created");
            entity.Property(e => e.DateUpdated)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("date_updated");
            entity.Property(e => e.Note)
                .HasColumnType("text")
                .HasColumnName("note");
            entity.Property(e => e.Pid)
                .HasComment("fk to patient_data.pid")
                .HasColumnType("int(11)")
                .HasColumnName("pid");
            entity.Property(e => e.Status)
                .HasMaxLength(100)
                .HasDefaultValueSql("'active'")
                .HasComment("fk to list_options.option_id where list_id=Care_Team_Status")
                .HasColumnName("status");
            entity.Property(e => e.TeamName)
                .HasMaxLength(255)
                .HasColumnName("team_name");
            entity.Property(e => e.UpdatedBy)
                .HasComment("fk to users.id for user who last updated this record")
                .HasColumnType("bigint(20)")
                .HasColumnName("updated_by");
            entity.Property(e => e.Uuid)
                .HasMaxLength(16)
                .IsFixedLength()
                .HasColumnName("uuid");
        });

        modelBuilder.Entity<CareTeamMember>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("care_team_member", tb => tb.HasComment("Stores members of a care team for a patient"));

            entity.HasIndex(e => new { e.CareTeamId, e.UserId, e.FacilityId, e.ContactId }, "care_team_member_unique").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.CareTeamId)
                .HasColumnType("int(11)")
                .HasColumnName("care_team_id");
            entity.Property(e => e.ContactId)
                .HasComment("fk to contact.id which represents a contact person not in users or facility table")
                .HasColumnType("bigint(20)")
                .HasColumnName("contact_id");
            entity.Property(e => e.CreatedBy)
                .HasComment("fk to users.id and is the user that added this team member")
                .HasColumnType("bigint(20)")
                .HasColumnName("created_by");
            entity.Property(e => e.DateCreated)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("date_created");
            entity.Property(e => e.DateUpdated)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("date_updated");
            entity.Property(e => e.FacilityId)
                .HasComment("fk to facility.id represents an organization or location")
                .HasColumnType("bigint(20)")
                .HasColumnName("facility_id");
            entity.Property(e => e.Note)
                .HasColumnType("text")
                .HasColumnName("note");
            entity.Property(e => e.ProviderSince).HasColumnName("provider_since");
            entity.Property(e => e.Role)
                .HasMaxLength(50)
                .HasComment("fk to list_options.option_id WHERE list_id=care_team_roles")
                .HasColumnName("role");
            entity.Property(e => e.Status)
                .HasMaxLength(100)
                .HasDefaultValueSql("'active'")
                .HasComment("fk to list_options.option_id where list_id=Care_Team_Status")
                .HasColumnName("status");
            entity.Property(e => e.UpdatedBy)
                .HasComment("fk to users.id and is the user that last updated this team member")
                .HasColumnType("bigint(20)")
                .HasColumnName("updated_by");
            entity.Property(e => e.UserId)
                .HasComment("fk to users.id represents a provider or staff member")
                .HasColumnType("bigint(20)")
                .HasColumnName("user_id");
        });

        modelBuilder.Entity<CategoriesSeq>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("categories_seq");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnType("int(11)")
                .HasColumnName("id");
        });

        modelBuilder.Entity<CategoriesToDocument>(entity =>
        {
            entity.HasKey(e => new { e.CategoryId, e.DocumentId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("categories_to_documents");

            entity.Property(e => e.CategoryId)
                .HasColumnType("int(11)")
                .HasColumnName("category_id");
            entity.Property(e => e.DocumentId)
                .HasColumnType("int(11)")
                .HasColumnName("document_id");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("categories");

            entity.HasIndex(e => new { e.Lft, e.Rght }, "lft");

            entity.HasIndex(e => e.Parent, "parent");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.AcoSpec)
                .HasMaxLength(63)
                .HasDefaultValueSql("'patients|docs'")
                .HasColumnName("aco_spec");
            entity.Property(e => e.Codes)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasComment("Category codes for documents stored in this category")
                .HasColumnName("codes");
            entity.Property(e => e.Lft)
                .HasColumnType("int(11)")
                .HasColumnName("lft");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.Parent)
                .HasColumnType("int(11)")
                .HasColumnName("parent");
            entity.Property(e => e.Rght)
                .HasColumnType("int(11)")
                .HasColumnName("rght");
            entity.Property(e => e.Value)
                .HasMaxLength(255)
                .HasColumnName("value");
        });

        modelBuilder.Entity<CcdaComponent>(entity =>
        {
            entity.HasKey(e => e.CcdaComponentsId).HasName("PRIMARY");

            entity.ToTable("ccda_components");

            entity.Property(e => e.CcdaComponentsId)
                .HasColumnType("int(11)")
                .HasColumnName("ccda_components_id");
            entity.Property(e => e.CcdaComponentsField)
                .HasMaxLength(100)
                .HasColumnName("ccda_components_field");
            entity.Property(e => e.CcdaComponentsName)
                .HasMaxLength(100)
                .HasColumnName("ccda_components_name");
            entity.Property(e => e.CcdaType)
                .HasComment("0=>sections,1=>components")
                .HasColumnType("int(11)")
                .HasColumnName("ccda_type");
        });

        modelBuilder.Entity<CcdaFieldMapping>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("ccda_field_mapping");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.CcdaField)
                .HasMaxLength(100)
                .HasColumnName("ccda_field");
            entity.Property(e => e.TableId)
                .HasColumnType("int(11)")
                .HasColumnName("table_id");
        });

        modelBuilder.Entity<CcdaSection>(entity =>
        {
            entity.HasKey(e => e.CcdaSectionsId).HasName("PRIMARY");

            entity.ToTable("ccda_sections");

            entity.Property(e => e.CcdaSectionsId)
                .HasColumnType("int(11)")
                .HasColumnName("ccda_sections_id");
            entity.Property(e => e.CcdaComponentsId)
                .HasColumnType("int(11)")
                .HasColumnName("ccda_components_id");
            entity.Property(e => e.CcdaSectionsField)
                .HasMaxLength(100)
                .HasColumnName("ccda_sections_field");
            entity.Property(e => e.CcdaSectionsName)
                .HasMaxLength(100)
                .HasColumnName("ccda_sections_name");
            entity.Property(e => e.CcdaSectionsReqMapping)
                .HasDefaultValueSql("'1'")
                .HasColumnType("tinyint(4)")
                .HasColumnName("ccda_sections_req_mapping");
        });

        modelBuilder.Entity<CcdaTableMapping>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("ccda_table_mapping");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.CcdaComponent)
                .HasMaxLength(100)
                .HasColumnName("ccda_component");
            entity.Property(e => e.CcdaComponentSection)
                .HasMaxLength(100)
                .HasColumnName("ccda_component_section");
            entity.Property(e => e.Deleted)
                .HasColumnType("tinyint(4)")
                .HasColumnName("deleted");
            entity.Property(e => e.FormDir)
                .HasMaxLength(100)
                .HasColumnName("form_dir");
            entity.Property(e => e.FormTable)
                .HasMaxLength(100)
                .HasColumnName("form_table");
            entity.Property(e => e.FormType)
                .HasColumnType("smallint(6)")
                .HasColumnName("form_type");
            entity.Property(e => e.Timestamp)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("timestamp")
                .HasColumnName("timestamp");
            entity.Property(e => e.UserId)
                .HasColumnType("int(11)")
                .HasColumnName("user_id");
        });

        modelBuilder.Entity<Ccdum>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("ccda");

            entity.HasIndex(e => new { e.Pid, e.Encounter, e.Time }, "unique_key").IsUnique();

            entity.HasIndex(e => e.Uuid, "uuid").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.CcdaData).HasColumnName("ccda_data");
            entity.Property(e => e.CouchDocid)
                .HasMaxLength(100)
                .HasColumnName("couch_docid");
            entity.Property(e => e.CouchRevid)
                .HasMaxLength(100)
                .HasColumnName("couch_revid");
            entity.Property(e => e.EmrTransfer)
                .HasColumnType("tinyint(4)")
                .HasColumnName("emr_transfer");
            entity.Property(e => e.Encounter)
                .HasColumnType("bigint(20)")
                .HasColumnName("encounter");
            entity.Property(e => e.Encrypted)
                .HasComment("0->No,1->Yes")
                .HasColumnType("tinyint(4)")
                .HasColumnName("encrypted");
            entity.Property(e => e.Hash)
                .HasMaxLength(255)
                .HasColumnName("hash");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.Status)
                .HasColumnType("smallint(6)")
                .HasColumnName("status");
            entity.Property(e => e.Time)
                .HasMaxLength(50)
                .HasColumnName("time");
            entity.Property(e => e.TransactionId)
                .HasComment("fk to transaction referral record")
                .HasColumnType("bigint(20)")
                .HasColumnName("transaction_id");
            entity.Property(e => e.Transfer)
                .HasColumnType("tinyint(4)")
                .HasColumnName("transfer");
            entity.Property(e => e.UpdatedDate)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("timestamp")
                .HasColumnName("updated_date");
            entity.Property(e => e.UserId)
                .HasMaxLength(50)
                .HasColumnName("user_id");
            entity.Property(e => e.Uuid)
                .HasMaxLength(16)
                .IsFixedLength()
                .HasColumnName("uuid");
            entity.Property(e => e.View)
                .HasColumnType("tinyint(4)")
                .HasColumnName("view");
        });

        modelBuilder.Entity<ChartTracker>(entity =>
        {
            entity.HasKey(e => new { e.CtPid, e.CtWhen })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("chart_tracker");

            entity.Property(e => e.CtPid)
                .HasColumnType("int(11)")
                .HasColumnName("ct_pid");
            entity.Property(e => e.CtWhen)
                .HasColumnType("datetime")
                .HasColumnName("ct_when");
            entity.Property(e => e.CtLocation)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasColumnName("ct_location");
            entity.Property(e => e.CtUserid)
                .HasColumnType("bigint(20)")
                .HasColumnName("ct_userid");
        });

        modelBuilder.Entity<Claim>(entity =>
        {
            entity.HasKey(e => new { e.PatientId, e.EncounterId, e.Version })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0, 0 });

            entity.ToTable("claims");

            entity.Property(e => e.PatientId)
                .HasColumnType("bigint(20)")
                .HasColumnName("patient_id");
            entity.Property(e => e.EncounterId)
                .HasColumnType("int(11)")
                .HasColumnName("encounter_id");
            entity.Property(e => e.Version)
                .HasComment("Claim version, incremented in code")
                .HasColumnType("int(10) unsigned")
                .HasColumnName("version");
            entity.Property(e => e.BillProcess)
                .HasColumnType("tinyint(2)")
                .HasColumnName("bill_process");
            entity.Property(e => e.BillTime)
                .HasColumnType("datetime")
                .HasColumnName("bill_time");
            entity.Property(e => e.PayerId)
                .HasColumnType("int(11)")
                .HasColumnName("payer_id");
            entity.Property(e => e.PayerType)
                .HasColumnType("tinyint(4)")
                .HasColumnName("payer_type");
            entity.Property(e => e.ProcessFile)
                .HasMaxLength(255)
                .HasColumnName("process_file");
            entity.Property(e => e.ProcessTime)
                .HasColumnType("datetime")
                .HasColumnName("process_time");
            entity.Property(e => e.Status)
                .HasColumnType("tinyint(2)")
                .HasColumnName("status");
            entity.Property(e => e.SubmittedClaim)
                .HasComment("This claims form claim data")
                .HasColumnType("text")
                .HasColumnName("submitted_claim");
            entity.Property(e => e.Target)
                .HasMaxLength(30)
                .HasColumnName("target");
            entity.Property(e => e.X12PartnerId)
                .HasColumnType("int(11)")
                .HasColumnName("x12_partner_id");
        });

        modelBuilder.Entity<ClinicalNotesDocument>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("clinical_notes_documents", tb => tb.HasComment("Links clinical notes to patient documents"));

            entity.HasIndex(e => e.ClinicalNoteId, "idx_clinical_note_id");

            entity.HasIndex(e => e.CreatedAt, "idx_created_at");

            entity.HasIndex(e => e.DocumentId, "idx_document_id");

            entity.HasIndex(e => new { e.ClinicalNoteId, e.DocumentId }, "unique_note_document").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.ClinicalNoteId)
                .HasComment("Foreign key to form_clinical_notes.id")
                .HasColumnType("bigint(20)")
                .HasColumnName("clinical_note_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("current_timestamp()")
                .HasComment("When the link was created")
                .HasColumnType("timestamp")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(255)
                .HasComment("Username who created the link")
                .HasColumnName("created_by");
            entity.Property(e => e.DocumentId)
                .HasComment("Foreign key to documents.id")
                .HasColumnType("bigint(20)")
                .HasColumnName("document_id");
        });

        modelBuilder.Entity<ClinicalNotesProcedureResult>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("clinical_notes_procedure_results", tb => tb.HasComment("Links clinical notes to procedure results/lab values"));

            entity.HasIndex(e => e.ClinicalNoteId, "idx_clinical_note_id");

            entity.HasIndex(e => e.CreatedAt, "idx_created_at");

            entity.HasIndex(e => e.ProcedureResultId, "idx_procedure_result_id");

            entity.HasIndex(e => new { e.ClinicalNoteId, e.ProcedureResultId }, "unique_note_result").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.ClinicalNoteId)
                .HasComment("Foreign key to form_clinical_notes.id")
                .HasColumnType("bigint(20)")
                .HasColumnName("clinical_note_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("current_timestamp()")
                .HasComment("When the link was created")
                .HasColumnType("timestamp")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(255)
                .HasComment("Username who created the link")
                .HasColumnName("created_by");
            entity.Property(e => e.ProcedureResultId)
                .HasComment("Foreign key to procedure_result.procedure_result_id")
                .HasColumnType("bigint(20)")
                .HasColumnName("procedure_result_id");
        });

        modelBuilder.Entity<ClinicalPlan>(entity =>
        {
            entity.HasKey(e => new { e.Id, e.Pid })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("clinical_plans");

            entity.Property(e => e.Id)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("Unique and maps to list_options list clinical_plans")
                .HasColumnName("id");
            entity.Property(e => e.Pid)
                .HasComment("0 is default for all patients, while > 0 is id from patient_data table")
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.Cqm2011Flag)
                .HasComment("2011 Clinical Quality Measure flag (unable to customize per patient)")
                .HasColumnName("cqm_2011_flag");
            entity.Property(e => e.Cqm2014Flag)
                .HasComment("2014 Clinical Quality Measure flag (unable to customize per patient)")
                .HasColumnName("cqm_2014_flag");
            entity.Property(e => e.CqmFlag)
                .HasComment("Clinical Quality Measure flag (unable to customize per patient)")
                .HasColumnName("cqm_flag");
            entity.Property(e => e.CqmMeasureGroup)
                .HasMaxLength(10)
                .HasDefaultValueSql("''")
                .HasComment("Clinical Quality Measure Group Identifier")
                .HasColumnName("cqm_measure_group");
            entity.Property(e => e.NormalFlag)
                .HasComment("Normal Activation Flag")
                .HasColumnName("normal_flag");
        });

        modelBuilder.Entity<ClinicalPlansRule>(entity =>
        {
            entity.HasKey(e => new { e.PlanId, e.RuleId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("clinical_plans_rules");

            entity.Property(e => e.PlanId)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("Unique and maps to list_options list clinical_plans")
                .HasColumnName("plan_id");
            entity.Property(e => e.RuleId)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("Unique and maps to list_options list clinical_rules")
                .HasColumnName("rule_id");
        });

        modelBuilder.Entity<ClinicalRule>(entity =>
        {
            entity.HasKey(e => new { e.Id, e.Pid })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("clinical_rules");

            entity.Property(e => e.Id)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("Unique and maps to list_options list clinical_rules")
                .HasColumnName("id");
            entity.Property(e => e.Pid)
                .HasComment("0 is default for all patients, while > 0 is id from patient_data table")
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.AccessControl)
                .HasMaxLength(255)
                .HasDefaultValueSql("'patients:med'")
                .HasComment("ACO link for access control")
                .HasColumnName("access_control");
            entity.Property(e => e.ActiveAlertFlag)
                .HasComment("Active Alert Widget Module flag - note not yet utilized")
                .HasColumnName("active_alert_flag");
            entity.Property(e => e.Amc2011Flag)
                .HasComment("2011 Automated Measure Calculation flag for (unable to customize per patient)")
                .HasColumnName("amc_2011_flag");
            entity.Property(e => e.Amc2014Flag)
                .HasComment("2014 Automated Measure Calculation flag for (unable to customize per patient)")
                .HasColumnName("amc_2014_flag");
            entity.Property(e => e.Amc2014Stage1Flag)
                .HasComment("2014 Stage 1 - Automated Measure Calculation flag for (unable to customize per patient)")
                .HasColumnName("amc_2014_stage1_flag");
            entity.Property(e => e.Amc2014Stage2Flag)
                .HasComment("2014 Stage 2 - Automated Measure Calculation flag for (unable to customize per patient)")
                .HasColumnName("amc_2014_stage2_flag");
            entity.Property(e => e.Amc2015Flag)
                .HasComment("2015 Automated Measure Calculation flag for (unable to customize per patient)")
                .HasColumnName("amc_2015_flag");
            entity.Property(e => e.AmcCode)
                .HasMaxLength(10)
                .HasDefaultValueSql("''")
                .HasComment("Automated Measure Calculation identifier (MU rule)")
                .HasColumnName("amc_code");
            entity.Property(e => e.AmcCode2014)
                .HasMaxLength(30)
                .HasDefaultValueSql("''")
                .HasComment("Automated Measure Calculation 2014 identifier (MU rule)")
                .HasColumnName("amc_code_2014");
            entity.Property(e => e.AmcCode2015)
                .HasMaxLength(30)
                .HasDefaultValueSql("''")
                .HasComment("Automated Measure Calculation 2014 identifier (MU rule)")
                .HasColumnName("amc_code_2015");
            entity.Property(e => e.AmcFlag)
                .HasComment("Automated Measure Calculation flag (unable to customize per patient)")
                .HasColumnName("amc_flag");
            entity.Property(e => e.BibliographicCitation)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("bibliographic_citation");
            entity.Property(e => e.Cqm2011Flag)
                .HasComment("2011 Clinical Quality Measure flag (unable to customize per patient)")
                .HasColumnName("cqm_2011_flag");
            entity.Property(e => e.Cqm2014Flag)
                .HasComment("2014 Clinical Quality Measure flag (unable to customize per patient)")
                .HasColumnName("cqm_2014_flag");
            entity.Property(e => e.CqmFlag)
                .HasComment("Clinical Quality Measure flag (unable to customize per patient)")
                .HasColumnName("cqm_flag");
            entity.Property(e => e.CqmNqfCode)
                .HasMaxLength(10)
                .HasDefaultValueSql("''")
                .HasComment("Clinical Quality Measure NQF identifier")
                .HasColumnName("cqm_nqf_code");
            entity.Property(e => e.CqmPqriCode)
                .HasMaxLength(10)
                .HasDefaultValueSql("''")
                .HasComment("Clinical Quality Measure PQRI identifier")
                .HasColumnName("cqm_pqri_code");
            entity.Property(e => e.Developer)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasComment("Clinical Rule Developer")
                .HasColumnName("developer");
            entity.Property(e => e.FundingSource)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasComment("Clinical Rule Funding Source")
                .HasColumnName("funding_source");
            entity.Property(e => e.LinkedReferentialCds)
                .HasMaxLength(50)
                .HasDefaultValueSql("''")
                .HasColumnName("linked_referential_cds");
            entity.Property(e => e.PassiveAlertFlag)
                .HasComment("Passive Alert Widget Module flag")
                .HasColumnName("passive_alert_flag");
            entity.Property(e => e.PatientDobUsage)
                .HasComment("Description of how patient DOB is used by this rule")
                .HasColumnType("text")
                .HasColumnName("patient_dob_usage");
            entity.Property(e => e.PatientEthnicityUsage)
                .HasComment("Description of how patient ethnicity is used by this rule")
                .HasColumnType("text")
                .HasColumnName("patient_ethnicity_usage");
            entity.Property(e => e.PatientGenderIdentityUsage)
                .HasComment("Description of how patient gender identity information is used by this rule")
                .HasColumnType("text")
                .HasColumnName("patient_gender_identity_usage");
            entity.Property(e => e.PatientHealthStatusUsage)
                .HasComment("Description of how patient health status assessments are used by this rule")
                .HasColumnType("text")
                .HasColumnName("patient_health_status_usage");
            entity.Property(e => e.PatientLanguageUsage)
                .HasComment("Description of how patient language information is used by this rule")
                .HasColumnType("text")
                .HasColumnName("patient_language_usage");
            entity.Property(e => e.PatientRaceUsage)
                .HasComment("Description of how patient race information is used by this rule")
                .HasColumnType("text")
                .HasColumnName("patient_race_usage");
            entity.Property(e => e.PatientReminderFlag)
                .HasComment("Clinical Reminder Module flag")
                .HasColumnName("patient_reminder_flag");
            entity.Property(e => e.PatientSexUsage)
                .HasComment("Description of how patient birth sex information is used by this rule")
                .HasColumnType("text")
                .HasColumnName("patient_sex_usage");
            entity.Property(e => e.PatientSexualOrientationUsage)
                .HasComment("Description of how patient sexual orientation is used by this rule")
                .HasColumnType("text")
                .HasColumnName("patient_sexual_orientation_usage");
            entity.Property(e => e.PatientSodhUsage)
                .HasComment("Description of how patient social determinants of health are used by this rule")
                .HasColumnType("text")
                .HasColumnName("patient_sodh_usage");
            entity.Property(e => e.ReleaseVersion)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasComment("Clinical Rule Release Version")
                .HasColumnName("release_version");
            entity.Property(e => e.WebReference)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasComment("Clinical Rule Web Reference")
                .HasColumnName("web_reference");
        });

        modelBuilder.Entity<ClinicalRulesLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("clinical_rules_log");

            entity.HasIndex(e => e.Category, "category");

            entity.HasIndex(e => e.Pid, "pid");

            entity.HasIndex(e => e.Uid, "uid");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Category)
                .HasDefaultValueSql("''")
                .HasComment("An example category is clinical_reminder_widget")
                .HasColumnName("category");
            entity.Property(e => e.Date)
                .HasColumnType("datetime")
                .HasColumnName("date");
            entity.Property(e => e.FacilityId)
                .HasDefaultValueSql("'0'")
                .HasComment("facility where the rule was executed, 0 if unknown")
                .HasColumnType("int(11)")
                .HasColumnName("facility_id");
            entity.Property(e => e.NewValue)
                .HasColumnType("text")
                .HasColumnName("new_value");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.Uid)
                .HasColumnType("bigint(20)")
                .HasColumnName("uid");
            entity.Property(e => e.Value)
                .HasColumnType("text")
                .HasColumnName("value");
        });

        modelBuilder.Entity<Code>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("codes");

            entity.HasIndex(e => e.Code1, "code");

            entity.HasIndex(e => e.CodeType, "code_type");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.Active)
                .HasDefaultValueSql("'1'")
                .HasComment("0 = inactive, 1 = active")
                .HasColumnName("active");
            entity.Property(e => e.Code1)
                .HasMaxLength(25)
                .HasDefaultValueSql("''")
                .HasColumnName("code");
            entity.Property(e => e.CodeText)
                .HasColumnType("text")
                .HasColumnName("code_text");
            entity.Property(e => e.CodeTextShort)
                .HasColumnType("text")
                .HasColumnName("code_text_short");
            entity.Property(e => e.CodeType)
                .HasColumnType("smallint(6)")
                .HasColumnName("code_type");
            entity.Property(e => e.CypFactor)
                .HasComment("quantity representing a years supply")
                .HasColumnName("cyp_factor");
            entity.Property(e => e.Fee)
                .HasPrecision(12, 2)
                .HasColumnName("fee");
            entity.Property(e => e.FinancialReporting)
                .HasDefaultValueSql("'0'")
                .HasComment("0 = negative, 1 = considered important code in financial reporting")
                .HasColumnName("financial_reporting");
            entity.Property(e => e.Modifier)
                .HasMaxLength(12)
                .HasDefaultValueSql("''")
                .HasColumnName("modifier");
            entity.Property(e => e.RelatedCode)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("related_code");
            entity.Property(e => e.Reportable)
                .HasDefaultValueSql("'0'")
                .HasComment("0 = non-reportable, 1 = reportable")
                .HasColumnName("reportable");
            entity.Property(e => e.RevenueCode)
                .HasMaxLength(6)
                .HasDefaultValueSql("''")
                .HasComment("Item revenue code")
                .HasColumnName("revenue_code");
            entity.Property(e => e.Superbill)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasColumnName("superbill");
            entity.Property(e => e.Taxrates)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("taxrates");
            entity.Property(e => e.Units)
                .HasColumnType("int(11)")
                .HasColumnName("units");
        });

        modelBuilder.Entity<CodeType>(entity =>
        {
            entity.HasKey(e => e.CtKey).HasName("PRIMARY");

            entity.ToTable("code_types");

            entity.HasIndex(e => e.CtId, "ct_id").IsUnique();

            entity.Property(e => e.CtKey)
                .HasMaxLength(15)
                .HasComment("short alphanumeric name")
                .HasColumnName("ct_key");
            entity.Property(e => e.CtActive)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasComment("1 if this is active")
                .HasColumnName("ct_active");
            entity.Property(e => e.CtClaim)
                .HasComment("1 if this is used in claims")
                .HasColumnName("ct_claim");
            entity.Property(e => e.CtDiag)
                .HasComment("1 if this is a diagnosis type")
                .HasColumnName("ct_diag");
            entity.Property(e => e.CtDrug)
                .HasComment("1 if this code type is used as a medication")
                .HasColumnName("ct_drug");
            entity.Property(e => e.CtExternal)
                .HasComment("0 if stored codes in codes tables, 1 or greater if codes stored in external tables")
                .HasColumnName("ct_external");
            entity.Property(e => e.CtFee)
                .HasComment("1 if fees are used")
                .HasColumnName("ct_fee");
            entity.Property(e => e.CtId)
                .HasComment("numeric identifier")
                .HasColumnType("int(11)")
                .HasColumnName("ct_id");
            entity.Property(e => e.CtJust)
                .HasMaxLength(15)
                .HasDefaultValueSql("''")
                .HasComment("ct_key of justify type, if any")
                .HasColumnName("ct_just");
            entity.Property(e => e.CtLabel)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("label of this code type")
                .HasColumnName("ct_label");
            entity.Property(e => e.CtMask)
                .HasMaxLength(9)
                .HasDefaultValueSql("''")
                .HasComment("formatting mask for code values")
                .HasColumnName("ct_mask");
            entity.Property(e => e.CtMod)
                .HasComment("length of modifier field")
                .HasColumnType("int(11)")
                .HasColumnName("ct_mod");
            entity.Property(e => e.CtNofs)
                .HasComment("1 if to be hidden in the fee sheet")
                .HasColumnName("ct_nofs");
            entity.Property(e => e.CtProblem)
                .HasComment("1 if this code type is used as a medical problem")
                .HasColumnName("ct_problem");
            entity.Property(e => e.CtProc)
                .HasComment("1 if this is a procedure type")
                .HasColumnName("ct_proc");
            entity.Property(e => e.CtRel)
                .HasComment("1 if can relate to other code types")
                .HasColumnName("ct_rel");
            entity.Property(e => e.CtSeq)
                .HasComment("sort order")
                .HasColumnType("int(11)")
                .HasColumnName("ct_seq");
            entity.Property(e => e.CtTerm)
                .HasComment("1 if this is a clinical term")
                .HasColumnName("ct_term");
        });

        modelBuilder.Entity<CodesHistory>(entity =>
        {
            entity.HasKey(e => e.LogId).HasName("PRIMARY");

            entity.ToTable("codes_history");

            entity.Property(e => e.LogId)
                .HasColumnType("bigint(20)")
                .HasColumnName("log_id");
            entity.Property(e => e.ActionType)
                .HasMaxLength(25)
                .HasColumnName("action_type");
            entity.Property(e => e.Active).HasColumnName("active");
            entity.Property(e => e.Category)
                .HasMaxLength(255)
                .HasColumnName("category");
            entity.Property(e => e.Code)
                .HasMaxLength(25)
                .HasColumnName("code");
            entity.Property(e => e.CodeText)
                .HasColumnType("text")
                .HasColumnName("code_text");
            entity.Property(e => e.CodeTextShort)
                .HasColumnType("text")
                .HasColumnName("code_text_short");
            entity.Property(e => e.CodeTypeName)
                .HasMaxLength(255)
                .HasColumnName("code_type_name");
            entity.Property(e => e.Date)
                .HasColumnType("datetime")
                .HasColumnName("date");
            entity.Property(e => e.DiagnosisReporting).HasColumnName("diagnosis_reporting");
            entity.Property(e => e.FinancialReporting).HasColumnName("financial_reporting");
            entity.Property(e => e.Modifier)
                .HasMaxLength(12)
                .HasColumnName("modifier");
            entity.Property(e => e.Prices)
                .HasColumnType("text")
                .HasColumnName("prices");
            entity.Property(e => e.UpdateBy)
                .HasMaxLength(255)
                .HasColumnName("update_by");
        });

        modelBuilder.Entity<Contact>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("contact");

            entity.HasIndex(e => e.ForeignId, "foreign_id");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.ForeignId)
                .HasColumnType("bigint(20)")
                .HasColumnName("foreign_id");
            entity.Property(e => e.ForeignTableName)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("foreign_table_name");
        });

        modelBuilder.Entity<ContactAddress>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("contact_address");

            entity.HasIndex(e => e.AddressId, "address_id");

            entity.HasIndex(e => new { e.ContactId, e.AddressId }, "contact_address_idx");

            entity.HasIndex(e => e.ContactId, "contact_id");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.AddressId)
                .HasColumnType("bigint(20)")
                .HasColumnName("address_id");
            entity.Property(e => e.ContactId)
                .HasColumnType("bigint(20)")
                .HasColumnName("contact_id");
            entity.Property(e => e.CreatedBy)
                .HasComment("users.id")
                .HasColumnType("bigint(20)")
                .HasColumnName("created_by");
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("created_date");
            entity.Property(e => e.InactivatedReason)
                .HasMaxLength(45)
                .HasComment("[Values: Moved, Mail Returned, etc]")
                .HasColumnName("inactivated_reason");
            entity.Property(e => e.IsPrimary)
                .HasMaxLength(1)
                .IsFixedLength()
                .HasComment("Y=yes,N=no")
                .HasColumnName("is_primary");
            entity.Property(e => e.Notes)
                .HasColumnType("tinytext")
                .HasColumnName("notes");
            entity.Property(e => e.PeriodEnd)
                .HasComment("Date the address became deactivated")
                .HasColumnType("datetime")
                .HasColumnName("period_end");
            entity.Property(e => e.PeriodStart)
                .HasComment("Date the address became active")
                .HasColumnType("datetime")
                .HasColumnName("period_start");
            entity.Property(e => e.Priority)
                .HasColumnType("int(11)")
                .HasColumnName("priority");
            entity.Property(e => e.Status)
                .HasMaxLength(1)
                .IsFixedLength()
                .HasComment("A=active,I=inactive")
                .HasColumnName("status");
            entity.Property(e => e.Type)
                .HasMaxLength(255)
                .HasComment("FK to list_options.option_id for list_id address-types")
                .HasColumnName("type");
            entity.Property(e => e.UpdatedBy)
                .HasComment("users.id")
                .HasColumnType("bigint(20)")
                .HasColumnName("updated_by");
            entity.Property(e => e.UpdatedDate)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("updated_date");
            entity.Property(e => e.Use)
                .HasMaxLength(255)
                .HasComment("FK to list_options.option_id for list_id address-uses")
                .HasColumnName("use");
        });

        modelBuilder.Entity<ContactRelation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("contact_relation");

            entity.HasIndex(e => e.ContactId, "contact_id");

            entity.HasIndex(e => new { e.TargetTable, e.TargetId }, "idx_contact_target_table");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Active)
                .HasDefaultValueSql("'1'")
                .HasColumnName("active");
            entity.Property(e => e.CanMakeMedicalDecisions)
                .HasDefaultValueSql("'0'")
                .HasColumnName("can_make_medical_decisions");
            entity.Property(e => e.CanReceiveMedicalInfo)
                .HasDefaultValueSql("'0'")
                .HasColumnName("can_receive_medical_info");
            entity.Property(e => e.ContactId)
                .HasColumnType("bigint(20)")
                .HasColumnName("contact_id");
            entity.Property(e => e.ContactPriority)
                .HasDefaultValueSql("'1'")
                .HasComment("1=highest priority")
                .HasColumnType("int(11)")
                .HasColumnName("contact_priority");
            entity.Property(e => e.CreatedBy)
                .HasComment("users.id")
                .HasColumnType("bigint(20)")
                .HasColumnName("created_by");
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("created_date");
            entity.Property(e => e.EndDate)
                .HasColumnType("datetime")
                .HasColumnName("end_date");
            entity.Property(e => e.IsEmergencyContact)
                .HasDefaultValueSql("'0'")
                .HasColumnName("is_emergency_contact");
            entity.Property(e => e.IsPrimaryContact)
                .HasDefaultValueSql("'0'")
                .HasColumnName("is_primary_contact");
            entity.Property(e => e.Notes)
                .HasColumnType("text")
                .HasColumnName("notes");
            entity.Property(e => e.Relationship)
                .HasMaxLength(63)
                .HasColumnName("relationship");
            entity.Property(e => e.Role)
                .HasMaxLength(63)
                .HasColumnName("role");
            entity.Property(e => e.StartDate)
                .HasColumnType("datetime")
                .HasColumnName("start_date");
            entity.Property(e => e.TargetId)
                .HasColumnType("bigint(20)")
                .HasColumnName("target_id");
            entity.Property(e => e.TargetTable)
                .HasDefaultValueSql("''")
                .HasColumnName("target_table");
            entity.Property(e => e.UpdatedBy)
                .HasComment("users.id")
                .HasColumnType("bigint(20)")
                .HasColumnName("updated_by");
            entity.Property(e => e.UpdatedDate)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("updated_date");
        });

        modelBuilder.Entity<ContactTelecom>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("contact_telecom");

            entity.HasIndex(e => e.ContactId, "contact_id");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.ContactId)
                .HasColumnType("bigint(20)")
                .HasColumnName("contact_id");
            entity.Property(e => e.CreatedBy)
                .HasComment("users.id")
                .HasColumnType("bigint(20)")
                .HasColumnName("created_by");
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("created_date");
            entity.Property(e => e.InactivatedReason)
                .HasMaxLength(45)
                .HasComment("[Values: ???, etc]")
                .HasColumnName("inactivated_reason");
            entity.Property(e => e.IsPrimary)
                .HasMaxLength(1)
                .IsFixedLength()
                .HasComment("Y=yes,N=no")
                .HasColumnName("is_primary");
            entity.Property(e => e.Notes)
                .HasColumnType("tinytext")
                .HasColumnName("notes");
            entity.Property(e => e.PeriodEnd)
                .HasComment("Date the telecom became deactivated")
                .HasColumnType("datetime")
                .HasColumnName("period_end");
            entity.Property(e => e.PeriodStart)
                .HasComment("Date the telecom became active")
                .HasColumnType("datetime")
                .HasColumnName("period_start");
            entity.Property(e => e.Rank)
                .HasComment("Specify preferred order of use (1 = highest)")
                .HasColumnType("int(11)")
                .HasColumnName("rank");
            entity.Property(e => e.Status)
                .HasMaxLength(1)
                .IsFixedLength()
                .HasComment("A=active,I=inactive")
                .HasColumnName("status");
            entity.Property(e => e.System)
                .HasMaxLength(255)
                .HasComment("FK to list_options.option_id for list_id telecom_systems [phone, fax, email, pager, url, sms, other]")
                .HasColumnName("system");
            entity.Property(e => e.UpdatedBy)
                .HasComment("users.id")
                .HasColumnType("bigint(20)")
                .HasColumnName("updated_by");
            entity.Property(e => e.UpdatedDate)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("updated_date");
            entity.Property(e => e.Use)
                .HasMaxLength(255)
                .HasComment("FK to list_options.option_id for list_id telecom_uses [home, work, temp, old, mobile]")
                .HasColumnName("use");
            entity.Property(e => e.Value)
                .HasMaxLength(255)
                .HasColumnName("value");
        });

        modelBuilder.Entity<Customlist>(entity =>
        {
            entity.HasKey(e => e.ClListSlno).HasName("PRIMARY");

            entity.ToTable("customlists");

            entity.Property(e => e.ClListSlno)
                .HasColumnType("int(10) unsigned")
                .HasColumnName("cl_list_slno");
            entity.Property(e => e.ClCreator)
                .HasColumnType("int(11)")
                .HasColumnName("cl_creator");
            entity.Property(e => e.ClDeleted)
                .HasDefaultValueSql("'0'")
                .HasColumnName("cl_deleted");
            entity.Property(e => e.ClListId)
                .HasComment("ID OF THE lIST FOR NEW TAKE SELECT MAX(cl_list_id)+1")
                .HasColumnType("int(10) unsigned")
                .HasColumnName("cl_list_id");
            entity.Property(e => e.ClListItemId)
                .HasComment("ID OF THE lIST FOR NEW TAKE SELECT MAX(cl_list_item_id)+1")
                .HasColumnType("int(10) unsigned")
                .HasColumnName("cl_list_item_id");
            entity.Property(e => e.ClListItemLevel)
                .HasComment("Flow level for List Designation")
                .HasColumnType("int(11)")
                .HasColumnName("cl_list_item_level");
            entity.Property(e => e.ClListItemLong)
                .HasColumnType("text")
                .HasColumnName("cl_list_item_long");
            entity.Property(e => e.ClListItemShort)
                .HasMaxLength(10)
                .HasColumnName("cl_list_item_short");
            entity.Property(e => e.ClListType)
                .HasComment("0=>List Name 1=>list items 2=>Context 3=>Template 4=>Sentence 5=> SavedTemplate 6=>CustomButton")
                .HasColumnType("int(10) unsigned")
                .HasColumnName("cl_list_type");
            entity.Property(e => e.ClOrder)
                .HasColumnType("int(11)")
                .HasColumnName("cl_order");
        });

        modelBuilder.Entity<DatedReminder>(entity =>
        {
            entity.HasKey(e => e.DrId).HasName("PRIMARY");

            entity.ToTable("dated_reminders");

            entity.HasIndex(e => new { e.DrFromId, e.DrMessageDueDate }, "dr_from_ID");

            entity.Property(e => e.DrId)
                .HasColumnType("int(11)")
                .HasColumnName("dr_id");
            entity.Property(e => e.DrFromId)
                .HasColumnType("int(11)")
                .HasColumnName("dr_from_ID");
            entity.Property(e => e.DrMessageDueDate).HasColumnName("dr_message_due_date");
            entity.Property(e => e.DrMessageSentDate)
                .HasColumnType("datetime")
                .HasColumnName("dr_message_sent_date");
            entity.Property(e => e.DrMessageText)
                .HasMaxLength(160)
                .HasColumnName("dr_message_text");
            entity.Property(e => e.DrProcessedBy)
                .HasColumnType("int(11)")
                .HasColumnName("dr_processed_by");
            entity.Property(e => e.MessagePriority).HasColumnName("message_priority");
            entity.Property(e => e.MessageProcessed).HasColumnName("message_processed");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.ProcessedDate)
                .HasColumnType("timestamp")
                .HasColumnName("processed_date");
        });

        modelBuilder.Entity<DatedRemindersLink>(entity =>
        {
            entity.HasKey(e => e.DrLinkId).HasName("PRIMARY");

            entity.ToTable("dated_reminders_link");

            entity.HasIndex(e => e.DrId, "dr_id");

            entity.HasIndex(e => e.ToId, "to_id");

            entity.Property(e => e.DrLinkId)
                .HasColumnType("int(11)")
                .HasColumnName("dr_link_id");
            entity.Property(e => e.DrId)
                .HasColumnType("int(11)")
                .HasColumnName("dr_id");
            entity.Property(e => e.ToId)
                .HasColumnType("int(11)")
                .HasColumnName("to_id");
        });

        modelBuilder.Entity<DirectMessageLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("direct_message_log");

            entity.HasIndex(e => e.MsgId, "msg_id");

            entity.HasIndex(e => e.PatientId, "patient_id");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.CreateTs)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("timestamp")
                .HasColumnName("create_ts");
            entity.Property(e => e.MsgId)
                .HasMaxLength(127)
                .HasColumnName("msg_id");
            entity.Property(e => e.MsgType)
                .HasMaxLength(1)
                .IsFixedLength()
                .HasComment("S=sent,R=received")
                .HasColumnName("msg_type");
            entity.Property(e => e.PatientId)
                .HasColumnType("bigint(20)")
                .HasColumnName("patient_id");
            entity.Property(e => e.Recipient)
                .HasMaxLength(255)
                .HasColumnName("recipient");
            entity.Property(e => e.Sender)
                .HasMaxLength(255)
                .HasColumnName("sender");
            entity.Property(e => e.Status)
                .HasMaxLength(1)
                .IsFixedLength()
                .HasComment("Q=queued,D=dispatched,R=received,F=failed")
                .HasColumnName("status");
            entity.Property(e => e.StatusInfo)
                .HasMaxLength(511)
                .HasColumnName("status_info");
            entity.Property(e => e.StatusTs)
                .HasColumnType("timestamp")
                .HasColumnName("status_ts");
            entity.Property(e => e.UserId)
                .HasColumnType("bigint(20)")
                .HasColumnName("user_id");
        });

        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("documents");

            entity.HasIndex(e => e.DriveUuid, "drive_uuid").IsUnique();

            entity.HasIndex(e => e.ForeignId, "foreign_id");

            entity.HasIndex(e => new { e.ForeignReferenceId, e.ForeignReferenceTable }, "foreign_reference");

            entity.HasIndex(e => e.Owner, "owner");

            entity.HasIndex(e => e.Revision, "revision");

            entity.HasIndex(e => e.Uuid, "uuid").IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.AuditMasterApprovalStatus)
                .HasDefaultValueSql("'1'")
                .HasComment("approval_status from audit_master table")
                .HasColumnType("tinyint(4)")
                .HasColumnName("audit_master_approval_status");
            entity.Property(e => e.AuditMasterId)
                .HasColumnType("int(11)")
                .HasColumnName("audit_master_id");
            entity.Property(e => e.CouchDocid)
                .HasMaxLength(100)
                .HasColumnName("couch_docid");
            entity.Property(e => e.CouchRevid)
                .HasMaxLength(100)
                .HasColumnName("couch_revid");
            entity.Property(e => e.Date)
                .HasColumnType("datetime")
                .HasColumnName("date");
            entity.Property(e => e.DateExpires)
                .HasColumnType("datetime")
                .HasColumnName("date_expires");
            entity.Property(e => e.Deleted).HasColumnName("deleted");
            entity.Property(e => e.Docdate).HasColumnName("docdate");
            entity.Property(e => e.DocumentData)
                .HasColumnType("mediumtext")
                .HasColumnName("document_data");
            entity.Property(e => e.DocumentationOf)
                .HasMaxLength(255)
                .HasColumnName("documentationOf");
            entity.Property(e => e.DriveUuid)
                .HasMaxLength(16)
                .IsFixedLength()
                .HasColumnName("drive_uuid");
            entity.Property(e => e.EncounterCheck)
                .HasComment("If encounter is created while tagging")
                .HasColumnName("encounter_check");
            entity.Property(e => e.EncounterId)
                .HasComment("Encounter id if tagged")
                .HasColumnType("bigint(20)")
                .HasColumnName("encounter_id");
            entity.Property(e => e.Encrypted)
                .HasComment("0->No,1->Yes")
                .HasColumnType("tinyint(4)")
                .HasColumnName("encrypted");
            entity.Property(e => e.ForeignId)
                .HasColumnType("bigint(20)")
                .HasColumnName("foreign_id");
            entity.Property(e => e.ForeignReferenceId)
                .HasColumnType("bigint(20)")
                .HasColumnName("foreign_reference_id");
            entity.Property(e => e.ForeignReferenceTable)
                .HasMaxLength(40)
                .HasColumnName("foreign_reference_table");
            entity.Property(e => e.Hash)
                .HasMaxLength(255)
                .HasColumnName("hash");
            entity.Property(e => e.Imported)
                .HasDefaultValueSql("'0'")
                .HasComment("Parsing status for CCR/CCD/CCDA importing")
                .HasColumnType("tinyint(4)")
                .HasColumnName("imported");
            entity.Property(e => e.ListId)
                .HasColumnType("bigint(20)")
                .HasColumnName("list_id");
            entity.Property(e => e.Mimetype)
                .HasMaxLength(255)
                .HasColumnName("mimetype");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.Owner)
                .HasColumnType("int(11)")
                .HasColumnName("owner");
            entity.Property(e => e.Pages)
                .HasColumnType("int(11)")
                .HasColumnName("pages");
            entity.Property(e => e.PathDepth)
                .HasDefaultValueSql("'1'")
                .HasComment("Depth of path to use in url to find document. Not applicable for CouchDB.")
                .HasColumnType("tinyint(4)")
                .HasColumnName("path_depth");
            entity.Property(e => e.Revision)
                .HasColumnType("timestamp")
                .HasColumnName("revision");
            entity.Property(e => e.Size)
                .HasColumnType("int(11)")
                .HasColumnName("size");
            entity.Property(e => e.Storagemethod)
                .HasComment("0->Harddisk,1->CouchDB")
                .HasColumnType("tinyint(4)")
                .HasColumnName("storagemethod");
            entity.Property(e => e.ThumbUrl)
                .HasMaxLength(255)
                .HasColumnName("thumb_url");
            entity.Property(e => e.Type)
                .HasColumnType("enum('file_url','blob','web_url')")
                .HasColumnName("type");
            entity.Property(e => e.Url)
                .HasMaxLength(255)
                .HasColumnName("url");
            entity.Property(e => e.Uuid)
                .HasMaxLength(16)
                .IsFixedLength()
                .HasColumnName("uuid");
        });

        modelBuilder.Entity<DocumentTemplate>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("document_templates");

            entity.HasIndex(e => new { e.Pid, e.Profile, e.Category, e.TemplateName }, "location").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("bigint(21) unsigned")
                .HasColumnName("id");
            entity.Property(e => e.Category)
                .HasMaxLength(63)
                .HasColumnName("category");
            entity.Property(e => e.Encounter)
                .HasColumnType("int(11) unsigned")
                .HasColumnName("encounter");
            entity.Property(e => e.EndDate)
                .HasColumnType("datetime")
                .HasColumnName("end_date");
            entity.Property(e => e.Location)
                .HasMaxLength(255)
                .HasColumnName("location");
            entity.Property(e => e.Mime)
                .HasMaxLength(31)
                .HasColumnName("mime");
            entity.Property(e => e.ModifiedDate)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("modified_date");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.Profile)
                .HasMaxLength(63)
                .HasColumnName("profile");
            entity.Property(e => e.Provider)
                .HasColumnType("int(11) unsigned")
                .HasColumnName("provider");
            entity.Property(e => e.SendDate)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("send_date");
            entity.Property(e => e.Size)
                .HasColumnType("int(11)")
                .HasColumnName("size");
            entity.Property(e => e.Status)
                .HasMaxLength(31)
                .HasColumnName("status");
            entity.Property(e => e.TemplateContent)
                .HasColumnType("mediumblob")
                .HasColumnName("template_content");
            entity.Property(e => e.TemplateName).HasColumnName("template_name");
        });

        modelBuilder.Entity<DocumentTemplateProfile>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("document_template_profiles");

            entity.HasIndex(e => new { e.Profile, e.TemplateId, e.MemberOf }, "location").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("bigint(21) unsigned")
                .HasColumnName("id");
            entity.Property(e => e.Active).HasColumnName("active");
            entity.Property(e => e.Category)
                .HasMaxLength(64)
                .HasColumnName("category");
            entity.Property(e => e.EventTrigger)
                .HasMaxLength(31)
                .HasColumnName("event_trigger");
            entity.Property(e => e.MemberOf)
                .HasMaxLength(64)
                .HasColumnName("member_of");
            entity.Property(e => e.ModifiedDate)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("modified_date");
            entity.Property(e => e.NotifyPeriod)
                .HasColumnType("int(4)")
                .HasColumnName("notify_period");
            entity.Property(e => e.NotifyTrigger)
                .HasMaxLength(31)
                .HasColumnName("notify_trigger");
            entity.Property(e => e.Period)
                .HasColumnType("int(4)")
                .HasColumnName("period");
            entity.Property(e => e.Profile)
                .HasMaxLength(64)
                .HasColumnName("profile");
            entity.Property(e => e.Provider)
                .HasColumnType("int(11) unsigned")
                .HasColumnName("provider");
            entity.Property(e => e.Recurring)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("recurring");
            entity.Property(e => e.TemplateId)
                .HasColumnType("bigint(21) unsigned")
                .HasColumnName("template_id");
            entity.Property(e => e.TemplateName)
                .HasMaxLength(255)
                .HasColumnName("template_name");
        });

        modelBuilder.Entity<DocumentsLegalCategory>(entity =>
        {
            entity.HasKey(e => e.DlcId).HasName("PRIMARY");

            entity.ToTable("documents_legal_categories");

            entity.Property(e => e.DlcId)
                .HasColumnType("int(10) unsigned")
                .HasColumnName("dlc_id");
            entity.Property(e => e.DlcCategoryName)
                .HasMaxLength(45)
                .HasColumnName("dlc_category_name");
            entity.Property(e => e.DlcCategoryParent)
                .HasColumnType("int(10) unsigned")
                .HasColumnName("dlc_category_parent");
            entity.Property(e => e.DlcCategoryType)
                .HasComment("1 category 2 subcategory")
                .HasColumnType("int(10) unsigned")
                .HasColumnName("dlc_category_type");
        });

        modelBuilder.Entity<DocumentsLegalDetail>(entity =>
        {
            entity.HasKey(e => e.DldId).HasName("PRIMARY");

            entity.ToTable("documents_legal_detail");

            entity.Property(e => e.DldId)
                .HasColumnType("int(10) unsigned")
                .HasColumnName("dld_id");
            entity.Property(e => e.DldContent)
                .HasMaxLength(50)
                .HasComment("Layout sign position")
                .HasColumnName("dld_content");
            entity.Property(e => e.DldDenialReason).HasColumnName("dld_denial_reason");
            entity.Property(e => e.DldEncounter)
                .HasColumnType("int(10) unsigned")
                .HasColumnName("dld_encounter");
            entity.Property(e => e.DldFacility)
                .HasColumnType("int(10) unsigned")
                .HasColumnName("dld_facility");
            entity.Property(e => e.DldFileForPdfGeneration)
                .HasComment("The filled details in the fdf file is stored here.Patient Registration Screen")
                .HasColumnType("blob")
                .HasColumnName("dld_file_for_pdf_generation");
            entity.Property(e => e.DldFilename)
                .HasMaxLength(45)
                .HasColumnName("dld_filename");
            entity.Property(e => e.DldFilepath)
                .HasMaxLength(75)
                .HasColumnName("dld_filepath");
            entity.Property(e => e.DldMasterDocid)
                .HasColumnType("int(10) unsigned")
                .HasColumnName("dld_master_docid");
            entity.Property(e => e.DldMoved)
                .HasColumnType("tinyint(4)")
                .HasColumnName("dld_moved");
            entity.Property(e => e.DldPatientComments)
                .HasComment("Patient comments stored here")
                .HasColumnType("text")
                .HasColumnName("dld_patient_comments");
            entity.Property(e => e.DldPid)
                .HasColumnType("int(10) unsigned")
                .HasColumnName("dld_pid");
            entity.Property(e => e.DldProvider)
                .HasColumnType("int(10) unsigned")
                .HasColumnName("dld_provider");
            entity.Property(e => e.DldSignLevel)
                .HasComment("Sign flow level")
                .HasColumnType("int(11)")
                .HasColumnName("dld_sign_level");
            entity.Property(e => e.DldSigned)
                .HasComment("0-Not Signed or Cannot Sign(Layout),1-Signed,2-Ready to sign,3-Denied(Pat Regi),4-Patient Upload,10-Save(Layout)")
                .HasColumnType("smallint(5) unsigned")
                .HasColumnName("dld_signed");
            entity.Property(e => e.DldSignedTime)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("timestamp")
                .HasColumnName("dld_signed_time");
            entity.Property(e => e.DldSigningPerson)
                .HasMaxLength(50)
                .HasColumnName("dld_signing_person");
        });

        modelBuilder.Entity<DocumentsLegalMaster>(entity =>
        {
            entity.HasKey(e => e.DlmDocumentId).HasName("PRIMARY");

            entity.ToTable("documents_legal_master", tb => tb.HasComment("List of Master Docs to be signed"));

            entity.Property(e => e.DlmDocumentId)
                .HasColumnType("int(10) unsigned")
                .HasColumnName("dlm_document_id");
            entity.Property(e => e.Content)
                .HasMaxLength(255)
                .HasColumnName("content");
            entity.Property(e => e.DlmCategory)
                .HasColumnType("int(10) unsigned")
                .HasColumnName("dlm_category");
            entity.Property(e => e.DlmDocumentName)
                .HasMaxLength(75)
                .HasColumnName("dlm_document_name");
            entity.Property(e => e.DlmEffectiveDate)
                .HasColumnType("datetime")
                .HasColumnName("dlm_effective_date");
            entity.Property(e => e.DlmFacility)
                .HasColumnType("int(10) unsigned")
                .HasColumnName("dlm_facility");
            entity.Property(e => e.DlmFilename)
                .HasMaxLength(45)
                .HasColumnName("dlm_filename");
            entity.Property(e => e.DlmFilepath)
                .HasMaxLength(75)
                .HasColumnName("dlm_filepath");
            entity.Property(e => e.DlmProvider)
                .HasColumnType("int(10) unsigned")
                .HasColumnName("dlm_provider");
            entity.Property(e => e.DlmReview)
                .HasMaxLength(255)
                .HasComment("0-Yes 1-No")
                .HasColumnName("dlm_review");
            entity.Property(e => e.DlmSavedsign)
                .HasMaxLength(255)
                .HasComment("0-Yes 1-No")
                .HasColumnName("dlm_savedsign");
            entity.Property(e => e.DlmSignHeight).HasColumnName("dlm_sign_height");
            entity.Property(e => e.DlmSignWidth).HasColumnName("dlm_sign_width");
            entity.Property(e => e.DlmSubcategory)
                .HasColumnType("int(10) unsigned")
                .HasColumnName("dlm_subcategory");
            entity.Property(e => e.DlmUploadType)
                .HasDefaultValueSql("'0'")
                .HasComment("0-Provider Uploaded,1-Patient Uploaded")
                .HasColumnType("tinyint(4)")
                .HasColumnName("dlm_upload_type");
            entity.Property(e => e.DlmVersion)
                .HasColumnType("int(10) unsigned")
                .HasColumnName("dlm_version");
        });

        modelBuilder.Entity<Drug>(entity =>
        {
            entity.HasKey(e => e.DrugId).HasName("PRIMARY");

            entity.ToTable("drugs");

            entity.HasIndex(e => e.Uuid, "uuid").IsUnique();

            entity.Property(e => e.DrugId)
                .HasColumnType("int(11)")
                .HasColumnName("drug_id");
            entity.Property(e => e.Active)
                .HasDefaultValueSql("'1'")
                .HasComment("0 = inactive, 1 = active")
                .HasColumnName("active");
            entity.Property(e => e.AllowCombining)
                .HasComment("1 = allow filling an order from multiple lots")
                .HasColumnName("allow_combining");
            entity.Property(e => e.AllowMultiple)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasComment("1 = allow multiple lots at one warehouse")
                .HasColumnName("allow_multiple");
            entity.Property(e => e.BillingUnits)
                .HasComment("default units when the related HCPCS code is added to a fee sheet")
                .HasColumnType("int(11)")
                .HasColumnName("billing_units");
            entity.Property(e => e.Consumable)
                .HasComment("1 = will not show on the fee sheet")
                .HasColumnName("consumable");
            entity.Property(e => e.CypFactor)
                .HasComment("quantity representing a years supply")
                .HasColumnName("cyp_factor");
            entity.Property(e => e.DateCreated)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("date_created");
            entity.Property(e => e.Dispensable)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasComment("0 = pharmacy elsewhere, 1 = dispensed here")
                .HasColumnName("dispensable");
            entity.Property(e => e.DrugCode)
                .HasMaxLength(25)
                .HasColumnName("drug_code");
            entity.Property(e => e.Form)
                .HasMaxLength(31)
                .HasDefaultValueSql("'0'")
                .HasColumnName("form");
            entity.Property(e => e.LastNotify).HasColumnName("last_notify");
            entity.Property(e => e.LastUpdated)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("last_updated");
            entity.Property(e => e.MaxLevel).HasColumnName("max_level");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("name");
            entity.Property(e => e.NdcNumber)
                .HasMaxLength(20)
                .HasDefaultValueSql("''")
                .HasColumnName("ndc_number");
            entity.Property(e => e.NdcQuantity)
                .HasPrecision(10, 3)
                .HasComment("NDC quantity for the related HCPCS service line")
                .HasColumnName("ndc_quantity");
            entity.Property(e => e.NdcUom)
                .HasMaxLength(2)
                .HasDefaultValueSql("''")
                .HasComment("NDC unit of measure for the related HCPCS service line")
                .HasColumnName("ndc_uom");
            entity.Property(e => e.OnOrder)
                .HasColumnType("int(11)")
                .HasColumnName("on_order");
            entity.Property(e => e.Reactions)
                .HasColumnType("text")
                .HasColumnName("reactions");
            entity.Property(e => e.RelatedCode)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasComment("may reference a related codes.code")
                .HasColumnName("related_code");
            entity.Property(e => e.ReorderPoint).HasColumnName("reorder_point");
            entity.Property(e => e.Route)
                .HasMaxLength(31)
                .HasDefaultValueSql("'0'")
                .HasColumnName("route");
            entity.Property(e => e.Size)
                .HasMaxLength(25)
                .HasDefaultValueSql("''")
                .HasColumnName("size");
            entity.Property(e => e.Substitute)
                .HasColumnType("int(11)")
                .HasColumnName("substitute");
            entity.Property(e => e.Unit)
                .HasMaxLength(31)
                .HasDefaultValueSql("'0'")
                .HasColumnName("unit");
            entity.Property(e => e.Uuid)
                .HasMaxLength(16)
                .IsFixedLength()
                .HasColumnName("uuid");
        });

        modelBuilder.Entity<DrugInventory>(entity =>
        {
            entity.HasKey(e => e.InventoryId).HasName("PRIMARY");

            entity.ToTable("drug_inventory");

            entity.Property(e => e.InventoryId)
                .HasColumnType("int(11)")
                .HasColumnName("inventory_id");
            entity.Property(e => e.DestroyDate).HasColumnName("destroy_date");
            entity.Property(e => e.DestroyMethod)
                .HasMaxLength(255)
                .HasColumnName("destroy_method");
            entity.Property(e => e.DestroyNotes)
                .HasMaxLength(255)
                .HasColumnName("destroy_notes");
            entity.Property(e => e.DestroyWitness)
                .HasMaxLength(255)
                .HasColumnName("destroy_witness");
            entity.Property(e => e.DrugId)
                .HasColumnType("int(11)")
                .HasColumnName("drug_id");
            entity.Property(e => e.Expiration).HasColumnName("expiration");
            entity.Property(e => e.LastNotify).HasColumnName("last_notify");
            entity.Property(e => e.LotNumber)
                .HasMaxLength(20)
                .HasColumnName("lot_number");
            entity.Property(e => e.Manufacturer)
                .HasMaxLength(255)
                .HasColumnName("manufacturer");
            entity.Property(e => e.OnHand)
                .HasColumnType("int(11)")
                .HasColumnName("on_hand");
            entity.Property(e => e.VendorId)
                .HasColumnType("bigint(20)")
                .HasColumnName("vendor_id");
            entity.Property(e => e.WarehouseId)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasColumnName("warehouse_id");
        });

        modelBuilder.Entity<DrugSale>(entity =>
        {
            entity.HasKey(e => e.SaleId).HasName("PRIMARY");

            entity.ToTable("drug_sales");

            entity.HasIndex(e => e.Uuid, "uuid").IsUnique();

            entity.Property(e => e.SaleId)
                .HasColumnType("int(11)")
                .HasColumnName("sale_id");
            entity.Property(e => e.BillDate)
                .HasColumnType("datetime")
                .HasColumnName("bill_date");
            entity.Property(e => e.Billed)
                .HasComment("indicates if the sale is posted to accounting")
                .HasColumnName("billed");
            entity.Property(e => e.Chargecat)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasColumnName("chargecat");
            entity.Property(e => e.CreatedBy)
                .HasComment("fk to users.id for user that created this entry")
                .HasColumnType("bigint(20)")
                .HasColumnName("created_by");
            entity.Property(e => e.DateCreated)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("date_created");
            entity.Property(e => e.DistributorId)
                .HasComment("references users.id")
                .HasColumnType("bigint(20)")
                .HasColumnName("distributor_id");
            entity.Property(e => e.DrugId)
                .HasColumnType("int(11)")
                .HasColumnName("drug_id");
            entity.Property(e => e.Encounter)
                .HasColumnType("int(11)")
                .HasColumnName("encounter");
            entity.Property(e => e.Fee)
                .HasPrecision(12, 2)
                .HasColumnName("fee");
            entity.Property(e => e.InventoryId)
                .HasColumnType("int(11)")
                .HasColumnName("inventory_id");
            entity.Property(e => e.LastUpdated)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("last_updated");
            entity.Property(e => e.Notes)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("notes");
            entity.Property(e => e.PharmacySupplyType)
                .HasMaxLength(50)
                .HasComment("fk to list_options.option_id where list_id=pharmacy_supply_type to indicate type of dispensing first order, refil, emergency, partial order, etc")
                .HasColumnName("pharmacy_supply_type");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.PrescriptionId)
                .HasColumnType("int(11)")
                .HasColumnName("prescription_id");
            entity.Property(e => e.Pricelevel)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasColumnName("pricelevel");
            entity.Property(e => e.Quantity)
                .HasColumnType("int(11)")
                .HasColumnName("quantity");
            entity.Property(e => e.SaleDate).HasColumnName("sale_date");
            entity.Property(e => e.Selector)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasComment("references drug_templates.selector")
                .HasColumnName("selector");
            entity.Property(e => e.TransType)
                .HasDefaultValueSql("'1'")
                .HasComment("1=sale, 2=purchase, 3=return, 4=transfer, 5=adjustment")
                .HasColumnType("tinyint(4)")
                .HasColumnName("trans_type");
            entity.Property(e => e.UpdatedBy)
                .HasComment("fk to users.id for user that last updated this entry")
                .HasColumnType("bigint(20)")
                .HasColumnName("updated_by");
            entity.Property(e => e.User)
                .HasMaxLength(255)
                .HasColumnName("user");
            entity.Property(e => e.Uuid)
                .HasMaxLength(16)
                .IsFixedLength()
                .HasComment("UUID for this drug sales record, for data exchange purposes")
                .HasColumnName("uuid");
            entity.Property(e => e.XferInventoryId)
                .HasColumnType("int(11)")
                .HasColumnName("xfer_inventory_id");
        });

        modelBuilder.Entity<DrugTemplate>(entity =>
        {
            entity.HasKey(e => new { e.DrugId, e.Selector })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("drug_templates");

            entity.Property(e => e.DrugId)
                .HasColumnType("int(11)")
                .HasColumnName("drug_id");
            entity.Property(e => e.Selector)
                .HasDefaultValueSql("''")
                .HasColumnName("selector");
            entity.Property(e => e.Dosage)
                .HasMaxLength(10)
                .HasColumnName("dosage");
            entity.Property(e => e.Period)
                .HasColumnType("int(11)")
                .HasColumnName("period");
            entity.Property(e => e.Pkgqty)
                .HasDefaultValueSql("'1'")
                .HasComment("Number of product items per template item")
                .HasColumnName("pkgqty");
            entity.Property(e => e.Quantity)
                .HasColumnType("int(11)")
                .HasColumnName("quantity");
            entity.Property(e => e.Refills)
                .HasColumnType("int(11)")
                .HasColumnName("refills");
            entity.Property(e => e.Taxrates)
                .HasMaxLength(255)
                .HasColumnName("taxrates");
        });

        modelBuilder.Entity<DsiSourceAttribute>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("dsi_source_attributes", tb => tb.HasComment("Holds information about decision support intervention system source attributes"));

            entity.HasIndex(e => new { e.ListId, e.OptionId, e.ClientId }, "list_id").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20) unsigned")
                .HasColumnName("id");
            entity.Property(e => e.ClientId)
                .HasMaxLength(80)
                .HasColumnName("client_id");
            entity.Property(e => e.ClinicalRuleId)
                .HasMaxLength(31)
                .HasColumnName("clinical_rule_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("timestamp")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasColumnType("bigint(20)")
                .HasColumnName("created_by");
            entity.Property(e => e.LastUpdatedAt)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("timestamp")
                .HasColumnName("last_updated_at");
            entity.Property(e => e.LastUpdatedBy)
                .HasColumnType("bigint(20)")
                .HasColumnName("last_updated_by");
            entity.Property(e => e.ListId)
                .HasMaxLength(100)
                .HasColumnName("list_id");
            entity.Property(e => e.OptionId)
                .HasMaxLength(100)
                .HasColumnName("option_id");
            entity.Property(e => e.SourceValue)
                .HasColumnType("text")
                .HasColumnName("source_value");
        });

        modelBuilder.Entity<EdiSequence>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("edi_sequences");

            entity.Property(e => e.Id)
                .HasColumnType("int(9) unsigned")
                .HasColumnName("id");
        });

        modelBuilder.Entity<EligibilityVerification>(entity =>
        {
            entity.HasKey(e => e.VerificationId).HasName("PRIMARY");

            entity.ToTable("eligibility_verification");

            entity.HasIndex(e => e.InsuranceId, "insurance_id");

            entity.Property(e => e.VerificationId)
                .HasColumnType("bigint(20)")
                .HasColumnName("verification_id");
            entity.Property(e => e.Copay)
                .HasColumnType("int(11)")
                .HasColumnName("copay");
            entity.Property(e => e.CreateDate).HasColumnName("create_date");
            entity.Property(e => e.Deductible)
                .HasColumnType("int(11)")
                .HasColumnName("deductible");
            entity.Property(e => e.Deductiblemet)
                .HasDefaultValueSql("'Y'")
                .HasColumnType("enum('Y','N')")
                .HasColumnName("deductiblemet");
            entity.Property(e => e.EligibilityCheckDate)
                .HasColumnType("datetime")
                .HasColumnName("eligibility_check_date");
            entity.Property(e => e.InsuranceId)
                .HasColumnType("bigint(20)")
                .HasColumnName("insurance_id");
            entity.Property(e => e.ResponseId)
                .HasMaxLength(32)
                .HasColumnName("response_id");
        });

        modelBuilder.Entity<EmailQueue>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("email_queue");

            entity.HasIndex(e => e.Sent, "sent");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Body)
                .HasColumnType("text")
                .HasColumnName("body");
            entity.Property(e => e.DatetimeError)
                .HasColumnType("datetime")
                .HasColumnName("datetime_error");
            entity.Property(e => e.DatetimeQueued)
                .HasColumnType("datetime")
                .HasColumnName("datetime_queued");
            entity.Property(e => e.DatetimeSent)
                .HasColumnType("datetime")
                .HasColumnName("datetime_sent");
            entity.Property(e => e.Error)
                .HasDefaultValueSql("'0'")
                .HasColumnType("tinyint(4)")
                .HasColumnName("error");
            entity.Property(e => e.ErrorMessage)
                .HasColumnType("text")
                .HasColumnName("error_message");
            entity.Property(e => e.Recipient)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("recipient");
            entity.Property(e => e.Sender)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("sender");
            entity.Property(e => e.Sent)
                .HasDefaultValueSql("'0'")
                .HasColumnType("tinyint(4)")
                .HasColumnName("sent");
            entity.Property(e => e.Subject)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("subject");
            entity.Property(e => e.TemplateName)
                .HasMaxLength(255)
                .HasComment("The folder prefix and base filename (w/o extension) of the twig template file to use for this email")
                .HasColumnName("template_name");
        });

        modelBuilder.Entity<EmployerDatum>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("employer_data");

            entity.HasIndex(e => e.Pid, "pid");

            entity.HasIndex(e => e.Uuid, "uuid_unique").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.City)
                .HasMaxLength(255)
                .HasColumnName("city");
            entity.Property(e => e.Country)
                .HasMaxLength(255)
                .HasColumnName("country");
            entity.Property(e => e.CreatedBy)
                .HasComment("fk to users.id for the user that entered in the employer data")
                .HasColumnType("int(11)")
                .HasColumnName("created_by");
            entity.Property(e => e.Date)
                .HasColumnType("datetime")
                .HasColumnName("date");
            entity.Property(e => e.EndDate)
                .HasComment("Employment end date for patient")
                .HasColumnType("datetime")
                .HasColumnName("end_date");
            entity.Property(e => e.Industry)
                .HasComment("Employment Industry fk to list_options.option_id where list_id=IndustryODH")
                .HasColumnType("text")
                .HasColumnName("industry");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.Occupation)
                .HasComment("Employment Occupation fk to list_options.option_id where list_id=OccupationODH")
                .HasColumnName("occupation");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.PostalCode)
                .HasMaxLength(255)
                .HasColumnName("postal_code");
            entity.Property(e => e.StartDate)
                .HasComment("Employment start date for patient")
                .HasColumnType("datetime")
                .HasColumnName("start_date");
            entity.Property(e => e.State)
                .HasMaxLength(255)
                .HasColumnName("state");
            entity.Property(e => e.Street)
                .HasMaxLength(255)
                .HasColumnName("street");
            entity.Property(e => e.StreetLine2)
                .HasColumnType("tinytext")
                .HasColumnName("street_line_2");
            entity.Property(e => e.Uuid)
                .HasMaxLength(16)
                .IsFixedLength()
                .HasComment("UUID for this employer record, for data exchange purposes")
                .HasColumnName("uuid");
        });

        modelBuilder.Entity<EncCategoryMap>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("enc_category_map");

            entity.HasIndex(e => new { e.RuleEncId, e.MainCatId }, "rule_enc_id");

            entity.Property(e => e.MainCatId)
                .HasComment("category id from event category in openemr_postcalendar_categories")
                .HasColumnType("int(11)")
                .HasColumnName("main_cat_id");
            entity.Property(e => e.RuleEncId)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("encounter id from rule_enc_types list in list_options")
                .HasColumnName("rule_enc_id");
        });

        modelBuilder.Entity<ErxNarcotic>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("erx_narcotics");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.CsaSch)
                .HasMaxLength(2)
                .HasColumnName("csa_sch");
            entity.Property(e => e.DeaNumber)
                .HasMaxLength(5)
                .HasColumnName("dea_number");
            entity.Property(e => e.Drug)
                .HasMaxLength(255)
                .HasColumnName("drug");
            entity.Property(e => e.Narc)
                .HasMaxLength(2)
                .HasColumnName("narc");
            entity.Property(e => e.OtherNames)
                .HasMaxLength(255)
                .HasColumnName("other_names");
        });

        modelBuilder.Entity<ErxRxLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("erx_rx_log");

            entity.Property(e => e.Id)
                .HasColumnType("int(20)")
                .HasColumnName("id");
            entity.Property(e => e.Code)
                .HasColumnType("int(6)")
                .HasColumnName("code");
            entity.Property(e => e.Date)
                .HasMaxLength(25)
                .HasColumnName("date");
            entity.Property(e => e.MessageId)
                .HasMaxLength(100)
                .HasColumnName("message_id");
            entity.Property(e => e.PrescriptionId)
                .HasColumnType("int(6)")
                .HasColumnName("prescription_id");
            entity.Property(e => e.Read)
                .HasColumnType("int(1)")
                .HasColumnName("read");
            entity.Property(e => e.Status)
                .HasColumnType("text")
                .HasColumnName("status");
            entity.Property(e => e.Time)
                .HasMaxLength(15)
                .HasColumnName("time");
        });

        modelBuilder.Entity<ErxTtlTouch>(entity =>
        {
            entity.HasKey(e => new { e.PatientId, e.Process })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("erx_ttl_touch", tb => tb.HasComment("Store records last update per patient data process"));

            entity.Property(e => e.PatientId)
                .HasComment("Patient record Id")
                .HasColumnType("bigint(20) unsigned")
                .HasColumnName("patient_id");
            entity.Property(e => e.Process)
                .HasComment("NewCrop eRx SOAP process")
                .HasColumnType("enum('allergies','medications')")
                .HasColumnName("process");
            entity.Property(e => e.Updated)
                .HasComment("Date and time of last process update for patient")
                .HasColumnType("datetime")
                .HasColumnName("updated");
        });

        modelBuilder.Entity<EsignSignature>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("esign_signatures");

            entity.HasIndex(e => e.Table, "table");

            entity.HasIndex(e => e.Tid, "tid");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.Amendment)
                .HasComment("amendment text, if any")
                .HasColumnType("text")
                .HasColumnName("amendment");
            entity.Property(e => e.Datetime)
                .HasComment("datetime of the signature action")
                .HasColumnType("datetime")
                .HasColumnName("datetime");
            entity.Property(e => e.Hash)
                .HasMaxLength(255)
                .HasComment("hash of signed data")
                .HasColumnName("hash");
            entity.Property(e => e.IsLock)
                .HasComment("sig, lock or amendment")
                .HasColumnName("is_lock");
            entity.Property(e => e.SignatureHash)
                .HasMaxLength(255)
                .HasComment("hash of signature itself")
                .HasColumnName("signature_hash");
            entity.Property(e => e.Table)
                .HasComment("table name for the signature")
                .HasColumnName("table");
            entity.Property(e => e.Tid)
                .HasComment("Table row ID for signature")
                .HasColumnType("int(11)")
                .HasColumnName("tid");
            entity.Property(e => e.Uid)
                .HasComment("user id for the signing user")
                .HasColumnType("int(11)")
                .HasColumnName("uid");
        });

        modelBuilder.Entity<ExportJob>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("export_job", tb => tb.HasComment("fhir export jobs"));

            entity.HasIndex(e => e.Uuid, "uuid").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.AccessTokenId)
                .HasColumnType("text")
                .HasColumnName("access_token_id");
            entity.Property(e => e.ClientId)
                .HasMaxLength(80)
                .HasColumnName("client_id");
            entity.Property(e => e.Errors)
                .HasColumnType("text")
                .HasColumnName("errors");
            entity.Property(e => e.Output)
                .HasColumnType("text")
                .HasColumnName("output");
            entity.Property(e => e.OutputFormat)
                .HasMaxLength(128)
                .HasColumnName("output_format");
            entity.Property(e => e.RequestUri)
                .HasMaxLength(128)
                .HasColumnName("request_uri");
            entity.Property(e => e.ResourceIncludeTime)
                .HasColumnType("datetime")
                .HasColumnName("resource_include_time");
            entity.Property(e => e.Resources)
                .HasColumnType("text")
                .HasColumnName("resources");
            entity.Property(e => e.StartTime)
                .HasColumnType("datetime")
                .HasColumnName("start_time");
            entity.Property(e => e.Status)
                .HasMaxLength(40)
                .HasColumnName("status");
            entity.Property(e => e.UserId)
                .HasMaxLength(40)
                .HasColumnName("user_id");
            entity.Property(e => e.Uuid)
                .HasMaxLength(16)
                .IsFixedLength()
                .HasColumnName("uuid");
        });

        modelBuilder.Entity<ExtendedLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("extended_log");

            entity.HasIndex(e => e.PatientId, "patient_id");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Date)
                .HasColumnType("datetime")
                .HasColumnName("date");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Event)
                .HasMaxLength(255)
                .HasColumnName("event");
            entity.Property(e => e.PatientId)
                .HasColumnType("bigint(20)")
                .HasColumnName("patient_id");
            entity.Property(e => e.Recipient)
                .HasMaxLength(255)
                .HasColumnName("recipient");
            entity.Property(e => e.User)
                .HasMaxLength(255)
                .HasColumnName("user");
        });

        modelBuilder.Entity<ExternalEncounter>(entity =>
        {
            entity.HasKey(e => e.EeId).HasName("PRIMARY");

            entity.ToTable("external_encounters");

            entity.Property(e => e.EeId)
                .HasColumnType("int(11)")
                .HasColumnName("ee_id");
            entity.Property(e => e.EeDate).HasColumnName("ee_date");
            entity.Property(e => e.EeEncounterDiagnosis)
                .HasMaxLength(255)
                .HasColumnName("ee_encounter_diagnosis");
            entity.Property(e => e.EeExternalId)
                .HasMaxLength(255)
                .HasColumnName("ee_external_id");
            entity.Property(e => e.EeFacilityId)
                .HasMaxLength(255)
                .HasColumnName("ee_facility_id");
            entity.Property(e => e.EePid)
                .HasColumnType("int(11)")
                .HasColumnName("ee_pid");
            entity.Property(e => e.EeProviderId)
                .HasMaxLength(255)
                .HasColumnName("ee_provider_id");
        });

        modelBuilder.Entity<ExternalProcedure>(entity =>
        {
            entity.HasKey(e => e.EpId).HasName("PRIMARY");

            entity.ToTable("external_procedures");

            entity.HasIndex(e => e.EpPid, "ep_pid");

            entity.Property(e => e.EpId)
                .HasColumnType("int(11)")
                .HasColumnName("ep_id");
            entity.Property(e => e.EpCode)
                .HasMaxLength(9)
                .HasColumnName("ep_code");
            entity.Property(e => e.EpCodeText).HasColumnName("ep_code_text");
            entity.Property(e => e.EpCodeType)
                .HasMaxLength(20)
                .HasColumnName("ep_code_type");
            entity.Property(e => e.EpDate).HasColumnName("ep_date");
            entity.Property(e => e.EpEncounter)
                .HasColumnType("int(11)")
                .HasColumnName("ep_encounter");
            entity.Property(e => e.EpExternalId)
                .HasMaxLength(255)
                .HasColumnName("ep_external_id");
            entity.Property(e => e.EpFacilityId)
                .HasMaxLength(255)
                .HasColumnName("ep_facility_id");
            entity.Property(e => e.EpPid)
                .HasColumnType("int(11)")
                .HasColumnName("ep_pid");
        });

        modelBuilder.Entity<Facility>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("facility");

            entity.HasIndex(e => e.Uuid, "uuid").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.AcceptsAssignment)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("accepts_assignment");
            entity.Property(e => e.Attn)
                .HasMaxLength(65)
                .HasColumnName("attn");
            entity.Property(e => e.BillingLocation)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("billing_location");
            entity.Property(e => e.City)
                .HasMaxLength(255)
                .HasColumnName("city");
            entity.Property(e => e.Color)
                .HasMaxLength(7)
                .HasDefaultValueSql("''")
                .HasColumnName("color");
            entity.Property(e => e.CountryCode)
                .HasMaxLength(30)
                .HasDefaultValueSql("''")
                .HasColumnName("country_code");
            entity.Property(e => e.DateCreated)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("date_created");
            entity.Property(e => e.DomainIdentifier)
                .HasMaxLength(60)
                .HasColumnName("domain_identifier");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasColumnName("email");
            entity.Property(e => e.ExtraValidation)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("extra_validation");
            entity.Property(e => e.FacilityCode)
                .HasMaxLength(31)
                .HasColumnName("facility_code");
            entity.Property(e => e.FacilityNpi)
                .HasMaxLength(15)
                .HasColumnName("facility_npi");
            entity.Property(e => e.FacilityTaxonomy)
                .HasMaxLength(15)
                .HasColumnName("facility_taxonomy");
            entity.Property(e => e.Fax)
                .HasMaxLength(30)
                .HasColumnName("fax");
            entity.Property(e => e.FederalEin)
                .HasMaxLength(15)
                .HasColumnName("federal_ein");
            entity.Property(e => e.Iban)
                .HasMaxLength(50)
                .HasColumnName("iban");
            entity.Property(e => e.Inactive).HasColumnName("inactive");
            entity.Property(e => e.Info)
                .HasColumnType("text")
                .HasColumnName("info");
            entity.Property(e => e.LastUpdated)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("last_updated");
            entity.Property(e => e.MailCity)
                .HasMaxLength(50)
                .HasColumnName("mail_city");
            entity.Property(e => e.MailState)
                .HasMaxLength(3)
                .HasColumnName("mail_state");
            entity.Property(e => e.MailStreet)
                .HasMaxLength(30)
                .HasColumnName("mail_street");
            entity.Property(e => e.MailStreet2)
                .HasMaxLength(30)
                .HasColumnName("mail_street2");
            entity.Property(e => e.MailZip)
                .HasMaxLength(10)
                .HasColumnName("mail_zip");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.Oid)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasComment("HIEs CCDA and FHIR an OID is required/wanted")
                .HasColumnName("oid");
            entity.Property(e => e.OrganizationType)
                .HasMaxLength(50)
                .HasDefaultValueSql("'prov'")
                .HasComment("Organization type as defined by HL7 Value Set: OrganizationType")
                .HasColumnName("organization_type");
            entity.Property(e => e.Phone)
                .HasMaxLength(30)
                .HasColumnName("phone");
            entity.Property(e => e.PosCode)
                .HasColumnType("tinyint(4)")
                .HasColumnName("pos_code");
            entity.Property(e => e.PostalCode)
                .HasMaxLength(11)
                .HasColumnName("postal_code");
            entity.Property(e => e.PrimaryBusinessEntity)
                .HasDefaultValueSql("'1'")
                .HasComment("0-Not Set as business entity 1-Set as business entity")
                .HasColumnType("int(10)")
                .HasColumnName("primary_business_entity");
            entity.Property(e => e.ServiceLocation)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("service_location");
            entity.Property(e => e.State)
                .HasMaxLength(50)
                .HasColumnName("state");
            entity.Property(e => e.Street)
                .HasMaxLength(255)
                .HasColumnName("street");
            entity.Property(e => e.TaxIdType)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasColumnName("tax_id_type");
            entity.Property(e => e.Uuid)
                .HasMaxLength(16)
                .IsFixedLength()
                .HasColumnName("uuid");
            entity.Property(e => e.Website)
                .HasMaxLength(255)
                .HasColumnName("website");
            entity.Property(e => e.WenoId)
                .HasMaxLength(10)
                .HasColumnName("weno_id");
            entity.Property(e => e.X12SenderId)
                .HasMaxLength(25)
                .HasColumnName("x12_sender_id");
        });

        modelBuilder.Entity<FacilityUserId>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("facility_user_ids");

            entity.HasIndex(e => new { e.Uid, e.FacilityId, e.FieldId }, "uid");

            entity.HasIndex(e => e.Uuid, "uuid");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.DateCreated)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("date_created");
            entity.Property(e => e.FacilityId)
                .HasColumnType("bigint(20)")
                .HasColumnName("facility_id");
            entity.Property(e => e.FieldId)
                .HasMaxLength(31)
                .HasComment("references layout_options.field_id")
                .HasColumnName("field_id");
            entity.Property(e => e.FieldValue)
                .HasColumnType("text")
                .HasColumnName("field_value");
            entity.Property(e => e.LastUpdated)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("last_updated");
            entity.Property(e => e.Uid)
                .HasColumnType("bigint(20)")
                .HasColumnName("uid");
            entity.Property(e => e.Uuid)
                .HasMaxLength(16)
                .IsFixedLength()
                .HasColumnName("uuid");
        });

        modelBuilder.Entity<FeeSchedule>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("fee_schedule");

            entity.HasIndex(e => new { e.InsuranceCompanyId, e.Plan, e.Code, e.Modifier, e.Type, e.EffectiveDate }, "ins_plan_code_mod_type_date").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Code)
                .HasMaxLength(10)
                .HasDefaultValueSql("''")
                .HasColumnName("code");
            entity.Property(e => e.EffectiveDate).HasColumnName("effective_date");
            entity.Property(e => e.Fee)
                .HasPrecision(12, 2)
                .HasColumnName("fee");
            entity.Property(e => e.InsuranceCompanyId)
                .HasColumnType("int(11)")
                .HasColumnName("insurance_company_id");
            entity.Property(e => e.Modifier)
                .HasMaxLength(2)
                .HasDefaultValueSql("''")
                .HasColumnName("modifier");
            entity.Property(e => e.Plan)
                .HasMaxLength(20)
                .HasDefaultValueSql("''")
                .HasColumnName("plan");
            entity.Property(e => e.Type)
                .HasMaxLength(20)
                .HasDefaultValueSql("''")
                .HasColumnName("type");
        });

        modelBuilder.Entity<FeeSheetOption>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("fee_sheet_options");

            entity.Property(e => e.FsCategory)
                .HasMaxLength(63)
                .HasColumnName("fs_category");
            entity.Property(e => e.FsCodes)
                .HasMaxLength(255)
                .HasColumnName("fs_codes");
            entity.Property(e => e.FsOption)
                .HasMaxLength(63)
                .HasColumnName("fs_option");
        });

        modelBuilder.Entity<Form>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("forms");

            entity.HasIndex(e => e.FormId, "form_id");

            entity.HasIndex(e => new { e.Pid, e.Encounter }, "pid_encounter");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Authorized)
                .HasColumnType("tinyint(4)")
                .HasColumnName("authorized");
            entity.Property(e => e.Date)
                .HasColumnType("datetime")
                .HasColumnName("date");
            entity.Property(e => e.Deleted)
                .HasComment("flag indicates form has been deleted")
                .HasColumnType("tinyint(4)")
                .HasColumnName("deleted");
            entity.Property(e => e.Encounter)
                .HasColumnType("bigint(20)")
                .HasColumnName("encounter");
            entity.Property(e => e.FormId)
                .HasColumnType("bigint(20)")
                .HasColumnName("form_id");
            entity.Property(e => e.FormName).HasColumnName("form_name");
            entity.Property(e => e.Formdir).HasColumnName("formdir");
            entity.Property(e => e.Groupname)
                .HasMaxLength(255)
                .HasColumnName("groupname");
            entity.Property(e => e.IssueId)
                .HasComment("references lists.id to identify a case")
                .HasColumnType("bigint(20)")
                .HasColumnName("issue_id");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.ProviderId)
                .HasComment("references users.id to identify a provider")
                .HasColumnType("bigint(20)")
                .HasColumnName("provider_id");
            entity.Property(e => e.TherapyGroupId)
                .HasColumnType("int(11)")
                .HasColumnName("therapy_group_id");
            entity.Property(e => e.User)
                .HasMaxLength(255)
                .HasColumnName("user");
        });

        modelBuilder.Entity<FormCarePlan>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("form_care_plan");

            entity.HasIndex(e => new { e.PlanStatus, e.Date, e.DateEnd }, "idx_status_date");

            entity.Property(e => e.Activity)
                .HasColumnType("tinyint(4)")
                .HasColumnName("activity");
            entity.Property(e => e.Authorized)
                .HasColumnType("tinyint(4)")
                .HasColumnName("authorized");
            entity.Property(e => e.CarePlanType)
                .HasMaxLength(30)
                .HasColumnName("care_plan_type");
            entity.Property(e => e.Code)
                .HasMaxLength(255)
                .HasColumnName("code");
            entity.Property(e => e.Codetext)
                .HasColumnType("text")
                .HasColumnName("codetext");
            entity.Property(e => e.Date)
                .HasColumnType("datetime")
                .HasColumnName("date");
            entity.Property(e => e.DateEnd)
                .HasColumnType("datetime")
                .HasColumnName("date_end");
            entity.Property(e => e.Description)
                .HasColumnType("text")
                .HasColumnName("description");
            entity.Property(e => e.Encounter)
                .HasMaxLength(255)
                .HasColumnName("encounter");
            entity.Property(e => e.ExternalId)
                .HasMaxLength(30)
                .HasColumnName("external_id");
            entity.Property(e => e.Groupname)
                .HasMaxLength(255)
                .HasColumnName("groupname");
            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.NoteRelatedTo)
                .HasColumnType("text")
                .HasColumnName("note_related_to");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.PlanEngagementCategory)
                .HasMaxLength(100)
                .HasDefaultValueSql("''")
                .HasComment("Expected engagement category with the patient based upon the care plan type")
                .HasColumnName("plan_engagement_category");
            entity.Property(e => e.PlanStatus)
                .HasMaxLength(32)
                .HasComment("Care Plan status (e.g., draft, active, completed, etc)")
                .HasColumnName("plan_status");
            entity.Property(e => e.ProposedDate)
                .HasComment("Target or Achieve-by date for the goal")
                .HasColumnType("datetime")
                .HasColumnName("proposed_date");
            entity.Property(e => e.ReasonCode)
                .HasMaxLength(31)
                .HasColumnName("reason_code");
            entity.Property(e => e.ReasonDateHigh)
                .HasComment("The date the explanation reason for the care plan entry value ends")
                .HasColumnType("datetime")
                .HasColumnName("reason_date_high");
            entity.Property(e => e.ReasonDateLow)
                .HasComment("The date the reason was recorded")
                .HasColumnType("datetime")
                .HasColumnName("reason_date_low");
            entity.Property(e => e.ReasonDescription)
                .HasColumnType("text")
                .HasColumnName("reason_description");
            entity.Property(e => e.ReasonStatus)
                .HasMaxLength(31)
                .HasColumnName("reason_status");
            entity.Property(e => e.User)
                .HasMaxLength(255)
                .HasColumnName("user");
        });

        modelBuilder.Entity<FormClinicalInstruction>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("form_clinical_instructions");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.Activity)
                .HasDefaultValueSql("'1'")
                .HasColumnType("tinyint(4)")
                .HasColumnName("activity");
            entity.Property(e => e.Date)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("timestamp")
                .HasColumnName("date");
            entity.Property(e => e.Encounter)
                .HasMaxLength(255)
                .HasColumnName("encounter");
            entity.Property(e => e.Instruction)
                .HasColumnType("text")
                .HasColumnName("instruction");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.User)
                .HasMaxLength(255)
                .HasColumnName("user");
        });

        modelBuilder.Entity<FormClinicalNote>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("form_clinical_notes");

            entity.HasIndex(e => e.Uuid, "uuid").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Activity)
                .HasColumnType("tinyint(4)")
                .HasColumnName("activity");
            entity.Property(e => e.Authorized)
                .HasColumnType("tinyint(4)")
                .HasColumnName("authorized");
            entity.Property(e => e.ClinicalNotesCategory)
                .HasMaxLength(100)
                .HasColumnName("clinical_notes_category");
            entity.Property(e => e.ClinicalNotesType)
                .HasMaxLength(100)
                .HasColumnName("clinical_notes_type");
            entity.Property(e => e.Code)
                .HasMaxLength(255)
                .HasColumnName("code");
            entity.Property(e => e.Codetext)
                .HasColumnType("text")
                .HasColumnName("codetext");
            entity.Property(e => e.Date).HasColumnName("date");
            entity.Property(e => e.Description)
                .HasColumnType("text")
                .HasColumnName("description");
            entity.Property(e => e.Encounter)
                .HasMaxLength(255)
                .HasColumnName("encounter");
            entity.Property(e => e.ExternalId)
                .HasMaxLength(30)
                .HasColumnName("external_id");
            entity.Property(e => e.FormId)
                .HasColumnType("bigint(20)")
                .HasColumnName("form_id");
            entity.Property(e => e.Groupname)
                .HasMaxLength(255)
                .HasColumnName("groupname");
            entity.Property(e => e.LastUpdated)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("last_updated");
            entity.Property(e => e.NoteRelatedTo)
                .HasColumnType("text")
                .HasColumnName("note_related_to");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.User)
                .HasMaxLength(255)
                .HasColumnName("user");
            entity.Property(e => e.Uuid)
                .HasMaxLength(16)
                .IsFixedLength()
                .HasColumnName("uuid");
        });

        modelBuilder.Entity<FormDictation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("form_dictation");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Activity)
                .HasColumnType("tinyint(4)")
                .HasColumnName("activity");
            entity.Property(e => e.AdditionalNotes).HasColumnName("additional_notes");
            entity.Property(e => e.Authorized)
                .HasColumnType("tinyint(4)")
                .HasColumnName("authorized");
            entity.Property(e => e.Date)
                .HasColumnType("datetime")
                .HasColumnName("date");
            entity.Property(e => e.Dictation).HasColumnName("dictation");
            entity.Property(e => e.Groupname)
                .HasMaxLength(255)
                .HasColumnName("groupname");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.User)
                .HasMaxLength(255)
                .HasColumnName("user");
        });

        modelBuilder.Entity<FormEncounter>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("form_encounter");

            entity.HasIndex(e => e.Date, "encounter_date");

            entity.HasIndex(e => new { e.Pid, e.Encounter }, "pid_encounter");

            entity.HasIndex(e => e.Uuid, "uuid").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.BillingFacility)
                .HasColumnType("int(11)")
                .HasColumnName("billing_facility");
            entity.Property(e => e.BillingNote)
                .HasColumnType("text")
                .HasColumnName("billing_note");
            entity.Property(e => e.ClassCode)
                .HasMaxLength(10)
                .HasDefaultValueSql("'AMB'")
                .HasColumnName("class_code");
            entity.Property(e => e.Date)
                .HasColumnType("datetime")
                .HasColumnName("date");
            entity.Property(e => e.DateEnd)
                .HasColumnType("datetime")
                .HasColumnName("date_end");
            entity.Property(e => e.DischargeDisposition)
                .HasMaxLength(100)
                .HasColumnName("discharge_disposition");
            entity.Property(e => e.Encounter)
                .HasColumnType("bigint(20)")
                .HasColumnName("encounter");
            entity.Property(e => e.EncounterTypeCode)
                .HasMaxLength(31)
                .HasComment("not all types are categories")
                .HasColumnName("encounter_type_code");
            entity.Property(e => e.EncounterTypeDescription)
                .HasColumnType("text")
                .HasColumnName("encounter_type_description");
            entity.Property(e => e.ExternalId)
                .HasMaxLength(20)
                .HasColumnName("external_id");
            entity.Property(e => e.Facility).HasColumnName("facility");
            entity.Property(e => e.FacilityId)
                .HasColumnType("int(11)")
                .HasColumnName("facility_id");
            entity.Property(e => e.InCollection).HasColumnName("in_collection");
            entity.Property(e => e.InvoiceRefno)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasColumnName("invoice_refno");
            entity.Property(e => e.LastLevelBilled)
                .HasComment("0=none, 1=ins1, 2=ins2, etc")
                .HasColumnType("int(11)")
                .HasColumnName("last_level_billed");
            entity.Property(e => e.LastLevelClosed)
                .HasComment("0=none, 1=ins1, 2=ins2, etc")
                .HasColumnType("int(11)")
                .HasColumnName("last_level_closed");
            entity.Property(e => e.LastStmtDate).HasColumnName("last_stmt_date");
            entity.Property(e => e.LastUpdate)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("timestamp")
                .HasColumnName("last_update");
            entity.Property(e => e.OnsetDate)
                .HasColumnType("datetime")
                .HasColumnName("onset_date");
            entity.Property(e => e.OrderingProviderId)
                .HasDefaultValueSql("'0'")
                .HasComment("referring provider, if any, for this visit")
                .HasColumnType("int(11)")
                .HasColumnName("ordering_provider_id");
            entity.Property(e => e.ParentEncounterId)
                .HasColumnType("bigint(20)")
                .HasColumnName("parent_encounter_id");
            entity.Property(e => e.PcCatid)
                .HasDefaultValueSql("'5'")
                .HasComment("event category from openemr_postcalendar_categories")
                .HasColumnType("int(11)")
                .HasColumnName("pc_catid");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.PosCode)
                .HasColumnType("tinyint(4)")
                .HasColumnName("pos_code");
            entity.Property(e => e.ProviderId)
                .HasDefaultValueSql("'0'")
                .HasComment("default and main provider for this visit")
                .HasColumnType("int(11)")
                .HasColumnName("provider_id");
            entity.Property(e => e.Reason).HasColumnName("reason");
            entity.Property(e => e.ReferralSource)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasColumnName("referral_source");
            entity.Property(e => e.ReferringProviderId)
                .HasDefaultValueSql("'0'")
                .HasComment("referring provider, if any, for this visit")
                .HasColumnType("int(11)")
                .HasColumnName("referring_provider_id");
            entity.Property(e => e.Sensitivity)
                .HasMaxLength(30)
                .HasColumnName("sensitivity");
            entity.Property(e => e.Shift)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasColumnName("shift");
            entity.Property(e => e.StmtCount)
                .HasColumnType("int(11)")
                .HasColumnName("stmt_count");
            entity.Property(e => e.SupervisorId)
                .HasDefaultValueSql("'0'")
                .HasComment("supervising provider, if any, for this visit")
                .HasColumnType("int(11)")
                .HasColumnName("supervisor_id");
            entity.Property(e => e.Uuid)
                .HasMaxLength(16)
                .IsFixedLength()
                .HasColumnName("uuid");
            entity.Property(e => e.VoucherNumber)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasComment("also called referral number")
                .HasColumnName("voucher_number");
        });

        modelBuilder.Entity<FormEyeAcuity>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("form_eye_acuity");

            entity.HasIndex(e => new { e.Id, e.Pid }, "id_pid").IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasComment("Links to forms.form_id")
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Arodva)
                .HasMaxLength(25)
                .HasColumnName("ARODVA");
            entity.Property(e => e.Arosva)
                .HasMaxLength(25)
                .HasColumnName("AROSVA");
            entity.Property(e => e.Binocva)
                .HasMaxLength(25)
                .HasColumnName("BINOCVA");
            entity.Property(e => e.Crodva)
                .HasMaxLength(25)
                .HasColumnName("CRODVA");
            entity.Property(e => e.Crosva)
                .HasMaxLength(25)
                .HasColumnName("CROSVA");
            entity.Property(e => e.Ctlodva)
                .HasMaxLength(25)
                .HasColumnName("CTLODVA");
            entity.Property(e => e.Ctlodva1)
                .HasMaxLength(25)
                .HasColumnName("CTLODVA1");
            entity.Property(e => e.Ctlosva)
                .HasMaxLength(25)
                .HasColumnName("CTLOSVA");
            entity.Property(e => e.Ctlosva1)
                .HasMaxLength(25)
                .HasColumnName("CTLOSVA1");
            entity.Property(e => e.Glarecomments)
                .HasMaxLength(255)
                .HasColumnName("GLARECOMMENTS");
            entity.Property(e => e.Glareodva)
                .HasMaxLength(25)
                .HasColumnName("GLAREODVA");
            entity.Property(e => e.Glareosva)
                .HasMaxLength(25)
                .HasColumnName("GLAREOSVA");
            entity.Property(e => e.Liodva)
                .HasMaxLength(25)
                .HasColumnName("LIODVA");
            entity.Property(e => e.Liosva)
                .HasMaxLength(25)
                .HasColumnName("LIOSVA");
            entity.Property(e => e.Mrnearodva)
                .HasMaxLength(25)
                .HasColumnName("MRNEARODVA");
            entity.Property(e => e.Mrnearosva)
                .HasMaxLength(25)
                .HasColumnName("MRNEAROSVA");
            entity.Property(e => e.Mrodva)
                .HasMaxLength(25)
                .HasColumnName("MRODVA");
            entity.Property(e => e.Mrosva)
                .HasMaxLength(25)
                .HasColumnName("MROSVA");
            entity.Property(e => e.Osvanearcc)
                .HasMaxLength(25)
                .HasColumnName("OSVANEARCC");
            entity.Property(e => e.Pamodva)
                .HasMaxLength(25)
                .HasColumnName("PAMODVA");
            entity.Property(e => e.Pamosva)
                .HasMaxLength(25)
                .HasColumnName("PAMOSVA");
            entity.Property(e => e.Phodva)
                .HasMaxLength(25)
                .HasColumnName("PHODVA");
            entity.Property(e => e.Phosva)
                .HasMaxLength(25)
                .HasColumnName("PHOSVA");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.Scnearodva)
                .HasMaxLength(25)
                .HasColumnName("SCNEARODVA");
            entity.Property(e => e.Scnearosva)
                .HasMaxLength(25)
                .HasColumnName("SCNEAROSVA");
            entity.Property(e => e.Scodva)
                .HasMaxLength(25)
                .HasColumnName("SCODVA");
            entity.Property(e => e.Scosva)
                .HasMaxLength(25)
                .HasColumnName("SCOSVA");
            entity.Property(e => e.Wodvanear)
                .HasMaxLength(25)
                .HasColumnName("WODVANEAR");
        });

        modelBuilder.Entity<FormEyeAntseg>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("form_eye_antseg");

            entity.HasIndex(e => new { e.Id, e.Pid }, "id_pid").IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasComment("Links to forms.form_id")
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.AntsegComments)
                .HasColumnType("text")
                .HasColumnName("ANTSEG_COMMENTS");
            entity.Property(e => e.Dimodpupilreactivity)
                .HasMaxLength(25)
                .HasColumnName("DIMODPUPILREACTIVITY");
            entity.Property(e => e.Dimodpupilsize1)
                .HasMaxLength(25)
                .HasColumnName("DIMODPUPILSIZE1");
            entity.Property(e => e.Dimodpupilsize2)
                .HasMaxLength(25)
                .HasColumnName("DIMODPUPILSIZE2");
            entity.Property(e => e.Dimospupilreactivity)
                .HasMaxLength(25)
                .HasColumnName("DIMOSPUPILREACTIVITY");
            entity.Property(e => e.Dimospupilsize1)
                .HasMaxLength(25)
                .HasColumnName("DIMOSPUPILSIZE1");
            entity.Property(e => e.Dimospupilsize2)
                .HasMaxLength(25)
                .HasColumnName("DIMOSPUPILSIZE2");
            entity.Property(e => e.Odac)
                .HasColumnType("text")
                .HasColumnName("ODAC");
            entity.Property(e => e.Odapd)
                .HasMaxLength(25)
                .HasColumnName("ODAPD");
            entity.Property(e => e.Odconj)
                .HasColumnType("text")
                .HasColumnName("ODCONJ");
            entity.Property(e => e.Odcornea)
                .HasColumnType("text")
                .HasColumnName("ODCORNEA");
            entity.Property(e => e.Odgonio)
                .HasMaxLength(25)
                .HasColumnName("ODGONIO");
            entity.Property(e => e.Odiris)
                .HasColumnType("text")
                .HasColumnName("ODIRIS");
            entity.Property(e => e.Odkthickness)
                .HasMaxLength(25)
                .HasColumnName("ODKTHICKNESS");
            entity.Property(e => e.Odlens)
                .HasColumnType("text")
                .HasColumnName("ODLENS");
            entity.Property(e => e.Odpupilreactivity)
                .HasMaxLength(25)
                .IsFixedLength()
                .HasColumnName("ODPUPILREACTIVITY");
            entity.Property(e => e.Odpupilsize1)
                .HasMaxLength(25)
                .HasColumnName("ODPUPILSIZE1");
            entity.Property(e => e.Odpupilsize2)
                .HasMaxLength(25)
                .HasColumnName("ODPUPILSIZE2");
            entity.Property(e => e.Odschirmer1)
                .HasMaxLength(25)
                .HasColumnName("ODSCHIRMER1");
            entity.Property(e => e.Odschirmer2)
                .HasMaxLength(25)
                .HasColumnName("ODSCHIRMER2");
            entity.Property(e => e.Odtbut)
                .HasMaxLength(25)
                .HasColumnName("ODTBUT");
            entity.Property(e => e.Osac)
                .HasColumnType("text")
                .HasColumnName("OSAC");
            entity.Property(e => e.Osapd)
                .HasMaxLength(25)
                .HasColumnName("OSAPD");
            entity.Property(e => e.Osconj)
                .HasColumnType("text")
                .HasColumnName("OSCONJ");
            entity.Property(e => e.Oscornea)
                .HasColumnType("text")
                .HasColumnName("OSCORNEA");
            entity.Property(e => e.Osgonio)
                .HasMaxLength(25)
                .HasColumnName("OSGONIO");
            entity.Property(e => e.Osiris)
                .HasColumnType("text")
                .HasColumnName("OSIRIS");
            entity.Property(e => e.Oskthickness)
                .HasMaxLength(25)
                .HasColumnName("OSKTHICKNESS");
            entity.Property(e => e.Oslens)
                .HasColumnType("text")
                .HasColumnName("OSLENS");
            entity.Property(e => e.Ospupilreactivity)
                .HasMaxLength(25)
                .IsFixedLength()
                .HasColumnName("OSPUPILREACTIVITY");
            entity.Property(e => e.Ospupilsize1)
                .HasMaxLength(25)
                .HasColumnName("OSPUPILSIZE1");
            entity.Property(e => e.Ospupilsize2)
                .HasMaxLength(25)
                .HasColumnName("OSPUPILSIZE2");
            entity.Property(e => e.Osschirmer1)
                .HasMaxLength(25)
                .HasColumnName("OSSCHIRMER1");
            entity.Property(e => e.Osschirmer2)
                .HasMaxLength(25)
                .HasColumnName("OSSCHIRMER2");
            entity.Property(e => e.Ostbut)
                .HasMaxLength(25)
                .HasColumnName("OSTBUT");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.PupilComments)
                .HasColumnType("text")
                .HasColumnName("PUPIL_COMMENTS");
            entity.Property(e => e.PupilNormal)
                .HasMaxLength(2)
                .HasDefaultValueSql("'1'")
                .HasColumnName("PUPIL_NORMAL");
        });

        modelBuilder.Entity<FormEyeBase>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("form_eye_base");

            entity.Property(e => e.Id)
                .HasComment("Links to forms.form_id")
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Activity)
                .HasColumnType("tinyint(4)")
                .HasColumnName("activity");
            entity.Property(e => e.Authorized)
                .HasColumnType("tinyint(4)")
                .HasColumnName("authorized");
            entity.Property(e => e.Date)
                .HasColumnType("datetime")
                .HasColumnName("date");
            entity.Property(e => e.Groupname)
                .HasMaxLength(255)
                .HasColumnName("groupname");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.User)
                .HasMaxLength(255)
                .HasColumnName("user");
        });

        modelBuilder.Entity<FormEyeBiometric>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("form_eye_biometrics");

            entity.HasIndex(e => new { e.Id, e.Pid }, "id_pid").IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasComment("Links to forms.form_id")
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Odacd)
                .HasMaxLength(20)
                .HasColumnName("ODACD");
            entity.Property(e => e.Odaxiallength)
                .HasMaxLength(20)
                .HasColumnName("ODAXIALLENGTH");
            entity.Property(e => e.Odk1)
                .HasMaxLength(10)
                .HasColumnName("ODK1");
            entity.Property(e => e.Odk2)
                .HasMaxLength(10)
                .HasColumnName("ODK2");
            entity.Property(e => e.Odk2axis)
                .HasMaxLength(10)
                .HasColumnName("ODK2AXIS");
            entity.Property(e => e.Odlt)
                .HasMaxLength(20)
                .HasColumnName("ODLT");
            entity.Property(e => e.Odpdmeasured)
                .HasMaxLength(20)
                .HasColumnName("ODPDMeasured");
            entity.Property(e => e.Odw2w)
                .HasMaxLength(20)
                .HasColumnName("ODW2W");
            entity.Property(e => e.Osacd)
                .HasMaxLength(20)
                .HasColumnName("OSACD");
            entity.Property(e => e.Osaxiallength)
                .HasMaxLength(20)
                .HasColumnName("OSAXIALLENGTH");
            entity.Property(e => e.Osk1)
                .HasMaxLength(10)
                .HasColumnName("OSK1");
            entity.Property(e => e.Osk2)
                .HasMaxLength(10)
                .HasColumnName("OSK2");
            entity.Property(e => e.Osk2axis)
                .HasMaxLength(10)
                .HasColumnName("OSK2AXIS");
            entity.Property(e => e.Oslt)
                .HasMaxLength(20)
                .HasColumnName("OSLT");
            entity.Property(e => e.Ospdmeasured)
                .HasMaxLength(20)
                .HasColumnName("OSPDMeasured");
            entity.Property(e => e.Osw2w)
                .HasMaxLength(20)
                .HasColumnName("OSW2W");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
        });

        modelBuilder.Entity<FormEyeExternal>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("form_eye_external");

            entity.HasIndex(e => new { e.Id, e.Pid }, "id_pid").IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasComment("Links to forms.form_id")
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.ExtComments)
                .HasColumnType("text")
                .HasColumnName("EXT_COMMENTS");
            entity.Property(e => e.Hertelbase)
                .HasMaxLength(25)
                .HasColumnName("HERTELBASE");
            entity.Property(e => e.Ladnexa)
                .HasColumnType("text")
                .HasColumnName("LADNEXA");
            entity.Property(e => e.Lbrow)
                .HasColumnType("text")
                .HasColumnName("LBROW");
            entity.Property(e => e.Lcarotid)
                .HasColumnType("text")
                .HasColumnName("LCAROTID");
            entity.Property(e => e.Lcnv)
                .HasColumnType("text")
                .HasColumnName("LCNV");
            entity.Property(e => e.Lcnvii)
                .HasColumnType("text")
                .HasColumnName("LCNVII");
            entity.Property(e => e.Llf)
                .HasMaxLength(25)
                .HasColumnName("LLF");
            entity.Property(e => e.Lll)
                .HasColumnType("text")
                .HasColumnName("LLL");
            entity.Property(e => e.Lmct)
                .HasColumnType("text")
                .HasColumnName("LMCT");
            entity.Property(e => e.Lmrd)
                .HasMaxLength(25)
                .HasColumnName("LMRD");
            entity.Property(e => e.Ltempart)
                .HasColumnType("text")
                .HasColumnName("LTEMPART");
            entity.Property(e => e.Lul)
                .HasColumnType("text")
                .HasColumnName("LUL");
            entity.Property(e => e.Lvfissure)
                .HasMaxLength(25)
                .HasColumnName("LVFISSURE");
            entity.Property(e => e.Odhertel)
                .HasMaxLength(25)
                .HasColumnName("ODHERTEL");
            entity.Property(e => e.Oshertel)
                .HasMaxLength(25)
                .HasColumnName("OSHERTEL");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.Radnexa)
                .HasColumnType("text")
                .HasColumnName("RADNEXA");
            entity.Property(e => e.Rbrow)
                .HasColumnType("text")
                .HasColumnName("RBROW");
            entity.Property(e => e.Rcarotid)
                .HasColumnType("text")
                .HasColumnName("RCAROTID");
            entity.Property(e => e.Rcnv)
                .HasColumnType("text")
                .HasColumnName("RCNV");
            entity.Property(e => e.Rcnvii)
                .HasColumnType("text")
                .HasColumnName("RCNVII");
            entity.Property(e => e.Rlf)
                .HasMaxLength(25)
                .HasColumnName("RLF");
            entity.Property(e => e.Rll)
                .HasColumnType("text")
                .HasColumnName("RLL");
            entity.Property(e => e.Rmct)
                .HasColumnType("text")
                .HasColumnName("RMCT");
            entity.Property(e => e.Rmrd)
                .HasMaxLength(25)
                .HasColumnName("RMRD");
            entity.Property(e => e.Rtempart)
                .HasColumnType("text")
                .HasColumnName("RTEMPART");
            entity.Property(e => e.Rul)
                .HasColumnType("text")
                .HasColumnName("RUL");
            entity.Property(e => e.Rvfissure)
                .HasMaxLength(25)
                .HasColumnName("RVFISSURE");
        });

        modelBuilder.Entity<FormEyeHpi>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("form_eye_hpi");

            entity.HasIndex(e => new { e.Id, e.Pid }, "id_pid").IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasComment("Links to forms.form_id")
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Associated1)
                .HasMaxLength(255)
                .HasColumnName("ASSOCIATED1");
            entity.Property(e => e.Associated2)
                .HasColumnType("text")
                .HasColumnName("ASSOCIATED2");
            entity.Property(e => e.Associated3)
                .HasColumnType("text")
                .HasColumnName("ASSOCIATED3");
            entity.Property(e => e.Cc1)
                .HasMaxLength(255)
                .HasColumnName("CC1");
            entity.Property(e => e.Cc2)
                .HasColumnType("text")
                .HasColumnName("CC2");
            entity.Property(e => e.Cc3)
                .HasColumnType("text")
                .HasColumnName("CC3");
            entity.Property(e => e.Chronic1)
                .HasMaxLength(255)
                .HasColumnName("CHRONIC1");
            entity.Property(e => e.Chronic2)
                .HasMaxLength(255)
                .HasColumnName("CHRONIC2");
            entity.Property(e => e.Chronic3)
                .HasMaxLength(255)
                .HasColumnName("CHRONIC3");
            entity.Property(e => e.Context1)
                .HasMaxLength(255)
                .HasColumnName("CONTEXT1");
            entity.Property(e => e.Context2)
                .HasColumnType("text")
                .HasColumnName("CONTEXT2");
            entity.Property(e => e.Context3)
                .HasColumnType("text")
                .HasColumnName("CONTEXT3");
            entity.Property(e => e.Duration1)
                .HasMaxLength(255)
                .HasColumnName("DURATION1");
            entity.Property(e => e.Duration2)
                .HasColumnType("text")
                .HasColumnName("DURATION2");
            entity.Property(e => e.Duration3)
                .HasColumnType("text")
                .HasColumnName("DURATION3");
            entity.Property(e => e.Hpi1)
                .HasColumnType("text")
                .HasColumnName("HPI1");
            entity.Property(e => e.Hpi2)
                .HasColumnType("text")
                .HasColumnName("HPI2");
            entity.Property(e => e.Hpi3)
                .HasColumnType("text")
                .HasColumnName("HPI3");
            entity.Property(e => e.Location1)
                .HasMaxLength(255)
                .HasColumnName("LOCATION1");
            entity.Property(e => e.Location2)
                .HasColumnType("text")
                .HasColumnName("LOCATION2");
            entity.Property(e => e.Location3)
                .HasColumnType("text")
                .HasColumnName("LOCATION3");
            entity.Property(e => e.Modify1)
                .HasMaxLength(255)
                .HasColumnName("MODIFY1");
            entity.Property(e => e.Modify2)
                .HasColumnType("text")
                .HasColumnName("MODIFY2");
            entity.Property(e => e.Modify3)
                .HasColumnType("text")
                .HasColumnName("MODIFY3");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.Quality1)
                .HasMaxLength(255)
                .HasColumnName("QUALITY1");
            entity.Property(e => e.Quality2)
                .HasColumnType("text")
                .HasColumnName("QUALITY2");
            entity.Property(e => e.Quality3)
                .HasColumnType("text")
                .HasColumnName("QUALITY3");
            entity.Property(e => e.Severity1)
                .HasMaxLength(255)
                .HasColumnName("SEVERITY1");
            entity.Property(e => e.Severity2)
                .HasColumnType("text")
                .HasColumnName("SEVERITY2");
            entity.Property(e => e.Severity3)
                .HasColumnType("text")
                .HasColumnName("SEVERITY3");
            entity.Property(e => e.Timing1)
                .HasMaxLength(255)
                .HasColumnName("TIMING1");
            entity.Property(e => e.Timing2)
                .HasColumnType("text")
                .HasColumnName("TIMING2");
            entity.Property(e => e.Timing3)
                .HasColumnType("text")
                .HasColumnName("TIMING3");
        });

        modelBuilder.Entity<FormEyeLocking>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("form_eye_locking");

            entity.HasIndex(e => new { e.Id, e.Pid }, "id_pid").IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasComment("Links to forms.form_id")
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Imp)
                .HasColumnType("text")
                .HasColumnName("IMP");
            entity.Property(e => e.Locked)
                .HasMaxLength(3)
                .HasColumnName("LOCKED");
            entity.Property(e => e.Lockedby)
                .HasMaxLength(50)
                .HasColumnName("LOCKEDBY");
            entity.Property(e => e.Lockeddate)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("timestamp")
                .HasColumnName("LOCKEDDATE");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.Plan)
                .HasColumnType("text")
                .HasColumnName("PLAN");
            entity.Property(e => e.Resource).HasMaxLength(50);
            entity.Property(e => e.Technician).HasMaxLength(50);
        });

        modelBuilder.Entity<FormEyeMagDispense>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("form_eye_mag_dispense");

            entity.HasIndex(e => new { e.Pid, e.Encounter, e.Id }, "pid").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Activity)
                .HasColumnType("tinyint(4)")
                .HasColumnName("activity");
            entity.Property(e => e.Authorized)
                .HasColumnType("tinyint(4)")
                .HasColumnName("authorized");
            entity.Property(e => e.Bpdd)
                .HasMaxLength(20)
                .HasColumnName("BPDD");
            entity.Property(e => e.Bpdn)
                .HasMaxLength(20)
                .HasColumnName("BPDN");
            entity.Property(e => e.Comments)
                .HasColumnType("text")
                .HasColumnName("COMMENTS");
            entity.Property(e => e.Ctlbrandod)
                .HasMaxLength(50)
                .HasColumnName("CTLBRANDOD");
            entity.Property(e => e.Ctlbrandos)
                .HasMaxLength(50)
                .HasColumnName("CTLBRANDOS");
            entity.Property(e => e.Ctlmanufacturerod)
                .HasMaxLength(25)
                .HasColumnName("CTLMANUFACTUREROD");
            entity.Property(e => e.Ctlmanufactureros)
                .HasMaxLength(25)
                .HasColumnName("CTLMANUFACTUREROS");
            entity.Property(e => e.Ctlodquantity)
                .HasMaxLength(255)
                .HasColumnName("CTLODQUANTITY");
            entity.Property(e => e.Ctlosquantity)
                .HasMaxLength(255)
                .HasColumnName("CTLOSQUANTITY");
            entity.Property(e => e.Ctlsupplierod)
                .HasMaxLength(25)
                .HasColumnName("CTLSUPPLIEROD");
            entity.Property(e => e.Ctlsupplieros)
                .HasMaxLength(25)
                .HasColumnName("CTLSUPPLIEROS");
            entity.Property(e => e.Date)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("timestamp")
                .HasColumnName("date");
            entity.Property(e => e.Encounter)
                .HasColumnType("bigint(20)")
                .HasColumnName("encounter");
            entity.Property(e => e.Groupname)
                .HasMaxLength(255)
                .HasColumnName("groupname");
            entity.Property(e => e.LensMaterial)
                .HasMaxLength(20)
                .HasColumnName("LENS_MATERIAL");
            entity.Property(e => e.LensTreatments)
                .HasMaxLength(100)
                .HasColumnName("LENS_TREATMENTS");
            entity.Property(e => e.Odadd)
                .HasMaxLength(10)
                .HasColumnName("ODADD");
            entity.Property(e => e.Odaxis)
                .HasMaxLength(10)
                .HasColumnName("ODAXIS");
            entity.Property(e => e.Odbc)
                .HasMaxLength(50)
                .HasColumnName("ODBC");
            entity.Property(e => e.Odcyl)
                .HasMaxLength(10)
                .HasColumnName("ODCYL");
            entity.Property(e => e.Oddiam)
                .HasMaxLength(50)
                .HasColumnName("ODDIAM");
            entity.Property(e => e.Odhbase)
                .HasMaxLength(20)
                .HasColumnName("ODHBASE");
            entity.Property(e => e.Odhpd)
                .HasMaxLength(20)
                .HasColumnName("ODHPD");
            entity.Property(e => e.Odmidadd)
                .HasMaxLength(10)
                .HasColumnName("ODMIDADD");
            entity.Property(e => e.Odmpdd)
                .HasMaxLength(20)
                .HasColumnName("ODMPDD");
            entity.Property(e => e.Odmpdn)
                .HasMaxLength(20)
                .HasColumnName("ODMPDN");
            entity.Property(e => e.Odslaboff)
                .HasMaxLength(20)
                .HasColumnName("ODSLABOFF");
            entity.Property(e => e.Odsph)
                .HasMaxLength(10)
                .HasColumnName("ODSPH");
            entity.Property(e => e.Odvbase)
                .HasMaxLength(20)
                .HasColumnName("ODVBASE");
            entity.Property(e => e.Odvertexdist)
                .HasMaxLength(20)
                .HasColumnName("ODVERTEXDIST");
            entity.Property(e => e.Odvpd)
                .HasMaxLength(20)
                .HasColumnName("ODVPD");
            entity.Property(e => e.Osadd)
                .HasMaxLength(10)
                .HasColumnName("OSADD");
            entity.Property(e => e.Osaxis)
                .HasMaxLength(10)
                .HasColumnName("OSAXIS");
            entity.Property(e => e.Osbc)
                .HasMaxLength(50)
                .HasColumnName("OSBC");
            entity.Property(e => e.Oscyl)
                .HasMaxLength(10)
                .HasColumnName("OSCYL");
            entity.Property(e => e.Osdiam)
                .HasMaxLength(50)
                .HasColumnName("OSDIAM");
            entity.Property(e => e.Oshbase)
                .HasMaxLength(20)
                .HasColumnName("OSHBASE");
            entity.Property(e => e.Oshpd)
                .HasMaxLength(20)
                .HasColumnName("OSHPD");
            entity.Property(e => e.Osmidadd)
                .HasMaxLength(10)
                .HasColumnName("OSMIDADD");
            entity.Property(e => e.Osmpdd)
                .HasMaxLength(20)
                .HasColumnName("OSMPDD");
            entity.Property(e => e.Osmpdn)
                .HasMaxLength(20)
                .HasColumnName("OSMPDN");
            entity.Property(e => e.Osslaboff)
                .HasMaxLength(20)
                .HasColumnName("OSSLABOFF");
            entity.Property(e => e.Ossph)
                .HasMaxLength(10)
                .HasColumnName("OSSPH");
            entity.Property(e => e.Osvbase)
                .HasMaxLength(20)
                .HasColumnName("OSVBASE");
            entity.Property(e => e.Osvertexdist)
                .HasMaxLength(20)
                .HasColumnName("OSVERTEXDIST");
            entity.Property(e => e.Osvpd)
                .HasMaxLength(20)
                .HasColumnName("OSVPD");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.Refdate)
                .HasColumnType("datetime")
                .HasColumnName("REFDATE");
            entity.Property(e => e.Reftype)
                .HasMaxLength(10)
                .HasColumnName("REFTYPE");
            entity.Property(e => e.Rxcomments)
                .HasColumnType("text")
                .HasColumnName("RXCOMMENTS");
            entity.Property(e => e.Rxtype)
                .HasMaxLength(20)
                .HasColumnName("RXTYPE");
            entity.Property(e => e.User)
                .HasMaxLength(255)
                .HasColumnName("user");
        });

        modelBuilder.Entity<FormEyeMagImpplan>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("form_eye_mag_impplan");

            entity.HasIndex(e => new { e.FormId, e.Pid, e.Title, e.Plan }, "second_index")
                .IsUnique()
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0, 0, 20 });

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .HasColumnName("code");
            entity.Property(e => e.Codedesc)
                .HasMaxLength(255)
                .HasColumnName("codedesc");
            entity.Property(e => e.Codetext)
                .HasMaxLength(255)
                .HasColumnName("codetext");
            entity.Property(e => e.Codetype)
                .HasMaxLength(50)
                .HasColumnName("codetype");
            entity.Property(e => e.FormId)
                .HasColumnType("bigint(20)")
                .HasColumnName("form_id");
            entity.Property(e => e.ImpplanOrder)
                .HasColumnType("tinyint(4)")
                .HasColumnName("IMPPLAN_order");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.Plan)
                .HasMaxLength(3000)
                .HasColumnName("plan");
            entity.Property(e => e.PmsfhLink)
                .HasMaxLength(50)
                .HasColumnName("PMSFH_link");
            entity.Property(e => e.Title).HasColumnName("title");
        });

        modelBuilder.Entity<FormEyeMagOrder>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("form_eye_mag_orders");

            entity.HasIndex(e => new { e.Pid, e.OrderDetails, e.OrderDatePlaced }, "VISIT_ID").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.FormId)
                .HasColumnType("int(20)")
                .HasColumnName("form_id");
            entity.Property(e => e.OrderCompletedBywhom)
                .HasMaxLength(50)
                .HasColumnName("ORDER_COMPLETED_BYWHOM");
            entity.Property(e => e.OrderDateCompleted).HasColumnName("ORDER_DATE_COMPLETED");
            entity.Property(e => e.OrderDatePlaced).HasColumnName("ORDER_DATE_PLACED");
            entity.Property(e => e.OrderDetails).HasColumnName("ORDER_DETAILS");
            entity.Property(e => e.OrderPlacedBywhom)
                .HasMaxLength(50)
                .HasColumnName("ORDER_PLACED_BYWHOM");
            entity.Property(e => e.OrderPriority)
                .HasMaxLength(50)
                .HasColumnName("ORDER_PRIORITY");
            entity.Property(e => e.OrderStatus)
                .HasMaxLength(50)
                .HasColumnName("ORDER_STATUS");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
        });

        modelBuilder.Entity<FormEyeMagPref>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("form_eye_mag_prefs");

            entity.HasIndex(e => new { e.Id, e.Pezone, e.Location, e.Selection }, "id").IsUnique();

            entity.Property(e => e.FillAction)
                .HasMaxLength(10)
                .HasDefaultValueSql("'ADD'")
                .HasColumnName("FILL_ACTION");
            entity.Property(e => e.Goleft)
                .HasMaxLength(50)
                .HasColumnName("GOLEFT");
            entity.Property(e => e.Goright)
                .HasMaxLength(50)
                .HasColumnName("GORIGHT");
            entity.Property(e => e.Govalue)
                .HasMaxLength(10)
                .HasDefaultValueSql("'0'")
                .HasColumnName("GOVALUE");
            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Location)
                .HasMaxLength(25)
                .HasColumnName("LOCATION");
            entity.Property(e => e.LocationText)
                .HasMaxLength(25)
                .HasColumnName("LOCATION_text");
            entity.Property(e => e.Ordering)
                .HasColumnType("smallint(6)")
                .HasColumnName("ordering");
            entity.Property(e => e.Pezone)
                .HasMaxLength(25)
                .HasColumnName("PEZONE");
            entity.Property(e => e.Selection).HasColumnName("selection");
            entity.Property(e => e.Unspec)
                .HasMaxLength(50)
                .HasColumnName("UNSPEC");
            entity.Property(e => e.ZoneOrder)
                .HasColumnType("int(11)")
                .HasColumnName("ZONE_ORDER");
        });

        modelBuilder.Entity<FormEyeMagWearing>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("form_eye_mag_wearing");

            entity.HasIndex(e => new { e.FormId, e.Encounter, e.Pid, e.RxNumber }, "FORM_ID").IsUnique();

            entity.HasIndex(e => e.Id, "id").IsUnique();

            entity.Property(e => e.Bpdd)
                .HasMaxLength(20)
                .HasColumnName("BPDD");
            entity.Property(e => e.Bpdn)
                .HasMaxLength(20)
                .HasColumnName("BPDN");
            entity.Property(e => e.Comments)
                .HasColumnType("text")
                .HasColumnName("COMMENTS");
            entity.Property(e => e.Encounter)
                .HasColumnType("int(11)")
                .HasColumnName("ENCOUNTER");
            entity.Property(e => e.FormId)
                .HasColumnType("smallint(6)")
                .HasColumnName("FORM_ID");
            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.LensMaterial)
                .HasMaxLength(20)
                .HasColumnName("LENS_MATERIAL");
            entity.Property(e => e.LensTreatments)
                .HasMaxLength(100)
                .HasColumnName("LENS_TREATMENTS");
            entity.Property(e => e.Odadd)
                .HasMaxLength(10)
                .HasColumnName("ODADD");
            entity.Property(e => e.Odaxis)
                .HasMaxLength(10)
                .HasColumnName("ODAXIS");
            entity.Property(e => e.Odcyl)
                .HasMaxLength(10)
                .HasColumnName("ODCYL");
            entity.Property(e => e.Odhbase)
                .HasMaxLength(20)
                .HasColumnName("ODHBASE");
            entity.Property(e => e.Odhpd)
                .HasMaxLength(20)
                .HasColumnName("ODHPD");
            entity.Property(e => e.Odmidadd)
                .HasMaxLength(10)
                .HasColumnName("ODMIDADD");
            entity.Property(e => e.Odmpdd)
                .HasMaxLength(20)
                .HasColumnName("ODMPDD");
            entity.Property(e => e.Odmpdn)
                .HasMaxLength(20)
                .HasColumnName("ODMPDN");
            entity.Property(e => e.Odnearva)
                .HasMaxLength(10)
                .HasColumnName("ODNEARVA");
            entity.Property(e => e.Odslaboff)
                .HasMaxLength(20)
                .HasColumnName("ODSLABOFF");
            entity.Property(e => e.Odsph)
                .HasMaxLength(10)
                .HasColumnName("ODSPH");
            entity.Property(e => e.Odva)
                .HasMaxLength(10)
                .HasColumnName("ODVA");
            entity.Property(e => e.Odvbase)
                .HasMaxLength(20)
                .HasColumnName("ODVBASE");
            entity.Property(e => e.Odvertexdist)
                .HasMaxLength(20)
                .HasColumnName("ODVERTEXDIST");
            entity.Property(e => e.Odvpd)
                .HasMaxLength(20)
                .HasColumnName("ODVPD");
            entity.Property(e => e.Osadd)
                .HasMaxLength(10)
                .HasColumnName("OSADD");
            entity.Property(e => e.Osaxis)
                .HasMaxLength(10)
                .HasColumnName("OSAXIS");
            entity.Property(e => e.Oscyl)
                .HasMaxLength(10)
                .HasColumnName("OSCYL");
            entity.Property(e => e.Oshbase)
                .HasMaxLength(20)
                .HasColumnName("OSHBASE");
            entity.Property(e => e.Oshpd)
                .HasMaxLength(20)
                .HasColumnName("OSHPD");
            entity.Property(e => e.Osmidadd)
                .HasMaxLength(10)
                .HasColumnName("OSMIDADD");
            entity.Property(e => e.Osmpdd)
                .HasMaxLength(20)
                .HasColumnName("OSMPDD");
            entity.Property(e => e.Osmpdn)
                .HasMaxLength(20)
                .HasColumnName("OSMPDN");
            entity.Property(e => e.Osnearva)
                .HasMaxLength(10)
                .HasColumnName("OSNEARVA");
            entity.Property(e => e.Osslaboff)
                .HasMaxLength(20)
                .HasColumnName("OSSLABOFF");
            entity.Property(e => e.Ossph)
                .HasMaxLength(10)
                .HasColumnName("OSSPH");
            entity.Property(e => e.Osva)
                .HasMaxLength(10)
                .HasColumnName("OSVA");
            entity.Property(e => e.Osvbase)
                .HasMaxLength(20)
                .HasColumnName("OSVBASE");
            entity.Property(e => e.Osvertexdist)
                .HasMaxLength(20)
                .HasColumnName("OSVERTEXDIST");
            entity.Property(e => e.Osvpd)
                .HasMaxLength(20)
                .HasColumnName("OSVPD");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("PID");
            entity.Property(e => e.RxNumber)
                .HasColumnType("int(11)")
                .HasColumnName("RX_NUMBER");
            entity.Property(e => e.RxType)
                .HasMaxLength(25)
                .HasColumnName("RX_TYPE");
        });

        modelBuilder.Entity<FormEyeNeuro>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("form_eye_neuro");

            entity.HasIndex(e => new { e.Id, e.Pid }, "id_pid").IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasComment("Links to forms.form_id")
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Act)
                .HasMaxLength(3)
                .HasDefaultValueSql("'on'")
                .IsFixedLength()
                .HasColumnName("ACT");
            entity.Property(e => e.Act10ccdist)
                .HasColumnType("text")
                .HasColumnName("ACT10CCDIST");
            entity.Property(e => e.Act10ccnear)
                .HasColumnType("text")
                .HasColumnName("ACT10CCNEAR");
            entity.Property(e => e.Act10scdist)
                .HasColumnType("text")
                .HasColumnName("ACT10SCDIST");
            entity.Property(e => e.Act10scnear)
                .HasColumnType("text")
                .HasColumnName("ACT10SCNEAR");
            entity.Property(e => e.Act11ccdist)
                .HasColumnType("text")
                .HasColumnName("ACT11CCDIST");
            entity.Property(e => e.Act11ccnear)
                .HasColumnType("text")
                .HasColumnName("ACT11CCNEAR");
            entity.Property(e => e.Act11scdist)
                .HasColumnType("text")
                .HasColumnName("ACT11SCDIST");
            entity.Property(e => e.Act11scnear)
                .HasColumnType("text")
                .HasColumnName("ACT11SCNEAR");
            entity.Property(e => e.Act1ccdist)
                .HasColumnType("text")
                .HasColumnName("ACT1CCDIST");
            entity.Property(e => e.Act1ccnear)
                .HasColumnType("text")
                .HasColumnName("ACT1CCNEAR");
            entity.Property(e => e.Act1scdist)
                .HasColumnType("text")
                .HasColumnName("ACT1SCDIST");
            entity.Property(e => e.Act1scnear)
                .HasColumnType("text")
                .HasColumnName("ACT1SCNEAR");
            entity.Property(e => e.Act2ccdist)
                .HasColumnType("text")
                .HasColumnName("ACT2CCDIST");
            entity.Property(e => e.Act2ccnear)
                .HasColumnType("text")
                .HasColumnName("ACT2CCNEAR");
            entity.Property(e => e.Act2scdist)
                .HasColumnType("text")
                .HasColumnName("ACT2SCDIST");
            entity.Property(e => e.Act2scnear)
                .HasColumnType("text")
                .HasColumnName("ACT2SCNEAR");
            entity.Property(e => e.Act3ccdist)
                .HasColumnType("text")
                .HasColumnName("ACT3CCDIST");
            entity.Property(e => e.Act3ccnear)
                .HasColumnType("text")
                .HasColumnName("ACT3CCNEAR");
            entity.Property(e => e.Act3scdist)
                .HasColumnType("text")
                .HasColumnName("ACT3SCDIST");
            entity.Property(e => e.Act3scnear)
                .HasColumnType("text")
                .HasColumnName("ACT3SCNEAR");
            entity.Property(e => e.Act4ccdist)
                .HasColumnType("text")
                .HasColumnName("ACT4CCDIST");
            entity.Property(e => e.Act4ccnear)
                .HasColumnType("text")
                .HasColumnName("ACT4CCNEAR");
            entity.Property(e => e.Act4scdist)
                .HasColumnType("text")
                .HasColumnName("ACT4SCDIST");
            entity.Property(e => e.Act4scnear)
                .HasColumnType("text")
                .HasColumnName("ACT4SCNEAR");
            entity.Property(e => e.Act5ccdist)
                .HasColumnType("text")
                .HasColumnName("ACT5CCDIST");
            entity.Property(e => e.Act5ccnear)
                .HasColumnType("text")
                .HasColumnName("ACT5CCNEAR");
            entity.Property(e => e.Act5scdist)
                .HasColumnType("text")
                .HasColumnName("ACT5SCDIST");
            entity.Property(e => e.Act5scnear)
                .HasColumnType("text")
                .HasColumnName("ACT5SCNEAR");
            entity.Property(e => e.Act6ccdist)
                .HasColumnType("text")
                .HasColumnName("ACT6CCDIST");
            entity.Property(e => e.Act6ccnear)
                .HasColumnType("text")
                .HasColumnName("ACT6CCNEAR");
            entity.Property(e => e.Act6scdist)
                .HasColumnType("text")
                .HasColumnName("ACT6SCDIST");
            entity.Property(e => e.Act6scnear)
                .HasColumnType("text")
                .HasColumnName("ACT6SCNEAR");
            entity.Property(e => e.Act7ccdist)
                .HasColumnType("text")
                .HasColumnName("ACT7CCDIST");
            entity.Property(e => e.Act7ccnear)
                .HasColumnType("text")
                .HasColumnName("ACT7CCNEAR");
            entity.Property(e => e.Act7scdist)
                .HasColumnType("text")
                .HasColumnName("ACT7SCDIST");
            entity.Property(e => e.Act7scnear)
                .HasColumnType("text")
                .HasColumnName("ACT7SCNEAR");
            entity.Property(e => e.Act8ccdist)
                .HasColumnType("text")
                .HasColumnName("ACT8CCDIST");
            entity.Property(e => e.Act8ccnear)
                .HasColumnType("text")
                .HasColumnName("ACT8CCNEAR");
            entity.Property(e => e.Act8scdist)
                .HasColumnType("text")
                .HasColumnName("ACT8SCDIST");
            entity.Property(e => e.Act8scnear)
                .HasColumnType("text")
                .HasColumnName("ACT8SCNEAR");
            entity.Property(e => e.Act9ccdist)
                .HasColumnType("text")
                .HasColumnName("ACT9CCDIST");
            entity.Property(e => e.Act9ccnear)
                .HasColumnType("text")
                .HasColumnName("ACT9CCNEAR");
            entity.Property(e => e.Act9scdist)
                .HasColumnType("text")
                .HasColumnName("ACT9SCDIST");
            entity.Property(e => e.Act9scnear)
                .HasColumnType("text")
                .HasColumnName("ACT9SCNEAR");
            entity.Property(e => e.Caccdist)
                .HasMaxLength(20)
                .HasColumnName("CACCDIST");
            entity.Property(e => e.Caccnear)
                .HasMaxLength(20)
                .HasColumnName("CACCNEAR");
            entity.Property(e => e.Daccdist)
                .HasMaxLength(20)
                .HasColumnName("DACCDIST");
            entity.Property(e => e.Daccnear)
                .HasMaxLength(20)
                .HasColumnName("DACCNEAR");
            entity.Property(e => e.Divergenceamps)
                .HasColumnType("text")
                .HasColumnName("DIVERGENCEAMPS");
            entity.Property(e => e.MotilityLi)
                .HasMaxLength(1)
                .HasDefaultValueSql("'0'")
                .IsFixedLength()
                .HasColumnName("MOTILITY_LI");
            entity.Property(e => e.MotilityLl)
                .HasMaxLength(1)
                .HasDefaultValueSql("'0'")
                .IsFixedLength()
                .HasColumnName("MOTILITY_LL");
            entity.Property(e => e.MotilityLlio)
                .HasColumnType("int(1)")
                .HasColumnName("MOTILITY_LLIO");
            entity.Property(e => e.MotilityLlso)
                .HasColumnType("int(1)")
                .HasColumnName("MOTILITY_LLSO");
            entity.Property(e => e.MotilityLr)
                .HasMaxLength(1)
                .HasDefaultValueSql("'0'")
                .IsFixedLength()
                .HasColumnName("MOTILITY_LR");
            entity.Property(e => e.MotilityLrio)
                .HasColumnType("int(1)")
                .HasColumnName("MOTILITY_LRIO");
            entity.Property(e => e.MotilityLrso)
                .HasColumnType("int(1)")
                .HasColumnName("MOTILITY_LRSO");
            entity.Property(e => e.MotilityLs)
                .HasMaxLength(1)
                .HasDefaultValueSql("'0'")
                .IsFixedLength()
                .HasColumnName("MOTILITY_LS");
            entity.Property(e => e.MotilityRi)
                .HasMaxLength(1)
                .HasDefaultValueSql("'0'")
                .IsFixedLength()
                .HasColumnName("MOTILITY_RI");
            entity.Property(e => e.MotilityRl)
                .HasMaxLength(1)
                .HasDefaultValueSql("'0'")
                .IsFixedLength()
                .HasColumnName("MOTILITY_RL");
            entity.Property(e => e.MotilityRlio)
                .HasColumnType("int(1)")
                .HasColumnName("MOTILITY_RLIO");
            entity.Property(e => e.MotilityRlso)
                .HasColumnType("int(1)")
                .HasColumnName("MOTILITY_RLSO");
            entity.Property(e => e.MotilityRr)
                .HasMaxLength(1)
                .HasDefaultValueSql("'0'")
                .IsFixedLength()
                .HasColumnName("MOTILITY_RR");
            entity.Property(e => e.MotilityRrio)
                .HasColumnType("int(1)")
                .HasColumnName("MOTILITY_RRIO");
            entity.Property(e => e.MotilityRrso)
                .HasColumnType("int(1)")
                .HasColumnName("MOTILITY_RRSO");
            entity.Property(e => e.MotilityRs)
                .HasMaxLength(1)
                .HasDefaultValueSql("'0'")
                .IsFixedLength()
                .HasColumnName("MOTILITY_RS");
            entity.Property(e => e.Motilitynormal)
                .HasMaxLength(3)
                .HasDefaultValueSql("'on'")
                .IsFixedLength()
                .HasColumnName("MOTILITYNORMAL");
            entity.Property(e => e.NeuroComments)
                .HasColumnType("text")
                .HasColumnName("NEURO_COMMENTS");
            entity.Property(e => e.Npc)
                .HasMaxLength(10)
                .HasColumnName("NPC");
            entity.Property(e => e.Odcoins)
                .HasColumnType("text")
                .HasColumnName("ODCOINS");
            entity.Property(e => e.Odcolor)
                .HasColumnType("text")
                .HasColumnName("ODCOLOR");
            entity.Property(e => e.Odnpa)
                .HasColumnType("text")
                .HasColumnName("ODNPA");
            entity.Property(e => e.Odreddesat)
                .HasMaxLength(20)
                .HasColumnName("ODREDDESAT");
            entity.Property(e => e.Oscoins)
                .HasColumnType("text")
                .HasColumnName("OSCOINS");
            entity.Property(e => e.Oscolor)
                .HasColumnType("text")
                .HasColumnName("OSCOLOR");
            entity.Property(e => e.Osnpa)
                .HasColumnType("text")
                .HasColumnName("OSNPA");
            entity.Property(e => e.Osreddesat)
                .HasMaxLength(20)
                .HasColumnName("OSREDDESAT");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.Stereopsis)
                .HasMaxLength(25)
                .HasColumnName("STEREOPSIS");
            entity.Property(e => e.Vertfusamps)
                .HasColumnType("text")
                .HasColumnName("VERTFUSAMPS");
        });

        modelBuilder.Entity<FormEyePostseg>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("form_eye_postseg");

            entity.HasIndex(e => new { e.Id, e.Pid }, "id_pid").IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasComment("Links to forms.form_id")
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Atropine)
                .HasMaxLength(25)
                .HasColumnName("ATROPINE");
            entity.Property(e => e.Cyclogyl)
                .HasMaxLength(25)
                .HasColumnName("CYCLOGYL");
            entity.Property(e => e.Cyclomydril)
                .HasMaxLength(25)
                .HasColumnName("CYCLOMYDRIL");
            entity.Property(e => e.DilMeds)
                .HasColumnType("mediumtext")
                .HasColumnName("DIL_MEDS");
            entity.Property(e => e.DilRisks)
                .HasMaxLength(2)
                .HasDefaultValueSql("'on'")
                .IsFixedLength()
                .HasColumnName("DIL_RISKS");
            entity.Property(e => e.Neo25)
                .HasMaxLength(25)
                .HasColumnName("NEO25");
            entity.Property(e => e.Odcmt)
                .HasColumnType("text")
                .HasColumnName("ODCMT");
            entity.Property(e => e.Odcup)
                .HasColumnType("text")
                .HasColumnName("ODCUP");
            entity.Property(e => e.Oddisc)
                .HasColumnType("text")
                .HasColumnName("ODDISC");
            entity.Property(e => e.Odmacula)
                .HasColumnType("text")
                .HasColumnName("ODMACULA");
            entity.Property(e => e.Odperiph)
                .HasColumnType("text")
                .HasColumnName("ODPERIPH");
            entity.Property(e => e.Odvessels)
                .HasColumnType("text")
                .HasColumnName("ODVESSELS");
            entity.Property(e => e.Odvitreous)
                .HasColumnType("text")
                .HasColumnName("ODVITREOUS");
            entity.Property(e => e.Oscmt)
                .HasColumnType("text")
                .HasColumnName("OSCMT");
            entity.Property(e => e.Oscup)
                .HasColumnType("text")
                .HasColumnName("OSCUP");
            entity.Property(e => e.Osdisc)
                .HasColumnType("text")
                .HasColumnName("OSDISC");
            entity.Property(e => e.Osmacula)
                .HasColumnType("text")
                .HasColumnName("OSMACULA");
            entity.Property(e => e.Osperiph)
                .HasColumnType("text")
                .HasColumnName("OSPERIPH");
            entity.Property(e => e.Osvessels)
                .HasColumnType("text")
                .HasColumnName("OSVESSELS");
            entity.Property(e => e.Osvitreous)
                .HasColumnType("text")
                .HasColumnName("OSVITREOUS");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.RetinaComments)
                .HasColumnType("text")
                .HasColumnName("RETINA_COMMENTS");
            entity.Property(e => e.Tropicamide)
                .HasMaxLength(25)
                .HasColumnName("TROPICAMIDE");
            entity.Property(e => e.Wettype)
                .HasMaxLength(10)
                .HasColumnName("WETTYPE");
        });

        modelBuilder.Entity<FormEyeRefraction>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("form_eye_refraction");

            entity.HasIndex(e => new { e.Id, e.Pid }, "id_pid").IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasComment("Links to forms.form_id")
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Addchecked)
                .HasMaxLength(25)
                .HasColumnName("ADDCHECKED");
            entity.Property(e => e.Arnearodva)
                .HasMaxLength(25)
                .HasColumnName("ARNEARODVA");
            entity.Property(e => e.Arnearosva)
                .HasMaxLength(25)
                .HasColumnName("ARNEAROSVA");
            entity.Property(e => e.Arodadd)
                .HasMaxLength(25)
                .HasColumnName("ARODADD");
            entity.Property(e => e.Arodaxis)
                .HasMaxLength(25)
                .HasColumnName("ARODAXIS");
            entity.Property(e => e.Arodcyl)
                .HasMaxLength(25)
                .HasColumnName("ARODCYL");
            entity.Property(e => e.Arodprism)
                .HasMaxLength(50)
                .HasColumnName("ARODPRISM");
            entity.Property(e => e.Arodsph)
                .HasMaxLength(25)
                .HasColumnName("ARODSPH");
            entity.Property(e => e.Arosadd)
                .HasMaxLength(25)
                .HasColumnName("AROSADD");
            entity.Property(e => e.Arosaxis)
                .HasMaxLength(25)
                .HasColumnName("AROSAXIS");
            entity.Property(e => e.Aroscyl)
                .HasMaxLength(25)
                .HasColumnName("AROSCYL");
            entity.Property(e => e.Arosprism)
                .HasMaxLength(50)
                .HasColumnName("AROSPRISM");
            entity.Property(e => e.Arossph)
                .HasMaxLength(25)
                .HasColumnName("AROSSPH");
            entity.Property(e => e.Balanced)
                .HasMaxLength(2)
                .IsFixedLength()
                .HasColumnName("BALANCED");
            entity.Property(e => e.Crcomments)
                .HasMaxLength(255)
                .HasColumnName("CRCOMMENTS");
            entity.Property(e => e.Crodaxis)
                .HasMaxLength(25)
                .HasColumnName("CRODAXIS");
            entity.Property(e => e.Crodcyl)
                .HasMaxLength(25)
                .HasColumnName("CRODCYL");
            entity.Property(e => e.Crodsph)
                .HasMaxLength(25)
                .HasColumnName("CRODSPH");
            entity.Property(e => e.Crosaxis)
                .HasMaxLength(25)
                .HasColumnName("CROSAXIS");
            entity.Property(e => e.Croscyl)
                .HasMaxLength(25)
                .HasColumnName("CROSCYL");
            entity.Property(e => e.Crossph)
                .HasMaxLength(25)
                .HasColumnName("CROSSPH");
            entity.Property(e => e.CtlComments)
                .HasColumnType("text")
                .HasColumnName("CTL_COMMENTS");
            entity.Property(e => e.Ctlbrandod)
                .HasMaxLength(50)
                .HasColumnName("CTLBRANDOD");
            entity.Property(e => e.Ctlbrandos)
                .HasMaxLength(50)
                .HasColumnName("CTLBRANDOS");
            entity.Property(e => e.Ctlmanufacturerod)
                .HasMaxLength(50)
                .HasColumnName("CTLMANUFACTUREROD");
            entity.Property(e => e.Ctlmanufactureros)
                .HasMaxLength(50)
                .HasColumnName("CTLMANUFACTUREROS");
            entity.Property(e => e.Ctlodadd)
                .HasMaxLength(25)
                .HasColumnName("CTLODADD");
            entity.Property(e => e.Ctlodaxis)
                .HasMaxLength(25)
                .HasColumnName("CTLODAXIS");
            entity.Property(e => e.Ctlodbc)
                .HasMaxLength(25)
                .HasColumnName("CTLODBC");
            entity.Property(e => e.Ctlodcyl)
                .HasMaxLength(25)
                .HasColumnName("CTLODCYL");
            entity.Property(e => e.Ctloddiam)
                .HasMaxLength(25)
                .HasColumnName("CTLODDIAM");
            entity.Property(e => e.Ctlodsph)
                .HasMaxLength(25)
                .HasColumnName("CTLODSPH");
            entity.Property(e => e.Ctlosadd)
                .HasMaxLength(25)
                .HasColumnName("CTLOSADD");
            entity.Property(e => e.Ctlosaxis)
                .HasMaxLength(25)
                .HasColumnName("CTLOSAXIS");
            entity.Property(e => e.Ctlosbc)
                .HasMaxLength(25)
                .HasColumnName("CTLOSBC");
            entity.Property(e => e.Ctloscyl)
                .HasMaxLength(25)
                .HasColumnName("CTLOSCYL");
            entity.Property(e => e.Ctlosdiam)
                .HasMaxLength(25)
                .HasColumnName("CTLOSDIAM");
            entity.Property(e => e.Ctlossph)
                .HasMaxLength(25)
                .HasColumnName("CTLOSSPH");
            entity.Property(e => e.Ctlsupplierod)
                .HasMaxLength(50)
                .HasColumnName("CTLSUPPLIEROD");
            entity.Property(e => e.Ctlsupplieros)
                .HasMaxLength(50)
                .HasColumnName("CTLSUPPLIEROS");
            entity.Property(e => e.Mrodadd)
                .HasMaxLength(25)
                .HasColumnName("MRODADD");
            entity.Property(e => e.Mrodaxis)
                .HasMaxLength(25)
                .HasColumnName("MRODAXIS");
            entity.Property(e => e.Mrodbase)
                .HasMaxLength(25)
                .HasColumnName("MRODBASE");
            entity.Property(e => e.Mrodbasenear)
                .HasMaxLength(25)
                .HasColumnName("MRODBASENEAR");
            entity.Property(e => e.Mrodcyl)
                .HasMaxLength(25)
                .HasColumnName("MRODCYL");
            entity.Property(e => e.Mrodnearaxis)
                .HasMaxLength(25)
                .HasColumnName("MRODNEARAXIS");
            entity.Property(e => e.Mrodnearcyl)
                .HasMaxLength(25)
                .HasColumnName("MRODNEARCYL");
            entity.Property(e => e.Mrodnearsphere)
                .HasMaxLength(25)
                .HasColumnName("MRODNEARSPHERE");
            entity.Property(e => e.Mrodprism)
                .HasMaxLength(25)
                .HasColumnName("MRODPRISM");
            entity.Property(e => e.Mrodprismnear)
                .HasMaxLength(50)
                .HasColumnName("MRODPRISMNEAR");
            entity.Property(e => e.Mrodsph)
                .HasMaxLength(25)
                .HasColumnName("MRODSPH");
            entity.Property(e => e.Mrosadd)
                .HasMaxLength(25)
                .HasColumnName("MROSADD");
            entity.Property(e => e.Mrosaxis)
                .HasMaxLength(25)
                .HasColumnName("MROSAXIS");
            entity.Property(e => e.Mrosbase)
                .HasMaxLength(50)
                .HasColumnName("MROSBASE");
            entity.Property(e => e.Mrosbasenear)
                .HasMaxLength(25)
                .HasColumnName("MROSBASENEAR");
            entity.Property(e => e.Mroscyl)
                .HasMaxLength(25)
                .HasColumnName("MROSCYL");
            entity.Property(e => e.Mrosnearaxis)
                .HasMaxLength(125)
                .HasColumnName("MROSNEARAXIS");
            entity.Property(e => e.Mrosnearcyl)
                .HasMaxLength(25)
                .HasColumnName("MROSNEARCYL");
            entity.Property(e => e.Mrosnearshpere)
                .HasMaxLength(25)
                .HasColumnName("MROSNEARSHPERE");
            entity.Property(e => e.Mrosprism)
                .HasMaxLength(50)
                .HasColumnName("MROSPRISM");
            entity.Property(e => e.Mrosprismnear)
                .HasMaxLength(50)
                .HasColumnName("MROSPRISMNEAR");
            entity.Property(e => e.Mrossph)
                .HasMaxLength(25)
                .HasColumnName("MROSSPH");
            entity.Property(e => e.Nvochecked)
                .HasMaxLength(25)
                .HasColumnName("NVOCHECKED");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
        });

        modelBuilder.Entity<FormEyeRo>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("form_eye_ros");

            entity.HasIndex(e => new { e.Id, e.Pid }, "id_pid").IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasComment("Links to forms.form_id")
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.Roscomments)
                .HasColumnType("text")
                .HasColumnName("ROSCOMMENTS");
            entity.Property(e => e.Roscv)
                .HasColumnType("text")
                .HasColumnName("ROSCV");
            entity.Property(e => e.Rosderm)
                .HasColumnType("text")
                .HasColumnName("ROSDERM");
            entity.Property(e => e.Rosendocrine)
                .HasColumnType("text")
                .HasColumnName("ROSENDOCRINE");
            entity.Property(e => e.Rosgeneral)
                .HasColumnType("text")
                .HasColumnName("ROSGENERAL");
            entity.Property(e => e.Rosgi)
                .HasColumnType("text")
                .HasColumnName("ROSGI");
            entity.Property(e => e.Rosgu)
                .HasColumnType("text")
                .HasColumnName("ROSGU");
            entity.Property(e => e.Rosheent)
                .HasColumnType("text")
                .HasColumnName("ROSHEENT");
            entity.Property(e => e.Rosimmuno)
                .HasColumnType("text")
                .HasColumnName("ROSIMMUNO");
            entity.Property(e => e.Rosmusculo)
                .HasColumnType("text")
                .HasColumnName("ROSMUSCULO");
            entity.Property(e => e.Rosneuro)
                .HasColumnType("text")
                .HasColumnName("ROSNEURO");
            entity.Property(e => e.Rospsych)
                .HasColumnType("text")
                .HasColumnName("ROSPSYCH");
            entity.Property(e => e.Rospulm)
                .HasColumnType("text")
                .HasColumnName("ROSPULM");
        });

        modelBuilder.Entity<FormEyeVital>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("form_eye_vitals");

            entity.HasIndex(e => new { e.Id, e.Pid }, "id_pid").IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasComment("Links to forms.form_id")
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Alert)
                .HasMaxLength(3)
                .HasDefaultValueSql("'yes'")
                .IsFixedLength()
                .HasColumnName("alert");
            entity.Property(e => e.Amslerod)
                .HasColumnType("smallint(1)")
                .HasColumnName("AMSLEROD");
            entity.Property(e => e.Amsleros)
                .HasColumnType("smallint(1)")
                .HasColumnName("AMSLEROS");
            entity.Property(e => e.Confused)
                .HasMaxLength(3)
                .HasDefaultValueSql("'nml'")
                .IsFixedLength()
                .HasColumnName("confused");
            entity.Property(e => e.Iopposttime)
                .HasColumnType("time")
                .HasColumnName("IOPPOSTTIME");
            entity.Property(e => e.Ioptime)
                .HasColumnType("time")
                .HasColumnName("IOPTIME");
            entity.Property(e => e.Odiopap)
                .HasMaxLength(10)
                .HasColumnName("ODIOPAP");
            entity.Property(e => e.Odiopftn)
                .HasMaxLength(10)
                .HasColumnName("ODIOPFTN");
            entity.Property(e => e.Odioppost)
                .HasMaxLength(10)
                .HasColumnName("ODIOPPOST");
            entity.Property(e => e.Odioptarget)
                .HasMaxLength(10)
                .HasColumnName("ODIOPTARGET");
            entity.Property(e => e.Odioptpn)
                .HasMaxLength(10)
                .HasColumnName("ODIOPTPN");
            entity.Property(e => e.Odvf1).HasColumnName("ODVF1");
            entity.Property(e => e.Odvf2).HasColumnName("ODVF2");
            entity.Property(e => e.Odvf3).HasColumnName("ODVF3");
            entity.Property(e => e.Odvf4).HasColumnName("ODVF4");
            entity.Property(e => e.Oriented)
                .HasMaxLength(3)
                .HasDefaultValueSql("'TPP'")
                .IsFixedLength()
                .HasColumnName("oriented");
            entity.Property(e => e.Osiopap)
                .HasMaxLength(10)
                .HasColumnName("OSIOPAP");
            entity.Property(e => e.Osiopftn)
                .HasMaxLength(10)
                .HasColumnName("OSIOPFTN");
            entity.Property(e => e.Osioppost)
                .HasMaxLength(10)
                .HasColumnName("OSIOPPOST");
            entity.Property(e => e.Osioptarget)
                .HasMaxLength(10)
                .HasColumnName("OSIOPTARGET");
            entity.Property(e => e.Osioptpn)
                .HasMaxLength(10)
                .HasColumnName("OSIOPTPN");
            entity.Property(e => e.Osvf1).HasColumnName("OSVF1");
            entity.Property(e => e.Osvf2).HasColumnName("OSVF2");
            entity.Property(e => e.Osvf3).HasColumnName("OSVF3");
            entity.Property(e => e.Osvf4).HasColumnName("OSVF4");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
        });

        modelBuilder.Entity<FormFunctionalCognitiveStatus>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("form_functional_cognitive_status");

            entity.Property(e => e.Activity)
                .HasColumnType("tinyint(4)")
                .HasColumnName("activity");
            entity.Property(e => e.Authorized)
                .HasColumnType("tinyint(4)")
                .HasColumnName("authorized");
            entity.Property(e => e.Code)
                .HasMaxLength(255)
                .HasColumnName("code");
            entity.Property(e => e.Codetext)
                .HasColumnType("text")
                .HasColumnName("codetext");
            entity.Property(e => e.Date).HasColumnName("date");
            entity.Property(e => e.Description)
                .HasColumnType("text")
                .HasColumnName("description");
            entity.Property(e => e.Encounter)
                .HasMaxLength(255)
                .HasColumnName("encounter");
            entity.Property(e => e.ExternalId)
                .HasMaxLength(30)
                .HasColumnName("external_id");
            entity.Property(e => e.Groupname)
                .HasMaxLength(255)
                .HasColumnName("groupname");
            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.User)
                .HasMaxLength(255)
                .HasColumnName("user");
        });

        modelBuilder.Entity<FormGroupAttendance>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("form_group_attendance");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Activity)
                .HasColumnType("tinyint(4)")
                .HasColumnName("activity");
            entity.Property(e => e.Authorized)
                .HasColumnType("tinyint(4)")
                .HasColumnName("authorized");
            entity.Property(e => e.Date).HasColumnName("date");
            entity.Property(e => e.EncounterId)
                .HasColumnType("int(11)")
                .HasColumnName("encounter_id");
            entity.Property(e => e.GroupId)
                .HasColumnType("int(11)")
                .HasColumnName("group_id");
            entity.Property(e => e.Groupname)
                .HasMaxLength(255)
                .HasColumnName("groupname");
            entity.Property(e => e.User)
                .HasMaxLength(255)
                .HasColumnName("user");
        });

        modelBuilder.Entity<FormGroupsEncounter>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("form_groups_encounter");

            entity.HasIndex(e => e.Date, "encounter_date");

            entity.HasIndex(e => new { e.GroupId, e.Encounter }, "pid_encounter");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.ApptId)
                .HasColumnType("int(11)")
                .HasColumnName("appt_id");
            entity.Property(e => e.BillingFacility)
                .HasColumnType("int(11)")
                .HasColumnName("billing_facility");
            entity.Property(e => e.BillingNote)
                .HasColumnType("text")
                .HasColumnName("billing_note");
            entity.Property(e => e.Counselors)
                .HasMaxLength(255)
                .HasColumnName("counselors");
            entity.Property(e => e.Date)
                .HasColumnType("datetime")
                .HasColumnName("date");
            entity.Property(e => e.Encounter)
                .HasColumnType("bigint(20)")
                .HasColumnName("encounter");
            entity.Property(e => e.ExternalId)
                .HasMaxLength(20)
                .HasColumnName("external_id");
            entity.Property(e => e.Facility).HasColumnName("facility");
            entity.Property(e => e.FacilityId)
                .HasColumnType("int(11)")
                .HasColumnName("facility_id");
            entity.Property(e => e.GroupId)
                .HasColumnType("bigint(20)")
                .HasColumnName("group_id");
            entity.Property(e => e.InvoiceRefno)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasColumnName("invoice_refno");
            entity.Property(e => e.LastLevelBilled)
                .HasComment("0=none, 1=ins1, 2=ins2, etc")
                .HasColumnType("int(11)")
                .HasColumnName("last_level_billed");
            entity.Property(e => e.LastLevelClosed)
                .HasComment("0=none, 1=ins1, 2=ins2, etc")
                .HasColumnType("int(11)")
                .HasColumnName("last_level_closed");
            entity.Property(e => e.LastStmtDate).HasColumnName("last_stmt_date");
            entity.Property(e => e.OnsetDate)
                .HasColumnType("datetime")
                .HasColumnName("onset_date");
            entity.Property(e => e.PcCatid)
                .HasDefaultValueSql("'5'")
                .HasComment("event category from openemr_postcalendar_categories")
                .HasColumnType("int(11)")
                .HasColumnName("pc_catid");
            entity.Property(e => e.PosCode)
                .HasColumnType("tinyint(4)")
                .HasColumnName("pos_code");
            entity.Property(e => e.ProviderId)
                .HasDefaultValueSql("'0'")
                .HasComment("default and main provider for this visit")
                .HasColumnType("int(11)")
                .HasColumnName("provider_id");
            entity.Property(e => e.Reason).HasColumnName("reason");
            entity.Property(e => e.ReferralSource)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasColumnName("referral_source");
            entity.Property(e => e.Sensitivity)
                .HasMaxLength(30)
                .HasColumnName("sensitivity");
            entity.Property(e => e.StmtCount)
                .HasColumnType("int(11)")
                .HasColumnName("stmt_count");
            entity.Property(e => e.SupervisorId)
                .HasDefaultValueSql("'0'")
                .HasComment("supervising provider, if any, for this visit")
                .HasColumnType("int(11)")
                .HasColumnName("supervisor_id");
        });

        modelBuilder.Entity<FormHistorySdoh>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("form_history_sdoh");

            entity.HasIndex(e => e.AssessmentDate, "assessment_idx");

            entity.HasIndex(e => e.Encounter, "encounter_idx");

            entity.HasIndex(e => e.Pid, "pid_idx");

            entity.HasIndex(e => e.Uuid, "uuid_idx");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(21) unsigned")
                .HasColumnName("id");
            entity.Property(e => e.AssessmentDate).HasColumnName("assessment_date");
            entity.Property(e => e.Assessor)
                .HasMaxLength(255)
                .HasComment("fk to users.username the user that administered the assessment")
                .HasColumnName("assessor");
            entity.Property(e => e.CaregiverStatus)
                .HasMaxLength(20)
                .HasComment("fk to list_options.option_id WHERE list_id=sdoh_food_insecurity_risk")
                .HasColumnName("caregiver_status");
            entity.Property(e => e.ChildcareNeeds)
                .HasMaxLength(50)
                .HasComment("fk to list_options.option_id WHERE list_id=sdoh_childcare_needs")
                .HasColumnName("childcare_needs");
            entity.Property(e => e.ChildcareNeedsNotes)
                .HasColumnType("text")
                .HasColumnName("childcare_needs_notes");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasComment("fk to users.id user that created this record")
                .HasColumnType("int(10) unsigned")
                .HasColumnName("created_by");
            entity.Property(e => e.DeclinedFlag).HasColumnName("declined_flag");
            entity.Property(e => e.DigitalAccess)
                .HasMaxLength(50)
                .HasComment("fk to list_options.option_id WHERE list_id=sdoh_digital_access")
                .HasColumnName("digital_access");
            entity.Property(e => e.DigitalAccessNotes)
                .HasColumnType("text")
                .HasColumnName("digital_access_notes");
            entity.Property(e => e.DisabilityScale)
                .HasColumnType("text")
                .HasColumnName("disability_scale");
            entity.Property(e => e.DisabilityStatus)
                .HasMaxLength(50)
                .HasComment("fk to list_options.option_id WHERE list_id=disability_status")
                .HasColumnName("disability_status");
            entity.Property(e => e.DisabilityStatusNotes)
                .HasColumnType("text")
                .HasColumnName("disability_status_notes");
            entity.Property(e => e.EducationLevel)
                .HasMaxLength(50)
                .HasComment("fk to list_options.option_id WHERE list_id=sdoh_education_level")
                .HasColumnName("education_level");
            entity.Property(e => e.EmploymentStatus)
                .HasMaxLength(50)
                .HasComment("fk to list_options.option_id WHERE list_id=sdoh_food_insecurity_risk")
                .HasColumnName("employment_status");
            entity.Property(e => e.Encounter)
                .HasColumnType("int(10) unsigned")
                .HasColumnName("encounter");
            entity.Property(e => e.FinancialStrain)
                .HasMaxLength(50)
                .HasComment("fk to list_options.option_id WHERE list_id=sdoh_financial_strain")
                .HasColumnName("financial_strain");
            entity.Property(e => e.FinancialStrainNotes)
                .HasColumnType("text")
                .HasColumnName("financial_strain_notes");
            entity.Property(e => e.FoodInsecurity)
                .HasMaxLength(50)
                .HasComment("fk to list_options.option_id WHERE list_id=sdoh_food_insecurity_risk")
                .HasColumnName("food_insecurity");
            entity.Property(e => e.FoodInsecurityNotes)
                .HasColumnType("text")
                .HasColumnName("food_insecurity_notes");
            entity.Property(e => e.Goals)
                .HasColumnType("text")
                .HasColumnName("goals");
            entity.Property(e => e.HousingInstability)
                .HasMaxLength(50)
                .HasComment("fk to list_options.option_id WHERE list_id=sdoh_housing_worry")
                .HasColumnName("housing_instability");
            entity.Property(e => e.HousingInstabilityNotes)
                .HasColumnType("text")
                .HasColumnName("housing_instability_notes");
            entity.Property(e => e.HungerQ1)
                .HasMaxLength(50)
                .HasComment("fk to list_options.option_id WHERE list_id=vital_signs_answers")
                .HasColumnName("hunger_q1");
            entity.Property(e => e.HungerQ2)
                .HasMaxLength(50)
                .HasComment("fk to list_options.option_id WHERE list_id=vital_signs_answers")
                .HasColumnName("hunger_q2");
            entity.Property(e => e.HungerScore)
                .HasComment("Calculated HVS score")
                .HasColumnType("int(11)")
                .HasColumnName("hunger_score");
            entity.Property(e => e.InstrumentScore)
                .HasColumnType("int(11)")
                .HasColumnName("instrument_score");
            entity.Property(e => e.InterpersonalSafety)
                .HasMaxLength(50)
                .HasComment("fk to list_options.option_id WHERE list_id=sdoh_financial_strain")
                .HasColumnName("interpersonal_safety");
            entity.Property(e => e.InterpersonalSafetyNotes)
                .HasColumnType("text")
                .HasColumnName("interpersonal_safety_notes");
            entity.Property(e => e.Interventions)
                .HasColumnType("text")
                .HasColumnName("interventions");
            entity.Property(e => e.Pid)
                .HasColumnType("int(10) unsigned")
                .HasColumnName("pid");
            entity.Property(e => e.PositiveDomainCount)
                .HasColumnType("int(11)")
                .HasColumnName("positive_domain_count");
            entity.Property(e => e.PostpartumEnd)
                .HasComment("PostPartum end date")
                .HasColumnName("postpartum_end");
            entity.Property(e => e.PostpartumStatus)
                .HasMaxLength(20)
                .HasComment("fk to list_options.option_id WHERE list_id=postpartum_status")
                .HasColumnName("postpartum_status");
            entity.Property(e => e.PregnancyEdd)
                .HasComment("Estimated due date for pregnancy")
                .HasColumnName("pregnancy_edd");
            entity.Property(e => e.PregnancyIntent)
                .HasMaxLength(32)
                .HasComment("fk to list_options.option_id WHERE list_id=pregnancy_intent Pregnancy Intent Over Next Year (codes from PregnancyIntent list)")
                .HasColumnName("pregnancy_intent");
            entity.Property(e => e.PregnancyStatus)
                .HasMaxLength(20)
                .HasComment("fk to list_options.option_id WHERE list_id=pregnancy_status")
                .HasColumnName("pregnancy_status");
            entity.Property(e => e.ScreeningTool)
                .HasMaxLength(255)
                .HasComment("fk to list_options.option_id WHERE list_id=sdoh_instruments represents the assessment tool used to administer this assessment")
                .HasColumnName("screening_tool");
            entity.Property(e => e.SocialIsolation)
                .HasMaxLength(50)
                .HasComment("fk to list_options.option_id WHERE list_id=sdoh_social_isolation_freq")
                .HasColumnName("social_isolation");
            entity.Property(e => e.SocialIsolationNotes)
                .HasColumnType("text")
                .HasColumnName("social_isolation_notes");
            entity.Property(e => e.TransportationInsecurity)
                .HasMaxLength(50)
                .HasComment("fk to list_options.option_id WHERE list_id=sdoh_transportation_barrier")
                .HasColumnName("transportation_insecurity");
            entity.Property(e => e.TransportationInsecurityNotes)
                .HasColumnType("text")
                .HasColumnName("transportation_insecurity_notes");
            entity.Property(e => e.UpdatedAt)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasComment("fk to users.id user that last modified this record")
                .HasColumnType("int(10) unsigned")
                .HasColumnName("updated_by");
            entity.Property(e => e.UtilitiesInsecurity)
                .HasMaxLength(50)
                .HasComment("fk to list_options.option_id WHERE list_id=sdoh_utilities_shutoff")
                .HasColumnName("utilities_insecurity");
            entity.Property(e => e.UtilitiesInsecurityNotes)
                .HasColumnType("text")
                .HasColumnName("utilities_insecurity_notes");
            entity.Property(e => e.Uuid)
                .HasMaxLength(16)
                .IsFixedLength()
                .HasColumnName("uuid");
            entity.Property(e => e.VeteranStatus)
                .HasMaxLength(20)
                .HasComment("fk to list_options.option_id WHERE list_id=sdoh_food_insecurity_risk")
                .HasColumnName("veteran_status");
        });

        modelBuilder.Entity<FormHistorySdohHealthConcern>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("form_history_sdoh_health_concerns", tb => tb.HasComment("Links SDOH assessments to health concern conditions"));

            entity.HasIndex(e => e.HealthConcernId, "idx_health_concern");

            entity.HasIndex(e => e.SdohHistoryId, "idx_sdoh_history");

            entity.HasIndex(e => new { e.SdohHistoryId, e.HealthConcernId }, "unique_sdoh_concern").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasComment("FK to users.id")
                .HasColumnType("bigint(20)")
                .HasColumnName("created_by");
            entity.Property(e => e.HealthConcernId)
                .HasComment("FK to lists.id where type=health_concern or medical_problem")
                .HasColumnType("bigint(20)")
                .HasColumnName("health_concern_id");
            entity.Property(e => e.SdohHistoryId)
                .HasComment("FK to form_history_sdoh.id")
                .HasColumnType("bigint(20) unsigned")
                .HasColumnName("sdoh_history_id");
        });

        modelBuilder.Entity<FormMiscBillingOption>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("form_misc_billing_options");

            entity.HasIndex(e => e.Encounter, "encounter").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.AccidentState)
                .HasMaxLength(2)
                .HasColumnName("accident_state");
            entity.Property(e => e.Activity)
                .HasColumnType("tinyint(4)")
                .HasColumnName("activity");
            entity.Property(e => e.Authorized)
                .HasColumnType("tinyint(4)")
                .HasColumnName("authorized");
            entity.Property(e => e.AutoAccident).HasColumnName("auto_accident");
            entity.Property(e => e.Box14DateQual)
                .HasMaxLength(3)
                .IsFixedLength()
                .HasColumnName("box_14_date_qual");
            entity.Property(e => e.Box15DateQual)
                .HasMaxLength(3)
                .IsFixedLength()
                .HasColumnName("box_15_date_qual");
            entity.Property(e => e.Comments)
                .HasMaxLength(255)
                .HasColumnName("comments");
            entity.Property(e => e.Date)
                .HasColumnType("datetime")
                .HasColumnName("date");
            entity.Property(e => e.DateInitialTreatment).HasColumnName("date_initial_treatment");
            entity.Property(e => e.EmploymentRelated).HasColumnName("employment_related");
            entity.Property(e => e.Encounter)
                .HasColumnType("bigint(20)")
                .HasColumnName("encounter");
            entity.Property(e => e.EpsdtFlag).HasColumnName("epsdt_flag");
            entity.Property(e => e.Groupname)
                .HasMaxLength(255)
                .HasColumnName("groupname");
            entity.Property(e => e.HospitalizationDateFrom).HasColumnName("hospitalization_date_from");
            entity.Property(e => e.HospitalizationDateTo).HasColumnName("hospitalization_date_to");
            entity.Property(e => e.IcnResubmissionNumber)
                .HasMaxLength(35)
                .HasColumnName("icn_resubmission_number");
            entity.Property(e => e.IsHospitalized).HasColumnName("is_hospitalized");
            entity.Property(e => e.IsUnableToWork).HasColumnName("is_unable_to_work");
            entity.Property(e => e.LabAmount)
                .HasPrecision(5, 2)
                .HasColumnName("lab_amount");
            entity.Property(e => e.MedicaidReferralCode)
                .HasMaxLength(2)
                .HasColumnName("medicaid_referral_code");
            entity.Property(e => e.OffWorkFrom).HasColumnName("off_work_from");
            entity.Property(e => e.OffWorkTo).HasColumnName("off_work_to");
            entity.Property(e => e.OnsetDate).HasColumnName("onset_date");
            entity.Property(e => e.OriginalReferenceNumber)
                .HasMaxLength(50)
                .HasColumnName("original_reference_number");
            entity.Property(e => e.OtherAccident).HasColumnName("other_accident");
            entity.Property(e => e.OutsideLab).HasColumnName("outside_lab");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.PriorAuthNumber)
                .HasMaxLength(20)
                .HasColumnName("prior_auth_number");
            entity.Property(e => e.ProviderId)
                .HasColumnType("int(11)")
                .HasColumnName("provider_id");
            entity.Property(e => e.ProviderQualifierCode)
                .HasMaxLength(2)
                .HasColumnName("provider_qualifier_code");
            entity.Property(e => e.ReplacementClaim)
                .HasDefaultValueSql("'0'")
                .HasColumnName("replacement_claim");
            entity.Property(e => e.ResubmissionCode)
                .HasMaxLength(10)
                .HasColumnName("resubmission_code");
            entity.Property(e => e.User)
                .HasMaxLength(255)
                .HasColumnName("user");
        });

        modelBuilder.Entity<FormObservation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("form_observation");

            entity.HasIndex(e => e.Category, "idx_category");

            entity.HasIndex(e => e.Date, "idx_date");

            entity.HasIndex(e => e.FormId, "idx_form_id");

            entity.HasIndex(e => e.ParentObservationId, "idx_parent_observation");

            entity.HasIndex(e => new { e.Pid, e.Encounter }, "idx_pid_encounter");

            entity.HasIndex(e => e.QuestionnaireResponseId, "idx_questionnaire_response");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Activity)
                .HasColumnType("tinyint(4)")
                .HasColumnName("activity");
            entity.Property(e => e.Authorized)
                .HasColumnType("tinyint(4)")
                .HasColumnName("authorized");
            entity.Property(e => e.Category)
                .HasMaxLength(64)
                .HasComment("FK to list_options.option_id for observation category (SDOH, Functional, Cognitive, Physical, etc)")
                .HasColumnName("category");
            entity.Property(e => e.Code)
                .HasMaxLength(255)
                .HasColumnName("code");
            entity.Property(e => e.CodeType)
                .HasMaxLength(255)
                .HasColumnName("code_type");
            entity.Property(e => e.Date)
                .HasColumnType("datetime")
                .HasColumnName("date");
            entity.Property(e => e.DateEnd)
                .HasColumnType("datetime")
                .HasColumnName("date_end");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
            entity.Property(e => e.Encounter).HasColumnName("encounter");
            entity.Property(e => e.FormId)
                .HasComment("FK to forms.form_id")
                .HasColumnType("bigint(20)")
                .HasColumnName("form_id");
            entity.Property(e => e.Groupname)
                .HasMaxLength(255)
                .HasColumnName("groupname");
            entity.Property(e => e.ObCode)
                .HasMaxLength(64)
                .HasColumnName("ob_code");
            entity.Property(e => e.ObDocumentationofTable)
                .HasMaxLength(255)
                .HasColumnName("ob_documentationof_table");
            entity.Property(e => e.ObDocumentationofTableId)
                .HasColumnType("bigint(21)")
                .HasColumnName("ob_documentationof_table_id");
            entity.Property(e => e.ObReasonCode)
                .HasMaxLength(64)
                .HasColumnName("ob_reason_code");
            entity.Property(e => e.ObReasonStatus)
                .HasMaxLength(32)
                .HasColumnName("ob_reason_status");
            entity.Property(e => e.ObReasonText)
                .HasColumnType("text")
                .HasColumnName("ob_reason_text");
            entity.Property(e => e.ObStatus)
                .HasMaxLength(32)
                .HasColumnName("ob_status");
            entity.Property(e => e.ObType)
                .HasMaxLength(64)
                .HasColumnName("ob_type");
            entity.Property(e => e.ObUnit)
                .HasMaxLength(255)
                .HasColumnName("ob_unit");
            entity.Property(e => e.ObValue)
                .HasMaxLength(255)
                .HasColumnName("ob_value");
            entity.Property(e => e.ObValueCodeDescription)
                .HasMaxLength(255)
                .HasColumnName("ob_value_code_description");
            entity.Property(e => e.Observation)
                .HasMaxLength(255)
                .HasColumnName("observation");
            entity.Property(e => e.ParentObservationId)
                .HasComment("FK to parent observation for sub-observations")
                .HasColumnType("bigint(20)")
                .HasColumnName("parent_observation_id");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.QuestionnaireResponseId)
                .HasComment("FK to questionnaire_response table")
                .HasColumnType("bigint(21)")
                .HasColumnName("questionnaire_response_id");
            entity.Property(e => e.ResultStatus)
                .HasMaxLength(32)
                .HasColumnName("result_status");
            entity.Property(e => e.TableCode)
                .HasMaxLength(255)
                .HasColumnName("table_code");
            entity.Property(e => e.User)
                .HasMaxLength(255)
                .HasColumnName("user");
            entity.Property(e => e.Uuid)
                .HasMaxLength(16)
                .IsFixedLength()
                .HasComment("UUID for the observation, used as unique logical identifier")
                .HasColumnName("uuid");
        });

        modelBuilder.Entity<FormQuestionnaireAssessment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("form_questionnaire_assessments");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(21)")
                .HasColumnName("id");
            entity.Property(e => e.Activity)
                .HasDefaultValueSql("'1'")
                .HasColumnType("tinyint(4)")
                .HasColumnName("activity");
            entity.Property(e => e.Authorized)
                .HasColumnType("tinyint(4)")
                .HasColumnName("authorized");
            entity.Property(e => e.Category)
                .HasMaxLength(64)
                .HasColumnName("category");
            entity.Property(e => e.Copyright)
                .HasColumnType("text")
                .HasColumnName("copyright");
            entity.Property(e => e.Date)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("date");
            entity.Property(e => e.FormName)
                .HasMaxLength(255)
                .HasColumnName("form_name");
            entity.Property(e => e.Groupname)
                .HasMaxLength(255)
                .HasColumnName("groupname");
            entity.Property(e => e.Lform).HasColumnName("lform");
            entity.Property(e => e.LformResponse).HasColumnName("lform_response");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(21)")
                .HasColumnName("pid");
            entity.Property(e => e.Questionnaire).HasColumnName("questionnaire");
            entity.Property(e => e.QuestionnaireId)
                .HasComment("The foreign id to the questionnaire_repository")
                .HasColumnType("text")
                .HasColumnName("questionnaire_id");
            entity.Property(e => e.QuestionnaireResponse).HasColumnName("questionnaire_response");
            entity.Property(e => e.ResponseId)
                .HasComment("The foreign id to the questionnaire_response repository")
                .HasColumnType("text")
                .HasColumnName("response_id");
            entity.Property(e => e.ResponseMeta)
                .HasComment("json meta data for the response resource")
                .HasColumnType("text")
                .HasColumnName("response_meta");
            entity.Property(e => e.User)
                .HasMaxLength(255)
                .HasColumnName("user");
        });

        modelBuilder.Entity<FormReviewof>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("form_reviewofs");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Activity)
                .HasColumnType("tinyint(4)")
                .HasColumnName("activity");
            entity.Property(e => e.AddisonSyndrom)
                .HasMaxLength(5)
                .HasColumnName("addison_syndrom");
            entity.Property(e => e.AdditionalNotes).HasColumnName("additional_notes");
            entity.Property(e => e.AnkleProblems)
                .HasMaxLength(5)
                .HasColumnName("ankle_problems");
            entity.Property(e => e.AnkylosingSondlilitis)
                .HasMaxLength(5)
                .HasColumnName("ankylosing_sondlilitis");
            entity.Property(e => e.Appendectomy)
                .HasMaxLength(5)
                .HasColumnName("appendectomy");
            entity.Property(e => e.Authorized)
                .HasColumnType("tinyint(4)")
                .HasColumnName("authorized");
            entity.Property(e => e.BackProblems)
                .HasMaxLength(5)
                .HasColumnName("back_problems");
            entity.Property(e => e.BackSurgery)
                .HasMaxLength(5)
                .HasColumnName("back_surgery");
            entity.Property(e => e.BladderCancer)
                .HasMaxLength(5)
                .HasColumnName("bladder_cancer");
            entity.Property(e => e.BladderInfections)
                .HasMaxLength(5)
                .HasColumnName("bladder_infections");
            entity.Property(e => e.BloodyNose)
                .HasMaxLength(5)
                .HasColumnName("bloody_nose");
            entity.Property(e => e.BlurredVision)
                .HasMaxLength(5)
                .HasColumnName("blurred_vision");
            entity.Property(e => e.BrokenBones)
                .HasMaxLength(5)
                .HasColumnName("broken_bones");
            entity.Property(e => e.BurningWithUrination)
                .HasMaxLength(5)
                .HasColumnName("burning_with_urination");
            entity.Property(e => e.CardiacCatheterization)
                .HasMaxLength(5)
                .HasColumnName("cardiac_catheterization");
            entity.Property(e => e.CataractSurgery)
                .HasMaxLength(5)
                .HasColumnName("cataract_surgery");
            entity.Property(e => e.Cataracts)
                .HasMaxLength(5)
                .HasColumnName("cataracts");
            entity.Property(e => e.ChestPains)
                .HasMaxLength(5)
                .HasColumnName("chest_pains");
            entity.Property(e => e.Chills)
                .HasMaxLength(5)
                .HasColumnName("chills");
            entity.Property(e => e.Cholecystectomy)
                .HasMaxLength(5)
                .HasColumnName("cholecystectomy");
            entity.Property(e => e.ChronicBronchitis)
                .HasMaxLength(5)
                .HasColumnName("chronic_bronchitis");
            entity.Property(e => e.CirrhosisOfTheLiver)
                .HasMaxLength(5)
                .HasColumnName("cirrhosis_of_the_liver");
            entity.Property(e => e.ColonCancer)
                .HasMaxLength(5)
                .HasColumnName("colon_cancer");
            entity.Property(e => e.ColonCancerSurgery)
                .HasMaxLength(5)
                .HasColumnName("colon_cancer_surgery");
            entity.Property(e => e.Colonoscopy)
                .HasMaxLength(5)
                .HasColumnName("colonoscopy");
            entity.Property(e => e.CoronaryArteryBypass)
                .HasMaxLength(5)
                .HasColumnName("coronary_artery_bypass");
            entity.Property(e => e.CrohnsDisease)
                .HasMaxLength(5)
                .HasColumnName("crohns_disease");
            entity.Property(e => e.CushingSyndrom)
                .HasMaxLength(5)
                .HasColumnName("cushing_syndrom");
            entity.Property(e => e.Date)
                .HasColumnType("datetime")
                .HasColumnName("date");
            entity.Property(e => e.Depressed)
                .HasMaxLength(5)
                .HasColumnName("depressed");
            entity.Property(e => e.DischargeFromUrethra)
                .HasMaxLength(5)
                .HasColumnName("discharge_from_urethra");
            entity.Property(e => e.Divirticulitis)
                .HasMaxLength(5)
                .HasColumnName("divirticulitis");
            entity.Property(e => e.DivirticulitisSurgery)
                .HasMaxLength(5)
                .HasColumnName("divirticulitis_surgery");
            entity.Property(e => e.DoubleVision)
                .HasMaxLength(5)
                .HasColumnName("double_vision");
            entity.Property(e => e.DryMouth)
                .HasMaxLength(5)
                .HasColumnName("dry_mouth");
            entity.Property(e => e.ElbowProblems)
                .HasMaxLength(5)
                .HasColumnName("elbow_problems");
            entity.Property(e => e.Emphysema)
                .HasMaxLength(5)
                .HasColumnName("emphysema");
            entity.Property(e => e.Endoscopy)
                .HasMaxLength(5)
                .HasColumnName("endoscopy");
            entity.Property(e => e.ExposureToForeignCountries)
                .HasMaxLength(5)
                .HasColumnName("exposure_to_foreign_countries");
            entity.Property(e => e.Fatigued)
                .HasMaxLength(5)
                .HasColumnName("fatigued");
            entity.Property(e => e.Fever)
                .HasMaxLength(5)
                .HasColumnName("fever");
            entity.Property(e => e.FootProblems)
                .HasMaxLength(5)
                .HasColumnName("foot_problems");
            entity.Property(e => e.GallStones)
                .HasMaxLength(5)
                .HasColumnName("gall_stones");
            entity.Property(e => e.Gastritis)
                .HasMaxLength(5)
                .HasColumnName("gastritis");
            entity.Property(e => e.Glaucoma)
                .HasMaxLength(5)
                .HasColumnName("glaucoma");
            entity.Property(e => e.Groupname)
                .HasMaxLength(255)
                .HasColumnName("groupname");
            entity.Property(e => e.HandProblems)
                .HasMaxLength(5)
                .HasColumnName("hand_problems");
            entity.Property(e => e.Headaches)
                .HasMaxLength(5)
                .HasColumnName("headaches");
            entity.Property(e => e.HeartAttack)
                .HasMaxLength(5)
                .HasColumnName("heart_attack");
            entity.Property(e => e.HeartFailure)
                .HasMaxLength(5)
                .HasColumnName("heart_failure");
            entity.Property(e => e.HeartTransplant)
                .HasMaxLength(5)
                .HasColumnName("heart_transplant");
            entity.Property(e => e.Hepatitis)
                .HasMaxLength(5)
                .HasColumnName("hepatitis");
            entity.Property(e => e.HerniatedDisc)
                .HasMaxLength(5)
                .HasColumnName("herniated_disc");
            entity.Property(e => e.Herpes)
                .HasMaxLength(5)
                .HasColumnName("herpes");
            entity.Property(e => e.HighBloodPressure)
                .HasMaxLength(5)
                .HasColumnName("high_blood_pressure");
            entity.Property(e => e.HipProblems)
                .HasMaxLength(5)
                .HasColumnName("hip_problems");
            entity.Property(e => e.Hyperactive)
                .HasMaxLength(5)
                .HasColumnName("hyperactive");
            entity.Property(e => e.Hyperthyroidism)
                .HasMaxLength(5)
                .HasColumnName("hyperthyroidism");
            entity.Property(e => e.Hypothyroidism)
                .HasMaxLength(5)
                .HasColumnName("hypothyroidism");
            entity.Property(e => e.Infections)
                .HasMaxLength(5)
                .HasColumnName("infections");
            entity.Property(e => e.Insomnia)
                .HasMaxLength(5)
                .HasColumnName("insomnia");
            entity.Property(e => e.InsulinDependentDiabetes)
                .HasMaxLength(5)
                .HasColumnName("insulin_dependent_diabetes");
            entity.Property(e => e.InterstitialLungDisease)
                .HasMaxLength(5)
                .HasColumnName("interstitial_lung_disease");
            entity.Property(e => e.IrregularHeartBeat)
                .HasMaxLength(5)
                .HasColumnName("irregular_heart_beat");
            entity.Property(e => e.KidneyCancer)
                .HasMaxLength(5)
                .HasColumnName("kidney_cancer");
            entity.Property(e => e.KidneyFailure)
                .HasMaxLength(5)
                .HasColumnName("kidney_failure");
            entity.Property(e => e.KidneyInfections)
                .HasMaxLength(5)
                .HasColumnName("kidney_infections");
            entity.Property(e => e.KidneyStones)
                .HasMaxLength(5)
                .HasColumnName("kidney_stones");
            entity.Property(e => e.KidneyTransplant)
                .HasMaxLength(5)
                .HasColumnName("kidney_transplant");
            entity.Property(e => e.KneeProblems)
                .HasMaxLength(5)
                .HasColumnName("knee_problems");
            entity.Property(e => e.LungCancer)
                .HasMaxLength(5)
                .HasColumnName("lung_cancer");
            entity.Property(e => e.LungCancerSurgery)
                .HasMaxLength(5)
                .HasColumnName("lung_cancer_surgery");
            entity.Property(e => e.Lupus)
                .HasMaxLength(5)
                .HasColumnName("lupus");
            entity.Property(e => e.NeckProblems)
                .HasMaxLength(5)
                .HasColumnName("neck_problems");
            entity.Property(e => e.NightSweats)
                .HasMaxLength(5)
                .HasColumnName("night_sweats");
            entity.Property(e => e.NoninsulinDependentDiabetes)
                .HasMaxLength(5)
                .HasColumnName("noninsulin_dependent_diabetes");
            entity.Property(e => e.Osetoarthritis)
                .HasMaxLength(5)
                .HasColumnName("osetoarthritis");
            entity.Property(e => e.Pemphigus)
                .HasMaxLength(5)
                .HasColumnName("pemphigus");
            entity.Property(e => e.PepticUlcerDisease)
                .HasMaxLength(5)
                .HasColumnName("peptic_ulcer_disease");
            entity.Property(e => e.Pheumothorax)
                .HasMaxLength(5)
                .HasColumnName("pheumothorax");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.Polyps)
                .HasMaxLength(5)
                .HasColumnName("polyps");
            entity.Property(e => e.PoorAppetite)
                .HasMaxLength(5)
                .HasColumnName("poor_appetite");
            entity.Property(e => e.PoorCirculation)
                .HasMaxLength(5)
                .HasColumnName("poor_circulation");
            entity.Property(e => e.PoorHearing)
                .HasMaxLength(5)
                .HasColumnName("poor_hearing");
            entity.Property(e => e.ProstateCancer)
                .HasMaxLength(5)
                .HasColumnName("prostate_cancer");
            entity.Property(e => e.ProstateProblems)
                .HasMaxLength(5)
                .HasColumnName("prostate_problems");
            entity.Property(e => e.Rashes)
                .HasMaxLength(5)
                .HasColumnName("rashes");
            entity.Property(e => e.RheumotoidArthritis)
                .HasMaxLength(5)
                .HasColumnName("rheumotoid_arthritis");
            entity.Property(e => e.RingingInEars)
                .HasMaxLength(5)
                .HasColumnName("ringing_in_ears");
            entity.Property(e => e.Scoliosis)
                .HasMaxLength(5)
                .HasColumnName("scoliosis");
            entity.Property(e => e.SexuallyTransmittedDisease)
                .HasMaxLength(5)
                .HasColumnName("sexually_transmitted_disease");
            entity.Property(e => e.ShortnessOfBreath)
                .HasMaxLength(5)
                .HasColumnName("shortness_of_breath");
            entity.Property(e => e.ShortnessOfBreath2)
                .HasMaxLength(5)
                .HasColumnName("shortness_of_breath_2");
            entity.Property(e => e.ShoulderProblems)
                .HasMaxLength(5)
                .HasColumnName("shoulder_problems");
            entity.Property(e => e.SinusSurgery)
                .HasMaxLength(5)
                .HasColumnName("sinus_surgery");
            entity.Property(e => e.Sinusitis)
                .HasMaxLength(5)
                .HasColumnName("sinusitis");
            entity.Property(e => e.Splenectomy)
                .HasMaxLength(5)
                .HasColumnName("splenectomy");
            entity.Property(e => e.StiffJoints)
                .HasMaxLength(5)
                .HasColumnName("stiff_joints");
            entity.Property(e => e.StomachPains)
                .HasMaxLength(5)
                .HasColumnName("stomach_pains");
            entity.Property(e => e.StrepThroat)
                .HasMaxLength(5)
                .HasColumnName("strep_throat");
            entity.Property(e => e.StressTest)
                .HasMaxLength(5)
                .HasColumnName("stress_test");
            entity.Property(e => e.SwollenJoints)
                .HasMaxLength(5)
                .HasColumnName("swollen_joints");
            entity.Property(e => e.SwollenLymphNodes)
                .HasMaxLength(5)
                .HasColumnName("swollen_lymph_nodes");
            entity.Property(e => e.ThroatCancer)
                .HasMaxLength(5)
                .HasColumnName("throat_cancer");
            entity.Property(e => e.ThroatCancerSurgery)
                .HasMaxLength(5)
                .HasColumnName("throat_cancer_surgery");
            entity.Property(e => e.Tonsillectomy)
                .HasMaxLength(5)
                .HasColumnName("tonsillectomy");
            entity.Property(e => e.Ulcerations)
                .HasMaxLength(5)
                .HasColumnName("ulcerations");
            entity.Property(e => e.UlcerativeColitis)
                .HasMaxLength(5)
                .HasColumnName("ulcerative_colitis");
            entity.Property(e => e.User)
                .HasMaxLength(255)
                .HasColumnName("user");
            entity.Property(e => e.VascularSurgery)
                .HasMaxLength(5)
                .HasColumnName("vascular_surgery");
            entity.Property(e => e.WeightLoss)
                .HasMaxLength(5)
                .HasColumnName("weight_loss");
            entity.Property(e => e.WristProblems)
                .HasMaxLength(5)
                .HasColumnName("wrist_problems");
        });

        modelBuilder.Entity<FormRo>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("form_ros");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.AbnormalBlood)
                .HasMaxLength(3)
                .HasColumnName("abnormal_blood");
            entity.Property(e => e.AbnormalHairGrowth)
                .HasMaxLength(3)
                .HasColumnName("abnormal_hair_growth");
            entity.Property(e => e.AbnormalMammogram)
                .HasMaxLength(3)
                .HasColumnName("abnormal_mammogram");
            entity.Property(e => e.Activity)
                .HasDefaultValueSql("'1'")
                .HasColumnType("int(11)")
                .HasColumnName("activity");
            entity.Property(e => e.Allergies)
                .HasMaxLength(3)
                .HasColumnName("allergies");
            entity.Property(e => e.Anemia)
                .HasMaxLength(3)
                .HasColumnName("anemia");
            entity.Property(e => e.Anorexia)
                .HasMaxLength(3)
                .HasColumnName("anorexia");
            entity.Property(e => e.Anxiety)
                .HasMaxLength(3)
                .HasColumnName("anxiety");
            entity.Property(e => e.Ap)
                .HasMaxLength(3)
                .HasColumnName("ap");
            entity.Property(e => e.Apnea)
                .HasMaxLength(3)
                .HasColumnName("apnea");
            entity.Property(e => e.Arrythmia)
                .HasMaxLength(3)
                .HasColumnName("arrythmia");
            entity.Property(e => e.Arthritis)
                .HasMaxLength(3)
                .HasColumnName("arthritis");
            entity.Property(e => e.Asthma)
                .HasMaxLength(3)
                .HasColumnName("asthma");
            entity.Property(e => e.Belching)
                .HasMaxLength(3)
                .HasColumnName("belching");
            entity.Property(e => e.Biopsy)
                .HasMaxLength(3)
                .HasColumnName("biopsy");
            entity.Property(e => e.BleedingProblems)
                .HasMaxLength(3)
                .HasColumnName("bleeding_problems");
            entity.Property(e => e.BlindSpots)
                .HasMaxLength(3)
                .HasColumnName("blind_spots");
            entity.Property(e => e.Bloating)
                .HasMaxLength(3)
                .HasColumnName("bloating");
            entity.Property(e => e.BreastDischarge)
                .HasMaxLength(3)
                .HasColumnName("breast_discharge");
            entity.Property(e => e.BreastMass)
                .HasMaxLength(3)
                .HasColumnName("breast_mass");
            entity.Property(e => e.ChangeInVision)
                .HasMaxLength(3)
                .HasColumnName("change_in_vision");
            entity.Property(e => e.ChangedBowel)
                .HasMaxLength(3)
                .HasColumnName("changed_bowel");
            entity.Property(e => e.ChestPain)
                .HasMaxLength(3)
                .HasColumnName("chest_pain");
            entity.Property(e => e.Chills)
                .HasMaxLength(3)
                .HasColumnName("chills");
            entity.Property(e => e.Constipation)
                .HasMaxLength(3)
                .HasColumnName("constipation");
            entity.Property(e => e.Copd)
                .HasMaxLength(3)
                .HasColumnName("copd");
            entity.Property(e => e.Cough)
                .HasMaxLength(3)
                .HasColumnName("cough");
            entity.Property(e => e.Date)
                .HasColumnType("datetime")
                .HasColumnName("date");
            entity.Property(e => e.Dementia)
                .HasMaxLength(3)
                .HasColumnName("dementia");
            entity.Property(e => e.Depression)
                .HasMaxLength(3)
                .HasColumnName("depression");
            entity.Property(e => e.Diabetes)
                .HasMaxLength(3)
                .HasColumnName("diabetes");
            entity.Property(e => e.Diarrhea)
                .HasMaxLength(3)
                .HasColumnName("diarrhea");
            entity.Property(e => e.Discharge)
                .HasMaxLength(3)
                .HasColumnName("discharge");
            entity.Property(e => e.Doe)
                .HasMaxLength(3)
                .HasColumnName("doe");
            entity.Property(e => e.DoubleVision)
                .HasMaxLength(3)
                .HasColumnName("double_vision");
            entity.Property(e => e.Dribbling)
                .HasMaxLength(3)
                .HasColumnName("dribbling");
            entity.Property(e => e.Dysphagia)
                .HasMaxLength(3)
                .HasColumnName("dysphagia");
            entity.Property(e => e.Dysuria)
                .HasMaxLength(3)
                .HasColumnName("dysuria");
            entity.Property(e => e.Edema)
                .HasMaxLength(3)
                .HasColumnName("edema");
            entity.Property(e => e.Ejaculations)
                .HasMaxLength(3)
                .HasColumnName("ejaculations");
            entity.Property(e => e.Erections)
                .HasMaxLength(3)
                .HasColumnName("erections");
            entity.Property(e => e.ExcessiveTearing)
                .HasMaxLength(3)
                .HasColumnName("excessive_tearing");
            entity.Property(e => e.EyePain)
                .HasMaxLength(3)
                .HasColumnName("eye_pain");
            entity.Property(e => e.FFlow)
                .HasMaxLength(3)
                .HasColumnName("f_flow");
            entity.Property(e => e.FFrequency)
                .HasMaxLength(3)
                .HasColumnName("f_frequency");
            entity.Property(e => e.FHirsutism)
                .HasMaxLength(3)
                .HasColumnName("f_hirsutism");
            entity.Property(e => e.FSymptoms)
                .HasMaxLength(3)
                .HasColumnName("f_symptoms");
            entity.Property(e => e.Fatigue)
                .HasMaxLength(3)
                .HasColumnName("fatigue");
            entity.Property(e => e.Fever)
                .HasMaxLength(3)
                .HasColumnName("fever");
            entity.Property(e => e.FhBloodProblems)
                .HasMaxLength(3)
                .HasColumnName("fh_blood_problems");
            entity.Property(e => e.Flatulence)
                .HasMaxLength(3)
                .HasColumnName("flatulence");
            entity.Property(e => e.Fms)
                .HasMaxLength(3)
                .HasColumnName("fms");
            entity.Property(e => e.FoodIntolerance)
                .HasMaxLength(3)
                .HasColumnName("food_intolerance");
            entity.Property(e => e.Frequency)
                .HasMaxLength(3)
                .HasColumnName("frequency");
            entity.Property(e => e.FrequentColds)
                .HasMaxLength(3)
                .HasColumnName("frequent_colds");
            entity.Property(e => e.FrequentIllness)
                .HasMaxLength(3)
                .HasColumnName("frequent_illness");
            entity.Property(e => e.G)
                .HasMaxLength(3)
                .HasColumnName("g");
            entity.Property(e => e.GastroPain)
                .HasMaxLength(3)
                .HasColumnName("gastro_pain");
            entity.Property(e => e.GlaucomaHistory)
                .HasMaxLength(3)
                .HasColumnName("glaucoma_history");
            entity.Property(e => e.HaiStatus)
                .HasMaxLength(3)
                .HasColumnName("hai_status");
            entity.Property(e => e.HearingLoss)
                .HasMaxLength(3)
                .HasColumnName("hearing_loss");
            entity.Property(e => e.HeartProblem)
                .HasMaxLength(3)
                .HasColumnName("heart_problem");
            entity.Property(e => e.Heartburn)
                .HasMaxLength(3)
                .HasColumnName("heartburn");
            entity.Property(e => e.HeatOrCold)
                .HasMaxLength(3)
                .HasColumnName("heat_or_cold");
            entity.Property(e => e.Hematemesis)
                .HasMaxLength(3)
                .HasColumnName("hematemesis");
            entity.Property(e => e.Hematochezia)
                .HasMaxLength(3)
                .HasColumnName("hematochezia");
            entity.Property(e => e.Hematuria)
                .HasMaxLength(3)
                .HasColumnName("hematuria");
            entity.Property(e => e.Hemoptsyis)
                .HasMaxLength(3)
                .HasColumnName("hemoptsyis");
            entity.Property(e => e.Hepatitis)
                .HasMaxLength(3)
                .HasColumnName("hepatitis");
            entity.Property(e => e.Hesitancy)
                .HasMaxLength(3)
                .HasColumnName("hesitancy");
            entity.Property(e => e.HistoryMurmur)
                .HasMaxLength(3)
                .HasColumnName("history_murmur");
            entity.Property(e => e.Hiv)
                .HasMaxLength(3)
                .HasColumnName("hiv");
            entity.Property(e => e.Incontinence)
                .HasMaxLength(3)
                .HasColumnName("incontinence");
            entity.Property(e => e.Insomnia)
                .HasMaxLength(3)
                .HasColumnName("insomnia");
            entity.Property(e => e.IntellectualDecline)
                .HasMaxLength(3)
                .HasColumnName("intellectual_decline");
            entity.Property(e => e.Intolerance)
                .HasMaxLength(3)
                .HasColumnName("intolerance");
            entity.Property(e => e.Irritability)
                .HasMaxLength(3)
                .HasColumnName("irritability");
            entity.Property(e => e.Irritation)
                .HasMaxLength(3)
                .HasColumnName("irritation");
            entity.Property(e => e.Jaundice)
                .HasMaxLength(3)
                .HasColumnName("jaundice");
            entity.Property(e => e.JointPain)
                .HasMaxLength(3)
                .HasColumnName("joint_pain");
            entity.Property(e => e.Lc)
                .HasMaxLength(3)
                .HasColumnName("lc");
            entity.Property(e => e.LegpainCramping)
                .HasMaxLength(3)
                .HasColumnName("legpain_cramping");
            entity.Property(e => e.Lmp)
                .HasMaxLength(3)
                .HasColumnName("lmp");
            entity.Property(e => e.Loc)
                .HasMaxLength(3)
                .HasColumnName("loc");
            entity.Property(e => e.MAches)
                .HasMaxLength(3)
                .HasColumnName("m_aches");
            entity.Property(e => e.MRedness)
                .HasMaxLength(3)
                .HasColumnName("m_redness");
            entity.Property(e => e.MStiffness)
                .HasMaxLength(3)
                .HasColumnName("m_stiffness");
            entity.Property(e => e.MWarm)
                .HasMaxLength(3)
                .HasColumnName("m_warm");
            entity.Property(e => e.Mearche)
                .HasMaxLength(3)
                .HasColumnName("mearche");
            entity.Property(e => e.MemoryProblems)
                .HasMaxLength(3)
                .HasColumnName("memory_problems");
            entity.Property(e => e.Menopause)
                .HasMaxLength(3)
                .HasColumnName("menopause");
            entity.Property(e => e.Muscle)
                .HasMaxLength(3)
                .HasColumnName("muscle");
            entity.Property(e => e.NHeadache)
                .HasMaxLength(3)
                .HasColumnName("n_headache");
            entity.Property(e => e.NNumbness)
                .HasMaxLength(3)
                .HasColumnName("n_numbness");
            entity.Property(e => e.NWeakness)
                .HasMaxLength(3)
                .HasColumnName("n_weakness");
            entity.Property(e => e.Nausea)
                .HasMaxLength(3)
                .HasColumnName("nausea");
            entity.Property(e => e.NightSweats)
                .HasMaxLength(3)
                .HasColumnName("night_sweats");
            entity.Property(e => e.Nocturia)
                .HasMaxLength(3)
                .HasColumnName("nocturia");
            entity.Property(e => e.Nosebleed)
                .HasMaxLength(3)
                .HasColumnName("nosebleed");
            entity.Property(e => e.Orthopnea)
                .HasMaxLength(3)
                .HasColumnName("orthopnea");
            entity.Property(e => e.P)
                .HasMaxLength(3)
                .HasColumnName("p");
            entity.Property(e => e.PDiagnosis)
                .HasMaxLength(3)
                .HasColumnName("p_diagnosis");
            entity.Property(e => e.PMedication)
                .HasMaxLength(3)
                .HasColumnName("p_medication");
            entity.Property(e => e.Pain)
                .HasMaxLength(3)
                .HasColumnName("pain");
            entity.Property(e => e.Palpitation)
                .HasMaxLength(3)
                .HasColumnName("palpitation");
            entity.Property(e => e.Paralysis)
                .HasMaxLength(3)
                .HasColumnName("paralysis");
            entity.Property(e => e.Peripheal)
                .HasMaxLength(3)
                .HasColumnName("peripheal");
            entity.Property(e => e.Photophobia)
                .HasMaxLength(3)
                .HasColumnName("photophobia");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.Pnd)
                .HasMaxLength(3)
                .HasColumnName("pnd");
            entity.Property(e => e.Polydypsia)
                .HasMaxLength(3)
                .HasColumnName("polydypsia");
            entity.Property(e => e.Polyuria)
                .HasMaxLength(3)
                .HasColumnName("polyuria");
            entity.Property(e => e.PostNasalDrip)
                .HasMaxLength(3)
                .HasColumnName("post_nasal_drip");
            entity.Property(e => e.Psoriasis)
                .HasMaxLength(3)
                .HasColumnName("psoriasis");
            entity.Property(e => e.Redness)
                .HasMaxLength(3)
                .HasColumnName("redness");
            entity.Property(e => e.RenalStones)
                .HasMaxLength(3)
                .HasColumnName("renal_stones");
            entity.Property(e => e.SAcne)
                .HasMaxLength(3)
                .HasColumnName("s_acne");
            entity.Property(e => e.SCancer)
                .HasMaxLength(3)
                .HasColumnName("s_cancer");
            entity.Property(e => e.SDisease)
                .HasMaxLength(3)
                .HasColumnName("s_disease");
            entity.Property(e => e.SOther)
                .HasMaxLength(3)
                .HasColumnName("s_other");
            entity.Property(e => e.Seizures)
                .HasMaxLength(3)
                .HasColumnName("seizures");
            entity.Property(e => e.ShortnessOfBreath)
                .HasMaxLength(3)
                .HasColumnName("shortness_of_breath");
            entity.Property(e => e.SinusProblems)
                .HasMaxLength(3)
                .HasColumnName("sinus_problems");
            entity.Property(e => e.Snoring)
                .HasMaxLength(3)
                .HasColumnName("snoring");
            entity.Property(e => e.SocialDifficulties)
                .HasMaxLength(3)
                .HasColumnName("social_difficulties");
            entity.Property(e => e.SoreThroat)
                .HasMaxLength(3)
                .HasColumnName("sore_throat");
            entity.Property(e => e.Sputum)
                .HasMaxLength(3)
                .HasColumnName("sputum");
            entity.Property(e => e.Stream)
                .HasMaxLength(3)
                .HasColumnName("stream");
            entity.Property(e => e.Stroke)
                .HasMaxLength(3)
                .HasColumnName("stroke");
            entity.Property(e => e.Swelling)
                .HasMaxLength(3)
                .HasColumnName("swelling");
            entity.Property(e => e.Syncope)
                .HasMaxLength(3)
                .HasColumnName("syncope");
            entity.Property(e => e.ThyroidProblems)
                .HasMaxLength(3)
                .HasColumnName("thyroid_problems");
            entity.Property(e => e.Tia)
                .HasMaxLength(3)
                .HasColumnName("tia");
            entity.Property(e => e.Tinnitus)
                .HasMaxLength(3)
                .HasColumnName("tinnitus");
            entity.Property(e => e.Urgency)
                .HasMaxLength(3)
                .HasColumnName("urgency");
            entity.Property(e => e.Utis)
                .HasMaxLength(3)
                .HasColumnName("utis");
            entity.Property(e => e.Vertigo)
                .HasMaxLength(3)
                .HasColumnName("vertigo");
            entity.Property(e => e.Vomiting)
                .HasMaxLength(3)
                .HasColumnName("vomiting");
            entity.Property(e => e.Weakness)
                .HasMaxLength(3)
                .HasColumnName("weakness");
            entity.Property(e => e.WeightChange)
                .HasMaxLength(3)
                .HasColumnName("weight_change");
            entity.Property(e => e.Wheezing)
                .HasMaxLength(3)
                .HasColumnName("wheezing");
        });

        modelBuilder.Entity<FormSoap>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("form_soap");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Activity)
                .HasDefaultValueSql("'0'")
                .HasColumnType("tinyint(4)")
                .HasColumnName("activity");
            entity.Property(e => e.Assessment)
                .HasColumnType("text")
                .HasColumnName("assessment");
            entity.Property(e => e.Authorized)
                .HasDefaultValueSql("'0'")
                .HasColumnType("tinyint(4)")
                .HasColumnName("authorized");
            entity.Property(e => e.Date)
                .HasColumnType("datetime")
                .HasColumnName("date");
            entity.Property(e => e.Groupname)
                .HasMaxLength(255)
                .HasColumnName("groupname");
            entity.Property(e => e.Objective)
                .HasColumnType("text")
                .HasColumnName("objective");
            entity.Property(e => e.Pid)
                .HasDefaultValueSql("'0'")
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.Plan)
                .HasColumnType("text")
                .HasColumnName("plan");
            entity.Property(e => e.Subjective)
                .HasColumnType("text")
                .HasColumnName("subjective");
            entity.Property(e => e.User)
                .HasMaxLength(255)
                .HasColumnName("user");
        });

        modelBuilder.Entity<FormTaskman>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("form_taskman");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("ID");
            entity.Property(e => e.Comment)
                .HasMaxLength(50)
                .HasColumnName("COMMENT");
            entity.Property(e => e.Completed)
                .HasMaxLength(1)
                .HasComment("1 = completed")
                .HasColumnName("COMPLETED");
            entity.Property(e => e.CompletedDate)
                .HasColumnType("datetime")
                .HasColumnName("COMPLETED_DATE");
            entity.Property(e => e.DocId)
                .HasColumnType("bigint(20)")
                .HasColumnName("DOC_ID");
            entity.Property(e => e.DocType)
                .HasMaxLength(20)
                .HasColumnName("DOC_TYPE");
            entity.Property(e => e.EncId)
                .HasColumnType("bigint(20)")
                .HasColumnName("ENC_ID");
            entity.Property(e => e.FromId)
                .HasColumnType("bigint(20)")
                .HasColumnName("FROM_ID");
            entity.Property(e => e.Method)
                .HasMaxLength(20)
                .HasColumnName("METHOD");
            entity.Property(e => e.PatientId)
                .HasColumnType("bigint(20)")
                .HasColumnName("PATIENT_ID");
            entity.Property(e => e.ReqDate)
                .HasColumnType("datetime")
                .HasColumnName("REQ_DATE");
            entity.Property(e => e.ToId)
                .HasColumnType("bigint(20)")
                .HasColumnName("TO_ID");
            entity.Property(e => e.Userfield1)
                .HasMaxLength(50)
                .HasColumnName("USERFIELD_1");
        });

        modelBuilder.Entity<FormVital>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("form_vitals");

            entity.HasIndex(e => e.Pid, "pid");

            entity.HasIndex(e => e.Uuid, "uuid").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Activity)
                .HasDefaultValueSql("'0'")
                .HasColumnType("tinyint(4)")
                .HasColumnName("activity");
            entity.Property(e => e.Authorized)
                .HasDefaultValueSql("'0'")
                .HasColumnType("tinyint(4)")
                .HasColumnName("authorized");
            entity.Property(e => e.Bmi)
                .HasPrecision(12, 6)
                .HasDefaultValueSql("'0.000000'")
                .HasColumnName("BMI");
            entity.Property(e => e.BmiStatus)
                .HasMaxLength(255)
                .HasColumnName("BMI_status");
            entity.Property(e => e.Bpd)
                .HasMaxLength(40)
                .HasColumnName("bpd");
            entity.Property(e => e.Bps)
                .HasMaxLength(40)
                .HasColumnName("bps");
            entity.Property(e => e.Date)
                .HasColumnType("datetime")
                .HasColumnName("date");
            entity.Property(e => e.ExternalId)
                .HasMaxLength(20)
                .HasColumnName("external_id");
            entity.Property(e => e.Groupname)
                .HasMaxLength(255)
                .HasColumnName("groupname");
            entity.Property(e => e.HeadCirc)
                .HasPrecision(12, 6)
                .HasDefaultValueSql("'0.000000'")
                .HasColumnName("head_circ");
            entity.Property(e => e.Height)
                .HasPrecision(12, 6)
                .HasDefaultValueSql("'0.000000'")
                .HasColumnName("height");
            entity.Property(e => e.InhaledOxygenConcentration)
                .HasPrecision(6, 2)
                .HasDefaultValueSql("'0.00'")
                .HasColumnName("inhaled_oxygen_concentration");
            entity.Property(e => e.LastUpdated)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("last_updated");
            entity.Property(e => e.Note)
                .HasMaxLength(255)
                .HasColumnName("note");
            entity.Property(e => e.OxygenFlowRate)
                .HasPrecision(12, 6)
                .HasDefaultValueSql("'0.000000'")
                .HasColumnName("oxygen_flow_rate");
            entity.Property(e => e.OxygenSaturation)
                .HasPrecision(6, 2)
                .HasDefaultValueSql("'0.00'")
                .HasColumnName("oxygen_saturation");
            entity.Property(e => e.PedBmi)
                .HasPrecision(6, 2)
                .HasDefaultValueSql("'0.00'")
                .HasColumnName("ped_bmi");
            entity.Property(e => e.PedHeadCirc)
                .HasPrecision(6, 2)
                .HasDefaultValueSql("'0.00'")
                .HasColumnName("ped_head_circ");
            entity.Property(e => e.PedWeightHeight)
                .HasPrecision(6, 2)
                .HasDefaultValueSql("'0.00'")
                .HasColumnName("ped_weight_height");
            entity.Property(e => e.Pid)
                .HasDefaultValueSql("'0'")
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.Pulse)
                .HasPrecision(12, 6)
                .HasDefaultValueSql("'0.000000'")
                .HasColumnName("pulse");
            entity.Property(e => e.Respiration)
                .HasPrecision(12, 6)
                .HasDefaultValueSql("'0.000000'")
                .HasColumnName("respiration");
            entity.Property(e => e.TempMethod)
                .HasMaxLength(255)
                .HasColumnName("temp_method");
            entity.Property(e => e.Temperature)
                .HasPrecision(12, 6)
                .HasDefaultValueSql("'0.000000'")
                .HasColumnName("temperature");
            entity.Property(e => e.User)
                .HasMaxLength(255)
                .HasColumnName("user");
            entity.Property(e => e.Uuid)
                .HasMaxLength(16)
                .IsFixedLength()
                .HasColumnName("uuid");
            entity.Property(e => e.WaistCirc)
                .HasPrecision(12, 6)
                .HasDefaultValueSql("'0.000000'")
                .HasColumnName("waist_circ");
            entity.Property(e => e.Weight)
                .HasPrecision(12, 6)
                .HasDefaultValueSql("'0.000000'")
                .HasColumnName("weight");
        });

        modelBuilder.Entity<FormVitalDetail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("form_vital_details", tb => tb.HasComment("Detailed information of each vital_forms observation column"));

            entity.HasIndex(e => e.FormId, "fk_form_id");

            entity.HasIndex(e => new { e.InterpretationListId, e.InterpretationOptionId }, "fk_list_options_id");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.FormId)
                .HasComment("FK to vital_forms.id")
                .HasColumnType("bigint(20)")
                .HasColumnName("form_id");
            entity.Property(e => e.InterpretationCodes)
                .HasMaxLength(255)
                .HasComment("Archived original codes value from list_options observation_interpretation")
                .HasColumnName("interpretation_codes");
            entity.Property(e => e.InterpretationListId)
                .HasMaxLength(100)
                .HasComment("FK to list_options.list_id for observation_interpretation")
                .HasColumnName("interpretation_list_id");
            entity.Property(e => e.InterpretationOptionId)
                .HasMaxLength(100)
                .HasComment("FK to list_options.option_id for observation_interpretation")
                .HasColumnName("interpretation_option_id");
            entity.Property(e => e.InterpretationTitle)
                .HasMaxLength(255)
                .HasComment("Archived original title value from list_options observation_interpretation")
                .HasColumnName("interpretation_title");
            entity.Property(e => e.ReasonCode)
                .HasMaxLength(31)
                .HasComment("Medical code explaining reason of the vital observation value in form codesystem:codetype;...;")
                .HasColumnName("reason_code");
            entity.Property(e => e.ReasonDescription)
                .HasComment("Human readable text description of the reason_code column")
                .HasColumnType("text")
                .HasColumnName("reason_description");
            entity.Property(e => e.ReasonStatus)
                .HasMaxLength(31)
                .HasComment("The status of the reason ie completed, in progress, etc")
                .HasColumnName("reason_status");
            entity.Property(e => e.VitalsColumn)
                .HasMaxLength(64)
                .HasComment("Column name from form_vitals")
                .HasColumnName("vitals_column");
        });

        modelBuilder.Entity<FormVitalsCalculation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("form_vitals_calculation", tb => tb.HasComment("Main calculation records - one per logical calculation (e.g., average BP)"));

            entity.HasIndex(e => e.CalculationId, "idx_calculation_id");

            entity.HasIndex(e => e.Encounter, "idx_encounter");

            entity.HasIndex(e => e.Pid, "idx_pid");

            entity.HasIndex(e => e.Uuid, "unq_uuid").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.CalculationId)
                .HasMaxLength(64)
                .HasComment("application identifier representing calculation e.g., bp-MeanLast5, bp-Mean3Day, bp-MeanEncounter")
                .HasColumnName("calculation_id");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasColumnType("bigint(20)")
                .HasColumnName("created_by");
            entity.Property(e => e.DateEnd)
                .HasColumnType("datetime")
                .HasColumnName("date_end");
            entity.Property(e => e.DateStart)
                .HasColumnType("datetime")
                .HasColumnName("date_start");
            entity.Property(e => e.Encounter)
                .HasComment("fk to form_encounter.id")
                .HasColumnType("bigint(20)")
                .HasColumnName("encounter");
            entity.Property(e => e.Pid)
                .HasComment("fk to patient_data.pid")
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("datetime")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasColumnType("bigint(20)")
                .HasColumnName("updated_by");
            entity.Property(e => e.Uuid)
                .HasMaxLength(16)
                .IsFixedLength()
                .HasColumnName("uuid");
        });

        modelBuilder.Entity<FormVitalsCalculationComponent>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("form_vitals_calculation_components", tb => tb.HasComment("Component values for calculations (e.g., systolic=120, diastolic=80)"));

            entity.HasIndex(e => new { e.FvcUuid, e.ComponentOrder }, "idx_component_order");

            entity.HasIndex(e => e.VitalsColumn, "idx_vitals_column");

            entity.HasIndex(e => new { e.FvcUuid, e.VitalsColumn }, "unq_fvc_component").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.ComponentOrder)
                .HasComment("Display order for components")
                .HasColumnType("int(11)")
                .HasColumnName("component_order");
            entity.Property(e => e.FvcUuid)
                .HasMaxLength(16)
                .IsFixedLength()
                .HasComment("fk to form_vitals_calculation.uuid")
                .HasColumnName("fvc_uuid");
            entity.Property(e => e.Value)
                .HasPrecision(12, 6)
                .HasComment("Calculated numeric component value")
                .HasColumnName("value");
            entity.Property(e => e.ValueString)
                .HasMaxLength(255)
                .HasComment("Calculated non-numeric component value")
                .HasColumnName("value_string");
            entity.Property(e => e.ValueUnit)
                .HasMaxLength(16)
                .HasComment("Unit for this component value")
                .HasColumnName("value_unit");
            entity.Property(e => e.VitalsColumn)
                .HasMaxLength(64)
                .HasComment("Component type: bps, bpd, pulse, etc.")
                .HasColumnName("vitals_column");
        });

        modelBuilder.Entity<FormVitalsCalculationFormVital>(entity =>
        {
            entity.HasKey(e => new { e.FvcUuid, e.VitalsId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("form_vitals_calculation_form_vitals", tb => tb.HasComment("Join table between form_vitals_calculation and form_vitals table representing the derivative observation relationship between the calculation and the source records"));

            entity.Property(e => e.FvcUuid)
                .HasMaxLength(16)
                .IsFixedLength()
                .HasComment("fk to form_vitals_calculation.uuid")
                .HasColumnName("fvc_uuid");
            entity.Property(e => e.VitalsId)
                .HasComment("fk to form_vitals.id")
                .HasColumnType("bigint(20)")
                .HasColumnName("vitals_id");
        });

        modelBuilder.Entity<GaclAcl>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("gacl_acl");

            entity.HasIndex(e => e.Enabled, "gacl_enabled_acl");

            entity.HasIndex(e => e.SectionValue, "gacl_section_value_acl");

            entity.HasIndex(e => e.UpdatedDate, "gacl_updated_date_acl");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.Allow)
                .HasColumnType("int(11)")
                .HasColumnName("allow");
            entity.Property(e => e.Enabled)
                .HasColumnType("int(11)")
                .HasColumnName("enabled");
            entity.Property(e => e.Note)
                .HasColumnType("text")
                .HasColumnName("note");
            entity.Property(e => e.ReturnValue)
                .HasColumnType("text")
                .HasColumnName("return_value");
            entity.Property(e => e.SectionValue)
                .HasMaxLength(150)
                .HasDefaultValueSql("'system'")
                .HasColumnName("section_value");
            entity.Property(e => e.UpdatedDate)
                .HasColumnType("int(11)")
                .HasColumnName("updated_date");
        });

        modelBuilder.Entity<GaclAclSection>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("gacl_acl_sections");

            entity.HasIndex(e => e.Hidden, "gacl_hidden_acl_sections");

            entity.HasIndex(e => e.Value, "gacl_value_acl_sections").IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.Hidden)
                .HasColumnType("int(11)")
                .HasColumnName("hidden");
            entity.Property(e => e.Name)
                .HasMaxLength(230)
                .HasColumnName("name");
            entity.Property(e => e.OrderValue)
                .HasColumnType("int(11)")
                .HasColumnName("order_value");
            entity.Property(e => e.Value)
                .HasMaxLength(150)
                .HasColumnName("value");
        });

        modelBuilder.Entity<GaclAclSeq>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("gacl_acl_seq");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
        });

        modelBuilder.Entity<GaclAco>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("gacl_aco");

            entity.HasIndex(e => e.Hidden, "gacl_hidden_aco");

            entity.HasIndex(e => new { e.SectionValue, e.Value }, "gacl_section_value_value_aco").IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.Hidden)
                .HasColumnType("int(11)")
                .HasColumnName("hidden");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.OrderValue)
                .HasColumnType("int(11)")
                .HasColumnName("order_value");
            entity.Property(e => e.SectionValue)
                .HasMaxLength(150)
                .HasDefaultValueSql("'0'")
                .HasColumnName("section_value");
            entity.Property(e => e.Value)
                .HasMaxLength(150)
                .HasColumnName("value");
        });

        modelBuilder.Entity<GaclAcoMap>(entity =>
        {
            entity.HasKey(e => new { e.AclId, e.SectionValue, e.Value })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0, 0 });

            entity.ToTable("gacl_aco_map");

            entity.Property(e => e.AclId)
                .HasColumnType("int(11)")
                .HasColumnName("acl_id");
            entity.Property(e => e.SectionValue)
                .HasMaxLength(150)
                .HasDefaultValueSql("'0'")
                .HasColumnName("section_value");
            entity.Property(e => e.Value)
                .HasMaxLength(150)
                .HasColumnName("value");
        });

        modelBuilder.Entity<GaclAcoSection>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("gacl_aco_sections");

            entity.HasIndex(e => e.Hidden, "gacl_hidden_aco_sections");

            entity.HasIndex(e => e.Value, "gacl_value_aco_sections").IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.Hidden)
                .HasColumnType("int(11)")
                .HasColumnName("hidden");
            entity.Property(e => e.Name)
                .HasMaxLength(230)
                .HasColumnName("name");
            entity.Property(e => e.OrderValue)
                .HasColumnType("int(11)")
                .HasColumnName("order_value");
            entity.Property(e => e.Value)
                .HasMaxLength(150)
                .HasColumnName("value");
        });

        modelBuilder.Entity<GaclAcoSectionsSeq>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("gacl_aco_sections_seq");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
        });

        modelBuilder.Entity<GaclAcoSeq>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("gacl_aco_seq");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
        });

        modelBuilder.Entity<GaclAro>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("gacl_aro");

            entity.HasIndex(e => e.Hidden, "gacl_hidden_aro");

            entity.HasIndex(e => new { e.SectionValue, e.Value }, "gacl_section_value_value_aro").IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.Hidden)
                .HasColumnType("int(11)")
                .HasColumnName("hidden");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.OrderValue)
                .HasColumnType("int(11)")
                .HasColumnName("order_value");
            entity.Property(e => e.SectionValue)
                .HasMaxLength(150)
                .HasDefaultValueSql("'0'")
                .HasColumnName("section_value");
            entity.Property(e => e.Value)
                .HasMaxLength(150)
                .HasColumnName("value");
        });

        modelBuilder.Entity<GaclAroGroup>(entity =>
        {
            entity.HasKey(e => new { e.Id, e.Value })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("gacl_aro_groups");

            entity.HasIndex(e => new { e.Lft, e.Rgt }, "gacl_lft_rgt_aro_groups");

            entity.HasIndex(e => e.ParentId, "gacl_parent_id_aro_groups");

            entity.HasIndex(e => e.Value, "gacl_value_aro_groups").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.Value)
                .HasMaxLength(150)
                .HasColumnName("value");
            entity.Property(e => e.Lft)
                .HasColumnType("int(11)")
                .HasColumnName("lft");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.ParentId)
                .HasColumnType("int(11)")
                .HasColumnName("parent_id");
            entity.Property(e => e.Rgt)
                .HasColumnType("int(11)")
                .HasColumnName("rgt");
        });

        modelBuilder.Entity<GaclAroGroupsIdSeq>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("gacl_aro_groups_id_seq");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
        });

        modelBuilder.Entity<GaclAroGroupsMap>(entity =>
        {
            entity.HasKey(e => new { e.AclId, e.GroupId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("gacl_aro_groups_map");

            entity.Property(e => e.AclId)
                .HasColumnType("int(11)")
                .HasColumnName("acl_id");
            entity.Property(e => e.GroupId)
                .HasColumnType("int(11)")
                .HasColumnName("group_id");
        });

        modelBuilder.Entity<GaclAroMap>(entity =>
        {
            entity.HasKey(e => new { e.AclId, e.SectionValue, e.Value })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0, 0 });

            entity.ToTable("gacl_aro_map");

            entity.Property(e => e.AclId)
                .HasColumnType("int(11)")
                .HasColumnName("acl_id");
            entity.Property(e => e.SectionValue)
                .HasMaxLength(150)
                .HasDefaultValueSql("'0'")
                .HasColumnName("section_value");
            entity.Property(e => e.Value)
                .HasMaxLength(150)
                .HasColumnName("value");
        });

        modelBuilder.Entity<GaclAroSection>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("gacl_aro_sections");

            entity.HasIndex(e => e.Hidden, "gacl_hidden_aro_sections");

            entity.HasIndex(e => e.Value, "gacl_value_aro_sections").IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.Hidden)
                .HasColumnType("int(11)")
                .HasColumnName("hidden");
            entity.Property(e => e.Name)
                .HasMaxLength(230)
                .HasColumnName("name");
            entity.Property(e => e.OrderValue)
                .HasColumnType("int(11)")
                .HasColumnName("order_value");
            entity.Property(e => e.Value)
                .HasMaxLength(150)
                .HasColumnName("value");
        });

        modelBuilder.Entity<GaclAroSectionsSeq>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("gacl_aro_sections_seq");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
        });

        modelBuilder.Entity<GaclAroSeq>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("gacl_aro_seq");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
        });

        modelBuilder.Entity<GaclAxo>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("gacl_axo");

            entity.HasIndex(e => e.Hidden, "gacl_hidden_axo");

            entity.HasIndex(e => new { e.SectionValue, e.Value }, "gacl_section_value_value_axo").IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.Hidden)
                .HasColumnType("int(11)")
                .HasColumnName("hidden");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.OrderValue)
                .HasColumnType("int(11)")
                .HasColumnName("order_value");
            entity.Property(e => e.SectionValue)
                .HasMaxLength(150)
                .HasDefaultValueSql("'0'")
                .HasColumnName("section_value");
            entity.Property(e => e.Value)
                .HasMaxLength(150)
                .HasColumnName("value");
        });

        modelBuilder.Entity<GaclAxoGroup>(entity =>
        {
            entity.HasKey(e => new { e.Id, e.Value })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("gacl_axo_groups");

            entity.HasIndex(e => new { e.Lft, e.Rgt }, "gacl_lft_rgt_axo_groups");

            entity.HasIndex(e => e.ParentId, "gacl_parent_id_axo_groups");

            entity.HasIndex(e => e.Value, "gacl_value_axo_groups").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.Value)
                .HasMaxLength(150)
                .HasColumnName("value");
            entity.Property(e => e.Lft)
                .HasColumnType("int(11)")
                .HasColumnName("lft");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.ParentId)
                .HasColumnType("int(11)")
                .HasColumnName("parent_id");
            entity.Property(e => e.Rgt)
                .HasColumnType("int(11)")
                .HasColumnName("rgt");
        });

        modelBuilder.Entity<GaclAxoGroupsMap>(entity =>
        {
            entity.HasKey(e => new { e.AclId, e.GroupId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("gacl_axo_groups_map");

            entity.Property(e => e.AclId)
                .HasColumnType("int(11)")
                .HasColumnName("acl_id");
            entity.Property(e => e.GroupId)
                .HasColumnType("int(11)")
                .HasColumnName("group_id");
        });

        modelBuilder.Entity<GaclAxoMap>(entity =>
        {
            entity.HasKey(e => new { e.AclId, e.SectionValue, e.Value })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0, 0 });

            entity.ToTable("gacl_axo_map");

            entity.Property(e => e.AclId)
                .HasColumnType("int(11)")
                .HasColumnName("acl_id");
            entity.Property(e => e.SectionValue)
                .HasMaxLength(150)
                .HasDefaultValueSql("'0'")
                .HasColumnName("section_value");
            entity.Property(e => e.Value)
                .HasMaxLength(150)
                .HasColumnName("value");
        });

        modelBuilder.Entity<GaclAxoSection>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("gacl_axo_sections");

            entity.HasIndex(e => e.Hidden, "gacl_hidden_axo_sections");

            entity.HasIndex(e => e.Value, "gacl_value_axo_sections").IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.Hidden)
                .HasColumnType("int(11)")
                .HasColumnName("hidden");
            entity.Property(e => e.Name)
                .HasMaxLength(230)
                .HasColumnName("name");
            entity.Property(e => e.OrderValue)
                .HasColumnType("int(11)")
                .HasColumnName("order_value");
            entity.Property(e => e.Value)
                .HasMaxLength(150)
                .HasColumnName("value");
        });

        modelBuilder.Entity<GaclGroupsAroMap>(entity =>
        {
            entity.HasKey(e => new { e.GroupId, e.AroId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("gacl_groups_aro_map");

            entity.HasIndex(e => e.AroId, "gacl_aro_id");

            entity.Property(e => e.GroupId)
                .HasColumnType("int(11)")
                .HasColumnName("group_id");
            entity.Property(e => e.AroId)
                .HasColumnType("int(11)")
                .HasColumnName("aro_id");
        });

        modelBuilder.Entity<GaclGroupsAxoMap>(entity =>
        {
            entity.HasKey(e => new { e.GroupId, e.AxoId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("gacl_groups_axo_map");

            entity.HasIndex(e => e.AxoId, "gacl_axo_id");

            entity.Property(e => e.GroupId)
                .HasColumnType("int(11)")
                .HasColumnName("group_id");
            entity.Property(e => e.AxoId)
                .HasColumnType("int(11)")
                .HasColumnName("axo_id");
        });

        modelBuilder.Entity<GaclPhpgacl>(entity =>
        {
            entity.HasKey(e => e.Name).HasName("PRIMARY");

            entity.ToTable("gacl_phpgacl");

            entity.Property(e => e.Name)
                .HasMaxLength(230)
                .HasColumnName("name");
            entity.Property(e => e.Value)
                .HasMaxLength(150)
                .HasColumnName("value");
        });

        modelBuilder.Entity<Global>(entity =>
        {
            entity.HasKey(e => new { e.GlName, e.GlIndex })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("globals");

            entity.Property(e => e.GlName)
                .HasMaxLength(63)
                .HasColumnName("gl_name");
            entity.Property(e => e.GlIndex)
                .HasColumnType("int(11)")
                .HasColumnName("gl_index");
            entity.Property(e => e.GlValue)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("gl_value");
        });

        modelBuilder.Entity<Gprelation>(entity =>
        {
            entity.HasKey(e => new { e.Type1, e.Id1, e.Type2, e.Id2 })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0, 0, 0 });

            entity.ToTable("gprelations", tb => tb.HasComment("general purpose relations"));

            entity.HasIndex(e => new { e.Type2, e.Id2 }, "key2");

            entity.Property(e => e.Type1)
                .HasColumnType("int(2)")
                .HasColumnName("type1");
            entity.Property(e => e.Id1)
                .HasColumnType("bigint(20)")
                .HasColumnName("id1");
            entity.Property(e => e.Type2)
                .HasColumnType("int(2)")
                .HasColumnName("type2");
            entity.Property(e => e.Id2)
                .HasColumnType("bigint(20)")
                .HasColumnName("id2");
        });

        modelBuilder.Entity<Group>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("groups");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.User).HasColumnName("user");
        });

        modelBuilder.Entity<HistoryDatum>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("history_data");

            entity.HasIndex(e => e.Pid, "pid");

            entity.HasIndex(e => e.Uuid, "uuid").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.AdditionalHistory)
                .HasColumnType("text")
                .HasColumnName("additional_history");
            entity.Property(e => e.Alcohol).HasColumnName("alcohol");
            entity.Property(e => e.Appendectomy)
                .HasColumnType("datetime")
                .HasColumnName("appendectomy");
            entity.Property(e => e.CataractSurgery)
                .HasColumnType("datetime")
                .HasColumnName("cataract_surgery");
            entity.Property(e => e.Cholecystestomy)
                .HasColumnType("datetime")
                .HasColumnName("cholecystestomy");
            entity.Property(e => e.Coffee).HasColumnName("coffee");
            entity.Property(e => e.Counseling).HasColumnName("counseling");
            entity.Property(e => e.CreatedBy)
                .HasComment("users.id the user that first created this record")
                .HasColumnType("bigint(20)")
                .HasColumnName("created_by");
            entity.Property(e => e.Date)
                .HasColumnType("datetime")
                .HasColumnName("date");
            entity.Property(e => e.DcFather)
                .HasColumnType("text")
                .HasColumnName("dc_father");
            entity.Property(e => e.DcMother)
                .HasColumnType("text")
                .HasColumnName("dc_mother");
            entity.Property(e => e.DcOffspring)
                .HasColumnType("text")
                .HasColumnName("dc_offspring");
            entity.Property(e => e.DcSiblings)
                .HasColumnType("text")
                .HasColumnName("dc_siblings");
            entity.Property(e => e.DcSpouse)
                .HasColumnType("text")
                .HasColumnName("dc_spouse");
            entity.Property(e => e.Exams)
                .HasColumnType("text")
                .HasColumnName("exams");
            entity.Property(e => e.ExercisePatterns).HasColumnName("exercise_patterns");
            entity.Property(e => e.HazardousActivities).HasColumnName("hazardous_activities");
            entity.Property(e => e.HeartSurgery)
                .HasColumnType("datetime")
                .HasColumnName("heart_surgery");
            entity.Property(e => e.HerniaRepair)
                .HasColumnType("datetime")
                .HasColumnName("hernia_repair");
            entity.Property(e => e.HipReplacement)
                .HasColumnType("datetime")
                .HasColumnName("hip_replacement");
            entity.Property(e => e.HistoryFather).HasColumnName("history_father");
            entity.Property(e => e.HistoryMother).HasColumnName("history_mother");
            entity.Property(e => e.HistoryOffspring).HasColumnName("history_offspring");
            entity.Property(e => e.HistorySiblings).HasColumnName("history_siblings");
            entity.Property(e => e.HistorySpouse).HasColumnName("history_spouse");
            entity.Property(e => e.Hysterectomy)
                .HasColumnType("datetime")
                .HasColumnName("hysterectomy");
            entity.Property(e => e.KneeReplacement)
                .HasColumnType("datetime")
                .HasColumnName("knee_replacement");
            entity.Property(e => e.LastBreastExam)
                .HasMaxLength(255)
                .HasColumnName("last_breast_exam");
            entity.Property(e => e.LastCardiacEcho)
                .HasMaxLength(255)
                .HasColumnName("last_cardiac_echo");
            entity.Property(e => e.LastEcg)
                .HasMaxLength(255)
                .HasColumnName("last_ecg");
            entity.Property(e => e.LastExamResults)
                .HasMaxLength(255)
                .HasColumnName("last_exam_results");
            entity.Property(e => e.LastFluvax)
                .HasMaxLength(255)
                .HasColumnName("last_fluvax");
            entity.Property(e => e.LastGynocologicalExam)
                .HasMaxLength(255)
                .HasColumnName("last_gynocological_exam");
            entity.Property(e => e.LastHemoglobin)
                .HasMaxLength(255)
                .HasColumnName("last_hemoglobin");
            entity.Property(e => e.LastLdl)
                .HasMaxLength(255)
                .HasColumnName("last_ldl");
            entity.Property(e => e.LastMammogram)
                .HasMaxLength(255)
                .HasColumnName("last_mammogram");
            entity.Property(e => e.LastPhysicalExam)
                .HasMaxLength(255)
                .HasColumnName("last_physical_exam");
            entity.Property(e => e.LastPneuvax)
                .HasMaxLength(255)
                .HasColumnName("last_pneuvax");
            entity.Property(e => e.LastProstateExam)
                .HasMaxLength(255)
                .HasColumnName("last_prostate_exam");
            entity.Property(e => e.LastPsa)
                .HasMaxLength(255)
                .HasColumnName("last_psa");
            entity.Property(e => e.LastRectalExam)
                .HasMaxLength(255)
                .HasColumnName("last_rectal_exam");
            entity.Property(e => e.LastRetinal)
                .HasMaxLength(255)
                .HasColumnName("last_retinal");
            entity.Property(e => e.LastSigmoidoscopyColonoscopy)
                .HasMaxLength(255)
                .HasColumnName("last_sigmoidoscopy_colonoscopy");
            entity.Property(e => e.Name1)
                .HasMaxLength(255)
                .HasColumnName("name_1");
            entity.Property(e => e.Name2)
                .HasMaxLength(255)
                .HasColumnName("name_2");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.RecreationalDrugs).HasColumnName("recreational_drugs");
            entity.Property(e => e.RelativesCancer).HasColumnName("relatives_cancer");
            entity.Property(e => e.RelativesDiabetes).HasColumnName("relatives_diabetes");
            entity.Property(e => e.RelativesEpilepsy).HasColumnName("relatives_epilepsy");
            entity.Property(e => e.RelativesHeartProblems).HasColumnName("relatives_heart_problems");
            entity.Property(e => e.RelativesHighBloodPressure).HasColumnName("relatives_high_blood_pressure");
            entity.Property(e => e.RelativesMentalIllness).HasColumnName("relatives_mental_illness");
            entity.Property(e => e.RelativesStroke).HasColumnName("relatives_stroke");
            entity.Property(e => e.RelativesSuicide).HasColumnName("relatives_suicide");
            entity.Property(e => e.RelativesTuberculosis).HasColumnName("relatives_tuberculosis");
            entity.Property(e => e.SeatbeltUse).HasColumnName("seatbelt_use");
            entity.Property(e => e.SleepPatterns).HasColumnName("sleep_patterns");
            entity.Property(e => e.Tobacco).HasColumnName("tobacco");
            entity.Property(e => e.Tonsillectomy)
                .HasColumnType("datetime")
                .HasColumnName("tonsillectomy");
            entity.Property(e => e.Userarea11)
                .HasColumnType("text")
                .HasColumnName("userarea11");
            entity.Property(e => e.Userarea12)
                .HasColumnType("text")
                .HasColumnName("userarea12");
            entity.Property(e => e.Userdate11).HasColumnName("userdate11");
            entity.Property(e => e.Userdate12).HasColumnName("userdate12");
            entity.Property(e => e.Userdate13).HasColumnName("userdate13");
            entity.Property(e => e.Userdate14).HasColumnName("userdate14");
            entity.Property(e => e.Userdate15).HasColumnName("userdate15");
            entity.Property(e => e.Usertext11)
                .HasColumnType("text")
                .HasColumnName("usertext11");
            entity.Property(e => e.Usertext12)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("usertext12");
            entity.Property(e => e.Usertext13)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("usertext13");
            entity.Property(e => e.Usertext14)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("usertext14");
            entity.Property(e => e.Usertext15)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("usertext15");
            entity.Property(e => e.Usertext16)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("usertext16");
            entity.Property(e => e.Usertext17)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("usertext17");
            entity.Property(e => e.Usertext18)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("usertext18");
            entity.Property(e => e.Usertext19)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("usertext19");
            entity.Property(e => e.Usertext20)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("usertext20");
            entity.Property(e => e.Usertext21)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("usertext21");
            entity.Property(e => e.Usertext22)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("usertext22");
            entity.Property(e => e.Usertext23)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("usertext23");
            entity.Property(e => e.Usertext24)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("usertext24");
            entity.Property(e => e.Usertext25)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("usertext25");
            entity.Property(e => e.Usertext26)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("usertext26");
            entity.Property(e => e.Usertext27)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("usertext27");
            entity.Property(e => e.Usertext28)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("usertext28");
            entity.Property(e => e.Usertext29)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("usertext29");
            entity.Property(e => e.Usertext30)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("usertext30");
            entity.Property(e => e.Uuid)
                .HasMaxLength(16)
                .IsFixedLength()
                .HasColumnName("uuid");
            entity.Property(e => e.Value1)
                .HasMaxLength(255)
                .HasColumnName("value_1");
            entity.Property(e => e.Value2)
                .HasMaxLength(255)
                .HasColumnName("value_2");
        });

        modelBuilder.Entity<Icd10DxOrderCode>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("icd10_dx_order_code");

            entity.HasIndex(e => e.Active, "active");

            entity.HasIndex(e => e.DxId, "dx_id").IsUnique();

            entity.HasIndex(e => e.FormattedDxCode, "formatted_dx_code");

            entity.Property(e => e.Active)
                .HasDefaultValueSql("'0'")
                .HasColumnType("tinyint(4)")
                .HasColumnName("active");
            entity.Property(e => e.DxCode)
                .HasMaxLength(7)
                .HasColumnName("dx_code");
            entity.Property(e => e.DxId)
                .ValueGeneratedOnAdd()
                .HasColumnType("bigint(20) unsigned")
                .HasColumnName("dx_id");
            entity.Property(e => e.FormattedDxCode)
                .HasMaxLength(10)
                .HasColumnName("formatted_dx_code");
            entity.Property(e => e.LongDesc)
                .HasColumnType("text")
                .HasColumnName("long_desc");
            entity.Property(e => e.Revision)
                .HasDefaultValueSql("'0'")
                .HasColumnType("int(11)")
                .HasColumnName("revision");
            entity.Property(e => e.ShortDesc)
                .HasMaxLength(60)
                .HasColumnName("short_desc");
            entity.Property(e => e.ValidForCoding)
                .HasMaxLength(1)
                .IsFixedLength()
                .HasColumnName("valid_for_coding");
        });

        modelBuilder.Entity<Icd10GemDx109>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("icd10_gem_dx_10_9");

            entity.HasIndex(e => e.MapId, "map_id").IsUnique();

            entity.Property(e => e.Active)
                .HasDefaultValueSql("'0'")
                .HasColumnType("tinyint(4)")
                .HasColumnName("active");
            entity.Property(e => e.DxIcd10Source)
                .HasMaxLength(7)
                .HasColumnName("dx_icd10_source");
            entity.Property(e => e.DxIcd9Target)
                .HasMaxLength(5)
                .HasColumnName("dx_icd9_target");
            entity.Property(e => e.Flags)
                .HasMaxLength(5)
                .HasColumnName("flags");
            entity.Property(e => e.MapId)
                .ValueGeneratedOnAdd()
                .HasColumnType("bigint(20) unsigned")
                .HasColumnName("map_id");
            entity.Property(e => e.Revision)
                .HasDefaultValueSql("'0'")
                .HasColumnType("int(11)")
                .HasColumnName("revision");
        });

        modelBuilder.Entity<Icd10GemDx910>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("icd10_gem_dx_9_10");

            entity.HasIndex(e => e.MapId, "map_id").IsUnique();

            entity.Property(e => e.Active)
                .HasDefaultValueSql("'0'")
                .HasColumnType("tinyint(4)")
                .HasColumnName("active");
            entity.Property(e => e.DxIcd10Target)
                .HasMaxLength(7)
                .HasColumnName("dx_icd10_target");
            entity.Property(e => e.DxIcd9Source)
                .HasMaxLength(5)
                .HasColumnName("dx_icd9_source");
            entity.Property(e => e.Flags)
                .HasMaxLength(5)
                .HasColumnName("flags");
            entity.Property(e => e.MapId)
                .ValueGeneratedOnAdd()
                .HasColumnType("bigint(20) unsigned")
                .HasColumnName("map_id");
            entity.Property(e => e.Revision)
                .HasDefaultValueSql("'0'")
                .HasColumnType("int(11)")
                .HasColumnName("revision");
        });

        modelBuilder.Entity<Icd10GemPcs109>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("icd10_gem_pcs_10_9");

            entity.HasIndex(e => e.MapId, "map_id").IsUnique();

            entity.Property(e => e.Active)
                .HasDefaultValueSql("'0'")
                .HasColumnType("tinyint(4)")
                .HasColumnName("active");
            entity.Property(e => e.Flags)
                .HasMaxLength(5)
                .HasColumnName("flags");
            entity.Property(e => e.MapId)
                .ValueGeneratedOnAdd()
                .HasColumnType("bigint(20) unsigned")
                .HasColumnName("map_id");
            entity.Property(e => e.PcsIcd10Source)
                .HasMaxLength(7)
                .HasColumnName("pcs_icd10_source");
            entity.Property(e => e.PcsIcd9Target)
                .HasMaxLength(5)
                .HasColumnName("pcs_icd9_target");
            entity.Property(e => e.Revision)
                .HasDefaultValueSql("'0'")
                .HasColumnType("int(11)")
                .HasColumnName("revision");
        });

        modelBuilder.Entity<Icd10GemPcs910>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("icd10_gem_pcs_9_10");

            entity.HasIndex(e => e.MapId, "map_id").IsUnique();

            entity.Property(e => e.Active)
                .HasDefaultValueSql("'0'")
                .HasColumnType("tinyint(4)")
                .HasColumnName("active");
            entity.Property(e => e.Flags)
                .HasMaxLength(5)
                .HasColumnName("flags");
            entity.Property(e => e.MapId)
                .ValueGeneratedOnAdd()
                .HasColumnType("bigint(20) unsigned")
                .HasColumnName("map_id");
            entity.Property(e => e.PcsIcd10Target)
                .HasMaxLength(7)
                .HasColumnName("pcs_icd10_target");
            entity.Property(e => e.PcsIcd9Source)
                .HasMaxLength(5)
                .HasColumnName("pcs_icd9_source");
            entity.Property(e => e.Revision)
                .HasDefaultValueSql("'0'")
                .HasColumnType("int(11)")
                .HasColumnName("revision");
        });

        modelBuilder.Entity<Icd10PcsOrderCode>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("icd10_pcs_order_code");

            entity.HasIndex(e => e.Active, "active");

            entity.HasIndex(e => e.PcsCode, "pcs_code");

            entity.HasIndex(e => e.PcsId, "pcs_id").IsUnique();

            entity.Property(e => e.Active)
                .HasDefaultValueSql("'0'")
                .HasColumnType("tinyint(4)")
                .HasColumnName("active");
            entity.Property(e => e.LongDesc)
                .HasColumnType("text")
                .HasColumnName("long_desc");
            entity.Property(e => e.PcsCode)
                .HasMaxLength(7)
                .HasColumnName("pcs_code");
            entity.Property(e => e.PcsId)
                .ValueGeneratedOnAdd()
                .HasColumnType("bigint(20) unsigned")
                .HasColumnName("pcs_id");
            entity.Property(e => e.Revision)
                .HasDefaultValueSql("'0'")
                .HasColumnType("int(11)")
                .HasColumnName("revision");
            entity.Property(e => e.ShortDesc)
                .HasMaxLength(60)
                .HasColumnName("short_desc");
            entity.Property(e => e.ValidForCoding)
                .HasMaxLength(1)
                .IsFixedLength()
                .HasColumnName("valid_for_coding");
        });

        modelBuilder.Entity<Icd10ReimbrDx910>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("icd10_reimbr_dx_9_10");

            entity.HasIndex(e => e.MapId, "map_id").IsUnique();

            entity.Property(e => e.Active)
                .HasDefaultValueSql("'0'")
                .HasColumnType("tinyint(4)")
                .HasColumnName("active");
            entity.Property(e => e.Code)
                .HasMaxLength(8)
                .HasColumnName("code");
            entity.Property(e => e.CodeCnt)
                .HasColumnType("tinyint(4)")
                .HasColumnName("code_cnt");
            entity.Property(e => e.Icd901)
                .HasMaxLength(5)
                .HasColumnName("ICD9_01");
            entity.Property(e => e.Icd902)
                .HasMaxLength(5)
                .HasColumnName("ICD9_02");
            entity.Property(e => e.Icd903)
                .HasMaxLength(5)
                .HasColumnName("ICD9_03");
            entity.Property(e => e.Icd904)
                .HasMaxLength(5)
                .HasColumnName("ICD9_04");
            entity.Property(e => e.Icd905)
                .HasMaxLength(5)
                .HasColumnName("ICD9_05");
            entity.Property(e => e.Icd906)
                .HasMaxLength(5)
                .HasColumnName("ICD9_06");
            entity.Property(e => e.MapId)
                .ValueGeneratedOnAdd()
                .HasColumnType("bigint(20) unsigned")
                .HasColumnName("map_id");
            entity.Property(e => e.Revision)
                .HasDefaultValueSql("'0'")
                .HasColumnType("int(11)")
                .HasColumnName("revision");
        });

        modelBuilder.Entity<Icd10ReimbrPcs910>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("icd10_reimbr_pcs_9_10");

            entity.HasIndex(e => e.MapId, "map_id").IsUnique();

            entity.Property(e => e.Active)
                .HasDefaultValueSql("'0'")
                .HasColumnType("tinyint(4)")
                .HasColumnName("active");
            entity.Property(e => e.Code)
                .HasMaxLength(8)
                .HasColumnName("code");
            entity.Property(e => e.CodeCnt)
                .HasColumnType("tinyint(4)")
                .HasColumnName("code_cnt");
            entity.Property(e => e.Icd901)
                .HasMaxLength(5)
                .HasColumnName("ICD9_01");
            entity.Property(e => e.Icd902)
                .HasMaxLength(5)
                .HasColumnName("ICD9_02");
            entity.Property(e => e.Icd903)
                .HasMaxLength(5)
                .HasColumnName("ICD9_03");
            entity.Property(e => e.Icd904)
                .HasMaxLength(5)
                .HasColumnName("ICD9_04");
            entity.Property(e => e.Icd905)
                .HasMaxLength(5)
                .HasColumnName("ICD9_05");
            entity.Property(e => e.Icd906)
                .HasMaxLength(5)
                .HasColumnName("ICD9_06");
            entity.Property(e => e.MapId)
                .ValueGeneratedOnAdd()
                .HasColumnType("bigint(20) unsigned")
                .HasColumnName("map_id");
            entity.Property(e => e.Revision)
                .HasDefaultValueSql("'0'")
                .HasColumnType("int(11)")
                .HasColumnName("revision");
        });

        modelBuilder.Entity<Icd9DxCode>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("icd9_dx_code");

            entity.HasIndex(e => e.Active, "active");

            entity.HasIndex(e => e.DxCode, "dx_code");

            entity.HasIndex(e => e.DxId, "dx_id").IsUnique();

            entity.HasIndex(e => e.FormattedDxCode, "formatted_dx_code");

            entity.Property(e => e.Active)
                .HasDefaultValueSql("'0'")
                .HasColumnType("tinyint(4)")
                .HasColumnName("active");
            entity.Property(e => e.DxCode)
                .HasMaxLength(5)
                .HasColumnName("dx_code");
            entity.Property(e => e.DxId)
                .ValueGeneratedOnAdd()
                .HasColumnType("bigint(20) unsigned")
                .HasColumnName("dx_id");
            entity.Property(e => e.FormattedDxCode)
                .HasMaxLength(6)
                .HasColumnName("formatted_dx_code");
            entity.Property(e => e.LongDesc)
                .HasMaxLength(300)
                .HasColumnName("long_desc");
            entity.Property(e => e.Revision)
                .HasDefaultValueSql("'0'")
                .HasColumnType("int(11)")
                .HasColumnName("revision");
            entity.Property(e => e.ShortDesc)
                .HasMaxLength(60)
                .HasColumnName("short_desc");
        });

        modelBuilder.Entity<Icd9DxLongCode>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("icd9_dx_long_code");

            entity.HasIndex(e => e.DxId, "dx_id").IsUnique();

            entity.Property(e => e.Active)
                .HasDefaultValueSql("'0'")
                .HasColumnType("tinyint(4)")
                .HasColumnName("active");
            entity.Property(e => e.DxCode)
                .HasMaxLength(5)
                .HasColumnName("dx_code");
            entity.Property(e => e.DxId)
                .ValueGeneratedOnAdd()
                .HasColumnType("bigint(20) unsigned")
                .HasColumnName("dx_id");
            entity.Property(e => e.LongDesc)
                .HasMaxLength(300)
                .HasColumnName("long_desc");
            entity.Property(e => e.Revision)
                .HasDefaultValueSql("'0'")
                .HasColumnType("int(11)")
                .HasColumnName("revision");
        });

        modelBuilder.Entity<Icd9SgCode>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("icd9_sg_code");

            entity.HasIndex(e => e.Active, "active");

            entity.HasIndex(e => e.FormattedSgCode, "formatted_sg_code");

            entity.HasIndex(e => e.SgCode, "sg_code");

            entity.HasIndex(e => e.SgId, "sg_id").IsUnique();

            entity.Property(e => e.Active)
                .HasDefaultValueSql("'0'")
                .HasColumnType("tinyint(4)")
                .HasColumnName("active");
            entity.Property(e => e.FormattedSgCode)
                .HasMaxLength(6)
                .HasColumnName("formatted_sg_code");
            entity.Property(e => e.LongDesc)
                .HasMaxLength(300)
                .HasColumnName("long_desc");
            entity.Property(e => e.Revision)
                .HasDefaultValueSql("'0'")
                .HasColumnType("int(11)")
                .HasColumnName("revision");
            entity.Property(e => e.SgCode)
                .HasMaxLength(5)
                .HasColumnName("sg_code");
            entity.Property(e => e.SgId)
                .ValueGeneratedOnAdd()
                .HasColumnType("bigint(20) unsigned")
                .HasColumnName("sg_id");
            entity.Property(e => e.ShortDesc)
                .HasMaxLength(60)
                .HasColumnName("short_desc");
        });

        modelBuilder.Entity<Icd9SgLongCode>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("icd9_sg_long_code");

            entity.HasIndex(e => e.SqId, "sq_id").IsUnique();

            entity.Property(e => e.Active)
                .HasDefaultValueSql("'0'")
                .HasColumnType("tinyint(4)")
                .HasColumnName("active");
            entity.Property(e => e.LongDesc)
                .HasMaxLength(300)
                .HasColumnName("long_desc");
            entity.Property(e => e.Revision)
                .HasDefaultValueSql("'0'")
                .HasColumnType("int(11)")
                .HasColumnName("revision");
            entity.Property(e => e.SgCode)
                .HasMaxLength(5)
                .HasColumnName("sg_code");
            entity.Property(e => e.SqId)
                .ValueGeneratedOnAdd()
                .HasColumnType("bigint(20) unsigned")
                .HasColumnName("sq_id");
        });

        modelBuilder.Entity<Immunization>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("immunizations");

            entity.HasIndex(e => e.PatientId, "patient_id");

            entity.HasIndex(e => e.Uuid, "uuid").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.AddedErroneously).HasColumnName("added_erroneously");
            entity.Property(e => e.AdministeredBy)
                .HasMaxLength(255)
                .HasComment("Alternative to administered_by_id")
                .HasColumnName("administered_by");
            entity.Property(e => e.AdministeredById)
                .HasColumnType("bigint(20)")
                .HasColumnName("administered_by_id");
            entity.Property(e => e.AdministeredDate)
                .HasColumnType("datetime")
                .HasColumnName("administered_date");
            entity.Property(e => e.AdministrationSite)
                .HasMaxLength(100)
                .HasColumnName("administration_site");
            entity.Property(e => e.AmountAdministered).HasColumnName("amount_administered");
            entity.Property(e => e.AmountAdministeredUnit)
                .HasMaxLength(50)
                .HasColumnName("amount_administered_unit");
            entity.Property(e => e.CompletionStatus)
                .HasMaxLength(50)
                .HasColumnName("completion_status");
            entity.Property(e => e.CreateDate)
                .HasColumnType("datetime")
                .HasColumnName("create_date");
            entity.Property(e => e.CreatedBy)
                .HasColumnType("bigint(20)")
                .HasColumnName("created_by");
            entity.Property(e => e.CvxCode)
                .HasMaxLength(64)
                .HasColumnName("cvx_code");
            entity.Property(e => e.EducationDate).HasColumnName("education_date");
            entity.Property(e => e.EncounterId)
                .HasComment("fk to form_encounter.encounter to link immunization to encounter record")
                .HasColumnType("bigint(20)")
                .HasColumnName("encounter_id");
            entity.Property(e => e.ExpirationDate).HasColumnName("expiration_date");
            entity.Property(e => e.ExternalId)
                .HasMaxLength(20)
                .HasColumnName("external_id");
            entity.Property(e => e.ImmunizationId)
                .HasColumnType("int(11)")
                .HasColumnName("immunization_id");
            entity.Property(e => e.InformationSource)
                .HasMaxLength(31)
                .HasColumnName("information_source");
            entity.Property(e => e.LotNumber)
                .HasMaxLength(50)
                .HasColumnName("lot_number");
            entity.Property(e => e.Manufacturer)
                .HasMaxLength(100)
                .HasColumnName("manufacturer");
            entity.Property(e => e.Note)
                .HasColumnType("text")
                .HasColumnName("note");
            entity.Property(e => e.OrderingProvider)
                .HasColumnType("int(11)")
                .HasColumnName("ordering_provider");
            entity.Property(e => e.PatientId)
                .HasColumnType("bigint(20)")
                .HasColumnName("patient_id");
            entity.Property(e => e.ReasonCode)
                .HasMaxLength(31)
                .HasComment("Medical code explaining reason of the vital observation value in form codesystem:codetype;...;")
                .HasColumnName("reason_code");
            entity.Property(e => e.ReasonDescription)
                .HasComment("Human readable text description of the reason_code column")
                .HasColumnType("text")
                .HasColumnName("reason_description");
            entity.Property(e => e.RefusalReason)
                .HasMaxLength(31)
                .HasColumnName("refusal_reason");
            entity.Property(e => e.Route)
                .HasMaxLength(100)
                .HasColumnName("route");
            entity.Property(e => e.UpdateDate)
                .HasColumnType("timestamp")
                .HasColumnName("update_date");
            entity.Property(e => e.UpdatedBy)
                .HasColumnType("bigint(20)")
                .HasColumnName("updated_by");
            entity.Property(e => e.Uuid)
                .HasMaxLength(16)
                .IsFixedLength()
                .HasColumnName("uuid");
            entity.Property(e => e.VisDate)
                .HasComment("Date of VIS Statement")
                .HasColumnName("vis_date");
        });

        modelBuilder.Entity<ImmunizationObservation>(entity =>
        {
            entity.HasKey(e => e.ImoId).HasName("PRIMARY");

            entity.ToTable("immunization_observation");

            entity.Property(e => e.ImoId)
                .HasColumnType("int(11)")
                .HasColumnName("imo_id");
            entity.Property(e => e.ImoCode)
                .HasMaxLength(255)
                .HasColumnName("imo_code");
            entity.Property(e => e.ImoCodetext)
                .HasMaxLength(255)
                .HasColumnName("imo_codetext");
            entity.Property(e => e.ImoCodetype)
                .HasMaxLength(255)
                .HasColumnName("imo_codetype");
            entity.Property(e => e.ImoCriteria)
                .HasMaxLength(255)
                .HasColumnName("imo_criteria");
            entity.Property(e => e.ImoCriteriaValue)
                .HasMaxLength(255)
                .HasColumnName("imo_criteria_value");
            entity.Property(e => e.ImoDateObservation)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("timestamp")
                .HasColumnName("imo_date_observation");
            entity.Property(e => e.ImoImId)
                .HasColumnType("int(11)")
                .HasColumnName("imo_im_id");
            entity.Property(e => e.ImoPid)
                .HasColumnType("int(11)")
                .HasColumnName("imo_pid");
            entity.Property(e => e.ImoUser)
                .HasColumnType("int(11)")
                .HasColumnName("imo_user");
            entity.Property(e => e.ImoVisDatePresented).HasColumnName("imo_vis_date_presented");
            entity.Property(e => e.ImoVisDatePublished).HasColumnName("imo_vis_date_published");
        });

        modelBuilder.Entity<InsuranceCompany>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("insurance_companies");

            entity.HasIndex(e => e.Uuid, "uuid").IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.AltCmsId)
                .HasMaxLength(15)
                .HasColumnName("alt_cms_id");
            entity.Property(e => e.Attn)
                .HasMaxLength(255)
                .HasColumnName("attn");
            entity.Property(e => e.CmsId)
                .HasMaxLength(15)
                .HasColumnName("cms_id");
            entity.Property(e => e.CqmSop)
                .HasComment("HL7 Source of Payment for eCQMs")
                .HasColumnType("int(11)")
                .HasColumnName("cqm_sop");
            entity.Property(e => e.DateCreated)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("date_created");
            entity.Property(e => e.EligibilityId)
                .HasMaxLength(32)
                .HasColumnName("eligibility_id");
            entity.Property(e => e.Inactive).HasColumnName("inactive");
            entity.Property(e => e.InsTypeCode)
                .HasColumnType("int(11)")
                .HasColumnName("ins_type_code");
            entity.Property(e => e.LastUpdated)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("last_updated");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.Uuid)
                .HasMaxLength(16)
                .IsFixedLength()
                .HasColumnName("uuid");
            entity.Property(e => e.X12DefaultEligibilityId)
                .HasColumnType("int(11)")
                .HasColumnName("x12_default_eligibility_id");
            entity.Property(e => e.X12DefaultPartnerId)
                .HasColumnType("int(11)")
                .HasColumnName("x12_default_partner_id");
            entity.Property(e => e.X12ReceiverId)
                .HasMaxLength(25)
                .HasColumnName("x12_receiver_id");
        });

        modelBuilder.Entity<InsuranceDatum>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("insurance_data");

            entity.HasIndex(e => new { e.Pid, e.Type, e.Date }, "pid_type_date").IsUnique();

            entity.HasIndex(e => e.Uuid, "uuid").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.AcceptAssignment)
                .HasMaxLength(5)
                .HasDefaultValueSql("'TRUE'")
                .HasColumnName("accept_assignment");
            entity.Property(e => e.Copay)
                .HasMaxLength(255)
                .HasColumnName("copay");
            entity.Property(e => e.Date).HasColumnName("date");
            entity.Property(e => e.DateEnd).HasColumnName("date_end");
            entity.Property(e => e.GroupNumber)
                .HasMaxLength(255)
                .HasColumnName("group_number");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.PlanName)
                .HasMaxLength(255)
                .HasColumnName("plan_name");
            entity.Property(e => e.PolicyNumber)
                .HasMaxLength(255)
                .HasColumnName("policy_number");
            entity.Property(e => e.PolicyType)
                .HasMaxLength(25)
                .HasDefaultValueSql("''")
                .HasColumnName("policy_type");
            entity.Property(e => e.Provider)
                .HasMaxLength(255)
                .HasColumnName("provider");
            entity.Property(e => e.SubscriberCity)
                .HasMaxLength(255)
                .HasColumnName("subscriber_city");
            entity.Property(e => e.SubscriberCountry)
                .HasMaxLength(255)
                .HasColumnName("subscriber_country");
            entity.Property(e => e.SubscriberDob).HasColumnName("subscriber_DOB");
            entity.Property(e => e.SubscriberEmployer)
                .HasMaxLength(255)
                .HasColumnName("subscriber_employer");
            entity.Property(e => e.SubscriberEmployerCity)
                .HasMaxLength(255)
                .HasColumnName("subscriber_employer_city");
            entity.Property(e => e.SubscriberEmployerCountry)
                .HasMaxLength(255)
                .HasColumnName("subscriber_employer_country");
            entity.Property(e => e.SubscriberEmployerPostalCode)
                .HasMaxLength(255)
                .HasColumnName("subscriber_employer_postal_code");
            entity.Property(e => e.SubscriberEmployerState)
                .HasMaxLength(255)
                .HasColumnName("subscriber_employer_state");
            entity.Property(e => e.SubscriberEmployerStreet)
                .HasMaxLength(255)
                .HasColumnName("subscriber_employer_street");
            entity.Property(e => e.SubscriberEmployerStreetLine2)
                .HasColumnType("tinytext")
                .HasColumnName("subscriber_employer_street_line_2");
            entity.Property(e => e.SubscriberFname)
                .HasMaxLength(255)
                .HasColumnName("subscriber_fname");
            entity.Property(e => e.SubscriberLname)
                .HasMaxLength(255)
                .HasColumnName("subscriber_lname");
            entity.Property(e => e.SubscriberMname)
                .HasMaxLength(255)
                .HasColumnName("subscriber_mname");
            entity.Property(e => e.SubscriberPhone)
                .HasMaxLength(255)
                .HasColumnName("subscriber_phone");
            entity.Property(e => e.SubscriberPostalCode)
                .HasMaxLength(255)
                .HasColumnName("subscriber_postal_code");
            entity.Property(e => e.SubscriberRelationship)
                .HasMaxLength(255)
                .HasColumnName("subscriber_relationship");
            entity.Property(e => e.SubscriberSex)
                .HasMaxLength(25)
                .HasColumnName("subscriber_sex");
            entity.Property(e => e.SubscriberSs)
                .HasMaxLength(255)
                .HasColumnName("subscriber_ss");
            entity.Property(e => e.SubscriberState)
                .HasMaxLength(255)
                .HasColumnName("subscriber_state");
            entity.Property(e => e.SubscriberStreet)
                .HasMaxLength(255)
                .HasColumnName("subscriber_street");
            entity.Property(e => e.SubscriberStreetLine2)
                .HasColumnType("tinytext")
                .HasColumnName("subscriber_street_line_2");
            entity.Property(e => e.Type)
                .HasColumnType("enum('primary','secondary','tertiary')")
                .HasColumnName("type");
            entity.Property(e => e.Uuid)
                .HasMaxLength(16)
                .IsFixedLength()
                .HasColumnName("uuid");
        });

        modelBuilder.Entity<InsuranceNumber>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("insurance_numbers");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.GroupNumber)
                .HasMaxLength(20)
                .HasColumnName("group_number");
            entity.Property(e => e.InsuranceCompanyId)
                .HasColumnType("int(11)")
                .HasColumnName("insurance_company_id");
            entity.Property(e => e.ProviderId)
                .HasColumnType("int(11)")
                .HasColumnName("provider_id");
            entity.Property(e => e.ProviderNumber)
                .HasMaxLength(20)
                .HasColumnName("provider_number");
            entity.Property(e => e.ProviderNumberType)
                .HasMaxLength(4)
                .HasColumnName("provider_number_type");
            entity.Property(e => e.RenderingProviderNumber)
                .HasMaxLength(20)
                .HasColumnName("rendering_provider_number");
            entity.Property(e => e.RenderingProviderNumberType)
                .HasMaxLength(4)
                .HasColumnName("rendering_provider_number_type");
        });

        modelBuilder.Entity<InsuranceTypeCode>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("insurance_type_codes");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnType("int(2)")
                .HasColumnName("id");
            entity.Property(e => e.ClaimType)
                .HasColumnType("text")
                .HasColumnName("claim_type");
            entity.Property(e => e.Type)
                .HasMaxLength(60)
                .HasColumnName("type");
        });

        modelBuilder.Entity<IpTracking>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("ip_tracking");

            entity.HasIndex(e => e.IpString, "ip_string").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.IpAutoBlockEmailed)
                .HasDefaultValueSql("'0'")
                .HasColumnType("tinyint(4)")
                .HasColumnName("ip_auto_block_emailed");
            entity.Property(e => e.IpForceBlock)
                .HasDefaultValueSql("'0'")
                .HasColumnType("tinyint(4)")
                .HasColumnName("ip_force_block");
            entity.Property(e => e.IpLastLoginFail)
                .HasColumnType("datetime")
                .HasColumnName("ip_last_login_fail");
            entity.Property(e => e.IpLoginFailCounter)
                .HasDefaultValueSql("'0'")
                .HasColumnType("bigint(20)")
                .HasColumnName("ip_login_fail_counter");
            entity.Property(e => e.IpNoPreventTimingAttack)
                .HasDefaultValueSql("'0'")
                .HasColumnType("tinyint(4)")
                .HasColumnName("ip_no_prevent_timing_attack");
            entity.Property(e => e.IpString)
                .HasDefaultValueSql("''")
                .HasColumnName("ip_string");
            entity.Property(e => e.MfaLastLoginFail)
                .HasComment("Timestamp of the last MFA challenge failure from this IP. Used for time-based counter reset.")
                .HasColumnType("datetime")
                .HasColumnName("mfa_last_login_fail");
            entity.Property(e => e.MfaLoginFailCounter)
                .HasDefaultValueSql("'0'")
                .HasComment("Per-IP MFA challenge failure counter. Independent of ip_login_fail_counter so an in-progress MFA brute force is not zeroed out by the password verify success on each attempt.")
                .HasColumnType("bigint(20)")
                .HasColumnName("mfa_login_fail_counter");
            entity.Property(e => e.TotalIpLoginFailCounter)
                .HasDefaultValueSql("'0'")
                .HasColumnType("bigint(20)")
                .HasColumnName("total_ip_login_fail_counter");
        });

        modelBuilder.Entity<IssueEncounter>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("issue_encounter");

            entity.HasIndex(e => new { e.Pid, e.ListId, e.Encounter }, "uniq_issue_key").IsUnique();

            entity.HasIndex(e => e.Uuid, "uuid_unique").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("current_timestamp()")
                .HasComment("timestamp when this issue encounter record was created")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasComment("fk to users.id for the user that entered in the issue encounter data")
                .HasColumnType("bigint(20)")
                .HasColumnName("created_by");
            entity.Property(e => e.Encounter)
                .HasColumnType("int(11)")
                .HasColumnName("encounter");
            entity.Property(e => e.ListId)
                .HasColumnType("int(11)")
                .HasColumnName("list_id");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.Resolved).HasColumnName("resolved");
            entity.Property(e => e.UpdatedAt)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("current_timestamp()")
                .HasComment("timestamp when this issue encounter record was last updated")
                .HasColumnType("datetime")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasComment("fk to users.id for the user that last updated the issue encounter data")
                .HasColumnType("bigint(20)")
                .HasColumnName("updated_by");
            entity.Property(e => e.Uuid)
                .HasMaxLength(16)
                .IsFixedLength()
                .HasComment("UUID for this issue encounter record, for data exchange purposes")
                .HasColumnName("uuid");
        });

        modelBuilder.Entity<IssueType>(entity =>
        {
            entity.HasKey(e => new { e.Category, e.Type })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("issue_types");

            entity.Property(e => e.Category)
                .HasMaxLength(75)
                .HasDefaultValueSql("''")
                .HasColumnName("category");
            entity.Property(e => e.Type)
                .HasMaxLength(75)
                .HasDefaultValueSql("''")
                .HasColumnName("type");
            entity.Property(e => e.Abbreviation)
                .HasMaxLength(75)
                .HasDefaultValueSql("''")
                .HasColumnName("abbreviation");
            entity.Property(e => e.AcoSpec)
                .HasMaxLength(63)
                .HasDefaultValueSql("'patients|med'")
                .HasColumnName("aco_spec");
            entity.Property(e => e.Active)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("active");
            entity.Property(e => e.ForceShow)
                .HasColumnType("smallint(6)")
                .HasColumnName("force_show");
            entity.Property(e => e.Ordering)
                .HasColumnType("int(11)")
                .HasColumnName("ordering");
            entity.Property(e => e.Plural)
                .HasMaxLength(75)
                .HasDefaultValueSql("''")
                .HasColumnName("plural");
            entity.Property(e => e.Singular)
                .HasMaxLength(75)
                .HasDefaultValueSql("''")
                .HasColumnName("singular");
            entity.Property(e => e.Style)
                .HasColumnType("smallint(6)")
                .HasColumnName("style");
        });

        modelBuilder.Entity<JwtGrantHistory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("jwt_grant_history", tb => tb.HasComment("Holds JWT authorization grant ids to prevent replay attacks"));

            entity.HasIndex(e => e.Jti, "jti");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.ClientId)
                .HasMaxLength(80)
                .HasComment("FK oauth2_clients.client_id")
                .HasColumnName("client_id");
            entity.Property(e => e.CreationDate)
                .HasDefaultValueSql("current_timestamp()")
                .HasComment("datetime the grant authorization was requested")
                .HasColumnType("datetime")
                .HasColumnName("creation_date");
            entity.Property(e => e.Jti)
                .HasMaxLength(100)
                .HasComment("Unique JWT id")
                .HasColumnName("jti");
            entity.Property(e => e.JtiExp)
                .HasComment("jwt exp claim when the jwt expires")
                .HasColumnType("timestamp")
                .HasColumnName("jti_exp");
        });

        modelBuilder.Entity<Key>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("keys");

            entity.HasIndex(e => e.Name, "name").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Name)
                .HasMaxLength(20)
                .HasDefaultValueSql("''")
                .HasColumnName("name");
            entity.Property(e => e.Value)
                .HasColumnType("text")
                .HasColumnName("value");
        });

        modelBuilder.Entity<LangConstant>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("lang_constants");

            entity.HasIndex(e => e.ConsId, "cons_id").IsUnique();

            entity.HasIndex(e => e.ConstantName, "constant_name").HasAnnotation("MySql:IndexPrefixLength", new[] { 100 });

            entity.Property(e => e.ConsId)
                .ValueGeneratedOnAdd()
                .HasColumnType("int(11)")
                .HasColumnName("cons_id");
            entity.Property(e => e.ConstantName)
                .HasColumnType("mediumtext")
                .HasColumnName("constant_name")
                .UseCollation("utf8mb4_bin");
        });

        modelBuilder.Entity<LangCustom>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("lang_custom");

            entity.Property(e => e.ConstantName)
                .HasColumnType("mediumtext")
                .HasColumnName("constant_name");
            entity.Property(e => e.Definition)
                .HasColumnType("mediumtext")
                .HasColumnName("definition");
            entity.Property(e => e.LangCode)
                .HasMaxLength(2)
                .HasDefaultValueSql("''")
                .IsFixedLength()
                .HasColumnName("lang_code");
            entity.Property(e => e.LangDescription)
                .HasMaxLength(100)
                .HasDefaultValueSql("''")
                .HasColumnName("lang_description");
        });

        modelBuilder.Entity<LangDefinition>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("lang_definitions");

            entity.HasIndex(e => e.ConsId, "cons_id");

            entity.HasIndex(e => e.DefId, "def_id").IsUnique();

            entity.HasIndex(e => new { e.LangId, e.ConsId }, "lang_cons");

            entity.Property(e => e.ConsId)
                .HasColumnType("int(11)")
                .HasColumnName("cons_id");
            entity.Property(e => e.DefId)
                .ValueGeneratedOnAdd()
                .HasColumnType("int(11)")
                .HasColumnName("def_id");
            entity.Property(e => e.Definition)
                .HasColumnType("mediumtext")
                .HasColumnName("definition");
            entity.Property(e => e.LangId)
                .HasColumnType("int(11)")
                .HasColumnName("lang_id");
        });

        modelBuilder.Entity<LangLanguage>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("lang_languages");

            entity.HasIndex(e => e.LangId, "lang_id").IsUnique();

            entity.Property(e => e.LangCode)
                .HasMaxLength(2)
                .HasDefaultValueSql("''")
                .IsFixedLength()
                .HasColumnName("lang_code");
            entity.Property(e => e.LangDescription)
                .HasMaxLength(100)
                .HasColumnName("lang_description");
            entity.Property(e => e.LangId)
                .ValueGeneratedOnAdd()
                .HasColumnType("int(11)")
                .HasColumnName("lang_id");
            entity.Property(e => e.LangIsRtl)
                .HasDefaultValueSql("'0'")
                .HasColumnType("tinyint(4)")
                .HasColumnName("lang_is_rtl");
        });

        modelBuilder.Entity<LayoutGroupProperty>(entity =>
        {
            entity.HasKey(e => new { e.GrpFormId, e.GrpGroupId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("layout_group_properties");

            entity.Property(e => e.GrpFormId)
                .HasMaxLength(31)
                .HasColumnName("grp_form_id");
            entity.Property(e => e.GrpGroupId)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("empty when representing the whole form")
                .HasColumnName("grp_group_id");
            entity.Property(e => e.GrpAcoSpec)
                .HasMaxLength(63)
                .HasDefaultValueSql("''")
                .HasColumnName("grp_aco_spec");
            entity.Property(e => e.GrpActivity)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("grp_activity");
            entity.Property(e => e.GrpColumns)
                .HasColumnType("int(11)")
                .HasColumnName("grp_columns");
            entity.Property(e => e.GrpDiags)
                .HasMaxLength(4095)
                .HasDefaultValueSql("''")
                .HasColumnName("grp_diags");
            entity.Property(e => e.GrpInitOpen).HasColumnName("grp_init_open");
            entity.Property(e => e.GrpIssueType)
                .HasMaxLength(75)
                .HasDefaultValueSql("''")
                .HasColumnName("grp_issue_type");
            entity.Property(e => e.GrpLastUpdate)
                .HasColumnType("timestamp")
                .HasColumnName("grp_last_update");
            entity.Property(e => e.GrpMapping)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("the form category")
                .HasColumnName("grp_mapping");
            entity.Property(e => e.GrpProducts)
                .HasMaxLength(4095)
                .HasDefaultValueSql("''")
                .HasColumnName("grp_products");
            entity.Property(e => e.GrpReferrals).HasColumnName("grp_referrals");
            entity.Property(e => e.GrpRepeats)
                .HasColumnType("int(11)")
                .HasColumnName("grp_repeats");
            entity.Property(e => e.GrpSaveClose).HasColumnName("grp_save_close");
            entity.Property(e => e.GrpSeq)
                .HasComment("optional order within mapping")
                .HasColumnType("int(11)")
                .HasColumnName("grp_seq");
            entity.Property(e => e.GrpServices)
                .HasMaxLength(4095)
                .HasDefaultValueSql("''")
                .HasColumnName("grp_services");
            entity.Property(e => e.GrpSize)
                .HasColumnType("int(11)")
                .HasColumnName("grp_size");
            entity.Property(e => e.GrpSubtitle)
                .HasMaxLength(63)
                .HasDefaultValueSql("''")
                .HasComment("for display under the title")
                .HasColumnName("grp_subtitle");
            entity.Property(e => e.GrpTitle)
                .HasMaxLength(63)
                .HasDefaultValueSql("''")
                .HasComment("descriptive name of the form or group")
                .HasColumnName("grp_title");
            entity.Property(e => e.GrpUnchecked).HasColumnName("grp_unchecked");
        });

        modelBuilder.Entity<LayoutOption>(entity =>
        {
            entity.HasKey(e => new { e.FormId, e.FieldId, e.Seq })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0, 0 });

            entity.ToTable("layout_options");

            entity.Property(e => e.FormId)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasColumnName("form_id");
            entity.Property(e => e.FieldId)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasColumnName("field_id");
            entity.Property(e => e.Seq)
                .HasColumnType("int(11)")
                .HasColumnName("seq");
            entity.Property(e => e.Codes)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("codes");
            entity.Property(e => e.Conditions)
                .HasComment("serialized array of skip conditions")
                .HasColumnType("text")
                .HasColumnName("conditions");
            entity.Property(e => e.DataType)
                .HasColumnType("tinyint(3)")
                .HasColumnName("data_type");
            entity.Property(e => e.Datacols)
                .HasDefaultValueSql("'1'")
                .HasColumnType("tinyint(3)")
                .HasColumnName("datacols");
            entity.Property(e => e.DefaultValue)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("default_value");
            entity.Property(e => e.Description)
                .HasColumnType("text")
                .HasColumnName("description");
            entity.Property(e => e.EditOptions)
                .HasMaxLength(36)
                .HasDefaultValueSql("''")
                .HasColumnName("edit_options");
            entity.Property(e => e.FldLength)
                .HasDefaultValueSql("'15'")
                .HasColumnType("int(11)")
                .HasColumnName("fld_length");
            entity.Property(e => e.FldRows)
                .HasColumnType("int(11)")
                .HasColumnName("fld_rows");
            entity.Property(e => e.GroupId)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasColumnName("group_id");
            entity.Property(e => e.ListBackupId)
                .HasMaxLength(100)
                .HasDefaultValueSql("''")
                .HasColumnName("list_backup_id");
            entity.Property(e => e.ListId)
                .HasMaxLength(100)
                .HasDefaultValueSql("''")
                .HasColumnName("list_id");
            entity.Property(e => e.MaxLength)
                .HasColumnType("int(11)")
                .HasColumnName("max_length");
            entity.Property(e => e.Source)
                .HasMaxLength(1)
                .HasDefaultValueSql("'F'")
                .IsFixedLength()
                .HasComment("F=Form, D=Demographics, H=History, E=Encounter")
                .HasColumnName("source");
            entity.Property(e => e.Title)
                .HasColumnType("text")
                .HasColumnName("title");
            entity.Property(e => e.Titlecols)
                .HasDefaultValueSql("'1'")
                .HasColumnType("tinyint(3)")
                .HasColumnName("titlecols");
            entity.Property(e => e.Uor)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("uor");
            entity.Property(e => e.Validation)
                .HasMaxLength(100)
                .HasColumnName("validation");
        });

        modelBuilder.Entity<LbfDatum>(entity =>
        {
            entity.HasKey(e => new { e.FormId, e.FieldId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("lbf_data", tb => tb.HasComment("contains all data from layout-based forms"));

            entity.Property(e => e.FormId)
                .ValueGeneratedOnAdd()
                .HasComment("references forms.form_id")
                .HasColumnType("int(11)")
                .HasColumnName("form_id");
            entity.Property(e => e.FieldId)
                .HasMaxLength(31)
                .HasComment("references layout_options.field_id")
                .HasColumnName("field_id");
            entity.Property(e => e.FieldValue).HasColumnName("field_value");
        });

        modelBuilder.Entity<LbtDatum>(entity =>
        {
            entity.HasKey(e => new { e.FormId, e.FieldId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("lbt_data", tb => tb.HasComment("contains all data from layout-based transactions"));

            entity.Property(e => e.FormId)
                .HasComment("references transactions.id")
                .HasColumnType("bigint(20)")
                .HasColumnName("form_id");
            entity.Property(e => e.FieldId)
                .HasMaxLength(31)
                .HasComment("references layout_options.field_id")
                .HasColumnName("field_id");
            entity.Property(e => e.FieldValue)
                .HasColumnType("text")
                .HasColumnName("field_value");
        });

        modelBuilder.Entity<List>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("lists");

            entity.HasIndex(e => e.Pid, "pid");

            entity.HasIndex(e => e.Type, "type");

            entity.HasIndex(e => e.Uuid, "uuid").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Activity)
                .HasColumnType("tinyint(4)")
                .HasColumnName("activity");
            entity.Property(e => e.Begdate)
                .HasColumnType("datetime")
                .HasColumnName("begdate");
            entity.Property(e => e.Classification)
                .HasDefaultValueSql("'0'")
                .HasColumnType("int(11)")
                .HasColumnName("classification");
            entity.Property(e => e.Comments).HasColumnName("comments");
            entity.Property(e => e.Date)
                .HasColumnType("datetime")
                .HasColumnName("date");
            entity.Property(e => e.Destination)
                .HasMaxLength(255)
                .HasColumnName("destination");
            entity.Property(e => e.Diagnosis)
                .HasMaxLength(255)
                .HasColumnName("diagnosis");
            entity.Property(e => e.Enddate)
                .HasColumnType("datetime")
                .HasColumnName("enddate");
            entity.Property(e => e.ErxSource)
                .HasDefaultValueSql("'0'")
                .HasComment("0-OpenEMR 1-External")
                .HasColumnType("enum('0','1')")
                .HasColumnName("erx_source");
            entity.Property(e => e.ErxUploaded)
                .HasDefaultValueSql("'0'")
                .HasComment("0-Pending NewCrop upload 1-Uploaded TO NewCrop")
                .HasColumnType("enum('0','1')")
                .HasColumnName("erx_uploaded");
            entity.Property(e => e.ExternalAllergyid)
                .HasColumnType("int(11)")
                .HasColumnName("external_allergyid");
            entity.Property(e => e.ExternalId)
                .HasMaxLength(20)
                .HasColumnName("external_id");
            entity.Property(e => e.Extrainfo)
                .HasMaxLength(255)
                .HasColumnName("extrainfo");
            entity.Property(e => e.Groupname)
                .HasMaxLength(255)
                .HasColumnName("groupname");
            entity.Property(e => e.InjuryGrade)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasColumnName("injury_grade");
            entity.Property(e => e.InjuryPart)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasColumnName("injury_part");
            entity.Property(e => e.InjuryType)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasColumnName("injury_type");
            entity.Property(e => e.ListOptionId)
                .HasMaxLength(100)
                .HasComment("Reference to list_options table")
                .HasColumnName("list_option_id");
            entity.Property(e => e.Modifydate)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("timestamp")
                .HasColumnName("modifydate");
            entity.Property(e => e.Occurrence)
                .HasDefaultValueSql("'0'")
                .HasComment("Reference to list_options option_id='occurrence'")
                .HasColumnType("int(11)")
                .HasColumnName("occurrence");
            entity.Property(e => e.Outcome)
                .HasColumnType("int(11)")
                .HasColumnName("outcome");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.Reaction)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("reaction");
            entity.Property(e => e.Referredby)
                .HasMaxLength(255)
                .HasColumnName("referredby");
            entity.Property(e => e.ReinjuryId)
                .HasColumnType("bigint(20)")
                .HasColumnName("reinjury_id");
            entity.Property(e => e.Returndate).HasColumnName("returndate");
            entity.Property(e => e.SeverityAl)
                .HasMaxLength(50)
                .HasColumnName("severity_al");
            entity.Property(e => e.Subtype)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasColumnName("subtype");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasColumnName("title");
            entity.Property(e => e.Type).HasColumnName("type");
            entity.Property(e => e.Udi)
                .HasMaxLength(255)
                .HasColumnName("udi");
            entity.Property(e => e.UdiData)
                .HasColumnType("text")
                .HasColumnName("udi_data");
            entity.Property(e => e.User)
                .HasMaxLength(255)
                .HasColumnName("user");
            entity.Property(e => e.Uuid)
                .HasMaxLength(16)
                .IsFixedLength()
                .HasColumnName("uuid");
            entity.Property(e => e.Verification)
                .HasMaxLength(36)
                .HasDefaultValueSql("''")
                .HasComment("Reference to list_options option_id = allergyintolerance-verification")
                .HasColumnName("verification");
        });

        modelBuilder.Entity<ListOption>(entity =>
        {
            entity.HasKey(e => new { e.ListId, e.OptionId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("list_options");

            entity.Property(e => e.ListId)
                .HasMaxLength(100)
                .HasDefaultValueSql("''")
                .HasColumnName("list_id");
            entity.Property(e => e.OptionId)
                .HasMaxLength(100)
                .HasDefaultValueSql("''")
                .HasColumnName("option_id");
            entity.Property(e => e.Activity)
                .HasDefaultValueSql("'1'")
                .HasColumnType("tinyint(4)")
                .HasColumnName("activity");
            entity.Property(e => e.Codes)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("codes");
            entity.Property(e => e.EditOptions)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("edit_options");
            entity.Property(e => e.IsDefault).HasColumnName("is_default");
            entity.Property(e => e.LastUpdated)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("last_updated");
            entity.Property(e => e.Mapping)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasColumnName("mapping");
            entity.Property(e => e.Notes)
                .HasColumnType("text")
                .HasColumnName("notes");
            entity.Property(e => e.OptionValue).HasColumnName("option_value");
            entity.Property(e => e.Seq)
                .HasColumnType("int(11)")
                .HasColumnName("seq");
            entity.Property(e => e.Subtype)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasColumnName("subtype");
            entity.Property(e => e.Timestamp)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("timestamp")
                .HasColumnName("timestamp");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("title");
            entity.Property(e => e.ToggleSetting1).HasColumnName("toggle_setting_1");
            entity.Property(e => e.ToggleSetting2).HasColumnName("toggle_setting_2");
        });

        modelBuilder.Entity<ListsMedication>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("lists_medication", tb => tb.HasComment("Holds additional data about patient medications."));

            entity.HasIndex(e => e.RequestIntent, "lists_med_request_intent_idx");

            entity.HasIndex(e => e.UsageCategory, "lists_med_usage_category_idx");

            entity.HasIndex(e => e.ListId, "lists_medication_list_idx");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.DrugDosageInstructions)
                .HasComment("Free text dosage instructions for taking the drug")
                .HasColumnName("drug_dosage_instructions");
            entity.Property(e => e.IsPrimaryRecord)
                .HasDefaultValueSql("'1'")
                .HasComment("Indicates if this medication is a primary record(1) or a reported record(0)")
                .HasColumnName("is_primary_record");
            entity.Property(e => e.ListId)
                .HasComment("FK Reference to lists.id")
                .HasColumnType("bigint(20)")
                .HasColumnName("list_id");
            entity.Property(e => e.MedicationAdherence)
                .HasMaxLength(50)
                .HasComment("fk to list_options.option_id where list_id=medication_adherence to indicate if patient is complying with medication regimen")
                .HasColumnName("medication_adherence");
            entity.Property(e => e.MedicationAdherenceDateAsserted)
                .HasComment("Date when the medication adherence information was asserted")
                .HasColumnType("datetime")
                .HasColumnName("medication_adherence_date_asserted");
            entity.Property(e => e.MedicationAdherenceInformationSource)
                .HasMaxLength(50)
                .HasComment("fk to list_options.option_id where list_id=medication_adherence_information_source to indicate who provided the medication adherence information")
                .HasColumnName("medication_adherence_information_source");
            entity.Property(e => e.PrescriptionId)
                .HasComment("fk to prescriptions.prescription_id to link medication to prescription record")
                .HasColumnType("bigint(20)")
                .HasColumnName("prescription_id");
            entity.Property(e => e.ReportingSourceRecordId)
                .HasComment("If this is a reported record, this is the fk to the users.id column for the address book user that the medication was reported by")
                .HasColumnType("bigint(20)")
                .HasColumnName("reporting_source_record_id");
            entity.Property(e => e.RequestIntent)
                .HasMaxLength(100)
                .HasComment("option_id in list_options.list_id=medication-request-intent")
                .HasColumnName("request_intent");
            entity.Property(e => e.RequestIntentTitle)
                .HasMaxLength(255)
                .HasComment("title in list_options.list_id=medication-request-intent")
                .HasColumnName("request_intent_title");
            entity.Property(e => e.UsageCategory)
                .HasMaxLength(100)
                .HasComment("option_id in list_options.list_id=medication-usage-category")
                .HasColumnName("usage_category");
            entity.Property(e => e.UsageCategoryTitle)
                .HasMaxLength(255)
                .HasComment("title in list_options.list_id=medication-usage-category")
                .HasColumnName("usage_category_title");
        });

        modelBuilder.Entity<ListsTouch>(entity =>
        {
            entity.HasKey(e => new { e.Pid, e.Type })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("lists_touch");

            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.Type)
                .HasDefaultValueSql("''")
                .HasColumnName("type");
            entity.Property(e => e.Date)
                .HasColumnType("datetime")
                .HasColumnName("date");
        });

        modelBuilder.Entity<Log>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("log");

            entity.HasIndex(e => e.PatientId, "patient_id");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Category)
                .HasMaxLength(255)
                .HasColumnName("category");
            entity.Property(e => e.CcdaDocId)
                .HasComment("CCDA document id from ccda")
                .HasColumnType("int(11)")
                .HasColumnName("ccda_doc_id");
            entity.Property(e => e.Checksum).HasColumnName("checksum");
            entity.Property(e => e.Comments).HasColumnName("comments");
            entity.Property(e => e.CrtUser)
                .HasMaxLength(255)
                .HasColumnName("crt_user");
            entity.Property(e => e.Date)
                .HasColumnType("datetime")
                .HasColumnName("date");
            entity.Property(e => e.Event)
                .HasMaxLength(255)
                .HasColumnName("event");
            entity.Property(e => e.Groupname)
                .HasMaxLength(255)
                .HasColumnName("groupname");
            entity.Property(e => e.LogFrom)
                .HasMaxLength(20)
                .HasDefaultValueSql("'open-emr'")
                .HasColumnName("log_from");
            entity.Property(e => e.MenuItemId)
                .HasColumnType("int(11)")
                .HasColumnName("menu_item_id");
            entity.Property(e => e.PatientId)
                .HasColumnType("bigint(20)")
                .HasColumnName("patient_id");
            entity.Property(e => e.Success)
                .HasDefaultValueSql("'1'")
                .HasColumnName("success");
            entity.Property(e => e.User)
                .HasMaxLength(255)
                .HasColumnName("user");
            entity.Property(e => e.UserNotes).HasColumnName("user_notes");
        });

        modelBuilder.Entity<LogCommentEncrypt>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("log_comment_encrypt");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.Checksum).HasColumnName("checksum");
            entity.Property(e => e.ChecksumApi).HasColumnName("checksum_api");
            entity.Property(e => e.Encrypt)
                .HasDefaultValueSql("'No'")
                .HasColumnType("enum('Yes','No')")
                .HasColumnName("encrypt");
            entity.Property(e => e.LogId)
                .HasColumnType("int(11)")
                .HasColumnName("log_id");
            entity.Property(e => e.Version)
                .HasComment("0 for mycrypt and 1 for openssl")
                .HasColumnType("tinyint(4)")
                .HasColumnName("version");
        });

        modelBuilder.Entity<LoginMfaRegistration>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.Name })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("login_mfa_registrations");

            entity.Property(e => e.UserId)
                .HasColumnType("bigint(20)")
                .HasColumnName("user_id");
            entity.Property(e => e.Name)
                .HasMaxLength(30)
                .HasColumnName("name");
            entity.Property(e => e.LastChallenge)
                .HasComment("Timestamp of the last successful TOTP verification.")
                .HasColumnType("datetime")
                .HasColumnName("last_challenge");
            entity.Property(e => e.LastUsedStep)
                .HasComment("TOTP time slice (RFC 6238) of the last consumed code. Incoming codes must land on a strictly greater slice; guards against A-B-A replay across two adjacent valid codes within the 90s acceptance window.")
                .HasColumnType("bigint(20)")
                .HasColumnName("last_used_step");
            entity.Property(e => e.Method)
                .HasMaxLength(31)
                .HasComment("Q&A, U2F, TOTP etc.")
                .HasColumnName("method");
            entity.Property(e => e.Var1)
                .HasMaxLength(4096)
                .HasDefaultValueSql("''")
                .HasComment("Question, U2F registration etc.")
                .HasColumnName("var1");
            entity.Property(e => e.Var2)
                .HasMaxLength(256)
                .HasDefaultValueSql("''")
                .HasComment("Answer etc.")
                .HasColumnName("var2");
        });

        modelBuilder.Entity<MedexIcon>(entity =>
        {
            entity.HasKey(e => e.IUid).HasName("PRIMARY");

            entity.ToTable("medex_icons");

            entity.Property(e => e.IUid)
                .HasColumnType("int(11)")
                .HasColumnName("i_UID");
            entity.Property(e => e.IBlob).HasColumnName("i_blob");
            entity.Property(e => e.IDescription)
                .HasMaxLength(255)
                .HasColumnName("i_description");
            entity.Property(e => e.IHtml)
                .HasColumnType("text")
                .HasColumnName("i_html");
            entity.Property(e => e.MsgStatus)
                .HasMaxLength(10)
                .HasColumnName("msg_status");
            entity.Property(e => e.MsgType)
                .HasMaxLength(50)
                .HasColumnName("msg_type");
        });

        modelBuilder.Entity<MedexOutgoing>(entity =>
        {
            entity.HasKey(e => e.MsgUid).HasName("PRIMARY");

            entity.ToTable("medex_outgoing");

            entity.HasIndex(e => new { e.MsgUid, e.MsgPcEid, e.MedexUid }, "msg_eid").IsUnique();

            entity.Property(e => e.MsgUid)
                .HasColumnType("int(11)")
                .HasColumnName("msg_uid");
            entity.Property(e => e.CampaignUid)
                .HasColumnType("int(11)")
                .HasColumnName("campaign_uid");
            entity.Property(e => e.MedexUid)
                .HasColumnType("int(11)")
                .HasColumnName("medex_uid");
            entity.Property(e => e.MsgDate)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("timestamp")
                .HasColumnName("msg_date");
            entity.Property(e => e.MsgExtraText)
                .HasColumnType("text")
                .HasColumnName("msg_extra_text");
            entity.Property(e => e.MsgPcEid)
                .HasMaxLength(11)
                .HasColumnName("msg_pc_eid");
            entity.Property(e => e.MsgPid)
                .HasColumnType("int(11)")
                .HasColumnName("msg_pid");
            entity.Property(e => e.MsgReply)
                .HasMaxLength(50)
                .HasColumnName("msg_reply");
            entity.Property(e => e.MsgType)
                .HasMaxLength(50)
                .HasColumnName("msg_type");
        });

        modelBuilder.Entity<MedexPref>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("medex_prefs");

            entity.HasIndex(e => e.MeUsername, "ME_username").IsUnique();

            entity.Property(e => e.CombineTime)
                .HasColumnType("tinyint(4)")
                .HasColumnName("combine_time");
            entity.Property(e => e.LabelsChoice)
                .HasMaxLength(50)
                .HasColumnName("LABELS_choice");
            entity.Property(e => e.LabelsLocal)
                .HasMaxLength(3)
                .HasColumnName("LABELS_local");
            entity.Property(e => e.MeApiKey)
                .HasColumnType("text")
                .HasColumnName("ME_api_key");
            entity.Property(e => e.MeFacilities)
                .HasMaxLength(50)
                .HasColumnName("ME_facilities");
            entity.Property(e => e.MeHipaaDefaultOverride)
                .HasMaxLength(3)
                .HasColumnName("ME_hipaa_default_override");
            entity.Property(e => e.MeProviders)
                .HasMaxLength(100)
                .HasColumnName("ME_providers");
            entity.Property(e => e.MeUsername)
                .HasMaxLength(100)
                .HasColumnName("ME_username");
            entity.Property(e => e.MedExId)
                .HasDefaultValueSql("'0'")
                .HasColumnType("int(11)")
                .HasColumnName("MedEx_id");
            entity.Property(e => e.MedExLastupdated)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("timestamp")
                .HasColumnName("MedEx_lastupdated");
            entity.Property(e => e.MsgsDefaultYes)
                .HasMaxLength(3)
                .HasColumnName("MSGS_default_yes");
            entity.Property(e => e.PhoneCountryCode)
                .HasDefaultValueSql("'1'")
                .HasColumnType("int(4)")
                .HasColumnName("PHONE_country_code");
            entity.Property(e => e.PostcardTop)
                .HasMaxLength(255)
                .HasColumnName("postcard_top");
            entity.Property(e => e.PostcardsLocal)
                .HasMaxLength(3)
                .HasColumnName("POSTCARDS_local");
            entity.Property(e => e.PostcardsRemote)
                .HasMaxLength(3)
                .HasColumnName("POSTCARDS_remote");
            entity.Property(e => e.Status)
                .HasColumnType("text")
                .HasColumnName("status");
        });

        modelBuilder.Entity<MedexRecall>(entity =>
        {
            entity.HasKey(e => e.RId).HasName("PRIMARY");

            entity.ToTable("medex_recalls");

            entity.HasIndex(e => new { e.RPractid, e.RPid }, "r_PRACTID").IsUnique();

            entity.Property(e => e.RId)
                .HasColumnType("int(11)")
                .HasColumnName("r_ID");
            entity.Property(e => e.RCreated)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("timestamp")
                .HasColumnName("r_created");
            entity.Property(e => e.REventDate)
                .HasComment("Date of Appt or Recall")
                .HasColumnName("r_eventDate");
            entity.Property(e => e.RFacility)
                .HasColumnType("int(11)")
                .HasColumnName("r_facility");
            entity.Property(e => e.RPid)
                .HasComment("PatientID from pat_data")
                .HasColumnType("int(11)")
                .HasColumnName("r_pid");
            entity.Property(e => e.RPractid)
                .HasColumnType("int(11)")
                .HasColumnName("r_PRACTID");
            entity.Property(e => e.RProvider)
                .HasColumnType("int(11)")
                .HasColumnName("r_provider");
            entity.Property(e => e.RReason)
                .HasMaxLength(255)
                .HasColumnName("r_reason");
        });

        modelBuilder.Entity<Migration>(entity =>
        {
            entity.HasKey(e => e.Version).HasName("PRIMARY");

            entity.ToTable("migrations");

            entity.Property(e => e.Version)
                .HasMaxLength(191)
                .HasColumnName("version");
            entity.Property(e => e.ExecutedAt)
                .HasColumnType("datetime")
                .HasColumnName("executed_at");
            entity.Property(e => e.ExecutionDurationMs)
                .HasColumnType("int(11)")
                .HasColumnName("execution_duration_ms");
        });

        modelBuilder.Entity<MiscAddressBook>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("misc_address_book");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.City)
                .HasMaxLength(30)
                .HasColumnName("city");
            entity.Property(e => e.Fname)
                .HasMaxLength(255)
                .HasColumnName("fname");
            entity.Property(e => e.Lname)
                .HasMaxLength(255)
                .HasColumnName("lname");
            entity.Property(e => e.Mname)
                .HasMaxLength(255)
                .HasColumnName("mname");
            entity.Property(e => e.Phone)
                .HasMaxLength(30)
                .HasColumnName("phone");
            entity.Property(e => e.State)
                .HasMaxLength(30)
                .HasColumnName("state");
            entity.Property(e => e.Street)
                .HasMaxLength(60)
                .HasColumnName("street");
            entity.Property(e => e.Zip)
                .HasMaxLength(20)
                .HasColumnName("zip");
        });

        modelBuilder.Entity<Module>(entity =>
        {
            entity.HasKey(e => new { e.ModId, e.ModDirectory })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("modules");

            entity.Property(e => e.ModId)
                .ValueGeneratedOnAdd()
                .HasColumnType("int(11)")
                .HasColumnName("mod_id");
            entity.Property(e => e.ModDirectory)
                .HasMaxLength(64)
                .HasDefaultValueSql("''")
                .HasColumnName("mod_directory");
            entity.Property(e => e.AclVersion)
                .HasMaxLength(150)
                .HasColumnName("acl_version");
            entity.Property(e => e.Date)
                .HasColumnType("datetime")
                .HasColumnName("date");
            entity.Property(e => e.Directory)
                .HasMaxLength(255)
                .HasColumnName("directory");
            entity.Property(e => e.ModActive)
                .HasColumnType("int(1) unsigned")
                .HasColumnName("mod_active");
            entity.Property(e => e.ModDescription)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("mod_description");
            entity.Property(e => e.ModEncMenu)
                .HasMaxLength(10)
                .HasDefaultValueSql("'no'")
                .HasColumnName("mod_enc_menu");
            entity.Property(e => e.ModName)
                .HasMaxLength(64)
                .HasDefaultValueSql("'0'")
                .HasColumnName("mod_name");
            entity.Property(e => e.ModNickName)
                .HasMaxLength(25)
                .HasDefaultValueSql("''")
                .HasColumnName("mod_nick_name");
            entity.Property(e => e.ModParent)
                .HasMaxLength(64)
                .HasDefaultValueSql("''")
                .HasColumnName("mod_parent");
            entity.Property(e => e.ModRelativeLink)
                .HasMaxLength(64)
                .HasDefaultValueSql("''")
                .HasColumnName("mod_relative_link");
            entity.Property(e => e.ModType)
                .HasMaxLength(64)
                .HasDefaultValueSql("''")
                .HasColumnName("mod_type");
            entity.Property(e => e.ModUiActive)
                .HasColumnType("int(1) unsigned")
                .HasColumnName("mod_ui_active");
            entity.Property(e => e.ModUiName)
                .HasMaxLength(64)
                .HasDefaultValueSql("''")
                .HasColumnName("mod_ui_name");
            entity.Property(e => e.ModUiOrder)
                .HasColumnType("tinyint(3)")
                .HasColumnName("mod_ui_order");
            entity.Property(e => e.PermissionsItemTable)
                .HasMaxLength(100)
                .IsFixedLength()
                .HasColumnName("permissions_item_table");
            entity.Property(e => e.SqlRun)
                .HasDefaultValueSql("'0'")
                .HasColumnType("tinyint(4)")
                .HasColumnName("sql_run");
            entity.Property(e => e.SqlVersion)
                .HasMaxLength(150)
                .HasColumnName("sql_version");
            entity.Property(e => e.Type)
                .HasDefaultValueSql("'0'")
                .HasColumnType("tinyint(4)")
                .HasColumnName("type");
        });

        modelBuilder.Entity<ModuleAclGroupSetting>(entity =>
        {
            entity.HasKey(e => new { e.ModuleId, e.GroupId, e.SectionId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0, 0 });

            entity.ToTable("module_acl_group_settings");

            entity.Property(e => e.ModuleId)
                .HasColumnType("int(11)")
                .HasColumnName("module_id");
            entity.Property(e => e.GroupId)
                .HasColumnType("int(11)")
                .HasColumnName("group_id");
            entity.Property(e => e.SectionId)
                .HasColumnType("int(11)")
                .HasColumnName("section_id");
            entity.Property(e => e.Allowed).HasColumnName("allowed");
        });

        modelBuilder.Entity<ModuleAclSection>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("module_acl_sections");

            entity.Property(e => e.ModuleId)
                .HasColumnType("int(11)")
                .HasColumnName("module_id");
            entity.Property(e => e.ParentSection)
                .HasColumnType("int(11)")
                .HasColumnName("parent_section");
            entity.Property(e => e.SectionId)
                .HasColumnType("int(11)")
                .HasColumnName("section_id");
            entity.Property(e => e.SectionIdentifier)
                .HasMaxLength(50)
                .HasColumnName("section_identifier");
            entity.Property(e => e.SectionName)
                .HasMaxLength(255)
                .HasColumnName("section_name");
        });

        modelBuilder.Entity<ModuleAclUserSetting>(entity =>
        {
            entity.HasKey(e => new { e.ModuleId, e.UserId, e.SectionId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0, 0 });

            entity.ToTable("module_acl_user_settings");

            entity.Property(e => e.ModuleId)
                .HasColumnType("int(11)")
                .HasColumnName("module_id");
            entity.Property(e => e.UserId)
                .HasColumnType("int(11)")
                .HasColumnName("user_id");
            entity.Property(e => e.SectionId)
                .HasColumnType("int(11)")
                .HasColumnName("section_id");
            entity.Property(e => e.Allowed)
                .HasColumnType("int(1)")
                .HasColumnName("allowed");
        });

        modelBuilder.Entity<ModuleConfiguration>(entity =>
        {
            entity.HasKey(e => e.ModuleConfigId).HasName("PRIMARY");

            entity.ToTable("module_configuration");

            entity.Property(e => e.ModuleConfigId)
                .HasColumnType("int(10) unsigned")
                .HasColumnName("module_config_id");
            entity.Property(e => e.CreatedBy)
                .HasComment("users.id the user that first created this record")
                .HasColumnType("bigint(20)")
                .HasColumnName("created_by");
            entity.Property(e => e.DateAdded)
                .HasComment("Datetime the record was initially created")
                .HasColumnType("datetime")
                .HasColumnName("date_added");
            entity.Property(e => e.DateCreated)
                .HasComment("Datetime the record was created")
                .HasColumnType("datetime")
                .HasColumnName("date_created");
            entity.Property(e => e.DateModified)
                .HasDefaultValueSql("current_timestamp()")
                .HasComment("Datetime the record was last modified")
                .HasColumnType("datetime")
                .HasColumnName("date_modified");
            entity.Property(e => e.FieldName)
                .HasMaxLength(45)
                .HasColumnName("field_name");
            entity.Property(e => e.FieldValue)
                .HasMaxLength(255)
                .HasColumnName("field_value");
            entity.Property(e => e.ModuleId)
                .HasColumnType("int(10) unsigned")
                .HasColumnName("module_id");
            entity.Property(e => e.UpdatedBy)
                .HasComment("users.id the user that last modified this record")
                .HasColumnType("bigint(20)")
                .HasColumnName("updated_by");
        });

        modelBuilder.Entity<ModulesHooksSetting>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("modules_hooks_settings");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.AttachedTo)
                .HasMaxLength(45)
                .HasColumnName("attached_to");
            entity.Property(e => e.EnabledHooks)
                .HasMaxLength(255)
                .HasColumnName("enabled_hooks");
            entity.Property(e => e.ModId)
                .HasColumnType("int(11)")
                .HasColumnName("mod_id");
        });

        modelBuilder.Entity<ModulesSetting>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("modules_settings");

            entity.Property(e => e.FldType)
                .HasComment("1=>ACL,2=>preferences,3=>hooks")
                .HasColumnType("smallint(6)")
                .HasColumnName("fld_type");
            entity.Property(e => e.MenuName)
                .HasMaxLength(255)
                .HasColumnName("menu_name");
            entity.Property(e => e.ModId)
                .HasColumnType("int(11)")
                .HasColumnName("mod_id");
            entity.Property(e => e.ObjName)
                .HasMaxLength(255)
                .HasColumnName("obj_name");
            entity.Property(e => e.Path)
                .HasMaxLength(255)
                .HasColumnName("path");
        });

        modelBuilder.Entity<Note>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("notes");

            entity.HasIndex(e => e.Date, "date");

            entity.HasIndex(e => e.Owner, "foreign_id");

            entity.HasIndex(e => e.ForeignId, "foreign_id_2");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.Date)
                .HasColumnType("datetime")
                .HasColumnName("date");
            entity.Property(e => e.ForeignId)
                .HasColumnType("int(11)")
                .HasColumnName("foreign_id");
            entity.Property(e => e.Note1)
                .HasMaxLength(255)
                .HasColumnName("note");
            entity.Property(e => e.Owner)
                .HasColumnType("int(11)")
                .HasColumnName("owner");
            entity.Property(e => e.Revision)
                .HasColumnType("timestamp")
                .HasColumnName("revision");
        });

        modelBuilder.Entity<NotificationLog>(entity =>
        {
            entity.HasKey(e => e.ILogId).HasName("PRIMARY");

            entity.ToTable("notification_log");

            entity.Property(e => e.ILogId)
                .HasColumnType("int(11)")
                .HasColumnName("iLogId");
            entity.Property(e => e.DSentDateTime)
                .HasColumnType("datetime")
                .HasColumnName("dSentDateTime");
            entity.Property(e => e.EmailSender)
                .HasMaxLength(255)
                .HasColumnName("email_sender");
            entity.Property(e => e.EmailSubject)
                .HasMaxLength(255)
                .HasColumnName("email_subject");
            entity.Property(e => e.Message)
                .HasColumnType("text")
                .HasColumnName("message");
            entity.Property(e => e.PatientInfo)
                .HasColumnType("text")
                .HasColumnName("patient_info");
            entity.Property(e => e.PcEid)
                .HasColumnType("int(11) unsigned")
                .HasColumnName("pc_eid");
            entity.Property(e => e.PcEndDate).HasColumnName("pc_endDate");
            entity.Property(e => e.PcEndTime)
                .HasColumnType("time")
                .HasColumnName("pc_endTime");
            entity.Property(e => e.PcEventDate).HasColumnName("pc_eventDate");
            entity.Property(e => e.PcStartTime)
                .HasColumnType("time")
                .HasColumnName("pc_startTime");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.SmsGatewayType)
                .HasMaxLength(50)
                .HasColumnName("sms_gateway_type");
            entity.Property(e => e.SmsgatewayInfo)
                .HasMaxLength(255)
                .HasColumnName("smsgateway_info");
            entity.Property(e => e.Type)
                .HasColumnType("enum('SMS','Email')")
                .HasColumnName("type");
        });

        modelBuilder.Entity<NotificationSetting>(entity =>
        {
            entity.HasKey(e => e.SettingsId).HasName("PRIMARY");

            entity.ToTable("notification_settings");

            entity.Property(e => e.SettingsId).HasColumnType("int(3)");
            entity.Property(e => e.SendEmailBeforeHours)
                .HasColumnType("int(3)")
                .HasColumnName("Send_Email_Before_Hours");
            entity.Property(e => e.SendSmsBeforeHours)
                .HasColumnType("int(3)")
                .HasColumnName("Send_SMS_Before_Hours");
            entity.Property(e => e.SmsGatewayApikey)
                .HasMaxLength(100)
                .HasColumnName("SMS_gateway_apikey");
            entity.Property(e => e.SmsGatewayPassword)
                .HasMaxLength(100)
                .HasColumnName("SMS_gateway_password");
            entity.Property(e => e.SmsGatewayUsername)
                .HasMaxLength(100)
                .HasColumnName("SMS_gateway_username");
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .HasColumnName("type");
        });

        modelBuilder.Entity<OauthClient>(entity =>
        {
            entity.HasKey(e => e.ClientId).HasName("PRIMARY");

            entity.ToTable("oauth_clients");

            entity.Property(e => e.ClientId)
                .HasMaxLength(80)
                .HasColumnName("client_id");
            entity.Property(e => e.ClientName)
                .HasMaxLength(80)
                .HasColumnName("client_name");
            entity.Property(e => e.ClientRole)
                .HasMaxLength(20)
                .HasColumnName("client_role");
            entity.Property(e => e.ClientSecret)
                .HasColumnType("text")
                .HasColumnName("client_secret");
            entity.Property(e => e.Contacts)
                .HasColumnType("text")
                .HasColumnName("contacts");
            entity.Property(e => e.DsiType)
                .HasDefaultValueSql("'1'")
                .HasComment("0=none, 1=evidence-based,2=predictive")
                .HasColumnType("tinyint(3) unsigned")
                .HasColumnName("dsi_type");
            entity.Property(e => e.Endorsements)
                .HasColumnType("text")
                .HasColumnName("endorsements");
            entity.Property(e => e.GrantTypes)
                .HasMaxLength(80)
                .HasColumnName("grant_types");
            entity.Property(e => e.InitiateLoginUri)
                .HasColumnType("text")
                .HasColumnName("initiate_login_uri");
            entity.Property(e => e.IsConfidential)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("is_confidential");
            entity.Property(e => e.IsEnabled).HasColumnName("is_enabled");
            entity.Property(e => e.Jwks)
                .HasColumnType("text")
                .HasColumnName("jwks");
            entity.Property(e => e.JwksUri)
                .HasColumnType("text")
                .HasColumnName("jwks_uri");
            entity.Property(e => e.LogoutRedirectUris)
                .HasColumnType("text")
                .HasColumnName("logout_redirect_uris");
            entity.Property(e => e.PolicyUri)
                .HasColumnType("text")
                .HasColumnName("policy_uri");
            entity.Property(e => e.RedirectUri)
                .HasColumnType("text")
                .HasColumnName("redirect_uri");
            entity.Property(e => e.RegisterDate)
                .HasColumnType("datetime")
                .HasColumnName("register_date");
            entity.Property(e => e.RegistrationToken)
                .HasMaxLength(80)
                .HasColumnName("registration_token");
            entity.Property(e => e.RegistrationUriPath)
                .HasMaxLength(40)
                .HasColumnName("registration_uri_path");
            entity.Property(e => e.RevokeDate)
                .HasColumnType("datetime")
                .HasColumnName("revoke_date");
            entity.Property(e => e.Scope)
                .HasColumnType("text")
                .HasColumnName("scope");
            entity.Property(e => e.SiteId)
                .HasMaxLength(64)
                .HasColumnName("site_id");
            entity.Property(e => e.SkipEhrLaunchAuthorizationFlow).HasColumnName("skip_ehr_launch_authorization_flow");
            entity.Property(e => e.TosUri)
                .HasColumnType("text")
                .HasColumnName("tos_uri");
            entity.Property(e => e.UserId)
                .HasMaxLength(40)
                .HasColumnName("user_id");
        });

        modelBuilder.Entity<OauthTrustedUser>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("oauth_trusted_user");

            entity.HasIndex(e => e.UserId, "accounts_id");

            entity.HasIndex(e => e.ClientId, "clients_id");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.ClientId)
                .HasMaxLength(80)
                .HasColumnName("client_id");
            entity.Property(e => e.Code)
                .HasColumnType("text")
                .HasColumnName("code");
            entity.Property(e => e.GrantType)
                .HasMaxLength(32)
                .HasColumnName("grant_type");
            entity.Property(e => e.PersistLogin)
                .HasDefaultValueSql("'0'")
                .HasColumnName("persist_login");
            entity.Property(e => e.Scope)
                .HasColumnType("text")
                .HasColumnName("scope");
            entity.Property(e => e.SessionCache)
                .HasColumnType("text")
                .HasColumnName("session_cache");
            entity.Property(e => e.Time)
                .HasColumnType("timestamp")
                .HasColumnName("time");
            entity.Property(e => e.UserId)
                .HasMaxLength(80)
                .HasColumnName("user_id");
        });

        modelBuilder.Entity<OnetimeAuth>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("onetime_auth");

            entity.HasIndex(e => new { e.Pid, e.OnetimeToken }, "pid").HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 32 });

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.AccessCount)
                .HasColumnType("int(11)")
                .HasColumnName("access_count");
            entity.Property(e => e.Context)
                .HasMaxLength(64)
                .HasColumnName("context");
            entity.Property(e => e.CreateUserId)
                .HasColumnType("bigint(20)")
                .HasColumnName("create_user_id");
            entity.Property(e => e.DateCreated)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("date_created");
            entity.Property(e => e.Expires)
                .HasColumnType("int(11)")
                .HasColumnName("expires");
            entity.Property(e => e.LastAccessed)
                .HasColumnType("datetime")
                .HasColumnName("last_accessed");
            entity.Property(e => e.OnetimeActions)
                .HasComment("JSON array of actions that can be performed with this token")
                .HasColumnType("text")
                .HasColumnName("onetime_actions");
            entity.Property(e => e.OnetimePin)
                .HasMaxLength(10)
                .HasComment("Max 10 numeric. Default 6")
                .HasColumnName("onetime_pin");
            entity.Property(e => e.OnetimeToken)
                .HasColumnType("tinytext")
                .HasColumnName("onetime_token");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.Profile)
                .HasComment("profile of scope for this token")
                .HasColumnType("tinytext")
                .HasColumnName("profile");
            entity.Property(e => e.RedirectUrl)
                .HasColumnType("tinytext")
                .HasColumnName("redirect_url");
            entity.Property(e => e.RemoteIp)
                .HasMaxLength(32)
                .HasColumnName("remote_ip");
            entity.Property(e => e.Scope)
                .HasComment("context scope for this token")
                .HasColumnType("tinytext")
                .HasColumnName("scope");
        });

        modelBuilder.Entity<Onote>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("onotes");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Activity)
                .HasColumnType("tinyint(4)")
                .HasColumnName("activity");
            entity.Property(e => e.Body).HasColumnName("body");
            entity.Property(e => e.Date)
                .HasColumnType("datetime")
                .HasColumnName("date");
            entity.Property(e => e.Groupname)
                .HasMaxLength(255)
                .HasColumnName("groupname");
            entity.Property(e => e.User)
                .HasMaxLength(255)
                .HasColumnName("user");
        });

        modelBuilder.Entity<OnsiteDocument>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("onsite_documents");

            entity.Property(e => e.Id)
                .HasColumnType("int(10) unsigned")
                .HasColumnName("id");
            entity.Property(e => e.AcceptSignedStatus)
                .HasColumnType("smallint(5)")
                .HasColumnName("accept_signed_status");
            entity.Property(e => e.AuthorizeSignedTime)
                .HasColumnType("datetime")
                .HasColumnName("authorize_signed_time");
            entity.Property(e => e.AuthorizedSignature)
                .HasColumnType("text")
                .HasColumnName("authorized_signature");
            entity.Property(e => e.AuthorizingSignator)
                .HasMaxLength(50)
                .HasColumnName("authorizing_signator");
            entity.Property(e => e.CreateDate)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("timestamp")
                .HasColumnName("create_date");
            entity.Property(e => e.DenialReason)
                .HasMaxLength(255)
                .HasColumnName("denial_reason");
            entity.Property(e => e.DocType)
                .HasMaxLength(255)
                .HasColumnName("doc_type");
            entity.Property(e => e.Encounter)
                .HasColumnType("int(10) unsigned")
                .HasColumnName("encounter");
            entity.Property(e => e.Facility)
                .HasColumnType("int(10) unsigned")
                .HasColumnName("facility");
            entity.Property(e => e.FileName)
                .HasMaxLength(255)
                .HasColumnName("file_name");
            entity.Property(e => e.FilePath)
                .HasMaxLength(255)
                .HasColumnName("file_path");
            entity.Property(e => e.FullDocument)
                .HasColumnType("mediumblob")
                .HasColumnName("full_document");
            entity.Property(e => e.PatientSignature)
                .HasColumnType("text")
                .HasColumnName("patient_signature");
            entity.Property(e => e.PatientSignedStatus)
                .HasColumnType("smallint(5) unsigned")
                .HasColumnName("patient_signed_status");
            entity.Property(e => e.PatientSignedTime)
                .HasColumnType("datetime")
                .HasColumnName("patient_signed_time");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20) unsigned")
                .HasColumnName("pid");
            entity.Property(e => e.Provider)
                .HasColumnType("int(10) unsigned")
                .HasColumnName("provider");
            entity.Property(e => e.ReviewDate)
                .HasColumnType("datetime")
                .HasColumnName("review_date");
            entity.Property(e => e.TemplateData).HasColumnName("template_data");
        });

        modelBuilder.Entity<OnsiteMail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("onsite_mail");

            entity.HasIndex(e => e.Owner, "pid");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Activity)
                .HasColumnType("tinyint(4)")
                .HasColumnName("activity");
            entity.Property(e => e.AssignedTo)
                .HasMaxLength(255)
                .HasColumnName("assigned_to");
            entity.Property(e => e.Authorized)
                .HasColumnType("tinyint(4)")
                .HasColumnName("authorized");
            entity.Property(e => e.Body).HasColumnName("body");
            entity.Property(e => e.Date)
                .HasColumnType("datetime")
                .HasColumnName("date");
            entity.Property(e => e.DeleteDate)
                .HasColumnType("datetime")
                .HasColumnName("delete_date");
            entity.Property(e => e.Deleted)
                .HasDefaultValueSql("'0'")
                .HasComment("flag indicates note is deleted")
                .HasColumnType("tinyint(4)")
                .HasColumnName("deleted");
            entity.Property(e => e.Groupname)
                .HasMaxLength(255)
                .HasColumnName("groupname");
            entity.Property(e => e.Header)
                .HasMaxLength(255)
                .HasColumnName("header");
            entity.Property(e => e.IsMsgEncrypted)
                .HasDefaultValueSql("'0'")
                .HasComment("Whether messsage encrypted 0-Not encrypted, 1-Encrypted")
                .HasColumnType("tinyint(2)")
                .HasColumnName("is_msg_encrypted");
            entity.Property(e => e.MailChain)
                .HasColumnType("int(11)")
                .HasColumnName("mail_chain");
            entity.Property(e => e.MessageStatus)
                .HasMaxLength(20)
                .HasDefaultValueSql("'New'")
                .HasColumnName("message_status");
            entity.Property(e => e.Mtype)
                .HasMaxLength(128)
                .HasColumnName("mtype");
            entity.Property(e => e.Owner)
                .HasMaxLength(128)
                .HasColumnName("owner");
            entity.Property(e => e.RecipientId)
                .HasMaxLength(128)
                .HasColumnName("recipient_id");
            entity.Property(e => e.RecipientName)
                .HasMaxLength(255)
                .HasColumnName("recipient_name");
            entity.Property(e => e.ReplyMailChain)
                .HasColumnType("int(11)")
                .HasColumnName("reply_mail_chain");
            entity.Property(e => e.SenderId)
                .HasMaxLength(128)
                .HasColumnName("sender_id");
            entity.Property(e => e.SenderName)
                .HasMaxLength(255)
                .HasColumnName("sender_name");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasColumnName("title");
            entity.Property(e => e.User)
                .HasMaxLength(255)
                .HasColumnName("user");
        });

        modelBuilder.Entity<OnsiteMessage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("onsite_messages", tb => tb.HasComment("Portal messages"));

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.Date)
                .HasColumnType("datetime")
                .HasColumnName("date");
            entity.Property(e => e.Ip)
                .HasMaxLength(15)
                .HasColumnName("ip");
            entity.Property(e => e.Message).HasColumnName("message");
            entity.Property(e => e.RecipId)
                .HasMaxLength(255)
                .HasComment("who to id array")
                .HasColumnName("recip_id");
            entity.Property(e => e.SenderId)
                .HasMaxLength(64)
                .HasComment("who sent id")
                .HasColumnName("sender_id");
            entity.Property(e => e.Username)
                .HasMaxLength(64)
                .HasColumnName("username");
        });

        modelBuilder.Entity<OnsiteOnline>(entity =>
        {
            entity.HasKey(e => e.Hash).HasName("PRIMARY");

            entity.ToTable("onsite_online");

            entity.Property(e => e.Hash)
                .HasMaxLength(32)
                .HasColumnName("hash");
            entity.Property(e => e.Ip)
                .HasMaxLength(15)
                .HasColumnName("ip");
            entity.Property(e => e.LastUpdate)
                .HasColumnType("datetime")
                .HasColumnName("last_update");
            entity.Property(e => e.Userid)
                .HasColumnType("int(11) unsigned")
                .HasColumnName("userid");
            entity.Property(e => e.Username)
                .HasMaxLength(64)
                .HasColumnName("username");
        });

        modelBuilder.Entity<OnsitePortalActivity>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("onsite_portal_activity");

            entity.HasIndex(e => e.Date, "date");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.ActionTaken)
                .HasMaxLength(255)
                .HasColumnName("action_taken");
            entity.Property(e => e.ActionTakenTime)
                .HasColumnType("datetime")
                .HasColumnName("action_taken_time");
            entity.Property(e => e.ActionUser)
                .HasColumnType("int(11)")
                .HasColumnName("action_user");
            entity.Property(e => e.Activity)
                .HasMaxLength(255)
                .HasColumnName("activity");
            entity.Property(e => e.Checksum).HasColumnName("checksum");
            entity.Property(e => e.Date)
                .HasColumnType("datetime")
                .HasColumnName("date");
            entity.Property(e => e.Narrative).HasColumnName("narrative");
            entity.Property(e => e.PatientId)
                .HasColumnType("bigint(20)")
                .HasColumnName("patient_id");
            entity.Property(e => e.PendingAction)
                .HasMaxLength(255)
                .HasColumnName("pending_action");
            entity.Property(e => e.RequireAudit)
                .HasDefaultValueSql("'1'")
                .HasColumnName("require_audit");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .HasColumnName("status");
            entity.Property(e => e.TableAction).HasColumnName("table_action");
            entity.Property(e => e.TableArgs).HasColumnName("table_args");
        });

        modelBuilder.Entity<OnsiteSignature>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("onsite_signatures");

            entity.HasIndex(e => e.Encounter, "encounter");

            entity.HasIndex(e => new { e.Pid, e.User }, "pid").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Activity)
                .HasColumnType("tinyint(4)")
                .HasColumnName("activity");
            entity.Property(e => e.Authorized)
                .HasColumnType("tinyint(4)")
                .HasColumnName("authorized");
            entity.Property(e => e.Created)
                .HasColumnType("int(11)")
                .HasColumnName("created");
            entity.Property(e => e.Encounter)
                .HasColumnType("int(11)")
                .HasColumnName("encounter");
            entity.Property(e => e.Ip)
                .HasMaxLength(46)
                .HasColumnName("ip");
            entity.Property(e => e.Lastmod)
                .HasColumnType("datetime")
                .HasColumnName("lastmod");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.SigHash)
                .HasMaxLength(255)
                .HasColumnName("sig_hash");
            entity.Property(e => e.SigImage)
                .HasColumnType("text")
                .HasColumnName("sig_image");
            entity.Property(e => e.Signator)
                .HasMaxLength(255)
                .HasColumnName("signator");
            entity.Property(e => e.Signature)
                .HasColumnType("text")
                .HasColumnName("signature");
            entity.Property(e => e.Status)
                .HasMaxLength(128)
                .HasDefaultValueSql("'waiting'")
                .HasColumnName("status");
            entity.Property(e => e.Type)
                .HasMaxLength(128)
                .HasColumnName("type");
            entity.Property(e => e.User).HasColumnName("user");
        });

        modelBuilder.Entity<OpenemrModule>(entity =>
        {
            entity.HasKey(e => e.PnId).HasName("PRIMARY");

            entity.ToTable("openemr_modules");

            entity.Property(e => e.PnId)
                .HasColumnType("int(11) unsigned")
                .HasColumnName("pn_id");
            entity.Property(e => e.PnAdminCapable).HasColumnName("pn_admin_capable");
            entity.Property(e => e.PnDescription)
                .HasMaxLength(255)
                .HasColumnName("pn_description");
            entity.Property(e => e.PnDirectory)
                .HasMaxLength(64)
                .HasColumnName("pn_directory");
            entity.Property(e => e.PnDisplayname)
                .HasMaxLength(64)
                .HasColumnName("pn_displayname");
            entity.Property(e => e.PnName)
                .HasMaxLength(64)
                .HasColumnName("pn_name");
            entity.Property(e => e.PnRegid)
                .HasColumnType("int(11) unsigned")
                .HasColumnName("pn_regid");
            entity.Property(e => e.PnState).HasColumnName("pn_state");
            entity.Property(e => e.PnType)
                .HasColumnType("int(6)")
                .HasColumnName("pn_type");
            entity.Property(e => e.PnUserCapable).HasColumnName("pn_user_capable");
            entity.Property(e => e.PnVersion)
                .HasMaxLength(10)
                .HasColumnName("pn_version");
        });

        modelBuilder.Entity<OpenemrModuleVar>(entity =>
        {
            entity.HasKey(e => e.PnId).HasName("PRIMARY");

            entity.ToTable("openemr_module_vars");

            entity.HasIndex(e => e.PnModname, "pn_modname");

            entity.HasIndex(e => e.PnName, "pn_name");

            entity.Property(e => e.PnId)
                .HasColumnType("int(11) unsigned")
                .HasColumnName("pn_id");
            entity.Property(e => e.PnModname)
                .HasMaxLength(64)
                .HasColumnName("pn_modname");
            entity.Property(e => e.PnName)
                .HasMaxLength(64)
                .HasColumnName("pn_name");
            entity.Property(e => e.PnValue).HasColumnName("pn_value");
        });

        modelBuilder.Entity<OpenemrPostcalendarCategory>(entity =>
        {
            entity.HasKey(e => e.PcCatid).HasName("PRIMARY");

            entity.ToTable("openemr_postcalendar_categories");

            entity.HasIndex(e => new { e.PcCatname, e.PcCatcolor }, "basic_cat");

            entity.HasIndex(e => e.PcConstantId, "pc_constant_id").IsUnique();

            entity.Property(e => e.PcCatid)
                .HasColumnType("int(11) unsigned")
                .HasColumnName("pc_catid");
            entity.Property(e => e.AcoSpec)
                .HasMaxLength(63)
                .HasDefaultValueSql("'encounters|notes'")
                .HasColumnName("aco_spec");
            entity.Property(e => e.PcActive)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("pc_active");
            entity.Property(e => e.PcCatcolor)
                .HasMaxLength(50)
                .HasColumnName("pc_catcolor");
            entity.Property(e => e.PcCatdesc)
                .HasColumnType("text")
                .HasColumnName("pc_catdesc");
            entity.Property(e => e.PcCatname)
                .HasMaxLength(100)
                .HasColumnName("pc_catname");
            entity.Property(e => e.PcCattype)
                .HasComment("Used in grouping categories")
                .HasColumnType("int(11)")
                .HasColumnName("pc_cattype");
            entity.Property(e => e.PcConstantId).HasColumnName("pc_constant_id");
            entity.Property(e => e.PcDailylimit)
                .HasColumnType("int(2)")
                .HasColumnName("pc_dailylimit");
            entity.Property(e => e.PcDuration)
                .HasColumnType("bigint(20)")
                .HasColumnName("pc_duration");
            entity.Property(e => e.PcEndAllDay).HasColumnName("pc_end_all_day");
            entity.Property(e => e.PcEndDateFlag).HasColumnName("pc_end_date_flag");
            entity.Property(e => e.PcEndDateFreq)
                .HasColumnType("int(11)")
                .HasColumnName("pc_end_date_freq");
            entity.Property(e => e.PcEndDateType)
                .HasColumnType("int(2)")
                .HasColumnName("pc_end_date_type");
            entity.Property(e => e.PcEnddate).HasColumnName("pc_enddate");
            entity.Property(e => e.PcLastUpdated)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("pc_last_updated");
            entity.Property(e => e.PcRecurrfreq)
                .HasColumnType("int(3)")
                .HasColumnName("pc_recurrfreq");
            entity.Property(e => e.PcRecurrspec)
                .HasColumnType("text")
                .HasColumnName("pc_recurrspec");
            entity.Property(e => e.PcRecurrtype)
                .HasColumnType("int(1)")
                .HasColumnName("pc_recurrtype");
            entity.Property(e => e.PcSeq)
                .HasColumnType("int(11)")
                .HasColumnName("pc_seq");
        });

        modelBuilder.Entity<OpenemrPostcalendarEvent>(entity =>
        {
            entity.HasKey(e => e.PcEid).HasName("PRIMARY");

            entity.ToTable("openemr_postcalendar_events");

            entity.HasIndex(e => new { e.PcCatid, e.PcAid, e.PcEventDate, e.PcEndDate, e.PcEventstatus, e.PcSharing, e.PcTopic }, "basic_event");

            entity.HasIndex(e => e.PcEventDate, "pc_eventDate");

            entity.HasIndex(e => e.Uuid, "uuid").IsUnique();

            entity.Property(e => e.PcEid)
                .HasColumnType("int(11) unsigned")
                .HasColumnName("pc_eid");
            entity.Property(e => e.PcAid)
                .HasMaxLength(30)
                .HasColumnName("pc_aid");
            entity.Property(e => e.PcAlldayevent)
                .HasColumnType("int(1)")
                .HasColumnName("pc_alldayevent");
            entity.Property(e => e.PcApptstatus)
                .HasMaxLength(15)
                .HasDefaultValueSql("'-'")
                .HasColumnName("pc_apptstatus");
            entity.Property(e => e.PcBillingLocation)
                .HasColumnType("smallint(6)")
                .HasColumnName("pc_billing_location");
            entity.Property(e => e.PcCatid)
                .HasColumnType("int(11)")
                .HasColumnName("pc_catid");
            entity.Property(e => e.PcComments)
                .HasDefaultValueSql("'0'")
                .HasColumnType("int(11)")
                .HasColumnName("pc_comments");
            entity.Property(e => e.PcContemail)
                .HasMaxLength(255)
                .HasColumnName("pc_contemail");
            entity.Property(e => e.PcContname)
                .HasMaxLength(50)
                .HasColumnName("pc_contname");
            entity.Property(e => e.PcConttel)
                .HasMaxLength(50)
                .HasColumnName("pc_conttel");
            entity.Property(e => e.PcCounter)
                .HasDefaultValueSql("'0'")
                .HasColumnType("mediumint(8) unsigned")
                .HasColumnName("pc_counter");
            entity.Property(e => e.PcDuration)
                .HasColumnType("bigint(20)")
                .HasColumnName("pc_duration");
            entity.Property(e => e.PcEndDate).HasColumnName("pc_endDate");
            entity.Property(e => e.PcEndTime)
                .HasColumnType("time")
                .HasColumnName("pc_endTime");
            entity.Property(e => e.PcEventDate).HasColumnName("pc_eventDate");
            entity.Property(e => e.PcEventstatus)
                .HasColumnType("int(11)")
                .HasColumnName("pc_eventstatus");
            entity.Property(e => e.PcFacility)
                .HasComment("facility id for this event")
                .HasColumnType("int(11)")
                .HasColumnName("pc_facility");
            entity.Property(e => e.PcFee)
                .HasMaxLength(50)
                .HasColumnName("pc_fee");
            entity.Property(e => e.PcGid)
                .HasDefaultValueSql("'0'")
                .HasColumnType("int(11)")
                .HasColumnName("pc_gid");
            entity.Property(e => e.PcHometext)
                .HasColumnType("text")
                .HasColumnName("pc_hometext");
            entity.Property(e => e.PcInformant)
                .HasMaxLength(20)
                .HasColumnName("pc_informant");
            entity.Property(e => e.PcLanguage)
                .HasMaxLength(30)
                .HasColumnName("pc_language");
            entity.Property(e => e.PcLocation)
                .HasColumnType("text")
                .HasColumnName("pc_location");
            entity.Property(e => e.PcMultiple)
                .HasColumnType("int(10) unsigned")
                .HasColumnName("pc_multiple");
            entity.Property(e => e.PcPid)
                .HasMaxLength(11)
                .HasColumnName("pc_pid");
            entity.Property(e => e.PcPrefcatid)
                .HasColumnType("int(11)")
                .HasColumnName("pc_prefcatid");
            entity.Property(e => e.PcRecurrfreq)
                .HasColumnType("int(3)")
                .HasColumnName("pc_recurrfreq");
            entity.Property(e => e.PcRecurrspec)
                .HasColumnType("text")
                .HasColumnName("pc_recurrspec");
            entity.Property(e => e.PcRecurrtype)
                .HasColumnType("int(1)")
                .HasColumnName("pc_recurrtype");
            entity.Property(e => e.PcRoom)
                .HasMaxLength(20)
                .HasDefaultValueSql("''")
                .HasColumnName("pc_room");
            entity.Property(e => e.PcSendalertemail)
                .HasMaxLength(3)
                .HasDefaultValueSql("'NO'")
                .HasColumnName("pc_sendalertemail");
            entity.Property(e => e.PcSendalertsms)
                .HasMaxLength(3)
                .HasDefaultValueSql("'NO'")
                .HasColumnName("pc_sendalertsms");
            entity.Property(e => e.PcSharing)
                .HasColumnType("int(11)")
                .HasColumnName("pc_sharing");
            entity.Property(e => e.PcStartTime)
                .HasColumnType("time")
                .HasColumnName("pc_startTime");
            entity.Property(e => e.PcTime)
                .HasColumnType("datetime")
                .HasColumnName("pc_time");
            entity.Property(e => e.PcTitle)
                .HasMaxLength(150)
                .HasColumnName("pc_title");
            entity.Property(e => e.PcTopic)
                .HasDefaultValueSql("'1'")
                .HasColumnType("int(3)")
                .HasColumnName("pc_topic");
            entity.Property(e => e.PcWebsite)
                .HasMaxLength(255)
                .HasColumnName("pc_website");
            entity.Property(e => e.Uuid)
                .HasMaxLength(16)
                .IsFixedLength()
                .HasColumnName("uuid");
        });

        modelBuilder.Entity<PatientAccessOnsite>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("patient_access_onsite");

            entity.HasIndex(e => e.Pid, "pid").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.DateCreated)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("date_created");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.PortalFailCounter)
                .HasDefaultValueSql("'0'")
                .HasComment("Per-portal-account failure counter. Independent of ip_login_fail_counter so a valid login on account A cannot clear an in-progress brute force against account B.")
                .HasColumnType("bigint(20)")
                .HasColumnName("portal_fail_counter");
            entity.Property(e => e.PortalLastFail)
                .HasComment("Timestamp of the last portal login failure for this account. Used for time-based counter reset.")
                .HasColumnType("datetime")
                .HasColumnName("portal_last_fail");
            entity.Property(e => e.PortalLoginUsername)
                .HasMaxLength(100)
                .HasComment("User entered username")
                .HasColumnName("portal_login_username");
            entity.Property(e => e.PortalOnetime)
                .HasMaxLength(255)
                .HasColumnName("portal_onetime");
            entity.Property(e => e.PortalPwd)
                .HasMaxLength(255)
                .HasColumnName("portal_pwd");
            entity.Property(e => e.PortalPwdStatus)
                .HasDefaultValueSql("'1'")
                .HasComment("0=>Password Created Through Demographics by The provider or staff. Patient Should Change it at first time it.1=>Pwd updated or created by patient itself")
                .HasColumnType("tinyint(4)")
                .HasColumnName("portal_pwd_status");
            entity.Property(e => e.PortalUsername)
                .HasMaxLength(100)
                .HasColumnName("portal_username");
        });

        modelBuilder.Entity<PatientBirthdayAlert>(entity =>
        {
            entity.HasKey(e => new { e.Pid, e.UserId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("patient_birthday_alert");

            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.UserId)
                .HasColumnType("bigint(20)")
                .HasColumnName("user_id");
            entity.Property(e => e.TurnedOffOn).HasColumnName("turned_off_on");
        });

        modelBuilder.Entity<PatientCareExperiencePreference>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("patient_care_experience_preferences");

            entity.HasIndex(e => e.ObservationCode, "observation_code");

            entity.HasIndex(e => e.PatientId, "patient_id");

            entity.HasIndex(e => e.Status, "status");

            entity.HasIndex(e => e.Uuid, "unq_uuid").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.EffectiveDatetime)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("effective_datetime");
            entity.Property(e => e.Note)
                .HasColumnType("text")
                .HasColumnName("note");
            entity.Property(e => e.ObservationCode)
                .HasMaxLength(50)
                .HasComment("LOINC code")
                .HasColumnName("observation_code");
            entity.Property(e => e.ObservationCodeText)
                .HasMaxLength(255)
                .HasColumnName("observation_code_text");
            entity.Property(e => e.PatientId)
                .HasColumnType("int(11)")
                .HasColumnName("patient_id");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'final'")
                .HasComment("valid options are final,amended,preliminary")
                .HasColumnName("status");
            entity.Property(e => e.Uuid)
                .HasMaxLength(16)
                .IsFixedLength()
                .HasColumnName("uuid");
            entity.Property(e => e.ValueBoolean).HasColumnName("value_boolean");
            entity.Property(e => e.ValueCode)
                .HasMaxLength(50)
                .HasComment("fk to preference_value_sets.answer_code")
                .HasColumnName("value_code");
            entity.Property(e => e.ValueCodeSystem)
                .HasMaxLength(255)
                .HasComment("fk to preference_value_sets.answer_system")
                .HasColumnName("value_code_system");
            entity.Property(e => e.ValueDisplay)
                .HasMaxLength(255)
                .HasComment("fk to preference_value_sets.answer_display")
                .HasColumnName("value_display");
            entity.Property(e => e.ValueText)
                .HasColumnType("text")
                .HasColumnName("value_text");
            entity.Property(e => e.ValueType)
                .HasDefaultValueSql("'coded'")
                .HasColumnType("enum('coded','text','boolean')")
                .HasColumnName("value_type");
        });

        modelBuilder.Entity<PatientDatum>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("patient_data");

            entity.HasIndex(e => e.Id, "id");

            entity.HasIndex(e => e.Dob, "idx_patient_dob");

            entity.HasIndex(e => new { e.Lname, e.Fname }, "idx_patient_name");

            entity.HasIndex(e => e.Pid, "pid").IsUnique();

            entity.HasIndex(e => e.Uuid, "uuid").IsUnique();

            entity.Property(e => e.AdReviewed)
                .HasComment("Date and time the advance care directive was reviewed and validated by the authenticator user.")
                .HasColumnType("datetime")
                .HasColumnName("ad_reviewed");
            entity.Property(e => e.AdvanceDirectiveUserAuthenticator)
                .HasComment("fk to users.id of the user who authenticates that the advance care directive is valid.")
                .HasColumnType("bigint(20)")
                .HasColumnName("advance_directive_user_authenticator");
            entity.Property(e => e.AllowHealthInfoEx)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("allow_health_info_ex");
            entity.Property(e => e.AllowImmInfoShare)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("allow_imm_info_share");
            entity.Property(e => e.AllowImmRegUse)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("allow_imm_reg_use");
            entity.Property(e => e.AllowPatientPortal)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasColumnName("allow_patient_portal");
            entity.Property(e => e.BillingNote)
                .HasColumnType("text")
                .HasColumnName("billing_note");
            entity.Property(e => e.BirthFname)
                .HasColumnType("text")
                .HasColumnName("birth_fname");
            entity.Property(e => e.BirthLname)
                .HasColumnType("text")
                .HasColumnName("birth_lname");
            entity.Property(e => e.BirthMname)
                .HasColumnType("text")
                .HasColumnName("birth_mname");
            entity.Property(e => e.CareTeamFacility)
                .HasColumnType("text")
                .HasColumnName("care_team_facility");
            entity.Property(e => e.CareTeamProvider)
                .HasColumnType("text")
                .HasColumnName("care_team_provider");
            entity.Property(e => e.CareTeamStatus)
                .HasColumnType("text")
                .HasColumnName("care_team_status");
            entity.Property(e => e.City)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("city");
            entity.Property(e => e.CmsportalLogin)
                .HasMaxLength(60)
                .HasDefaultValueSql("''")
                .HasColumnName("cmsportal_login");
            entity.Property(e => e.CompletedAd)
                .HasMaxLength(3)
                .HasDefaultValueSql("'NO'")
                .HasColumnName("completed_ad");
            entity.Property(e => e.ContactRelationship)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("contact_relationship");
            entity.Property(e => e.Contrastart)
                .HasComment("Date contraceptives initially used")
                .HasColumnName("contrastart");
            entity.Property(e => e.CountryCode)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("country_code");
            entity.Property(e => e.County)
                .HasMaxLength(40)
                .HasDefaultValueSql("''")
                .HasColumnName("county");
            entity.Property(e => e.CreatedBy)
                .HasComment("users.id the user that first created this record")
                .HasColumnType("bigint(20)")
                .HasColumnName("created_by");
            entity.Property(e => e.Date)
                .HasColumnType("datetime")
                .HasColumnName("date");
            entity.Property(e => e.DeceasedDate)
                .HasColumnType("datetime")
                .HasColumnName("deceased_date");
            entity.Property(e => e.DeceasedReason)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("deceased_reason");
            entity.Property(e => e.Dob).HasColumnName("DOB");
            entity.Property(e => e.DriversLicense)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("drivers_license");
            entity.Property(e => e.Dupscore)
                .HasDefaultValueSql("-9")
                .HasColumnType("int(11)")
                .HasColumnName("dupscore");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("email");
            entity.Property(e => e.EmailDirect)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("email_direct");
            entity.Property(e => e.Ethnicity)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("ethnicity");
            entity.Property(e => e.Ethnoracial)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("ethnoracial");
            entity.Property(e => e.FamilySize)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("family_size");
            entity.Property(e => e.Financial)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("financial");
            entity.Property(e => e.FinancialReview)
                .HasColumnType("datetime")
                .HasColumnName("financial_review");
            entity.Property(e => e.Fitness)
                .HasColumnType("int(11)")
                .HasColumnName("fitness");
            entity.Property(e => e.Fname)
                .HasDefaultValueSql("''")
                .HasColumnName("fname");
            entity.Property(e => e.GenderIdentity)
                .HasColumnType("text")
                .HasColumnName("gender_identity");
            entity.Property(e => e.Genericname1)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("genericname1");
            entity.Property(e => e.Genericname2)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("genericname2");
            entity.Property(e => e.Genericval1)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("genericval1");
            entity.Property(e => e.Genericval2)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("genericval2");
            entity.Property(e => e.Guardianaddress)
                .HasColumnType("text")
                .HasColumnName("guardianaddress");
            entity.Property(e => e.Guardiancity)
                .HasColumnType("text")
                .HasColumnName("guardiancity");
            entity.Property(e => e.Guardiancountry)
                .HasColumnType("text")
                .HasColumnName("guardiancountry");
            entity.Property(e => e.Guardianemail)
                .HasColumnType("text")
                .HasColumnName("guardianemail");
            entity.Property(e => e.Guardianphone)
                .HasColumnType("text")
                .HasColumnName("guardianphone");
            entity.Property(e => e.Guardianpostalcode)
                .HasColumnType("text")
                .HasColumnName("guardianpostalcode");
            entity.Property(e => e.Guardianrelationship)
                .HasColumnType("text")
                .HasColumnName("guardianrelationship");
            entity.Property(e => e.Guardiansex)
                .HasColumnType("text")
                .HasColumnName("guardiansex");
            entity.Property(e => e.Guardiansname)
                .HasColumnType("text")
                .HasColumnName("guardiansname");
            entity.Property(e => e.Guardianstate)
                .HasColumnType("text")
                .HasColumnName("guardianstate");
            entity.Property(e => e.Guardianworkphone)
                .HasColumnType("text")
                .HasColumnName("guardianworkphone");
            entity.Property(e => e.HipaaAllowemail)
                .HasMaxLength(3)
                .HasDefaultValueSql("'NO'")
                .HasColumnName("hipaa_allowemail");
            entity.Property(e => e.HipaaAllowsms)
                .HasMaxLength(3)
                .HasDefaultValueSql("'NO'")
                .HasColumnName("hipaa_allowsms");
            entity.Property(e => e.HipaaMail)
                .HasMaxLength(3)
                .HasDefaultValueSql("''")
                .HasColumnName("hipaa_mail");
            entity.Property(e => e.HipaaMessage)
                .HasMaxLength(20)
                .HasDefaultValueSql("''")
                .HasColumnName("hipaa_message");
            entity.Property(e => e.HipaaNotice)
                .HasMaxLength(3)
                .HasDefaultValueSql("''")
                .HasColumnName("hipaa_notice");
            entity.Property(e => e.HipaaVoice)
                .HasMaxLength(3)
                .HasDefaultValueSql("''")
                .HasColumnName("hipaa_voice");
            entity.Property(e => e.Homeless)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("homeless");
            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.ImmRegStatEffdate)
                .HasColumnType("text")
                .HasColumnName("imm_reg_stat_effdate");
            entity.Property(e => e.ImmRegStatus)
                .HasColumnType("text")
                .HasColumnName("imm_reg_status");
            entity.Property(e => e.Industry)
                .HasColumnType("text")
                .HasColumnName("industry");
            entity.Property(e => e.Interpreter)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasComment("original field used for determining if patient needs an interpreter, now used for additional notes about need for interpreter")
                .HasColumnName("interpreter");
            entity.Property(e => e.InterpreterNeeded)
                .HasComment("fk to list_options.option_id where list_id=yes_no_unknown used to determine if patient needs an interpreter")
                .HasColumnType("text")
                .HasColumnName("interpreter_needed");
            entity.Property(e => e.Language)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("language");
            entity.Property(e => e.LastUpdated)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("last_updated");
            entity.Property(e => e.Lname)
                .HasDefaultValueSql("''")
                .HasColumnName("lname");
            entity.Property(e => e.Migrantseasonal)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("migrantseasonal");
            entity.Property(e => e.Mname)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("mname");
            entity.Property(e => e.MonthlyIncome)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("monthly_income");
            entity.Property(e => e.Mothersname)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("mothersname");
            entity.Property(e => e.NameHistory)
                .HasColumnType("tinytext")
                .HasColumnName("name_history");
            entity.Property(e => e.NationalityCountry)
                .HasColumnType("tinytext")
                .HasColumnName("nationality_country");
            entity.Property(e => e.Occupation).HasColumnName("occupation");
            entity.Property(e => e.PatientGroups)
                .HasColumnType("text")
                .HasColumnName("patient_groups");
            entity.Property(e => e.PharmacyId)
                .HasColumnType("int(11)")
                .HasColumnName("pharmacy_id");
            entity.Property(e => e.PhoneBiz)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("phone_biz");
            entity.Property(e => e.PhoneCell)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("phone_cell");
            entity.Property(e => e.PhoneContact)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("phone_contact");
            entity.Property(e => e.PhoneHome)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("phone_home");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.PostalCode)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("postal_code");
            entity.Property(e => e.PreferredName)
                .HasColumnType("tinytext")
                .HasColumnName("preferred_name");
            entity.Property(e => e.PreventPortalApps)
                .HasColumnType("text")
                .HasColumnName("prevent_portal_apps");
            entity.Property(e => e.Pricelevel)
                .HasMaxLength(255)
                .HasDefaultValueSql("'standard'")
                .HasColumnName("pricelevel");
            entity.Property(e => e.Pronoun)
                .HasColumnType("text")
                .HasColumnName("pronoun");
            entity.Property(e => e.ProtIndiEffdate)
                .HasColumnType("text")
                .HasColumnName("prot_indi_effdate");
            entity.Property(e => e.ProtectIndicator)
                .HasColumnType("text")
                .HasColumnName("protect_indicator");
            entity.Property(e => e.ProviderId)
                .HasColumnType("int(11)")
                .HasColumnName("providerID");
            entity.Property(e => e.ProviderSinceDate)
                .HasColumnType("tinytext")
                .HasColumnName("provider_since_date");
            entity.Property(e => e.PublCodeEffDate)
                .HasColumnType("text")
                .HasColumnName("publ_code_eff_date");
            entity.Property(e => e.PublicityCode)
                .HasColumnType("text")
                .HasColumnName("publicity_code");
            entity.Property(e => e.Pubpid)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("pubpid");
            entity.Property(e => e.Race)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("race");
            entity.Property(e => e.RefProviderId)
                .HasColumnType("int(11)")
                .HasColumnName("ref_providerID");
            entity.Property(e => e.ReferralSource)
                .HasMaxLength(30)
                .HasDefaultValueSql("''")
                .HasColumnName("referral_source");
            entity.Property(e => e.Referrer)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("referrer");
            entity.Property(e => e.ReferrerId)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("referrerID");
            entity.Property(e => e.Regdate)
                .HasComment("Registration Date")
                .HasColumnType("datetime")
                .HasColumnName("regdate");
            entity.Property(e => e.Religion)
                .HasMaxLength(40)
                .HasDefaultValueSql("''")
                .HasColumnName("religion");
            entity.Property(e => e.Sex)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasComment("Sex at birth")
                .HasColumnName("sex");
            entity.Property(e => e.SexIdentified)
                .HasComment("Patient reported current sex")
                .HasColumnType("text")
                .HasColumnName("sex_identified");
            entity.Property(e => e.SexualOrientation)
                .HasColumnType("text")
                .HasColumnName("sexual_orientation");
            entity.Property(e => e.SoapImportStatus)
                .HasComment("1-Prescription Press 2-Prescription Import 3-Allergy Press 4-Allergy Import")
                .HasColumnType("tinyint(4)")
                .HasColumnName("soap_import_status");
            entity.Property(e => e.Squad)
                .HasMaxLength(32)
                .HasDefaultValueSql("''")
                .HasColumnName("squad");
            entity.Property(e => e.Ss)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("ss");
            entity.Property(e => e.State)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("state");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("status");
            entity.Property(e => e.Street)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("street");
            entity.Property(e => e.StreetLine2)
                .HasColumnType("tinytext")
                .HasColumnName("street_line_2");
            entity.Property(e => e.Suffix)
                .HasColumnType("tinytext")
                .HasColumnName("suffix");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("title");
            entity.Property(e => e.TribalAffiliations)
                .HasColumnType("text")
                .HasColumnName("tribal_affiliations");
            entity.Property(e => e.UpdatedBy)
                .HasComment("users.id the user that last modified this record")
                .HasColumnType("bigint(20)")
                .HasColumnName("updated_by");
            entity.Property(e => e.Userlist1)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("userlist1");
            entity.Property(e => e.Userlist2)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("userlist2");
            entity.Property(e => e.Userlist3)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("userlist3");
            entity.Property(e => e.Userlist4)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("userlist4");
            entity.Property(e => e.Userlist5)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("userlist5");
            entity.Property(e => e.Userlist6)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("userlist6");
            entity.Property(e => e.Userlist7)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("userlist7");
            entity.Property(e => e.Usertext1)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("usertext1");
            entity.Property(e => e.Usertext2)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("usertext2");
            entity.Property(e => e.Usertext3)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("usertext3");
            entity.Property(e => e.Usertext4)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("usertext4");
            entity.Property(e => e.Usertext5)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("usertext5");
            entity.Property(e => e.Usertext6)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("usertext6");
            entity.Property(e => e.Usertext7)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("usertext7");
            entity.Property(e => e.Usertext8)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("usertext8");
            entity.Property(e => e.Uuid)
                .HasMaxLength(16)
                .IsFixedLength()
                .HasColumnName("uuid");
            entity.Property(e => e.Vfc)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("vfc");
        });

        modelBuilder.Entity<PatientHistory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("patient_history");

            entity.HasIndex(e => e.Pid, "pid_idx");

            entity.HasIndex(e => e.Uuid, "uuid").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.CareTeamFacility)
                .HasColumnType("text")
                .HasColumnName("care_team_facility");
            entity.Property(e => e.CareTeamProvider)
                .HasColumnType("text")
                .HasColumnName("care_team_provider");
            entity.Property(e => e.CreatedBy)
                .HasComment("users.id the user that first created this record")
                .HasColumnType("bigint(20)")
                .HasColumnName("created_by");
            entity.Property(e => e.Date)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("date");
            entity.Property(e => e.HistoryTypeKey)
                .HasMaxLength(36)
                .HasColumnName("history_type_key");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.PreviousNameEnddate).HasColumnName("previous_name_enddate");
            entity.Property(e => e.PreviousNameFirst)
                .HasColumnType("text")
                .HasColumnName("previous_name_first");
            entity.Property(e => e.PreviousNameLast)
                .HasColumnType("text")
                .HasColumnName("previous_name_last");
            entity.Property(e => e.PreviousNameMiddle)
                .HasColumnType("text")
                .HasColumnName("previous_name_middle");
            entity.Property(e => e.PreviousNamePrefix)
                .HasColumnType("text")
                .HasColumnName("previous_name_prefix");
            entity.Property(e => e.PreviousNameSuffix)
                .HasColumnType("text")
                .HasColumnName("previous_name_suffix");
            entity.Property(e => e.Uuid)
                .HasMaxLength(16)
                .IsFixedLength()
                .HasColumnName("uuid");
        });

        modelBuilder.Entity<PatientPortalMenu>(entity =>
        {
            entity.HasKey(e => e.PatientPortalMenuId).HasName("PRIMARY");

            entity.ToTable("patient_portal_menu");

            entity.Property(e => e.PatientPortalMenuId)
                .HasColumnType("int(11)")
                .HasColumnName("patient_portal_menu_id");
            entity.Property(e => e.MenuName)
                .HasMaxLength(40)
                .HasColumnName("menu_name");
            entity.Property(e => e.MenuOrder)
                .HasColumnType("smallint(4)")
                .HasColumnName("menu_order");
            entity.Property(e => e.MenuStatus)
                .HasDefaultValueSql("'1'")
                .HasColumnType("tinyint(2)")
                .HasColumnName("menu_status");
            entity.Property(e => e.PatientPortalMenuGroupId)
                .HasColumnType("int(11)")
                .HasColumnName("patient_portal_menu_group_id");
        });

        modelBuilder.Entity<PatientReminder>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("patient_reminders");

            entity.HasIndex(e => new { e.Category, e.Item }, "category");

            entity.HasIndex(e => e.Pid, "pid");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Active)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasComment("1 if active and 0 if not active")
                .HasColumnName("active");
            entity.Property(e => e.Category)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("Maps to the category item in the rule_action_item table")
                .HasColumnName("category");
            entity.Property(e => e.DateCreated)
                .HasColumnType("datetime")
                .HasColumnName("date_created");
            entity.Property(e => e.DateInactivated)
                .HasColumnType("datetime")
                .HasColumnName("date_inactivated");
            entity.Property(e => e.DateSent)
                .HasColumnType("datetime")
                .HasColumnName("date_sent");
            entity.Property(e => e.DueStatus)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("Maps to list_options list rule_reminder_due_opt")
                .HasColumnName("due_status");
            entity.Property(e => e.EmailStatus)
                .HasComment("0 if not sent and 1 if sent")
                .HasColumnName("email_status");
            entity.Property(e => e.Item)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("Maps to the item column in the rule_action_item table")
                .HasColumnName("item");
            entity.Property(e => e.MailStatus)
                .HasComment("0 if not sent and 1 if sent")
                .HasColumnName("mail_status");
            entity.Property(e => e.Pid)
                .HasComment("id from patient_data table")
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.ReasonInactivated)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("Maps to list_options list rule_reminder_inactive_opt")
                .HasColumnName("reason_inactivated");
            entity.Property(e => e.SmsStatus)
                .HasComment("0 if not sent and 1 if sent")
                .HasColumnName("sms_status");
            entity.Property(e => e.VoiceStatus)
                .HasComment("0 if not sent and 1 if sent")
                .HasColumnName("voice_status");
        });

        modelBuilder.Entity<PatientSetting>(entity =>
        {
            entity.HasKey(e => new { e.SettingPatient, e.SettingLabel })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("patient_settings");

            entity.Property(e => e.SettingPatient)
                .HasColumnType("bigint(20)")
                .HasColumnName("setting_patient");
            entity.Property(e => e.SettingLabel)
                .HasMaxLength(100)
                .HasColumnName("setting_label");
            entity.Property(e => e.SettingValue)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("setting_value");
        });

        modelBuilder.Entity<PatientTracker>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("patient_tracker");

            entity.HasIndex(e => e.Eid, "eid");

            entity.HasIndex(e => e.Pid, "pid");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Apptdate).HasColumnName("apptdate");
            entity.Property(e => e.Appttime)
                .HasColumnType("time")
                .HasColumnName("appttime");
            entity.Property(e => e.Date)
                .HasColumnType("datetime")
                .HasColumnName("date");
            entity.Property(e => e.DrugScreenCompleted).HasColumnName("drug_screen_completed");
            entity.Property(e => e.Eid)
                .HasColumnType("bigint(20)")
                .HasColumnName("eid");
            entity.Property(e => e.Encounter)
                .HasColumnType("bigint(20)")
                .HasColumnName("encounter");
            entity.Property(e => e.Lastseq)
                .HasMaxLength(4)
                .HasDefaultValueSql("''")
                .HasComment("The element file should contain this number of elements")
                .HasColumnName("lastseq");
            entity.Property(e => e.OriginalUser)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasComment("This is the user that created the original record")
                .HasColumnName("original_user");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.RandomDrugTest)
                .HasComment("NULL if not randomized. If randomized, 0 is no, 1 is yes")
                .HasColumnName("random_drug_test");
        });

        modelBuilder.Entity<PatientTrackerElement>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("patient_tracker_element");

            entity.HasIndex(e => new { e.PtTrackerId, e.Seq }, "pt_tracker_id");

            entity.Property(e => e.PtTrackerId)
                .HasComment("maps to id column in patient_tracker table")
                .HasColumnType("bigint(20)")
                .HasColumnName("pt_tracker_id");
            entity.Property(e => e.Room)
                .HasMaxLength(20)
                .HasDefaultValueSql("''")
                .HasColumnName("room");
            entity.Property(e => e.Seq)
                .HasMaxLength(4)
                .HasDefaultValueSql("''")
                .HasComment("This is a numerical sequence for this pt_tracker_id events")
                .HasColumnName("seq");
            entity.Property(e => e.StartDatetime)
                .HasColumnType("datetime")
                .HasColumnName("start_datetime");
            entity.Property(e => e.Status)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasColumnName("status");
            entity.Property(e => e.User)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasComment("This is the user that created this element")
                .HasColumnName("user");
        });

        modelBuilder.Entity<PatientTreatmentInterventionPreference>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("patient_treatment_intervention_preferences");

            entity.HasIndex(e => e.ObservationCode, "observation_code");

            entity.HasIndex(e => e.PatientId, "patient_id");

            entity.HasIndex(e => e.Status, "status");

            entity.HasIndex(e => e.Uuid, "unq_uuid").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.EffectiveDatetime)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("effective_datetime");
            entity.Property(e => e.Note)
                .HasColumnType("text")
                .HasColumnName("note");
            entity.Property(e => e.ObservationCode)
                .HasMaxLength(50)
                .HasComment("LOINC code")
                .HasColumnName("observation_code");
            entity.Property(e => e.ObservationCodeText)
                .HasMaxLength(255)
                .HasColumnName("observation_code_text");
            entity.Property(e => e.PatientId)
                .HasComment("fk to patient_data.pid")
                .HasColumnType("int(11)")
                .HasColumnName("patient_id");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'final'")
                .HasComment("valid options are final,amended,preliminary")
                .HasColumnName("status");
            entity.Property(e => e.Uuid)
                .HasMaxLength(16)
                .IsFixedLength()
                .HasColumnName("uuid");
            entity.Property(e => e.ValueBoolean).HasColumnName("value_boolean");
            entity.Property(e => e.ValueCode)
                .HasMaxLength(50)
                .HasComment("fk to preference_value_sets.answer_code")
                .HasColumnName("value_code");
            entity.Property(e => e.ValueCodeSystem)
                .HasMaxLength(255)
                .HasComment("fk to preference_value_sets.answer_system")
                .HasColumnName("value_code_system");
            entity.Property(e => e.ValueDisplay)
                .HasMaxLength(255)
                .HasComment("fk to preference_value_sets.answer_display")
                .HasColumnName("value_display");
            entity.Property(e => e.ValueText)
                .HasColumnType("text")
                .HasColumnName("value_text");
            entity.Property(e => e.ValueType)
                .HasDefaultValueSql("'coded'")
                .HasColumnType("enum('coded','text','boolean')")
                .HasColumnName("value_type");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("payments");

            entity.HasIndex(e => e.Pid, "pid");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Amount1)
                .HasPrecision(12, 2)
                .HasColumnName("amount1");
            entity.Property(e => e.Amount2)
                .HasPrecision(12, 2)
                .HasColumnName("amount2");
            entity.Property(e => e.Dtime)
                .HasColumnType("datetime")
                .HasColumnName("dtime");
            entity.Property(e => e.Encounter)
                .HasColumnType("bigint(20)")
                .HasColumnName("encounter");
            entity.Property(e => e.Method)
                .HasMaxLength(255)
                .HasColumnName("method");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.Posted1)
                .HasPrecision(12, 2)
                .HasColumnName("posted1");
            entity.Property(e => e.Posted2)
                .HasPrecision(12, 2)
                .HasColumnName("posted2");
            entity.Property(e => e.Source)
                .HasMaxLength(255)
                .HasColumnName("source");
            entity.Property(e => e.User)
                .HasMaxLength(255)
                .HasColumnName("user");
        });

        modelBuilder.Entity<PaymentGatewayDetail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("payment_gateway_details");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.LoginId)
                .HasMaxLength(255)
                .HasColumnName("login_id");
            entity.Property(e => e.Md5)
                .HasMaxLength(255)
                .HasColumnName("md5");
            entity.Property(e => e.ServiceName)
                .HasMaxLength(100)
                .HasColumnName("service_name");
            entity.Property(e => e.TransactionKey)
                .HasMaxLength(255)
                .HasColumnName("transaction_key");
        });

        modelBuilder.Entity<PaymentProcessingAudit>(entity =>
        {
            entity.HasKey(e => e.Uuid).HasName("PRIMARY");

            entity.ToTable("payment_processing_audit");

            entity.HasIndex(e => e.Pid, "pid");

            entity.HasIndex(e => e.Success, "success");

            entity.Property(e => e.Uuid)
                .HasMaxLength(16)
                .HasDefaultValueSql("x'00000000000000000000000000000000'")
                .IsFixedLength()
                .HasColumnName("uuid");
            entity.Property(e => e.ActionName)
                .HasMaxLength(50)
                .HasColumnName("action_name");
            entity.Property(e => e.Amount)
                .HasMaxLength(20)
                .HasColumnName("amount");
            entity.Property(e => e.AuditData)
                .HasColumnType("text")
                .HasColumnName("audit_data");
            entity.Property(e => e.Date)
                .HasColumnType("datetime")
                .HasColumnName("date");
            entity.Property(e => e.MapTransactionId)
                .HasMaxLength(100)
                .HasColumnName("map_transaction_id");
            entity.Property(e => e.MapUuid)
                .HasMaxLength(16)
                .IsFixedLength()
                .HasColumnName("map_uuid");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.RevertActionName)
                .HasMaxLength(50)
                .HasColumnName("revert_action_name");
            entity.Property(e => e.RevertAuditData)
                .HasColumnType("text")
                .HasColumnName("revert_audit_data");
            entity.Property(e => e.RevertDate)
                .HasColumnType("datetime")
                .HasColumnName("revert_date");
            entity.Property(e => e.RevertTransactionId)
                .HasMaxLength(100)
                .HasColumnName("revert_transaction_id");
            entity.Property(e => e.Reverted)
                .HasDefaultValueSql("'0'")
                .HasColumnType("tinyint(4)")
                .HasColumnName("reverted");
            entity.Property(e => e.Service)
                .HasMaxLength(50)
                .HasColumnName("service");
            entity.Property(e => e.Success)
                .HasDefaultValueSql("'0'")
                .HasColumnType("tinyint(4)")
                .HasColumnName("success");
            entity.Property(e => e.Ticket)
                .HasMaxLength(100)
                .HasColumnName("ticket");
            entity.Property(e => e.TransactionId)
                .HasMaxLength(100)
                .HasColumnName("transaction_id");
        });

        modelBuilder.Entity<Person>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("person", tb => tb.HasComment("Core person demographics - contact info in contact_telecom"));

            entity.HasIndex(e => e.Active, "idx_person_active");

            entity.HasIndex(e => e.BirthDate, "idx_person_dob");

            entity.HasIndex(e => new { e.LastName, e.FirstName }, "idx_person_name");

            entity.HasIndex(e => new { e.LastName, e.FirstName, e.BirthDate }, "idx_person_search");

            entity.HasIndex(e => e.Uuid, "uuid").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Active)
                .HasDefaultValueSql("'1'")
                .HasComment("1=active, 0=inactive")
                .HasColumnName("active");
            entity.Property(e => e.BirthDate).HasColumnName("birth_date");
            entity.Property(e => e.Communication)
                .HasMaxLength(254)
                .HasComment("Communication preferences/needs")
                .HasColumnName("communication");
            entity.Property(e => e.CreatedBy)
                .HasComment("users.id")
                .HasColumnType("bigint(20)")
                .HasColumnName("created_by");
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("created_date");
            entity.Property(e => e.DeathDate).HasColumnName("death_date");
            entity.Property(e => e.Ethnicity)
                .HasMaxLength(63)
                .HasColumnName("ethnicity");
            entity.Property(e => e.FirstName)
                .HasMaxLength(63)
                .HasColumnName("first_name");
            entity.Property(e => e.Gender)
                .HasMaxLength(31)
                .HasColumnName("gender");
            entity.Property(e => e.InactiveDate)
                .HasColumnType("datetime")
                .HasColumnName("inactive_date");
            entity.Property(e => e.InactiveReason)
                .HasMaxLength(255)
                .HasColumnName("inactive_reason");
            entity.Property(e => e.LastName)
                .HasMaxLength(63)
                .HasColumnName("last_name");
            entity.Property(e => e.MaritalStatus)
                .HasMaxLength(31)
                .HasColumnName("marital_status");
            entity.Property(e => e.MiddleName)
                .HasMaxLength(63)
                .HasColumnName("middle_name");
            entity.Property(e => e.Notes)
                .HasColumnType("text")
                .HasColumnName("notes");
            entity.Property(e => e.PreferredLanguage)
                .HasMaxLength(63)
                .HasComment("ISO 639-1 code")
                .HasColumnName("preferred_language");
            entity.Property(e => e.PreferredName)
                .HasMaxLength(63)
                .HasComment("Name person prefers to be called")
                .HasColumnName("preferred_name");
            entity.Property(e => e.Race)
                .HasMaxLength(63)
                .HasColumnName("race");
            entity.Property(e => e.Ssn)
                .HasMaxLength(31)
                .HasComment("Should be encrypted in application")
                .HasColumnName("ssn");
            entity.Property(e => e.Title)
                .HasMaxLength(31)
                .HasComment("Mr., Mrs., Dr., etc.")
                .HasColumnName("title");
            entity.Property(e => e.UpdatedBy)
                .HasComment("users.id")
                .HasColumnType("bigint(20)")
                .HasColumnName("updated_by");
            entity.Property(e => e.UpdatedDate)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("updated_date");
            entity.Property(e => e.Uuid)
                .HasMaxLength(16)
                .IsFixedLength()
                .HasColumnName("uuid");
        });

        modelBuilder.Entity<PersonPatientLink>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("person_patient_link", tb => tb.HasComment("Links person records to patient_data records when person becomes patient"));

            entity.HasIndex(e => e.Active, "idx_ppl_active");

            entity.HasIndex(e => e.LinkedDate, "idx_ppl_linked_date");

            entity.HasIndex(e => e.LinkMethod, "idx_ppl_method");

            entity.HasIndex(e => e.PatientId, "idx_ppl_patient");

            entity.HasIndex(e => e.PersonId, "idx_ppl_person");

            entity.HasIndex(e => new { e.PersonId, e.PatientId, e.Active }, "unique_active_link").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Active)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasComment("Whether link is active (allows soft delete)")
                .HasColumnName("active");
            entity.Property(e => e.LinkMethod)
                .HasMaxLength(50)
                .HasDefaultValueSql("'manual'")
                .HasComment("How link was created: manual, auto_detected, migrated, import")
                .HasColumnName("link_method");
            entity.Property(e => e.LinkedBy)
                .HasComment("FK to users.id - who created the link")
                .HasColumnType("bigint(20)")
                .HasColumnName("linked_by");
            entity.Property(e => e.LinkedDate)
                .HasDefaultValueSql("current_timestamp()")
                .HasComment("When the link was created")
                .HasColumnType("datetime")
                .HasColumnName("linked_date");
            entity.Property(e => e.Notes)
                .HasComment("Optional notes about why/how they were linked")
                .HasColumnType("text")
                .HasColumnName("notes");
            entity.Property(e => e.PatientId)
                .HasComment("FK to patient_data.id")
                .HasColumnType("bigint(20)")
                .HasColumnName("patient_id");
            entity.Property(e => e.PersonId)
                .HasComment("FK to person.id")
                .HasColumnType("bigint(20)")
                .HasColumnName("person_id");
        });

        modelBuilder.Entity<Pharmacy>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("pharmacies");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasColumnName("email");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.Ncpdp)
                .HasColumnType("int(12)")
                .HasColumnName("ncpdp");
            entity.Property(e => e.Npi)
                .HasColumnType("int(12)")
                .HasColumnName("npi");
            entity.Property(e => e.TransmitMethod)
                .HasDefaultValueSql("'1'")
                .HasColumnType("int(11)")
                .HasColumnName("transmit_method");
        });

        modelBuilder.Entity<PhoneNumber>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("phone_numbers");

            entity.HasIndex(e => e.ForeignId, "foreign_id");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.AreaCode)
                .HasMaxLength(3)
                .IsFixedLength()
                .HasColumnName("area_code");
            entity.Property(e => e.CountryCode)
                .HasMaxLength(5)
                .HasColumnName("country_code");
            entity.Property(e => e.ForeignId)
                .HasColumnType("int(11)")
                .HasColumnName("foreign_id");
            entity.Property(e => e.Number)
                .HasMaxLength(4)
                .HasColumnName("number");
            entity.Property(e => e.Prefix)
                .HasMaxLength(3)
                .IsFixedLength()
                .HasColumnName("prefix");
            entity.Property(e => e.Type)
                .HasColumnType("int(11)")
                .HasColumnName("type");
        });

        modelBuilder.Entity<Pnote>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("pnotes");

            entity.HasIndex(e => e.Pid, "pid");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Activity)
                .HasColumnType("tinyint(4)")
                .HasColumnName("activity");
            entity.Property(e => e.AssignedTo)
                .HasMaxLength(255)
                .HasColumnName("assigned_to");
            entity.Property(e => e.Authorized)
                .HasColumnType("tinyint(4)")
                .HasColumnName("authorized");
            entity.Property(e => e.Body).HasColumnName("body");
            entity.Property(e => e.Date)
                .HasColumnType("datetime")
                .HasColumnName("date");
            entity.Property(e => e.Deleted)
                .HasDefaultValueSql("'0'")
                .HasComment("flag indicates note is deleted")
                .HasColumnType("tinyint(4)")
                .HasColumnName("deleted");
            entity.Property(e => e.Groupname)
                .HasMaxLength(255)
                .HasColumnName("groupname");
            entity.Property(e => e.IsMsgEncrypted)
                .HasDefaultValueSql("'0'")
                .HasComment("Whether messsage encrypted 0-Not encrypted, 1-Encrypted")
                .HasColumnType("tinyint(2)")
                .HasColumnName("is_msg_encrypted");
            entity.Property(e => e.MessageStatus)
                .HasMaxLength(20)
                .HasDefaultValueSql("'New'")
                .HasColumnName("message_status");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.PortalRelation)
                .HasMaxLength(100)
                .HasColumnName("portal_relation");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasColumnName("title");
            entity.Property(e => e.UpdateBy)
                .HasColumnType("bigint(20)")
                .HasColumnName("update_by");
            entity.Property(e => e.UpdateDate)
                .HasColumnType("datetime")
                .HasColumnName("update_date");
            entity.Property(e => e.User)
                .HasMaxLength(255)
                .HasColumnName("user");
        });

        modelBuilder.Entity<PreferenceValueSet>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("preference_value_sets", tb => tb.HasComment("Answer lists for preference codes"));

            entity.HasIndex(e => e.LoincCode, "loinc_code");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.Active)
                .HasDefaultValueSql("'1'")
                .HasColumnName("active");
            entity.Property(e => e.AnswerCode)
                .HasMaxLength(100)
                .HasColumnName("answer_code");
            entity.Property(e => e.AnswerDefinition)
                .HasColumnType("text")
                .HasColumnName("answer_definition");
            entity.Property(e => e.AnswerDisplay)
                .HasMaxLength(255)
                .HasColumnName("answer_display");
            entity.Property(e => e.AnswerSystem)
                .HasMaxLength(255)
                .HasColumnName("answer_system");
            entity.Property(e => e.LoincCode)
                .HasMaxLength(50)
                .HasColumnName("loinc_code");
            entity.Property(e => e.SortOrder)
                .HasDefaultValueSql("'0'")
                .HasColumnType("int(11)")
                .HasColumnName("sort_order");
        });

        modelBuilder.Entity<Prescription>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("prescriptions");

            entity.HasIndex(e => e.PatientId, "patient_id");

            entity.HasIndex(e => e.Uuid, "uuid").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.Active)
                .HasDefaultValueSql("'1'")
                .HasColumnType("int(11)")
                .HasColumnName("active");
            entity.Property(e => e.CreatedBy)
                .HasComment("users.id the user that first created this record")
                .HasColumnType("bigint(20)")
                .HasColumnName("created_by");
            entity.Property(e => e.DateAdded)
                .HasComment("Datetime the prescriptions was initially created")
                .HasColumnType("datetime")
                .HasColumnName("date_added");
            entity.Property(e => e.DateModified)
                .HasComment("Datetime the prescriptions was last modified")
                .HasColumnType("datetime")
                .HasColumnName("date_modified");
            entity.Property(e => e.Datetime)
                .HasColumnType("datetime")
                .HasColumnName("datetime");
            entity.Property(e => e.Diagnosis)
                .HasComment("Diagnosis or reason for the prescription")
                .HasColumnType("text")
                .HasColumnName("diagnosis");
            entity.Property(e => e.Dosage)
                .HasMaxLength(100)
                .HasColumnName("dosage");
            entity.Property(e => e.Drug)
                .HasMaxLength(150)
                .HasColumnName("drug");
            entity.Property(e => e.DrugDosageInstructions)
                .HasComment("Medication dosage instructions")
                .HasColumnName("drug_dosage_instructions");
            entity.Property(e => e.DrugId)
                .HasColumnType("int(11)")
                .HasColumnName("drug_id");
            entity.Property(e => e.DrugInfoErx)
                .HasColumnType("text")
                .HasColumnName("drug_info_erx");
            entity.Property(e => e.Encounter)
                .HasColumnType("int(11)")
                .HasColumnName("encounter");
            entity.Property(e => e.EndDate).HasColumnName("end_date");
            entity.Property(e => e.ErxSource)
                .HasComment("0-OpenEMR 1-External")
                .HasColumnType("tinyint(4)")
                .HasColumnName("erx_source");
            entity.Property(e => e.ErxUploaded)
                .HasComment("0-Pending NewCrop upload 1-Uploaded to NewCrop")
                .HasColumnType("tinyint(4)")
                .HasColumnName("erx_uploaded");
            entity.Property(e => e.ExternalId)
                .HasMaxLength(20)
                .HasColumnName("external_id");
            entity.Property(e => e.FilledById)
                .HasColumnType("int(11)")
                .HasColumnName("filled_by_id");
            entity.Property(e => e.FilledDate).HasColumnName("filled_date");
            entity.Property(e => e.Form)
                .HasColumnType("int(3)")
                .HasColumnName("form");
            entity.Property(e => e.Indication)
                .HasColumnType("text")
                .HasColumnName("indication");
            entity.Property(e => e.Interval)
                .HasColumnType("int(11)")
                .HasColumnName("interval");
            entity.Property(e => e.Medication)
                .HasColumnType("int(11)")
                .HasColumnName("medication");
            entity.Property(e => e.Note)
                .HasColumnType("text")
                .HasColumnName("note");
            entity.Property(e => e.Ntx)
                .HasColumnType("int(2)")
                .HasColumnName("ntx");
            entity.Property(e => e.PatientId)
                .HasColumnType("bigint(20)")
                .HasColumnName("patient_id");
            entity.Property(e => e.PerRefill)
                .HasColumnType("int(11)")
                .HasColumnName("per_refill");
            entity.Property(e => e.PharmacyId)
                .HasColumnType("int(11)")
                .HasColumnName("pharmacy_id");
            entity.Property(e => e.Prescriptionguid)
                .HasMaxLength(50)
                .HasColumnName("prescriptionguid");
            entity.Property(e => e.Prn)
                .HasMaxLength(30)
                .HasColumnName("prn");
            entity.Property(e => e.ProviderId)
                .HasColumnType("int(11)")
                .HasColumnName("provider_id");
            entity.Property(e => e.Quantity)
                .HasMaxLength(31)
                .HasColumnName("quantity");
            entity.Property(e => e.Refills)
                .HasColumnType("int(11)")
                .HasColumnName("refills");
            entity.Property(e => e.RequestIntent)
                .HasMaxLength(100)
                .HasComment("option_id in list_options.list_id=medication-request-intent")
                .HasColumnName("request_intent");
            entity.Property(e => e.RequestIntentTitle)
                .HasMaxLength(255)
                .HasComment("title in list_options.list_id=medication-request-intent")
                .HasColumnName("request_intent_title");
            entity.Property(e => e.Route)
                .HasMaxLength(100)
                .HasComment("Max size 100 characters is same max as immunizations")
                .HasColumnName("route");
            entity.Property(e => e.Rtx)
                .HasColumnType("int(2)")
                .HasColumnName("rtx");
            entity.Property(e => e.RxnormDrugcode)
                .HasMaxLength(25)
                .HasColumnName("rxnorm_drugcode");
            entity.Property(e => e.Site)
                .HasMaxLength(50)
                .HasColumnName("site");
            entity.Property(e => e.Size)
                .HasMaxLength(25)
                .HasColumnName("size");
            entity.Property(e => e.StartDate).HasColumnName("start_date");
            entity.Property(e => e.Substitute)
                .HasColumnType("int(11)")
                .HasColumnName("substitute");
            entity.Property(e => e.TxDate).HasColumnName("txDate");
            entity.Property(e => e.Unit)
                .HasColumnType("int(11)")
                .HasColumnName("unit");
            entity.Property(e => e.UpdatedBy)
                .HasComment("users.id the user that last modified this record")
                .HasColumnType("bigint(20)")
                .HasColumnName("updated_by");
            entity.Property(e => e.UsageCategory)
                .HasMaxLength(100)
                .HasComment("option_id in list_options.list_id=medication-usage-category")
                .HasColumnName("usage_category");
            entity.Property(e => e.UsageCategoryTitle)
                .HasMaxLength(255)
                .HasComment("title in list_options.list_id=medication-usage-category")
                .HasColumnName("usage_category_title");
            entity.Property(e => e.User)
                .HasMaxLength(50)
                .HasColumnName("user");
            entity.Property(e => e.Uuid)
                .HasMaxLength(16)
                .IsFixedLength()
                .HasColumnName("uuid");
        });

        modelBuilder.Entity<Price>(entity =>
        {
            entity.HasKey(e => new { e.PrId, e.PrSelector, e.PrLevel })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0, 0 });

            entity.ToTable("prices");

            entity.Property(e => e.PrId)
                .HasMaxLength(11)
                .HasDefaultValueSql("''")
                .HasColumnName("pr_id");
            entity.Property(e => e.PrSelector)
                .HasDefaultValueSql("''")
                .HasComment("template selector for drugs, empty for codes")
                .HasColumnName("pr_selector");
            entity.Property(e => e.PrLevel)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasColumnName("pr_level");
            entity.Property(e => e.PrPrice)
                .HasPrecision(12, 2)
                .HasComment("price in local currency")
                .HasColumnName("pr_price");
        });

        modelBuilder.Entity<ProAssessment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("pro_assessments");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.AssessmentOid)
                .HasMaxLength(255)
                .HasComment("unique id for this specific assessment, pulled from assessment center API")
                .HasColumnName("assessment_oid");
            entity.Property(e => e.CreatedAt)
                .HasComment("timestamp recording the creation time of this assessment")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.Deadline)
                .HasComment("deadline to complete the form, will be used when sending notification and reminders")
                .HasColumnType("datetime")
                .HasColumnName("deadline");
            entity.Property(e => e.Error)
                .HasComment("Standard error for the score")
                .HasColumnName("error");
            entity.Property(e => e.FormName)
                .HasMaxLength(255)
                .HasComment("pulled from assessment center API")
                .HasColumnName("form_name");
            entity.Property(e => e.FormOid)
                .HasMaxLength(255)
                .HasComment("unique id for specific instrument, pulled from assessment center API")
                .HasColumnName("form_oid");
            entity.Property(e => e.PatientId)
                .HasComment("ID for patient to order the form for")
                .HasColumnType("int(11)")
                .HasColumnName("patient_id");
            entity.Property(e => e.Score)
                .HasComment("T-Score for the assessment")
                .HasColumnName("score");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .HasComment("ordered or completed")
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasComment("this field indicates the completion time when the status is completed")
                .HasColumnType("datetime")
                .HasColumnName("updated_at");
            entity.Property(e => e.UserId)
                .HasComment("ID for user that orders the form")
                .HasColumnType("int(11)")
                .HasColumnName("user_id");
        });

        modelBuilder.Entity<ProcedureAnswer>(entity =>
        {
            entity.HasKey(e => new { e.ProcedureOrderId, e.ProcedureOrderSeq, e.QuestionCode, e.AnswerSeq })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0, 0, 0 });

            entity.ToTable("procedure_answers");

            entity.Property(e => e.ProcedureOrderId)
                .HasComment("references procedure_order.procedure_order_id")
                .HasColumnType("bigint(20)")
                .HasColumnName("procedure_order_id");
            entity.Property(e => e.ProcedureOrderSeq)
                .HasComment("references procedure_order_code.procedure_order_seq")
                .HasColumnType("int(11)")
                .HasColumnName("procedure_order_seq");
            entity.Property(e => e.QuestionCode)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("references procedure_questions.question_code")
                .HasColumnName("question_code");
            entity.Property(e => e.AnswerSeq)
                .HasComment("supports multiple-choice questions. answer_seq, incremented in code")
                .HasColumnType("int(11)")
                .HasColumnName("answer_seq");
            entity.Property(e => e.Answer)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasComment("answer data")
                .HasColumnName("answer");
            entity.Property(e => e.ProcedureCode)
                .HasMaxLength(31)
                .HasColumnName("procedure_code");
        });

        modelBuilder.Entity<ProcedureOrder>(entity =>
        {
            entity.HasKey(e => e.ProcedureOrderId).HasName("PRIMARY");

            entity.ToTable("procedure_order");

            entity.HasIndex(e => new { e.DateOrdered, e.PatientId }, "datepid");

            entity.HasIndex(e => e.LocationId, "idx_location_id");

            entity.HasIndex(e => e.OrderIntent, "idx_order_intent");

            entity.HasIndex(e => e.ScheduledDate, "idx_scheduled_date");

            entity.HasIndex(e => e.SpecimenType, "idx_specimen_type");

            entity.HasIndex(e => e.PatientId, "patient_id");

            entity.HasIndex(e => e.Uuid, "uuid").IsUnique();

            entity.Property(e => e.ProcedureOrderId)
                .HasColumnType("bigint(20)")
                .HasColumnName("procedure_order_id");
            entity.Property(e => e.Account)
                .HasMaxLength(60)
                .HasColumnName("account");
            entity.Property(e => e.AccountFacility)
                .HasColumnType("int(11)")
                .HasColumnName("account_facility");
            entity.Property(e => e.Activity)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasComment("0 if deleted")
                .HasColumnName("activity");
            entity.Property(e => e.BillingType)
                .HasMaxLength(4)
                .HasColumnName("billing_type");
            entity.Property(e => e.ClinicalHx)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasComment("clinical history text that may be relevant to the order")
                .HasColumnName("clinical_hx");
            entity.Property(e => e.CollectorId)
                .HasColumnType("bigint(11)")
                .HasColumnName("collector_id");
            entity.Property(e => e.ControlId)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasComment("This is the CONTROL ID that is sent back from lab")
                .HasColumnName("control_id");
            entity.Property(e => e.DateCollected)
                .HasComment("time specimen collected")
                .HasColumnType("datetime")
                .HasColumnName("date_collected");
            entity.Property(e => e.DateOrdered)
                .HasColumnType("datetime")
                .HasColumnName("date_ordered");
            entity.Property(e => e.DateTransmitted)
                .HasComment("time of order transmission, null if unsent")
                .HasColumnType("datetime")
                .HasColumnName("date_transmitted");
            entity.Property(e => e.EncounterId)
                .HasComment("references form_encounter.encounter")
                .HasColumnType("bigint(20)")
                .HasColumnName("encounter_id");
            entity.Property(e => e.ExternalId)
                .HasMaxLength(20)
                .HasColumnName("external_id");
            entity.Property(e => e.HistoryOrder)
                .HasDefaultValueSql("'0'")
                .HasComment("references order is added for history purpose only.")
                .HasColumnType("enum('0','1')")
                .HasColumnName("history_order");
            entity.Property(e => e.LabId)
                .HasComment("references procedure_providers.ppid")
                .HasColumnType("bigint(20)")
                .HasColumnName("lab_id");
            entity.Property(e => e.LocationId)
                .HasComment("References facility.id for service location (FHIR locationReference)")
                .HasColumnType("int(11)")
                .HasColumnName("location_id");
            entity.Property(e => e.OrderAbn)
                .HasMaxLength(31)
                .HasDefaultValueSql("'not_required'")
                .HasColumnName("order_abn");
            entity.Property(e => e.OrderDiagnosis)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasComment("primary order diagnosis")
                .HasColumnName("order_diagnosis");
            entity.Property(e => e.OrderIntent)
                .HasMaxLength(31)
                .HasDefaultValueSql("'order'")
                .HasComment("FHIR intent: order, plan, directive, proposal")
                .HasColumnName("order_intent");
            entity.Property(e => e.OrderPriority)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasColumnName("order_priority");
            entity.Property(e => e.OrderPsc)
                .HasColumnType("tinyint(4)")
                .HasColumnName("order_psc");
            entity.Property(e => e.OrderStatus)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("pending,routed,complete,canceled")
                .HasColumnName("order_status");
            entity.Property(e => e.PatientId)
                .HasComment("references patient_data.pid")
                .HasColumnType("bigint(20)")
                .HasColumnName("patient_id");
            entity.Property(e => e.PatientInstructions)
                .HasColumnType("text")
                .HasColumnName("patient_instructions");
            entity.Property(e => e.PerformerType)
                .HasMaxLength(50)
                .HasComment("Type of performer: laboratory, radiology, pathology (SNOMED CT)")
                .HasColumnName("performer_type");
            entity.Property(e => e.ProcedureOrderType)
                .HasMaxLength(32)
                .HasDefaultValueSql("'laboratory_test'")
                .HasColumnName("procedure_order_type");
            entity.Property(e => e.ProviderId)
                .HasComment("references users.id, the ordering provider")
                .HasColumnType("bigint(20)")
                .HasColumnName("provider_id");
            entity.Property(e => e.ProviderNumber)
                .HasMaxLength(30)
                .HasColumnName("provider_number");
            entity.Property(e => e.ScheduledDate)
                .HasComment("Scheduled date for service (FHIR occurrence[x])")
                .HasColumnType("datetime")
                .HasColumnName("scheduled_date");
            entity.Property(e => e.ScheduledEnd)
                .HasComment("Scheduled end time (FHIR occurrencePeriod.end)")
                .HasColumnType("datetime")
                .HasColumnName("scheduled_end");
            entity.Property(e => e.ScheduledStart)
                .HasComment("Scheduled start time (FHIR occurrencePeriod.start)")
                .HasColumnType("datetime")
                .HasColumnName("scheduled_start");
            entity.Property(e => e.SpecimenFasting)
                .HasMaxLength(31)
                .HasColumnName("specimen_fasting");
            entity.Property(e => e.SpecimenLocation)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("from the Specimen_Location list")
                .HasColumnName("specimen_location");
            entity.Property(e => e.SpecimenType)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("from the Specimen_Type list")
                .HasColumnName("specimen_type");
            entity.Property(e => e.SpecimenVolume)
                .HasMaxLength(30)
                .HasDefaultValueSql("''")
                .HasComment("from a text input field")
                .HasColumnName("specimen_volume");
            entity.Property(e => e.Uuid)
                .HasMaxLength(16)
                .IsFixedLength()
                .HasColumnName("uuid");
        });

        modelBuilder.Entity<ProcedureOrderCode>(entity =>
        {
            entity.HasKey(e => new { e.ProcedureOrderId, e.ProcedureOrderSeq })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("procedure_order_code");

            entity.Property(e => e.ProcedureOrderId)
                .HasComment("references procedure_order.procedure_order_id")
                .HasColumnType("bigint(20)")
                .HasColumnName("procedure_order_id");
            entity.Property(e => e.ProcedureOrderSeq)
                .HasComment("Supports multiple tests per order. Procedure_order_seq, incremented in code")
                .HasColumnType("int(11)")
                .HasColumnName("procedure_order_seq");
            entity.Property(e => e.DateEnd)
                .HasColumnType("datetime")
                .HasColumnName("date_end");
            entity.Property(e => e.Diagnoses)
                .HasComment("diagnoses and maybe other coding (e.g. ICD9:111.11)")
                .HasColumnType("text")
                .HasColumnName("diagnoses");
            entity.Property(e => e.DoNotSend)
                .HasComment("0 = normal, 1 = do not transmit to lab")
                .HasColumnName("do_not_send");
            entity.Property(e => e.ProcedureCode)
                .HasMaxLength(64)
                .HasDefaultValueSql("''")
                .HasComment("like procedure_type.procedure_code")
                .HasColumnName("procedure_code");
            entity.Property(e => e.ProcedureName)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasComment("descriptive name of the procedure code")
                .HasColumnName("procedure_name");
            entity.Property(e => e.ProcedureOrderTitle)
                .HasMaxLength(255)
                .HasColumnName("procedure_order_title");
            entity.Property(e => e.ProcedureSource)
                .HasMaxLength(1)
                .HasDefaultValueSql("'1'")
                .IsFixedLength()
                .HasComment("1=original order, 2=added after order sent")
                .HasColumnName("procedure_source");
            entity.Property(e => e.ProcedureType)
                .HasMaxLength(31)
                .HasColumnName("procedure_type");
            entity.Property(e => e.ReasonCode)
                .HasMaxLength(31)
                .HasColumnName("reason_code");
            entity.Property(e => e.ReasonDateHigh)
                .HasColumnType("datetime")
                .HasColumnName("reason_date_high");
            entity.Property(e => e.ReasonDateLow)
                .HasColumnType("datetime")
                .HasColumnName("reason_date_low");
            entity.Property(e => e.ReasonDescription)
                .HasColumnType("text")
                .HasColumnName("reason_description");
            entity.Property(e => e.ReasonStatus)
                .HasMaxLength(31)
                .HasColumnName("reason_status");
            entity.Property(e => e.Transport)
                .HasMaxLength(31)
                .HasColumnName("transport");
        });

        modelBuilder.Entity<ProcedureOrderRelationship>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("procedure_order_relationships", tb => tb.HasComment("Links ServiceRequests to supporting clinical information"));

            entity.HasIndex(e => e.CreatedAt, "idx_created_at");

            entity.HasIndex(e => e.ProcedureOrderId, "idx_order_id");

            entity.HasIndex(e => new { e.ResourceType, e.ResourceUuid }, "idx_resource");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("timestamp")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasComment("User who created this link")
                .HasColumnType("bigint(20)")
                .HasColumnName("created_by");
            entity.Property(e => e.ProcedureOrderId)
                .HasComment("Links to procedure_order.procedure_order_id")
                .HasColumnType("bigint(20)")
                .HasColumnName("procedure_order_id");
            entity.Property(e => e.Relationship)
                .HasMaxLength(50)
                .HasComment("Type of relationship")
                .HasColumnName("relationship");
            entity.Property(e => e.ResourceType)
                .HasMaxLength(50)
                .HasComment("FHIR resource type (Observation, Condition, etc.)")
                .HasColumnName("resource_type");
            entity.Property(e => e.ResourceUuid)
                .HasMaxLength(16)
                .IsFixedLength()
                .HasComment("UUID of the related resource")
                .HasColumnName("resource_uuid");
        });

        modelBuilder.Entity<ProcedureProvider>(entity =>
        {
            entity.HasKey(e => e.Ppid).HasName("PRIMARY");

            entity.ToTable("procedure_providers");

            entity.HasIndex(e => e.Uuid, "uuid").IsUnique();

            entity.Property(e => e.Ppid)
                .HasColumnType("bigint(20)")
                .HasColumnName("ppid");
            entity.Property(e => e.Active)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("active");
            entity.Property(e => e.DateCreated)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("date_created");
            entity.Property(e => e.Direction)
                .HasMaxLength(1)
                .HasDefaultValueSql("'B'")
                .IsFixedLength()
                .HasComment("Bidirectional or Results-only")
                .HasColumnName("direction");
            entity.Property(e => e.DorP)
                .HasMaxLength(1)
                .HasDefaultValueSql("'D'")
                .IsFixedLength()
                .HasComment("Debugging or Production (MSH-11)");
            entity.Property(e => e.LabDirector)
                .HasColumnType("bigint(20)")
                .HasColumnName("lab_director");
            entity.Property(e => e.LastUpdated)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("last_updated");
            entity.Property(e => e.Login)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("login");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("name");
            entity.Property(e => e.Notes)
                .HasColumnType("text")
                .HasColumnName("notes");
            entity.Property(e => e.Npi)
                .HasMaxLength(15)
                .HasDefaultValueSql("''")
                .HasColumnName("npi");
            entity.Property(e => e.OrdersPath)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("orders_path");
            entity.Property(e => e.Password)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("password");
            entity.Property(e => e.Protocol)
                .HasMaxLength(15)
                .HasDefaultValueSql("'DL'")
                .HasColumnName("protocol");
            entity.Property(e => e.RecvAppId)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasComment("Receiving application ID (MSH-5.1)")
                .HasColumnName("recv_app_id");
            entity.Property(e => e.RecvFacId)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasComment("Receiving facility ID (MSH-6.1)")
                .HasColumnName("recv_fac_id");
            entity.Property(e => e.RemoteHost)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("remote_host");
            entity.Property(e => e.ResultsPath)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("results_path");
            entity.Property(e => e.SendAppId)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasComment("Sending application ID (MSH-3.1)")
                .HasColumnName("send_app_id");
            entity.Property(e => e.SendFacId)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasComment("Sending facility ID (MSH-4.1)")
                .HasColumnName("send_fac_id");
            entity.Property(e => e.Type)
                .HasMaxLength(31)
                .HasColumnName("type");
            entity.Property(e => e.Uuid)
                .HasMaxLength(16)
                .IsFixedLength()
                .HasColumnName("uuid");
        });

        modelBuilder.Entity<ProcedureQuestion>(entity =>
        {
            entity.HasKey(e => new { e.LabId, e.ProcedureCode, e.QuestionCode })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0, 0 });

            entity.ToTable("procedure_questions");

            entity.Property(e => e.LabId)
                .HasComment("references procedure_providers.ppid to identify the lab")
                .HasColumnType("bigint(20)")
                .HasColumnName("lab_id");
            entity.Property(e => e.ProcedureCode)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("references procedure_type.procedure_code to identify this order type")
                .HasColumnName("procedure_code");
            entity.Property(e => e.QuestionCode)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("code identifying this question")
                .HasColumnName("question_code");
            entity.Property(e => e.Activity)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasComment("1 = active, 0 = inactive")
                .HasColumnName("activity");
            entity.Property(e => e.Fldtype)
                .HasMaxLength(1)
                .HasDefaultValueSql("'T'")
                .IsFixedLength()
                .HasComment("Text, Number, Select, Multiselect, Date, Gestational-age")
                .HasColumnName("fldtype");
            entity.Property(e => e.Maxsize)
                .HasComment("maximum length if text input field")
                .HasColumnType("int(11)")
                .HasColumnName("maxsize");
            entity.Property(e => e.Options)
                .HasComment("choices for fldtype S and T")
                .HasColumnType("text")
                .HasColumnName("options");
            entity.Property(e => e.QuestionText)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasComment("descriptive text for question_code")
                .HasColumnName("question_text");
            entity.Property(e => e.Required)
                .HasComment("1 = required, 0 = not")
                .HasColumnName("required");
            entity.Property(e => e.Seq)
                .HasComment("sequence number for ordering")
                .HasColumnType("int(11)")
                .HasColumnName("seq");
            entity.Property(e => e.Tips)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasComment("Additional instructions for answering the question")
                .HasColumnName("tips");
        });

        modelBuilder.Entity<ProcedureReport>(entity =>
        {
            entity.HasKey(e => e.ProcedureReportId).HasName("PRIMARY");

            entity.ToTable("procedure_report");

            entity.HasIndex(e => e.ProcedureOrderId, "procedure_order_id");

            entity.HasIndex(e => e.Uuid, "uuid").IsUnique();

            entity.Property(e => e.ProcedureReportId)
                .HasColumnType("bigint(20)")
                .HasColumnName("procedure_report_id");
            entity.Property(e => e.DateCollected)
                .HasColumnType("datetime")
                .HasColumnName("date_collected");
            entity.Property(e => e.DateCollectedTz)
                .HasMaxLength(5)
                .HasDefaultValueSql("''")
                .HasComment("+-hhmm offset from UTC")
                .HasColumnName("date_collected_tz");
            entity.Property(e => e.DateReport)
                .HasColumnType("datetime")
                .HasColumnName("date_report");
            entity.Property(e => e.DateReportTz)
                .HasMaxLength(5)
                .HasDefaultValueSql("''")
                .HasComment("+-hhmm offset from UTC")
                .HasColumnName("date_report_tz");
            entity.Property(e => e.ProcedureOrderId)
                .HasComment("references procedure_order.procedure_order_id")
                .HasColumnType("bigint(20)")
                .HasColumnName("procedure_order_id");
            entity.Property(e => e.ProcedureOrderSeq)
                .HasDefaultValueSql("'1'")
                .HasComment("references procedure_order_code.procedure_order_seq")
                .HasColumnType("int(11)")
                .HasColumnName("procedure_order_seq");
            entity.Property(e => e.ReportNotes)
                .HasComment("notes from the lab")
                .HasColumnType("text")
                .HasColumnName("report_notes");
            entity.Property(e => e.ReportStatus)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("received,complete,error")
                .HasColumnName("report_status");
            entity.Property(e => e.ReviewStatus)
                .HasMaxLength(31)
                .HasDefaultValueSql("'received'")
                .HasComment("pending review status: received,reviewed")
                .HasColumnName("review_status");
            entity.Property(e => e.Source)
                .HasComment("references users.id, who entered this data")
                .HasColumnType("bigint(20)")
                .HasColumnName("source");
            entity.Property(e => e.SpecimenNum)
                .HasMaxLength(63)
                .HasDefaultValueSql("''")
                .HasColumnName("specimen_num");
            entity.Property(e => e.Uuid)
                .HasMaxLength(16)
                .IsFixedLength()
                .HasColumnName("uuid");
        });

        modelBuilder.Entity<ProcedureResult>(entity =>
        {
            entity.HasKey(e => e.ProcedureResultId).HasName("PRIMARY");

            entity.ToTable("procedure_result");

            entity.HasIndex(e => e.ProcedureReportId, "procedure_report_id");

            entity.HasIndex(e => e.Uuid, "uuid").IsUnique();

            entity.Property(e => e.ProcedureResultId)
                .HasColumnType("bigint(20)")
                .HasColumnName("procedure_result_id");
            entity.Property(e => e.Abnormal)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("no,yes,high,low")
                .HasColumnName("abnormal");
            entity.Property(e => e.Comments)
                .HasComment("comments from the lab")
                .HasColumnType("text")
                .HasColumnName("comments");
            entity.Property(e => e.Date)
                .HasComment("lab-provided date specific to this result")
                .HasColumnType("datetime")
                .HasColumnName("date");
            entity.Property(e => e.DateEnd)
                .HasComment("lab-provided end date specific to this result")
                .HasColumnType("datetime")
                .HasColumnName("date_end");
            entity.Property(e => e.DocumentId)
                .HasComment("references documents.id if this result is a document")
                .HasColumnType("bigint(20)")
                .HasColumnName("document_id");
            entity.Property(e => e.Facility)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasComment("lab-provided testing facility ID")
                .HasColumnName("facility");
            entity.Property(e => e.ProcedureReportId)
                .HasComment("references procedure_report.procedure_report_id")
                .HasColumnType("bigint(20)")
                .HasColumnName("procedure_report_id");
            entity.Property(e => e.Range)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("range");
            entity.Property(e => e.Result)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("result");
            entity.Property(e => e.ResultCode)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("LOINC code, might match a procedure_type.procedure_code")
                .HasColumnName("result_code");
            entity.Property(e => e.ResultDataType)
                .HasMaxLength(1)
                .HasDefaultValueSql("'S'")
                .IsFixedLength()
                .HasComment("N=Numeric, S=String, F=Formatted, E=External, L=Long text as first line of comments")
                .HasColumnName("result_data_type");
            entity.Property(e => e.ResultStatus)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("preliminary, cannot be done, final, corrected, incomplete...etc.")
                .HasColumnName("result_status");
            entity.Property(e => e.ResultText)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasComment("Description of result_code")
                .HasColumnName("result_text");
            entity.Property(e => e.Units)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasColumnName("units");
            entity.Property(e => e.Uuid)
                .HasMaxLength(16)
                .IsFixedLength()
                .HasColumnName("uuid");
        });

        modelBuilder.Entity<ProcedureSpeciman>(entity =>
        {
            entity.HasKey(e => e.ProcedureSpecimenId).HasName("PRIMARY");

            entity.ToTable("procedure_specimen");

            entity.HasIndex(e => e.AccessionIdentifier, "idx_accession");

            entity.HasIndex(e => e.SpecimenIdentifier, "idx_identifier");

            entity.HasIndex(e => new { e.ProcedureOrderId, e.ProcedureOrderSeq }, "idx_order_line");

            entity.HasIndex(e => e.Uuid, "uuid_unique").IsUnique();

            entity.Property(e => e.ProcedureSpecimenId)
                .HasComment("record id")
                .HasColumnType("bigint(20)")
                .HasColumnName("procedure_specimen_id");
            entity.Property(e => e.AccessionIdentifier)
                .HasMaxLength(128)
                .HasComment("lab accession number")
                .HasColumnName("accession_identifier");
            entity.Property(e => e.CollectedDate)
                .HasComment("single instant")
                .HasColumnType("datetime")
                .HasColumnName("collected_date");
            entity.Property(e => e.CollectionDateHigh)
                .HasComment("period end")
                .HasColumnType("datetime")
                .HasColumnName("collection_date_high");
            entity.Property(e => e.CollectionDateLow)
                .HasComment("period start")
                .HasColumnType("datetime")
                .HasColumnName("collection_date_low");
            entity.Property(e => e.CollectionMethod)
                .HasMaxLength(255)
                .HasColumnName("collection_method");
            entity.Property(e => e.CollectionMethodCode)
                .HasMaxLength(64)
                .HasColumnName("collection_method_code");
            entity.Property(e => e.Comments)
                .HasColumnType("text")
                .HasColumnName("comments");
            entity.Property(e => e.ConditionCode)
                .HasMaxLength(32)
                .HasComment("HL7 v2 0493 (e.g., ACT, HEM)")
                .HasColumnName("condition_code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasColumnType("bigint(20)")
                .HasColumnName("created_by");
            entity.Property(e => e.Deleted)
                .HasDefaultValueSql("'0'")
                .HasColumnName("deleted");
            entity.Property(e => e.ProcedureOrderId)
                .HasComment("links to procedure_order.procedure_order_id")
                .HasColumnType("bigint(20)")
                .HasColumnName("procedure_order_id");
            entity.Property(e => e.ProcedureOrderSeq)
                .HasComment("links to procedure_order_code.procedure_order_seq (per test line)")
                .HasColumnType("int(11)")
                .HasColumnName("procedure_order_seq");
            entity.Property(e => e.SpecimenCondition)
                .HasMaxLength(64)
                .HasColumnName("specimen_condition");
            entity.Property(e => e.SpecimenIdentifier)
                .HasMaxLength(128)
                .HasComment("tube/barcode/internal id")
                .HasColumnName("specimen_identifier");
            entity.Property(e => e.SpecimenLocation)
                .HasMaxLength(255)
                .HasColumnName("specimen_location");
            entity.Property(e => e.SpecimenLocationCode)
                .HasMaxLength(64)
                .HasColumnName("specimen_location_code");
            entity.Property(e => e.SpecimenType)
                .HasMaxLength(255)
                .HasComment("display/text")
                .HasColumnName("specimen_type");
            entity.Property(e => e.SpecimenTypeCode)
                .HasMaxLength(64)
                .HasComment("prefer SNOMED CT code")
                .HasColumnName("specimen_type_code");
            entity.Property(e => e.UpdatedAt)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasColumnType("bigint(20)")
                .HasColumnName("updated_by");
            entity.Property(e => e.Uuid)
                .HasMaxLength(16)
                .IsFixedLength()
                .HasComment("FHIR Specimen id")
                .HasColumnName("uuid");
            entity.Property(e => e.VolumeUnit)
                .HasMaxLength(32)
                .HasDefaultValueSql("'mL'")
                .HasColumnName("volume_unit");
            entity.Property(e => e.VolumeValue)
                .HasPrecision(10, 3)
                .HasColumnName("volume_value");
        });

        modelBuilder.Entity<ProcedureType>(entity =>
        {
            entity.HasKey(e => e.ProcedureTypeId).HasName("PRIMARY");

            entity.ToTable("procedure_type");

            entity.HasIndex(e => e.Parent, "parent");

            entity.HasIndex(e => e.ProcedureCode, "ptype_procedure_code");

            entity.Property(e => e.ProcedureTypeId)
                .HasColumnType("bigint(20)")
                .HasColumnName("procedure_type_id");
            entity.Property(e => e.Activity)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasComment("1=active, 0=inactive")
                .HasColumnName("activity");
            entity.Property(e => e.BodySite)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("where to do injection, e.g. arm, buttock")
                .HasColumnName("body_site");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasComment("descriptive text for procedure_code")
                .HasColumnName("description");
            entity.Property(e => e.LabId)
                .HasComment("references procedure_providers.ppid, 0 means default to parent")
                .HasColumnType("bigint(20)")
                .HasColumnName("lab_id");
            entity.Property(e => e.Laterality)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("left, right, ...")
                .HasColumnName("laterality");
            entity.Property(e => e.Name)
                .HasMaxLength(63)
                .HasDefaultValueSql("''")
                .HasComment("name for this category, procedure or result type")
                .HasColumnName("name");
            entity.Property(e => e.Notes)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasComment("additional notes to enhance description")
                .HasColumnName("notes");
            entity.Property(e => e.Parent)
                .HasComment("references procedure_type.procedure_type_id")
                .HasColumnType("bigint(20)")
                .HasColumnName("parent");
            entity.Property(e => e.ProcedureCode)
                .HasMaxLength(64)
                .HasDefaultValueSql("''")
                .HasComment("code identifying this procedure")
                .HasColumnName("procedure_code");
            entity.Property(e => e.ProcedureType1)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("see list proc_type")
                .HasColumnName("procedure_type");
            entity.Property(e => e.ProcedureTypeName)
                .HasMaxLength(64)
                .HasColumnName("procedure_type_name");
            entity.Property(e => e.Range)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasComment("default for procedure_result.range")
                .HasColumnName("range");
            entity.Property(e => e.RelatedCode)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasComment("suggested code(s) for followup services if result is abnormal")
                .HasColumnName("related_code");
            entity.Property(e => e.RouteAdmin)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("oral, injection")
                .HasColumnName("route_admin");
            entity.Property(e => e.Seq)
                .HasComment("sequence number for ordering")
                .HasColumnType("int(11)")
                .HasColumnName("seq");
            entity.Property(e => e.Specimen)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("blood, urine, saliva, etc.")
                .HasColumnName("specimen");
            entity.Property(e => e.StandardCode)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasComment("industry standard code type and code (e.g. CPT4:12345)")
                .HasColumnName("standard_code");
            entity.Property(e => e.Transport)
                .HasMaxLength(31)
                .HasColumnName("transport");
            entity.Property(e => e.Units)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("default for procedure_result.units")
                .HasColumnName("units");
        });

        modelBuilder.Entity<ProductRegistration>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("product_registration");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.AuthById)
                .HasColumnType("int(11)")
                .HasColumnName("auth_by_id");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasColumnName("email");
            entity.Property(e => e.LastAskDate)
                .HasColumnType("datetime")
                .HasColumnName("last_ask_date");
            entity.Property(e => e.LastAskVersion)
                .HasColumnType("tinytext")
                .HasColumnName("last_ask_version");
            entity.Property(e => e.OptOut).HasColumnName("opt_out");
            entity.Property(e => e.Options)
                .HasComment("JSON array of scope options")
                .HasColumnType("text")
                .HasColumnName("options");
            entity.Property(e => e.TelemetryDisabled)
                .HasComment("1 opted out, disabled. NULL ask. 0 use option scopes")
                .HasColumnName("telemetry_disabled");
        });

        modelBuilder.Entity<ProductWarehouse>(entity =>
        {
            entity.HasKey(e => new { e.PwDrugId, e.PwWarehouse })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("product_warehouse");

            entity.Property(e => e.PwDrugId)
                .HasColumnType("int(11)")
                .HasColumnName("pw_drug_id");
            entity.Property(e => e.PwWarehouse)
                .HasMaxLength(31)
                .HasColumnName("pw_warehouse");
            entity.Property(e => e.PwMaxLevel)
                .HasDefaultValueSql("'0'")
                .HasColumnName("pw_max_level");
            entity.Property(e => e.PwMinLevel)
                .HasDefaultValueSql("'0'")
                .HasColumnName("pw_min_level");
        });

        modelBuilder.Entity<QuestionnaireRepository>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("questionnaire_repository");

            entity.HasIndex(e => new { e.Name, e.QuestionnaireId }, "search");

            entity.HasIndex(e => e.Uuid, "uuid").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("bigint(21) unsigned")
                .HasColumnName("id");
            entity.Property(e => e.Active)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("active");
            entity.Property(e => e.Category)
                .HasMaxLength(64)
                .HasComment("Used for grouping and organizing ")
                .HasColumnName("category");
            entity.Property(e => e.Code)
                .HasMaxLength(255)
                .HasColumnName("code");
            entity.Property(e => e.CodeDisplay)
                .HasColumnType("text")
                .HasColumnName("code_display");
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("created_date");
            entity.Property(e => e.Lform).HasColumnName("lform");
            entity.Property(e => e.ModifiedDate)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("modified_date");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.Profile)
                .HasMaxLength(255)
                .HasColumnName("profile");
            entity.Property(e => e.Provider)
                .HasColumnType("int(11) unsigned")
                .HasColumnName("provider");
            entity.Property(e => e.Questionnaire).HasColumnName("questionnaire");
            entity.Property(e => e.QuestionnaireId).HasColumnName("questionnaire_id");
            entity.Property(e => e.SourceUrl)
                .HasColumnType("text")
                .HasColumnName("source_url");
            entity.Property(e => e.Status)
                .HasMaxLength(31)
                .HasColumnName("status");
            entity.Property(e => e.Type)
                .HasMaxLength(63)
                .HasDefaultValueSql("'Questionnaire'")
                .HasColumnName("type");
            entity.Property(e => e.Uuid)
                .HasMaxLength(16)
                .IsFixedLength()
                .HasColumnName("uuid");
            entity.Property(e => e.Version)
                .HasDefaultValueSql("'1'")
                .HasColumnType("int(11)")
                .HasColumnName("version");
        });

        modelBuilder.Entity<QuestionnaireResponse>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("questionnaire_response");

            entity.HasIndex(e => new { e.ResponseId, e.PatientId, e.QuestionnaireId, e.QuestionnaireName }, "response_index");

            entity.HasIndex(e => e.Uuid, "uuid").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("bigint(21)")
                .HasColumnName("id");
            entity.Property(e => e.AuditUserId)
                .HasColumnType("int(11)")
                .HasColumnName("audit_user_id");
            entity.Property(e => e.CreateTime)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("create_time");
            entity.Property(e => e.CreatorUserId)
                .HasComment("user id if answers are provider")
                .HasColumnType("int(11)")
                .HasColumnName("creator_user_id");
            entity.Property(e => e.Encounter)
                .HasComment("May or may not be associated with an encounter")
                .HasColumnType("int(11)")
                .HasColumnName("encounter");
            entity.Property(e => e.Error)
                .HasComment("Standard error for the T-Score")
                .HasColumnName("error");
            entity.Property(e => e.FormResponse)
                .HasComment("lform answers array json")
                .HasColumnName("form_response");
            entity.Property(e => e.FormScore)
                .HasComment("Arithmetic scoring of questionnaires")
                .HasColumnType("int(11)")
                .HasColumnName("form_score");
            entity.Property(e => e.LastUpdated)
                .HasColumnType("datetime")
                .HasColumnName("last_updated");
            entity.Property(e => e.PatientId)
                .HasColumnType("int(11)")
                .HasColumnName("patient_id");
            entity.Property(e => e.Questionnaire)
                .HasComment("the subject questionnaire json")
                .HasColumnName("questionnaire");
            entity.Property(e => e.QuestionnaireForeignId)
                .HasComment("questionnaire_repository id for subject questionnaire")
                .HasColumnType("bigint(21)")
                .HasColumnName("questionnaire_foreign_id");
            entity.Property(e => e.QuestionnaireId)
                .HasComment("Id for questionnaire content. String version of UUID")
                .HasColumnName("questionnaire_id");
            entity.Property(e => e.QuestionnaireName).HasColumnName("questionnaire_name");
            entity.Property(e => e.QuestionnaireResponse1)
                .HasComment("questionnaire response json")
                .HasColumnName("questionnaire_response");
            entity.Property(e => e.ResponseId)
                .HasComment("A globally unique id for answer set. String version of UUID")
                .HasColumnName("response_id");
            entity.Property(e => e.Status)
                .HasMaxLength(63)
                .HasComment("form current status. completed,active,incomplete")
                .HasColumnName("status");
            entity.Property(e => e.Tscore)
                .HasComment("T-Score")
                .HasColumnName("tscore");
            entity.Property(e => e.Uuid)
                .HasMaxLength(16)
                .IsFixedLength()
                .HasColumnName("uuid");
            entity.Property(e => e.Version)
                .HasDefaultValueSql("'1'")
                .HasColumnType("int(11)")
                .HasColumnName("version");
        });

        modelBuilder.Entity<RecentPatient>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PRIMARY");

            entity.ToTable("recent_patients");

            entity.Property(e => e.UserId)
                .HasMaxLength(40)
                .HasColumnName("user_id");
            entity.Property(e => e.Patients)
                .HasColumnType("text")
                .HasColumnName("patients");
        });

        modelBuilder.Entity<Registry>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("registry");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.AcoSpec)
                .HasMaxLength(63)
                .HasDefaultValueSql("'encounters|notes'")
                .HasColumnName("aco_spec");
            entity.Property(e => e.Category)
                .HasMaxLength(255)
                .HasColumnName("category");
            entity.Property(e => e.Date)
                .HasColumnType("datetime")
                .HasColumnName("date");
            entity.Property(e => e.Directory)
                .HasMaxLength(255)
                .HasColumnName("directory");
            entity.Property(e => e.FormForeignId)
                .HasComment("An id to a form repository. Primarily questionnaire_repository.")
                .HasColumnType("bigint(21)")
                .HasColumnName("form_foreign_id");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.Nickname)
                .HasMaxLength(255)
                .HasColumnName("nickname");
            entity.Property(e => e.PatientEncounter)
                .HasDefaultValueSql("'1'")
                .HasColumnType("tinyint(4)")
                .HasColumnName("patient_encounter");
            entity.Property(e => e.Priority)
                .HasDefaultValueSql("'0'")
                .HasColumnType("int(11)")
                .HasColumnName("priority");
            entity.Property(e => e.SqlRun)
                .HasColumnType("tinyint(4)")
                .HasColumnName("sql_run");
            entity.Property(e => e.State)
                .HasColumnType("tinyint(4)")
                .HasColumnName("state");
            entity.Property(e => e.TherapyGroupEncounter)
                .HasColumnType("tinyint(4)")
                .HasColumnName("therapy_group_encounter");
            entity.Property(e => e.Unpackaged)
                .HasColumnType("tinyint(4)")
                .HasColumnName("unpackaged");
        });

        modelBuilder.Entity<ReportItemized>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("report_itemized");

            entity.HasIndex(e => new { e.ReportId, e.ItemizedTestId, e.NumeratorLabel, e.Pass }, "report_id");

            entity.Property(e => e.ItemDetails)
                .HasComment("JSON with specific sub item results for a clinical rule")
                .HasColumnType("text")
                .HasColumnName("item_details");
            entity.Property(e => e.ItemizedTestId)
                .HasColumnType("smallint(6)")
                .HasColumnName("itemized_test_id");
            entity.Property(e => e.NumeratorLabel)
                .HasMaxLength(25)
                .HasDefaultValueSql("''")
                .HasComment("Only used in special cases")
                .HasColumnName("numerator_label");
            entity.Property(e => e.Pass)
                .HasComment("0 is fail, 1 is pass, 2 is excluded")
                .HasColumnName("pass");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.ReportId)
                .HasColumnType("bigint(20)")
                .HasColumnName("report_id");
            entity.Property(e => e.RuleId)
                .HasMaxLength(31)
                .HasComment("fk to clinical_rules.rule_id")
                .HasColumnName("rule_id");
        });

        modelBuilder.Entity<ReportResult>(entity =>
        {
            entity.HasKey(e => new { e.ReportId, e.FieldId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("report_results");

            entity.Property(e => e.ReportId)
                .HasColumnType("bigint(20)")
                .HasColumnName("report_id");
            entity.Property(e => e.FieldId)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasColumnName("field_id");
            entity.Property(e => e.FieldValue)
                .HasColumnType("text")
                .HasColumnName("field_value");
        });

        modelBuilder.Entity<RuleAction>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("rule_action");

            entity.HasIndex(e => e.Id, "id");

            entity.Property(e => e.Category)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("Maps to the category item in the rule_action_item table")
                .HasColumnName("category");
            entity.Property(e => e.GroupId)
                .HasDefaultValueSql("'1'")
                .HasComment("Contains group id to identify collection of targets in a rule")
                .HasColumnType("bigint(20)")
                .HasColumnName("group_id");
            entity.Property(e => e.Id)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("Maps to the id column in the clinical_rules table")
                .HasColumnName("id");
            entity.Property(e => e.Item)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("Maps to the item column in the rule_action_item table")
                .HasColumnName("item");
        });

        modelBuilder.Entity<RuleActionItem>(entity =>
        {
            entity.HasKey(e => new { e.Category, e.Item })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("rule_action_item");

            entity.Property(e => e.Category)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("Maps to list_options list rule_action_category")
                .HasColumnName("category");
            entity.Property(e => e.Item)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("Maps to list_options list rule_action")
                .HasColumnName("item");
            entity.Property(e => e.ClinRemLink)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasComment("Custom html link in clinical reminder widget")
                .HasColumnName("clin_rem_link");
            entity.Property(e => e.CustomFlag)
                .HasComment("1 indexed to rule_patient_data, 0 indexed within main schema")
                .HasColumnName("custom_flag");
            entity.Property(e => e.ReminderMessage)
                .HasComment("Custom message in patient reminder")
                .HasColumnType("text")
                .HasColumnName("reminder_message");
        });

        modelBuilder.Entity<RuleFilter>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("rule_filter");

            entity.HasIndex(e => e.Id, "id");

            entity.Property(e => e.Id)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("Maps to the id column in the clinical_rules table")
                .HasColumnName("id");
            entity.Property(e => e.IncludeFlag)
                .HasComment("0 is exclude and 1 is include")
                .HasColumnName("include_flag");
            entity.Property(e => e.Method)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("Maps to list_options list rule_filters")
                .HasColumnName("method");
            entity.Property(e => e.MethodDetail)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("Maps to list_options lists rule__intervals")
                .HasColumnName("method_detail");
            entity.Property(e => e.RequiredFlag)
                .HasComment("0 is optional and 1 is required")
                .HasColumnName("required_flag");
            entity.Property(e => e.Value)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("value");
        });

        modelBuilder.Entity<RulePatientDatum>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("rule_patient_data");

            entity.HasIndex(e => new { e.Category, e.Item }, "category");

            entity.HasIndex(e => e.Pid, "pid");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Category)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("Maps to the category item in the rule_action_item table")
                .HasColumnName("category");
            entity.Property(e => e.Complete)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("Maps to list_options list yesno")
                .HasColumnName("complete");
            entity.Property(e => e.Date)
                .HasColumnType("datetime")
                .HasColumnName("date");
            entity.Property(e => e.Item)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("Maps to the item column in the rule_action_item table")
                .HasColumnName("item");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.Result)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("result");
        });

        modelBuilder.Entity<RuleReminder>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("rule_reminder");

            entity.HasIndex(e => e.Id, "id");

            entity.Property(e => e.Id)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("Maps to the id column in the clinical_rules table")
                .HasColumnName("id");
            entity.Property(e => e.Method)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("Maps to list_options list rule_reminder_methods")
                .HasColumnName("method");
            entity.Property(e => e.MethodDetail)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("Maps to list_options list rule_reminder_intervals")
                .HasColumnName("method_detail");
            entity.Property(e => e.Value)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("value");
        });

        modelBuilder.Entity<RuleTarget>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("rule_target");

            entity.HasIndex(e => e.Id, "id");

            entity.Property(e => e.GroupId)
                .HasDefaultValueSql("'1'")
                .HasComment("Contains group id to identify collection of targets in a rule")
                .HasColumnType("bigint(20)")
                .HasColumnName("group_id");
            entity.Property(e => e.Id)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("Maps to the id column in the clinical_rules table")
                .HasColumnName("id");
            entity.Property(e => e.IncludeFlag)
                .HasComment("0 is exclude and 1 is include")
                .HasColumnName("include_flag");
            entity.Property(e => e.Interval)
                .HasComment("Only used in interval entries")
                .HasColumnType("bigint(20)")
                .HasColumnName("interval");
            entity.Property(e => e.Method)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasComment("Maps to list_options list rule_targets")
                .HasColumnName("method");
            entity.Property(e => e.RequiredFlag)
                .HasComment("0 is required and 1 is optional")
                .HasColumnName("required_flag");
            entity.Property(e => e.Value)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasComment("Data is dependent on the method")
                .HasColumnName("value");
        });

        modelBuilder.Entity<Sequence>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("sequences");

            entity.Property(e => e.Id)
                .HasColumnType("int(11) unsigned")
                .HasColumnName("id");
        });

        modelBuilder.Entity<SessionTracker>(entity =>
        {
            entity.HasKey(e => e.Uuid).HasName("PRIMARY");

            entity.ToTable("session_tracker");

            entity.Property(e => e.Uuid)
                .HasMaxLength(16)
                .HasDefaultValueSql("x'00000000000000000000000000000000'")
                .IsFixedLength()
                .HasColumnName("uuid");
            entity.Property(e => e.Created)
                .HasColumnType("timestamp")
                .HasColumnName("created");
            entity.Property(e => e.LastUpdated)
                .HasColumnType("timestamp")
                .HasColumnName("last_updated");
            entity.Property(e => e.NumberScripts)
                .HasDefaultValueSql("'1'")
                .HasColumnType("bigint(20)")
                .HasColumnName("number_scripts");
        });

        modelBuilder.Entity<SharedAttribute>(entity =>
        {
            entity.HasKey(e => new { e.Pid, e.Encounter, e.FieldId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0, 0 });

            entity.ToTable("shared_attributes");

            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.Encounter)
                .HasComment("0 if patient attribute, else encounter attribute")
                .HasColumnType("bigint(20)")
                .HasColumnName("encounter");
            entity.Property(e => e.FieldId)
                .HasMaxLength(31)
                .HasComment("references layout_options.field_id")
                .HasColumnName("field_id");
            entity.Property(e => e.FieldValue)
                .HasColumnType("text")
                .HasColumnName("field_value");
            entity.Property(e => e.LastUpdate)
                .HasComment("time of last update")
                .HasColumnType("datetime")
                .HasColumnName("last_update");
            entity.Property(e => e.UserId)
                .HasComment("user who last updated")
                .HasColumnType("bigint(20)")
                .HasColumnName("user_id");
        });

        modelBuilder.Entity<StandardizedTablesTrack>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("standardized_tables_track");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.FileChecksum)
                .HasMaxLength(32)
                .HasDefaultValueSql("''")
                .HasColumnName("file_checksum");
            entity.Property(e => e.ImportedDate)
                .HasColumnType("datetime")
                .HasColumnName("imported_date");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasComment("name of standardized tables such as RXNORM")
                .HasColumnName("name");
            entity.Property(e => e.RevisionDate)
                .HasComment("revision of standardized tables that were imported")
                .HasColumnType("datetime")
                .HasColumnName("revision_date");
            entity.Property(e => e.RevisionVersion)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasComment("revision of standardized tables that were imported")
                .HasColumnName("revision_version");
        });

        modelBuilder.Entity<SupportedExternalDataload>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("supported_external_dataloads");

            entity.HasIndex(e => e.LoadId, "load_id").IsUnique();

            entity.Property(e => e.LoadChecksum)
                .HasMaxLength(32)
                .HasDefaultValueSql("''")
                .HasColumnName("load_checksum");
            entity.Property(e => e.LoadFilename)
                .HasMaxLength(256)
                .HasDefaultValueSql("''")
                .HasColumnName("load_filename");
            entity.Property(e => e.LoadId)
                .ValueGeneratedOnAdd()
                .HasColumnType("bigint(20) unsigned")
                .HasColumnName("load_id");
            entity.Property(e => e.LoadReleaseDate).HasColumnName("load_release_date");
            entity.Property(e => e.LoadSource)
                .HasMaxLength(24)
                .HasDefaultValueSql("'CMS'")
                .HasColumnName("load_source");
            entity.Property(e => e.LoadType)
                .HasMaxLength(24)
                .HasDefaultValueSql("''")
                .HasColumnName("load_type");
        });

        modelBuilder.Entity<SyndromicSurveillance>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("syndromic_surveillance");

            entity.HasIndex(e => e.ListsId, "lists_id");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Filename)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("filename");
            entity.Property(e => e.ListsId)
                .HasColumnType("bigint(20)")
                .HasColumnName("lists_id");
            entity.Property(e => e.SubmissionDate)
                .HasColumnType("datetime")
                .HasColumnName("submission_date");
        });

        modelBuilder.Entity<TemplateUser>(entity =>
        {
            entity.HasKey(e => e.TuId).HasName("PRIMARY");

            entity.ToTable("template_users");

            entity.HasIndex(e => new { e.TuUserId, e.TuTemplateId }, "templateuser").IsUnique();

            entity.Property(e => e.TuId)
                .HasColumnType("int(11)")
                .HasColumnName("tu_id");
            entity.Property(e => e.TuFacilityId)
                .HasColumnType("int(11)")
                .HasColumnName("tu_facility_id");
            entity.Property(e => e.TuTemplateId)
                .HasColumnType("int(11)")
                .HasColumnName("tu_template_id");
            entity.Property(e => e.TuTemplateOrder)
                .HasColumnType("int(11)")
                .HasColumnName("tu_template_order");
            entity.Property(e => e.TuUserId)
                .HasColumnType("int(11)")
                .HasColumnName("tu_user_id");
        });

        modelBuilder.Entity<TherapyGroup>(entity =>
        {
            entity.HasKey(e => e.GroupId).HasName("PRIMARY");

            entity.ToTable("therapy_groups");

            entity.Property(e => e.GroupId)
                .HasColumnType("int(11)")
                .HasColumnName("group_id");
            entity.Property(e => e.GroupEndDate).HasColumnName("group_end_date");
            entity.Property(e => e.GroupGuestCounselors)
                .HasMaxLength(255)
                .HasColumnName("group_guest_counselors");
            entity.Property(e => e.GroupName)
                .HasMaxLength(255)
                .HasColumnName("group_name");
            entity.Property(e => e.GroupNotes)
                .HasColumnType("text")
                .HasColumnName("group_notes");
            entity.Property(e => e.GroupParticipation)
                .HasColumnType("tinyint(4)")
                .HasColumnName("group_participation");
            entity.Property(e => e.GroupStartDate).HasColumnName("group_start_date");
            entity.Property(e => e.GroupStatus)
                .HasColumnType("int(11)")
                .HasColumnName("group_status");
            entity.Property(e => e.GroupType)
                .HasColumnType("tinyint(4)")
                .HasColumnName("group_type");
        });

        modelBuilder.Entity<TherapyGroupsCounselor>(entity =>
        {
            entity.HasKey(e => new { e.GroupId, e.UserId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("therapy_groups_counselors");

            entity.Property(e => e.GroupId)
                .HasColumnType("int(11)")
                .HasColumnName("group_id");
            entity.Property(e => e.UserId)
                .HasColumnType("int(11)")
                .HasColumnName("user_id");
        });

        modelBuilder.Entity<TherapyGroupsParticipant>(entity =>
        {
            entity.HasKey(e => new { e.GroupId, e.Pid })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("therapy_groups_participants");

            entity.Property(e => e.GroupId)
                .HasColumnType("int(11)")
                .HasColumnName("group_id");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.GroupPatientComment)
                .HasColumnType("text")
                .HasColumnName("group_patient_comment");
            entity.Property(e => e.GroupPatientEnd).HasColumnName("group_patient_end");
            entity.Property(e => e.GroupPatientStart).HasColumnName("group_patient_start");
            entity.Property(e => e.GroupPatientStatus)
                .HasColumnType("int(11)")
                .HasColumnName("group_patient_status");
        });

        modelBuilder.Entity<TherapyGroupsParticipantAttendance>(entity =>
        {
            entity.HasKey(e => new { e.FormId, e.Pid })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("therapy_groups_participant_attendance");

            entity.Property(e => e.FormId)
                .HasColumnType("int(11)")
                .HasColumnName("form_id");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.MeetingPatientComment)
                .HasColumnType("text")
                .HasColumnName("meeting_patient_comment");
            entity.Property(e => e.MeetingPatientStatus)
                .HasMaxLength(15)
                .HasColumnName("meeting_patient_status");
        });

        modelBuilder.Entity<TrackEvent>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("track_events", tb => tb.HasComment("Telemetry Event Data"));

            entity.HasIndex(e => new { e.EventLabel, e.EventUrl, e.EventTarget }, "unique_event_label_target")
                .IsUnique()
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 255, 255 });

            entity.Property(e => e.Id)
                .HasColumnType("int(11) unsigned")
                .HasColumnName("id");
            entity.Property(e => e.EventLabel).HasColumnName("event_label");
            entity.Property(e => e.EventTarget)
                .HasColumnType("text")
                .HasColumnName("event_target");
            entity.Property(e => e.EventType)
                .HasColumnType("text")
                .HasColumnName("event_type");
            entity.Property(e => e.EventUrl)
                .HasColumnType("text")
                .HasColumnName("event_url");
            entity.Property(e => e.FirstEvent)
                .HasColumnType("datetime")
                .HasColumnName("first_event");
            entity.Property(e => e.LabelCount)
                .HasDefaultValueSql("'1'")
                .HasColumnType("int(10) unsigned")
                .HasColumnName("label_count");
            entity.Property(e => e.LastEvent)
                .HasColumnType("datetime")
                .HasColumnName("last_event");
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("transactions");

            entity.HasIndex(e => e.Pid, "pid");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Authorized)
                .HasColumnType("tinyint(4)")
                .HasColumnName("authorized");
            entity.Property(e => e.Date)
                .HasColumnType("datetime")
                .HasColumnName("date");
            entity.Property(e => e.Groupname)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("groupname");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("title");
            entity.Property(e => e.User)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("user");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("users");

            entity.HasIndex(e => e.AbookType, "abook_type");

            entity.HasIndex(e => e.GoogleSigninEmail, "google_signin_email").IsUnique();

            entity.HasIndex(e => e.Uuid, "uuid").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.AbookType)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasColumnName("abook_type");
            entity.Property(e => e.Active)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("active");
            entity.Property(e => e.Assistant)
                .HasMaxLength(255)
                .HasColumnName("assistant");
            entity.Property(e => e.Authorized)
                .HasColumnType("tinyint(4)")
                .HasColumnName("authorized");
            entity.Property(e => e.BillingFacility)
                .HasColumnType("text")
                .HasColumnName("billing_facility");
            entity.Property(e => e.BillingFacilityId)
                .HasColumnType("int(11)")
                .HasColumnName("billing_facility_id");
            entity.Property(e => e.Billname)
                .HasMaxLength(255)
                .HasColumnName("billname");
            entity.Property(e => e.CalUi)
                .HasDefaultValueSql("'1'")
                .HasColumnType("tinyint(4)")
                .HasColumnName("cal_ui");
            entity.Property(e => e.Calendar)
                .HasComment("1 = appears in calendar")
                .HasColumnName("calendar");
            entity.Property(e => e.City)
                .HasMaxLength(30)
                .HasColumnName("city");
            entity.Property(e => e.City2)
                .HasMaxLength(30)
                .HasColumnName("city2");
            entity.Property(e => e.CountryCode)
                .HasMaxLength(255)
                .HasComment("ISO 3166-1 alpha-2 country code for address but can take entire country name for now")
                .HasColumnName("country_code");
            entity.Property(e => e.CountryCode2)
                .HasMaxLength(255)
                .HasComment("ISO 3166-1 alpha-2 country code for address but can take entire country name for now")
                .HasColumnName("country_code2");
            entity.Property(e => e.Cpoe).HasColumnName("cpoe");
            entity.Property(e => e.DateCreated)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("date_created");
            entity.Property(e => e.DefaultWarehouse)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasColumnName("default_warehouse");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasColumnName("email");
            entity.Property(e => e.EmailDirect)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("email_direct");
            entity.Property(e => e.Facility)
                .HasMaxLength(255)
                .HasColumnName("facility");
            entity.Property(e => e.FacilityId)
                .HasColumnType("int(11)")
                .HasColumnName("facility_id");
            entity.Property(e => e.Fax)
                .HasMaxLength(30)
                .HasColumnName("fax");
            entity.Property(e => e.Federaldrugid)
                .HasMaxLength(255)
                .HasColumnName("federaldrugid");
            entity.Property(e => e.Federaltaxid)
                .HasMaxLength(255)
                .HasColumnName("federaltaxid");
            entity.Property(e => e.Fname)
                .HasMaxLength(255)
                .HasColumnName("fname");
            entity.Property(e => e.GoogleSigninEmail).HasColumnName("google_signin_email");
            entity.Property(e => e.Info).HasColumnName("info");
            entity.Property(e => e.Irnpool)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasColumnName("irnpool");
            entity.Property(e => e.LastUpdated)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("last_updated");
            entity.Property(e => e.Lname)
                .HasMaxLength(255)
                .HasColumnName("lname");
            entity.Property(e => e.MainMenuRole)
                .HasMaxLength(50)
                .HasDefaultValueSql("'standard'")
                .HasColumnName("main_menu_role");
            entity.Property(e => e.Mname)
                .HasMaxLength(255)
                .HasColumnName("mname");
            entity.Property(e => e.NewcropUserRole)
                .HasMaxLength(30)
                .HasColumnName("newcrop_user_role");
            entity.Property(e => e.Notes)
                .HasColumnType("text")
                .HasColumnName("notes");
            entity.Property(e => e.Npi)
                .HasMaxLength(15)
                .HasColumnName("npi");
            entity.Property(e => e.Organization)
                .HasMaxLength(255)
                .HasColumnName("organization");
            entity.Property(e => e.Password).HasColumnName("password");
            entity.Property(e => e.PatientMenuRole)
                .HasMaxLength(50)
                .HasDefaultValueSql("'standard'")
                .HasColumnName("patient_menu_role");
            entity.Property(e => e.Phone)
                .HasMaxLength(30)
                .HasColumnName("phone");
            entity.Property(e => e.Phonecell)
                .HasMaxLength(30)
                .HasColumnName("phonecell");
            entity.Property(e => e.Phonew1)
                .HasMaxLength(30)
                .HasColumnName("phonew1");
            entity.Property(e => e.Phonew2)
                .HasMaxLength(30)
                .HasColumnName("phonew2");
            entity.Property(e => e.PhysicianType)
                .HasMaxLength(50)
                .HasColumnName("physician_type");
            entity.Property(e => e.PortalUser).HasColumnName("portal_user");
            entity.Property(e => e.SeeAuth)
                .HasDefaultValueSql("'1'")
                .HasColumnType("int(11)")
                .HasColumnName("see_auth");
            entity.Property(e => e.Source)
                .HasColumnType("tinyint(4)")
                .HasColumnName("source");
            entity.Property(e => e.Specialty)
                .HasMaxLength(255)
                .HasColumnName("specialty");
            entity.Property(e => e.State)
                .HasMaxLength(30)
                .HasColumnName("state");
            entity.Property(e => e.State2)
                .HasMaxLength(30)
                .HasColumnName("state2");
            entity.Property(e => e.StateLicenseNumber)
                .HasMaxLength(25)
                .HasColumnName("state_license_number");
            entity.Property(e => e.Street)
                .HasMaxLength(60)
                .HasColumnName("street");
            entity.Property(e => e.Street2)
                .HasMaxLength(60)
                .HasColumnName("street2");
            entity.Property(e => e.Streetb)
                .HasMaxLength(60)
                .HasColumnName("streetb");
            entity.Property(e => e.Streetb2)
                .HasMaxLength(60)
                .HasColumnName("streetb2");
            entity.Property(e => e.Suffix)
                .HasMaxLength(255)
                .HasColumnName("suffix");
            entity.Property(e => e.SupervisorId)
                .HasColumnType("int(11)")
                .HasColumnName("supervisor_id");
            entity.Property(e => e.Taxonomy)
                .HasMaxLength(30)
                .HasDefaultValueSql("'207Q00000X'")
                .HasColumnName("taxonomy");
            entity.Property(e => e.Title)
                .HasMaxLength(30)
                .HasColumnName("title");
            entity.Property(e => e.Upin)
                .HasMaxLength(255)
                .HasColumnName("upin");
            entity.Property(e => e.Url)
                .HasMaxLength(255)
                .HasColumnName("url");
            entity.Property(e => e.Username)
                .HasMaxLength(255)
                .HasColumnName("username");
            entity.Property(e => e.Uuid)
                .HasMaxLength(16)
                .IsFixedLength()
                .HasColumnName("uuid");
            entity.Property(e => e.Valedictory)
                .HasMaxLength(255)
                .HasColumnName("valedictory");
            entity.Property(e => e.WenoProvId)
                .HasMaxLength(15)
                .HasColumnName("weno_prov_id");
            entity.Property(e => e.Zip)
                .HasMaxLength(20)
                .HasColumnName("zip");
            entity.Property(e => e.Zip2)
                .HasMaxLength(20)
                .HasColumnName("zip2");
        });

        modelBuilder.Entity<UserSetting>(entity =>
        {
            entity.HasKey(e => new { e.SettingUser, e.SettingLabel })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("user_settings");

            entity.Property(e => e.SettingUser)
                .HasColumnType("bigint(20)")
                .HasColumnName("setting_user");
            entity.Property(e => e.SettingLabel)
                .HasMaxLength(100)
                .HasColumnName("setting_label");
            entity.Property(e => e.SettingValue)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("setting_value");
        });

        modelBuilder.Entity<UsersFacility>(entity =>
        {
            entity.HasKey(e => new { e.Tablename, e.TableId, e.FacilityId, e.WarehouseId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0, 0, 0 });

            entity.ToTable("users_facility", tb => tb.HasComment("joins users or patient_data to facility table"));

            entity.Property(e => e.Tablename)
                .HasMaxLength(64)
                .HasColumnName("tablename");
            entity.Property(e => e.TableId)
                .HasColumnType("int(11)")
                .HasColumnName("table_id");
            entity.Property(e => e.FacilityId)
                .HasColumnType("int(11)")
                .HasColumnName("facility_id");
            entity.Property(e => e.WarehouseId)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasColumnName("warehouse_id");
        });

        modelBuilder.Entity<UsersSecure>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("users_secure");

            entity.HasIndex(e => new { e.Id, e.Username }, "USERNAME_ID").IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.AutoBlockEmailed)
                .HasDefaultValueSql("'0'")
                .HasColumnType("tinyint(4)")
                .HasColumnName("auto_block_emailed");
            entity.Property(e => e.LastChallengeResponse)
                .HasColumnType("datetime")
                .HasColumnName("last_challenge_response");
            entity.Property(e => e.LastLoginFail)
                .HasColumnType("datetime")
                .HasColumnName("last_login_fail");
            entity.Property(e => e.LastUpdate)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("timestamp")
                .HasColumnName("last_update");
            entity.Property(e => e.LastUpdatePassword)
                .HasColumnType("datetime")
                .HasColumnName("last_update_password");
            entity.Property(e => e.LoginFailCounter)
                .HasDefaultValueSql("'0'")
                .HasColumnType("int(11)")
                .HasColumnName("login_fail_counter");
            entity.Property(e => e.LoginWorkArea)
                .HasColumnType("text")
                .HasColumnName("login_work_area");
            entity.Property(e => e.MfaFailCounter)
                .HasDefaultValueSql("'0'")
                .HasComment("Per-user MFA challenge failure counter. Independent of login_fail_counter so an in-progress MFA brute force does not get zeroed out by the password verify success that happens on every attempt.")
                .HasColumnType("bigint(20)")
                .HasColumnName("mfa_fail_counter");
            entity.Property(e => e.MfaLastFail)
                .HasComment("Timestamp of the last MFA challenge failure. Used for time-based counter reset.")
                .HasColumnType("datetime")
                .HasColumnName("mfa_last_fail");
            entity.Property(e => e.Password)
                .HasMaxLength(255)
                .HasColumnName("password");
            entity.Property(e => e.PasswordHistory1)
                .HasMaxLength(255)
                .HasColumnName("password_history1");
            entity.Property(e => e.PasswordHistory2)
                .HasMaxLength(255)
                .HasColumnName("password_history2");
            entity.Property(e => e.PasswordHistory3)
                .HasMaxLength(255)
                .HasColumnName("password_history3");
            entity.Property(e => e.PasswordHistory4)
                .HasMaxLength(255)
                .HasColumnName("password_history4");
            entity.Property(e => e.TotalLoginFailCounter)
                .HasDefaultValueSql("'0'")
                .HasColumnType("bigint(20)")
                .HasColumnName("total_login_fail_counter");
            entity.Property(e => e.Username).HasColumnName("username");
        });

        modelBuilder.Entity<UuidMapping>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("uuid_mapping");

            entity.HasIndex(e => e.Resource, "resource");

            entity.HasIndex(e => e.Table, "table");

            entity.HasIndex(e => e.TargetUuid, "target_uuid");

            entity.HasIndex(e => e.Uuid, "uuid");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Created)
                .HasColumnType("timestamp")
                .HasColumnName("created");
            entity.Property(e => e.Resource)
                .HasDefaultValueSql("''")
                .HasColumnName("resource");
            entity.Property(e => e.ResourcePath)
                .HasMaxLength(255)
                .HasColumnName("resource_path");
            entity.Property(e => e.Table)
                .HasDefaultValueSql("''")
                .HasColumnName("table");
            entity.Property(e => e.TargetUuid)
                .HasMaxLength(16)
                .HasDefaultValueSql("x'00000000000000000000000000000000'")
                .IsFixedLength()
                .HasColumnName("target_uuid");
            entity.Property(e => e.Uuid)
                .HasMaxLength(16)
                .HasDefaultValueSql("x'00000000000000000000000000000000'")
                .IsFixedLength()
                .HasColumnName("uuid");
        });

        modelBuilder.Entity<UuidRegistry>(entity =>
        {
            entity.HasKey(e => e.Uuid).HasName("PRIMARY");

            entity.ToTable("uuid_registry");

            entity.Property(e => e.Uuid)
                .HasMaxLength(16)
                .HasDefaultValueSql("x'00000000000000000000000000000000'")
                .IsFixedLength()
                .HasColumnName("uuid");
            entity.Property(e => e.Couchdb)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("couchdb");
            entity.Property(e => e.Created)
                .HasColumnType("timestamp")
                .HasColumnName("created");
            entity.Property(e => e.DocumentDrive)
                .HasColumnType("tinyint(4)")
                .HasColumnName("document_drive");
            entity.Property(e => e.Mapped)
                .HasColumnType("tinyint(4)")
                .HasColumnName("mapped");
            entity.Property(e => e.TableId)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("table_id");
            entity.Property(e => e.TableName)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("table_name");
            entity.Property(e => e.TableVertical)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("table_vertical");
        });

        modelBuilder.Entity<Valueset>(entity =>
        {
            entity.HasKey(e => new { e.NqfCode, e.Code, e.Valueset1 })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0, 0 });

            entity.ToTable("valueset");

            entity.Property(e => e.NqfCode)
                .HasDefaultValueSql("''")
                .HasColumnName("nqf_code");
            entity.Property(e => e.Code)
                .HasDefaultValueSql("''")
                .HasColumnName("code");
            entity.Property(e => e.Valueset1)
                .HasDefaultValueSql("''")
                .HasColumnName("valueset");
            entity.Property(e => e.CodeSystem)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("code_system");
            entity.Property(e => e.CodeType)
                .HasMaxLength(255)
                .HasColumnName("code_type");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
            entity.Property(e => e.ValuesetName)
                .HasMaxLength(500)
                .HasColumnName("valueset_name");
        });

        modelBuilder.Entity<ValuesetOid>(entity =>
        {
            entity.HasKey(e => new { e.NqfCode, e.Code, e.Valueset })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0, 0 });

            entity.ToTable("valueset_oid");

            entity.Property(e => e.NqfCode)
                .HasDefaultValueSql("''")
                .HasColumnName("nqf_code");
            entity.Property(e => e.Code)
                .HasDefaultValueSql("''")
                .HasColumnName("code");
            entity.Property(e => e.Valueset)
                .HasDefaultValueSql("''")
                .HasColumnName("valueset");
            entity.Property(e => e.CodeSystem)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("code_system");
            entity.Property(e => e.CodeType)
                .HasMaxLength(255)
                .HasColumnName("code_type");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
            entity.Property(e => e.ValuesetName)
                .HasMaxLength(500)
                .HasColumnName("valueset_name");
        });

        modelBuilder.Entity<VerifyEmail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("verify_email");

            entity.HasIndex(e => e.Email, "email").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Active)
                .HasDefaultValueSql("'1'")
                .HasColumnType("tinyint(4)")
                .HasColumnName("active");
            entity.Property(e => e.Dob).HasColumnName("dob");
            entity.Property(e => e.Email).HasColumnName("email");
            entity.Property(e => e.Fname)
                .HasMaxLength(255)
                .HasColumnName("fname");
            entity.Property(e => e.Language)
                .HasMaxLength(100)
                .HasColumnName("language");
            entity.Property(e => e.Lname)
                .HasMaxLength(255)
                .HasColumnName("lname");
            entity.Property(e => e.Mname)
                .HasMaxLength(255)
                .HasColumnName("mname");
            entity.Property(e => e.PidHolder)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid_holder");
            entity.Property(e => e.TokenOnetime)
                .HasMaxLength(255)
                .HasColumnName("token_onetime");
        });

        modelBuilder.Entity<Version>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("version");

            entity.Property(e => e.VAcl)
                .HasColumnType("int(11)")
                .HasColumnName("v_acl");
            entity.Property(e => e.VDatabase)
                .HasColumnType("int(11)")
                .HasColumnName("v_database");
            entity.Property(e => e.VMajor)
                .HasColumnType("int(11)")
                .HasColumnName("v_major");
            entity.Property(e => e.VMinor)
                .HasColumnType("int(11)")
                .HasColumnName("v_minor");
            entity.Property(e => e.VPatch)
                .HasColumnType("int(11)")
                .HasColumnName("v_patch");
            entity.Property(e => e.VRealpatch)
                .HasColumnType("int(11)")
                .HasColumnName("v_realpatch");
            entity.Property(e => e.VTag)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasColumnName("v_tag");
        });

        modelBuilder.Entity<Void>(entity =>
        {
            entity.HasKey(e => e.VoidId).HasName("PRIMARY");

            entity.ToTable("voids");

            entity.HasIndex(e => e.DateVoided, "datevoided");

            entity.HasIndex(e => new { e.PatientId, e.EncounterId }, "pidenc");

            entity.Property(e => e.VoidId)
                .HasColumnType("bigint(20)")
                .HasColumnName("void_id");
            entity.Property(e => e.Amount1)
                .HasPrecision(12, 2)
                .HasComment("for checkout,receipt total voided adjustments")
                .HasColumnName("amount1");
            entity.Property(e => e.Amount2)
                .HasPrecision(12, 2)
                .HasComment("for checkout,receipt total voided payments")
                .HasColumnName("amount2");
            entity.Property(e => e.DateOriginal)
                .HasComment("time of original action that is now voided")
                .HasColumnType("datetime")
                .HasColumnName("date_original");
            entity.Property(e => e.DateVoided)
                .HasComment("time of void action")
                .HasColumnType("datetime")
                .HasColumnName("date_voided");
            entity.Property(e => e.EncounterId)
                .HasComment("references form_encounter.encounter")
                .HasColumnType("bigint(20)")
                .HasColumnName("encounter_id");
            entity.Property(e => e.Notes)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("notes");
            entity.Property(e => e.OtherInfo)
                .HasComment("for checkout,receipt the old invoice refno")
                .HasColumnType("text")
                .HasColumnName("other_info");
            entity.Property(e => e.PatientId)
                .HasComment("references patient_data.pid")
                .HasColumnType("bigint(20)")
                .HasColumnName("patient_id");
            entity.Property(e => e.Reason)
                .HasMaxLength(31)
                .HasDefaultValueSql("''")
                .HasColumnName("reason");
            entity.Property(e => e.UserId)
                .HasComment("references users.id")
                .HasColumnType("bigint(20)")
                .HasColumnName("user_id");
            entity.Property(e => e.WhatVoided)
                .HasMaxLength(31)
                .HasComment("checkout,receipt and maybe other options later")
                .HasColumnName("what_voided");
        });

        modelBuilder.Entity<X12Partner>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("x12_partners");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.IdNumber)
                .HasMaxLength(255)
                .HasColumnName("id_number");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.ProcessingFormat)
                .HasColumnType("enum('standard','medi-cal','cms','proxymed','oa_eligibility','availity_eligibility')")
                .HasColumnName("processing_format");
            entity.Property(e => e.X12AttachmentEndpoint)
                .HasColumnType("tinytext")
                .HasColumnName("x12_attachment_endpoint");
            entity.Property(e => e.X12ClaimStatusEndpoint)
                .HasColumnType("tinytext")
                .HasColumnName("x12_claim_status_endpoint");
            entity.Property(e => e.X12ClientId)
                .HasColumnType("tinytext")
                .HasColumnName("x12_client_id");
            entity.Property(e => e.X12ClientSecret)
                .HasColumnType("tinytext")
                .HasColumnName("x12_client_secret");
            entity.Property(e => e.X12Dtp03)
                .HasMaxLength(1)
                .HasDefaultValueSql("'A'")
                .IsFixedLength()
                .HasColumnName("x12_dtp03");
            entity.Property(e => e.X12EligibilityEndpoint)
                .HasColumnType("tinytext")
                .HasColumnName("x12_eligibility_endpoint");
            entity.Property(e => e.X12Gs02)
                .HasMaxLength(15)
                .HasDefaultValueSql("''")
                .HasColumnName("x12_gs02");
            entity.Property(e => e.X12Gs03)
                .HasMaxLength(15)
                .HasColumnName("x12_gs03");
            entity.Property(e => e.X12Isa01)
                .HasMaxLength(2)
                .HasDefaultValueSql("'00'")
                .HasComment("User logon Required Indicator")
                .HasColumnName("x12_isa01");
            entity.Property(e => e.X12Isa02)
                .HasMaxLength(10)
                .HasDefaultValueSql("'          '")
                .HasComment("User Logon")
                .HasColumnName("x12_isa02");
            entity.Property(e => e.X12Isa03)
                .HasMaxLength(2)
                .HasDefaultValueSql("'00'")
                .HasComment("User password required Indicator")
                .HasColumnName("x12_isa03");
            entity.Property(e => e.X12Isa04)
                .HasMaxLength(10)
                .HasDefaultValueSql("'          '")
                .HasComment("User Password")
                .HasColumnName("x12_isa04");
            entity.Property(e => e.X12Isa05)
                .HasMaxLength(2)
                .HasDefaultValueSql("'ZZ'")
                .IsFixedLength()
                .HasColumnName("x12_isa05");
            entity.Property(e => e.X12Isa07)
                .HasMaxLength(2)
                .HasDefaultValueSql("'ZZ'")
                .IsFixedLength()
                .HasColumnName("x12_isa07");
            entity.Property(e => e.X12Isa14)
                .HasMaxLength(1)
                .HasDefaultValueSql("'0'")
                .IsFixedLength()
                .HasColumnName("x12_isa14");
            entity.Property(e => e.X12Isa15)
                .HasMaxLength(1)
                .HasDefaultValueSql("'P'")
                .IsFixedLength()
                .HasColumnName("x12_isa15");
            entity.Property(e => e.X12Per06)
                .HasMaxLength(80)
                .HasDefaultValueSql("''")
                .HasColumnName("x12_per06");
            entity.Property(e => e.X12ReceiverId)
                .HasMaxLength(255)
                .HasColumnName("x12_receiver_id");
            entity.Property(e => e.X12SenderId)
                .HasMaxLength(255)
                .HasColumnName("x12_sender_id");
            entity.Property(e => e.X12SftpHost)
                .HasMaxLength(255)
                .HasColumnName("x12_sftp_host");
            entity.Property(e => e.X12SftpLocalDir)
                .HasMaxLength(255)
                .HasColumnName("x12_sftp_local_dir");
            entity.Property(e => e.X12SftpLogin)
                .HasMaxLength(255)
                .HasColumnName("x12_sftp_login");
            entity.Property(e => e.X12SftpPass)
                .HasMaxLength(255)
                .HasColumnName("x12_sftp_pass");
            entity.Property(e => e.X12SftpPort)
                .HasMaxLength(255)
                .HasColumnName("x12_sftp_port");
            entity.Property(e => e.X12SftpRemoteDir)
                .HasMaxLength(255)
                .HasColumnName("x12_sftp_remote_dir");
            entity.Property(e => e.X12SubmitterId)
                .HasColumnType("smallint(6)")
                .HasColumnName("x12_submitter_id");
            entity.Property(e => e.X12SubmitterName)
                .HasMaxLength(255)
                .HasColumnName("x12_submitter_name");
            entity.Property(e => e.X12TokenEndpoint)
                .HasColumnType("tinytext")
                .HasColumnName("x12_token_endpoint");
        });

        modelBuilder.Entity<X12RemoteTracker>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("x12_remote_tracker");

            entity.Property(e => e.Id)
                .HasColumnType("bigint(20)")
                .HasColumnName("id");
            entity.Property(e => e.Claims)
                .HasColumnType("text")
                .HasColumnName("claims");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.Messages)
                .HasColumnType("text")
                .HasColumnName("messages");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("datetime")
                .HasColumnName("updated_at");
            entity.Property(e => e.X12Filename)
                .HasMaxLength(255)
                .HasColumnName("x12_filename");
            entity.Property(e => e.X12PartnerId)
                .HasColumnType("int(11)")
                .HasColumnName("x12_partner_id");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
