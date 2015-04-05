using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Redis.Protocol;

namespace Redis.Extension
{
    static class RedisExtension
    {
        public static string[] CastRedisString(this object source)
        {
            var item = source as string;
            if (item != null) return new[] { item };

            var items = source as IEnumerable<object>;
            if (items != null) return items.Cast<string>().ToArray();

            return new string[0];
        }
    }
}
