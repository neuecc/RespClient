using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Redis.PowerShell.Cmdlet
{
    // use for Commandlet only
    internal static class Global
    {
        public static Redis.Protocol.RespClient RespClient;
        public static Redis.Protocol.RespClient.PipelineCommand PipelineCommand;
    }
}