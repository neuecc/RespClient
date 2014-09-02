using Redis.Protocol;
using System;
using System.Linq;
using System.Management.Automation;
using System.Text;

namespace Redis.PowerShell.Cmdlet
{
    /// <summary>
    /// Open socket to connect specified Redis Server.
    /// If connection already establishded to some host, then that connection will dispose and new connection is created.
    /// </summary>
    /// <param name="Host">Redis server computer name or IPAddress. default is 127.0.0.1</param>
    /// <param name="Port">Redis Server port number waiting connection. default is 6379</param>
    /// <param name="IoTimeout">Socket client timeout values. default is -1</param>
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
            // existing connection will be disposed.
            if (Global.RespClient != null) Global.RespClient.Dispose();

            var client = new RespClient(Host ?? "127.0.0.1", Port ?? 6379, IoTimeout ?? -1);
            client.Connect();

            Global.RespClient = client;
        }
    }

    /// <summary>
    /// Get current socket Redis server connection info.
    /// You can see host, port and ioTimeout.
    /// </summary>
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

    /// <summary>
    /// Disconnect current Redis server socket connection.
    /// </summary>
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

    /// <summary>
    /// Send Command to the Redis server.
    /// Command will be immediately executed.
    /// Pipeline will queue command if there are already pipeline exist..
    /// </summary>
    /// <param name="Command">Redis Command to send. e.g. info</param>
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

    /// <summary>
    /// Send Command to the Redis server with pipeline mode.
    /// You don't need to handle pipeline status with this mode.
    /// </summary>
    /// <param name="Command">Redis Commands to send. e.g. "info", "config get save"</param>
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

    /// <summary>
    /// Begin pipeline.
    /// Command will be queued into pipeline from this cmdlet.
    /// </summary>
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

    /// <summary>
    /// Execute queued command in pipeline.
    /// Make sure you have created pipeline.
    /// </summary>
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

    /// <summary>
    /// Get Redis info with info command.
    /// </summary>
    /// <param name="All">change "info" to "info all".</param>
    [Cmdlet(VerbsCommon.Get, "RedisInfo")]
    public class GetRedisInfoCommand : System.Management.Automation.Cmdlet
    {
        [Parameter(ParameterSetName = "All", Position = 0, Mandatory = false, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        public SwitchParameter All { get; set; }

        private string Command = "info";

        protected override void BeginProcessing()
        {
            if (Global.RespClient == null) throw new InvalidOperationException("Server is not connecting");

            // use "info all"
            if (All == true) { Command = Command + " " + "all"; }
        }

        protected override void EndProcessing()
        {            
            // no pipeline mode
            var value = Global.RespClient.SendCommand(Command, x => Encoding.UTF8.GetString(x));

            // parse string to List
            var InfoCommand = new Redis.Protocol.RespClient.InfoCommand();
            var dicationary = InfoCommand.ParseInfo(value);
            foreach (var x in dicationary) { this.WriteObject(x); }
        }
    }
}