using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NepalHub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "provinces",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    sort_order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_provinces", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    description = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    normalized_name = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    concurrency_stamp = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    full_name = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    avatar_url = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    xp = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    level = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    streak_count = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    last_active_on = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.ComputedColumn),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    user_name = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    normalized_user_name = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    email = table.Column<string>(type: "varchar(254)", maxLength: 254, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    normalized_email = table.Column<string>(type: "varchar(254)", maxLength: 254, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    email_confirmed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    password_hash = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    security_stamp = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    concurrency_stamp = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    phone_number = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    phone_number_confirmed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    lockout_end = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    lockout_enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    access_failed_count = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "districts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    province_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    code = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    sort_order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_districts", x => x.id);
                    table.ForeignKey(
                        name: "districts_fk_province_id",
                        column: x => x.province_id,
                        principalTable: "provinces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "role_claims",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    role_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    claim_type = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    claim_value = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_claims", x => x.id);
                    table.ForeignKey(
                        name: "role_claims_fk_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "user_claims",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    claim_type = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    claim_value = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_claims", x => x.id);
                    table.ForeignKey(
                        name: "user_claims_fk_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "user_logins",
                columns: table => new
                {
                    login_provider = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    provider_key = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    provider_display_name = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    user_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_logins", x => new { x.login_provider, x.provider_key });
                    table.ForeignKey(
                        name: "user_logins_fk_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    role_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "user_roles_fk_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "user_roles_fk_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "user_tokens",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    login_provider = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    value = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_tokens", x => new { x.user_id, x.login_provider, x.name });
                    table.ForeignKey(
                        name: "user_tokens_fk_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "provinces",
                columns: new[] { "id", "code", "name", "sort_order" },
                values: new object[,]
                {
                    { new Guid("022783be-2840-0659-a265-69aa034e6bcf"), "gandaki", "Gandaki", 4 },
                    { new Guid("50f7cc85-8bf8-885a-9509-b66d60379a95"), "karnali", "Karnali", 6 },
                    { new Guid("662a67e2-2f2a-f05a-8654-952a2720e353"), "lumbini", "Lumbini", 5 },
                    { new Guid("6fbab2e1-0b7f-1455-a054-5e04eea380b2"), "sudurpashchim", "Sudurpashchim", 7 },
                    { new Guid("9caef719-c7ca-a65b-a516-4ff524dd176a"), "bagmati", "Bagmati", 3 },
                    { new Guid("a68f06c6-2bff-7c51-9938-ed19624ba3cb"), "madhesh", "Madhesh", 2 },
                    { new Guid("f1e6810b-474e-1151-93ca-032c43419d5c"), "koshi", "Koshi", 1 }
                });

            migrationBuilder.InsertData(
                table: "districts",
                columns: new[] { "id", "code", "name", "province_id", "sort_order" },
                values: new object[,]
                {
                    { new Guid("047c0da2-69f6-c659-8e75-4b4f483e19bf"), "bagmati-lalitpur", "Lalitpur", new Guid("9caef719-c7ca-a65b-a516-4ff524dd176a"), 7 },
                    { new Guid("0632de93-c588-125e-a61a-952da85eeda1"), "sudurpashchim-bajura", "Bajura", new Guid("6fbab2e1-0b7f-1455-a054-5e04eea380b2"), 4 },
                    { new Guid("0d059280-47d5-ce52-bafc-6c210d9e9194"), "koshi-okhaldhunga", "Okhaldhunga", new Guid("f1e6810b-474e-1151-93ca-032c43419d5c"), 7 },
                    { new Guid("0dcfb5d8-266d-cc56-aad9-672bdbefe34c"), "madhesh-parsa", "Parsa", new Guid("a68f06c6-2bff-7c51-9938-ed19624ba3cb"), 4 },
                    { new Guid("0e804f66-5c54-935a-ab05-4f7a680fb921"), "koshi-udayapur", "Udayapur", new Guid("f1e6810b-474e-1151-93ca-032c43419d5c"), 14 },
                    { new Guid("11084c02-a353-7e5b-9da1-1aac8611b2c2"), "gandaki-myagdi", "Myagdi", new Guid("022783be-2840-0659-a265-69aa034e6bcf"), 7 },
                    { new Guid("14d44900-4350-3352-bb1d-1f3f1c0ddd41"), "lumbini-rukum-east", "Rukum East", new Guid("662a67e2-2f2a-f05a-8654-952a2720e353"), 11 },
                    { new Guid("198d6abe-8063-5657-bf41-bebdd8eed859"), "sudurpashchim-darchula", "Darchula", new Guid("6fbab2e1-0b7f-1455-a054-5e04eea380b2"), 6 },
                    { new Guid("1fa3f453-727c-c25a-b972-11f0be84413a"), "koshi-sankhuwasabha", "Sankhuwasabha", new Guid("f1e6810b-474e-1151-93ca-032c43419d5c"), 9 },
                    { new Guid("214111f7-e6d0-7750-9908-e7fa408586dd"), "bagmati-dhading", "Dhading", new Guid("9caef719-c7ca-a65b-a516-4ff524dd176a"), 3 },
                    { new Guid("2352756e-c6ea-e05c-91e2-efd00c315bb1"), "karnali-kalikot", "Kalikot", new Guid("50f7cc85-8bf8-885a-9509-b66d60379a95"), 6 },
                    { new Guid("28c4cf34-223e-ea56-9b5f-a72540300015"), "lumbini-kapilvastu", "Kapilvastu", new Guid("662a67e2-2f2a-f05a-8654-952a2720e353"), 6 },
                    { new Guid("2a7b719d-444a-5e55-bcb7-757a414c772d"), "sudurpashchim-dadeldhura", "Dadeldhura", new Guid("6fbab2e1-0b7f-1455-a054-5e04eea380b2"), 5 },
                    { new Guid("2a9c4d76-8f97-845a-b032-a5fc568df2bd"), "koshi-solukhumbu", "Solukhumbu", new Guid("f1e6810b-474e-1151-93ca-032c43419d5c"), 10 },
                    { new Guid("2dc6589b-70f1-895d-82fc-8e0db66b34aa"), "madhesh-siraha", "Siraha", new Guid("a68f06c6-2bff-7c51-9938-ed19624ba3cb"), 8 },
                    { new Guid("318f3f9f-5b1e-a050-9f02-d8fc747445cf"), "karnali-mugu", "Mugu", new Guid("50f7cc85-8bf8-885a-9509-b66d60379a95"), 7 },
                    { new Guid("35489ed0-db49-185f-888c-b3376aa4f253"), "koshi-sunsari", "Sunsari", new Guid("f1e6810b-474e-1151-93ca-032c43419d5c"), 11 },
                    { new Guid("3bf0ed73-1d10-ed5f-836d-fc3c55dae181"), "gandaki-kaski", "Kaski", new Guid("022783be-2840-0659-a265-69aa034e6bcf"), 3 },
                    { new Guid("3bf616b9-c978-5f5d-acd7-4abaeaa8c1f3"), "bagmati-sindhupalchok", "Sindhupalchok", new Guid("9caef719-c7ca-a65b-a516-4ff524dd176a"), 13 },
                    { new Guid("42f291bb-fb07-cc57-9dfd-d2aefb293166"), "lumbini-bardiya", "Bardiya", new Guid("662a67e2-2f2a-f05a-8654-952a2720e353"), 3 },
                    { new Guid("4642efad-fbb1-bc59-83e7-33e9446f48e7"), "karnali-rukum-west", "Rukum West", new Guid("50f7cc85-8bf8-885a-9509-b66d60379a95"), 8 },
                    { new Guid("4dc8a4d9-7b3d-955f-86c2-1b57cc79a192"), "koshi-ilam", "Ilam", new Guid("f1e6810b-474e-1151-93ca-032c43419d5c"), 3 },
                    { new Guid("5494e425-234e-015d-b9ce-2ad635b4c9a0"), "bagmati-dolakha", "Dolakha", new Guid("9caef719-c7ca-a65b-a516-4ff524dd176a"), 4 },
                    { new Guid("5aa74f54-d6fe-4d5d-a7ba-d04cba9a3ded"), "lumbini-arghakhanchi", "Arghakhanchi", new Guid("662a67e2-2f2a-f05a-8654-952a2720e353"), 1 },
                    { new Guid("5aee9003-15e1-c35c-8971-f18d98ca04d6"), "bagmati-bhaktapur", "Bhaktapur", new Guid("9caef719-c7ca-a65b-a516-4ff524dd176a"), 1 },
                    { new Guid("65d0a328-7be1-7055-82ca-43f91727bf66"), "sudurpashchim-achham", "Achham", new Guid("6fbab2e1-0b7f-1455-a054-5e04eea380b2"), 1 },
                    { new Guid("66ce6398-df6f-5f5a-952e-67f7d70d40a0"), "bagmati-nuwakot", "Nuwakot", new Guid("9caef719-c7ca-a65b-a516-4ff524dd176a"), 9 },
                    { new Guid("72eeaefc-e6eb-4854-8522-b5434d72d385"), "gandaki-gorkha", "Gorkha", new Guid("022783be-2840-0659-a265-69aa034e6bcf"), 2 },
                    { new Guid("74fc7eb4-6c90-3d5c-abad-1158c249b264"), "lumbini-rolpa", "Rolpa", new Guid("662a67e2-2f2a-f05a-8654-952a2720e353"), 10 },
                    { new Guid("792be10d-2b9a-9b54-ac46-4f75b2c58e30"), "lumbini-palpa", "Palpa", new Guid("662a67e2-2f2a-f05a-8654-952a2720e353"), 8 },
                    { new Guid("7b626f8f-ced1-e95c-9486-6b7d79c76cb5"), "gandaki-parbat", "Parbat", new Guid("022783be-2840-0659-a265-69aa034e6bcf"), 9 },
                    { new Guid("808dfb05-73ba-3f5e-aeab-c873f5e963a2"), "koshi-bhojpur", "Bhojpur", new Guid("f1e6810b-474e-1151-93ca-032c43419d5c"), 1 },
                    { new Guid("81fa14a2-4eb3-b355-bb22-3c2eddf9b764"), "gandaki-nawalpur", "Nawalpur", new Guid("022783be-2840-0659-a265-69aa034e6bcf"), 8 },
                    { new Guid("88964e8f-5733-705f-b241-5f810311f895"), "lumbini-rupandehi", "Rupandehi", new Guid("662a67e2-2f2a-f05a-8654-952a2720e353"), 12 },
                    { new Guid("89965174-0d33-3b50-9154-4ff79cf03c2a"), "koshi-khotang", "Khotang", new Guid("f1e6810b-474e-1151-93ca-032c43419d5c"), 5 },
                    { new Guid("8a225b9c-8058-215f-ad88-62c4dc69be97"), "bagmati-kathmandu", "Kathmandu", new Guid("9caef719-c7ca-a65b-a516-4ff524dd176a"), 5 },
                    { new Guid("8bf7b4c6-74cd-925f-9209-65f9425fb0ae"), "koshi-dhankuta", "Dhankuta", new Guid("f1e6810b-474e-1151-93ca-032c43419d5c"), 2 },
                    { new Guid("8e77efea-234a-cd5c-a68d-12ce29f7bf1c"), "karnali-salyan", "Salyan", new Guid("50f7cc85-8bf8-885a-9509-b66d60379a95"), 9 },
                    { new Guid("95bb5b0c-1011-9455-a903-9e632f4a7e32"), "karnali-surkhet", "Surkhet", new Guid("50f7cc85-8bf8-885a-9509-b66d60379a95"), 10 },
                    { new Guid("96240b7a-f441-9259-9d17-45a26e048582"), "bagmati-makwanpur", "Makwanpur", new Guid("9caef719-c7ca-a65b-a516-4ff524dd176a"), 8 },
                    { new Guid("963ba661-c1e2-8557-9655-86d87c1d3724"), "gandaki-baglung", "Baglung", new Guid("022783be-2840-0659-a265-69aa034e6bcf"), 1 },
                    { new Guid("9ce2597a-7194-445c-8480-2d662fe83cfd"), "koshi-jhapa", "Jhapa", new Guid("f1e6810b-474e-1151-93ca-032c43419d5c"), 4 },
                    { new Guid("a2005772-56d2-5e51-97fe-72be83c35d2b"), "koshi-taplejung", "Taplejung", new Guid("f1e6810b-474e-1151-93ca-032c43419d5c"), 12 },
                    { new Guid("a58ce4a3-2346-5f5e-918c-4fc7e2197a6f"), "sudurpashchim-baitadi", "Baitadi", new Guid("6fbab2e1-0b7f-1455-a054-5e04eea380b2"), 2 },
                    { new Guid("a7560b75-68d9-e957-bc40-d0f84094cf2e"), "karnali-dolpa", "Dolpa", new Guid("50f7cc85-8bf8-885a-9509-b66d60379a95"), 2 },
                    { new Guid("aa7c48d4-77ce-ca53-b0a1-9aeb01423101"), "gandaki-tanahun", "Tanahun", new Guid("022783be-2840-0659-a265-69aa034e6bcf"), 11 },
                    { new Guid("ab310256-af80-865d-98c5-7c91c61840bd"), "lumbini-banke", "Banke", new Guid("662a67e2-2f2a-f05a-8654-952a2720e353"), 2 },
                    { new Guid("b156d113-3460-fe5a-9068-e439db8c6a5d"), "madhesh-bara", "Bara", new Guid("a68f06c6-2bff-7c51-9938-ed19624ba3cb"), 1 },
                    { new Guid("b1c14a2d-cb93-7e54-beea-27ab8e872ed6"), "gandaki-lamjung", "Lamjung", new Guid("022783be-2840-0659-a265-69aa034e6bcf"), 4 },
                    { new Guid("b1ff2eb6-5953-fb52-ba4d-a75fb5567845"), "koshi-morang", "Morang", new Guid("f1e6810b-474e-1151-93ca-032c43419d5c"), 6 },
                    { new Guid("b5921362-72e4-d25e-a292-50fdd5168c6d"), "gandaki-manang", "Manang", new Guid("022783be-2840-0659-a265-69aa034e6bcf"), 5 },
                    { new Guid("b65822ad-8ef7-795f-9396-11c255664ccb"), "sudurpashchim-kailali", "Kailali", new Guid("6fbab2e1-0b7f-1455-a054-5e04eea380b2"), 8 },
                    { new Guid("b6d925aa-f90e-bb59-aae0-f904ff351c23"), "bagmati-ramechhap", "Ramechhap", new Guid("9caef719-c7ca-a65b-a516-4ff524dd176a"), 10 },
                    { new Guid("b6db5f1d-5b2e-1d59-b05d-cc0dc88e2a18"), "karnali-dailekh", "Dailekh", new Guid("50f7cc85-8bf8-885a-9509-b66d60379a95"), 1 },
                    { new Guid("c417b030-e38c-345e-ae8f-a6c600a513a2"), "madhesh-saptari", "Saptari", new Guid("a68f06c6-2bff-7c51-9938-ed19624ba3cb"), 6 },
                    { new Guid("c507d4fe-42a5-fc5b-bae8-bf2f48f7f323"), "gandaki-mustang", "Mustang", new Guid("022783be-2840-0659-a265-69aa034e6bcf"), 6 },
                    { new Guid("cbccdbe5-69c3-7755-8a49-bf1845399cdd"), "gandaki-syangja", "Syangja", new Guid("022783be-2840-0659-a265-69aa034e6bcf"), 10 },
                    { new Guid("cd2a526a-d5db-1557-93ff-e80a7ead909a"), "sudurpashchim-kanchanpur", "Kanchanpur", new Guid("6fbab2e1-0b7f-1455-a054-5e04eea380b2"), 9 },
                    { new Guid("d0a94095-7b42-5e54-bb02-871c942dcc6b"), "madhesh-rautahat", "Rautahat", new Guid("a68f06c6-2bff-7c51-9938-ed19624ba3cb"), 5 },
                    { new Guid("d2174c50-cec4-3a59-acde-6834ea6dff28"), "madhesh-sarlahi", "Sarlahi", new Guid("a68f06c6-2bff-7c51-9938-ed19624ba3cb"), 7 },
                    { new Guid("d36ffa6c-5ebd-605e-8d9b-5d5effc316d2"), "bagmati-kavrepalanchok", "Kavrepalanchok", new Guid("9caef719-c7ca-a65b-a516-4ff524dd176a"), 6 },
                    { new Guid("d472d12d-2e4f-a157-82fb-c0400a2ef59d"), "karnali-humla", "Humla", new Guid("50f7cc85-8bf8-885a-9509-b66d60379a95"), 3 },
                    { new Guid("d58c88d7-176e-cc53-a8a3-9a4b7b4afeb8"), "lumbini-dang", "Dang", new Guid("662a67e2-2f2a-f05a-8654-952a2720e353"), 4 },
                    { new Guid("d798cf88-37d4-f75b-a98f-9e77e85b65ee"), "bagmati-sindhuli", "Sindhuli", new Guid("9caef719-c7ca-a65b-a516-4ff524dd176a"), 12 },
                    { new Guid("d895153c-b224-fb5e-8787-5be149234186"), "sudurpashchim-bajhang", "Bajhang", new Guid("6fbab2e1-0b7f-1455-a054-5e04eea380b2"), 3 },
                    { new Guid("dea15a05-20a7-c85f-9a4c-9f99cd7ab06c"), "karnali-jumla", "Jumla", new Guid("50f7cc85-8bf8-885a-9509-b66d60379a95"), 5 },
                    { new Guid("e0e29cae-a6c7-4752-8bd1-7a00882891bd"), "lumbini-pyuthan", "Pyuthan", new Guid("662a67e2-2f2a-f05a-8654-952a2720e353"), 9 },
                    { new Guid("e9ce5560-39a7-985b-978e-b699d90629f6"), "koshi-panchthar", "Panchthar", new Guid("f1e6810b-474e-1151-93ca-032c43419d5c"), 8 },
                    { new Guid("ec39a109-5705-1056-8ce4-b52c57dcf092"), "karnali-jajarkot", "Jajarkot", new Guid("50f7cc85-8bf8-885a-9509-b66d60379a95"), 4 },
                    { new Guid("ecfd4906-1737-3a53-9baa-db0cd2051806"), "bagmati-chitwan", "Chitwan", new Guid("9caef719-c7ca-a65b-a516-4ff524dd176a"), 2 },
                    { new Guid("ed11b4b6-363b-7654-82f8-44c01b03e54c"), "lumbini-nawalparasi-west", "Nawalparasi West", new Guid("662a67e2-2f2a-f05a-8654-952a2720e353"), 7 },
                    { new Guid("ef0903ff-ebc6-2753-b586-3b78865ddc47"), "sudurpashchim-doti", "Doti", new Guid("6fbab2e1-0b7f-1455-a054-5e04eea380b2"), 7 },
                    { new Guid("f229004d-81bc-e85a-90c8-5e2baa7a05aa"), "bagmati-rasuwa", "Rasuwa", new Guid("9caef719-c7ca-a65b-a516-4ff524dd176a"), 11 },
                    { new Guid("f272342f-9f67-615c-8d77-54bae18b0775"), "madhesh-dhanusha", "Dhanusha", new Guid("a68f06c6-2bff-7c51-9938-ed19624ba3cb"), 2 },
                    { new Guid("f75c9bdc-8a89-9c50-90db-0c1b7d3ff8e4"), "lumbini-gulmi", "Gulmi", new Guid("662a67e2-2f2a-f05a-8654-952a2720e353"), 5 },
                    { new Guid("f8663724-0f8a-eb55-b5d9-d402ea944323"), "madhesh-mahottari", "Mahottari", new Guid("a68f06c6-2bff-7c51-9938-ed19624ba3cb"), 3 },
                    { new Guid("fd3f1137-780f-a151-8e30-1f805943da4c"), "koshi-tehrathum", "Tehrathum", new Guid("f1e6810b-474e-1151-93ca-032c43419d5c"), 13 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_districts_code",
                table: "districts",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_districts_province_id",
                table: "districts",
                column: "province_id");

            migrationBuilder.CreateIndex(
                name: "ix_districts_province_id_sort_order",
                table: "districts",
                columns: new[] { "province_id", "sort_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_provinces_code",
                table: "provinces",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_provinces_name",
                table: "provinces",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_role_claims_role_id",
                table: "role_claims",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "roles",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_claims_user_id",
                table: "user_claims",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_logins_user_id",
                table: "user_logins",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_roles_role_id",
                table: "user_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_created_at",
                table: "users",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_users_level_xp",
                table: "users",
                columns: new[] { "level", "xp" });

            migrationBuilder.CreateIndex(
                name: "ix_users_normalized_email",
                table: "users",
                column: "normalized_email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_phone_number",
                table: "users",
                column: "phone_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "users",
                column: "normalized_user_name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "districts");

            migrationBuilder.DropTable(
                name: "role_claims");

            migrationBuilder.DropTable(
                name: "user_claims");

            migrationBuilder.DropTable(
                name: "user_logins");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "user_tokens");

            migrationBuilder.DropTable(
                name: "provinces");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
