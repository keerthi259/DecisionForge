using DecisionForge.Application.PurchaseRequests;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.PurchaseRequests;
using DecisionForge.Domain.PurchaseRequests.Events;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.UnitTests.PurchaseRequests;

public sealed class PurchaseRequestLifecycleServiceTests
{
    private static readonly Guid _newRequestId = Guid.Parse("77777777-7777-7777-8777-777777777777");
    private static readonly RequestNumber _newNumber = RequestNumber.Parse("PR-2026-000002");

    [Fact]
    public void ConstructorRejectsNullDependencies()
    {
        RecordingPurchaseRequestRepository repository = new();
        StubRequestNumberGenerator numbers = new(_newNumber);
        StubCurrentUser user = new(PurchaseRequestApplicationTestData.RequesterId);
        RequestSequenceIdGenerator ids = new(_newRequestId);
        RequestFixedTimeProvider time = new(PurchaseRequestApplicationTestData.CurrentTime);

        Assert.Throws<ArgumentNullException>(
            () => new PurchaseRequestLifecycleService(null!, numbers, user, ids, time));
        Assert.Throws<ArgumentNullException>(
            () => new PurchaseRequestLifecycleService(repository, null!, user, ids, time));
        Assert.Throws<ArgumentNullException>(
            () => new PurchaseRequestLifecycleService(repository, numbers, null!, ids, time));
        Assert.Throws<ArgumentNullException>(
            () => new PurchaseRequestLifecycleService(repository, numbers, user, null!, time));
        Assert.Throws<ArgumentNullException>(
            () => new PurchaseRequestLifecycleService(repository, numbers, user, ids, null!));
    }

    [Fact]
    public async Task CreateUsesTrustedUserAndGeneratedIdentityAndPersistsDraft()
    {
        RecordingPurchaseRequestRepository repository = new();
        StubRequestNumberGenerator numbers = new(_newNumber);
        RequestSequenceIdGenerator ids = new(_newRequestId, TokenId(10));
        PurchaseRequestLifecycleService service = CreateService(repository, numbers, ids);
        using CancellationTokenSource source = new();

        PurchaseRequest result = await service.CreateAsync(
            new CreatePurchaseRequestCommand(
                PurchaseRequestApplicationTestData.Currency,
                PurchaseRequestApplicationTestData.Metadata()),
            source.Token);

        Assert.Same(result, repository.Added);
        Assert.Equal(_newRequestId, result.Id);
        Assert.Equal(_newNumber, result.RequestNumber);
        Assert.Equal(PurchaseRequestApplicationTestData.RequesterId, result.RequesterId);
        Assert.Equal(PurchaseRequestStatus.Draft, result.Status);
        Assert.Equal(PurchaseRequestApplicationTestData.Token(10), result.ConcurrencyToken);
        Assert.Equal(1, repository.SaveCalls);
        Assert.Equal(source.Token, repository.LastCancellationToken);
        Assert.Equal(1, numbers.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task CreateRejectsMissingTrustedIdentityWithoutGeneratingData(string? userId)
    {
        RecordingPurchaseRequestRepository repository = new();
        StubRequestNumberGenerator numbers = new(_newNumber);
        RequestSequenceIdGenerator ids = new(_newRequestId, TokenId(10));
        PurchaseRequestLifecycleService service = CreateService(
            repository,
            numbers,
            ids,
            new StubCurrentUser(userId is null ? null : Guid.Parse(userId)));

        DomainRuleException exception = await Assert.ThrowsAsync<DomainRuleException>(
            () => service.CreateAsync(
                new CreatePurchaseRequestCommand(
                    PurchaseRequestApplicationTestData.Currency,
                    PurchaseRequestApplicationTestData.Metadata()),
                CancellationToken.None));

        Assert.Equal(PurchaseRequestApplicationErrorCodes.Unauthenticated, exception.Code);
        Assert.Equal(0, numbers.Calls);
        Assert.Equal(0, ids.Calls);
        Assert.Null(repository.Added);
    }

    [Fact]
    public async Task UpdateDraftUsesOwnerScopeAndRotatesConcurrencyToken()
    {
        PurchaseRequest existing = PurchaseRequestApplicationTestData.CreateRequest();
        RecordingPurchaseRequestRepository repository = new() { Existing = existing };
        PurchaseRequestLifecycleService service = CreateService(
            repository,
            new StubRequestNumberGenerator(_newNumber),
            new RequestSequenceIdGenerator(TokenId(10)));
        PurchaseRequestMetadata updated = PurchaseRequestMetadata.Create(
            PurchaseRequestApplicationTestData.DepartmentId,
            PurchaseRequestApplicationTestData.SupplierId,
            Urgency.Urgent,
            DataSensitivity.Confidential,
            new DateOnly(2026, 9, 2),
            BusinessJustification.Parse("Customer deadline requires expedited delivery."));

        PurchaseRequest result = await service.UpdateDraftAsync(
            new UpdatePurchaseRequestDraftCommand(existing.Id, updated, existing.ConcurrencyToken),
            CancellationToken.None);

        Assert.Same(existing, result);
        Assert.Equal(updated, result.Metadata);
        Assert.Equal(PurchaseRequestApplicationTestData.Token(10), result.ConcurrencyToken);
        Assert.Equal(PurchaseRequestApplicationTestData.RequesterId, repository.RequestedOwnerId);
        Assert.Equal(1, repository.SaveCalls);
    }

    [Fact]
    public async Task UpdateDraftRejectsStaleTokenWithoutMutationOrSave()
    {
        PurchaseRequest existing = PurchaseRequestApplicationTestData.CreateRequest();
        RecordingPurchaseRequestRepository repository = new() { Existing = existing };
        PurchaseRequestLifecycleService service = CreateService(
            repository,
            new StubRequestNumberGenerator(_newNumber),
            new RequestSequenceIdGenerator(TokenId(10)));

        DomainRuleException exception = await Assert.ThrowsAsync<DomainRuleException>(
            () => service.UpdateDraftAsync(
                new UpdatePurchaseRequestDraftCommand(
                    existing.Id,
                    existing.Metadata with { },
                    PurchaseRequestApplicationTestData.Token(99)),
                CancellationToken.None));

        Assert.Equal(DomainErrorCodes.ConcurrencyConflict, exception.Code);
        Assert.Equal(PurchaseRequestApplicationTestData.Token(0), existing.ConcurrencyToken);
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task IdenticalDraftUpdateDoesNotRotateOrPersist()
    {
        PurchaseRequest existing = PurchaseRequestApplicationTestData.CreateRequest();
        RecordingPurchaseRequestRepository repository = new() { Existing = existing };
        PurchaseRequestLifecycleService service = CreateService(
            repository,
            new StubRequestNumberGenerator(_newNumber),
            new RequestSequenceIdGenerator(TokenId(10)));

        PurchaseRequest result = await service.UpdateDraftAsync(
            new UpdatePurchaseRequestDraftCommand(
                existing.Id,
                existing.Metadata,
                existing.ConcurrencyToken),
            CancellationToken.None);

        Assert.Same(existing, result);
        Assert.Equal(PurchaseRequestApplicationTestData.Token(0), result.ConcurrencyToken);
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task NonOwnerReceivesNonDisclosingNotFoundError()
    {
        PurchaseRequest existing = PurchaseRequestApplicationTestData.CreateRequest();
        RecordingPurchaseRequestRepository repository = new() { Existing = existing };
        Guid otherUser = Guid.Parse("88888888-8888-7888-8888-888888888888");
        PurchaseRequestLifecycleService service = CreateService(
            repository,
            new StubRequestNumberGenerator(_newNumber),
            new RequestSequenceIdGenerator(TokenId(10)),
            new StubCurrentUser(otherUser));

        DomainRuleException exception = await Assert.ThrowsAsync<DomainRuleException>(
            () => service.UpdateDraftAsync(
                new UpdatePurchaseRequestDraftCommand(
                    existing.Id,
                    existing.Metadata,
                    existing.ConcurrencyToken),
                CancellationToken.None));

        Assert.Equal(PurchaseRequestApplicationErrorCodes.NotFound, exception.Code);
        Assert.Equal(otherUser, repository.RequestedOwnerId);
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task ItemCommandsCalculateTotalsOnServerAndRotateTokens()
    {
        PurchaseRequest existing = PurchaseRequestApplicationTestData.CreateRequest();
        RecordingPurchaseRequestRepository repository = new() { Existing = existing };
        Guid itemId = PurchaseRequestApplicationTestData.ItemId(2);
        PurchaseRequestLifecycleService service = CreateService(
            repository,
            new StubRequestNumberGenerator(_newNumber),
            new RequestSequenceIdGenerator(
                itemId,
                TokenId(10),
                TokenId(11),
                TokenId(12)));

        PurchaseRequestItem item = await service.AddItemAsync(
            new AddPurchaseRequestItemCommand(
                existing.Id,
                "Laptop",
                3,
                Money.Create(100.25m, existing.Currency),
                ProcurementCategory.Hardware,
                existing.ConcurrencyToken),
            CancellationToken.None);
        Assert.Equal(300.75m, existing.Total.Amount);

        item = await service.UpdateItemAsync(
            new UpdatePurchaseRequestItemCommand(
                existing.Id,
                item.Id,
                "Laptop and dock",
                2,
                Money.Create(125m, existing.Currency),
                ProcurementCategory.Hardware,
                existing.ConcurrencyToken),
            CancellationToken.None);
        Assert.Equal(250m, item.LineTotal.Amount);
        Assert.Equal(250m, existing.Total.Amount);

        await service.RemoveItemAsync(
            new RemovePurchaseRequestItemCommand(
                existing.Id,
                item.Id,
                existing.ConcurrencyToken),
            CancellationToken.None);
        Assert.Equal(0m, existing.Total.Amount);
        Assert.Empty(existing.Items);
        Assert.Equal(3, repository.SaveCalls);
    }

    [Fact]
    public async Task SubmittedRequestCannotBeEditedThroughItemCommand()
    {
        PurchaseRequest existing = PurchaseRequestApplicationTestData.CreateRequest(withItem: true);
        existing.Submit(
            existing.ConcurrencyToken,
            PurchaseRequestApplicationTestData.Token(10),
            PurchaseRequestApplicationTestData.CurrentTime);
        RecordingPurchaseRequestRepository repository = new() { Existing = existing };
        PurchaseRequestLifecycleService service = CreateService(
            repository,
            new StubRequestNumberGenerator(_newNumber),
            new RequestSequenceIdGenerator(PurchaseRequestApplicationTestData.ItemId(2), TokenId(11)));

        DomainRuleException exception = await Assert.ThrowsAsync<DomainRuleException>(
            () => service.AddItemAsync(
                new AddPurchaseRequestItemCommand(
                    existing.Id,
                    "Extra",
                    1,
                    Money.Create(1m, existing.Currency),
                    ProcurementCategory.Other,
                    existing.ConcurrencyToken),
                CancellationToken.None));

        Assert.Equal(DomainErrorCodes.InvalidState, exception.Code);
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task InvalidItemInputDoesNotChangeTotalOrPersist()
    {
        PurchaseRequest existing = PurchaseRequestApplicationTestData.CreateRequest();
        RecordingPurchaseRequestRepository repository = new() { Existing = existing };
        PurchaseRequestLifecycleService service = CreateService(
            repository,
            new StubRequestNumberGenerator(_newNumber),
            new RequestSequenceIdGenerator(PurchaseRequestApplicationTestData.ItemId(2), TokenId(10)));

        DomainRuleException exception = await Assert.ThrowsAsync<DomainRuleException>(
            () => service.AddItemAsync(
                new AddPurchaseRequestItemCommand(
                    existing.Id,
                    "Invalid",
                    0,
                    Money.Create(1m, existing.Currency),
                    ProcurementCategory.Other,
                    existing.ConcurrencyToken),
                CancellationToken.None));

        Assert.Equal(DomainErrorCodes.Validation, exception.Code);
        Assert.Equal(0m, existing.Total.Amount);
        Assert.Empty(existing.Items);
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task WithdrawRequiresOwnedSubmittedRequestAndExpectedToken()
    {
        PurchaseRequest existing = PurchaseRequestApplicationTestData.CreateRequest(withItem: true);
        existing.Submit(
            existing.ConcurrencyToken,
            PurchaseRequestApplicationTestData.Token(10),
            PurchaseRequestApplicationTestData.CurrentTime);
        existing.ClearDomainEvents();
        RecordingPurchaseRequestRepository repository = new() { Existing = existing };
        PurchaseRequestLifecycleService service = CreateService(
            repository,
            new StubRequestNumberGenerator(_newNumber),
            new RequestSequenceIdGenerator(TokenId(11)));

        PurchaseRequest result = await service.WithdrawAsync(
            new WithdrawPurchaseRequestCommand(existing.Id, existing.ConcurrencyToken),
            CancellationToken.None);

        Assert.Equal(PurchaseRequestStatus.Withdrawn, result.Status);
        Assert.Equal(PurchaseRequestApplicationTestData.Token(11), result.ConcurrencyToken);
        Assert.IsType<PurchaseRequestWithdrawnDomainEvent>(Assert.Single(result.DomainEvents));
        Assert.Equal(1, repository.SaveCalls);
    }

    [Fact]
    public async Task CloneCopiesOwnedSnapshotIntoNewDraftWithNewIdentities()
    {
        PurchaseRequest source = PurchaseRequestApplicationTestData.CreateRequest(withItem: true);
        RecordingPurchaseRequestRepository repository = new() { Existing = source };
        Guid clonedItemId = PurchaseRequestApplicationTestData.ItemId(2);
        PurchaseRequestLifecycleService service = CreateService(
            repository,
            new StubRequestNumberGenerator(_newNumber),
            new RequestSequenceIdGenerator(_newRequestId, TokenId(10), clonedItemId));

        PurchaseRequest clone = await service.CloneAsync(
            new ClonePurchaseRequestCommand(source.Id, source.ConcurrencyToken),
            CancellationToken.None);

        Assert.Same(clone, repository.Added);
        Assert.Equal(_newRequestId, clone.Id);
        Assert.Equal(PurchaseRequestStatus.Draft, clone.Status);
        Assert.Equal(source.RequesterId, clone.RequesterId);
        Assert.Equal(source.Total, clone.Total);
        Assert.Equal(clonedItemId, Assert.Single(clone.Items).Id);
        Assert.NotEqual(source.Items[0].Id, clone.Items[0].Id);
        Assert.Equal(1, repository.SaveCalls);
    }

    [Fact]
    public async Task CloneRejectsStaleSourceBeforeGeneratingIdentity()
    {
        PurchaseRequest source = PurchaseRequestApplicationTestData.CreateRequest(withItem: true);
        RecordingPurchaseRequestRepository repository = new() { Existing = source };
        StubRequestNumberGenerator numbers = new(_newNumber);
        RequestSequenceIdGenerator ids = new(PurchaseRequestApplicationTestData.ItemId(2));
        PurchaseRequestLifecycleService service = CreateService(repository, numbers, ids);

        DomainRuleException exception = await Assert.ThrowsAsync<DomainRuleException>(
            () => service.CloneAsync(
                new ClonePurchaseRequestCommand(
                    source.Id,
                    PurchaseRequestApplicationTestData.Token(99)),
                CancellationToken.None));

        Assert.Equal(DomainErrorCodes.ConcurrencyConflict, exception.Code);
        Assert.Equal(0, numbers.Calls);
        Assert.Equal(0, ids.Calls);
        Assert.Null(repository.Added);
    }

    [Fact]
    public async Task PreCancelledCommandDoesNotAccessContextOrRepository()
    {
        RecordingPurchaseRequestRepository repository = new();
        StubRequestNumberGenerator numbers = new(_newNumber);
        PurchaseRequestLifecycleService service = CreateService(
            repository,
            numbers,
            new RequestSequenceIdGenerator(_newRequestId, TokenId(10)));
        using CancellationTokenSource source = new();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.CreateAsync(
                new CreatePurchaseRequestCommand(
                    PurchaseRequestApplicationTestData.Currency,
                    PurchaseRequestApplicationTestData.Metadata()),
                source.Token));

        Assert.Equal(0, numbers.Calls);
        Assert.Equal(0, repository.FindCalls);
    }

    private static PurchaseRequestLifecycleService CreateService(
        RecordingPurchaseRequestRepository repository,
        StubRequestNumberGenerator numbers,
        RequestSequenceIdGenerator ids,
        StubCurrentUser? currentUser = null)
    {
        return new PurchaseRequestLifecycleService(
            repository,
            numbers,
            currentUser ?? new StubCurrentUser(PurchaseRequestApplicationTestData.RequesterId),
            ids,
            new RequestFixedTimeProvider(PurchaseRequestApplicationTestData.CurrentTime));
    }

    private static Guid TokenId(int sequence)
    {
        return PurchaseRequestApplicationTestData.Token(sequence).Value;
    }
}
