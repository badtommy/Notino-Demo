using Microsoft.Data.SqlClient;
using NotinoDemo.Models;

namespace NotinoDemo.Data;

public sealed class SqlBootstrapper
{
    private readonly string _connectionString;
    private readonly string _databaseName;

    public SqlBootstrapper(IConfiguration configuration)
    {
        _connectionString = ResolveConnectionString(configuration);
        var builder = new SqlConnectionStringBuilder(_connectionString);
        _databaseName = string.IsNullOrWhiteSpace(builder.InitialCatalog) ? "sqldb" : builder.InitialCatalog;
    }

    public string ConnectionString => _connectionString;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureDatabaseExistsAsync(cancellationToken);

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await EnsureDiscountCodesSchemaAsync(connection, cancellationToken);

            var commands = new[]
            {
                @"
IF OBJECT_ID('dbo.Products', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Products
    (
        Id INT NOT NULL PRIMARY KEY,
        Name NVARCHAR(200) NOT NULL,
        Category NVARCHAR(100) NOT NULL,
        Price DECIMAL(18,2) NOT NULL,
        Stock INT NOT NULL,
        IsActive BIT NOT NULL DEFAULT(1)
    );
END",
                @"
IF OBJECT_ID('dbo.Customers', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Customers
    (
        Id INT NOT NULL PRIMARY KEY,
        Name NVARCHAR(200) NULL,
        Email NVARCHAR(200) NOT NULL,
        IsGuest BIT NOT NULL DEFAULT(0)
    );
END",
                @"
IF OBJECT_ID('dbo.DiscountCodes', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DiscountCodes
    (
        Code NVARCHAR(50) NOT NULL PRIMARY KEY,
        DiscountPercent DECIMAL(9,2) NOT NULL
    );
END",
                @"
IF OBJECT_ID('dbo.Orders', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Orders
    (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CustomerId INT NOT NULL,
        CustomerName NVARCHAR(200) NOT NULL,
        Subtotal DECIMAL(18,2) NOT NULL,
        DiscountPercent DECIMAL(9,2) NOT NULL,
        Total DECIMAL(18,2) NOT NULL,
        CreatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END",
                @"
IF OBJECT_ID('dbo.Logs', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Logs
    (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Timestamp] DATETIME2 NOT NULL,
        [Level] NVARCHAR(16) NULL,
        [Message] NVARCHAR(MAX) NULL,
        [Exception] NVARCHAR(MAX) NULL,
        [Properties] NVARCHAR(MAX) NULL,
        ServiceName NVARCHAR(256) NULL
    );
END",
                @"
IF NOT EXISTS (SELECT 1 FROM dbo.Products)
BEGIN
    INSERT INTO dbo.Products (Id, Name, Category, Price, Stock, IsActive)
    VALUES
        (1, 'Chanel Coco Mademoiselle', 'Parfemy', 2499.00, 12, 1),
        (2, 'Dior Sauvage', 'Parfemy', 2199.00, 8, 1),
        (3, 'La Roche-Posay Effaclar', 'Pece', 499.00, 24, 1),
        (4, 'Nivea Soft Cream', 'Pece', 139.00, 40, 1),
        (5, 'Maybelline Lash Sensational', 'Make-up', 259.00, 30, 1),
        (6, 'NYX Butter Gloss', 'Make-up', 189.00, 20, 1),
        (7, 'Sample Tester Item', 'Parfemy', 0.00, 100, 1),
        (8, 'Gift Box Deluxe', 'Darky', 799.00, 15, 1);
END",
                @"
IF NOT EXISTS (SELECT 1 FROM dbo.Customers)
BEGIN
    INSERT INTO dbo.Customers (Id, Name, Email, IsGuest)
    VALUES
        (1, 'Anna Novakova', 'anna@example.com', 0),
        (2, 'Petr Svoboda', 'petr@example.com', 0),
        (3, NULL, 'guest@example.com', 1);
END",
                @"
IF NOT EXISTS (SELECT 1 FROM dbo.DiscountCodes)
BEGIN
    INSERT INTO dbo.DiscountCodes (Code, DiscountPercent)
    VALUES
        ('SPRING10', 10),
        ('VIP15', 15),
        ('WELCOME5', 5);
END"
            };

            foreach (var sql in commands)
            {
                await using var command = new SqlCommand(sql, connection);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        catch (SqlException ex)
        {
            throw new InvalidOperationException(BuildConnectionFailureMessage(), ex);
        }
    }

    private static string ResolveConnectionString(IConfiguration configuration)
    {
        var configured = configuration.GetConnectionString("sqldb")
                        ?? configuration.GetConnectionString("notinodemo-db")
                        ?? configuration.GetConnectionString("sql");

        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException("No SQL connection string was configured. Run the app through Aspire or set ConnectionStrings__sqldb.");
        }

        var builder = new SqlConnectionStringBuilder(configured);
        if (string.IsNullOrWhiteSpace(builder.InitialCatalog) && !string.IsNullOrWhiteSpace(builder.DataSource))
        {
            builder.InitialCatalog = "sqldb";
        }

        return builder.ConnectionString;
    }

    private async Task EnsureDatabaseExistsAsync(CancellationToken cancellationToken)
    {
        var masterConnectionString = BuildMasterConnectionString();

        await using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
IF DB_ID(@DatabaseName) IS NULL
BEGIN
    DECLARE @statement nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@DatabaseName);
    EXEC(@statement);
END";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@DatabaseName", _databaseName);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private string BuildMasterConnectionString()
    {
        var builder = new SqlConnectionStringBuilder(_connectionString)
        {
            InitialCatalog = "master"
        };

        return builder.ConnectionString;
    }

    private string BuildConnectionFailureMessage()
    {
        var builder = new SqlConnectionStringBuilder(_connectionString);
        var target = string.IsNullOrWhiteSpace(builder.DataSource) ? "<unknown-server>" : builder.DataSource;

        return $"Unable to connect to SQL Server '{target}' for database '{_databaseName}'. If you're running the API directly, set a valid ConnectionStrings__sqldb value. If you're running under Aspire, start the AppHost project so the SQL resource and connection string are provisioned automatically.";
    }

    public async Task<List<Product>> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        var products = new List<Product>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        const string sql = "SELECT Id, Name, Category, Price, Stock, IsActive FROM dbo.Products WHERE IsActive = 1 ORDER BY Id";
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            products.Add(new Product(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetDecimal(3),
                reader.GetInt32(4),
                reader.GetBoolean(5)));
        }

        return products;
    }

    public async Task<List<Customer>> GetCustomersAsync(CancellationToken cancellationToken = default)
    {
        var customers = new List<Customer>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        const string sql = "SELECT Id, Name, Email, IsGuest FROM dbo.Customers ORDER BY Id";
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            customers.Add(new Customer(
                reader.GetInt32(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.GetString(2),
                reader.GetBoolean(3)));
        }

        return customers;
    }

    public async Task<Dictionary<string, decimal>> GetDiscountCodesAsync(CancellationToken cancellationToken = default)
    {
        var codes = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        const string sql = "SELECT Code, DiscountPercent FROM dbo.DiscountCodes ORDER BY Code";
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            codes[reader.GetString(0)] = reader.GetDecimal(1);
        }

        return codes;
    }

    public async Task<int> InsertOrderAsync(OrderResult order, int customerId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        const string sql = @"
INSERT INTO dbo.Orders (CustomerId, CustomerName, Subtotal, DiscountPercent, Total)
OUTPUT INSERTED.Id
VALUES (@CustomerId, @CustomerName, @Subtotal, @DiscountPercent, @Total);";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@CustomerId", customerId);
        command.Parameters.AddWithValue("@CustomerName", order.CustomerName);
        command.Parameters.AddWithValue("@Subtotal", order.Subtotal);
        command.Parameters.AddWithValue("@DiscountPercent", order.DiscountPercent);
        command.Parameters.AddWithValue("@Total", order.Total);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    private async Task EnsureDiscountCodesSchemaAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = @"
IF OBJECT_ID('dbo.DiscountCodes', 'U') IS NOT NULL
    AND COL_LENGTH('dbo.DiscountCodes', 'DiscountPercent') IS NULL
    AND COL_LENGTH('dbo.DiscountCodes', 'Percent') IS NOT NULL
BEGIN
    EXEC sp_rename 'dbo.DiscountCodes.[Percent]', 'DiscountPercent', 'COLUMN';
END";

        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
