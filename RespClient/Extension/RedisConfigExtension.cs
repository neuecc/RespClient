using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Redis.Protocol;

namespace Redis.Extension
{
    static class RedisConfigExtension
    {
        /// <summary>
        /// Convert redis config result string to Dictionary.
        /// </summary>
        /// <param name="source">input Redis config returned item</param>
        /// <returns></returns>
        public static Dictionary<string, string> ToRedisConfigDictionary(this object source)
        {
            var dictionary = new Dictionary<string, string>();
            var items = source.CastRedisString();

            switch (items.Length)
            {
                case 1: dictionary[items[0]] = "";
                    break;
                case 2: dictionary[items[0]] = items[1];
                    break;
                default:
                    break;
            }
            return dictionary;
        }
    }
}
