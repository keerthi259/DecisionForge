namespace DecisionForge.Domain.Outbox;

public enum OutboxStatus
{
    Pending = 1,
    Processing = 2,
    Completed = 3,
    Failed = 4,
}
