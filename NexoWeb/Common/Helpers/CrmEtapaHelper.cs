using MudBlazor;

namespace NexoWeb.Common.Helpers;

public static class CrmEtapaHelper
{
    public static string HexEtapa(string etapa) => etapa switch
    {
        "CONTACTO_INICIAL" => "#90A4AE",
        "PRESENTACION"     => "#42A5F5",
        "REVISITA"         => "#FFA726",
        "VENTA_GANADA"     => "#66BB6A",
        "CONTACTO_NO_UTIL" => "#EF5350",
        "PROSPECCION"      => "#FFA726",
        "PROPUESTA"        => "#7E57C2",
        "NEGOCIACION"      => "#AB47BC",
        "GANADA"           => "#26A69A",
        "PERDIDA"          => "#EF5350",
        _                  => "#90A4AE"
    };

    public static Color ColorEtapa(string etapa) => etapa switch
    {
        "CONTACTO_INICIAL"  => Color.Default,
        "PRESENTACION"      => Color.Info,
        "REVISITA"          => Color.Warning,
        "VENTA_GANADA"      => Color.Success,
        "CONTACTO_NO_UTIL"  => Color.Error,
        "PROSPECCION"       => Color.Warning,
        "PROPUESTA"         => Color.Primary,
        "NEGOCIACION"       => Color.Secondary,
        "GANADA"            => Color.Success,
        "PERDIDA"           => Color.Error,
        "DESCARTADO"        => Color.Error,
        _                   => Color.Default
    };

    public static string EtiquetaEtapa(string etapa) => etapa switch
    {
        "CONTACTO_INICIAL"  => "Contacto Inicial",
        "PRESENTACION"      => "Presentación",
        "REVISITA"          => "Revisita",
        "VENTA_GANADA"      => "Venta Ganada",
        "CONTACTO_NO_UTIL"  => "No Útil",
        "PROSPECCION"       => "Prospección",
        "PROPUESTA"         => "Propuesta",
        "NEGOCIACION"       => "Negociación",
        "GANADA"            => "Ganada",
        "PERDIDA"           => "Perdida",
        "DESCARTADO"        => "Descartado",
        "CONVERTIDO"        => "Convertido",
        _ => etapa.Length > 0 ? char.ToUpper(etapa[0]) + etapa[1..].ToLower() : etapa
    };

    public static string HexEtapaOp(string etapa) => etapa switch
    {
        "PROSPECCION" => "#FFA726",
        "PROPUESTA"   => "#7E57C2",
        "NEGOCIACION" => "#AB47BC",
        "GANADA"      => "#26A69A",
        "PERDIDA"     => "#EF5350",
        _             => "#90A4AE"
    };

    public static Color ColorEtapaOp(string etapa) => etapa switch
    {
        "PROSPECCION" => Color.Warning,
        "PROPUESTA"   => Color.Primary,
        "NEGOCIACION" => Color.Secondary,
        "GANADA"      => Color.Success,
        "PERDIDA"     => Color.Error,
        _             => Color.Default
    };

    public static string ConfianzaEtiqueta(string? c) => c switch
    {
        "OPTIMISTA" => "Optimista",
        "NEUTRO"    => "Neutro",
        "BAJA"      => "Baja confianza",
        _           => c ?? "-"
    };

    public static string ScoreBadgeStyle(int score)
    {
        var (bg, fg) = score >= 70 ? ("var(--mud-palette-success)", "#fff")
                     : score >= 40 ? ("var(--mud-palette-warning)", "#fff")
                     : ("var(--mud-palette-error)", "#fff");
        return $"display:inline-block;min-width:28px;text-align:center;padding:1px 5px;border-radius:10px;font-size:0.68rem;font-weight:700;background:{bg};color:{fg};flex-shrink:0";
    }
}
