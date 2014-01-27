using Redis.Protocol;
using System;
using System.Management.Automation;
using System.Text;

namespace Redis.PowerShell
{
    [Cmdlet("Connect", "RedisServer")]
    public class ConnectRedisServer : Cmdlet
    {
        [Parameter(ParameterSetName = "Host", Mandatory = false, Position = 0)]
        public string Host { get; set; }

        [Parameter(ParameterSetName = "Port", Mandatory = false, Position = 1)]
        public int? Port { get; set; }

        [Parameter(ParameterSetName = "IoTimeout", Mandatory = false, Position = 2)]
        public int? IoTimeout { get; set; }

        protected override void BeginProcessing()
        {
            if (Global.RespClient != null) Global.RespClient.Dispose();

            var client = new RespClient(Host ?? "127.0.0.1", Port ?? 6379, IoTimeout ?? -1);
            client.Connect();

            Global.RespClient = client;
        }
    }

    [Cmdlet("Disconnect", "RedisServer")]
    public class DisconnectRedisServer : Cmdlet
    {
        protected override void BeginProcessing()
        {
            if (Global.RespClient != null)
            {
                Global.RespClient.Dispose();
                Global.RespClient = null;
            }
        }
    }

    // TODO:Command Arguments
    [Cmdlet("Send", "RedisCommand")]
    public class SendCommand : Cmdlet
    {
        [Parameter(ParameterSetName = "Command", Position = 0, Mandatory = true)]
        public string Command { get; set; }

        protected override void ProcessRecord()
        {
            if (Global.RespClient == null) throw new InvalidOperationException();

            var value = Global.RespClient.SendCommand(Command, x => Encoding.UTF8.GetString(x));
            this.WriteObject(value);
        }
    }

    // TODO:UsePipeline, ExecutePipeline
}