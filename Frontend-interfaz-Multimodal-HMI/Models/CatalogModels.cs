using System;
using System.Collections.Generic;
using System.Text;

namespace Frontend_interfaz_Multimodal_HMI.Models;

public sealed class CobotMenuItem
{
    public string NombreItem { get; set; } = "";
    public decimal PrecioItem { get; set; }
    public bool Estado { get; set; }
    public string? Ingredientes { get; set; }
    public string? CodigoModbus { get; set; }
}

public sealed class CobotCatalogResponse
{
    public List<CobotMenuItem> Items { get; set; } = new();
    public int Total { get; set; }
}

public sealed class CatalogUpdatePayload
{
    public string UltimaActualizacion { get; set; } = "";
    public CobotCatalogResponse Catalogo { get; set; } = new();
}
