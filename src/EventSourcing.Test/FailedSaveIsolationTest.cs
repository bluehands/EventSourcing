using System.Reactive.Subjects;
using EventSourcing.Commands;
using EventSourcing.Commands.Infrastructure.Internal;
using EventSourcing.Commands.SerializablePayloads;
using EventSourcing.Infrastructure;
using EventSourcing.Persistence.EntityFramework;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace EventSourcing.Test;

public class FailedSaveIsolationTest
{
    [Fact]
    public async Task SubsequentWriteInSameScopeDoesNotPersistFailedBatch()
    {
        var interceptor = new FailFirstSaveInterceptor();
        await using var services = CreateServices(interceptor);
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<EventStoreContext>().Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var store = scope.ServiceProvider.GetRequiredService<IEventStore>();

        Func<Task> failedWrite = () => store.WriteEvents([new EntryAdded("Failed batch")]);
        await failedWrite.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage(FailFirstSaveInterceptor.ErrorMessage);

        await store.WriteEvents([new EntryAdded("Independent write")]);

        using var readScope = services.CreateScope();
        var persisted = await readScope.ServiceProvider.GetRequiredService<EventStoreContext>()
            .Events.ToListAsync(TestContext.Current.CancellationToken);
        persisted.Should().ContainSingle()
            .Which.Payload.Should().Contain("Independent write");
        interceptor.SaveAttempts.Should().Be(2);
    }

    [Fact]
    public async Task FailedWriteDiscardsAllPendingChangesAndKeepsUnchangedEntries()
    {
        var interceptor = new FailFirstSaveInterceptor(failOnAttempt: 2);
        await using var services = CreateServices(interceptor);
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventStoreContext>();
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var modified = DbEvent("Modified");
        var deleted = DbEvent("Deleted");
        var unchanged = DbEvent("Unchanged");
        context.Events.AddRange(modified, deleted, unchanged);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.Entry(modified).Property(e => e.Payload).CurrentValue = "Changed payload";
        context.Events.Remove(deleted);
        var added = DbEvent("Added");
        context.Events.Add(added);
        var store = scope.ServiceProvider.GetRequiredService<IEventStore>();

        Func<Task> failedWrite = () => store.WriteEvents([new EntryAdded("Failed batch")]);
        await failedWrite.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage(FailFirstSaveInterceptor.ErrorMessage);

        var remaining = context.ChangeTracker.Entries<Persistence.EntityFramework.Event>()
            .Should().ContainSingle().Which;
        remaining.Entity.Should().BeSameAs(unchanged);
        remaining.State.Should().Be(EntityState.Unchanged);
        context.Entry(added).State.Should().Be(EntityState.Detached);
        context.Entry(modified).State.Should().Be(EntityState.Detached);
        context.Entry(deleted).State.Should().Be(EntityState.Detached);

        await store.WriteEvents([new EntryAdded("Independent write")]);

        using var readScope = services.CreateScope();
        var persisted = await readScope.ServiceProvider.GetRequiredService<EventStoreContext>()
            .Events.ToListAsync(TestContext.Current.CancellationToken);
        persisted.Should().HaveCount(4);
        persisted.Should().ContainSingle(e => e.EventType == "Modified" && e.Payload == "{}");
        persisted.Should().ContainSingle(e => e.EventType == "Deleted");
        persisted.Should().ContainSingle(e => e.EventType == "Unchanged");
        persisted.Should().ContainSingle(e => e.Payload.Contains("Independent write"));
    }

    [Fact]
    public async Task SecondEventSerializationFailureDoesNotContaminateSubsequentWrite()
    {
        var serializer = new FailSecondSerialization();
        await using var services = CreateServices(new FailFirstSaveInterceptor(failOnAttempt: int.MaxValue), serializer);
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventStoreContext>();
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var store = scope.ServiceProvider.GetRequiredService<IEventStore>();

        Func<Task> failedWrite = () => store.WriteEvents([
            new EntryAdded("First event"), new EntryAdded("Second event")]);
        await failedWrite.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage(FailSecondSerialization.ErrorMessage);
        serializer.SerializeAttempts.Should().Be(2);

        using (var readScope = services.CreateScope())
            (await readScope.ServiceProvider.GetRequiredService<EventStoreContext>()
                .Events.ToListAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
        context.ChangeTracker.Entries().Should().BeEmpty();

        await store.WriteEvents([new EntryAdded("Independent write")]);

        using var finalReadScope = services.CreateScope();
        var persisted = await finalReadScope.ServiceProvider.GetRequiredService<EventStoreContext>()
            .Events.ToListAsync(TestContext.Current.CancellationToken);
        persisted.Should().ContainSingle().Which.Payload.Should().Contain("Independent write");
    }

    [Fact]
    public async Task CommandFallbackPersistsOnlyFaultMarkerAfterSaveFailure()
    {
        var interceptor = new FailFirstSaveInterceptor();
        await using var services = CreateServices(interceptor);
        using (var setupScope = services.CreateScope())
            await setupScope.ServiceProvider.GetRequiredService<EventStoreContext>().Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        using var commandScope = new CompletionScope(services.CreateScope());
        using var commands = new Subject<ScopedCommand>();
        using var subscription = commands.SubscribeCommandProcessors(
            (_, _) => new AddEntryProcessor(), logger: null, eventPollWakeUp: null);
        var command = new AddEntry();

        commands.OnNext(new ScopedCommand(command, commandScope, DisposeAfterProcess: true));
        await commandScope.Disposed.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        using var readScope = services.CreateScope();
        var persisted = await readScope.ServiceProvider.GetRequiredService<EventStoreContext>()
            .Events.ToListAsync(TestContext.Current.CancellationToken);
        persisted.Should().ContainSingle()
            .Which.EventType.Should().Be(EventTypes.CommandProcessed);

        // Read through the public store as well to distinguish faulted and successful markers.
        var events = new List<Event>();
        await foreach (var @event in readScope.ServiceProvider.GetRequiredService<IEventStore>().ReadEvents())
            events.Add(@event);
        var marker = events.Should().ContainSingle().Which.Payload
            .Should().BeOfType<CommandProcessed<string>>().Subject;
        marker.CommandId.Should().Be(command.Id);
        marker.CommandResult.Should().BeOfType<CommandResult<string>.Faulted_>();
        interceptor.SaveAttempts.Should().Be(2);
    }

    static Persistence.EntityFramework.Event DbEvent(string eventType) =>
        new(0, "Pending", "Work", eventType, "{}", DateTimeOffset.UtcNow);

    static ServiceProvider CreateServices(SaveChangesInterceptor interceptor,
        IEventSerializer<string>? serializer = null)
    {
        var services = new ServiceCollection()
            .AddEventSourcing(options => options
                .UseInMemoryEventStore()
                .PayloadAssemblies(typeof(FailedSaveIsolationTest).Assembly)
                .UseFunicularCommands<string, ErrorPayload>(commands =>
                    commands.CommandProcessorAssemblies(typeof(FailedSaveIsolationTest).Assembly)))
            .AddDbContext<EventStoreContext>(options => options.AddInterceptors(interceptor));
        if (serializer != null)
            services.AddSingleton(serializer);
        return services.BuildServiceProvider();
    }

    [SerializableEventPayload("FailedSaveIsolation.EntryAdded")]
    public record EntryAdded(string Text)
        : EventPayload(new StreamId("FailedSaveIsolation", "Journal"), "FailedSaveIsolation.EntryAdded");

    public record ErrorPayload(string Message) : IErrorPayload<string, ErrorPayload>
    {
        public string ToError() => Message;
        public static ErrorPayload FromError(string error) => new(error);
    }

    public record AddEntry : Command;

    public class AddEntryProcessor : SynchronousCommandProcessor<AddEntry, string>
    {
        public override ProcessingResult<string> ProcessSync(AddEntry command) =>
            new ProcessingResult<string>.Ok_(new EntryAdded("Command domain event"));
    }

    sealed class FailFirstSaveInterceptor(int failOnAttempt = 1) : SaveChangesInterceptor
    {
        public const string ErrorMessage = "Injected first-save failure";
        int _saveAttempts;
        public int SaveAttempts => Volatile.Read(ref _saveAttempts);

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _saveAttempts) == failOnAttempt)
                throw new InvalidOperationException(ErrorMessage);
            return ValueTask.FromResult(result);
        }
    }

    sealed class FailSecondSerialization : IEventSerializer<string>
    {
        public const string ErrorMessage = "Injected second-event serialization failure";
        public int SerializeAttempts { get; private set; }

        public string Serialize(object serializablePayload)
        {
            if (++SerializeAttempts == 2)
                throw new InvalidOperationException(ErrorMessage);
            return System.Text.Json.JsonSerializer.Serialize(serializablePayload);
        }

        public object Deserialize(Type serializablePayloadType, string serializedPayload) =>
            System.Text.Json.JsonSerializer.Deserialize(serializedPayload, serializablePayloadType)!;
    }

    sealed class CompletionScope(IServiceScope inner) : IServiceScope
    {
        readonly TaskCompletionSource _disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IServiceProvider ServiceProvider => inner.ServiceProvider;
        public Task Disposed => _disposed.Task;

        public void Dispose()
        {
            inner.Dispose();
            _disposed.TrySetResult();
        }
    }
}
