namespace NexoApi.Features.Chatbot.Dtos;

public record ChatMensaje(string Rol, string Contenido);

public record ChatRequest(string Mensaje, List<ChatMensaje> Historial);

public record ChatResponse(string Respuesta, List<ChatMensaje> Historial);
