using MudBlazor;

namespace NexoWeb.Components;

/// <summary>
/// Tema visual de NEXO ERP — paleta "Morado profundo + Gris moderno".
/// Inspirada en el diseño de referencia SaaS: sidebar oscuro, acento morado,
/// fondo gris muy claro, tarjetas blancas.
/// </summary>
public static class NexoTheme
{
    public static readonly MudTheme Theme = new()
    {
        PaletteLight = new PaletteLight
        {
            // ── Acción principal ──────────────────────────────────────
            Primary             = "#7C5CFF",
            PrimaryContrastText = "#FFFFFF",
            PrimaryDarken       = "#5A3BFF",
            PrimaryLighten      = "#9B7FFF",

            // ── Semánticos ────────────────────────────────────────────
            Secondary           = "#6B6B8A",
            SecondaryContrastText = "#FFFFFF",
            Info                = "#0EA5E9",
            Success             = "#10B981",
            Warning             = "#F59E0B",
            Error               = "#EF4444",

            // ── Fondos ────────────────────────────────────────────────
            // Agosto 2026: antes Surface era #FFFFFF puro -- el usuario lo vio
            // demasiado luminoso/con brillo. Se bajo a un blanco roto suave
            // (#FAFBFD) y se oscurecio un poco el fondo general.
            // Sep 2026: Background cambiado a #ECECF3 (Arctic Pearl) para dar
            // mas cuerpo al fondo claro; misma actualización en --n-bg y
            // gradiente base de #nexo-app-root en wwwroot/app.css.
            Background          = "#ECECF3",   // Arctic Pearl (fondo claro)
            Surface             = "#FAFBFD",   // Tarjetas y paneles (blanco roto, no puro)

            // ── Barra superior ────────────────────────────────────────
            AppbarBackground    = "#FAFBFD",
            AppbarText          = "#1A1C2E",

            // ── Sidebar oscuro ────────────────────────────────────────
            DrawerBackground    = "#13131F",
            DrawerText          = "#9B9BB4",
            DrawerIcon          = "#6B6B8A",

            // ── Texto ─────────────────────────────────────────────────
            TextPrimary         = "#1A1C2E",
            TextSecondary       = "#6B6B8A",
            TextDisabled        = "#B0B0C8",

            // ── Líneas y bordes ───────────────────────────────────────
            LinesDefault        = "#E8E9EF",
            LinesInputs         = "#D1D5E8",
            TableLines          = "#EEEEF5",
            Divider             = "#E8E9EF",

            // ── Superposiciones ───────────────────────────────────────
            OverlayDark         = "rgba(26, 28, 46, 0.5)",
            OverlayLight        = "rgba(26, 28, 46, 0.04)",
        }
    };
}
