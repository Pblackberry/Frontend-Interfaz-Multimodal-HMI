using System.Threading;
using System.Threading.Tasks;

namespace Frontend_interfaz_Multimodal_HMI.Services;

public interface IWsSender
{
    Task<bool> SendAsync<T>(string type, T payload, CancellationToken ct = default);
}
