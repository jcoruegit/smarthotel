using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHotel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentTypeToGuests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTypes", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "DocumentTypes",
                columns: new[] { "Name" },
                values: new object[,]
                {
                    { "DNI" },
                    { "Pasaporte" }
                });

            migrationBuilder.AddColumn<int>(
                name: "DocumentTypeId",
                table: "Guests",
                type: "int",
                nullable: true);

            migrationBuilder.DropIndex(
                name: "IX_Guests_DocumentNumber",
                table: "Guests");

            migrationBuilder.Sql("""
                UPDATE [Guests]
                SET [DocumentNumber] = REPLACE(REPLACE(REPLACE(REPLACE([DocumentNumber], 'DNI-', ''), 'PASAPORTE-', ''), '-', ''), '.', '');
                """);

            migrationBuilder.Sql("""
                UPDATE [Guests]
                SET [DocumentNumber] = RIGHT(CONCAT('00000000', CAST([Id] AS varchar(20))), 8)
                WHERE [DocumentNumber] IS NULL OR [DocumentNumber] LIKE '%[^0-9]%' OR LEN([DocumentNumber]) <> 8;
                """);

            migrationBuilder.Sql("""
                UPDATE [Guests]
                SET [DocumentTypeId] = (
                    SELECT TOP 1 [Id]
                    FROM [DocumentTypes]
                    WHERE [Name] = 'DNI'
                )
                WHERE [DocumentTypeId] IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "DocumentNumber",
                table: "Guests",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<int>(
                name: "DocumentTypeId",
                table: "Guests",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTypes_Name",
                table: "DocumentTypes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Guests_DocumentTypeId",
                table: "Guests",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Guests_DocumentTypeId_DocumentNumber",
                table: "Guests",
                columns: new[] { "DocumentTypeId", "DocumentNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Guests_DocumentTypes_DocumentTypeId",
                table: "Guests",
                column: "DocumentTypeId",
                principalTable: "DocumentTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Guests_DocumentTypes_DocumentTypeId",
                table: "Guests");

            migrationBuilder.DropTable(
                name: "DocumentTypes");

            migrationBuilder.DropIndex(
                name: "IX_Guests_DocumentTypeId",
                table: "Guests");

            migrationBuilder.DropIndex(
                name: "IX_Guests_DocumentTypeId_DocumentNumber",
                table: "Guests");

            migrationBuilder.AlterColumn<string>(
                name: "DocumentNumber",
                table: "Guests",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(8)",
                oldMaxLength: 8);

            migrationBuilder.DropColumn(
                name: "DocumentTypeId",
                table: "Guests");

            migrationBuilder.CreateIndex(
                name: "IX_Guests_DocumentNumber",
                table: "Guests",
                column: "DocumentNumber",
                unique: true);
        }
    }
}
