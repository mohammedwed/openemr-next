using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IAccessControl"/> (ADR 0076): legacy's own access decision, <c>AclMain::aclCheckCore($section, $value, $username)</c>, read straight from its <c>gacl_*</c> tables with one parameterized
/// query, so a right granted or taken away in OpenEMR's administration screens takes effect here with no sync.
/// </summary>
/// <remarks>
/// <para>What legacy does, and so what this does: it first asks whether the user is a superuser (<c>admin</c>/<c>super</c>), who holds everything; otherwise it collects every enabled rule for the right whose object
/// is the user (<c>gacl_aro_map</c>) or a group the user is in or that contains a group the user is in (<c>gacl_groups_aro_map</c>, then the nested-set bounds in <c>gacl_aro_groups</c>), and which names no
/// extended object (<c>gacl_axo_*</c>, which legacy uses for per-record rules and this service does not). The user holds the right when at least one such rule allows it and none refuses it: a refusal wins.</para>
/// <para>Not reproduced: legacy's optional <c>return_value</c> narrowing (read, write, addonly) and its per-session cache. Nothing here asks for a narrowed value, and a cache would keep serving a right
/// legacy has since taken away. The adapter never writes and reads only the nine columns it needs (the seed script's grant).</para>
/// </remarks>
public sealed class EfAccessControl(ClinicalCareDbContext db) : IAccessControl
{
    public async Task<bool> IsAllowedAsync(string? username, AccessRight right, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(username))
            return false;

        // Legacy's order: the superuser probe first, so a superuser needs no rule for the right itself.
        if (await RulesSayYesAsync(username, AccessRight.AdminSuper, ct))
            return true;

        return right != AccessRight.AdminSuper && await RulesSayYesAsync(username, right, ct);
    }

    private async Task<bool> RulesSayYesAsync(string username, AccessRight right, CancellationToken ct)
    {
        var rules = await db.Database
            .SqlQuery<RuleRow>(
                $"""
                SELECT DISTINCT a.id AS Id, CAST(a.allow AS SIGNED) AS Allow
                FROM gacl_acl a
                JOIN gacl_aco_map ac ON ac.acl_id = a.id AND ac.section_value = {right.Section} AND ac.value = {right.Value}
                LEFT JOIN gacl_aro_map ar ON ar.acl_id = a.id
                LEFT JOIN gacl_aro_groups_map arg ON arg.acl_id = a.id
                LEFT JOIN gacl_axo_map ax ON ax.acl_id = a.id
                LEFT JOIN gacl_axo_groups_map axg ON axg.acl_id = a.id
                WHERE a.enabled = 1
                  AND ax.acl_id IS NULL AND axg.acl_id IS NULL
                  AND ((ar.section_value = 'users' AND ar.value = {username})
                       OR arg.group_id IN (
                            SELECT g2.id
                            FROM gacl_aro o
                            JOIN gacl_groups_aro_map gm ON gm.aro_id = o.id
                            JOIN gacl_aro_groups g1 ON g1.id = gm.group_id
                            JOIN gacl_aro_groups g2 ON g2.lft <= g1.lft AND g2.rgt >= g1.rgt
                            WHERE o.section_value = 'users' AND o.value = {username}))
                """)
            .ToListAsync(ct);

        // Legacy: access when any rule allows, and a refusal anywhere wins.
        return rules.Any(r => r.Allow != 0) && rules.All(r => r.Allow != 0);
    }

    private sealed class RuleRow
    {
        public int Id { get; set; }
        public long Allow { get; set; }
    }
}
