namespace NexoApi.Common.Security
{
    public interface IJwtService
    {

        String GenerateAccessToken(int usuarioId, string username, string rol, int? centroCostoId);
        String GenerateRefreshToken();

    }
}
