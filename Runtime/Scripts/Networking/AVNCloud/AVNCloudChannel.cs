using System;
using System.Net.Http;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;

namespace ClassVR {
  namespace AvnCloud {
    public enum EndpointServer {
      Production,
      Alpha
    }
  }

  /// <summary>
  /// Singleton class providing access to gRPC channels
  /// </summary>
  public class AvnCloudChannel {
    public GrpcChannel Alpha { get; private set; }
    public GrpcChannel Production { get; private set; }

    // Channels are expensive to create, so are cached in a singleton
    public static AvnCloudChannel Instance {
      get { return lazy.Value; }
    }
    private static readonly Lazy<AvnCloudChannel> lazy = new Lazy<AvnCloudChannel>(() => new AvnCloudChannel());

    private AvnCloudChannel() {
      Alpha = GrpcChannel.ForAddress("https://gweb-alpha.avncloud.com:443", new GrpcChannelOptions {
        HttpHandler = new GrpcWebHandler(new HttpClientHandler())
      });

      Production = GrpcChannel.ForAddress("https://gweb.avncloud.com:443", new GrpcChannelOptions {
        HttpHandler = new GrpcWebHandler(new HttpClientHandler())
      });
    }

    public GrpcChannel ChannelForServer(AvnCloud.EndpointServer server) {
      return server == AvnCloud.EndpointServer.Production ? Production : Alpha;
    }
  }
}
