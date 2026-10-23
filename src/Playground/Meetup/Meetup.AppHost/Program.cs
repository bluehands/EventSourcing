var builder = DistributedApplication.CreateBuilder(args);

var events = builder.AddPostgres("postgres")
    .WithDataVolume()
    .AddDatabase("events");

builder.AddProject<Projects.Meetup>("meetup", launchProfileName: null)
    .WithHttpEndpoint()
    .WithReference(events, connectionName: "Postgres")
    .WaitFor(events);

builder.Build().Run();
