using System;
using System.Collections.Generic;
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

        public class InfoCommand
        {
            readonly string carriageReturn = "\r\n";
            readonly char delimiter = ':';
            readonly char valueDelimiter = ',';
            readonly char valueDicDelimiter = '=';

            /// <summary>
            /// Split Redis return string to string[] with Carriage Return
            /// </summary>
            /// <param name="source">input Redis returned item</param>
            /// <returns></returns>
            public string[] SplitRedisString(object source)
            {
                var item = source.ToString().Split(new string[] { carriageReturn }, StringSplitOptions.RemoveEmptyEntries);
                return item;
            }

            /// <summary>
            /// Convert redis String to Dictionary.
            /// </summary>
            /// <param name="source">input Redis returned item</param>
            /// <returns></returns>
            public Dictionary<string, object> ParseInfo(object source)
            {
                var dic = SplitRedisString(source)
                    .Where(x => x.Contains(delimiter))
                    .Select(x => x.Split(delimiter))
                    .ToDictionary(kv => kv[0], kv =>
                    {
                        var split = kv[1].Split(valueDelimiter);
                        if (split.Length == 1) return (object)split[0];
                        return split.Select(x => x.Split(valueDicDelimiter))
                            .Select(xs => Tuple.Create(xs[0], xs[1]))
                            .ToArray();
                    });
                return dic;
            }
        }
    }
}