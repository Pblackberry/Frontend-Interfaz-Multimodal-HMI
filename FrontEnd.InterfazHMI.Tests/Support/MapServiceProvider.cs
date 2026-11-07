namespace FrontEnd.InterfazHMI.Tests.Support;

internal sealed class MapServiceProvider : IServiceProvider
{
    private readonly Dictionary<Type, object> _services = new();

    public MapServiceProvider Add<T>(T service) where T : notnull
    {
        _services[typeof(T)] = service;
        return this;
    }

    public object? GetService(Type serviceType) => _services.GetValueOrDefault(serviceType);
}
