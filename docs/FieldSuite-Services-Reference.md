# FieldSuite Infrastructure Services — Method Reference

## AssistantService

### AssistantReply (line 6)
```csharp
public record AssistantReply(string Answer, bool UsedLlm);

public interface IAssistantService
```

### AskAsync (line 24)
```csharp
public async Task<AssistantReply> AskAsync(int organizationId, string question)
```

## AuditNotificationServices

### SaveAsync (line 12)
```csharp
public async Task SaveAsync(int organizationId, string userId, string userName, string action, string entityType, string entityId, string detail)
```

### NotifyAsync (line 40)
```csharp
public async Task NotifyAsync(int organizationId, string userId, string message, string link)
```

### NotifyRoleAsync (line 52)
```csharp
public async Task NotifyRoleAsync(int organizationId, string role, string message, string link)
```

### MarkReadAsync (line 74)
```csharp
public async Task MarkReadAsync(int notificationId, string userId)
```

## CostSuggestionService

### SuggestUnitRateAsync (line 15)
```csharp
public async Task<decimal?> SuggestUnitRateAsync(int organizationId, string unit, string description)
```

## DashboardService

### DashboardStats (line 6)
```csharp
public record DashboardStats(
    int ActiveProjects,
    int OpenIncidents,
    int OverdueActions,
    int ActivePermits,
    int PendingPermits,
    int OpenSnags,
    int OpenNCRs,
    int WorkersPresentToday,
    int TotalActiveWorkers,
    int ExpiringDocuments,
    int SentEstimates);

public interface IDashboardService
```

### GetAsync (line 29)
```csharp
public async Task<DashboardStats> GetAsync(int organizationId)
```

## IAuditService

## LlmServices

### CompleteAsync (line 34)
```csharp
public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
```

### StructuredDraft (line 76)
```csharp
public record StructuredDraft(string Title, string Description, string Category, string Severity);

public interface IStructuredDraftService
```

### DraftAsync (line 92)
```csharp
public async Task<StructuredDraft> DraftAsync(string kind, string rawInput)
```

## NudgeEngine

### Nudge (line 6)
```csharp
public record Nudge(string Kind, string Severity, string Message, string Link);

public interface INudgeEngine
```

### ExpireStalePermitsAsync (line 19)
```csharp
public async Task<int> ExpireStalePermitsAsync(int organizationId)
```

### ComputeAsync (line 37)
```csharp
public async Task<List<Nudge>> ComputeAsync(int organizationId)
```

## PermitRiskScoringService

### RiskScoreResult (line 5)
```csharp
public record RiskScoreResult(RiskLevel RiskLevel, int Score, IReadOnlyList<string> Reasons, IReadOnlyList<string> RequiredApproverRoles);

public interface IPermitRiskScoringService
```

### Score (line 24)
```csharp
public RiskScoreResult Score(WorkPermit permit)
```

