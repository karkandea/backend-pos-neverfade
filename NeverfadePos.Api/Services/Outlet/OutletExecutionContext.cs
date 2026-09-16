namespace NeverfadePos.Api.Services.Outlet;

public interface IOutletExecutionContext
{
    Guid? OutletId { get; }
}

public interface IOutletExecutionScope
{
    IDisposable Begin(Guid outletId);
}

public sealed class OutletExecutionContext
    : IOutletExecutionContext,
      IOutletExecutionScope
{
    private Guid? _outletId;

    public Guid? OutletId => _outletId;

    public IDisposable Begin(Guid outletId)
    {
        if (outletId == Guid.Empty)
        {
            throw new ArgumentException(
                "Outlet execution target must not be empty.",
                nameof(outletId));
        }

        if (_outletId.HasValue)
        {
            throw new InvalidOperationException(
                "An outlet execution scope is already active.");
        }

        _outletId = outletId;
        return new Scope(this);
    }

    private void End()
    {
        _outletId = null;
    }

    private sealed class Scope(
        OutletExecutionContext context)
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
