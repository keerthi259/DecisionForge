using DecisionForge.Application.Platform;
using DecisionForge.Application.ReferenceData.Ports;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.ReferenceData;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.ReferenceData.Suppliers;

public sealed class SupplierManagementService
{
    private readonly ISupplierRepository _repository;
    private readonly IIdGenerator _idGenerator;
    private readonly TimeProvider _timeProvider;

    public SupplierManagementService(
        ISupplierRepository repository,
        IIdGenerator idGenerator,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(idGenerator);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _repository = repository;
        _idGenerator = idGenerator;
        _timeProvider = timeProvider;
    }

    public async Task<Supplier> CreateAsync(
        CreateSupplierCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        if (await _repository.RegistrationNumberExistsAsync(
                command.RegistrationNumber,
                cancellationToken))
        {
            throw new DomainRuleException(
                DomainErrorCodes.DuplicateEntity,
                $"Supplier registration '{command.RegistrationNumber}' already exists.",
                nameof(command.RegistrationNumber));
        }

        Supplier supplier = Supplier.Create(
            _idGenerator.Create(),
            command.RegistrationNumber,
            command.Name,
            command.ApprovalStatus,
            command.OnboardingStatus,
            command.RiskRating,
            NextToken(),
            _timeProvider.GetUtcNow());
        await _repository.AddAsync(supplier, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return supplier;
    }

    public async Task<Supplier> UpdateAsync(
        UpdateSupplierCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        Supplier supplier = await FindRequiredAsync(command.SupplierId, cancellationToken);
        ConcurrencyToken previousToken = supplier.ConcurrencyToken;
        supplier.UpdateDetails(
            command.Name,
            command.ApprovalStatus,
            command.OnboardingStatus,
            command.RiskRating,
            command.ExpectedToken,
            NextToken(),
            _timeProvider.GetUtcNow());
        await SaveIfChangedAsync(supplier, previousToken, cancellationToken);
        return supplier;
    }

    public async Task<Supplier> SetActiveAsync(
        SetSupplierActiveCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        Supplier supplier = await FindRequiredAsync(command.SupplierId, cancellationToken);
        supplier.SetActive(
            command.IsActive,
            command.ExpectedToken,
            NextToken(),
            _timeProvider.GetUtcNow());
        await _repository.SaveChangesAsync(cancellationToken);
        return supplier;
    }

    private async Task<Supplier> FindRequiredAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        Supplier? supplier = await _repository.FindByIdAsync(id, cancellationToken);
        return supplier
            ?? throw new DomainRuleException(
                DomainErrorCodes.EntityNotFound,
                $"Supplier '{id}' was not found.",
                nameof(id));
    }

    private async Task SaveIfChangedAsync(
        Supplier supplier,
        ConcurrencyToken previousToken,
        CancellationToken cancellationToken)
    {
        if (supplier.ConcurrencyToken != previousToken)
        {
            await _repository.SaveChangesAsync(cancellationToken);
        }
    }

    private ConcurrencyToken NextToken()
    {
        return ConcurrencyToken.Create(_idGenerator.Create());
    }
}
