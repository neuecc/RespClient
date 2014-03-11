using Redis.Protocol;
using System;
using System.Management.Automation;
using System.Text;

namespace Redis.PowerShell.Cmdlet
{
    [Cmdlet("Connect", "RedisServer")]
    public class ConnectRedisServer : System.Management.Automation.Cmdlet
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
    public class DisconnectRedisServer : System.Management.Automation.Cmdlet
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

    [Cmdlet("Send", "RedisCommand")]
    public class SendCommand : System.Management.Automation.Cmdlet
    {
        [Parameter(ParameterSetName = "Command", Position = 0, Mandatory = true, ValueFromPipeline = true)]
        public string Command { get; set; }

        protected override void ProcessRecord()
        {
            if (Global.RespClient == null) throw new InvalidOperationException("Server is not connecting");

            if (Global.PipelineCommand == null)
            {
                var value = Global.RespClient.SendCommand(Command, x => Encoding.UTF8.GetString(x));
                this.WriteObject(value);
            }
            else
            {
                // pipeline mode
                Global.PipelineCommand.QueueCommand(Command, x => Encoding.UTF8.GetString(x));
            }
        }
    }

    [Cmdlet("Begin", "RedisPipeline")]
    public class BeginPipeline : System.Management.Automation.Cmdlet
    {
        protected override void BeginProcessing()
        {
            if (Global.RespClient == null) throw new InvalidOperationException("Server is not connecting");
            if (Global.PipelineCommand != null) throw new InvalidOperationException("Pipeline is always beginning");

            Global.PipelineCommand = Global.RespClient.UsePipeline();
        }
    }

    [Cmdlet("Execute", "RedisPipeline")]
    public class ExecutePipeline : System.Management.Automation.Cmdlet
    {
        protected override void ProcessRecord()
        {
            if (Global.PipelineCommand == null) throw new InvalidOperationException("Pipeline is not beginning");

            try
            {
                var results = Global.PipelineCommand.Execute();
                this.WriteObject(results);
            }
            finally
            {
                Global.PipelineCommand = null;
            }
        }
    }
}