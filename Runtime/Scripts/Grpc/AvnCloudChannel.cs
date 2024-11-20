using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using System;
using System.Net.Http;

namespace ClassVR
{
    public enum EndpointServer
    {
        Production,
        Alpha
    }

    public static class AvnCloudChannel
    {
        public static GrpcChannel Alpha { get; } = 
            GrpcChannel.ForAddress("https://gweb-alpha.avncloud.com:443", new GrpcChannelOptions
            {
                HttpHandler = new GrpcWebHandler(new HttpClientHandler())
            });

        public static GrpcChannel Production { get; } =
            GrpcChannel.ForAddress("https://gweb.avncloud.com:443", new GrpcChannelOptions
            {
                HttpHandler = new GrpcWebHandler(new HttpClientHandler())
            });

        public static GrpcChannel ChannelForServer(EndpointServer server)
        {
            return server == EndpointServer.Production ? Production : Alpha;
        }
    }
}
