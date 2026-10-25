using System;
using System.Collections.Generic;
using System.Text;
using Frontend_interfaz_Multimodal_HMI.Models;

namespace FrontEnd.InterfazHMI.Tests.Support;

internal static class TestData
{
    public static CobotMenuItem Item(
        string nombre = "Mojito",
        decimal precio = 5.5m,
        bool estado = true,
        string? ingredientes = "Ron, menta",
        string? codigoModbus = "0x01") => new()
        {
            NombreItem = nombre,
            PrecioItem = precio,
            Estado = estado,
            Ingredientes = ingredientes,
            CodigoModbus = codigoModbus,
        };

    public static CatalogUpdatePayload Payload(string fecha, params CobotMenuItem[] items) => new()
    {
        UltimaActualizacion = fecha,
        Catalogo = new CobotCatalogResponse { Items = items.ToList(), Total = items.Length },
    };
}
