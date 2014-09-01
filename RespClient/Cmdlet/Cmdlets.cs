using Redis.Protocol;
using System;
using System.Management.Automation;
using System.Text;

namespace Redis.PowerShell.Cmdlet
{
    [Cmdlet(VerbsCommunications.Connect, "RedisServer")]
    public class ConnectRedisServer : System.Management.Automation.Cmdlet
    {
        [Alias("IPAddress", "ComputerName")]
        [Parameter(Mandatory = false, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        public string Host { get; set; }

        [Parameter(Mandatory = false, Position = 1, ValueFromPipelineByPropertyName = true)]
        public int? Port { get; set; }

        [Alias("Timeout")]
        [Parameter(Mandatory = false, Position = 2, ValueFromPipelineByPropertyName = true)]
        public int? IoTimeout { get; set; }

        protected override void BeginProcessing()
        {
            if (Global.RespClient != null) Global.RespClient.Dispose();

            var client = new RespClient(Host ?? "127.0.0.1", Port ?? 6379, IoTimeout ?? -1);
            client.Connect();

            Global.RespClient = client;
        }
    }

    [Cmdlet(VerbsCommon.Get, "RedisCurrentInfo")]
    public class GetRedisCurrentInfo : System.Management.Automation.Cmdlet
    {
        protected override void BeginProcessing()
        {
            if (Global.RespClient != null)
            {
                this.WriteObject(Global.RespClient);
            }
        }
    }

    [Cmdlet(VerbsCommunications.Disconnect, "RedisServer")]
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

    [Cmdlet(VerbsCommunications.Send, "RedisCommand")]
    public class SendCommand : System.Management.Automation.Cmdlet
    {
        [Parameter(ParameterSetName = "Command", Position = 0, Mandatory = true, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
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

    [Cmdlet(VerbsCommunications.Send, "RedisPipelineCommand")]
    public class PipelineCommand : System.Management.Automation.Cmdlet
    {
        [Parameter(ParameterSetName = "Command", Position = 0, Mandatory = true, ValueFromPipeline = true)]
        public string[] Command { get; set; }

        protected override void ProcessRecord()
        {
            if (Global.RespClient == null) throw new InvalidOperationException("Server is not connecting");
            if (Global.PipelineCommand != null) throw new InvalidOperationException("pipeline already created. Please execute current pipeline before use this cmdlet.");

            // pipeline mode
            Global.PipelineCommand = Global.RespClient.UsePipeline();
            foreach (var c in Command) Global.PipelineCommand.QueueCommand(c, x => Encoding.UTF8.GetString(x));
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