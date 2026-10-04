using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class PaymentGatewayDetail
{
    public int Id { get; set; }

    public string? ServiceName { get; set; }

    public string? LoginId { get; set; }

    public string? TransactionKey { get; set; }

    public string? Md5 { get; set; }
}
