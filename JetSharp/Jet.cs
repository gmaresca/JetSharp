﻿using System.Net;
using System.Security.Authentication;
using System.Threading.Tasks;
using JetBrains.Annotations;
using JetSharp.Authentication;
using JetSharp.Serialization;
using Newtonsoft.Json;
using RestSharp;
using RestSharp.Serializers;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace JetSharp
{
  public class Jet
  {
    private const string ApiUrl = "https://merchant-api.jet.com/api";

    private IRestClient RestClient { get; }
    private ISerializer Serializer { get; }

    private string Username { get; set; }
    private string Password { get; set; }

    private JetAuthenticator Authenticator
    {
      get { return (JetAuthenticator)RestClient.Authenticator; }
      set { RestClient.Authenticator = value; }
    }

    [CanBeNull]
    public JetToken AutheticationToken => Authenticator?.Token;

    public Jet()
    {
      RestClient = new RestClient(ApiUrl);

      Serializer = new JsonDotNetSerializer(new JsonSerializer
      {
        ContractResolver = new PrivatePropertyContractResolver(),
        MissingMemberHandling = MissingMemberHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.Ignore,
        NullValueHandling = NullValueHandling.Include
      });
    }

    public async Task AuthenticateAsync([NotNull] string username, [NotNull] string password)
    {
      Username = username;
      Password = password;
      Authenticator = new JetAuthenticator(
        await ExecuteRequestAsync<JetTokenRequest, JetToken>(
          new JetTokenRequest(username, password), false)
                .ConfigureAwait(false));
    }

    private async Task<TResponse> ExecuteRequestAsync<TRequest, TResponse>(TRequest request, bool authenticate = true)
      where TRequest : JetRequest
    {
      if (authenticate && (!Authenticator?.IsTokenValid ?? false))
        await AuthenticateAsync(Username, Password).ConfigureAwait(false);

      var response = await RestClient.ExecuteTaskAsync(
        new RestRequest(request.Resource, request.Method)
        { JsonSerializer = Serializer }
          .AddJsonBody(request))
        .ConfigureAwait(false);

      if (response.StatusCode == HttpStatusCode.Unauthorized)
        throw new AuthenticationException();

      return JsonConvert.DeserializeObject<TResponse>(response.Content);
    }
  }
}