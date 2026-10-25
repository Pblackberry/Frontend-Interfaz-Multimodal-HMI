using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Frontend_interfaz_Multimodal_HMI.Models;
using CommunityToolkit.Mvvm.Messaging;

namespace Frontend_interfaz_Multimodal_HMI.Services;


public sealed record CatalogChangedMessage(CatalogUpdatePayload Snapshot);
public sealed class CatalogStore
{
    private volatile CatalogUpdatePayload? _current;

    public CatalogUpdatePayload? Current => _current;

    public IReadOnlyList<CobotMenuItem> Items
        => _current is { } c ? c.Catalogo.Items : Array.Empty<CobotMenuItem>();

    public void Update(CatalogUpdatePayload payload)
    {
        _current = payload;   // se reemplaza el snapshot completo, nunca se edita la lista
        Debug.WriteLine($"[CatalogStore] Catálogo guardado: {payload.Catalogo.Items.Count} items ({payload.UltimaActualizacion})");
        WeakReferenceMessenger.Default.Send(new CatalogChangedMessage(payload));
    }
}
