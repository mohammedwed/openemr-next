using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class HistoryDatum
{
    public long Id { get; set; }

    public byte[]? Uuid { get; set; }

    public string? Coffee { get; set; }

    public string? Tobacco { get; set; }

    public string? Alcohol { get; set; }

    public string? SleepPatterns { get; set; }

    public string? ExercisePatterns { get; set; }

    public string? SeatbeltUse { get; set; }

    public string? Counseling { get; set; }

    public string? HazardousActivities { get; set; }

    public string? RecreationalDrugs { get; set; }

    public string? LastBreastExam { get; set; }

    public string? LastMammogram { get; set; }

    public string? LastGynocologicalExam { get; set; }

    public string? LastRectalExam { get; set; }

    public string? LastProstateExam { get; set; }

    public string? LastPhysicalExam { get; set; }

    public string? LastSigmoidoscopyColonoscopy { get; set; }

    public string? LastEcg { get; set; }

    public string? LastCardiacEcho { get; set; }

    public string? LastRetinal { get; set; }

    public string? LastFluvax { get; set; }

    public string? LastPneuvax { get; set; }

    public string? LastLdl { get; set; }

    public string? LastHemoglobin { get; set; }

    public string? LastPsa { get; set; }

    public string? LastExamResults { get; set; }

    public string? HistoryMother { get; set; }

    public string? DcMother { get; set; }

    public string? HistoryFather { get; set; }

    public string? DcFather { get; set; }

    public string? HistorySiblings { get; set; }

    public string? DcSiblings { get; set; }

    public string? HistoryOffspring { get; set; }

    public string? DcOffspring { get; set; }

    public string? HistorySpouse { get; set; }

    public string? DcSpouse { get; set; }

    public string? RelativesCancer { get; set; }

    public string? RelativesTuberculosis { get; set; }

    public string? RelativesDiabetes { get; set; }

    public string? RelativesHighBloodPressure { get; set; }

    public string? RelativesHeartProblems { get; set; }

    public string? RelativesStroke { get; set; }

    public string? RelativesEpilepsy { get; set; }

    public string? RelativesMentalIllness { get; set; }

    public string? RelativesSuicide { get; set; }

    public DateTime? CataractSurgery { get; set; }

    public DateTime? Tonsillectomy { get; set; }

    public DateTime? Cholecystestomy { get; set; }

    public DateTime? HeartSurgery { get; set; }

    public DateTime? Hysterectomy { get; set; }

    public DateTime? HerniaRepair { get; set; }

    public DateTime? HipReplacement { get; set; }

    public DateTime? KneeReplacement { get; set; }

    public DateTime? Appendectomy { get; set; }

    public DateTime? Date { get; set; }

    public long Pid { get; set; }

    public string? Name1 { get; set; }

    public string? Value1 { get; set; }

    public string? Name2 { get; set; }

    public string? Value2 { get; set; }

    public string? AdditionalHistory { get; set; }

    public string? Exams { get; set; }

    public string? Usertext11 { get; set; }

    public string Usertext12 { get; set; } = null!;

    public string Usertext13 { get; set; } = null!;

    public string Usertext14 { get; set; } = null!;

    public string Usertext15 { get; set; } = null!;

    public string Usertext16 { get; set; } = null!;

    public string Usertext17 { get; set; } = null!;

    public string Usertext18 { get; set; } = null!;

    public string Usertext19 { get; set; } = null!;

    public string Usertext20 { get; set; } = null!;

    public string Usertext21 { get; set; } = null!;

    public string Usertext22 { get; set; } = null!;

    public string Usertext23 { get; set; } = null!;

    public string Usertext24 { get; set; } = null!;

    public string Usertext25 { get; set; } = null!;

    public string Usertext26 { get; set; } = null!;

    public string Usertext27 { get; set; } = null!;

    public string Usertext28 { get; set; } = null!;

    public string Usertext29 { get; set; } = null!;

    public string Usertext30 { get; set; } = null!;

    public DateOnly? Userdate11 { get; set; }

    public DateOnly? Userdate12 { get; set; }

    public DateOnly? Userdate13 { get; set; }

    public DateOnly? Userdate14 { get; set; }

    public DateOnly? Userdate15 { get; set; }

    public string? Userarea11 { get; set; }

    public string? Userarea12 { get; set; }

    /// <summary>
    /// users.id the user that first created this record
    /// </summary>
    public long? CreatedBy { get; set; }
}
