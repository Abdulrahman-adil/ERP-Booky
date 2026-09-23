using Erp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ErpDbContext))]
[Migration("20260831040000_AddFinancialReportingPermissions")]
public partial class AddFinancialReportingPermissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$
            DECLARE
                view_permission_id uuid;
                export_permission_id uuid;
                administrator_role record;
            BEGIN
                INSERT INTO erp.permissions ("Id", "Key", "Name", "Description", "IsActive")
                VALUES
                    ('08af9718-0313-4fe9-a0e4-7a4a0d2b6a11', 'financialreports.view', 'Financial Reports View', 'View financial reports derived from posted ledger entries.', true),
                    ('fc9030fe-4967-45e4-a279-e05a9c4d5352', 'financialreports.export', 'Financial Reports Export', 'Export financial reports to XLSX and PDF.', true)
                ON CONFLICT ("Key") DO NOTHING;

                SELECT "Id" INTO view_permission_id FROM erp.permissions WHERE "Key" = 'financialreports.view';
                SELECT "Id" INTO export_permission_id FROM erp.permissions WHERE "Key" = 'financialreports.export';

                FOR administrator_role IN
                    SELECT "Id" AS role_id FROM erp.roles WHERE "Name" = 'Administrator' AND "IsActive"
                LOOP
                    INSERT INTO erp.role_permissions ("Id", "RoleId", "PermissionId")
                    VALUES ((md5(administrator_role.role_id::text || view_permission_id::text))::uuid, administrator_role.role_id, view_permission_id)
                    ON CONFLICT ("RoleId", "PermissionId") DO NOTHING;

                    INSERT INTO erp.role_permissions ("Id", "RoleId", "PermissionId")
                    VALUES ((md5(administrator_role.role_id::text || export_permission_id::text))::uuid, administrator_role.role_id, export_permission_id)
                    ON CONFLICT ("RoleId", "PermissionId") DO NOTHING;
                END LOOP;
            END $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM erp.role_permissions AS role_permission
            USING erp.permissions AS permission
            WHERE role_permission."PermissionId" = permission."Id"
              AND permission."Key" IN ('financialreports.view', 'financialreports.export');

            DELETE FROM erp.permissions
            WHERE "Key" IN ('financialreports.view', 'financialreports.export');
            """);
    }
}
