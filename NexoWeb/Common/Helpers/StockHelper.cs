namespace NexoWeb.Common.Helpers;

public static class StockHelper
{
    /// <summary>
    /// Formatea una cantidad de cajas mostrando "X cajas + Y und".
    /// La cantidad en DB se almacena en cajas (posiblemente decimal: 6.7 = 6 cajas + 7 unidades).
    /// </summary>
    public static string FormatearCajaUnidades(decimal cantidadCajas, decimal upEmbalaje)
    {
        var cajasCompletas = (int)Math.Floor((double)cantidadCajas);
        var unidadesSueltas = (int)Math.Round((double)((cantidadCajas - cajasCompletas) * upEmbalaje));

        if (cajasCompletas == 0) return $"{unidadesSueltas} und";
        if (unidadesSueltas == 0) return $"{cajasCompletas} {(cajasCompletas == 1 ? "caja" : "cajas")}";
        return $"{cajasCompletas} {(cajasCompletas == 1 ? "caja" : "cajas")} + {unidadesSueltas} und";
    }

    /// <summary>
    /// Convierte cajas enteras + unidades sueltas a la cantidad decimal en cajas que va al backend.
    /// Ej: 6 cajas + 7 und con upEmbalaje=10 → 6.7
    /// </summary>
    public static decimal CajasYUnidadesADecimal(int cajas, int unidades, decimal upEmbalaje)
        => upEmbalaje > 0 ? cajas + unidades / upEmbalaje : cajas;

    /// <summary>
    /// Descompone una cantidad decimal de cajas en cajas enteras + unidades sueltas.
    /// </summary>
    public static (int Cajas, int Unidades) DescomponerCajas(decimal cantidadCajas, decimal upEmbalaje)
    {
        var cajas = (int)Math.Floor((double)cantidadCajas);
        var und   = (int)Math.Round((double)((cantidadCajas - cajas) * upEmbalaje));
        return (cajas, und);
    }
}
