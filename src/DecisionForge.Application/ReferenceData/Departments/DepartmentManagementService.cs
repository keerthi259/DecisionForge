using DecisionForge.Application.Platform;
using DecisionForge.Application.ReferenceData.Ports;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.ReferenceData;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.ReferenceData.Departments;

public sealed class DepartmentManagementService
{
    private readonly IDepartmentRepository _repository;
    private readonly IIdGenerator _idGenerator;
    private readonly TimeProvider _timeProvider;

    public DepartmentManagementService(
        IDepartmentRepository repository,
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

    public async Task<Department> CreateAsync(
        CreateDepartmentCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        if (await _repository.CodeExistsAsync(command.Code, cancellationToken))
        {
            throw new DomainRuleException(
                DomainErrorCodes.DuplicateEntity,
                $"Department code '{command.Code}' already exists.",
                nameof(command.Code));
        }

        Department department = Department.Create(
            _idGenerator.Create(),
            command.Code,
            command.Name,
            command.AutoApprovalLimit,
            NextToken(),
            _timeProvider.GetUtcNow());
        await _repository.AddAsync(department, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return department;
    }

    public async Task<Department> UpdateAsync(
        UpdateDepartmentCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        Department department = await FindRequiredAsync(command.DepartmentId, cancellationToken);
        ConcurrencyToken previousToken = department.ConcurrencyToken;
        department.UpdateDetails(
            command.Name,
            command.AutoApprovalLimit,
            command.ExpectedToken,
            NextToken(),
            _timeProvider.GetUtcNow());
        await SaveIfChangedAsync(department, previousToken, cancellationToken);
        return department;
    }

    public async Task<Department> SetActiveAsync(
        SetDepartmentActiveCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        Department department = await FindRequiredAsync(command.DepartmentId, cancellationToken);
        department.SetActive(
            command.IsActive,
            command.ExpectedToken,
            NextToken(),
            _timeProvider.GetUtcNow());
        await _repository.SaveChangesAsync(cancellationToken);
        return department;
    }

    private async Task<Department> FindRequiredAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        Department? department = await _repository.FindByIdAsync(id, cancellationToken);
        return department
            ?? throw new DomainRuleException(
                DomainErrorCodes.EntityNotFound,
                $"Department '{id}' was not found.",
                nameof(id));
    }

    private async Task SaveIfChangedAsync(
        Department department,
        ConcurrencyToken previousToken,
        CancellationToken cancellationToken)
    {
        if (department.ConcurrencyToken != previousToken)
        {
            await _repository.SaveChangesAsync(cancellationToken);
        }
    }

    private ConcurrencyToken NextToken()
    {
        return ConcurrencyToken.Create(_idGenerator.Create());
    }
}
