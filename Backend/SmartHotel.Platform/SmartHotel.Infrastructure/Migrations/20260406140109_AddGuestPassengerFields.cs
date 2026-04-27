using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHotel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestPassengerFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BirthDate",
                table: "Guests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentNumber",
                table: "Guests",
                type: "nvarchar(40)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "Guests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "Guests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "Guests",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Guests",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.Sql("""
                UPDATE [Guests]
                SET
                    [FirstName] = COALESCE(NULLIF(LTRIM(RTRIM([FullName])), ''), 'Pasajero'),
                    [LastName] = 'SinApellido',
                    [DocumentNumber] = CONCAT('LEGACY-', [Id]),
                    [BirthDate] = '1990-01-01'
                WHERE [FirstName] IS NULL
                    OR [LastName] IS NULL
                    OR [DocumentNumber] IS NULL
                    OR [BirthDate] IS NULL;
                """);

            migrationBuilder.AlterColumn<DateTime>(
                name: "BirthDate",
                table: "Guests",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DocumentNumber",
                table: "Guests",
                type: "nvarchar(40)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FirstName",
                table: "Guests",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LastName",
                table: "Guests",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "Guests");

            migrationBuilder.CreateIndex(
                name: "IX_Guests_DocumentNumber",
                table: "Guests",
                column: "DocumentNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Guests_DocumentNumber",
                table: "Guests");

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "Guests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE [Guests]
                SET [FullName] = LTRIM(RTRIM(
                    CONCAT(
                        COALESCE([FirstName], ''),
                        CASE WHEN [LastName] IS NULL OR LTRIM(RTRIM([LastName])) = '' THEN '' ELSE CONCAT(' ', [LastName]) END
                    )
                ));
                """);

            migrationBuilder.Sql("""
                UPDATE [Guests]
                SET [FullName] = 'Pasajero'
                WHERE [FullName] IS NULL OR LTRIM(RTRIM([FullName])) = '';
                """);

            migrationBuilder.Sql("""
                UPDATE [Guests]
                SET [Email] = ''
                WHERE [Email] IS NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE [Guests]
                SET [Phone] = ''
                WHERE [Phone] IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "Guests",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "Guests",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Guests",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "BirthDate",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "DocumentNumber",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "Guests");
        }
    }
}
