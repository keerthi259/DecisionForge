using System.Collections.ObjectModel;
using DecisionForge.Domain.EvaluationFacts;

namespace DecisionForge.Domain.Policies.Evaluation;

public sealed record PolicyFactSet
{
    private readonly ReadOnlyDictionary<string, PolicyFact> _byPath;

    private PolicyFactSet(IEnumerable<PolicyFact> facts)
    {
        PolicyFact[] ordered = facts.OrderBy(fact => fact.Path, StringComparer.Ordinal).ToArray();
        Facts = new ReadOnlyCollection<PolicyFact>(ordered);
        _byPath = new ReadOnlyDictionary<string, PolicyFact>(
            ordered.ToDictionary(fact => fact.Path, StringComparer.Ordinal));
    }

    public IReadOnlyList<PolicyFact> Facts { get; }

    public static PolicyFactSet Create(IEnumerable<PolicyFact> facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        List<PolicyFact> materialized = [];
        HashSet<string> paths = new(StringComparer.Ordinal);
        foreach (PolicyFact fact in facts)
        {
            ArgumentNullException.ThrowIfNull(fact);
            if (!paths.Add(fact.Path))
            {
                throw new PolicyEvaluationException(
                    PolicyEvaluationErrorCodes.DuplicateFact,
                    fact.Path,
                    "Evaluation fact paths must be unique.");
            }

            materialized.Add(fact);
        }

        return new PolicyFactSet(materialized);
    }

    public static PolicyFactSet FromSnapshot(EvaluationFactSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return Create(
        [
            PolicyFact.DecimalNumber("request.totalAmount", snapshot.Request.TotalAmount),
            PolicyFact.Text("request.currency", snapshot.Request.Currency.Value),
            PolicyFact.ControlledText("request.category", snapshot.Request.Category.ToString()),
            PolicyFact.ControlledText("request.urgency", snapshot.Request.Urgency.ToString()),
            PolicyFact.ControlledText(
                "request.dataSensitivity",
                snapshot.Request.DataSensitivity.ToString()),
            PolicyFact.WholeNumber("request.itemCount", snapshot.Request.ItemCount),
            PolicyFact.WholeNumber(
                "request.expectedDeliveryDays",
                snapshot.Request.ExpectedDeliveryDays),
            PolicyFact.Logical(
                "request.hasBusinessJustification",
                snapshot.Request.HasBusinessJustification),
            PolicyFact.Text("department.code", snapshot.Department.Code.Value),
            PolicyFact.DecimalNumber(
                "department.autoApprovalLimit",
                snapshot.Department.AutoApprovalLimit),
            PolicyFact.Logical("supplier.isApproved", snapshot.Supplier.IsApproved),
            PolicyFact.ControlledText(
                "supplier.onboardingStatus",
                snapshot.Supplier.OnboardingStatus.ToString()),
            PolicyFact.ControlledText(
                "supplier.riskRating",
                snapshot.Supplier.RiskRating.ToString()),
            PolicyFact.Logical("supplier.isActive", snapshot.Supplier.IsActive),
            PolicyFact.Logical(
                "derived.containsTechnologyPurchase",
                snapshot.Derived.ContainsTechnologyPurchase),
            PolicyFact.Logical(
                "derived.requiresUrgencyException",
                snapshot.Derived.RequiresUrgencyException),
        ]);
    }

    internal bool TryGet(string path, out PolicyFact fact)
    {
        return _byPath.TryGetValue(path, out fact!);
    }
}
