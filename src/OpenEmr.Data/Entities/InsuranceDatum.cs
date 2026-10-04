using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class InsuranceDatum
{
    public long Id { get; set; }

    public byte[]? Uuid { get; set; }

    public string? Type { get; set; }

    public string? Provider { get; set; }

    public string? PlanName { get; set; }

    public string? PolicyNumber { get; set; }

    public string? GroupNumber { get; set; }

    public string? SubscriberLname { get; set; }

    public string? SubscriberMname { get; set; }

    public string? SubscriberFname { get; set; }

    public string? SubscriberRelationship { get; set; }

    public string? SubscriberSs { get; set; }

    public DateOnly? SubscriberDob { get; set; }

    public string? SubscriberStreet { get; set; }

    public string? SubscriberPostalCode { get; set; }

    public string? SubscriberCity { get; set; }

    public string? SubscriberState { get; set; }

    public string? SubscriberCountry { get; set; }

    public string? SubscriberPhone { get; set; }

    public string? SubscriberEmployer { get; set; }

    public string? SubscriberEmployerStreet { get; set; }

    public string? SubscriberEmployerPostalCode { get; set; }

    public string? SubscriberEmployerState { get; set; }

    public string? SubscriberEmployerCountry { get; set; }

    public string? SubscriberEmployerCity { get; set; }

    public string? Copay { get; set; }

    public DateOnly? Date { get; set; }

    public long Pid { get; set; }

    public string? SubscriberSex { get; set; }

    public string AcceptAssignment { get; set; } = null!;

    public string PolicyType { get; set; } = null!;

    public string? SubscriberStreetLine2 { get; set; }

    public string? SubscriberEmployerStreetLine2 { get; set; }

    public DateOnly? DateEnd { get; set; }
}
