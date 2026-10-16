namespace NexoWeb.Common.Auth;

// Singleton: una sola instancia para toda la aplicacion.
// Cuando cualquier circuito de Blazor crea/edita/elimina un evento,
// llama a NotificarCambio() y TODOS los componentes Calendario.razor
// suscritos reciben el aviso y recargan su vista en tiempo real.
// Los componentes deben usar InvokeAsync para volver al hilo del circuito.
public class CalendarioBroadcast
{
    public event Action? OnCambio;
    public void NotificarCambio() => OnCambio?.Invoke();
}
