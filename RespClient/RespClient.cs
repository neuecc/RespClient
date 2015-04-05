using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace Redis.Protocol
{
    public enum RespType : byte
    {
        SimpleStrings = (byte)'+',
        Erorrs = (byte)'-',
        Integers = (byte)':',
        BulkStrings = (byte)'$',
        Arrays = (byte)'*'
    }

    public enum RedisInfoKeyType
    {
        redis_version,
        redis_git_sha1,
        redis_git_dirty,
        redis_build_id,
        redis_mode,
        os,
        arch_bits,
        multiplexing_api,
        gcc_version,
        process_id,
        run_id,
        tcp_port,
        uptime_in_seconds,
        uptime_in_days,
        hz,
        lru_clock,
        connected_clients,
        client_longest_output_list,
        client_biggest_input_buf,
        blocked_clients,
        used_memory,
        used_memory_human,
        used_memory_rss,
        used_memory_peak,
        used_memory_peak_human,
        used_memory_lua,
        mem_fragmentation_ratio,
        mem_allocator,
        loading,
        rdb_changes_since_last_save,
        rdb_bgsave_in_progress,
        rdb_last_save_time,
        rdb_last_bgsave_status,
        rdb_last_bgsave_time_sec,
        rdb_current_bgsave_time_sec,
        aof_enabled,
        aof_rewrite_in_progress,
        aof_rewrite_scheduled,
        aof_last_rewrite_time_sec,
        aof_current_rewrite_time_sec,
        aof_last_bgrewrite_status,
        aof_last_write_status,
        total_connections_received,
        total_commands_processed,
        instantaneous_ops_per_sec,
        total_net_input_bytes,
        total_net_output_bytes,
        instantaneous_input_kbps,
        instantaneous_output_kbps,
        rejected_connections,
        sync_full,
        sync_partial_ok,
        sync_partial_err,
        expired_keys,
        evicted_keys,
        keyspace_hits,
        keyspace_misses,
        pubsub_channels,
        pubsub_patterns,
        latest_fork_usec,
        role,
        connected_slaves,
        master_repl_offset,
        repl_backlog_active,
        repl_backlog_size,
        repl_backlog_first_byte_offset,
        repl_backlog_histlen,
        used_cpu_sys,
        used_cpu_user,
        used_cpu_sys_children,
        used_cpu_user_children,
        cmdstat_set,
        cmdstat_info,
        db0, // up to 16 databases allowed in redis
        db1,
        db2,
        db3,
        db4,
        db5,
        db6,
        db7,
        db8,
        db9,
        db10,
        db11,
        db12,
        db13,
        db14,
        db15
    }

    public enum RedisInfoSubkeyType
    {
        calls,
        usec,
        usec_per_call,
        keys,
        expires,
        avg_ttl
    }

    public enum RedisInfoInfoType
    {
        Default,
        Server,
        Clients,
        Memory,
        Persistence,
        Stats,
        Replication,
        CPU,
        KeySpace,
        CommandStats,
        All
    }

    /// <summary>
    /// Container for RedisInfo command parse result
    /// </summary>
    public class RedisInfo
    {
        public string InfoType { get; set; }
        public string Key { get; set; }
        public string SubKey { get; set; }
        public string Value { get; set; }

        public RedisInfo(string infoType, string key, string subKey, string value)
        {
            this.InfoType = infoType;
            this.Key = key;
            this.SubKey = subKey;
            this.Value = value;
        }
    }

    public class RespClient : IDisposable
    {
        const string TerminateStrings = "\r\n";
        static readonly Encoding Encoding = Encoding.UTF8;

        readonly string host;
        readonly int port;
        readonly int ioTimeout;

        public string Host { get { return host; } }
        public int Port { get { return port; } }
        public int IoTimeout { get { return ioTimeout; } }

        Socket socket;
        BufferedStream stream;

        public RespClient(string host = "127.0.0.1", int port = 6379, int ioTimeout = -1)
        {
            this.host = host;
            this.port = port;
            this.ioTimeout = ioTimeout;
        }

        public void Connect()
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true,
                SendTimeout = ioTimeout
            };
            socket.Connect(host, port);

            if (!socket.Connected)
            {
                socket.Close();
                socket = null;
                return;
            }
            stream = new BufferedStream(new NetworkStream(socket), 16 * 1024);
        }

        string ReadFirstLine()
        {
            var sb = new StringBuilder();

            int current;
            var prev = default(char);
            while ((current = stream.ReadByte()) != -1)
            {
                var c = (char)current;
                if (prev == '\r' && c == '\n') // reach at TerminateLine
                {
                    break;
                }
                else if (prev == '\r' && c == '\r')
                {
                    sb.Append(prev); // append prev '\r'
                    continue;
                }
                else if (c == '\r')
                {
                    prev = c; // not append '\r'
                    continue;
                }

                prev = c;
                sb.Append(c);
            }

            return sb.ToString();
        }

        byte[] BuildBinarySafeCommand(string command, byte[][] arguments)
        {
            var firstLine = Encoding.GetBytes((char)RespType.Arrays + (arguments.Length + 1).ToString() + TerminateStrings);
            var secondLine = Encoding.GetBytes((char)RespType.BulkStrings + Encoding.GetBytes(command).Length.ToString() + TerminateStrings + command + TerminateStrings);
            var thirdLine = arguments.Select(x =>
            {
                var head = Encoding.GetBytes((char)RespType.BulkStrings + x.Length.ToString() + TerminateStrings);
                return head.Concat(x).Concat(Encoding.GetBytes(TerminateStrings)).ToArray();
            })
            .ToArray();

            return new[] { firstLine, secondLine }.Concat(thirdLine).SelectMany(xs => xs).ToArray();
        }

        void SendRequest(byte[] command)
        {
            if (socket == null) Connect();
            if (socket == null) throw new Exception("Socket can't connect");

            try
            {
                socket.Send(command);
            }
            catch (SocketException)
            {
                socket.Close();
                socket = null;
                throw;
            }
        }

        object FetchResponse(Func<byte[], object> binaryDecoder)
        {
            var type = (RespType)stream.ReadByte();
            switch (type)
            {
                case RespType.SimpleStrings:
                    {
                        var result = ReadFirstLine();
                        return result;
                    }
                case RespType.Erorrs:
                    {
                        var result = ReadFirstLine();
                        return result;
                    }
                case RespType.Integers:
                    {
                        var line = ReadFirstLine();
                        return long.Parse(line);
                    }
                case RespType.BulkStrings:
                    {
                        var line = ReadFirstLine();
                        var length = int.Parse(line);
                        if (length == -1)
                        {
                            return null;
                        }
                        var buffer = new byte[length];
                        stream.Read(buffer, 0, length);

                        ReadFirstLine(); // read terminate

                        if (binaryDecoder == null)
                        {
                            return buffer;
                        }
                        else
                        {
                            return binaryDecoder(buffer);
                        }
                    }
                case RespType.Arrays:
                    {
                        var line = ReadFirstLine();
                        var length = int.Parse(line);

                        if (length == 0)
                        {
                            return new object[0];
                        }
                        if (length == -1)
                        {
                            return null;
                        }

                        var objects = new object[length];

                        for (int i = 0; i < length; i++)
                        {
                            objects[i] = FetchResponse(binaryDecoder);
                        }

                        return objects;
                    }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public object SendCommand(string command)
        {
            return SendCommand(command, (Func<byte[], object>)null);
        }

        public object SendCommand(string command, Func<byte[], object> binaryDecoder)
        {
            // Request
            SendRequest(Encoding.GetBytes(command + TerminateStrings));

            // Response
            return FetchResponse(binaryDecoder);
        }

        public object SendCommand(string command, params byte[][] arguments)
        {
            return SendCommand(command, arguments, null);
        }

        public object SendCommand(string command, byte[][] arguments, Func<byte[], object> binaryDecoder)
        {
            var sendCommand = BuildBinarySafeCommand(command, arguments);

            // Request
            SendRequest(sendCommand);

            // Response
            return FetchResponse(binaryDecoder);
        }

        public PipelineCommand UsePipeline()
        {
            return new PipelineCommand(this);
        }

        public void Dispose()
        {
            try
            {
                if (stream != null)
                {
                    stream.Dispose();
                }
                stream = null;
            }
            finally
            {
                if (socket != null)
                {
                    socket.Close();
                }
                socket = null;
            }
            GC.SuppressFinalize(this);
        }

        ~RespClient()
        {
            Dispose();
        }

        public class PipelineCommand
        {
            readonly RespClient client;
            readonly List<Tuple<byte[], Func<byte[], object>>> commands = new List<Tuple<byte[], Func<byte[], object>>>();

            internal PipelineCommand(RespClient client)
            {
                this.client = client;
            }

            public PipelineCommand QueueCommand(string command)
            {
                commands.Add(Tuple.Create(Encoding.GetBytes(command + TerminateStrings), (Func<byte[], object>)null));
                return this;
            }

            public PipelineCommand QueueCommand(string command, Func<byte[], object> binaryDecoder)
            {
                commands.Add(Tuple.Create(Encoding.GetBytes(command + TerminateStrings), binaryDecoder));
                return this;
            }

            public PipelineCommand QueueCommand(string command, params byte[][] arguments)
            {
                return QueueCommand(command, arguments, null);
            }

            public PipelineCommand QueueCommand(string command, byte[][] arguments, Func<byte[], object> binaryDecoder)
            {
                var sendCommand = client.BuildBinarySafeCommand(command, arguments);

                commands.Add(Tuple.Create(sendCommand, binaryDecoder));
                return this;
            }

            public object[] Execute()
            {
                // Request
                client.SendRequest(commands.SelectMany(x => x.Item1).ToArray());

                // Response
                var result = new object[commands.Count];

                for (int i = 0; i < result.Length; i++)
                {
                    result[i] = client.FetchResponse(commands[i].Item2);
                }

                commands.Clear();

                return result;
            }
        }
    }
}