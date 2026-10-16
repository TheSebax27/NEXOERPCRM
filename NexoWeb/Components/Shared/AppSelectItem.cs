namespace NexoWeb.Components.Shared;

/// <summary>Opción para AppSelect: etiqueta visible + valor tipado.</summary>
public record AppSelectItem<TValue>(string Label, TValue? ItemValue);
