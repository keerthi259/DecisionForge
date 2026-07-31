using DecisionForge.Application.ReferenceData.Departments;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.ReferenceData;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.UnitTests.ReferenceData;

public sealed class DepartmentManagementServiceTests
{
    private static readonly Guid _departmentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid _initialTokenId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid _nextTokenId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset _initialTime = new(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _currentTime = _initialTime.AddHours(1);

    [Fact]
    public void ConstructorRejectsNullDependencies()
    {
        RecordingDepartmentRepository repository = new();
        SequenceIdGenerator ids = new(_departmentId);
        FixedTimeProvider time = new(_currentTime);

        Assert.Throws<ArgumentNullException>(
            () => new DepartmentManagementService(null!, ids, time));
        Assert.Throws<ArgumentNullException>(
            () => new DepartmentManagementService(repository, null!, time));
        Assert.Throws<ArgumentNullException>(
            () => new DepartmentManagementService(repository, ids, null!));
    }

    [Fact]
    public async Task CreatePersistsValidatedDepartmentAndPropagatesCancellationToken()
    {
        RecordingDepartmentRepository repository = new();
        SequenceIdGenerator ids = new(_departmentId, _initialTokenId);
        DepartmentManagementService service = CreateService(repository, ids);
        using CancellationTokenSource source = new();

        Department result = await service.CreateAsync(
            new CreateDepartmentCommand(Code(), " Engineering ", Limit(250_000m)),
            source.Token);

        Assert.Same(result, repository.Added);
        Assert.Equal(_departmentId, result.Id);
        Assert.Equal("Engineering", result.Name);
        Assert.Equal(_initialTokenId, result.ConcurrencyToken.Value);
        Assert.True(result.IsActive);
        Assert.Equal(1, repository.ExistenceCalls);
        Assert.Equal(1, repository.SaveCalls);
        Assert.Equal(source.Token, repository.LastCancellationToken);
        Assert.Equal(2, ids.Calls);
    }

    [Fact]
    public async Task CreateRejectsDuplicateCodeWithoutGeneratingOrPersisting()
    {
        RecordingDepartmentRepository repository = new() { CodeExists = true };
        SequenceIdGenerator ids = new(_departmentId, _initialTokenId);
        DepartmentManagementService service = CreateService(repository, ids);

        DomainRuleException exception = await Assert.ThrowsAsync<DomainRuleException>(
            () => service.CreateAsync(
                new CreateDepartmentCommand(Code(), "Engineering", Limit(250_000m)),
                CancellationToken.None));

        Assert.Equal(DomainErrorCodes.DuplicateEntity, exception.Code);
        Assert.Null(repository.Added);
        Assert.Equal(0, repository.SaveCalls);
        Assert.Equal(0, ids.Calls);
    }

    [Fact]
    public async Task UpdatePersistsAChangedDepartmentWithNewConcurrencyToken()
    {
        Department existing = Existing();
        RecordingDepartmentRepository repository = new() { Existing = existing };
        SequenceIdGenerator ids = new(_nextTokenId);
        DepartmentManagementService service = CreateService(repository, ids);

        Department result = await service.UpdateAsync(
            new UpdateDepartmentCommand(
                _departmentId,
                "Platform Engineering",
                Limit(300_000m),
                existing.ConcurrencyToken),
            CancellationToken.None);

        Assert.Same(existing, result);
        Assert.Equal("Platform Engineering", result.Name);
        Assert.Equal(300_000m, result.AutoApprovalLimit.Amount);
        Assert.Equal(_nextTokenId, result.ConcurrencyToken.Value);
        Assert.Equal(1, repository.SaveCalls);
    }

    [Fact]
    public async Task UpdateDoesNotPersistAnUnchangedDepartment()
    {
        Department existing = Existing();
        RecordingDepartmentRepository repository = new() { Existing = existing };
        SequenceIdGenerator ids = new(_nextTokenId);
        DepartmentManagementService service = CreateService(repository, ids);

        Department result = await service.UpdateAsync(
            new UpdateDepartmentCommand(
                _departmentId,
                existing.Name,
                existing.AutoApprovalLimit,
                existing.ConcurrencyToken),
            CancellationToken.None);

        Assert.Same(existing, result);
        Assert.Equal(_initialTokenId, result.ConcurrencyToken.Value);
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task UpdateRejectsStaleConcurrencyTokenWithoutPersisting()
    {
        Department existing = Existing();
        RecordingDepartmentRepository repository = new() { Existing = existing };
        DepartmentManagementService service = CreateService(
            repository,
            new SequenceIdGenerator(_nextTokenId));

        DomainRuleException exception = await Assert.ThrowsAsync<DomainRuleException>(
            () => service.UpdateAsync(
                new UpdateDepartmentCommand(
                    _departmentId,
                    "Changed",
                    Limit(300_000m),
                    ConcurrencyToken.Create(Guid.Parse("44444444-4444-4444-4444-444444444444"))),
                CancellationToken.None));

        Assert.Equal(DomainErrorCodes.ConcurrencyConflict, exception.Code);
        Assert.Equal(0, repository.SaveCalls);
        Assert.Equal("Engineering", existing.Name);
    }

    [Fact]
    public async Task SetActivePersistsTransitionAndRejectsRepeatedTransition()
    {
        Department existing = Existing();
        RecordingDepartmentRepository repository = new() { Existing = existing };
        SequenceIdGenerator ids = new(
            _nextTokenId,
            Guid.Parse("55555555-5555-5555-5555-555555555555"));
        DepartmentManagementService service = CreateService(repository, ids);

        await service.SetActiveAsync(
            new SetDepartmentActiveCommand(_departmentId, false, existing.ConcurrencyToken),
            CancellationToken.None);
        DomainRuleException exception = await Assert.ThrowsAsync<DomainRuleException>(
            () => service.SetActiveAsync(
                new SetDepartmentActiveCommand(_departmentId, false, existing.ConcurrencyToken),
                CancellationToken.None));

        Assert.False(existing.IsActive);
        Assert.Equal(1, repository.SaveCalls);
        Assert.Equal(DomainErrorCodes.InvalidState, exception.Code);
    }

    [Fact]
    public async Task MissingDepartmentReturnsStableNotFoundError()
    {
        RecordingDepartmentRepository repository = new();
        DepartmentManagementService service = CreateService(
            repository,
            new SequenceIdGenerator(_nextTokenId));

        DomainRuleException exception = await Assert.ThrowsAsync<DomainRuleException>(
            () => service.UpdateAsync(
                new UpdateDepartmentCommand(
                    _departmentId,
                    "Engineering",
                    Limit(1m),
                    ConcurrencyToken.Create(_initialTokenId)),
                CancellationToken.None));

        Assert.Equal(DomainErrorCodes.EntityNotFound, exception.Code);
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task PreCancelledRequestDoesNotCallRepository()
    {
        RecordingDepartmentRepository repository = new();
        DepartmentManagementService service = CreateService(
            repository,
            new SequenceIdGenerator(_departmentId, _initialTokenId));
        using CancellationTokenSource source = new();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.CreateAsync(
                new CreateDepartmentCommand(Code(), "Engineering", Limit(1m)),
                source.Token));

        Assert.Equal(0, repository.ExistenceCalls);
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task NullCommandIsRejected()
    {
        DepartmentManagementService service = CreateService(
            new RecordingDepartmentRepository(),
            new SequenceIdGenerator(_departmentId, _initialTokenId));

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.CreateAsync(null!, CancellationToken.None));
    }

    private static DepartmentManagementService CreateService(
        RecordingDepartmentRepository repository,
        SequenceIdGenerator ids)
    {
        return new DepartmentManagementService(
            repository,
            ids,
            new FixedTimeProvider(_currentTime));
    }

    private static Department Existing()
    {
        return Department.Create(
            _departmentId,
            Code(),
            "Engineering",
            Limit(250_000m),
            ConcurrencyToken.Create(_initialTokenId),
            _initialTime);
    }

    private static DepartmentCode Code()
    {
        return DepartmentCode.Parse("ENG");
    }

    private static Money Limit(decimal amount)
    {
        return Money.Create(amount, CurrencyCode.Parse("INR"));
    }
}
