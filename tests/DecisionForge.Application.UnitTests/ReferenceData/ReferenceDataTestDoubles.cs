using DecisionForge.Application.Platform;
using DecisionForge.Application.ReferenceData.Ports;
using DecisionForge.Domain.ReferenceData;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.UnitTests.ReferenceData;

internal sealed class SequenceIdGenerator(params Guid[] values) : IIdGenerator
{
    private readonly Queue<Guid> _values = new(values);

    public int Calls { get; private set; }

    public Guid Create()
    {
        Calls++;
        return _values.Dequeue();
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow()
    {
        return utcNow;
    }
}

internal sealed class RecordingDepartmentRepository : IDepartmentRepository
{
    public Department? Existing { get; set; }

    public bool CodeExists { get; set; }

    public Department? Added { get; private set; }

    public int FindCalls { get; private set; }

    public int ExistenceCalls { get; private set; }

    public int SaveCalls { get; private set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public Task<Department?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        FindCalls++;
        LastCancellationToken = cancellationToken;
        return Task.FromResult(Existing?.Id == id ? Existing : null);
    }

    public Task<bool> CodeExistsAsync(
        DepartmentCode code,
        CancellationToken cancellationToken)
    {
        ExistenceCalls++;
        LastCancellationToken = cancellationToken;
        return Task.FromResult(CodeExists);
    }

    public Task AddAsync(Department department, CancellationToken cancellationToken)
    {
        Added = department;
        LastCancellationToken = cancellationToken;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCalls++;
        LastCancellationToken = cancellationToken;
        return Task.CompletedTask;
    }
}

internal sealed class RecordingSupplierRepository : ISupplierRepository
{
    public Supplier? Existing { get; set; }

    public bool RegistrationNumberExists { get; set; }

    public Supplier? Added { get; private set; }

    public int FindCalls { get; private set; }

    public int ExistenceCalls { get; private set; }

    public int SaveCalls { get; private set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public Task<Supplier?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        FindCalls++;
        LastCancellationToken = cancellationToken;
        return Task.FromResult(Existing?.Id == id ? Existing : null);
    }

    public Task<bool> RegistrationNumberExistsAsync(
        SupplierRegistrationNumber registrationNumber,
        CancellationToken cancellationToken)
    {
        ExistenceCalls++;
        LastCancellationToken = cancellationToken;
        return Task.FromResult(RegistrationNumberExists);
    }

    public Task AddAsync(Supplier supplier, CancellationToken cancellationToken)
    {
        Added = supplier;
        LastCancellationToken = cancellationToken;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCalls++;
        LastCancellationToken = cancellationToken;
        return Task.CompletedTask;
    }
}
