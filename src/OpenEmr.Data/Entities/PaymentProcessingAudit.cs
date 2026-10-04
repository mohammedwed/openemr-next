using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class PaymentProcessingAudit
{
    public byte[] Uuid { get; set; } = null!;

    public string? Service { get; set; }

    public long Pid { get; set; }

    public sbyte? Success { get; set; }

    public string? ActionName { get; set; }

    public string? Amount { get; set; }

    public string? Ticket { get; set; }

    public string? TransactionId { get; set; }

    public string? AuditData { get; set; }

    public DateTime? Date { get; set; }

    public byte[]? MapUuid { get; set; }

    public string? MapTransactionId { get; set; }

    public sbyte? Reverted { get; set; }

    public string? RevertActionName { get; set; }

    public string? RevertTransactionId { get; set; }

    public string? RevertAuditData { get; set; }

    public DateTime? RevertDate { get; set; }
}
