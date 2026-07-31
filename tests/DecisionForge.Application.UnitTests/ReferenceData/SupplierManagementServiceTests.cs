using DecisionForge.Application.ReferenceData.Suppliers;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.ReferenceData;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.UnitTests.ReferenceData;

public sealed class SupplierManagementServiceTests
{
    private static readonly Guid _supplierId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid _initialTokenId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid _nextTokenId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly DateTimeOffset _initialTime = new(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _currentTime = _initialTime.AddHours(1);

    [Fact]
    public void ConstructorRejectsNullDependencies()
    {
        RecordingSupplierRepository repository = new();
        SequenceIdGenerator ids = new(_supplierId);
        FixedTimeProvider time = new(_currentTime);

        Assert.Throws<ArgumentNullException>(
            () => new SupplierManagementService(null!, ids, time));
        Assert.Throws<ArgumentNullException>(
            () => new SupplierManagementService(repository, null!, time));
        Assert.Throws<ArgumentNullException>(
            () => new SupplierManagementService(repository, ids, null!));
    }

    [Fact]
    public async Task CreatePersistsValidatedSupplierAndPropagatesCancellationToken()
    {
        RecordingSupplierRepository repository = new();
        SequenceIdGenerator ids = new(_supplierId, _initialTokenId);
        SupplierManagementService service = CreateService(repository, ids);
        using CancellationTokenSource source = new();

        Supplier result = await service.CreateAsync(
            new CreateSupplierCommand(
                Registration(),
                " Acme India ",
                SupplierApprovalStatus.Approved,
                SupplierOnboardingStatus.Completed,
                SupplierRiskRating.Low),
            source.Token);

        Assert.Same(result, repository.Added);
        Assert.Equal(_supplierId, result.Id);
        Assert.Equal("Acme India", result.Name);
        Assert.Equal(_initialTokenId, result.ConcurrencyToken.Value);
        Assert.Equal(1, repository.ExistenceCalls);
        Assert.Equal(1, repository.SaveCalls);
        Assert.Equal(source.Token, repository.LastCancellationToken);
        Assert.Equal(2, ids.Calls);
    }

    [Fact]
    public async Task CreateRejectsDuplicateRegistrationWithoutGeneratingOrPersisting()
    {
        RecordingSupplierRepository repository = new() { RegistrationNumberExists = true };
        SequenceIdGenerator ids = new(_supplierId, _initialTokenId);
        SupplierManagementService service = CreateService(repository, ids);

        DomainRuleException exception = await Assert.ThrowsAsync<DomainRuleException>(
            () => service.CreateAsync(
                new CreateSupplierCommand(
                    Registration(),
                    "Acme",
                    SupplierApprovalStatus.Pending,
                    SupplierOnboardingStatus.InProgress,
                    SupplierRiskRating.Medium),
                CancellationToken.None));

        Assert.Equal(DomainErrorCodes.DuplicateEntity, exception.Code);
        Assert.Null(repository.Added);
        Assert.Equal(0, repository.SaveCalls);
        Assert.Equal(0, ids.Calls);
    }

    [Fact]
    public async Task UpdatePersistsChangedSupplierState()
    {
        Supplier existing = Existing();
        RecordingSupplierRepository repository = new() { Existing = existing };
        SupplierManagementService service = CreateService(
            repository,
            new SequenceIdGenerator(_nextTokenId));

        Supplier result = await service.UpdateAsync(
            new UpdateSupplierCommand(
                _supplierId,
                "Acme Procurement",
                SupplierApprovalStatus.Suspended,
                SupplierOnboardingStatus.Suspended,
                SupplierRiskRating.Critical,
                existing.ConcurrencyToken),
            CancellationToken.None);

        Assert.Same(existing, result);
        Assert.Equal("Acme Procurement", result.Name);
        Assert.Equal(SupplierApprovalStatus.Suspended, result.ApprovalStatus);
        Assert.Equal(SupplierOnboardingStatus.Suspended, result.OnboardingStatus);
        Assert.Equal(SupplierRiskRating.Critical, result.RiskRating);
        Assert.Equal(_nextTokenId, result.ConcurrencyToken.Value);
        Assert.Equal(1, repository.SaveCalls);
    }

    [Fact]
    public async Task UpdateDoesNotPersistUnchangedSupplier()
    {
        Supplier existing = Existing();
        RecordingSupplierRepository repository = new() { Existing = existing };
        SupplierManagementService service = CreateService(
            repository,
            new SequenceIdGenerator(_nextTokenId));

        Supplier result = await service.UpdateAsync(
            new UpdateSupplierCommand(
                _supplierId,
                existing.Name,
                existing.ApprovalStatus,
                existing.OnboardingStatus,
                existing.RiskRating,
                existing.ConcurrencyToken),
            CancellationToken.None);

        Assert.Same(existing, result);
        Assert.Equal(_initialTokenId, result.ConcurrencyToken.Value);
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task UpdateRejectsStaleTokenWithoutPersisting()
    {
        Supplier existing = Existing();
        RecordingSupplierRepository repository = new() { Existing = existing };
        SupplierManagementService service = CreateService(
            repository,
            new SequenceIdGenerator(_nextTokenId));

        DomainRuleException exception = await Assert.ThrowsAsync<DomainRuleException>(
            () => service.UpdateAsync(
                new UpdateSupplierCommand(
                    _supplierId,
                    "Changed",
                    SupplierApprovalStatus.Rejected,
                    SupplierOnboardingStatus.Suspended,
                    SupplierRiskRating.High,
                    ConcurrencyToken.Create(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"))),
                CancellationToken.None));

        Assert.Equal(DomainErrorCodes.ConcurrencyConflict, exception.Code);
        Assert.Equal(0, repository.SaveCalls);
        Assert.Equal("Acme", existing.Name);
    }

    [Fact]
    public async Task SetActivePersistsTransitionAndRejectsRepeatedTransition()
    {
        Supplier existing = Existing();
        RecordingSupplierRepository repository = new() { Existing = existing };
        SupplierManagementService service = CreateService(
            repository,
            new SequenceIdGenerator(
                _nextTokenId,
                Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee")));

        await service.SetActiveAsync(
            new SetSupplierActiveCommand(_supplierId, false, existing.ConcurrencyToken),
            CancellationToken.None);
        DomainRuleException exception = await Assert.ThrowsAsync<DomainRuleException>(
            () => service.SetActiveAsync(
                new SetSupplierActiveCommand(_supplierId, false, existing.ConcurrencyToken),
                CancellationToken.None));

        Assert.False(existing.IsActive);
        Assert.Equal(1, repository.SaveCalls);
        Assert.Equal(DomainErrorCodes.InvalidState, exception.Code);
    }

    [Fact]
    public async Task MissingSupplierReturnsStableNotFoundError()
    {
        RecordingSupplierRepository repository = new();
        SupplierManagementService service = CreateService(
            repository,
            new SequenceIdGenerator(_nextTokenId));

        DomainRuleException exception = await Assert.ThrowsAsync<DomainRuleException>(
            () => service.UpdateAsync(
                new UpdateSupplierCommand(
                    _supplierId,
                    "Acme",
                    SupplierApprovalStatus.Approved,
                    SupplierOnboardingStatus.Completed,
                    SupplierRiskRating.Low,
                    ConcurrencyToken.Create(_initialTokenId)),
                CancellationToken.None));

        Assert.Equal(DomainErrorCodes.EntityNotFound, exception.Code);
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task PreCancelledRequestDoesNotCallRepository()
    {
        RecordingSupplierRepository repository = new();
        SupplierManagementService service = CreateService(
            repository,
            new SequenceIdGenerator(_supplierId, _initialTokenId));
        using CancellationTokenSource source = new();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.CreateAsync(
                new CreateSupplierCommand(
                    Registration(),
                    "Acme",
                    SupplierApprovalStatus.Pending,
                    SupplierOnboardingStatus.NotStarted,
                    SupplierRiskRating.Low),
                source.Token));

        Assert.Equal(0, repository.ExistenceCalls);
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task NullCommandIsRejected()
    {
        SupplierManagementService service = CreateService(
            new RecordingSupplierRepository(),
            new SequenceIdGenerator(_supplierId, _initialTokenId));

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.CreateAsync(null!, CancellationToken.None));
    }

    private static SupplierManagementService CreateService(
        RecordingSupplierRepository repository,
        SequenceIdGenerator ids)
    {
        return new SupplierManagementService(
            repository,
            ids,
            new FixedTimeProvider(_currentTime));
    }

    private static Supplier Existing()
    {
        return Supplier.Create(
            _supplierId,
            Registration(),
            "Acme",
            SupplierApprovalStatus.Approved,
            SupplierOnboardingStatus.Completed,
            SupplierRiskRating.Low,
            ConcurrencyToken.Create(_initialTokenId),
            _initialTime);
    }

    private static SupplierRegistrationNumber Registration()
    {
        return SupplierRegistrationNumber.Parse("SUP-0001");
    }
}
