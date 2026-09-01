using Microsoft.PowerPlatform.Dataverse.Client;
using Umayor.Dynamics.DeletePoc.Models;

namespace Umayor.Dynamics.DeletePoc.Services;

public class DataverseConnectionFactory
{
    public ServiceClient CreateClient(DataverseSettings settings)
    {
        string connectionString;
        if (settings.AuthType.Equals("ClientSecret", StringComparison.OrdinalIgnoreCase))
        {
            connectionString = $"AuthType=ClientSecret;Url={settings.Url};ClientId={settings.ClientId};ClientSecret={settings.ClientSecret}";
        }
        else
        {
            connectionString = $"AuthType=OAuth;Url={settings.Url};AppId=51f81489-12ee-4a9e-aaae-a2591f45987d;RedirectUri=http://localhost;LoginPrompt=Auto";
        }
        
        var client = new ServiceClient(connectionString);
        
        if (!client.IsReady)
        {
            throw new Exception($"Error conectando a Dataverse: {client.LastError}");
        }

        return client;
    }
}
