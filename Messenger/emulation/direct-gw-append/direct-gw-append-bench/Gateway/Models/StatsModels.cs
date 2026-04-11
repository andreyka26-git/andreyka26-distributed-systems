namespace Gateway.Models;

public record LatencyStats(
    int    OperationsPerformed,
    double P50Ms,
    double P99Ms,
    double MinMs,
    double MaxMs
);

public record DistributionStats(
    double P50,
    double P99
);

public record ChatStats(
    int              TotalChats,
    DistributionStats UsersPerChat,
    long             TotalMessagesProcessed,
    DistributionStats MessagesPerChat
);

public record StatsResponse(
    LatencyStats DeliveryLatency,
    ChatStats    Chat
);
