using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class PatientReminder
{
    public long Id { get; set; }

    /// <summary>
    /// 1 if active and 0 if not active
    /// </summary>
    public bool? Active { get; set; }

    public DateTime? DateInactivated { get; set; }

    /// <summary>
    /// Maps to list_options list rule_reminder_inactive_opt
    /// </summary>
    public string ReasonInactivated { get; set; } = null!;

    /// <summary>
    /// Maps to list_options list rule_reminder_due_opt
    /// </summary>
    public string DueStatus { get; set; } = null!;

    /// <summary>
    /// id from patient_data table
    /// </summary>
    public long Pid { get; set; }

    /// <summary>
    /// Maps to the category item in the rule_action_item table
    /// </summary>
    public string Category { get; set; } = null!;

    /// <summary>
    /// Maps to the item column in the rule_action_item table
    /// </summary>
    public string Item { get; set; } = null!;

    public DateTime? DateCreated { get; set; }

    public DateTime? DateSent { get; set; }

    /// <summary>
    /// 0 if not sent and 1 if sent
    /// </summary>
    public bool VoiceStatus { get; set; }

    /// <summary>
    /// 0 if not sent and 1 if sent
    /// </summary>
    public bool SmsStatus { get; set; }

    /// <summary>
    /// 0 if not sent and 1 if sent
    /// </summary>
    public bool EmailStatus { get; set; }

    /// <summary>
    /// 0 if not sent and 1 if sent
    /// </summary>
    public bool MailStatus { get; set; }
}
