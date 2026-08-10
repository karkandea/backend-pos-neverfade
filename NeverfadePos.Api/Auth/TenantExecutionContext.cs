namespace NeverfadePos.Api.Auth;

public enum TenantExecutionMode
{
    None,
    AuthenticatedTenant,
    TrustedSystem
}

public interface ITenantExecutionContext
{
    TenantExecutionMode Mode { get; }

    Guid? TargetTenantId { get; }

    string? OperationName { get; }

    bool HasTargetTenant { get; }
}

internal interface ITrustedTenantExecutionScope
{
    IDisposable Begin(
        Guid targetTenantId,
        string operationName);
}

internal sealed class TenantExecutionContext(
    CurrentUser currentUser)
    : ITenantExecutionContext,
      ITrustedTenantExecutionScope
{
    private Guid? _trustedTenantId;
    private string? _operationName;

    public TenantExecutionMode Mode
    {
        get
        {
            if (_trustedTenantId.HasValue)
            {
                return TenantExecutionMode.TrustedSystem;
            }

            return GetAuthenticatedTenantId().HasValue
                ? TenantExecutionMode.AuthenticatedTenant
                : TenantExecutionMode.None;
        }
    }

    public Guid? TargetTenantId =>
        _trustedTenantId ?? GetAuthenticatedTenantId();

    public string? OperationName =>
        Mode == TenantExecutionMode.TrustedSystem
            ? _operationName
            : null;

    public bool HasTargetTenant =>
        TargetTenantId.HasValue;

    public IDisposable Begin(
        Guid targetTenantId,
        string operationName)
    {
        if (targetTenantId == Guid.Empty)
        {
            throw new ArgumentException(
                "Trusted tenant target must not be empty.",
                nameof(targetTenantId));
        }

        if (string.IsNullOrWhiteSpace(operationName))
        {
            throw new ArgumentException(
                "Trusted tenant operation name is required.",
                nameof(operationName));
        }

        if (Mode != TenantExecutionMode.None)
        {
            throw new InvalidOperationException(
                "A tenant execution scope is already active.");
        }

        _trustedTenantId = targetTenantId;
        _operationName = operationName.Trim();

        return new Scope(this);
    }

    private Guid? GetAuthenticatedTenantId()
    {
        var tenantId = currentUser.TenantId;

        return tenantId.HasValue &&
               tenantId.Value != Guid.Empty
            ? tenantId
            : null;
    }

    private void End()
    {
        _trustedTenantId = null;
        _operationName = null;
    }

    private sealed class Scope(
        TenantExecutionContext context)
        : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            context.End();
        }
    }
}
