using Redis.Protocol;
using System;
using System.Linq;
using System.Management.Automation;
using System.Text;
using Redis.Extension;

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

            this.WriteVerbose(string.Format("trying connect to server : {0}:{1}", Host, Port));
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
                this.WriteVerbose("showing current Redis connection info");
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
                this.WriteVerbose(string.Format("send command : {0}", Command));
                var value = Global.RespClient.SendCommand(Command, x => Encoding.UTF8.GetString(x));
                this.WriteObject(value);
            }
            else
            {
                // pipeline mode
                this.WriteVerbose(string.Format("queue command to pipeline : {0}", Command));
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
    /// Get Redis info.
    /// </summary>
    /// <param name="InfoType">Add specific info selector.</param>
    [OutputType(typeof(RedisInfo[]))]
    [Cmdlet(VerbsCommon.Get, "RedisInfo")]
    public class GetRedisInfoCommand : System.Management.Automation.Cmdlet
    {
        [Parameter(Position = 0, Mandatory = false, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        public RedisInfoKeyType[] Key { get; set; }

        [Parameter(Position = 1, Mandatory = false, ValueFromPipelineByPropertyName = true)]
        public RedisInfoSubkeyType[] SubKey { get; set; }
        
        [Parameter(Position = 2, Mandatory = false, ValueFromPipelineByPropertyName = true)]
        public RedisInfoInfoType InfoType { get; set; }

        public GetRedisInfoCommand()
        {
            // Default value
            Key = null;
            SubKey = null;
            InfoType = RedisInfoInfoType.Default;
        }
        
        private string _command = "info";

        protected override void ProcessRecord()
        {
            if (Global.RespClient == null) throw new InvalidOperationException("Server is not connecting");

            // use "info xxxx"
            switch (InfoType)
            {
                case RedisInfoInfoType.Default:
                    this._command = this._command + " " + RedisInfoInfoType.Default;
                    break;
                case RedisInfoInfoType.Server:
                    this._command = this._command + " " + RedisInfoInfoType.Server;
                    break;
                case RedisInfoInfoType.Clients:
                    this._command = this._command + " " + RedisInfoInfoType.Clients;
                    break;
                case RedisInfoInfoType.Memory:
                    this._command = this._command + " " + RedisInfoInfoType.Memory;
                    break;
                case RedisInfoInfoType.Persistence:
                    this._command = this._command + " " + RedisInfoInfoType.Persistence;
                    break;
                case RedisInfoInfoType.Stats:
                    this._command = this._command + " " + RedisInfoInfoType.Stats;
                    break;
                case RedisInfoInfoType.Replication:
                    this._command = this._command + " " + RedisInfoInfoType.Replication;
                    break;
                case RedisInfoInfoType.CPU:
                    this._command = this._command + " " + RedisInfoInfoType.CPU;
                    break;
                case RedisInfoInfoType.KeySpace:
                    this._command = this._command + " " + RedisInfoInfoType.KeySpace;
                    break;
                case RedisInfoInfoType.CommandStats:
                    this._command = this._command + " " + RedisInfoInfoType.CommandStats;
                    break;
                case RedisInfoInfoType.All:
                    this._command = this._command + " " + RedisInfoInfoType.All;
                    break;
                default:
                    break;
            }

            // no pipeline mode send command
            this.WriteVerbose(string.Format("running command : {0}", this._command));
            var redisReturnObject = Global.RespClient.SendCommand(this._command, x => Encoding.UTF8.GetString(x));

            // parse redis info
            var redisInfo = redisReturnObject.AsRedisInfo().FilterKeySubKey(Key, SubKey);
            
            // Output elements
            foreach (var item in redisInfo)
                this.WriteObject(item);
        }

    }

    /// <summary>
    /// Get Redis Config.
    /// Make sure you have created pipeline.
    /// </summary>
    /// <param name="Key">input config name to obtain. e.g. save</param>
    [Cmdlet(VerbsCommon.Get, "RedisConfig")]
    public class GetRedisConfig : System.Management.Automation.Cmdlet
    {
        [Parameter(ParameterSetName = "Key", Position = 0, Mandatory = true, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        public string[] Key { get; set; }

        protected override void ProcessRecord()
        {
            if (Global.RespClient == null) throw new InvalidOperationException("Server is not connecting");

            foreach (var k in Key)
            {
                var Command = "config get" + " " + k;

                // no pipeline mode
                this.WriteVerbose(string.Format("running command : {0}", Command));
                var value = Global.RespClient.SendCommand(Command, x => Encoding.UTF8.GetString(x));

                // parse redis config
                var dictionary = value.ToRedisConfigDictionary();
                this.WriteObject(dictionary);
            }
        }
    }
}