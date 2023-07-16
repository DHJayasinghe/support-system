using System.Collections;

namespace supportSystem;

// Ambient Context approach: Reference: https://methodpoet.com/unit-testing-datetime-now/
// Thread Local Storage (TLS) is a programming method which uses static memory (global) to a thread.
// Which helps to maintain unique data for each, thread which can access using a global index.
// Unit Testing Usage: using var context = new DateTimeProviderContext(new DateTime(2021, 4, 5));
public sealed class DateTimeProvider
{
    public static DateTime UtcNow =>
        DateTimeProviderContext.Current == null
                ? DateTime.UtcNow
                : DateTimeProviderContext.Current.ContextDateTimeNow;
    public static DateTime Now =>
        DateTimeProviderContext.Current == null
                ? DateTime.Now
                : DateTimeProviderContext.Current.ContextDateTimeNow.ToLocalTime();
}

public sealed class DateTimeProviderContext : IDisposable
{
    private readonly DateTime startTime;
    private readonly DateTime contextDateTimeNow;
    internal DateTime ContextDateTimeNow => contextDateTimeNow.AddTicks(DateTime.UtcNow.Subtract(startTime).Ticks);
    private static readonly ThreadLocal<Stack> ThreadScopeStack = new(() => new Stack());

    public DateTimeProviderContext(DateTime contextDateTimeNow)
    {
        startTime = DateTime.UtcNow;
        this.contextDateTimeNow = contextDateTimeNow;
        ThreadScopeStack.Value.Push(this);
    }

    public static DateTimeProviderContext Current => ThreadScopeStack.Value.Count == 0 ? null : ThreadScopeStack.Value.Peek() as DateTimeProviderContext;

    public void Dispose()
    {
        ThreadScopeStack.Value.Pop();
    }
}
