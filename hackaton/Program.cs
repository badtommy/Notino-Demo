var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql");
var sqldb = sql.AddDatabase("sqldb");


builder.AddProject<Projects.NotinoDemo>("notinodemo")
    .WaitFor(sqldb)
    .WithReference(sqldb);

builder.Build().Run();